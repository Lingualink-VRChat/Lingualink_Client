using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Concentus.Structs;
using Concentus.Enums;
using NAudio.Wave;

namespace lingualink_client.Services
{
    /// <summary>
    /// 音频编码器服务 - 支持将PCM音频数据编码为OGG容器中的Opus格式以减少带宽消耗
    /// </summary>
    public class AudioEncoderService : IDisposable
    {
        private OpusEncoder? _opusEncoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _bitrate;
        private readonly int _complexity;
        private bool _disposed = false;

        public AudioEncoderService(int sampleRate = 16000, int channels = 1, int bitrate = 32000, int complexity = 5)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _bitrate = bitrate;
            _complexity = complexity;
            
            InitializeOpusEncoder();
        }

        private void InitializeOpusEncoder()
        {
            try
            {
                // 使用正确的枚举类型
                #pragma warning disable CS0618 // Type or member is obsolete
                _opusEncoder = new OpusEncoder(_sampleRate, _channels, OpusApplication.OPUS_APPLICATION_VOIP);
                #pragma warning restore CS0618 // Type or member is obsolete
                _opusEncoder.Bitrate = _bitrate;
                _opusEncoder.Complexity = _complexity;
                _opusEncoder.UseVBR = true;
                _opusEncoder.UseDTX = true; // 启用DTX(Discontinuous Transmission)以节省带宽
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize Opus encoder: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将PCM音频数据编码为OGG容器中的Opus格式
        /// </summary>
        /// <param name="pcmData">PCM音频数据（16位）</param>
        /// <param name="waveFormat">音频格式信息</param>
        /// <returns>编码后的OGG/Opus数据</returns>
        public byte[] EncodePcmToOpus(byte[] pcmData, WaveFormat waveFormat)
        {
            if (_opusEncoder == null)
                throw new ObjectDisposedException(nameof(AudioEncoderService));

            if (pcmData == null || pcmData.Length == 0)
                return Array.Empty<byte>();

            // 验证音频格式
            if (waveFormat.SampleRate != _sampleRate || waveFormat.Channels != _channels)
            {
                throw new ArgumentException($"Audio format mismatch. Expected {_sampleRate}Hz {_channels}ch, got {waveFormat.SampleRate}Hz {waveFormat.Channels}ch");
            }

            try
            {
                // 将字节数组转换为short数组（16位PCM）
                short[] samples = new short[pcmData.Length / 2];
                Buffer.BlockCopy(pcmData, 0, samples, 0, pcmData.Length);

                // Opus要求固定大小的帧，计算合适的帧大小
                // 对于16kHz单声道，20ms = 320 samples
                int frameSize = _sampleRate * 20 / 1000 * _channels; // 20ms frames
                
                // 收集所有编码后的 Opus 帧
                var opusFrames = new List<byte[]>();
                
                // 按帧编码音频数据
                for (int i = 0; i < samples.Length; i += frameSize)
                {
                    int currentFrameSize = Math.Min(frameSize, samples.Length - i);
                    
                    // 如果不是完整帧，用零填充
                    short[] frame = new short[frameSize];
                    Array.Copy(samples, i, frame, 0, currentFrameSize);
                    
                    // 编码这一帧
                    byte[] outputBuffer = new byte[4000]; // Opus推荐的最大缓冲区大小
                    
                    #pragma warning disable CS0618 // Type or member is obsolete
                    int encodedLength = _opusEncoder.Encode(frame, 0, frameSize, outputBuffer, 0, outputBuffer.Length);
                    #pragma warning restore CS0618 // Type or member is obsolete
                    
                    if (encodedLength > 0)
                    {
                        byte[] frameData = new byte[encodedLength];
                        Array.Copy(outputBuffer, 0, frameData, 0, encodedLength);
                        opusFrames.Add(frameData);
                    }
                }
                
                // 使用简化的 OGG 写入器创建标准 OGG 文件
                return SimpleOggOpusWriter.CreateOggOpusFile(opusFrames, _sampleRate, _channels);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to encode PCM to OGG/Opus: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建包含OGG/Opus音频数据的文件
        /// </summary>
        /// <param name="opusData">OGG/Opus编码的音频数据</param>
        /// <param name="tempFilePath">临时文件路径</param>
        /// <returns>OGG文件路径</returns>
        public async Task<string> CreateOpusFileAsync(byte[] opusData, string tempFilePath)
        {
            if (opusData == null || opusData.Length == 0)
                throw new ArgumentException("OGG/Opus data cannot be null or empty");

            string oggFilePath = Path.ChangeExtension(tempFilePath, ".ogg");
            
            try
            {
                // 直接写入OGG/Opus数据
                await File.WriteAllBytesAsync(oggFilePath, opusData);
                return oggFilePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create OGG/Opus file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 估算压缩比率
        /// </summary>
        /// <param name="originalSize">原始PCM数据大小</param>
        /// <param name="compressedSize">压缩后Opus数据大小</param>
        /// <returns>压缩比率百分比</returns>
        public static double CalculateCompressionRatio(int originalSize, int compressedSize)
        {
            if (originalSize == 0) return 0;
            return (1.0 - (double)compressedSize / originalSize) * 100.0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _opusEncoder?.Dispose();
                _opusEncoder = null;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
} 