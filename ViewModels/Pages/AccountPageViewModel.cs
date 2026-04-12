using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using lingualink_client.Models;
using lingualink_client.Models.Auth;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.ViewModels
{
    public partial class AccountPageViewModel : ViewModelBase, IDisposable
    {
        private readonly ISettingsManager _settingsManager;
        private readonly IAuthService? _authService;
        private AppSettings _currentSettings;
        private bool _isLoadingSettings;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly DispatcherTimer _emailCodeTimer;
        private bool _hasPendingChanges;
        private CancellationTokenSource? _orderPollingCts;
        private CancellationTokenSource? _profileSyncCts;
        private bool _isRestoringPendingOrder;
        private bool _isRecoveringUserProfile;

        public string AuthenticationModeLabel => LanguageManager.GetString("AuthenticationMode");
        public string OfficialServiceLabel => LanguageManager.GetString("OfficialService");
        public string CustomServiceLabel => LanguageManager.GetString("CustomService");
        public string OfficialServiceHint => LanguageManager.GetString("OfficialServiceHint");
        public string CustomServiceHint => LanguageManager.GetString("CustomServiceHint");
        public string UserLoginLabel => LanguageManager.GetString("UserLogin");
        public string UsernameLabel => LanguageManager.GetString("Username");
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
        public string OfficialServiceLoginLabel => LanguageManager.GetString("OfficialServiceLogin");
        public string OfficialServiceSubtitleLabel => LanguageManager.GetString("OfficialServiceSubtitle");
        public string AdvancedOptionsLabel => LanguageManager.GetString("AdvancedOptions");
        public string UseCustomServerLabel => LanguageManager.GetString("UseCustomServer");
        public string UseCustomServerHint => LanguageManager.GetString("UseCustomServerHint");
        public string ConnectionTestLabel => LanguageManager.GetString("ConnectionTest");
        public string RefreshLabel => LanguageManager.GetString("Refresh");
        public string CurrentPlanPrefixLabel => LanguageManager.GetString("AccountCurrentPlanPrefix");
        public string RefreshProfileTooltipLabel => LanguageManager.GetString("AccountRefreshProfileTooltip");
        public string SubscriptionOverviewLabel => LanguageManager.GetString("AccountSubscriptionOverview");
        public string SubscriptionCurrentStatusLabel => LanguageManager.GetString("AccountSubscriptionCurrentStatus");
        public string SubscriptionCurrentPlanLabel => LanguageManager.GetString("AccountSubscriptionCurrentPlan");
        public string SubscriptionRemainingLabel => LanguageManager.GetString("AccountSubscriptionRemaining");
        public string PersonalInfoLabel => LanguageManager.GetString("AccountPersonalInfo");
        public string EditLabel => LanguageManager.GetString("AccountEdit");
        public string UsernamePlaceholderLabel => LanguageManager.GetString("AccountUsernamePlaceholder");
        public string CancelLabel => LanguageManager.GetString("AccountCancel");
        public string AccountStatusLabel => LanguageManager.GetString("AccountStatusLabel");
        public string AccountSecurityLabel => LanguageManager.GetString("AccountSecurity");
        public string EmailLabel => LanguageManager.GetString("AccountEmail");
        public string EmailPlaceholderLabel => LanguageManager.GetString("AccountEmailPlaceholder");
        public string EmailCodeLockHintLabel => LanguageManager.GetString("AccountEmailCodeLockHint");
        public string EmailCodePlaceholderLabel => LanguageManager.GetString("AccountEmailCodePlaceholder");
        public string PasswordPlaceholderLabel => LanguageManager.GetString("AccountPasswordPlaceholder");
        public string ConfirmPasswordPlaceholderLabel => LanguageManager.GetString("AccountConfirmPasswordPlaceholder");
        public string BindEmailLabel => LanguageManager.GetString("AccountBindEmail");
        public string ProviderBindingsLabel => LanguageManager.GetString("AccountProviderBindings");
        public string WechatLabel => LanguageManager.GetString("AccountWechat");
        public string QqLabel => LanguageManager.GetString("AccountQq");
        public string BindQqLabel => LanguageManager.GetString("AccountBindQq");
        public string BindQqTooltipLabel => LanguageManager.GetString("AccountBindQqTooltip");
        public string BindWechatLabel => LanguageManager.GetString("AccountBindWechat");
        public string BindWechatTooltipLabel => LanguageManager.GetString("AccountBindWechatTooltip");
        public string ServerUrlPlaceholderLabel => LanguageManager.GetString("AccountServerUrlPlaceholder");
        public string ApiKeyPlaceholderLabel => LanguageManager.GetString("AccountApiKeyPlaceholder");
        public string ConnectionTestHintLabel => LanguageManager.GetString("AccountConnectionTestHint");
        public string SendEmailCodeButtonText => EmailCodeCountdownSeconds > 0
            ? string.Format(LanguageManager.GetString("BindEmailSendCodeCountdown"), EmailCodeCountdownSeconds)
            : LanguageManager.GetString("BindEmailSendCode");

        [ObservableProperty]
        private bool _isTestingConnection;

        [ObservableProperty]
        private bool _useCustomServer;

        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _loggedInUsername = string.Empty;

        [ObservableProperty]
        private string _serverUrl = string.Empty;

        [ObservableProperty]
        private string _apiKey = string.Empty;

        [ObservableProperty]
        private bool _isLoggingIn;

        [ObservableProperty]
        private UserProfile? _userProfile;

        [ObservableProperty]
        private string _subscriptionStatus = string.Empty;

        [ObservableProperty]
        private string _currentPlanDisplay = string.Empty;

        [ObservableProperty]
        private string _subscriptionRemainingDisplay = string.Empty;

        [ObservableProperty]
        private string _expiryReminder = string.Empty;

        [ObservableProperty]
        private bool _hasActiveSubscription;

        [ObservableProperty]
        private bool _hasPaidSubscriptionPlan;

        [ObservableProperty]
        private bool _isUpdatingUserProfile;

        [ObservableProperty]
        private bool _isEditingUsername;

        [ObservableProperty]
        private bool _isEditingEmail;

        [ObservableProperty]
        private bool _isEditingProvider;

        [ObservableProperty]
        private string _editUsername = string.Empty;

        [ObservableProperty]
        private string _bindEmailInput = string.Empty;

        [ObservableProperty]
        private string _bindEmailCodeInput = string.Empty;

        [ObservableProperty]
        private string _bindEmailPasswordInput = string.Empty;

        [ObservableProperty]
        private string _bindEmailConfirmPasswordInput = string.Empty;

        [ObservableProperty]
        private bool _isEmailCodeSent;

        [ObservableProperty]
        private bool _isSendingEmailCode;

        [ObservableProperty]
        private int _emailCodeCountdownSeconds;

        [ObservableProperty]
        private IReadOnlyList<string> _providerOptions = UserBindProviderCatalog.AllowedProviders;

        [ObservableProperty]
        private string _selectedProvider = UserBindProviderCatalog.AllowedProviders[0];

        [ObservableProperty]
        private string _providerUserIdInput = string.Empty;

        [ObservableProperty]
        private bool _isLoadingPlans;

        [ObservableProperty]
        private bool _isCreatingOrder;

        [ObservableProperty]
        private bool _isPollingOrder;

        [ObservableProperty]
        private IReadOnlyList<SubscriptionPlanInfo> _availablePlans = Array.Empty<SubscriptionPlanInfo>();

        [ObservableProperty]
        private SubscriptionPlanInfo? _selectedPlan;

        [ObservableProperty]
        private IReadOnlyList<PaymentMethodOption> _paymentMethodOptions = Array.Empty<PaymentMethodOption>();

        [ObservableProperty]
        private string _selectedPaymentMethod = "wechat";

        [ObservableProperty]
        private IReadOnlyList<int> _durationMonthOptions = new[] { 1, 3, 6, 12 };

        [ObservableProperty]
        private int _orderDurationMonths = 1;

        [ObservableProperty]
        private string _latestOrderOutTradeNo = string.Empty;

        [ObservableProperty]
        private string _latestOrderStatus = string.Empty;

        [ObservableProperty]
        private string _latestOrderAmountDisplay = string.Empty;

        [ObservableProperty]
        private string _latestOrderExpireAtDisplay = string.Empty;

        [ObservableProperty]
        private string _latestOrderMessage = string.Empty;

        [ObservableProperty]
        private string _latestOrderProvider = string.Empty;

        [ObservableProperty]
        private string _latestOrderIntegrationStatus = string.Empty;

        [ObservableProperty]
        private string _latestOrderCodeUrl = string.Empty;

        [ObservableProperty]
        private BitmapImage? _latestOrderQrImage;

        public bool HasPlans => AvailablePlans.Count > 0;
        public bool HasUserProfile => UserProfile != null;
        public bool HasLatestOrder => !string.IsNullOrWhiteSpace(LatestOrderOutTradeNo);
        public bool HasLatestOrderMessage => !string.IsNullOrWhiteSpace(LatestOrderMessage);
        public bool HasLatestOrderExpireAt => !string.IsNullOrWhiteSpace(LatestOrderExpireAtDisplay);
        public bool HasLatestOrderProvider => !string.IsNullOrWhiteSpace(LatestOrderProvider);
        public bool HasLatestOrderIntegrationStatus => !string.IsNullOrWhiteSpace(LatestOrderIntegrationStatus);
        public bool HasLatestOrderCodeUrl => !string.IsNullOrWhiteSpace(LatestOrderCodeUrl);
        public bool HasLatestOrderQrImage => LatestOrderQrImage != null;
        public bool HasPendingOrder => HasLatestOrder && string.Equals(LatestOrderStatus, "pending", StringComparison.OrdinalIgnoreCase);
        public string VipActionButtonText => HasPaidSubscriptionPlan
            ? LanguageManager.GetString("AccountVipActionRenewUpgrade")
            : LanguageManager.GetString("AccountVipActionOpen");
        public string UsernameDisplay => string.IsNullOrWhiteSpace(UserProfile?.Username) ? "-" : UserProfile!.Username!;
        public string EmailDisplay => string.IsNullOrWhiteSpace(UserProfile?.Email)
            ? LanguageManager.GetString("AccountUnbound")
            : UserProfile!.Email!;
        public string EmailActionButtonText => string.IsNullOrWhiteSpace(UserProfile?.Email)
            ? LanguageManager.GetString("AccountBind")
            : LanguageManager.GetString("AccountRebind");
        public string UserStatusDisplay => MapUserStatus(UserProfile?.Status);
        public bool IsWechatBound => UserProfile?.SocialBindings?.Wechat?.Bound == true;
        public bool IsQqBound => UserProfile?.SocialBindings?.Qq?.Bound == true;
        public string WechatBindingStatusDisplay => BuildSocialBindingStatusDisplay(UserProfile?.SocialBindings?.Wechat);
        public string QqBindingStatusDisplay => BuildSocialBindingStatusDisplay(UserProfile?.SocialBindings?.Qq);

        public AccountPageViewModel()
            : this(CreateSettingsManager(), TryResolveAuthService())
        {
        }

        public AccountPageViewModel(ISettingsManager settingsManager, IAuthService? authService = null)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _authService = authService;
            _currentSettings = _settingsManager.LoadSettings();
            RefreshLocalizedOptions();
            LoadSettingsFromModel(_currentSettings);

            LanguageManager.LanguageChanged += OnLanguageChanged;

            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _autoSaveTimer.Tick += AutoSaveTimerOnTick;

            _emailCodeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _emailCodeTimer.Tick += EmailCodeTimerOnTick;

            PropertyChanged += OnAccountPropertyChanged;

            if (_authService != null)
            {
                _authService.LoginStateChanged += OnAuthServiceLoginStateChanged;
                UpdateLoginState();
            }
        }

        private static ISettingsManager CreateSettingsManager()
        {
            return ServiceContainer.TryResolve<ISettingsManager>(out var settingsManager) && settingsManager != null
                ? settingsManager
                : new SettingsManager();
        }

        private static IAuthService? TryResolveAuthService()
        {
            return ServiceContainer.TryResolve<IAuthService>(out var authService) ? authService : null;
        }

        private static string BuildSocialBindingStatusDisplay(SocialBindingInfo? binding)
        {
            if (binding?.Bound != true)
            {
                return LanguageManager.GetString("AccountUnbound");
            }

            var masked = binding.AccountMasked?.Trim();
            if (string.IsNullOrWhiteSpace(masked))
            {
                return LanguageManager.GetString("AccountBound");
            }

            return string.Format(LanguageManager.GetString("AccountBoundMaskedFormat"), masked);
        }

        partial void OnServerUrlChanged(string value)
        {
            Debug.WriteLine($"[AccountPageViewModel] ServerUrl property changed to: '{value}'");
            TestConnectionCommand.NotifyCanExecuteChanged();
        }

        partial void OnApiKeyChanged(string value)
        {
            Debug.WriteLine($"[AccountPageViewModel] ApiKey property changed. HasValue: {!string.IsNullOrWhiteSpace(value)}");
        }

        partial void OnUserProfileChanged(UserProfile? value)
        {
            OnPropertyChanged(nameof(HasUserProfile));
            OnPropertyChanged(nameof(UsernameDisplay));
            OnPropertyChanged(nameof(EmailDisplay));
            OnPropertyChanged(nameof(EmailActionButtonText));
            OnPropertyChanged(nameof(UserStatusDisplay));
            OnPropertyChanged(nameof(IsWechatBound));
            OnPropertyChanged(nameof(IsQqBound));
            OnPropertyChanged(nameof(WechatBindingStatusDisplay));
            OnPropertyChanged(nameof(QqBindingStatusDisplay));
            SyncProfileEditorWithUser(value);
            RefreshProfileEditingCommandStates();
        }

        partial void OnEditUsernameChanged(string value)
        {
            UpdateUserProfileCommand.NotifyCanExecuteChanged();
            SaveUsernameCommand.NotifyCanExecuteChanged();
        }

        partial void OnBindEmailInputChanged(string value)
        {
            SendEmailCodeCommand.NotifyCanExecuteChanged();
            SaveEmailCommand.NotifyCanExecuteChanged();
        }

        partial void OnBindEmailCodeInputChanged(string value)
        {
            SaveEmailCommand.NotifyCanExecuteChanged();
        }

        partial void OnBindEmailPasswordInputChanged(string value)
        {
            SaveEmailCommand.NotifyCanExecuteChanged();
        }

        partial void OnBindEmailConfirmPasswordInputChanged(string value)
        {
            SaveEmailCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsEmailCodeSentChanged(bool value)
        {
            OnPropertyChanged(nameof(SendEmailCodeButtonText));
            SendEmailCodeCommand.NotifyCanExecuteChanged();
            SaveEmailCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsSendingEmailCodeChanged(bool value)
        {
            OnPropertyChanged(nameof(SendEmailCodeButtonText));
            SendEmailCodeCommand.NotifyCanExecuteChanged();
            SaveEmailCommand.NotifyCanExecuteChanged();
        }

        partial void OnEmailCodeCountdownSecondsChanged(int value)
        {
            OnPropertyChanged(nameof(SendEmailCodeButtonText));
            SendEmailCodeCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedProviderChanged(string value)
        {
            BindProviderCommand.NotifyCanExecuteChanged();
        }

        partial void OnProviderUserIdInputChanged(string value)
        {
            BindProviderCommand.NotifyCanExecuteChanged();
            SaveProviderCommand.NotifyCanExecuteChanged();
        }

        partial void OnAvailablePlansChanged(IReadOnlyList<SubscriptionPlanInfo> value)
        {
            OnPropertyChanged(nameof(HasPlans));
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedPlanChanged(SubscriptionPlanInfo? value)
        {
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnOrderDurationMonthsChanged(int value)
        {
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedPaymentMethodChanged(string value)
        {
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnLatestOrderOutTradeNoChanged(string value)
        {
            OnPropertyChanged(nameof(HasLatestOrder));
            OnPropertyChanged(nameof(HasPendingOrder));
            QueryOrderStatusCommand.NotifyCanExecuteChanged();
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnLatestOrderStatusChanged(string value)
        {
            OnPropertyChanged(nameof(HasPendingOrder));
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnLatestOrderMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasLatestOrderMessage));
        }

        partial void OnLatestOrderExpireAtDisplayChanged(string value)
        {
            OnPropertyChanged(nameof(HasLatestOrderExpireAt));
        }

        partial void OnLatestOrderProviderChanged(string value)
        {
            OnPropertyChanged(nameof(HasLatestOrderProvider));
        }

        partial void OnLatestOrderIntegrationStatusChanged(string value)
        {
            OnPropertyChanged(nameof(HasLatestOrderIntegrationStatus));
        }

        partial void OnLatestOrderCodeUrlChanged(string value)
        {
            OnPropertyChanged(nameof(HasLatestOrderCodeUrl));
            LatestOrderQrImage = BuildQrCodeImage(value);
        }

        partial void OnLatestOrderQrImageChanged(BitmapImage? value)
        {
            OnPropertyChanged(nameof(HasLatestOrderQrImage));
        }

        partial void OnIsLoadingPlansChanged(bool value)
        {
            LoadSubscriptionPlansCommand.NotifyCanExecuteChanged();
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsCreatingOrderChanged(bool value)
        {
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
            QueryOrderStatusCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsPollingOrderChanged(bool value)
        {
            StopOrderPollingCommand.NotifyCanExecuteChanged();
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsUpdatingUserProfileChanged(bool value)
        {
            RefreshProfileEditingCommandStates();
        }

        partial void OnHasPaidSubscriptionPlanChanged(bool value)
        {
            OnPropertyChanged(nameof(VipActionButtonText));
        }

        private void RefreshLoginCommandStates()
        {
            LoginCommand.NotifyCanExecuteChanged();
            LogoutCommand.NotifyCanExecuteChanged();
        }

        private void RefreshProfileEditingCommandStates()
        {
            UpdateUserProfileCommand.NotifyCanExecuteChanged();
            SendEmailCodeCommand.NotifyCanExecuteChanged();
            BindProviderCommand.NotifyCanExecuteChanged();
            BindQqProviderCommand.NotifyCanExecuteChanged();
            BindWechatProviderCommand.NotifyCanExecuteChanged();
            BeginEditUsernameCommand.NotifyCanExecuteChanged();
            BeginEditEmailCommand.NotifyCanExecuteChanged();
            BeginEditProviderCommand.NotifyCanExecuteChanged();
            SaveUsernameCommand.NotifyCanExecuteChanged();
            SaveEmailCommand.NotifyCanExecuteChanged();
            SaveProviderCommand.NotifyCanExecuteChanged();
        }

        private void RefreshSubscriptionCommandStates()
        {
            OpenSubscriptionDialogCommand.NotifyCanExecuteChanged();
            LoadSubscriptionPlansCommand.NotifyCanExecuteChanged();
            CreateSubscriptionOrderCommand.NotifyCanExecuteChanged();
            QueryOrderStatusCommand.NotifyCanExecuteChanged();
            StopOrderPollingCommand.NotifyCanExecuteChanged();
        }

        private void RefreshAccountCommandStates()
        {
            RefreshLoginCommandStates();
            RefreshProfileEditingCommandStates();
            RefreshSubscriptionCommandStates();
            RefreshUserProfileCommand.NotifyCanExecuteChanged();
            TestConnectionCommand.NotifyCanExecuteChanged();
        }

        private void EmailCodeTimerOnTick(object? sender, EventArgs e)
        {
            if (EmailCodeCountdownSeconds <= 1)
            {
                EmailCodeCountdownSeconds = 0;
                _emailCodeTimer.Stop();
                return;
            }

            EmailCodeCountdownSeconds--;
        }

        private void OnAuthServiceLoginStateChanged(object? sender, bool isLoggedIn)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(UpdateLoginState);
        }

        private void UpdateLoginState()
        {
            if (_authService == null)
            {
                IsLoggedIn = false;
                IsEditingUsername = false;
                IsEditingEmail = false;
                ResetBindEmailState();
                IsEditingProvider = false;
                ClearPlans();
                StopOrderPollingInternal();
                ResetOrderState();
                RefreshAccountCommandStates();
                return;
            }

            IsLoggedIn = _authService.IsLoggedIn;
            UserProfile = _authService.CurrentUser;

            if (UserProfile != null)
            {
                LoggedInUsername = !string.IsNullOrWhiteSpace(UserProfile.Username)
                    ? UserProfile.Username
                    : UserProfile.Email ?? UserProfile.Id ?? LanguageManager.GetString("AccountDefaultUserName");

                UpdateSubscriptionPresentation(UserProfile.Subscription);
            }
            else
            {
                IsEditingUsername = false;
                IsEditingEmail = false;
                ResetBindEmailState();
                IsEditingProvider = false;
                LoggedInUsername = string.Empty;
                ResetSubscriptionPresentation();
                ClearPlans();
                StopOrderPollingInternal();
                ResetOrderState();

                if (IsLoggedIn)
                {
                    _ = EnsureUserProfileLoadedAsync();
                }
            }

            RefreshAccountCommandStates();
        }

        private async Task EnsureUserProfileLoadedAsync()
        {
            if (_authService == null || !IsLoggedIn || UserProfile != null)
            {
                return;
            }

            await TrySyncUserProfileAsync(
                maxAttempts: 2,
                retryDelay: TimeSpan.FromSeconds(2),
                allowWhenLoggedOut: false);
        }

        public async Task EnsureProfileFreshOnPageEnterAsync()
        {
            if (_authService == null || UserProfile != null)
            {
                return;
            }

            await TrySyncUserProfileAsync(
                maxAttempts: 4,
                retryDelay: TimeSpan.FromSeconds(2),
                allowWhenLoggedOut: true);
        }

        public void CancelPendingProfileSync()
        {
            try
            {
                _profileSyncCts?.Cancel();
                _profileSyncCts?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _profileSyncCts = null;
            }
        }

        private async Task TrySyncUserProfileAsync(int maxAttempts, TimeSpan retryDelay, bool allowWhenLoggedOut)
        {
            if (_authService == null || UserProfile != null || _isRecoveringUserProfile || maxAttempts <= 0)
            {
                return;
            }

            CancelPendingProfileSync();
            var cts = new CancellationTokenSource();
            _profileSyncCts = cts;
            var token = cts.Token;

            _isRecoveringUserProfile = true;
            try
            {
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    token.ThrowIfCancellationRequested();

                    if (!IsLoggedIn)
                    {
                        if (!allowWhenLoggedOut)
                        {
                            return;
                        }

                        await _authService.TryRestoreSessionAsync();
                        UpdateLoginState();
                    }

                    await _authService.RefreshUserProfileAsync();
                    UpdateLoginState();

                    if (UserProfile != null)
                    {
                        return;
                    }

                    if (attempt < maxAttempts - 1)
                    {
                        await Task.Delay(retryDelay, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] TrySyncUserProfileAsync exception: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_profileSyncCts, cts))
                {
                    CancelPendingProfileSync();
                }

                _isRecoveringUserProfile = false;
            }
        }

        private void UpdateSubscriptionPresentation(SubscriptionInfo? subscription)
        {
            if (subscription == null)
            {
                ResetSubscriptionPresentation();
                return;
            }

            HasActiveSubscription = subscription.IsPaidActiveNow;
            HasPaidSubscriptionPlan = subscription.IsPaidPlan;
            CurrentPlanDisplay = subscription.DisplayPlanName;
            SubscriptionStatus = BuildSubscriptionStatusText(subscription);
            SubscriptionRemainingDisplay = BuildSubscriptionRemainingText(subscription);
            ExpiryReminder = BuildExpiryReminderText(subscription);
        }

        private void ResetSubscriptionPresentation()
        {
            HasActiveSubscription = false;
            HasPaidSubscriptionPlan = false;
            SubscriptionStatus = LanguageManager.GetString("AccountSubscriptionStatusNotOpened");
            CurrentPlanDisplay = LanguageManager.GetString("AccountPlanUnsubscribed");
            SubscriptionRemainingDisplay = LanguageManager.GetString("AccountPlanUnsubscribed");
            ExpiryReminder = LanguageManager.GetString("AccountSubscriptionPromptNoActiveSubscription");
        }

        private static string BuildSubscriptionStatusText(SubscriptionInfo subscription)
        {
            if (!subscription.IsPaidPlan)
            {
                return LanguageManager.GetString("AccountPlanUnsubscribed");
            }

            if (subscription.IsPaidActiveNow)
            {
                return LanguageManager.GetString("AccountSubscriptionStatusActive");
            }

            var now = DateTime.UtcNow;
            if (subscription.StartDate.HasValue && subscription.StartDate.Value.ToUniversalTime() > now)
            {
                return LanguageManager.GetString("AccountSubscriptionStatusPending");
            }

            if (subscription.EffectiveEndDate.HasValue && subscription.EffectiveEndDate.Value.ToUniversalTime() < now)
            {
                return LanguageManager.GetString("AccountSubscriptionStatusExpired");
            }

            return string.IsNullOrWhiteSpace(subscription.Status)
                ? LanguageManager.GetString("AccountSubscriptionStatusNotOpened")
                : subscription.Status;
        }

        private static string BuildExpiryReminderText(SubscriptionInfo subscription)
        {
            if (!subscription.IsPaidPlan)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptNoSubscription");
            }

            if (subscription.AutoRenew && subscription.IsPaidActiveNow)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptAutoRenew");
            }

            var endDate = subscription.EffectiveEndDate;
            if (!endDate.HasValue)
            {
                return subscription.IsPaidActiveNow
                    ? LanguageManager.GetString("AccountSubscriptionPromptUnavailable")
                    : LanguageManager.GetString("AccountSubscriptionPromptNoActiveSubscription");
            }

            var daysRemaining = (endDate.Value.ToLocalTime().Date - DateTime.Now.Date).Days;
            if (daysRemaining < 0)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptExpired");
            }

            if (!subscription.IsPaidActiveNow)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptPending");
            }

            if (daysRemaining == 0)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptExpireToday");
            }

            if (daysRemaining <= 7)
            {
                return string.Format(LanguageManager.GetString("AccountSubscriptionPromptExpireInDaysFormat"), daysRemaining);
            }

            return string.Empty;
        }

        private static string BuildSubscriptionRemainingText(SubscriptionInfo subscription)
        {
            if (!subscription.IsPaidPlan)
            {
                return LanguageManager.GetString("AccountPlanUnsubscribed");
            }

            var endDate = subscription.EffectiveEndDate;
            if (!endDate.HasValue)
            {
                return subscription.IsPaidActiveNow
                    ? LanguageManager.GetString("AccountRemainingUnknown")
                    : LanguageManager.GetString("AccountRemainingPending");
            }

            var now = DateTime.UtcNow;
            var endUtc = endDate.Value.ToUniversalTime();
            var remaining = endUtc - now;
            if (remaining <= TimeSpan.Zero)
            {
                return LanguageManager.GetString("AccountRemainingExpired");
            }

            if (remaining.TotalDays >= 1)
            {
                return string.Format(LanguageManager.GetString("AccountRemainingDaysFormat"), Math.Floor(remaining.TotalDays));
            }

            if (remaining.TotalHours >= 1)
            {
                return string.Format(LanguageManager.GetString("AccountRemainingHoursFormat"), Math.Floor(remaining.TotalHours));
            }

            return string.Format(LanguageManager.GetString("AccountRemainingMinutesFormat"), Math.Max(1, Math.Floor(remaining.TotalMinutes)));
        }

        private static string FormatDate(DateTime value)
        {
            return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static string MapUserStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return LanguageManager.GetString("AccountStatusUnknown");
            }

            return status.Trim().ToLowerInvariant() switch
            {
                "active" => LanguageManager.GetString("AccountStatusNormal"),
                "inactive" => LanguageManager.GetString("AccountStatusInactive"),
                "disabled" => LanguageManager.GetString("AccountStatusDisabled"),
                _ => status
            };
        }

        private static bool TryValidateUsernameInput(string? value, out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = LanguageManager.GetString("AccountUsernameRequired");
                return false;
            }

            if (value.Length > 100)
            {
                errorMessage = LanguageManager.GetString("AccountUsernameTooLong");
                return false;
            }

            foreach (var c in value)
            {
                if (c == '/')
                {
                    errorMessage = LanguageManager.GetString("AccountUsernameSlashInvalid");
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private void OnLanguageChanged()
        {
            RefreshLocalizedOptions();
            OnPropertyChanged(string.Empty);
        }

        private void RefreshLocalizedOptions()
        {
            PaymentMethodOptions = new[]
            {
                new PaymentMethodOption("wechat", LanguageManager.GetString("AccountPaymentMethodWechat")),
                new PaymentMethodOption("alipay", LanguageManager.GetString("AccountPaymentMethodAlipayReserved"))
            };
        }

        public void Dispose()
        {
            PropertyChanged -= OnAccountPropertyChanged;
            LanguageManager.LanguageChanged -= OnLanguageChanged;

            if (_authService != null)
            {
                _authService.LoginStateChanged -= OnAuthServiceLoginStateChanged;
            }

            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            if (_emailCodeTimer.IsEnabled)
            {
                _emailCodeTimer.Stop();
            }

            _autoSaveTimer.Tick -= AutoSaveTimerOnTick;
            _emailCodeTimer.Tick -= EmailCodeTimerOnTick;

            CancelPendingProfileSync();
            StopOrderPollingInternal();
        }
    }
}
