using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Models.Auth;
using lingualink_client.Services;
using lingualink_client.Views;
using QRCoder;
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class AccountPageViewModel
    {
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
            LatestOrderExpireAtDisplay = order.ExpireAt.HasValue ? FormatDate(order.ExpireAt.Value) : string.Empty;

            if (payment == null)
            {
                return;
            }

            LatestOrderProvider = payment.Provider ?? string.Empty;
            LatestOrderIntegrationStatus = payment.IntegrationStatus ?? string.Empty;
            LatestOrderCodeUrl = payment.CodeUrl?.Trim() ?? string.Empty;

            if (!order.ExpireAt.HasValue && payment.OrderExpireAt.HasValue)
            {
                LatestOrderExpireAtDisplay = FormatDate(payment.OrderExpireAt.Value);
            }

            if (!string.IsNullOrWhiteSpace(payment.Message))
            {
                LatestOrderMessage = payment.Message;
            }
        }

        private static BitmapImage? BuildQrCodeImage(string? codeUrl)
        {
            if (string.IsNullOrWhiteSpace(codeUrl))
            {
                return null;
            }

            try
            {
                using var generator = new QRCodeGenerator();
                using var qrData = generator.CreateQrCode(codeUrl, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrData);
                var pngBytes = qrCode.GetGraphic(20);

                using var memoryStream = new MemoryStream(pngBytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = memoryStream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
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
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase);
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

        private async Task RestorePendingOrderAsync()
        {
            if (_authService == null || !IsLoggedIn || _isRestoringPendingOrder)
            {
                return;
            }

            var outTradeNo = _currentSettings.PendingSubscriptionOrderOutTradeNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(outTradeNo))
            {
                return;
            }

            _isRestoringPendingOrder = true;
            try
            {
                LatestOrderOutTradeNo = outTradeNo;
                LatestOrderMessage = string.Format(LanguageManager.GetString("AccountOrderRestoreDetectedFormat"), outTradeNo);

                var order = await QueryOrderStatusInternalAsync(outTradeNo, showErrorMessage: false);
                if (order == null)
                {
                    LatestOrderMessage = string.Format(LanguageManager.GetString("AccountOrderRestoreRecoveredFormat"), outTradeNo);
                    return;
                }

                if (string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    _ = StartOrderPollingAsync(outTradeNo);
                    return;
                }

                if (IsTerminalOrderStatus(order.Status))
                {
                    await ApplyTerminalOrderStateAsync(order, refreshSubscriptionIfPaid: true);
                }
            }
            finally
            {
                _isRestoringPendingOrder = false;
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

            var checkoutWindow = new CheckoutWindow(accessToken, ResolveCheckoutAuthHost(_authService.AuthServerUrl))
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

            await RefreshUserProfileAsync();
            if (!paymentCompleted)
            {
                await Task.Delay(1200);
                await RefreshUserProfileAsync();
            }

            if (!paymentCompleted)
            {
                return;
            }

            var message = string.IsNullOrWhiteSpace(paidOutTradeNo)
                ? LanguageManager.GetString("AccountSubscribeSuccess")
                : string.Format(LanguageManager.GetString("AccountSubscribeSuccessWithOrderFormat"), paidOutTradeNo);

            MessageBox.Show(
                message,
                LanguageManager.GetString("SuccessTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private bool CanOpenSubscriptionDialog()
        {
            return IsLoggedIn && _authService != null;
        }

        private static string ResolveCheckoutAuthHost(string? authServerUrl)
        {
            return AppEndpoints.NormalizeBaseUrl(authServerUrl, AppEndpoints.DefaultAuthServerUrl);
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
    }
}
