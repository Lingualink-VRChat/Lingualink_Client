using System;
using CommunityToolkit.Mvvm.ComponentModel;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly IAuthService? _authService;
        private readonly SettingsService _settingsService;
        private readonly AppSettings _appSettings;

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

        public MainWindowViewModel()
        {
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();

            if (ServiceContainer.TryResolve<IAuthService>(out var authService) && authService != null)
            {
                _authService = authService;
            }

            LanguageManager.LanguageChanged += RefreshLanguageBindings;
            InitializeAnnouncements();
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
            RefreshAnnouncementBindings();
        }

        public void Dispose()
        {
            LanguageManager.LanguageChanged -= RefreshLanguageBindings;
            DisposeAnnouncements();
        }
    }
}
