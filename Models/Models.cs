// lingualink_client.Models.Models.cs
using NAudio.CoreAudioApi;
using System;

namespace lingualink_client.Models
{
    // MMDeviceWrapper remains the same

    public class MMDeviceWrapper
    {
        public string ID { get; }
        public string FriendlyName { get; }
        public int WaveInDeviceIndex { get; set; }

        public MMDeviceWrapper(MMDevice device, int waveInIndex)
        {
            ID = device.ID;
            FriendlyName = device.FriendlyName;
            WaveInDeviceIndex = waveInIndex;
        }

        public override string ToString() => FriendlyName;
    }

    public class ServerResponse
    {
        public string? Status { get; set; }
        public double Duration_Seconds { get; set; }
        public TranslationData? Data { get; set; } // This will hold our simplified TranslationData
        public string? Message { get; set; }
        public ErrorDetails? Details { get; set; }
    }

    public class TranslationData // Simplified or adjusted
    {
        // Primary field we are interested in now
        public string? Raw_Text { get; set; }

        // Optional: Keep these if your LLM might sometimes fill them,
        // or if you plan to parse raw_text into these fields later on the client.
        // If not, you can remove them for a cleaner model.
        public string? Original_Language { get; set; }
        public string? Original_Text { get; set; }
        public string? English_Translation { get; set; }
        public string? Japanese_Translation { get; set; }
    }

    public class ErrorDetails // Remains the same
    {
        public int Status_Code { get; set; }
        public string? Content { get; set; }
    }

    public class AudioSegmentEventArgs : EventArgs // Remains the same
    {
        public byte[] AudioData { get; }
        public string TriggerReason { get; }

        public AudioSegmentEventArgs(byte[] audioData, string triggerReason)
        {
            AudioData = audioData;
            TriggerReason = triggerReason;
        }
    }
}