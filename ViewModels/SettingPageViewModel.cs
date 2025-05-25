using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
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
        private readonly ILoggingManager _loggingManager;

        public string PageTitle => LanguageManager.GetString("GeneralSettings");
        public string InterfaceLanguage => LanguageManager.GetString("InterfaceLanguage");
        public string LanguageHint => LanguageManager.GetString("LanguageHint");
        public string RunningLogLabel => LanguageManager.GetString("RunningLog");
        public string ClearLogLabel => LanguageManager.GetString("ClearLog");

        // Log Properties - 现在从ILoggingManager获取
        public ObservableCollection<string> LogMessages => _loggingManager.LogMessages;
        public string FormattedLogMessages => _loggingManager.FormattedLogMessages;

        public SettingPageViewModel()
        {
            _settingsService = new SettingsService();
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>(); // 从容器解析中央日志管理器
            _appSettings = _settingsService.LoadSettings();
            
            LanguageManager.LanguageChanged += () => {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(InterfaceLanguage));
                OnPropertyChanged(nameof(LanguageHint));
                OnPropertyChanged(nameof(RunningLogLabel));
                OnPropertyChanged(nameof(ClearLogLabel));
            };

            // 订阅日志变化以更新FormattedLogMessages
            _loggingManager.LogMessages.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FormattedLogMessages));
            
            // 初始属性通知
            OnPropertyChanged(nameof(FormattedLogMessages));
        }

        [RelayCommand]
        private void ClearLog()
        {
            _loggingManager.ClearMessages(); // 直接调用管理器的清除方法
        }

        public void RefreshSettings()
        {
            _appSettings = _settingsService.LoadSettings();
        }
    }
}
