using System;
using System.Threading;
using System.Threading.Tasks;
using lingualink_client.Models.Updates;
using NuGet.Versioning;

namespace lingualink_client.Services.Interfaces
{
    /// <summary>
    /// 对客户端自动更新流程的统一抽象。
    /// </summary>
    public interface IUpdateService : IDisposable
    {
        /// <summary>
        /// 当前运行环境是否支持自动更新。
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// 当前使用的更新源地址。
        /// </summary>
        string? FeedUrl { get; }

        /// <summary>
        /// 最近一次检查后暂存的更新会话。
        /// </summary>
        UpdateSession? ActiveSession { get; }

        /// <summary>
        /// 读取当前已安装的版本号。
        /// </summary>
        SemanticVersion? GetInstalledVersion();

        /// <summary>
        /// 检查更新并返回检查结果。
        /// </summary>
        Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 下载并缓存更新包。
        /// </summary>
        Task DownloadAsync(UpdateSession session, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 在客户端退出后应用更新。
        /// </summary>
        Task ApplyAsync(UpdateSession session, bool restart, bool silent, CancellationToken cancellationToken = default);

        /// <summary>
        /// 主动释放当前会话。
        /// </summary>
        void ReleaseSession(UpdateSession session);
    }
}
