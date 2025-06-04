using System.Collections.Generic;

namespace lingualink_client.Models
{
    public class AppSettings
    {
        public string GlobalLanguage { get; set; } = Services.LanguageManager.DetectSystemLanguage();

        public string TargetLanguages { get; set; } = "英文,日文"; // Default: English, Japanese
        public string ServerUrl { get; set; } = "http://localhost:8080/api/v1/";

        // API Authentication Settings
        public string ApiKey { get; set; } = "";
        public bool AuthEnabled { get; } = true;

        // VAD Parameters (defaults from your AudioService)
        public double SilenceThresholdSeconds { get; set; } = 0.6;
        public double MinVoiceDurationSeconds { get; set; } = 0.5;
        public double MaxVoiceDurationSeconds { get; set; } = 10.0;
        public double MinRecordingVolumeThreshold { get; set; } = 0.05;

         // OSC Settings for VRChat
        public bool EnableOsc { get; set; } = true;
        public string OscIpAddress { get; set; } = "127.0.0.1";
        public int OscPort { get; set; } = 9000; // VRChat default input port
        public bool OscSendImmediately { get; set; } = true; // Corresponds to 'b' param in /chatbox/input
        public bool OscPlayNotificationSound { get; set; } = false; // Corresponds to 'n' param, false is less intrusive

        // Message Template Settings
        public string MessageTemplate { get; set; } = "{raw_text}"; // Default template
        public bool UseCustomTemplate { get; set; } = false; // Whether to use template system or raw text
        public string UserCustomTemplateText { get; set; } = "{英文}\n{日文}\n{中文}"; // User's custom template text that persists

        // Audio Encoding Settings
        public bool UseOpusEncoding { get; set; } = true; // 默认使用Opus编码以减少带宽
        public int OpusBitrate { get; set; } = 32000; // Opus比特率 (32kbps是语音的好选择)
        public int OpusComplexity { get; set; } = 5; // 编码复杂度 (0-10, 5是平衡选择)

        // Get the currently selected template
        public MessageTemplate GetSelectedTemplate()
        {
            if (UseCustomTemplate)
            {
                // Return user's custom template
                return new MessageTemplate("自定义模板", UserCustomTemplateText, "用户自定义模板");
            }
            
            // Default fallback when custom template is disabled
            return new MessageTemplate("完整文本", "{raw_text}", "显示服务器返回的完整原始文本");
        }
    }
}