using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;
using Velopack;

namespace lingualink_client.Models.Updates
{
    /// <summary>
    /// 表示一次 Velopack 更新检查的上下文，用于后续下载与应用。
    /// </summary>
    public sealed class UpdateSession : IDisposable
    {
        private readonly UpdateManager _updateManager;
        private bool _disposed;

        internal UpdateSession(UpdateManager updateManager, UpdateInfo updateInfo, string feedUrl, SemanticVersion? currentVersion, bool hasUpdate)
        {
            _updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));
            UpdateInfo = updateInfo ?? throw new ArgumentNullException(nameof(updateInfo));
            FeedUrl = feedUrl;
            CurrentVersion = currentVersion;
            HasUpdate = hasUpdate;
        }

        /// <summary>
        /// 本次检查得到的原始更新信息。
        /// </summary>
        public UpdateInfo UpdateInfo { get; }

        /// <summary>
        /// 更新源地址。
        /// </summary>
        public string FeedUrl { get; }

        /// <summary>
        /// 检查时客户端的已安装版本。
        /// </summary>
        public SemanticVersion? CurrentVersion { get; }

        /// <summary>
        /// 是否存在可供应用的更新。
        /// </summary>
        public bool HasUpdate { get; }

        /// <summary>
        /// 目标完整包描述。
        /// </summary>
        public VelopackAsset? TargetRelease => UpdateInfo.TargetFullRelease;

        /// <summary>
        /// 目标版本号。
        /// </summary>
        public SemanticVersion? TargetVersion => TargetRelease?.Version;

        /// <summary>
        /// 目标版本的 Markdown 格式发布说明。
        /// </summary>
        public string? ReleaseNotesMarkdown => TargetRelease?.NotesMarkdown;

        /// <summary>
        /// 目标版本的 HTML 格式发布说明。
        /// </summary>
        public string? ReleaseNotesHtml => TargetRelease?.NotesHTML;

        /// <summary>
        /// 增量补丁列表。
        /// </summary>
        public VelopackAsset[]? DeltaReleases => UpdateInfo.DeltasToTarget;

        internal UpdateManager Manager => _updateManager;

        public Task DownloadAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _updateManager.DownloadUpdatesAsync(UpdateInfo, progress == null ? null : new Action<int>(progress.Report), cancellationToken);
        }

        public Task WaitExitThenApplyAsync(bool silent, bool restart)
        {
            EnsureNotDisposed();
            if (TargetRelease is null)
            {
                throw new InvalidOperationException("Target release is not available for this session.");
            }

            return _updateManager.WaitExitThenApplyUpdatesAsync(TargetRelease, silent, restart);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_updateManager is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            else if (_updateManager is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UpdateSession));
            }
        }
    }
}





