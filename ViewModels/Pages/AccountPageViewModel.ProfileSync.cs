using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    public partial class AccountPageViewModel
    {
        private static readonly TimeSpan ProfileSyncRetryDelay = TimeSpan.FromSeconds(2);
        private const int RestoreOnlyProfileSyncAttempts = 2;
        private const int PageEnterProfileSyncAttempts = 4;

        private void OnAuthServiceLoginStateChanged(object? sender, bool isLoggedIn)
        {
            Application.Current.Dispatcher.Invoke(UpdateLoginState);
        }

        private void UpdateLoginState()
        {
            if (_authService == null)
            {
                ApplySignedOutState();
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
                _ = RefreshWalletAsync();
            }
            else
            {
                ApplyProfileUnavailableState();

                if (IsLoggedIn)
                {
                    _ = EnsureUserProfileLoadedAsync();
                }
            }

            RefreshAccountCommandStates();
        }

        public Task EnsureProfileFreshOnPageEnterAsync()
        {
            return SynchronizeUserProfileAsync(
                forceRemoteRefresh: true,
                allowSessionRestore: true,
                maxAttempts: PageEnterProfileSyncAttempts,
                retryDelay: ProfileSyncRetryDelay);
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

        private Task EnsureUserProfileLoadedAsync()
        {
            return SynchronizeUserProfileAsync(
                forceRemoteRefresh: false,
                allowSessionRestore: false,
                maxAttempts: RestoreOnlyProfileSyncAttempts,
                retryDelay: ProfileSyncRetryDelay);
        }

        private async Task SynchronizeUserProfileAsync(
            bool forceRemoteRefresh,
            bool allowSessionRestore,
            int maxAttempts,
            TimeSpan retryDelay)
        {
            if (_authService == null || _isRecoveringUserProfile || maxAttempts <= 0)
            {
                return;
            }

            if (!forceRemoteRefresh && UserProfile != null)
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
                        if (!allowSessionRestore)
                        {
                            return;
                        }

                        await _authService.TryRestoreSessionAsync();
                        UpdateLoginState();

                        if (!IsLoggedIn)
                        {
                            if (attempt < maxAttempts - 1)
                            {
                                await Task.Delay(retryDelay, token);
                            }

                            continue;
                        }
                    }

                    var refreshedProfile = await _authService.RefreshUserProfileAsync();
                    UpdateLoginState();

                    if (refreshedProfile != null || UserProfile != null)
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
                Debug.WriteLine($"[AccountPageViewModel] SynchronizeUserProfileAsync exception: {ex.Message}");
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

        private void ApplySignedOutState()
        {
            IsLoggedIn = false;
            ApplyProfileUnavailableState();
        }

        private void ApplyProfileUnavailableState()
        {
            IsEditingUsername = false;
            IsEditingEmail = false;
            ResetBindEmailState();
            IsEditingProvider = false;
            LoggedInUsername = string.Empty;
            ResetSubscriptionPresentation();
            ResetWalletPresentation();
            ClearPlans();
            StopOrderPollingInternal();
            ResetOrderState();
        }
    }
}
