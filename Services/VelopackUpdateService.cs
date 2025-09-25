using System;
using System.Threading;
using System.Threading.Tasks;
using lingualink_client.Models.Updates;
using lingualink_client.Services.Interfaces;
using NuGet.Versioning;
using Velopack;
using Velopack.Locators;

namespace lingualink_client.Services
{
    /// <summary>
    /// 基于 Velopack 的自动更新服务实现，只在 Windows x64 环境下启用。
    /// </summary>
    public sealed class VelopackUpdateService : IUpdateService
    {
        private readonly object _syncRoot = new();
        private readonly ILoggingManager? _loggingManager;
        private bool _disposed;

        public VelopackUpdateService()
        {
            if (ServiceContainer.TryResolve<ILoggingManager>(out var logger))
            {
                _loggingManager = logger;
            }

            FeedUrl = ResolveFeedUrl();
        }

        public bool IsSupported => OperatingSystem.IsWindows() && Environment.Is64BitOperatingSystem;

        public string? FeedUrl { get; private set; }

        public UpdateSession? ActiveSession { get; private set; }

        public SemanticVersion? GetInstalledVersion()
        {
            try
            {
                var locator = VelopackLocator.IsCurrentSet
                    ? VelopackLocator.Current
                    : VelopackLocator.CreateDefaultForPlatform(null);

                return locator?.CurrentlyInstalledVersion;
            }
            catch (Exception ex)
            {
                _loggingManager?.AddMessage($"[Update] Failed to read installed version: {ex.Message}");
                return null;
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelopackUpdateService));
            }

            var installedVersion = GetInstalledVersion();

            if (!IsSupported)
            {
                ReleaseActiveSession();
                return new UpdateCheckResult(false, installedVersion, null);
            }

            var feedUrl = FeedUrl;
            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                ReleaseActiveSession();
                return new UpdateCheckResult(false, installedVersion, null);
            }

            try
            {
                var updateManager = new UpdateManager(feedUrl);
                var updateInfo = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
                var currentVersion = updateManager.CurrentVersion ?? installedVersion;

                if (updateInfo is null)
                {
                    ReleaseActiveSession();
                    return new UpdateCheckResult(true, currentVersion, null);
                }

                var hasUpdate = ShouldApplyUpdate(updateInfo, currentVersion);

                if (!hasUpdate)
                {
                    ReleaseActiveSession();
                    return new UpdateCheckResult(true, currentVersion, null);
                }

                var session = new UpdateSession(updateManager, updateInfo, feedUrl, currentVersion, hasUpdate);
                ReplaceActiveSession(session);
                return new UpdateCheckResult(true, currentVersion, session);
            }
            catch (Exception ex)
            {
                _loggingManager?.AddMessage($"[Update] Check failed: {ex.Message}");
                ReleaseActiveSession();
                return new UpdateCheckResult(true, installedVersion, null, ex);
            }
        }

        public Task DownloadAsync(UpdateSession session, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelopackUpdateService));
            }

            return session.DownloadAsync(progress, cancellationToken);
        }

        public async Task ApplyAsync(UpdateSession session, bool restart, bool silent, CancellationToken cancellationToken = default)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelopackUpdateService));
            }

            await session.WaitExitThenApplyAsync(silent, restart).ConfigureAwait(false);
            ReleaseSession(session);
        }

        public void ReleaseSession(UpdateSession session)
        {
            if (session is null)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (ReferenceEquals(ActiveSession, session))
                {
                    ReleaseActiveSession();
                }
                else
                {
                    session.Dispose();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            ReleaseActiveSession();
            _disposed = true;
        }

        private void ReplaceActiveSession(UpdateSession session)
        {
            lock (_syncRoot)
            {
                var previous = ActiveSession;
                ActiveSession = session;
                previous?.Dispose();
            }
        }

        private void ReleaseActiveSession()
        {
            lock (_syncRoot)
            {
                ActiveSession?.Dispose();
                ActiveSession = null;
            }
        }

        private static bool ShouldApplyUpdate(UpdateInfo updateInfo, SemanticVersion? currentVersion)
        {
            var target = updateInfo.TargetFullRelease;
            if (target?.Version is null)
            {
                return false;
            }

            if (updateInfo.IsDowngrade)
            {
                return false;
            }

            if (currentVersion is null)
            {
                return true;
            }

            return target.Version > currentVersion;
        }

        private static string? ResolveFeedUrl()
        {
#if SELF_CONTAINED
            return EnsureTrailingSlash("https://download.cn-nb1.rains3.com/lingualink/stable-self-contained");
#elif FRAMEWORK_DEPENDENT
            return EnsureTrailingSlash("https://download.cn-nb1.rains3.com/lingualink/stable-framework-dependent");
#else
            return null;
#endif
        }

        private static string EnsureTrailingSlash(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
        }
    }
}




