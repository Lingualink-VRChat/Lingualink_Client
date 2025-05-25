using lingualink_client.Models;

namespace lingualink_client.ViewModels.Events
{
    /// <summary>
    /// 工作开始事件
    /// </summary>
    public class WorkStartedEvent
    {
        public int MicrophoneIndex { get; set; }
        public string MicrophoneName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 工作停止事件
    /// </summary>
    public class WorkStoppedEvent
    {
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 音频状态更新事件
    /// </summary>
    public class AudioStatusUpdatedEvent
    {
        public string Status { get; set; } = string.Empty;
        public bool IsWorking { get; set; }
    }

    /// <summary>
    /// 翻译完成事件
    /// </summary>
    public class TranslationCompletedEvent
    {
        public string TriggerReason { get; set; } = string.Empty;
        public string OriginalText { get; set; } = string.Empty;
        public string ProcessedText { get; set; } = string.Empty;
        public ServerResponse? Response { get; set; }
        public string? ErrorMessage { get; set; }
        public double Duration { get; set; }
    }

    /// <summary>
    /// OSC消息发送事件
    /// </summary>
    public class OscMessageSentEvent
    {
        public string Message { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 设置变更事件
    /// </summary>
    public class SettingsChangedEvent
    {
        public AppSettings Settings { get; set; } = null!;
        public string ChangeSource { get; set; } = string.Empty;
    }

    /// <summary>
    /// 麦克风变更事件
    /// </summary>
    public class MicrophoneChangedEvent
    {
        public MMDeviceWrapper? SelectedMicrophone { get; set; }
        public bool IsRefreshing { get; set; }
    }

    /// <summary>
    /// 目标语言变更事件
    /// </summary>
    public class TargetLanguagesChangedEvent
    {
        public string[] Languages { get; set; } = Array.Empty<string>();
        public bool IsTemplateMode { get; set; }
        public string Source { get; set; } = string.Empty;
    }
} 