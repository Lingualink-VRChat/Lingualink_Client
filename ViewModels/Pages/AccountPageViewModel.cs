using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Models.Auth;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Views;
using MessageBox = lingualink_client.Services.MessageBox;

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
        private bool _isRecoveringUserProfile;

        public string AuthenticationModeLabel => LanguageManager.GetString("AuthenticationMode");
        public string OfficialServiceLabel => LanguageManager.GetString("OfficialService");
        public string OfficialServiceHint => LanguageManager.GetString("OfficialServiceHint");
        public string UserLoginLabel => LanguageManager.GetString("UserLogin");
        public string UsernameLabel => LanguageManager.GetString("Username");
        public string LoginLabel => LanguageManager.GetString("Login");
        public string LogoutLabel => LanguageManager.GetString("Logout");
        public string LoginStatusLabel => LanguageManager.GetString("LoginStatus");
        public string NotLoggedInLabel => LanguageManager.GetString("NotLoggedIn");
        public string ComingSoonLabel => LanguageManager.GetString("ComingSoon");
        public string SaveLabel => LanguageManager.GetString("Save");
        public string RevertLabel => LanguageManager.GetString("Revert");
        public string OfficialServiceLoginLabel => LanguageManager.GetString("OfficialServiceLogin");
        public string OfficialServiceSubtitleLabel => LanguageManager.GetString("OfficialServiceSubtitle");
        public string RefreshLabel => LanguageManager.GetString("Refresh");
        public string CurrentPlanPrefixLabel => LanguageManager.GetString("AccountCurrentPlanPrefix");
        public string RefreshProfileTooltipLabel => LanguageManager.GetString("AccountRefreshProfileTooltip");
        public string SubscriptionOverviewLabel => LanguageManager.GetString("AccountSubscriptionOverview");
        public string WalletSectionLabel => LanguageManager.GetString("AccountWalletSection");
        public string WalletBalanceLabel => LanguageManager.GetString("AccountWalletBalance");
        public string WalletRechargeLabel => LanguageManager.GetString("AccountWalletRecharge");
        public string WalletAutoRenewLabel => LanguageManager.GetString("AccountWalletAutoRenew");
        public string WalletEnableAutoRenewLabel => LanguageManager.GetString("AccountWalletEnableAutoRenew");
        public string WalletDisableAutoRenewLabel => LanguageManager.GetString("AccountWalletDisableAutoRenew");
        public string WalletRenewalWarningLabel => LanguageManager.GetString("AccountWalletRenewalWarning");
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
        public string SendEmailCodeButtonText => EmailCodeCountdownSeconds > 0
            ? string.Format(LanguageManager.GetString("BindEmailSendCodeCountdown"), EmailCodeCountdownSeconds)
            : LanguageManager.GetString("BindEmailSendCode");

        // 官方服务登录相关属性
        [ObservableProperty]
        private bool _isLoggedIn;

        [ObservableProperty]
        private string _loggedInUsername = string.Empty;

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
        private int _walletBalanceCents;

        [ObservableProperty]
        private bool _isLoadingWallet;

        [ObservableProperty]
        private bool _isUpdatingAutoRenew;

        [ObservableProperty]
        private string _renewalWarning = string.Empty;

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
        public string UserStatusDisplay => AccountSubscriptionPresentation.MapUserStatus(UserProfile?.Status);
        public bool IsWechatBound => UserProfile?.SocialBindings?.Wechat?.Bound == true;
        public bool IsQqBound => UserProfile?.SocialBindings?.Qq?.Bound == true;
        public string WechatBindingStatusDisplay => AccountBindingDisplayFormatter.BuildSocialBindingStatusDisplay(UserProfile?.SocialBindings?.Wechat);
        public string QqBindingStatusDisplay => AccountBindingDisplayFormatter.BuildSocialBindingStatusDisplay(UserProfile?.SocialBindings?.Qq);
        public string WalletBalanceDisplay => AccountSubscriptionPresentation.FormatCurrencyAmount(WalletBalanceCents);
        public string AutoRenewStatusDisplay => UserProfile?.Subscription?.AutoRenew == true
            ? LanguageManager.GetString("AccountWalletAutoRenewEnabledStatus")
            : LanguageManager.GetString("AccountWalletAutoRenewDisabledStatus");
        public string AutoRenewActionText => UserProfile?.Subscription?.AutoRenew == true ? WalletDisableAutoRenewLabel : WalletEnableAutoRenewLabel;
        public bool HasRenewalWarning => !string.IsNullOrWhiteSpace(RenewalWarning);

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

        private void SyncProfileEditorWithUser(UserProfile? profile)
        {
            if (profile == null)
            {
                EditUsername = string.Empty;
                return;
            }

            EditUsername = profile.Username ?? string.Empty;
        }

        private void ResetBindEmailState()
        {
            _emailCodeTimer.Stop();
            BindEmailInput = string.Empty;
            BindEmailCodeInput = string.Empty;
            BindEmailPasswordInput = string.Empty;
            BindEmailConfirmPasswordInput = string.Empty;
            IsEmailCodeSent = false;
            IsSendingEmailCode = false;
            EmailCodeCountdownSeconds = 0;
        }

        private bool HasValidBindEmailPassword()
        {
            return AccountProfileInputValidator.HasValidBindEmailPassword(
                BindEmailPasswordInput,
                BindEmailConfirmPasswordInput);
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
            OnPropertyChanged(nameof(AutoRenewStatusDisplay));
            OnPropertyChanged(nameof(AutoRenewActionText));
            SyncProfileEditorWithUser(value);
            RefreshProfileEditingCommandStates();
            RechargeWalletCommand.NotifyCanExecuteChanged();
            ToggleAutoRenewCommand.NotifyCanExecuteChanged();
        }

        partial void OnWalletBalanceCentsChanged(int value)
        {
            OnPropertyChanged(nameof(WalletBalanceDisplay));
            RechargeWalletCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsLoadingWalletChanged(bool value)
        {
            RechargeWalletCommand.NotifyCanExecuteChanged();
            ToggleAutoRenewCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsUpdatingAutoRenewChanged(bool value)
        {
            ToggleAutoRenewCommand.NotifyCanExecuteChanged();
        }

        partial void OnRenewalWarningChanged(string value)
        {
            OnPropertyChanged(nameof(HasRenewalWarning));
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
            LatestOrderQrImage = AccountQrCodeImageBuilder.Build(value);
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
            RechargeWalletCommand.NotifyCanExecuteChanged();
            ToggleAutoRenewCommand.NotifyCanExecuteChanged();
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
            SubscriptionStatus = AccountSubscriptionPresentation.BuildSubscriptionStatusText(subscription);
            SubscriptionRemainingDisplay = AccountSubscriptionPresentation.BuildSubscriptionRemainingText(subscription);
            ExpiryReminder = AccountSubscriptionPresentation.BuildExpiryReminderText(subscription);
            RenewalWarning = subscription.RenewalWarning ?? string.Empty;
        }

        private void ResetSubscriptionPresentation()
        {
            HasActiveSubscription = false;
            HasPaidSubscriptionPlan = false;
            SubscriptionStatus = LanguageManager.GetString("AccountSubscriptionStatusNotOpened");
            CurrentPlanDisplay = LanguageManager.GetString("AccountPlanUnsubscribed");
            SubscriptionRemainingDisplay = LanguageManager.GetString("AccountPlanUnsubscribed");
            ExpiryReminder = LanguageManager.GetString("AccountSubscriptionPromptNoActiveSubscription");
            RenewalWarning = string.Empty;
        }

        private void ResetWalletPresentation()
        {
            WalletBalanceCents = 0;
            IsLoadingWallet = false;
        }

        private async Task RefreshWalletAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                ResetWalletPresentation();
                return;
            }

            IsLoadingWallet = true;
            try
            {
                var wallet = await _authService.GetWalletAsync();
                WalletBalanceCents = wallet?.BalanceCents ?? 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Refresh wallet exception: {ex.Message}");
            }
            finally
            {
                IsLoadingWallet = false;
            }
        }

        private void ClearPlans()
        {
            AvailablePlans = Array.Empty<SubscriptionPlanInfo>();
            SelectedPlan = null;
        }

        private void ResetOrderState()
        {
            LatestOrderOutTradeNo = string.Empty;
            LatestOrderStatus = string.Empty;
            LatestOrderAmountDisplay = string.Empty;
            LatestOrderExpireAtDisplay = string.Empty;
            LatestOrderProvider = string.Empty;
            LatestOrderIntegrationStatus = string.Empty;
            LatestOrderCodeUrl = string.Empty;
            LatestOrderQrImage = null;
            LatestOrderMessage = string.Empty;
        }

        private void UpdateOrderPresentation(SubscriptionOrderInfo order, PaymentInstructionInfo? payment = null)
        {
            LatestOrderOutTradeNo = order.OutTradeNo ?? string.Empty;
            LatestOrderStatus = string.IsNullOrWhiteSpace(order.Status) ? "unknown" : order.Status;
            LatestOrderAmountDisplay = order.AmountDisplay;
            LatestOrderExpireAtDisplay = order.ExpireAt.HasValue
                ? AccountSubscriptionPresentation.FormatDate(order.ExpireAt.Value)
                : string.Empty;

            if (payment != null)
            {
                LatestOrderProvider = payment.Provider ?? string.Empty;
                LatestOrderIntegrationStatus = payment.IntegrationStatus ?? string.Empty;
                LatestOrderCodeUrl = payment.CodeUrl?.Trim() ?? string.Empty;

                if (!order.ExpireAt.HasValue && payment.OrderExpireAt.HasValue)
                {
                    LatestOrderExpireAtDisplay = AccountSubscriptionPresentation.FormatDate(payment.OrderExpireAt.Value);
                }

                if (!string.IsNullOrWhiteSpace(payment.Message))
                {
                    LatestOrderMessage = payment.Message;
                }
            }
        }

        private void PersistPendingOrderOutTradeNo(string outTradeNo)
        {
            var normalized = outTradeNo?.Trim() ?? string.Empty;
            var current = _currentSettings.PendingSubscriptionOrderOutTradeNo?.Trim() ?? string.Empty;

            if (string.Equals(normalized, current, StringComparison.Ordinal))
            {
                return;
            }

            if (_settingsManager.TryUpdateAndSave(
                    "AccountPagePendingOrder",
                    settings =>
                    {
                        settings.PendingSubscriptionOrderOutTradeNo = normalized;
                        return true;
                    },
                    out var updated)
                && updated != null)
            {
                _currentSettings = updated;
            }
        }

        private void ClearPendingOrderOutTradeNo()
        {
            PersistPendingOrderOutTradeNo(string.Empty);
        }

        private static bool IsTerminalOrderStatus(string status)
        {
            return AccountOrderStatus.IsTerminal(status);
        }

        private async Task ApplyTerminalOrderStateAsync(SubscriptionOrderInfo order, bool refreshSubscriptionIfPaid)
        {
            if (string.IsNullOrWhiteSpace(order.Status))
            {
                return;
            }

            var normalized = order.Status.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "paid":
                    LatestOrderMessage = LanguageManager.GetString("AccountOrderPaidRefreshing");
                    if (refreshSubscriptionIfPaid)
                    {
                        await RefreshUserProfileAsync();
                    }
                    break;
                case "failed":
                    LatestOrderMessage = LanguageManager.GetString("AccountOrderFailed");
                    break;
                case "expired":
                    LatestOrderMessage = LanguageManager.GetString("AccountOrderExpired");
                    break;
                case "cancelled":
                case "canceled":
                    LatestOrderMessage = LanguageManager.GetString("AccountOrderCancelled");
                    break;
            }
        }

        private void StopOrderPollingInternal()
        {
            try
            {
                _orderPollingCts?.Cancel();
                _orderPollingCts?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _orderPollingCts = null;
                IsPollingOrder = false;
            }
        }

        private async Task StartOrderPollingAsync(string outTradeNo)
        {
            StopOrderPollingInternal();

            var cts = new CancellationTokenSource();
            _orderPollingCts = cts;
            var token = cts.Token;
            IsPollingOrder = true;

            try
            {
                for (var i = 0; i < 60; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var order = await QueryOrderStatusInternalAsync(outTradeNo, showErrorMessage: false);
                    if (order != null && IsTerminalOrderStatus(order.Status))
                    {
                        await ApplyTerminalOrderStateAsync(order, refreshSubscriptionIfPaid: true);
                        return;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }

                LatestOrderMessage = LanguageManager.GetString("AccountOrderPollingTimeout");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_orderPollingCts, cts))
                {
                    StopOrderPollingInternal();
                }
            }
        }

        private async Task<SubscriptionOrderInfo?> QueryOrderStatusInternalAsync(string outTradeNo, bool showErrorMessage)
        {
            if (_authService == null || !IsLoggedIn || string.IsNullOrWhiteSpace(outTradeNo))
            {
                return null;
            }

            var order = await _authService.GetSubscriptionOrderStatusAsync(outTradeNo);
            if (order == null)
            {
                if (showErrorMessage)
                {
                    MessageBox.Show(
                        LanguageManager.GetString("AccountOrderQueryFailed"),
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return null;
            }

            UpdateOrderPresentation(order);

            if (string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                PersistPendingOrderOutTradeNo(order.OutTradeNo);
            }
            else if (IsTerminalOrderStatus(order.Status))
            {
                ClearPendingOrderOutTradeNo();
            }

            return order;
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

        private void LoadSettingsFromModel(AppSettings settings)
        {
            _isLoadingSettings = true;
            try
            {
                _currentSettings = settings;
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
            return false;
        }

        private bool UpdateSettingsFromView(AppSettings updatedSettings)
        {
            Debug.WriteLine("[AccountPageViewModel] ValidateAndBuildSettings() called");
            Debug.WriteLine($"[AccountPageViewModel] Loaded latest settings base - ActiveServerUrl: '{updatedSettings.ActiveServerUrl}'");
            return true;
        }

        private void SaveInternal(bool showConfirmation, string changeSource)
        {
            Debug.WriteLine($"[AccountPageViewModel] SaveInternal() called - Source: {changeSource}");

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

            Debug.WriteLine("[AccountPageViewModel] Settings saved, raising SettingsChanged event");

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
                MessageBox.Show(LanguageManager.GetString("AccountAuthServiceUnavailable"),
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
                    if (result.IsCancelled)
                    {
                        return;
                    }

                    MessageBox.Show(
                        result.ErrorMessage ?? LanguageManager.GetString("AccountLoginFailed"),
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Login exception: {ex.Message}");
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("AccountLoginFailedFormat"), ex.Message),
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
            {
                return;
            }

            var result = MessageBox.Show(
                LanguageManager.GetString("AccountLogoutConfirmMessage"),
                LanguageManager.GetString("AccountLogoutConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    StopOrderPollingInternal();
                    ClearPendingOrderOutTradeNo();
                    await _authService.LogoutAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AccountPageViewModel] Logout exception: {ex.Message}");
                }
            }
        }

        private bool CanLogout() => IsLoggedIn && _authService != null;

        [RelayCommand(CanExecute = nameof(CanOpenSubscriptionDialog))]
        private async Task OpenSubscriptionDialogAsync()
        {
            if (!IsLoggedIn || _authService == null)
            {
                MessageBox.Show(
                    LanguageManager.GetString("AccountVipRequireLogin"),
                    LanguageManager.GetString("InfoTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var accessToken = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                MessageBox.Show(
                    LanguageManager.GetString("AccountSubscriptionReloginRequired"),
                    LanguageManager.GetString("WarningTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var checkoutWindow = new CheckoutWindow(accessToken, AccountCheckoutHostResolver.Resolve(_authService.AuthServerUrl))
            {
                Owner = Application.Current.MainWindow
            };

            var paymentCompleted = false;
            var paidOutTradeNo = string.Empty;
            checkoutWindow.PaymentCompleted += outTradeNo =>
            {
                paymentCompleted = true;
                paidOutTradeNo = outTradeNo ?? string.Empty;
            };

            checkoutWindow.ShowDialog();

            // 无论是否捕获到 postMessage，都刷新一次资料；
            // 部分 Web 环境只触发 window.close，不会直达 WebMessageReceived。
            await RefreshUserProfileAsync();
            await RefreshWalletAsync();
            if (!paymentCompleted)
            {
                await Task.Delay(1200);
                await RefreshUserProfileAsync();
                await RefreshWalletAsync();
            }

            if (paymentCompleted)
            {
                var message = string.IsNullOrWhiteSpace(paidOutTradeNo)
                    ? LanguageManager.GetString("AccountSubscribeSuccess")
                    : string.Format(LanguageManager.GetString("AccountSubscribeSuccessWithOrderFormat"), paidOutTradeNo);

                MessageBox.Show(
                    message,
                    LanguageManager.GetString("SuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private bool CanOpenSubscriptionDialog()
        {
            return IsLoggedIn && _authService != null;
        }

        [RelayCommand(CanExecute = nameof(CanRechargeWallet))]
        private async Task RechargeWalletAsync()
        {
            if (!IsLoggedIn || _authService == null)
            {
                return;
            }

            var accessToken = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                MessageBox.Show(
                    LanguageManager.GetString("AuthSessionExpiredRelogin"),
                    LanguageManager.GetString("WarningTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var checkoutWindow = new CheckoutWindow(accessToken, AccountCheckoutHostResolver.Resolve(_authService.AuthServerUrl), "recharge")
            {
                Owner = Application.Current.MainWindow
            };
            checkoutWindow.ShowDialog();

            await RefreshUserProfileAsync();
            await RefreshWalletAsync();
        }

        private bool CanRechargeWallet()
        {
            return IsLoggedIn && _authService != null && !IsLoadingWallet;
        }

        [RelayCommand(CanExecute = nameof(CanToggleAutoRenew))]
        private async Task ToggleAutoRenewAsync()
        {
            if (_authService == null || !IsLoggedIn || UserProfile?.Subscription == null)
            {
                return;
            }

            IsUpdatingAutoRenew = true;
            try
            {
                var enabled = !(UserProfile.Subscription.AutoRenew);
                var result = await _authService.SetSubscriptionAutoRenewAsync(enabled);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? "自动续费设置失败",
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                await RefreshUserProfileAsync();
                await RefreshWalletAsync();
            }
            finally
            {
                IsUpdatingAutoRenew = false;
            }
        }

        private bool CanToggleAutoRenew()
        {
            return IsLoggedIn && _authService != null && UserProfile?.Subscription?.IsPaidPlan == true && !IsUpdatingAutoRenew && !IsLoadingWallet;
        }

        [RelayCommand(CanExecute = nameof(CanRefreshUserProfile))]
        private async Task RefreshUserProfileAsync()
        {
            if (_authService == null)
            {
                return;
            }

            try
            {
                await _authService.RefreshUserProfileAsync();
                UpdateLoginState();
                await RefreshWalletAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Refresh profile exception: {ex.Message}");
            }
        }

        private bool CanRefreshUserProfile() => IsLoggedIn && _authService != null;

        [RelayCommand(CanExecute = nameof(CanBeginEditUsername))]
        private void BeginEditUsername()
        {
            EditUsername = UserProfile?.Username ?? string.Empty;
            IsEditingUsername = true;
        }

        private bool CanBeginEditUsername() => IsLoggedIn && !IsUpdatingUserProfile;

        [RelayCommand]
        private void CancelEditUsername()
        {
            IsEditingUsername = false;
            EditUsername = UserProfile?.Username ?? string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanSaveUsername))]
        private async Task SaveUsernameAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            var trimmedUsername = string.IsNullOrWhiteSpace(EditUsername) ? string.Empty : EditUsername.Trim();
            if (!AccountProfileInputValidator.TryValidateUsername(trimmedUsername, out var validationError))
            {
                MessageBox.Show(validationError ?? LanguageManager.GetString("AccountUsernameInvalid"), LanguageManager.GetString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsUpdatingUserProfile = true;
            try
            {
                var result = await _authService.UpdateUserProfileAsync(trimmedUsername, null);
                if (!result.Success)
                {
                    MessageBox.Show(result.ErrorMessage ?? LanguageManager.GetString("AccountUpdateUsernameFailed"), LanguageManager.GetString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                IsEditingUsername = false;
                await RefreshUserProfileAsync();
            }
            finally
            {
                IsUpdatingUserProfile = false;
            }
        }

        private bool CanSaveUsername()
        {
            if (!IsLoggedIn || _authService == null || IsUpdatingUserProfile || string.IsNullOrWhiteSpace(EditUsername))
            {
                return false;
            }

            return AccountProfileInputValidator.TryValidateUsername(EditUsername.Trim(), out _);
        }

        [RelayCommand(CanExecute = nameof(CanBeginEditEmail))]
        private void BeginEditEmail()
        {
            BindEmailInput = UserProfile?.Email ?? string.Empty;
            BindEmailCodeInput = string.Empty;
            BindEmailPasswordInput = string.Empty;
            BindEmailConfirmPasswordInput = string.Empty;
            IsEmailCodeSent = false;
            EmailCodeCountdownSeconds = 0;
            _emailCodeTimer.Stop();
            IsEditingEmail = true;
        }

        private bool CanBeginEditEmail() => IsLoggedIn && !IsUpdatingUserProfile;

        [RelayCommand]
        private void CancelEditEmail()
        {
            IsEditingEmail = false;
            ResetBindEmailState();
        }

        [RelayCommand(CanExecute = nameof(CanSendEmailCode))]
        private async Task SendEmailCodeAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            if (!AccountProfileInputValidator.IsValidEmail(BindEmailInput))
            {
                MessageBox.Show(
                    LanguageManager.GetString("BindEmailInvalidFormat"),
                    LanguageManager.GetString("EmailBindingErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsSendingEmailCode = true;
            try
            {
                var result = await _authService.SendBindEmailCodeAsync(BindEmailInput);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? LanguageManager.GetString("BindEmailCodeSendFailed"),
                        LanguageManager.GetString("EmailBindingErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                IsEmailCodeSent = true;
                EmailCodeCountdownSeconds = 60;
                _emailCodeTimer.Stop();
                _emailCodeTimer.Start();

                MessageBox.Show(
                    result.Message ?? LanguageManager.GetString("BindEmailCodeSent"),
                    LanguageManager.GetString("EmailBindingSuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                IsSendingEmailCode = false;
            }
        }

        private bool CanSendEmailCode()
        {
            return IsLoggedIn
                   && _authService != null
                   && !IsUpdatingUserProfile
                   && !IsSendingEmailCode
                   && EmailCodeCountdownSeconds == 0
                   && AccountProfileInputValidator.IsValidEmail(BindEmailInput);
        }

        [RelayCommand(CanExecute = nameof(CanSaveEmail))]
        private async Task SaveEmailAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            if (!AccountProfileInputValidator.IsValidEmail(BindEmailInput))
            {
                MessageBox.Show(
                    LanguageManager.GetString("BindEmailInvalidFormat"),
                    LanguageManager.GetString("EmailBindingErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!IsEmailCodeSent || string.IsNullOrWhiteSpace(BindEmailCodeInput))
            {
                MessageBox.Show(
                    LanguageManager.GetString("BindEmailCodeInvalid"),
                    LanguageManager.GetString("EmailBindingErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if ((BindEmailPasswordInput ?? string.Empty).Length < 8)
            {
                MessageBox.Show(
                    LanguageManager.GetString("BindEmailPasswordTooShort"),
                    LanguageManager.GetString("EmailBindingErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if ((BindEmailPasswordInput ?? string.Empty).Length > 128)
            {
                MessageBox.Show(
                    LanguageManager.GetString("BindEmailPasswordTooLong"),
                    LanguageManager.GetString("EmailBindingErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!string.Equals(BindEmailPasswordInput, BindEmailConfirmPasswordInput, StringComparison.Ordinal))
            {
                MessageBox.Show(
                    LanguageManager.GetString("BindEmailPasswordMismatch"),
                    LanguageManager.GetString("EmailBindingErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsUpdatingUserProfile = true;
            try
            {
                var result = await _authService.BindEmailAsync(
                    BindEmailInput,
                    BindEmailCodeInput,
                    BindEmailPasswordInput ?? string.Empty);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? LanguageManager.GetString("BindEmailFailedLater"),
                        LanguageManager.GetString("EmailBindingErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                IsEditingEmail = false;
                ResetBindEmailState();
                await RefreshUserProfileAsync();

                MessageBox.Show(
                    result.Message ?? LanguageManager.GetString("BindEmailSuccess"),
                    LanguageManager.GetString("EmailBindingSuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                IsUpdatingUserProfile = false;
            }
        }

        private bool CanSaveEmail()
        {
            return IsLoggedIn
                   && _authService != null
                   && !IsUpdatingUserProfile
                   && IsEmailCodeSent
                   && AccountProfileInputValidator.IsValidEmail(BindEmailInput)
                   && !string.IsNullOrWhiteSpace(BindEmailCodeInput)
                   && HasValidBindEmailPassword();
        }

        [RelayCommand(CanExecute = nameof(CanBeginEditProvider))]
        private void BeginEditProvider()
        {
            IsEditingProvider = true;
        }

        private bool CanBeginEditProvider() => IsLoggedIn && !IsUpdatingUserProfile;

        [RelayCommand]
        private void CancelEditProvider()
        {
            IsEditingProvider = false;
            ProviderUserIdInput = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanSaveProvider))]
        private async Task SaveProviderAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            IsUpdatingUserProfile = true;
            try
            {
                var provider = SelectedProvider?.Trim() ?? string.Empty;
                var result = await _authService.BindProviderAsync(provider, ProviderUserIdInput);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? LanguageManager.GetString("BindFailedLater"),
                        LanguageManager.GetString("ProviderBindingErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                IsEditingProvider = false;
                ProviderUserIdInput = string.Empty;
                await RefreshUserProfileAsync();
            }
            finally
            {
                IsUpdatingUserProfile = false;
            }
        }

        private bool CanSaveProvider()
        {
            return IsLoggedIn
                   && _authService != null
                   && !IsUpdatingUserProfile
                   && UserBindProviderCatalog.IsAllowed(SelectedProvider)
                   && !string.IsNullOrWhiteSpace(ProviderUserIdInput);
        }

        [RelayCommand(CanExecute = nameof(CanUpdateUserProfile))]
        private async Task UpdateUserProfileAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            var trimmedUsername = string.IsNullOrWhiteSpace(EditUsername) ? null : EditUsername.Trim();
            if (trimmedUsername != null && !AccountProfileInputValidator.TryValidateUsername(trimmedUsername, out var validationError))
            {
                MessageBox.Show(
                    validationError ?? LanguageManager.GetString("AccountUsernameInvalid"),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsUpdatingUserProfile = true;

            try
            {
                var result = await _authService.UpdateUserProfileAsync(trimmedUsername, null);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? LanguageManager.GetString("AccountUpdateProfileFailed"),
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                UpdateLoginState();
                MessageBox.Show(
                    result.Message ?? LanguageManager.GetString("AccountUpdateProfileSuccess"),
                    LanguageManager.GetString("SuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Update profile exception: {ex.Message}");
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("AccountUpdateProfileFailedFormat"), ex.Message),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsUpdatingUserProfile = false;
            }
        }

        private bool CanUpdateUserProfile()
        {
            var hasUsername = !string.IsNullOrWhiteSpace(EditUsername);
            if (hasUsername && !AccountProfileInputValidator.TryValidateUsername(EditUsername.Trim(), out _))
            {
                return false;
            }

            return IsLoggedIn
                   && _authService != null
                   && !IsUpdatingUserProfile
                   && hasUsername;
        }

        [RelayCommand(CanExecute = nameof(CanBindProvider))]
        private async Task BindProviderAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            IsUpdatingUserProfile = true;

            try
            {
                var provider = SelectedProvider?.Trim() ?? string.Empty;
                var result = await _authService.BindProviderAsync(provider, ProviderUserIdInput);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? LanguageManager.GetString("AccountBindProviderFailed"),
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                ProviderUserIdInput = string.Empty;
                UpdateLoginState();
                MessageBox.Show(
                    result.Message ?? LanguageManager.GetString("AccountBindProviderSuccess"),
                    LanguageManager.GetString("SuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Bind provider exception: {ex.Message}");
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("AccountBindProviderFailedFormat"), ex.Message),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsUpdatingUserProfile = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanBindQqProvider))]
        private Task BindQqProviderAsync()
        {
            return BindSocialProviderInternalAsync("qq", LanguageManager.GetString("AccountQq"));
        }

        private bool CanBindQqProvider()
        {
            return IsLoggedIn && _authService != null && !IsUpdatingUserProfile && !IsQqBound;
        }

        [RelayCommand(CanExecute = nameof(CanBindWechatProvider))]
        private Task BindWechatProviderAsync()
        {
            return BindSocialProviderInternalAsync("wechat", LanguageManager.GetString("AccountWechat"));
        }

        private bool CanBindWechatProvider()
        {
            return IsLoggedIn && _authService != null && !IsUpdatingUserProfile && !IsWechatBound;
        }

        private async Task BindSocialProviderInternalAsync(string provider, string providerDisplayName)
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            var isAlreadyBound = string.Equals(provider, "wechat", StringComparison.OrdinalIgnoreCase)
                ? IsWechatBound
                : IsQqBound;

            if (isAlreadyBound)
            {
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("AccountProviderAlreadyBoundFormat"), providerDisplayName),
                    LanguageManager.GetString("ProviderBindingSuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            IsUpdatingUserProfile = true;
            try
            {
                var result = await _authService.BindSocialProviderAsync(provider);
                if (!result.Success)
                {
                    MessageBox.Show(
                        result.ErrorMessage ?? LanguageManager.GetString("BindFailedLater"),
                        LanguageManager.GetString("ProviderBindingErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                await RefreshUserProfileAsync();
                MessageBox.Show(
                    result.Message ?? string.Format(LanguageManager.GetString("AccountProviderBindSuccessFormat"), providerDisplayName),
                    LanguageManager.GetString("ProviderBindingSuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Bind {providerDisplayName} exception: {ex.Message}");
                MessageBox.Show(
                    LanguageManager.GetString("BindFailedLater"),
                    LanguageManager.GetString("ProviderBindingErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsUpdatingUserProfile = false;
            }
        }

        private bool CanBindProvider()
        {
            return IsLoggedIn
                   && _authService != null
                   && !IsUpdatingUserProfile
                   && UserBindProviderCatalog.IsAllowed(SelectedProvider)
                   && !string.IsNullOrWhiteSpace(ProviderUserIdInput);
        }

        [RelayCommand(CanExecute = nameof(CanLoadSubscriptionPlans))]
        private async Task LoadSubscriptionPlansAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            IsLoadingPlans = true;

            try
            {
                var plans = await _authService.GetSubscriptionPlansAsync();
                AvailablePlans = plans;

                if (plans.Count == 0)
                {
                    SelectedPlan = null;
                    LatestOrderMessage = LanguageManager.GetString("AccountPlansEmpty");
                    return;
                }

                if (SelectedPlan == null || string.IsNullOrWhiteSpace(SelectedPlan.Id))
                {
                    SelectedPlan = plans[0];
                }

                LatestOrderMessage = string.Format(LanguageManager.GetString("AccountPlansLoadedFormat"), plans.Count);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Load plans exception: {ex.Message}");
                LatestOrderMessage = string.Format(LanguageManager.GetString("AccountPlansLoadFailedFormat"), ex.Message);
            }
            finally
            {
                IsLoadingPlans = false;
            }
        }

        private bool CanLoadSubscriptionPlans()
        {
            return IsLoggedIn && !IsLoadingPlans && _authService != null;
        }

        [RelayCommand(CanExecute = nameof(CanCreateSubscriptionOrder))]
        private async Task CreateSubscriptionOrderAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            if (HasPendingOrder)
            {
                MessageBox.Show(
                    LanguageManager.GetString("AccountPendingOrderExists"),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (SelectedPlan == null || string.IsNullOrWhiteSpace(SelectedPlan.Id))
            {
                MessageBox.Show(
                    LanguageManager.GetString("AccountSelectPlanFirst"),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsCreatingOrder = true;
            StopOrderPollingInternal();

            try
            {
                var result = await _authService.CreateSubscriptionOrderAsync(
                    SelectedPlan.Id,
                    SelectedPaymentMethod,
                    OrderDurationMonths);

                if (!result.Success || result.Order == null)
                {
                    var errorMessage = result.ErrorMessage ?? LanguageManager.GetString("AccountCreateOrderFailed");
                    LatestOrderMessage = errorMessage;
                    MessageBox.Show(
                        errorMessage,
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                UpdateOrderPresentation(result.Order, result.Payment);
                PersistPendingOrderOutTradeNo(result.Order.OutTradeNo);

                if (result.Payment != null)
                {
                    if (!string.IsNullOrWhiteSpace(result.Payment.Message))
                    {
                        LatestOrderMessage = result.Payment.Message;
                    }

                    if (string.Equals(result.Payment.IntegrationStatus, "native_qr_ready", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrWhiteSpace(result.Payment.CodeUrl))
                    {
                        LatestOrderMessage = LanguageManager.GetString("AccountPaymentQrMissing");
                    }
                }

                if (string.Equals(result.Order.Status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    _ = StartOrderPollingAsync(result.Order.OutTradeNo);
                }
                else if (IsTerminalOrderStatus(result.Order.Status))
                {
                    ClearPendingOrderOutTradeNo();
                    await ApplyTerminalOrderStateAsync(result.Order, refreshSubscriptionIfPaid: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountPageViewModel] Create order exception: {ex.Message}");
                LatestOrderMessage = string.Format(LanguageManager.GetString("AccountCreateOrderFailedFormat"), ex.Message);
                MessageBox.Show(
                    LatestOrderMessage,
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsCreatingOrder = false;
            }
        }

        private bool CanCreateSubscriptionOrder()
        {
            return IsLoggedIn
                   && !IsCreatingOrder
                   && !IsLoadingPlans
                   && _authService != null
                   && SelectedPlan != null
                   && !string.IsNullOrWhiteSpace(SelectedPlan.Id)
                   && OrderDurationMonths > 0
                   && !HasPendingOrder
                   && !IsPollingOrder;
        }

        [RelayCommand(CanExecute = nameof(CanQueryOrderStatus))]
        private async Task QueryOrderStatusAsync()
        {
            if (string.IsNullOrWhiteSpace(LatestOrderOutTradeNo))
            {
                return;
            }

            var order = await QueryOrderStatusInternalAsync(LatestOrderOutTradeNo, showErrorMessage: true);
            if (order != null && IsTerminalOrderStatus(order.Status))
            {
                await ApplyTerminalOrderStateAsync(order, refreshSubscriptionIfPaid: true);
            }
        }

        private bool CanQueryOrderStatus()
        {
            return IsLoggedIn
                   && _authService != null
                   && !string.IsNullOrWhiteSpace(LatestOrderOutTradeNo)
                   && !IsCreatingOrder;
        }

        [RelayCommand(CanExecute = nameof(CanStopOrderPolling))]
        private void StopOrderPolling()
        {
            StopOrderPollingInternal();
        }

        private bool CanStopOrderPolling()
        {
            return IsPollingOrder;
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
        #endregion
    }
}
