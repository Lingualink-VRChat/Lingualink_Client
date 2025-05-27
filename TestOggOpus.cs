using System;
using System.IO;
using System.Threading.Tasks;
using lingualink_client.Services;
using NAudio.Wave;

namespace lingualink_client
{
    public class TestOggOpus
    {
        public static async Task<bool> TestOggOpusGeneration()
        {
            try
            {
                Console.WriteLine("Testing OGG/OPUS file generation...");
                
                // Create a simple test PCM audio data (silence)
                int sampleRate = 16000; // Use the default sample rate for AudioEncoderService
                int channels = 1;
                int durationSeconds = 2;
                int totalSamples = sampleRate * durationSeconds * channels;
                
                byte[] pcmData = new byte[totalSamples * 2]; // 16-bit samples
                // Fill with silence (zeros are already there)
                
                // Create WaveFormat
                var waveFormat = new WaveFormat(sampleRate, 16, channels);
                
                // Test our encoder
                var audioEncoder = new AudioEncoderService(sampleRate, channels);
                byte[] opusData = audioEncoder.EncodePcmToOpus(pcmData, waveFormat);
                
                if (opusData == null || opusData.Length == 0)
                {
                    Console.WriteLine("❌ Failed: No OPUS data generated");
                    return false;
                }
                
                Console.WriteLine($"✅ Generated {opusData.Length} bytes of OGG/OPUS data");
                
                // Check if it starts with OGG signature
                if (opusData.Length >= 4 && 
                    opusData[0] == 0x4F && opusData[1] == 0x67 && 
                    opusData[2] == 0x67 && opusData[3] == 0x53)
                {
                    Console.WriteLine("✅ OGG container signature found");
                    
                    // Save test file
                    string testFile = Path.Combine(Path.GetTempPath(), "test_output.ogg");
                    await File.WriteAllBytesAsync(testFile, opusData);
                    Console.WriteLine($"✅ Test file saved: {testFile}");
                    
                    return true;
                }
                else
                {
                    Console.WriteLine("❌ Failed: No OGG signature found");
                    Console.WriteLine($"First 4 bytes: {opusData[0]:X2} {opusData[1]:X2} {opusData[2]:X2} {opusData[3]:X2}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
                return false;
            }
        }
    }
}
