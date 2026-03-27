using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly DispatcherTimer _announcementPollTimer;
        private readonly HashSet<string> _dismissedAnnouncementIds = new();
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
        private SymbolRegular _announcementIcon = SymbolRegular.Info24;

        public MainWindowViewModel()
        {
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

                var next = _announcements.Find(item => !_dismissedAnnouncementIds.Contains(item.Id));
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (next == null)
                    {
                        _currentAnnouncementId = string.Empty;
                        AnnouncementVisible = false;
                        return;
                    }

                    ShowAnnouncement(next);
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
                _dismissedAnnouncementIds.Add(_currentAnnouncementId);
            }

            var next = _announcements.Find(item => !_dismissedAnnouncementIds.Contains(item.Id));
            if (next == null)
            {
                _currentAnnouncementId = string.Empty;
                AnnouncementVisible = false;
                return;
            }

            ShowAnnouncement(next);
        }

        private void ShowAnnouncement(PublicAnnouncement announcement)
        {
            _currentAnnouncementId = announcement.Id;
            AnnouncementTitle = announcement.Title;
            AnnouncementContent = announcement.Content;
            AnnouncementBackground = GetBrushForType(announcement.Type);
            AnnouncementIcon = GetIconForType(announcement.Type);
            AnnouncementVisible = true;
        }

        private static MediaBrush GetBrushForType(string type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "warning" => CreateBrush("#CC5C4A1A"),
                "critical" => CreateBrush("#CC5C1A1A"),
                _ => CreateBrush("#CC1A3A5C"),
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
