using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Services;
using lingualink_client.Models.Updates;
using lingualink_client.Services.Interfaces;
using Velopack;

namespace lingualink_client.ViewModels
{
    public partial class SettingPageViewModel : ViewModelBase
    {
        private readonly IUpdateService _updateService;

        private UpdateSession? _activeSession;
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

        public SettingPageViewModel(
            SettingsService? settingsService = null,
            IUpdateService? updateService = null)
        {
            _updateService = updateService ?? ServiceContainer.Resolve<IUpdateService>();

            CurrentVersion = ResolveCurrentVersion();

            if (_updateService.ActiveSession is { HasUpdate: true } existingSession)
            {
                _activeSession = existingSession;
                HasUpdate = true;
                _latestStatus = UpdateStatus.UpdateAvailable;
                UpdateNotes = existingSession.ReleaseNotesMarkdown ?? LanguageManager.GetString("UpdateNotesUnavailable");
            }

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

            if (!_updateService.IsSupported || string.IsNullOrWhiteSpace(_updateService.FeedUrl))
            {
                _latestStatus = UpdateStatus.Disabled;
                HasUpdate = false;
                UpdateNotes = string.Empty;
                RefreshLatestVersionText();
                return;
            }

            try
            {
                IsCheckingUpdate = true;
                _latestStatus = UpdateStatus.Checking;
                RefreshLatestVersionText();

                var result = await _updateService.CheckForUpdatesAsync();

                if (result.Error is not null)
                {
                    _latestStatus = UpdateStatus.Failed;
                    HasUpdate = false;
                    UpdateNotes = string.Empty;
                    RefreshLatestVersionText();
                    ShowError(LanguageManager.GetString("UpdateErrorCheck"), result.Error);
                    return;
                }

                if (!result.IsSupported)
                {
                    _latestStatus = UpdateStatus.Disabled;
                    HasUpdate = false;
                    UpdateNotes = string.Empty;
                    RefreshLatestVersionText();
                    return;
                }

                if (result.InstalledVersion is { } installed)
                {
                    CurrentVersion = installed.ToString();
                }
                else
                {
                    CurrentVersion = ResolveCurrentVersion();
                }

                if (result.HasUpdate && result.Session is not null)
                {
                    _activeSession = result.Session;
                    HasUpdate = true;
                    _latestStatus = UpdateStatus.UpdateAvailable;
                    UpdateNotes = string.IsNullOrWhiteSpace(result.ReleaseNotesMarkdown)
                        ? LanguageManager.GetString("UpdateNotesUnavailable")
                        : result.ReleaseNotesMarkdown!;
                }
                else
                {
                    if (_activeSession is not null)
                    {
                        _updateService.ReleaseSession(_activeSession);
                        _activeSession = null;
                    }

                    HasUpdate = false;
                    _latestStatus = UpdateStatus.UpToDate;
                    UpdateNotes = string.Empty;
                }

                RefreshLatestVersionText();
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
            if (_activeSession is null || !_activeSession.HasUpdate)
            {
                return;
            }

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;

                var progress = new Progress<int>(value =>
                {
                    DownloadProgress = value;
                });

                await _updateService.DownloadAsync(_activeSession, progress, CancellationToken.None);

                var prompt = LanguageManager.GetString("UpdateDialogDownloadPrompt");
                var title = LanguageManager.GetString("UpdateReadyTitle");
                var result = WpfMessageBox.Show(prompt, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _updateService.ApplyAsync(_activeSession, restart: true, silent: false, CancellationToken.None);
                    _activeSession = null;
                    HasUpdate = false;
                    _latestStatus = UpdateStatus.UpToDate;
                    RefreshLatestVersionText();

                    Application.Current.Shutdown();
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
                    if (_activeSession?.TargetVersion is not null)
                    {
                        LatestVersion = _activeSession.TargetVersion.ToString();
                    }
                    else
                    {
                        LatestVersion = LanguageManager.GetString("UpdateStatusUpToDate");
                    }
                    break;
            }
        }



        private static void ShowError(string message, Exception exception)
        {
            var errorTitle = LanguageManager.GetString("UpdateErrorTitle");
            WpfMessageBox.Show($"{message}: {exception.Message}", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string ResolveCurrentVersion()
        {
            try
            {
                var installed = _updateService.GetInstalledVersion();
                if (installed is not null)
                {
                    return installed.ToString();
                }

                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var version = assembly?.GetName().Version;
                return version?.ToString() ?? LanguageManager.GetString("UpdateVersionUnknown");
            }
            catch
            {
                return LanguageManager.GetString("UpdateVersionUnknown");
            }
        }

        partial void OnHasUpdateChanged(bool value)
        {
            if (!value)
            {
                UpdateNotes = string.Empty;
                if (_latestStatus == UpdateStatus.UpdateAvailable)
                {
                    _latestStatus = UpdateStatus.UpToDate;
                }
            }

            CheckForUpdateCommand?.NotifyCanExecuteChanged();
            DownloadAndUpdateCommand?.NotifyCanExecuteChanged();
            RefreshLatestVersionText();
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










