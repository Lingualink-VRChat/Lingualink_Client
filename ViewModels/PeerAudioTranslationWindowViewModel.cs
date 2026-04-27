using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Managers;
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class PeerAudioTranslationWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly ILoggingManager _loggingManager;
        private readonly IAuthService? _authService;
        private readonly StringBuilder _resultBuilder = new StringBuilder();

        private PeerAudioTranslationService? _translationService;
        private AppSettings _appSettings;
        private bool _disposed;

        public ObservableCollection<PeerAudioCaptureTarget> CaptureTargets { get; } = new ObservableCollection<PeerAudioCaptureTarget>();

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

        public string WindowTitle => LanguageManager.GetString("PeerAudioWindowTitle");
        public string Description => LanguageManager.GetString("PeerAudioWindowDescription");
        public string ClearLabel => LanguageManager.GetString("ClearLog");
        public string CaptureTargetLabel => LanguageManager.GetString("PeerAudioCaptureTarget");
        public string RefreshProcessesLabel => LanguageManager.GetString("PeerAudioRefreshProcesses");
        public bool IsCaptureTargetSelectionEnabled => !IsWorking;

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

            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        [RelayCommand]
        private async Task ToggleAsync()
        {
            if (!IsWorking)
            {
                if (_authService?.IsLoggedIn != true)
                {
                    MessageBox.Show(
                        LanguageManager.GetString("BindRequireLogin"),
                        LanguageManager.GetString("WarningTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _appSettings = _settingsService.LoadSettings();
                _translationService = new PeerAudioTranslationService(_appSettings, _loggingManager);
                _translationService.StatusUpdated += OnStatusUpdated;
                _translationService.TranslationReceived += OnTranslationReceived;

                var target = SelectedCaptureTarget ?? CaptureTargets.FirstOrDefault() ?? CreateSystemAudioTarget();
                var success = await Task.Run(() => _translationService.Start(target));
                if (success)
                {
                    IsWorking = true;
                    ToggleButtonText = LanguageManager.GetString("PeerAudioStopListening");
                }
                else
                {
                    DisposeTranslationService();
                }

                return;
            }

            await Task.Run(() => _translationService?.Stop());
            DisposeTranslationService();
            IsWorking = false;
            ToggleButtonText = LanguageManager.GetString("PeerAudioStartListening");
            StatusText = LanguageManager.GetString("PeerAudioStatusStopped");
        }

        [RelayCommand]
        private void Clear()
        {
            _resultBuilder.Clear();
            ResultText = string.Empty;
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
            RefreshCaptureTargetsCommand.NotifyCanExecuteChanged();
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
                if (_resultBuilder.Length > 0)
                {
                    _resultBuilder.AppendLine();
                    _resultBuilder.AppendLine();
                }

                _resultBuilder.Append(e.DisplayText);
                ResultText = _resultBuilder.ToString();
            });
        }

        private void OnLanguageChanged()
        {
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(ClearLabel));
            OnPropertyChanged(nameof(CaptureTargetLabel));
            OnPropertyChanged(nameof(RefreshProcessesLabel));
            ToggleButtonText = IsWorking
                ? LanguageManager.GetString("PeerAudioStopListening")
                : LanguageManager.GetString("PeerAudioStartListening");
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
            DisposeTranslationService();
        }
    }
}
