using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using lingualink_client.Models;
using lingualink_client.Models.Auth;
using lingualink_client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Services.Interfaces;
// 使用现代化MessageBox替换系统默认的MessageBox
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class AccountPageViewModel : ViewModelBase
    {
        private readonly ISettingsManager _settingsManager;
        private readonly IAuthService? _authService;
        private AppSettings _currentSettings;
        private bool _isLoadingSettings;
        private readonly DispatcherTimer _autoSaveTimer;
        private bool _hasPendingChanges;

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

        // 新增：测试连接状态
        [ObservableProperty]
        private bool _isTestingConnection = false;

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

        // 新增：登录状态相关属性
        [ObservableProperty]
        private bool _isLoggingIn = false;

        [ObservableProperty]
        private UserProfile? _userProfile;

        [ObservableProperty]
        private string _subscriptionStatus = string.Empty;

        [ObservableProperty]
        private string _quotaDisplay = string.Empty;

        // API Key 管理相关属性
        [ObservableProperty]
        private ObservableCollection<ApiKeyInfo> _apiKeys = new();

        [ObservableProperty]
        private bool _isLoadingApiKeys = false;

        [ObservableProperty]
        private string _newApiKeyName = string.Empty;

        [ObservableProperty]
        private bool _isCreatingApiKey = false;

        [ObservableProperty]
        private string? _newlyCreatedApiKey = null;

        [ObservableProperty]
        private bool _showNewApiKeyDialog = false;

        // 当 InfoBar 关闭时清空已创建的 Key
        partial void OnShowNewApiKeyDialogChanged(bool value)
        {
            if (!value)
            {
                NewlyCreatedApiKey = null;
            }
        }

        // 属性变更监听（主要用于调试）
        partial void OnApiKeyChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ApiKey property changed to: '{value}'");
        }

        partial void OnServerUrlChanged(string value)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ServerUrl property changed to: '{value}'");
        }

        public AccountPageViewModel(ISettingsManager? settingsManager = null, IAuthService? authService = null)
        {
            _settingsManager = settingsManager
                               ?? (ServiceContainer.TryResolve<ISettingsManager>(out var resolved) && resolved != null
                                   ? resolved
                                   : new SettingsManager());
            _currentSettings = _settingsManager.LoadSettings();
            
            // 尝试获取 AuthService
            if (authService != null)
            {
                _authService = authService;
            }
            else if (ServiceContainer.TryResolve<IAuthService>(out var resolvedAuth) && resolvedAuth != null)
            {
                _authService = resolvedAuth;
            }
            
            LoadSettingsFromModel(_currentSettings);
            
            // 订阅语言变化事件（统一刷新所有本地化绑定）
            LanguageManager.LanguageChanged += OnLanguageChanged;

            // 初始化自动保存计时器（防抖）
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoSaveTimer.Tick += AutoSaveTimerOnTick;

            // 监听属性变更，用于触发自动保存
            PropertyChanged += OnAccountPropertyChanged;

            // 订阅登录状态变化
            if (_authService != null)
            {
                _authService.LoginStateChanged += OnAuthServiceLoginStateChanged;
                // 初始化登录状态
                UpdateLoginState();
            }
        }

        private void OnAuthServiceLoginStateChanged(object? sender, bool isLoggedIn)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateLoginState();
                
                // 登录成功后自动加载 API Keys
                if (isLoggedIn)
                {
                    _ = LoadApiKeysAsync();
                }
            });
        }

        private void UpdateLoginState()
        {
            if (_authService == null)
            {
                IsLoggedIn = false;
                return;
            }

            IsLoggedIn = _authService.IsLoggedIn;
            UserProfile = _authService.CurrentUser;

            if (UserProfile != null)
            {
                LoggedInUsername = !string.IsNullOrEmpty(UserProfile.DisplayName)
                    ? UserProfile.DisplayName
                    : UserProfile.CasdoorName ?? "用户";

                // 更新订阅状态显示
                if (UserProfile.Subscription != null)
                {
                    SubscriptionStatus = UserProfile.Subscription.PlanName;
                    QuotaDisplay = $"{UserProfile.Subscription.QuotaRemaining:N0} / {UserProfile.Subscription.QuotaTotal:N0}";
                }
                else
                {
                    SubscriptionStatus = "Free";
                    QuotaDisplay = string.Empty;
                }
            }
            else
            {
                LoggedInUsername = string.Empty;
                SubscriptionStatus = string.Empty;
                QuotaDisplay = string.Empty;
            }

            // 通知 UI 更新命令状态
            LoginCommand.NotifyCanExecuteChanged();
            LogoutCommand.NotifyCanExecuteChanged();
            RefreshUserProfileCommand.NotifyCanExecuteChanged();
            CreateApiKeyCommand.NotifyCanExecuteChanged();
        }

        private void OnLanguageChanged()
        {
            // 空字符串表示刷新该 ViewModel 的所有绑定属性，适合本地化场景
            OnPropertyChanged(string.Empty);
        }

        private void LoadSettingsFromModel(AppSettings settings)
        {
            _isLoadingSettings = true;
            try
            {
                _currentSettings = settings;

                // 从设置中恢复是否使用自定义服务器
                UseCustomServer = settings.UseCustomServer;

                if (UseCustomServer)
                {
                    // 优先使用单独存储的自定义服务器配置，否则回退到当前全局配置
                    ServerUrl = string.IsNullOrWhiteSpace(settings.CustomServerUrl)
                        ? settings.ServerUrl
                        : settings.CustomServerUrl;

                    ApiKey = string.IsNullOrEmpty(settings.CustomApiKey)
                        ? settings.ApiKey
                        : settings.CustomApiKey;
                }
                else
                {
                    // 使用官方服务配置；如果未显式设置则使用默认官方地址
                    ServerUrl = string.IsNullOrWhiteSpace(settings.OfficialServerUrl)
                        ? "https://api.lingualink.aiatechco.com/api/v1/"
                        : settings.OfficialServerUrl;

                    ApiKey = settings.OfficialApiKey;
                }
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void OnAccountPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null || _isLoadingSettings)
            {
                return;
            }

            if (!IsAutoSaveProperty(e.PropertyName))
            {
                return;
            }

            _hasPendingChanges = true;

            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            _autoSaveTimer.Start();
        }

        private void AutoSaveTimerOnTick(object? sender, EventArgs e)
        {
            _autoSaveTimer.Stop();

            if (!_hasPendingChanges)
            {
                return;
            }

            _hasPendingChanges = false;

            SaveInternal(showConfirmation: false, changeSource: "AccountPageAutoSave");
        }

        private static bool IsAutoSaveProperty(string propertyName)
        {
            return propertyName == nameof(UseCustomServer)
                   || propertyName == nameof(ServerUrl)
                   || propertyName == nameof(ApiKey);
        }

        partial void OnUseCustomServerChanged(bool value)
        {
            if (_currentSettings == null || _isLoadingSettings)
            {
                return;
            }

            // 在切换模式时，不立刻写盘，只在保存时持久化。
            if (value)
            {
                // 切换到自定义服务器：先记录当前官方配置，再恢复自定义配置
                _currentSettings.OfficialServerUrl = ServerUrl;
                _currentSettings.OfficialApiKey = ApiKey;

                var customUrl = string.IsNullOrWhiteSpace(_currentSettings.CustomServerUrl)
                    ? _currentSettings.ServerUrl
                    : _currentSettings.CustomServerUrl;

                var customKey = string.IsNullOrEmpty(_currentSettings.CustomApiKey)
                    ? _currentSettings.ApiKey
                    : _currentSettings.CustomApiKey;

                ServerUrl = customUrl;
                ApiKey = customKey;
            }
            else
            {
                // 切换到官方服务：先记录当前自定义配置，再恢复官方配置
                _currentSettings.CustomServerUrl = ServerUrl;
                _currentSettings.CustomApiKey = ApiKey;

                if (string.IsNullOrWhiteSpace(_currentSettings.OfficialServerUrl))
                {
                    _currentSettings.OfficialServerUrl = "https://api.lingualink.aiatechco.com/api/v1/";
                }

                ServerUrl = _currentSettings.OfficialServerUrl;
                ApiKey = _currentSettings.OfficialApiKey;
            }
        }

        private bool UpdateSettingsFromView(AppSettings updatedSettings)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ValidateAndBuildSettings() called");
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Loaded latest settings base - ApiKey: '{updatedSettings.ApiKey}', ServerUrl: '{updatedSettings.ServerUrl}'");

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

            // 持久化当前认证模式选择
            updatedSettings.UseCustomServer = this.UseCustomServer;

            // 只有在使用自定义服务器时才更新URL和API密钥
            if (UseCustomServer)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Using custom server - updating settings");
                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] ViewModel values - ServerUrl: '{this.ServerUrl}', ApiKey: '{this.ApiKey}'");

                // 保存自定义服务器配置
                updatedSettings.CustomServerUrl = this.ServerUrl;
                updatedSettings.CustomApiKey = this.ApiKey;

                // 将全局 ServerUrl/ApiKey 也指向当前自定义配置
                updatedSettings.ServerUrl = this.ServerUrl;
                updatedSettings.ApiKey = this.ApiKey;

                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Updated settings - ServerUrl: '{updatedSettings.ServerUrl}', ApiKey: '{updatedSettings.ApiKey}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Using official service - clearing custom settings");

                // 使用官方服务时，仅更新官方服务相关配置，保留已有官方 APIKey
                updatedSettings.OfficialServerUrl = string.IsNullOrWhiteSpace(updatedSettings.OfficialServerUrl)
                    ? "https://api.lingualink.aiatechco.com/api/v1/"
                    : updatedSettings.OfficialServerUrl;

                // 将全局 ServerUrl/ApiKey 指向官方服务配置
                updatedSettings.ServerUrl = updatedSettings.OfficialServerUrl;
                updatedSettings.ApiKey = updatedSettings.OfficialApiKey;
            }

            return true;
        }

        private void SaveInternal(bool showConfirmation, string changeSource)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] SaveInternal() called - Source: {changeSource}, Current ApiKey: '{ApiKey}', UseCustomServer: {UseCustomServer}");

            if (!_settingsManager.TryUpdateAndSave(changeSource, UpdateSettingsFromView, out var updatedSettings) || updatedSettings == null)
            {
                return;
            }

            _currentSettings = updatedSettings;

            _hasPendingChanges = false;
            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            System.Diagnostics.Debug.WriteLine($"[AccountPageViewModel] Settings saved, raising SettingsChanged event");

            if (showConfirmation)
            {
                MessageBox.Show(
                    LanguageManager.GetString("SettingsSavedSuccess"),
                    LanguageManager.GetString("SuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void Save()
        {
            SaveInternal(showConfirmation: true, changeSource: "AccountPage");
        }

        #region 登录/登出

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            if (_authService == null)
            {
                MessageBox.Show("认证服务未初始化",
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsLoggingIn = true;
            LoginCommand.NotifyCanExecuteChanged();

            try
            {
                var result = await _authService.LoginAsync();

                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? "登录失败",
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Login exception: {ex.Message}");
                MessageBox.Show(
                    $"登录失败: {ex.Message}",
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoggingIn = false;
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanLogin() => !IsLoggingIn && !IsLoggedIn && _authService != null;

        [RelayCommand(CanExecute = nameof(CanLogout))]
        private async Task LogoutAsync()
        {
            if (_authService == null)
                return;

            var result = MessageBox.Show(
                "确定要退出登录吗？",
                "确认退出",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _authService.LogoutAsync();
                    ApiKeys.Clear();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AccountPageViewModel] Logout exception: {ex.Message}");
                }
            }
        }

        private bool CanLogout() => IsLoggedIn && _authService != null;

        [RelayCommand(CanExecute = nameof(CanRefreshUserProfile))]
        private async Task RefreshUserProfileAsync()
        {
            if (_authService == null)
                return;

            try
            {
                await _authService.RefreshUserProfileAsync();
                UpdateLoginState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Refresh profile exception: {ex.Message}");
            }
        }

        private bool CanRefreshUserProfile() => IsLoggedIn && _authService != null;

        #endregion

        #region API Key 管理

        [RelayCommand]
        private async Task LoadApiKeysAsync()
        {
            if (_authService == null || !IsLoggedIn)
                return;

            IsLoadingApiKeys = true;

            try
            {
                var keys = await _authService.GetApiKeysAsync();
                ApiKeys.Clear();
                foreach (var key in keys)
                {
                    ApiKeys.Add(key);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Load API keys exception: {ex.Message}");
            }
            finally
            {
                IsLoadingApiKeys = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanCreateApiKey))]
        private async Task CreateApiKeyAsync()
        {
            if (_authService == null || !IsLoggedIn)
                return;

            if (string.IsNullOrWhiteSpace(NewApiKeyName))
            {
                MessageBox.Show("请输入 API Key 名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsCreatingApiKey = true;
            CreateApiKeyCommand.NotifyCanExecuteChanged();

            try
            {
                var result = await _authService.CreateApiKeyAsync(NewApiKeyName.Trim());

                if (result.Success && result.KeyInfo != null)
                {
                    // 添加到列表
                    ApiKeys.Insert(0, result.KeyInfo);
                    
                    // 显示完整的 API Key（仅此一次）
                    NewlyCreatedApiKey = result.FullKey;
                    ShowNewApiKeyDialog = true;
                    
                    // 清空输入框
                    NewApiKeyName = string.Empty;
                }
                else
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? "创建 API Key 失败",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Create API key exception: {ex.Message}");
                MessageBox.Show(
                    $"创建 API Key 失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsCreatingApiKey = false;
                CreateApiKeyCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanCreateApiKey() => IsLoggedIn && !IsCreatingApiKey && _authService != null;

        [RelayCommand]
        private async Task DeleteApiKeyAsync(ApiKeyInfo? keyInfo)
        {
            if (_authService == null || keyInfo == null)
                return;

            var result = MessageBox.Show(
                $"确定要删除 API Key \"{keyInfo.Name}\" 吗？\n此操作不可撤销。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                var success = await _authService.DeleteApiKeyAsync(keyInfo.Id);
                if (success)
                {
                    ApiKeys.Remove(keyInfo);
                }
                else
                {
                    MessageBox.Show("删除 API Key 失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Delete API key exception: {ex.Message}");
                MessageBox.Show(
                    $"删除 API Key 失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CopyApiKeyToClipboard(string? key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (ClipboardHelper.TrySetText(key))
            {
                MessageBox.Show("API Key 已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void CloseNewApiKeyDialog()
        {
            ShowNewApiKeyDialog = false;
            NewlyCreatedApiKey = null;
        }

        #endregion

        #region 自定义服务器测试

        [RelayCommand(CanExecute = nameof(CanTestConnection))]
        private async Task TestConnectionAsync()
        {
            IsTestingConnection = true;
            OnPropertyChanged(nameof(ConnectionTestLabel)); // 通知UI更新

            ILingualinkApiService? testApiService = null;
            bool success = false;
            string errorMessage = "An unknown error occurred.";

            try
            {
                // 使用工厂创建临时的API服务实例进行测试
                testApiService = LingualinkApiServiceFactory.CreateTestApiService(ServerUrl, ApiKey);
                success = await testApiService.ValidateConnectionAsync();
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
            }
            finally
            {
                // 确保释放资源
                testApiService?.Dispose();
                IsTestingConnection = false;
                OnPropertyChanged(nameof(ConnectionTestLabel)); // 恢复按钮文本
            }

            if (success)
            {
                MessageBox.Show("Connection successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Connection failed: {errorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanTestConnection()
        {
            return !IsTestingConnection && !string.IsNullOrWhiteSpace(ServerUrl);
        }

        #endregion
    }
}
