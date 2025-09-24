using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using Velopack;

namespace lingualink_client.ViewModels
{
    public partial class SettingPageViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;

        private UpdateManager? _updateManager;
        private UpdateInfo? _pendingUpdate;
        private UpdateStatus _latestStatus = UpdateStatus.NotChecked;

        public string PageTitle => LanguageManager.GetString("GeneralSettings");
        public string InterfaceLanguage => LanguageManager.GetString("InterfaceLanguage");
        public string LanguageHint => LanguageManager.GetString("LanguageHint");
        public string UpdateSectionTitle => LanguageManager.GetString("UpdateSettings");
        public string CurrentVersionLabel => LanguageManager.GetString("UpdateCurrentVersion");
        public string LatestVersionLabel => LanguageManager.GetString("UpdateLatestVersion");
        public string CheckForUpdatesLabel => LanguageManager.GetString("UpdateCheckButton");
        public string DownloadAndUpdateLabel => LanguageManager.GetString("UpdateDownloadButton");
        public string DownloadProgressLabel => LanguageManager.GetString("UpdateDownloadProgress");

        [ObservableProperty]
        private string currentVersion = string.Empty;

        [ObservableProperty]
        private string latestVersion = string.Empty;

        [ObservableProperty]
        private bool isCheckingUpdate;

        [ObservableProperty]
        private bool hasUpdate;

        [ObservableProperty]
        private double downloadProgress;

        [ObservableProperty]
        private bool isDownloading;

        [ObservableProperty]
        private string updateNotes = string.Empty;

        public SettingPageViewModel()
        {
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();

            CurrentVersion = ResolveCurrentVersion();
            RefreshLatestVersionText();

            LanguageManager.LanguageChanged += () =>
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(InterfaceLanguage));
                OnPropertyChanged(nameof(LanguageHint));
                OnPropertyChanged(nameof(UpdateSectionTitle));
                OnPropertyChanged(nameof(CurrentVersionLabel));
                OnPropertyChanged(nameof(LatestVersionLabel));
                OnPropertyChanged(nameof(CheckForUpdatesLabel));
                OnPropertyChanged(nameof(DownloadAndUpdateLabel));
                OnPropertyChanged(nameof(DownloadProgressLabel));
                RefreshLatestVersionText();
            };
        }

        [RelayCommand(CanExecute = nameof(CanCheckForUpdate))]
        private async Task CheckForUpdateAsync()
        {
            if (IsCheckingUpdate || IsDownloading)
            {
                return;
            }

            var updateUrl = GetUpdateFeedUrl();
            if (string.IsNullOrWhiteSpace(updateUrl))
            {
                _latestStatus = UpdateStatus.Disabled;
                HasUpdate = false;
                RefreshLatestVersionText();
                return;
            }

            try
            {
                IsCheckingUpdate = true;
                _latestStatus = UpdateStatus.Checking;
                RefreshLatestVersionText();

                DisposeUpdateManager();
                _updateManager = new UpdateManager(updateUrl);

                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                _pendingUpdate = updateInfo;

#pragma warning disable CS8602
                var baseVersion = updateInfo.BaseRelease?.Version?.ToString();
                var targetVersion = updateInfo.TargetFullRelease?.Version?.ToString();
                var isDowngrade = updateInfo.IsDowngrade;
                var hasTarget = !string.IsNullOrWhiteSpace(targetVersion);
                var isNewer = hasTarget && !string.Equals(baseVersion, targetVersion, StringComparison.OrdinalIgnoreCase) && !isDowngrade;

                if (isNewer && targetVersion is { Length: > 0 } resolvedTarget)
                {
                    _latestStatus = UpdateStatus.UpdateAvailable;
                    HasUpdate = true;
                    LatestVersion = resolvedTarget;
                    UpdateNotes = ExtractReleaseNotes(updateInfo);
                }
                else
                {
                    _latestStatus = UpdateStatus.UpToDate;
                    HasUpdate = false;
                    UpdateNotes = string.Empty;
                    RefreshLatestVersionText();
                }
#pragma warning restore CS8602
            }
            catch (Exception ex)
            {
                _latestStatus = UpdateStatus.Failed;
                HasUpdate = false;
                UpdateNotes = string.Empty;
                RefreshLatestVersionText();

                ShowError(LanguageManager.GetString("UpdateErrorCheck"), ex);
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
        private async Task DownloadAndUpdateAsync()
        {
            if (_updateManager is null || _pendingUpdate is null)
            {
                return;
            }

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;

                await _updateManager.DownloadUpdatesAsync(_pendingUpdate, progress =>
                {
                    Application.Current.Dispatcher.Invoke(() => DownloadProgress = progress);
                });

                var prompt = LanguageManager.GetString("UpdateDialogDownloadPrompt");
                var title = LanguageManager.GetString("UpdateReadyTitle");
                var result = WpfMessageBox.Show(prompt, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _updateManager.WaitExitThenApplyUpdatesAsync(_pendingUpdate, silent: false, restart: true);
                    HasUpdate = false;
                    _latestStatus = UpdateStatus.UpToDate;
                    RefreshLatestVersionText();
                }
            }
            catch (Exception ex)
            {
                ShowError(LanguageManager.GetString("UpdateErrorDownload"), ex);
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        public void RefreshSettings()
        {
            _appSettings = _settingsService.LoadSettings();
            CurrentVersion = ResolveCurrentVersion();
        }

        private bool CanCheckForUpdate() => !IsCheckingUpdate && !IsDownloading;

        private bool CanDownloadUpdate() => HasUpdate && !IsDownloading && !IsCheckingUpdate;

        private void RefreshLatestVersionText()
        {
            switch (_latestStatus)
            {
                case UpdateStatus.NotChecked:
                    LatestVersion = LanguageManager.GetString("UpdateStatusNotChecked");
                    break;
                case UpdateStatus.Checking:
                    LatestVersion = LanguageManager.GetString("UpdateStatusChecking");
                    break;
                case UpdateStatus.UpToDate:
                    LatestVersion = LanguageManager.GetString("UpdateStatusUpToDate");
                    break;
                case UpdateStatus.Failed:
                    LatestVersion = LanguageManager.GetString("UpdateStatusFailed");
                    break;
                case UpdateStatus.Disabled:
                    LatestVersion = LanguageManager.GetString("UpdateStatusUnavailable");
                    break;
                case UpdateStatus.UpdateAvailable:
                    if (_pendingUpdate is not null)
                    {
                        LatestVersion = _pendingUpdate.TargetFullRelease.Version.ToString();
                    }
                    break;
            }
        }

        private static string ExtractReleaseNotes(UpdateInfo info)
        {
            var notes = info.TargetFullRelease.NotesMarkdown;
            if (string.IsNullOrWhiteSpace(notes))
            {
                return LanguageManager.GetString("UpdateNotesUnavailable");
            }

            return notes.Trim();
        }

        private static void ShowError(string message, Exception exception)
        {
            var errorTitle = LanguageManager.GetString("UpdateErrorTitle");
            WpfMessageBox.Show($"{message}: {exception.Message}", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static string ResolveCurrentVersion()
        {
            try
            {
#if SELF_CONTAINED || FRAMEWORK_DEPENDENT
                var veloApp = VelopackApp.Current;
                if (veloApp?.Version is not null)
                {
                    return veloApp.Version.ToString();
                }
#endif
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var version = assembly?.GetName().Version;
                return version?.ToString() ?? LanguageManager.GetString("UpdateVersionUnknown");
            }
            catch
            {
                return LanguageManager.GetString("UpdateVersionUnknown");
            }
        }

        private static string GetUpdateFeedUrl()
        {
#if SELF_CONTAINED
            return "https://download.cn-nb1.rains3.com/lingualink/stable-self-contained";
#elif FRAMEWORK_DEPENDENT
            return "https://download.cn-nb1.rains3.com/lingualink/stable-framework-dependent";
#else
            return string.Empty;
#endif
        }

        private void DisposeUpdateManager()
        {
            if (_updateManager is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _updateManager = null;
        }

        partial void OnHasUpdateChanged(bool value)
        {
            if (!value)
            {
                UpdateNotes = string.Empty;
                if (_latestStatus != UpdateStatus.UpdateAvailable)
                {
                    _pendingUpdate = null;
                    DisposeUpdateManager();
                }
            }

            CheckForUpdateCommand?.NotifyCanExecuteChanged();
            DownloadAndUpdateCommand?.NotifyCanExecuteChanged();
        }

        partial void OnIsCheckingUpdateChanged(bool value)
        {
            if (value)
            {
                HasUpdate = false;
            }

            CheckForUpdateCommand?.NotifyCanExecuteChanged();
            DownloadAndUpdateCommand?.NotifyCanExecuteChanged();
        }

        partial void OnIsDownloadingChanged(bool value)
        {
            CheckForUpdateCommand?.NotifyCanExecuteChanged();
            DownloadAndUpdateCommand?.NotifyCanExecuteChanged();
        }

        private enum UpdateStatus
        {
            NotChecked,
            Checking,
            UpToDate,
            UpdateAvailable,
            Failed,
            Disabled
        }
    }
}





















