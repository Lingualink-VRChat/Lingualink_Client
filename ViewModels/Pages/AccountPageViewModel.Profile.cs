using System;
using System.Diagnostics;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models.Auth;
using lingualink_client.Services;
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class AccountPageViewModel
    {
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

        private static bool TryValidateEmailInput(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                var parsed = new MailAddress(email.Trim());
                return string.Equals(parsed.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private bool HasValidBindEmailPassword()
        {
            var password = BindEmailPasswordInput ?? string.Empty;
            if (password.Length < 8 || password.Length > 128)
            {
                return false;
            }

            return string.Equals(password, BindEmailConfirmPasswordInput ?? string.Empty, StringComparison.Ordinal);
        }

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            if (_authService == null)
            {
                MessageBox.Show(
                    LanguageManager.GetString("AccountAuthServiceUnavailable"),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            IsLoggingIn = true;
            RefreshLoginCommandStates();

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
                RefreshLoginCommandStates();
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

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

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

        private bool CanLogout() => IsLoggedIn && _authService != null;

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
            if (!TryValidateUsernameInput(trimmedUsername, out var validationError))
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
                        result.ErrorMessage ?? LanguageManager.GetString("AccountUpdateUsernameFailed"),
                        LanguageManager.GetString("ErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
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

            return TryValidateUsernameInput(EditUsername.Trim(), out _);
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

            if (!TryValidateEmailInput(BindEmailInput))
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
                   && TryValidateEmailInput(BindEmailInput);
        }

        [RelayCommand(CanExecute = nameof(CanSaveEmail))]
        private async Task SaveEmailAsync()
        {
            if (_authService == null || !IsLoggedIn)
            {
                return;
            }

            if (!TryValidateEmailInput(BindEmailInput))
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
                   && TryValidateEmailInput(BindEmailInput)
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
            if (trimmedUsername != null && !TryValidateUsernameInput(trimmedUsername, out var validationError))
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
            if (hasUsername && !TryValidateUsernameInput(EditUsername.Trim(), out _))
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
    }
}
