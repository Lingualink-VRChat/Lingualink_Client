using CommunityToolkit.Mvvm.ComponentModel;
using lingualink_client.Models;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public partial class SettingPageViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;

        public string PageTitle => LanguageManager.GetString("GeneralSettings");
        public string InterfaceLanguage => LanguageManager.GetString("InterfaceLanguage");
        public string LanguageHint => LanguageManager.GetString("LanguageHint");

        public SettingPageViewModel()
        {
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();

            LanguageManager.LanguageChanged += () =>
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(InterfaceLanguage));
                OnPropertyChanged(nameof(LanguageHint));
            };
        }

        public void RefreshSettings()
        {
            _appSettings = _settingsService.LoadSettings();
        }
    }
}
