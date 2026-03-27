using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models.Auth;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using Wpf.Ui.Controls;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace lingualink_client.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly IAuthService? _authService;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _appSettings;
        private readonly DispatcherTimer _announcementPollTimer;
        private readonly HashSet<string> _sessionDismissedAnnouncementIds = new();
        private List<PublicAnnouncement> _announcements = new();
        private string _currentAnnouncementId = string.Empty;
        private bool _isRefreshingAnnouncements;

        public string Start => LanguageManager.GetString("Start");
        public string Account => LanguageManager.GetString("Account");
        public string MessageTemplates => LanguageManager.GetString("MessageTemplates");
        public string MessageTyping => LanguageManager.GetString("MessageTyping");
        public string Voice => LanguageManager.GetString("Voice");
        public string Settings => LanguageManager.GetString("Settings");
        public string ConversationHistory => LanguageManager.GetString("ConversationHistory");
        public string Logs => LanguageManager.GetString("Logs");
        public string AppTitle => LanguageManager.GetString("AppTitle");
        public string AppTitleBar => LanguageManager.GetString("AppTitleBar");

        [ObservableProperty]
        private bool _announcementVisible;

        [ObservableProperty]
        private string _announcementTitle = string.Empty;

        [ObservableProperty]
        private string _announcementContent = string.Empty;

        [ObservableProperty]
        private MediaBrush _announcementBackground = CreateBrush("#CC1A3A5C");

        [ObservableProperty]
        private MediaBrush _announcementBorderBrush = CreateBrush("#E9E9EB");

        [ObservableProperty]
        private MediaBrush _announcementTitleForeground = CreateBrush("#303133");

        [ObservableProperty]
        private MediaBrush _announcementContentForeground = CreateBrush("#606266");

        [ObservableProperty]
        private MediaBrush _announcementAccentForeground = CreateBrush("#909399");

        [ObservableProperty]
        private MediaBrush _announcementCloseForeground = CreateBrush("#909399");

        [ObservableProperty]
        private SymbolRegular _announcementIcon = SymbolRegular.Info24;

        public MainWindowViewModel()
        {
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();

            if (ServiceContainer.TryResolve<IAuthService>(out var authService) && authService != null)
            {
                _authService = authService;
            }

            LanguageManager.LanguageChanged += RefreshLanguageBindings;

            _announcementPollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _announcementPollTimer.Tick += async (_, _) => await RefreshAnnouncementsAsync();
            _announcementPollTimer.Start();

            _ = ScheduleInitialAnnouncementRefreshAsync();
        }

        public void RefreshLanguageBindings()
        {
            OnPropertyChanged(nameof(Start));
            OnPropertyChanged(nameof(Account));
            OnPropertyChanged(nameof(MessageTemplates));
            OnPropertyChanged(nameof(MessageTyping));
            OnPropertyChanged(nameof(Voice));
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(ConversationHistory));
            OnPropertyChanged(nameof(Logs));
            OnPropertyChanged(nameof(AppTitle));
            OnPropertyChanged(nameof(AppTitleBar));
        }

        private async Task ScheduleInitialAnnouncementRefreshAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            await RefreshAnnouncementsAsync();
        }

        private async Task RefreshAnnouncementsAsync()
        {
            if (_authService == null || _isRefreshingAnnouncements)
            {
                return;
            }

            _isRefreshingAnnouncements = true;
            try
            {
                var items = await _authService.GetActiveAnnouncementsAsync();
                _announcements = new List<PublicAnnouncement>(items);
                CleanupStaleDismissedAnnouncementIds();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ShowNextAnnouncement();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel] Failed to refresh announcements: {ex.Message}");
            }
            finally
            {
                _isRefreshingAnnouncements = false;
            }
        }

        [RelayCommand]
        private void DismissAnnouncement()
        {
            if (!string.IsNullOrWhiteSpace(_currentAnnouncementId))
            {
                _sessionDismissedAnnouncementIds.Add(_currentAnnouncementId);
            }

            ShowNextAnnouncement();
        }

        [RelayCommand]
        private void PermanentDismissAnnouncement()
        {
            if (string.IsNullOrWhiteSpace(_currentAnnouncementId))
            {
                return;
            }

            _sessionDismissedAnnouncementIds.Add(_currentAnnouncementId);

            if (!_appSettings.DismissedAnnouncementIds.Contains(_currentAnnouncementId, StringComparer.Ordinal))
            {
                _appSettings.DismissedAnnouncementIds.Add(_currentAnnouncementId);
                _settingsService.SaveSettings(_appSettings);
            }

            ShowNextAnnouncement();
        }

        private void ShowAnnouncement(PublicAnnouncement announcement)
        {
            _currentAnnouncementId = announcement.Id;
            AnnouncementTitle = announcement.Title;
            AnnouncementContent = announcement.Content;
            AnnouncementBackground = GetBrushForType(announcement.Type);
            AnnouncementBorderBrush = GetBorderBrushForType(announcement.Type);
            AnnouncementTitleForeground = CreateBrush("#303133");
            AnnouncementContentForeground = CreateBrush("#606266");
            AnnouncementAccentForeground = GetAccentBrushForType(announcement.Type);
            AnnouncementCloseForeground = CreateBrush("#909399");
            AnnouncementIcon = GetIconForType(announcement.Type);
            AnnouncementVisible = true;
        }

        private void ShowNextAnnouncement()
        {
            var next = _announcements.FirstOrDefault(item => !IsAnnouncementDismissed(item.Id));
            if (next == null)
            {
                _currentAnnouncementId = string.Empty;
                AnnouncementVisible = false;
                return;
            }

            ShowAnnouncement(next);
        }

        private bool IsAnnouncementDismissed(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return true;
            }

            return _sessionDismissedAnnouncementIds.Contains(id)
                || _appSettings.DismissedAnnouncementIds.Contains(id, StringComparer.Ordinal);
        }

        private void CleanupStaleDismissedAnnouncementIds()
        {
            if (_appSettings.DismissedAnnouncementIds.Count == 0)
            {
                return;
            }

            var activeIds = new HashSet<string>(
                _announcements
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => item.Id),
                StringComparer.Ordinal);

            var removed = _appSettings.DismissedAnnouncementIds.RemoveAll(id => !activeIds.Contains(id));
            if (removed > 0)
            {
                _settingsService.SaveSettings(_appSettings);
            }
        }

        private static MediaBrush GetBrushForType(string type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "warning" => CreateBrush("#FDF6EC"),
                "critical" => CreateBrush("#FEF0F0"),
                _ => CreateBrush("#F4F4F5"),
            };
        }

        private static MediaBrush GetBorderBrushForType(string type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "warning" => CreateBrush("#FAECD8"),
                "critical" => CreateBrush("#FDE2E2"),
                _ => CreateBrush("#E9E9EB"),
            };
        }

        private static MediaBrush GetAccentBrushForType(string type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "warning" => CreateBrush("#E6A23C"),
                "critical" => CreateBrush("#F56C6C"),
                _ => CreateBrush("#909399"),
            };
        }

        private static SymbolRegular GetIconForType(string type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "warning" => SymbolRegular.Warning24,
                "critical" => SymbolRegular.ErrorCircle24,
                _ => SymbolRegular.Info24,
            };
        }

        private static MediaBrush CreateBrush(string hex)
        {
            return new MediaSolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(hex));
        }

        public void Dispose()
        {
            LanguageManager.LanguageChanged -= RefreshLanguageBindings;
            _announcementPollTimer.Stop();
        }
    }
}
