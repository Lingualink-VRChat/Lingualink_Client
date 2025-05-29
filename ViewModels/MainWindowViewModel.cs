﻿using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string Start => LanguageManager.GetString("Start");
        public string Account => LanguageManager.GetString("Account");
        public string MessageTemplates => LanguageManager.GetString("MessageTemplates");
        public string Service => LanguageManager.GetString("Service");
        public string Settings => LanguageManager.GetString("Settings");
        public string AppTitle => LanguageManager.GetString("AppTitle");
        public string AppTitleBar => LanguageManager.GetString("AppTitleBar");

        public MainWindowViewModel()
        {
            LanguageManager.LanguageChanged += RefreshLanguageBindings;
        }

        public void RefreshLanguageBindings()
        {
            OnPropertyChanged(nameof(Start));
            OnPropertyChanged(nameof(Account));
            OnPropertyChanged(nameof(MessageTemplates));
            OnPropertyChanged(nameof(Service));
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(AppTitle));
            OnPropertyChanged(nameof(AppTitleBar));
        }
    }
}
