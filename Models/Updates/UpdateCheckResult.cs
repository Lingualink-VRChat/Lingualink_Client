using System;
using NuGet.Versioning;

namespace lingualink_client.Models.Updates
{
    /// <summary>
    /// 表示一次更新检查的结果。
    /// </summary>
    public sealed class UpdateCheckResult
    {
        public UpdateCheckResult(bool isSupported, SemanticVersion? installedVersion, UpdateSession? session, Exception? error = null)
        {
            IsSupported = isSupported;
            InstalledVersion = installedVersion;
            Session = session;
            Error = error;
        }

        /// <summary>
        /// 是否支持自动更新。
        /// </summary>
        public bool IsSupported { get; }

        /// <summary>
        /// 检查时客户端的安装版本。
        /// </summary>
        public SemanticVersion? InstalledVersion { get; }

        /// <summary>
        /// 如果存在可更新内容，包含会话上下文。
        /// </summary>
        public UpdateSession? Session { get; }

        /// <summary>
        /// 检查过程中出现的异常。
        /// </summary>
        public Exception? Error { get; }

        /// <summary>
        /// 是否有新版本。
        /// </summary>
        public bool HasUpdate => Session?.HasUpdate == true;

        /// <summary>
        /// 目标版本号字符串。
        /// </summary>
        public string? TargetVersion => Session?.TargetVersion?.ToString();

        /// <summary>
        /// 发布说明（Markdown）。
        /// </summary>
        public string? ReleaseNotesMarkdown => Session?.ReleaseNotesMarkdown;
    }
}
