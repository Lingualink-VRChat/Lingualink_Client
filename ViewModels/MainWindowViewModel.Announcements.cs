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
using Wpf.Ui.Controls;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace lingualink_client.ViewModels
{
    public partial class MainWindowViewModel
    {
        private DispatcherTimer _announcementPollTimer = null!;
        private readonly HashSet<string> _sessionDismissedAnnouncementIds = new();
        private List<PublicAnnouncement> _announcements = new();
        private string _currentAnnouncementId = string.Empty;
        private bool _isRefreshingAnnouncements;

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

        public string AnnouncementPermanentDismissLabel => LanguageManager.GetString("AnnouncementPermanentDismiss");
        public string AnnouncementDismissLabel => LanguageManager.GetString("AnnouncementDismiss");

        private void InitializeAnnouncements()
        {
            _announcementPollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30)
            };
            _announcementPollTimer.Tick += AnnouncementPollTimerOnTick;
            _announcementPollTimer.Start();

            _ = ScheduleInitialAnnouncementRefreshAsync();
        }

        private void RefreshAnnouncementBindings()
        {
            OnPropertyChanged(nameof(AnnouncementPermanentDismissLabel));
            OnPropertyChanged(nameof(AnnouncementDismissLabel));
        }

        private void DisposeAnnouncements()
        {
            _announcementPollTimer.Tick -= AnnouncementPollTimerOnTick;
            _announcementPollTimer.Stop();
        }

        private async void AnnouncementPollTimerOnTick(object? sender, EventArgs e)
        {
            await RefreshAnnouncementsAsync();
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

                await Application.Current.Dispatcher.InvokeAsync(ShowNextAnnouncement);
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
    }
}
