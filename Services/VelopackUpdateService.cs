using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using lingualink_client.Models;
using lingualink_client.Models.Updates;
using lingualink_client.Services.Interfaces;
using NuGet.Versioning;
using Velopack;
using Velopack.Locators;

namespace lingualink_client.Services
{
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

        public bool IsSupported
        {
            get
            {
                if (!OperatingSystem.IsWindows() || !Environment.Is64BitOperatingSystem)
                {
                    return false;
                }

                if (!VelopackLocator.IsCurrentSet)
                {
                    return false;
                }

                return VelopackLocator.Current?.CurrentlyInstalledVersion is not null;
            }
        }

        public string? FeedUrl { get; }

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
                LogWarning($"Failed to read installed version: {ex.Message}");
                return null;
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var installedVersion = GetInstalledVersion();

            if (!IsSupported)
            {
                ReleaseActiveSession();
                LogInfo("Update check skipped: current process is not running from a packaged install.");
                return new UpdateCheckResult(false, installedVersion, null);
            }

            if (string.IsNullOrWhiteSpace(FeedUrl))
            {
                ReleaseActiveSession();
                LogInfo("Update check skipped: update feed is not configured for this build.");
                return new UpdateCheckResult(false, installedVersion, null);
            }

            UpdateManager? manager = null;

            try
            {
                manager = new UpdateManager(FeedUrl);
                LogInfo($"Checking for updates at {FeedUrl}");

                var info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
                var currentVersion = manager.CurrentVersion ?? installedVersion;

                if (!HasApplicableUpdate(info, currentVersion))
                {
                    
                    manager = null;
                    ReleaseActiveSession();
                    LogInfo("No updates available.");
                    return new UpdateCheckResult(true, currentVersion, null);
                }

                var session = new UpdateSession(manager, info!, FeedUrl, currentVersion, hasUpdate: true);
                manager = null; // ownership transferred to the session
                ReplaceActiveSession(session);

                LogInfo($"Update available: {session.TargetVersion}");
                return new UpdateCheckResult(true, currentVersion, session);
            }
            catch (OperationCanceledException)
            {
                ReleaseActiveSession();
                throw;
            }
            catch (Exception ex)
            {
                ReleaseActiveSession();
                LogError("Failed to check for updates.", ex);
                return new UpdateCheckResult(true, installedVersion, null, ex);
            }
        }

        public async Task DownloadAsync(UpdateSession session, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            cancellationToken.ThrowIfCancellationRequested();

            LogInfo($"Downloading update {session.TargetVersion}");
            await session.DownloadAsync(progress, cancellationToken).ConfigureAwait(false);
            LogInfo("Download completed.");
        }

        public async Task ApplyAsync(UpdateSession session, bool restart, bool silent, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                LogInfo("Scheduling update to be applied on exit.");
                await session.WaitExitThenApplyAsync(silent, restart).ConfigureAwait(false);
                LogInfo("Update scheduled successfully.");
            }
            finally
            {
                ReleaseSession(session);
            }
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

        private static bool HasApplicableUpdate(UpdateInfo? info, SemanticVersion? currentVersion)
        {
            if (info?.TargetFullRelease?.Version is null)
            {
                return false;
            }

            if (info.IsDowngrade)
            {
                return false;
            }

            return currentVersion is null || info.TargetFullRelease.Version > currentVersion;
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

        private void LogInfo(string message)
        {
            _loggingManager?.AddMessage(message, LogLevel.Info, "Update");
            Debug.WriteLine($"[Update] {message}");
        }

        private void LogWarning(string message)
        {
            _loggingManager?.AddMessage(message, LogLevel.Warning, "Update");
            Debug.WriteLine($"[Update] WARNING: {message}");
        }

        private void LogError(string message, Exception exception)
        {
            _loggingManager?.AddMessage(message, LogLevel.Error, "Update", exception.ToString());
            Debug.WriteLine($"[Update] ERROR: {message} -> {exception}");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelopackUpdateService));
            }
        }
    }
}


