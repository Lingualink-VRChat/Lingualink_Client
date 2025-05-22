using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string Start => LanguageManager.GetString("Start");
        public string Service => LanguageManager.GetString("Service");
        public string Log => LanguageManager.GetString("Log");
        public string Settings => LanguageManager.GetString("Settings");
        public string AppTitle => LanguageManager.GetString("AppTitle");
        public string AppTitleBar => LanguageManager.GetString("AppTitleBar");

        public MainWindowViewModel()
        {
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(Start));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(Service));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(Log));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(Settings));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AppTitle));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AppTitleBar));
        }
    }
}
