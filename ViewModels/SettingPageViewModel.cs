using lingualink_client.Services;
using lingualink_client.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace lingualink_client.ViewModels
{
    public partial class SettingPageViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;

        public string PageTitle => LanguageManager.GetString("GeneralSettings");
        public string InterfaceLanguage => LanguageManager.GetString("InterfaceLanguage");
        public string LanguageHint => LanguageManager.GetString("LanguageHint");
        public string RunningLogLabel => LanguageManager.GetString("RunningLog");
        public string ClearLogLabel => LanguageManager.GetString("ClearLog");

        // Log Properties - shared with IndexWindowViewModel
        public ObservableCollection<string> LogMessages => 
            ((App)Application.Current).SharedIndexWindowViewModel.LogMessages;
        public string FormattedLogMessages => 
            ((App)Application.Current).SharedIndexWindowViewModel.FormattedLogMessages;

        public SettingPageViewModel()
        {
            _settingsService = new SettingsService();
            _appSettings = _settingsService.LoadSettings();
            
            LanguageManager.LanguageChanged += () => {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(InterfaceLanguage));
                OnPropertyChanged(nameof(LanguageHint));
                OnPropertyChanged(nameof(RunningLogLabel));
                OnPropertyChanged(nameof(ClearLogLabel));
            };

            // Subscribe to log changes
            LogMessages.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FormattedLogMessages));
        }

        [RelayCommand]
        private void ClearLog()
        {
            ((App)Application.Current).SharedIndexWindowViewModel.ClearLogCommand.Execute(null);
        }

        public void RefreshSettings()
        {
            _appSettings = _settingsService.LoadSettings();
        }
    }
}
