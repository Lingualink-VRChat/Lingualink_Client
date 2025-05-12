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
        public double MinRecordingVolumeThreshold { get; set; } = 0.01;

         // OSC Settings for VRChat
        public bool EnableOsc { get; set; } = false;
        public string OscIpAddress { get; set; } = "127.0.0.1";
        public int OscPort { get; set; } = 9000; // VRChat default input port
        public bool OscSendImmediately { get; set; } = true; // Corresponds to 'b' param in /chatbox/input
        public bool OscPlayNotificationSound { get; set; } = false; // Corresponds to 'n' param, false is less intrusive
    }
}