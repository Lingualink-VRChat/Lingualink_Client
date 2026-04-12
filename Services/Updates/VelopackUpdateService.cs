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
        private readonly SettingsService _settingsService;
        private bool _disposed;

        public VelopackUpdateService(SettingsService? settingsService = null, ILoggingManager? loggingManager = null)
        {
            _settingsService = settingsService ?? new SettingsService();
            _loggingManager = loggingManager;
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

        public string? FeedUrl => ResolveFeedUrl();

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

        private string? ResolveFeedUrl()
        {
            var overrideUrl = ResolveFeedOverride();
#if SELF_CONTAINED
            return UpdateFeedResolver.Resolve(overrideUrl, UpdateFeedChannel.SelfContained);
#elif FRAMEWORK_DEPENDENT
            return UpdateFeedResolver.Resolve(overrideUrl, UpdateFeedChannel.FrameworkDependent);
#else
            return UpdateFeedResolver.Resolve(overrideUrl, UpdateFeedChannel.None);
#endif
        }

        private string? ResolveFeedOverride()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                return string.IsNullOrWhiteSpace(settings.UpdateFeedOverride)
                    ? null
                    : settings.UpdateFeedOverride.Trim();
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to read update feed override: {ex.Message}");
                return null;
            }
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


