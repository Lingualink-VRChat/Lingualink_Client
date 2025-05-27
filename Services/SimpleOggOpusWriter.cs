using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace lingualink_client.Services
{
    /// <summary>
    /// 简化的 OGG Opus 文件写入器
    /// 专门用于生成标准的 OGG 容器格式，确保 FFmpeg 可以正确解析
    /// </summary>
    public static class SimpleOggOpusWriter
    {
        /// <summary>
        /// 将 Opus 音频帧封装到 OGG 容器中
        /// </summary>
        /// <param name="opusFrames">Opus 编码的音频帧</param>
        /// <param name="sampleRate">采样率</param>
        /// <param name="channels">声道数</param>
        /// <returns>OGG 格式的音频数据</returns>
        public static byte[] CreateOggOpusFile(List<byte[]> opusFrames, int sampleRate, int channels)
        {
            using var stream = new MemoryStream();
            const uint serialNumber = 0x12345678; // Use consistent serial number for all pages
            
            // 1. 写入 OpusHead 页面 (BOS - Beginning of Stream)
            var opusHead = CreateOpusHeadPacket(sampleRate, channels);
            WriteOggPage(stream, opusHead, 0, 0, 0x02, serialNumber); // BOS flag
            
            // 2. 写入 OpusTags 页面
            var opusTags = CreateOpusTagsPacket();
            WriteOggPage(stream, opusTags, 1, 0, 0x00, serialNumber);
            
            // 3. 写入音频数据页面
            // For Opus, granule position represents the total number of 48kHz samples
            // that would be decoded (including pre-skip)
            uint granulePosition = 312; // Start with pre-skip value
            uint sequenceNumber = 2;
            
            for (int i = 0; i < opusFrames.Count; i++)
            {
                // Each Opus frame represents 20ms of audio at 48kHz = 960 samples
                // Regardless of the input sample rate, Opus internally works at 48kHz
                granulePosition += 960; // 20ms at 48kHz
                
                byte headerType = 0x00;
                if (i == opusFrames.Count - 1)
                {
                    headerType = 0x04; // EOS flag for last page
                }
                
                WriteOggPage(stream, opusFrames[i], sequenceNumber++, granulePosition, headerType, serialNumber);
            }
            
            return stream.ToArray();
        }

        /// <summary>
        /// 创建 OpusHead 数据包
        /// </summary>
        private static byte[] CreateOpusHeadPacket(int sampleRate, int channels)
        {
            var packet = new List<byte>();
            
            // Magic signature "OpusHead"
            packet.AddRange(Encoding.ASCII.GetBytes("OpusHead"));
            
            // Version
            packet.Add(1);
            
            // Channel count
            packet.Add((byte)channels);
            
            // Pre-skip (little endian)
            packet.AddRange(BitConverter.GetBytes((ushort)312));
            
            // Input sample rate (little endian)
            packet.AddRange(BitConverter.GetBytes((uint)sampleRate));
            
            // Output gain (little endian)
            packet.AddRange(BitConverter.GetBytes((ushort)0));
            
            // Mapping family
            packet.Add(0);
            
            return packet.ToArray();
        }

        /// <summary>
        /// 创建 OpusTags 数据包
        /// </summary>
        private static byte[] CreateOpusTagsPacket()
        {
            var packet = new List<byte>();
            
            // Magic signature "OpusTags"
            packet.AddRange(Encoding.ASCII.GetBytes("OpusTags"));
            
            // Vendor string
            string vendor = "LinguaLink";
            byte[] vendorBytes = Encoding.UTF8.GetBytes(vendor);
            packet.AddRange(BitConverter.GetBytes((uint)vendorBytes.Length));
            packet.AddRange(vendorBytes);
            
            // User comment list count
            packet.AddRange(BitConverter.GetBytes((uint)0));
            
            return packet.ToArray();
        }

        /// <summary>
        /// 写入 OGG 页面
        /// </summary>
        private static void WriteOggPage(Stream stream, byte[] data, uint sequenceNumber, uint granulePosition, byte headerType, uint serialNumber)
        {
            // OGG 页面头部
            var header = new List<byte>();
            
            // Capture pattern "OggS"
            header.AddRange(Encoding.ASCII.GetBytes("OggS"));
            
            // Stream structure version
            header.Add(0);
            
            // Header type flag
            header.Add(headerType);
            
            // Granule position (8 bytes, little endian)
            header.AddRange(BitConverter.GetBytes(granulePosition));
            header.AddRange(BitConverter.GetBytes((uint)0)); // High 32 bits
            
            // Bitstream serial number
            header.AddRange(BitConverter.GetBytes(serialNumber));
            
            // Page sequence number
            header.AddRange(BitConverter.GetBytes(sequenceNumber));
            
            // CRC checksum (placeholder, will be calculated)
            int crcPosition = header.Count;
            header.AddRange(BitConverter.GetBytes((uint)0));
            
            // Number of page segments and segment table
            if (data.Length == 0)
            {
                header.Add(0);
            }
            else
            {
                // Build segment table - each segment can be max 255 bytes
                var segments = new List<byte>();
                int remaining = data.Length;
                while (remaining > 0)
                {
                    int segmentSize = Math.Min(255, remaining);
                    segments.Add((byte)segmentSize);
                    remaining -= segmentSize;
                }
                
                header.Add((byte)segments.Count);
                header.AddRange(segments);
            }
            
            // 组合完整页面
            var fullPage = new List<byte>();
            fullPage.AddRange(header);
            fullPage.AddRange(data);
            
            // 计算 CRC
            uint crc = CalculateCRC(fullPage.ToArray());
            byte[] crcBytes = BitConverter.GetBytes(crc);
            for (int i = 0; i < 4; i++)
            {
                fullPage[crcPosition + i] = crcBytes[i];
            }
            
            // 写入流
            stream.Write(fullPage.ToArray(), 0, fullPage.Count);
        }

        /// <summary>
        /// 计算 OGG CRC-32
        /// </summary>
        private static uint CalculateCRC(byte[] data)
        {
            uint[] table = new uint[256];
            
            // 初始化 CRC 表
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i << 24;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;
                }
                table[i] = crc;
            }
            
            uint result = 0;
            for (int i = 0; i < data.Length; i++)
            {
                result = (result << 8) ^ table[((result >> 24) ^ data[i]) & 0xFF];
            }
            
            return result;
        }
    }
}
