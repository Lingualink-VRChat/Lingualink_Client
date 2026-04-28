using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Managers;
using lingualink_client.Views;
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class PeerAudioTranslationWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly ILoggingManager _loggingManager;
        private readonly IAuthService? _authService;

        private PeerAudioTranslationService? _translationService;
        private PeerAudioTranslationWindow? _separateWindow;
        private AppSettings _appSettings;
        private bool _disposed;
        private bool _isApplyingFeatureEnabledChange;
        private bool _isLoadingPeerTargetLanguages;
        private bool _isUpdatingPeerLanguageItems;

        public ObservableCollection<PeerAudioCaptureTarget> CaptureTargets { get; } = new ObservableCollection<PeerAudioCaptureTarget>();
        public ObservableCollection<PeerAudioLanguageItemViewModel> PeerTargetLanguages { get; } = new ObservableCollection<PeerAudioLanguageItemViewModel>();
        public ObservableCollection<PeerAudioChatMessage> Messages { get; } = new ObservableCollection<PeerAudioChatMessage>();

        [ObservableProperty]
        private PeerAudioCaptureTarget? _selectedCaptureTarget;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private string _toggleButtonText = string.Empty;

        [ObservableProperty]
        private string _resultText = string.Empty;

        [ObservableProperty]
        private bool _isWorking;

        [ObservableProperty]
        private bool _isFeatureEnabled;

        [ObservableProperty]
        private bool _showResultsInSeparateWindow;

        public string WindowTitle => LanguageManager.GetString("PeerAudioWindowTitle");
        public string Description => LanguageManager.GetString("PeerAudioWindowDescription");
        public string ClearLabel => LanguageManager.GetString("ClearLog");
        public string CaptureTargetLabel => LanguageManager.GetString("PeerAudioCaptureTarget");
        public string RefreshProcessesLabel => LanguageManager.GetString("PeerAudioRefreshProcesses");
        public string EnableFeatureLabel => LanguageManager.GetString("PeerAudioEnableFeature");
        public string SeparateWindowLabel => LanguageManager.GetString("PeerAudioSeparateWindow");
        public string EmbeddedResultLabel => LanguageManager.GetString("PeerAudioEmbeddedResult");
        public string TargetLanguagesLabel => LanguageManager.GetString("TargetLanguages");
        public string AddLanguageLabel => LanguageManager.GetString("AddLanguage");
        public bool IsCaptureTargetSelectionEnabled => !IsWorking;
        public bool IsPeerTargetLanguageSelectionEnabled => !IsWorking;
        public bool IsSeparateWindowOptionVisible => IsFeatureEnabled;
        public bool IsEmbeddedResultVisible => !ShowResultsInSeparateWindow;
        public bool CanAddPeerTargetLanguage => !IsWorking && PeerTargetLanguages.Count < 3;

        public PeerAudioTranslationWindowViewModel()
        {
            _settingsService = new SettingsService();
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();
            if (ServiceContainer.TryResolve<IAuthService>(out var authService) && authService != null)
            {
                _authService = authService;
            }
            _appSettings = _settingsService.LoadSettings();

            StatusText = LanguageManager.GetString("PeerAudioStatusReady");
            ToggleButtonText = LanguageManager.GetString("PeerAudioStartListening");
            RefreshCaptureTargets();
            LoadPeerTargetLanguagesFromSettings();
            _ = InitializePeerLanguagesAsync();

            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        [RelayCommand]
        private Task ToggleAsync()
        {
            IsFeatureEnabled = !IsFeatureEnabled;
            return Task.CompletedTask;
        }

        [RelayCommand]
        private void Clear()
        {
            Messages.Clear();
            ResultText = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanAddPeerTargetLanguage))]
        private void AddPeerTargetLanguage()
        {
            var available = GetAvailablePeerBackendLanguages(null);
            var backendName = available.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(backendName))
            {
                return;
            }

            PeerTargetLanguages.Add(CreatePeerTargetLanguageItem(backendName));
            UpdatePeerLanguageItemState();
            PersistPeerTargetLanguages();
        }

        [RelayCommand(CanExecute = nameof(CanRefreshCaptureTargets))]
        private void RefreshCaptureTargets()
        {
            var previousProcessId = SelectedCaptureTarget?.ProcessId;
            CaptureTargets.Clear();

            var systemTarget = CreateSystemAudioTarget();
            CaptureTargets.Add(systemTarget);

            var processTargets = Process.GetProcesses()
                .Select(TryCreateProcessTarget)
                .Where(target => target != null)
                .OrderBy(target => target!.DisplayName, StringComparer.OrdinalIgnoreCase);

            foreach (var target in processTargets)
            {
                CaptureTargets.Add(target!);
            }

            SelectedCaptureTarget = CaptureTargets.FirstOrDefault(target => target.ProcessId == previousProcessId)
                ?? systemTarget;
        }

        private bool CanRefreshCaptureTargets() => !IsWorking;

        partial void OnIsWorkingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsCaptureTargetSelectionEnabled));
            OnPropertyChanged(nameof(IsPeerTargetLanguageSelectionEnabled));
            OnPropertyChanged(nameof(CanAddPeerTargetLanguage));
            RefreshCaptureTargetsCommand.NotifyCanExecuteChanged();
            AddPeerTargetLanguageCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsFeatureEnabledChanged(bool value)
        {
            if (_isApplyingFeatureEnabledChange)
            {
                return;
            }

            _ = ApplyFeatureEnabledChangeAsync(value);
        }

        partial void OnShowResultsInSeparateWindowChanged(bool value)
        {
            OnPropertyChanged(nameof(IsEmbeddedResultVisible));
            if (!IsFeatureEnabled)
            {
                CloseSeparateWindow();
                return;
            }

            if (value)
            {
                OpenSeparateWindow();
            }
            else
            {
                CloseSeparateWindow();
            }
        }

        private async Task ApplyFeatureEnabledChangeAsync(bool enabled)
        {
            if (enabled)
            {
                var started = await StartAsync();
                if (!started)
                {
                    _isApplyingFeatureEnabledChange = true;
                    IsFeatureEnabled = false;
                    _isApplyingFeatureEnabledChange = false;
                    OnPropertyChanged(nameof(IsSeparateWindowOptionVisible));
                    return;
                }

                OnPropertyChanged(nameof(IsSeparateWindowOptionVisible));
                if (ShowResultsInSeparateWindow)
                {
                    OpenSeparateWindow();
                }
                return;
            }

            await StopAsync();
            ShowResultsInSeparateWindow = false;
            CloseSeparateWindow();
            OnPropertyChanged(nameof(IsSeparateWindowOptionVisible));
        }

        private async Task<bool> StartAsync()
        {
            if (IsWorking)
            {
                return true;
            }

            if (_authService?.IsLoggedIn != true)
            {
                MessageBox.Show(
                    LanguageManager.GetString("BindRequireLogin"),
                    LanguageManager.GetString("WarningTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            _appSettings = _settingsService.LoadSettings();
            await InitializePeerLanguagesAsync();
            _appSettings.PeerAudioTargetLanguages = string.Join(",", GetSelectedPeerTargetBackendLanguages());
            _translationService = new PeerAudioTranslationService(_appSettings, _loggingManager, GetSelectedPeerTargetBackendLanguages());
            _translationService.StatusUpdated += OnStatusUpdated;
            _translationService.TranslationReceived += OnTranslationReceived;

            var target = SelectedCaptureTarget ?? CaptureTargets.FirstOrDefault() ?? CreateSystemAudioTarget();
            var success = await Task.Run(() => _translationService.Start(target));
            if (success)
            {
                IsWorking = true;
                ToggleButtonText = LanguageManager.GetString("PeerAudioStopListening");
                return true;
            }

            DisposeTranslationService();
            return false;
        }

        private async Task StopAsync()
        {
            if (!IsWorking && _translationService == null)
            {
                return;
            }

            await Task.Run(() => _translationService?.Stop());
            DisposeTranslationService();
            IsWorking = false;
            ToggleButtonText = LanguageManager.GetString("PeerAudioStartListening");
            StatusText = LanguageManager.GetString("PeerAudioStatusStopped");
        }

        private static PeerAudioCaptureTarget CreateSystemAudioTarget()
        {
            return new PeerAudioCaptureTarget(null, LanguageManager.GetString("PeerAudioSystemAudioTarget"));
        }

        private static PeerAudioCaptureTarget? TryCreateProcessTarget(Process process)
        {
            try
            {
                var title = process.MainWindowTitle;
                if (string.IsNullOrWhiteSpace(title))
                {
                    return null;
                }

                return new PeerAudioCaptureTarget(
                    process.Id,
                    $"{process.ProcessName} ({process.Id}) - {title}");
            }
            catch
            {
                return null;
            }
            finally
            {
                process.Dispose();
            }
        }

        private void OnStatusUpdated(object? sender, string status)
        {
            Application.Current.Dispatcher.Invoke(() => StatusText = status);
        }

        private void OnTranslationReceived(object? sender, PeerAudioTranslationResultEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new PeerAudioChatMessage(e.DisplayText, !e.IsSuccess));
                ResultText = string.Join($"{Environment.NewLine}{Environment.NewLine}", Messages.Select(message => message.Text));
            });
        }

        public void RemovePeerAudioTargetLanguage(PeerAudioLanguageItemViewModel item)
        {
            if (IsWorking || !PeerTargetLanguages.Contains(item) || PeerTargetLanguages.Count <= 1)
            {
                return;
            }

            PeerTargetLanguages.Remove(item);
            UpdatePeerLanguageItemState();
            PersistPeerTargetLanguages();
        }

        public void OnPeerAudioTargetLanguageChanged()
        {
            if (_isLoadingPeerTargetLanguages || _isUpdatingPeerLanguageItems)
            {
                return;
            }

            UpdatePeerLanguageItemState();
            PersistPeerTargetLanguages();
        }

        private void LoadPeerTargetLanguagesFromSettings()
        {
            _isLoadingPeerTargetLanguages = true;
            try
            {
                PeerTargetLanguages.Clear();
                var supported = GetSupportedPeerBackendLanguages();
                var configured = _appSettings.PeerAudioTargetLanguages
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(name => supported.Contains(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList();

                if (configured.Count == 0)
                {
                    configured = supported
                        .Where(name => name != LanguageDisplayHelper.TranscriptionBackendName)
                        .Take(2)
                        .ToList();
                }

                if (configured.Count == 0)
                {
                    configured = supported.Take(1).ToList();
                }

                foreach (var backendName in configured)
                {
                    PeerTargetLanguages.Add(CreatePeerTargetLanguageItem(backendName));
                }
            }
            finally
            {
                _isLoadingPeerTargetLanguages = false;
            }

            UpdatePeerLanguageItemState();
        }

        private async Task InitializePeerLanguagesAsync()
        {
            if (LanguageDisplayHelper.BackendLanguageNames.Count > 0)
            {
                return;
            }

            try
            {
                using var apiService = LingualinkApiServiceFactory.CreateApiService(_appSettings);
                await LanguageDisplayHelper.InitializeAsync(apiService);
                await Application.Current.Dispatcher.InvokeAsync(LoadPeerTargetLanguagesFromSettings);
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Failed to initialize peer audio target languages: {ex.Message}", LogLevel.Warning, "PeerAudio");
            }
        }

        private PeerAudioLanguageItemViewModel CreatePeerTargetLanguageItem(string backendName)
        {
            return new PeerAudioLanguageItemViewModel(
                this,
                backendName,
                new ObservableCollection<LanguageDisplayItem>(
                    GetAvailablePeerBackendLanguages(backendName)
                        .Select(name => new LanguageDisplayItem
                        {
                            BackendName = name,
                            DisplayName = LanguageDisplayHelper.GetDisplayName(name)
                        })));
        }

        private List<string> GetSupportedPeerBackendLanguages()
        {
            var languages = LanguageDisplayHelper.BackendLanguageNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (languages.Count > 0)
            {
                return languages;
            }

            return new List<string> { LanguageDisplayHelper.TranscriptionBackendName, "英文", "日文", "中文" };
        }

        private List<string> GetAvailablePeerBackendLanguages(string? currentBackendName)
        {
            var selected = PeerTargetLanguages
                .Select(item => item.BackendName)
                .Where(name => !string.IsNullOrWhiteSpace(name) && name != currentBackendName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return GetSupportedPeerBackendLanguages()
                .Where(name => name == currentBackendName || !selected.Contains(name))
                .ToList();
        }

        private void UpdatePeerLanguageItemState()
        {
            _isUpdatingPeerLanguageItems = true;
            try
            {
                for (var i = 0; i < PeerTargetLanguages.Count; i++)
                {
                    var item = PeerTargetLanguages[i];
                    item.Label = $"{LanguageManager.GetString("TargetLabel")} {i + 1}:";
                    item.CanRemove = PeerTargetLanguages.Count > 1 && !IsWorking;

                    var selectedBackendName = item.BackendName;
                    item.AvailableLanguages = new ObservableCollection<LanguageDisplayItem>(
                        GetAvailablePeerBackendLanguages(selectedBackendName)
                            .Select(name => new LanguageDisplayItem
                            {
                                BackendName = name,
                                DisplayName = LanguageDisplayHelper.GetDisplayName(name)
                            }));
                    item.SelectedDisplayLanguage = item.AvailableLanguages.FirstOrDefault(lang => lang.BackendName == selectedBackendName)
                        ?? item.AvailableLanguages.FirstOrDefault();
                }
            }
            finally
            {
                _isUpdatingPeerLanguageItems = false;
            }

            OnPropertyChanged(nameof(CanAddPeerTargetLanguage));
            AddPeerTargetLanguageCommand.NotifyCanExecuteChanged();
        }

        private IReadOnlyList<string> GetSelectedPeerTargetBackendLanguages()
        {
            return PeerTargetLanguages
                .Select(item => item.BackendName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void PersistPeerTargetLanguages()
        {
            if (_isLoadingPeerTargetLanguages)
            {
                return;
            }

            _appSettings.PeerAudioTargetLanguages = string.Join(",", GetSelectedPeerTargetBackendLanguages());
            _settingsService.SaveSettings(_appSettings);
        }

        private void OnLanguageChanged()
        {
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(ClearLabel));
            OnPropertyChanged(nameof(CaptureTargetLabel));
            OnPropertyChanged(nameof(RefreshProcessesLabel));
            OnPropertyChanged(nameof(EnableFeatureLabel));
            OnPropertyChanged(nameof(SeparateWindowLabel));
            OnPropertyChanged(nameof(EmbeddedResultLabel));
            OnPropertyChanged(nameof(TargetLanguagesLabel));
            OnPropertyChanged(nameof(AddLanguageLabel));
            UpdatePeerLanguageItemState();
            ToggleButtonText = IsWorking
                ? LanguageManager.GetString("PeerAudioStopListening")
                : LanguageManager.GetString("PeerAudioStartListening");
        }

        private void OpenSeparateWindow()
        {
            if (_separateWindow != null)
            {
                _separateWindow.Activate();
                return;
            }

            _separateWindow = new PeerAudioTranslationWindow(this, disposeViewModelOnClose: false);
            _separateWindow.Closed += OnSeparateWindowClosed;
            _separateWindow.Show();
        }

        private void CloseSeparateWindow()
        {
            if (_separateWindow == null)
            {
                return;
            }

            var window = _separateWindow;
            _separateWindow = null;
            window.Closed -= OnSeparateWindowClosed;
            window.Close();
        }

        private void OnSeparateWindowClosed(object? sender, EventArgs e)
        {
            if (_separateWindow != null)
            {
                _separateWindow.Closed -= OnSeparateWindowClosed;
                _separateWindow = null;
            }

            ShowResultsInSeparateWindow = false;
        }

        private void DisposeTranslationService()
        {
            if (_translationService == null)
            {
                return;
            }

            _translationService.StatusUpdated -= OnStatusUpdated;
            _translationService.TranslationReceived -= OnTranslationReceived;
            _translationService.Dispose();
            _translationService = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            CloseSeparateWindow();
            DisposeTranslationService();
        }
    }
}
