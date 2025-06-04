using System.Collections.Generic;

namespace lingualink_client.Models
{
    public class AppSettings
    {
        public string GlobalLanguage { get; set; } = Services.LanguageManager.DetectSystemLanguage();

        public string TargetLanguages { get; set; } = "英文,日文"; // Default: English, Japanese
        public string ServerUrl { get; set; } = "http://api2.lingualink.aiatechco.com/api/v1/";

        // API Authentication Settings
        public string ApiKey { get; set; } = "";
        public bool AuthEnabled { get; } = true;

        // VAD Parameters (defaults from your AudioService)
        public double PostSpeechRecordingDurationSeconds { get; set; } = 0.5; // 追加录音时长，用于捕捉尾音
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

        // Audio Encoding Settings - Opus is always enabled with fixed 16kbps bitrate
        public int OpusComplexity { get; set; } = 7; // 编码复杂度 (5-10, 7是默认选择，更高压缩率)

        // Audio Enhancement Settings - 音频增强设置
        public bool EnableAudioNormalization { get; set; } = true; // 是否启用峰值归一化
        public double NormalizationTargetDb { get; set; } = -3.0; // 峰值归一化目标电平 (dBFS)
        public bool EnableQuietBoost { get; set; } = true; // 是否启用安静语音增强
        public double QuietBoostRmsThresholdDbFs { get; set; } = -25.0; // RMS阈值，低于此值的片段将被增强 (dBFS)
        public double QuietBoostGainDb { get; set; } = 6.0; // 对安静片段应用的增益量 (dB)

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