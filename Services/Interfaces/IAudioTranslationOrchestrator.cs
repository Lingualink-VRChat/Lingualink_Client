using System;
using lingualink_client.Models;

namespace lingualink_client.Services.Interfaces
{
    /// <summary>
    /// 音频翻译协调器接口 - 负责协调音频处理和翻译的完整流程
    /// </summary>
    public interface IAudioTranslationOrchestrator : IDisposable
    {
        /// <summary>
        /// 是否正在工作
        /// </summary>
        bool IsWorking { get; }

        /// <summary>
        /// 开始音频翻译工作
        /// </summary>
        /// <param name="microphoneIndex">麦克风设备索引</param>
        /// <returns>是否成功启动</returns>
        bool Start(int microphoneIndex);

        /// <summary>
        /// 停止音频翻译工作
        /// </summary>
        void Stop();

        /// <summary>
        /// 状态更新事件
        /// </summary>
        event EventHandler<string> StatusUpdated;

        /// <summary>
        /// 翻译完成事件
        /// </summary>
        event EventHandler<TranslationResultEventArgs> TranslationCompleted;

        /// <summary>
        /// OSC消息发送事件
        /// </summary>
        event EventHandler<OscMessageEventArgs> OscMessageSent;
    }

    /// <summary>
    /// 翻译结果事件参数
    /// </summary>
    public class TranslationResultEventArgs : EventArgs
    {
        public string OriginalText { get; set; } = string.Empty;
        public string ProcessedText { get; set; } = string.Empty;
        public string TriggerReason { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public double? DurationSeconds { get; set; }
    }

    /// <summary>
    /// OSC消息事件参数
    /// </summary>
    public class OscMessageEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
} 