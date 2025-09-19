using System;
using System.Collections.Generic;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.ViewModels.Events
{
    public enum TranslationSource
    {
        Unknown,
        Audio,
        Text
    }

    /// <summary>
    /// 翻译完成事件
    /// </summary>
    public class TranslationCompletedEvent
    {
        public string TriggerReason { get; set; } = string.Empty;
        public string OriginalText { get; set; } = string.Empty;
        public string ProcessedText { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public double Duration { get; set; }
        public bool IsSuccess { get; set; }
        public TranslationSource Source { get; set; } = TranslationSource.Unknown;
        public List<string> TargetLanguages { get; set; } = new();
        public Dictionary<string, string> Translations { get; set; } = new();
        public string Task { get; set; } = string.Empty;
        public string? RequestId { get; set; }
        public ApiMetadata? Metadata { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
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
    /// 麦克风刷新状态变更事件
    /// </summary>
    public class MicrophoneRefreshingStateChangedEvent
    {
        public bool IsRefreshing { get; set; }
    }

    /// <summary>
    /// 麦克风启用状态变更事件
    /// </summary>
    public class MicrophoneEnabledStateChangedEvent
    {
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// 状态更新事件
    /// </summary>
    public class StatusUpdatedEvent
    {
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// OSC消息发送事件
    /// </summary>
    public class OscMessageSentEvent
    {
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// VAD状态变化事件
    /// </summary>
    public class VadStateChangedEvent
    {
        public VadState State { get; set; }
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

    /// <summary>
    /// 语言列表初始化完成事件
    /// 让事件携带加载完成的语言列表
    /// </summary>
    public class LanguagesInitializedEvent
    {
        /// <summary>
        /// 支持的语言列表
        /// </summary>
        public List<string> SupportedLanguages { get; }

        public LanguagesInitializedEvent(List<string> supportedLanguages)
        {
            SupportedLanguages = supportedLanguages;
        }
    }
}
