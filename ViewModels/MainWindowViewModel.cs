using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string Start => LanguageManager.GetString("Start");
        public string MessageTemplates => LanguageManager.GetString("MessageTemplates");
        public string Service => LanguageManager.GetString("Service");
        public string Settings => LanguageManager.GetString("Settings");
        public string AppTitle => LanguageManager.GetString("AppTitle");
        public string AppTitleBar => LanguageManager.GetString("AppTitleBar");

        public MainWindowViewModel()
        {
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(Start));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(MessageTemplates));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(Service));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(Settings));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AppTitle));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AppTitleBar));
        }
    }
}
