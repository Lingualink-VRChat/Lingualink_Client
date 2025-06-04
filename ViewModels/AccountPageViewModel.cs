using System;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
// 使用现代化MessageBox替换系统默认的MessageBox
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class AccountPageViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;

        // 语言相关的标签
        public string AccountSettingsLabel => LanguageManager.GetString("AccountSettings");
        public string AuthenticationModeLabel => LanguageManager.GetString("AuthenticationMode");
        public string OfficialServiceLabel => LanguageManager.GetString("OfficialService");
        public string CustomServiceLabel => LanguageManager.GetString("CustomService");
        public string OfficialServiceHint => LanguageManager.GetString("OfficialServiceHint");
        public string CustomServiceHint => LanguageManager.GetString("CustomServiceHint");
        public string UserLoginLabel => LanguageManager.GetString("UserLogin");
        public string UsernameLabel => LanguageManager.GetString("Username");
        public string PasswordLabel => LanguageManager.GetString("Password");
        public string LoginLabel => LanguageManager.GetString("Login");
        public string LogoutLabel => LanguageManager.GetString("Logout");
        public string LoginStatusLabel => LanguageManager.GetString("LoginStatus");
        public string NotLoggedInLabel => LanguageManager.GetString("NotLoggedIn");
        public string ComingSoonLabel => LanguageManager.GetString("ComingSoon");
        public string CustomServerSettingsLabel => LanguageManager.GetString("CustomServerSettings");
        public string ServerUrlLabel => LanguageManager.GetString("ServerUrl");
        public string ApiKeyLabel => LanguageManager.GetString("ApiKey");
        public string SaveLabel => LanguageManager.GetString("Save");
        public string RevertLabel => LanguageManager.GetString("Revert");

        // 新增的标签
        public string OfficialServiceLoginLabel => LanguageManager.GetString("OfficialServiceLogin");
        public string OfficialServiceSubtitleLabel => LanguageManager.GetString("OfficialServiceSubtitle");
        public string AdvancedOptionsLabel => LanguageManager.GetString("AdvancedOptions");
        public string UseCustomServerLabel => LanguageManager.GetString("UseCustomServer");
        public string UseCustomServerHint => LanguageManager.GetString("UseCustomServerHint");
        public string GetStartedLabel => LanguageManager.GetString("GetStarted");
        public string CreateAccountLabel => LanguageManager.GetString("CreateAccount");
        public string ForgotPasswordLabel => LanguageManager.GetString("ForgotPassword");
        public string ConnectionTestLabel => LanguageManager.GetString("ConnectionTest");



        // 认证模式属性 - 简化为只有一个开关
        [ObservableProperty] private bool _useCustomServer = false;
        
        // 官方服务登录相关属性
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _userPassword = string.Empty;
        [ObservableProperty] private bool _isLoggedIn = false;
        [ObservableProperty] private string _loggedInUsername = string.Empty;
        
        // 自定义服务器设置
        [ObservableProperty] private string _serverUrl = string.Empty;
        [ObservableProperty] private string _apiKey = string.Empty;

        // 属性变更监听
        partial void OnApiKeyChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ApiKey property changed to: '{value}'");
        }

        partial void OnServerUrlChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ServerUrl property changed to: '{value}'");
        }

        public AccountPageViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _currentSettings = _settingsService.LoadSettings();
            
            LoadSettingsFromModel(_currentSettings);
            
            // 订阅语言变化事件
            SubscribeToLanguageChanges();
        }

        private void SubscribeToLanguageChanges()
        {
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AccountSettingsLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AuthenticationModeLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OfficialServiceLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(CustomServiceLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OfficialServiceHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(CustomServiceHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(UserLoginLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(UsernameLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(PasswordLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(LoginLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(LogoutLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(LoginStatusLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(NotLoggedInLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ComingSoonLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(CustomServerSettingsLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ServerUrlLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ApiKeyLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(SaveLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(RevertLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OfficialServiceLoginLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(OfficialServiceSubtitleLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(AdvancedOptionsLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(UseCustomServerLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(UseCustomServerHint));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(GetStartedLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(CreateAccountLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ForgotPasswordLabel));
            LanguageManager.LanguageChanged += () => OnPropertyChanged(nameof(ConnectionTestLabel));


        }

        private void LoadSettingsFromModel(AppSettings settings)
        {
            // 目前默认使用自定义服务
            UseCustomServer = true; // 默认启用自定义服务器

            ServerUrl = settings.ServerUrl;
            ApiKey = settings.ApiKey;

            // 官方服务相关的设置暂时保持默认值
            IsLoggedIn = false;
            LoggedInUsername = string.Empty;
        }

        private bool ValidateAndBuildSettings(out AppSettings? updatedSettings)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ValidateAndBuildSettings() called");

            // 使用当前设置作为基础，而不是重新从文件加载
            updatedSettings = _currentSettings ?? new AppSettings();

            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Current settings base - ApiKey: '{updatedSettings.ApiKey}', ServerUrl: '{updatedSettings.ServerUrl}'");

            // 只有在使用自定义服务器时才需要验证URL和API密钥
            if (UseCustomServer)
            {
                if (string.IsNullOrWhiteSpace(ServerUrl) || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
                {
                    MessageBox.Show(LanguageManager.GetString("ValidationServerUrlInvalid"),
                                   LanguageManager.GetString("ValidationErrorTitle"),
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    MessageBox.Show(LanguageManager.GetString("ValidationApiKeyRequired"),
                                   LanguageManager.GetString("ValidationErrorTitle"),
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            // 只有在使用自定义服务器时才更新URL和API密钥
            if (UseCustomServer)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Using custom server - updating settings");
                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ViewModel values - ServerUrl: '{this.ServerUrl}', ApiKey: '{this.ApiKey}'");

                updatedSettings.ServerUrl = this.ServerUrl;
                updatedSettings.ApiKey = this.ApiKey;

                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Updated settings - ServerUrl: '{updatedSettings.ServerUrl}', ApiKey: '{updatedSettings.ApiKey}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Using official service - clearing custom settings");

                // 使用官方服务时，清空自定义设置或使用默认的官方服务设置
                updatedSettings.ServerUrl = "https://api.lingualink.aiatechco.com/api/v1/";
                updatedSettings.ApiKey = string.Empty; // 官方服务将通过登录token处理认证
            }

            return true;
        }

        [RelayCommand]
        private void Save()
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ===== SAVE COMMAND TRIGGERED =====");
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Save() called - Current ApiKey: '{ApiKey}', UseCustomServer: {UseCustomServer}");

            if (ValidateAndBuildSettings(out AppSettings? updatedSettings))
            {
                if (updatedSettings != null)
                {
                    updatedSettings.GlobalLanguage = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;

                    System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Attempting to save ApiKey: '{updatedSettings.ApiKey}'");
                    System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] About to save settings - ApiKey: '{updatedSettings.ApiKey}', ServerUrl: '{updatedSettings.ServerUrl}'");

                    _settingsService.SaveSettings(updatedSettings);
                    _currentSettings = updatedSettings;

                    System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Settings saved, raising SettingsChanged event");
                    SettingsChangedNotifier.RaiseSettingsChanged();

                    MessageBox.Show(LanguageManager.GetString("SettingsSavedSuccess"),
                                   LanguageManager.GetString("SuccessTitle"),
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        [RelayCommand]
        private void Revert()
        {
            _currentSettings = _settingsService.LoadSettings();
            LoadSettingsFromModel(_currentSettings);
            
            MessageBox.Show(LanguageManager.GetString("SettingsReverted"),
                           LanguageManager.GetString("InfoTitle"),
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Login()
        {
            // 官方服务登录逻辑 - 即将实现
            MessageBox.Show(LanguageManager.GetString("ComingSoon"),
                           LanguageManager.GetString("InfoTitle"),
                           MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Logout()
        {
            // 退出登录逻辑
            IsLoggedIn = false;
            LoggedInUsername = string.Empty;
            Username = string.Empty;
            UserPassword = string.Empty;
        }
    }
} 