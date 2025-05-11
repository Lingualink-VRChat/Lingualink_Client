namespace lingualink_client.Models
{
    public class AppSettings
    {
        public string TargetLanguages { get; set; } = "英文,日文"; // Default: English, Japanese
        public string ServerUrl { get; set; } = "http://localhost:5000/translate_audio";

        // VAD Parameters (defaults from your AudioService)
        public double SilenceThresholdSeconds { get; set; } = 0.8;
        public double MinVoiceDurationSeconds { get; set; } = 1.3;
        public double MaxVoiceDurationSeconds { get; set; } = 18.0;
    }
}