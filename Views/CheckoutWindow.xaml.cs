using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using lingualink_client.Models;
using lingualink_client.Services;
using Microsoft.Web.WebView2.Core;
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.Views
{
    public partial class CheckoutWindow : Window
    {
        private readonly string _accessToken;
        private readonly string _authHost;
        private CoreWebView2? _coreWebView2;

        /// <summary>
        /// 支付成功后触发，携带 out_trade_no 方便调用方刷新状态。
        /// </summary>
        public event Action<string>? PaymentCompleted;

        public CheckoutWindow(string accessToken, string authHost = AppEndpoints.DefaultAuthServerUrl)
        {
            InitializeComponent();

            _accessToken = accessToken ?? string.Empty;
            _authHost = AppEndpoints.NormalizeBaseUrl(authHost, AppEndpoints.DefaultAuthServerUrl);

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "LinguaLink",
                        "WebView2Cache"));

                await CheckoutWebView.EnsureCoreWebView2Async(env);
                _coreWebView2 = CheckoutWebView.CoreWebView2;

                if (_coreWebView2 == null)
                {
                    throw new InvalidOperationException("WebView2 初始化失败。");
                }

                _coreWebView2.WebMessageReceived += OnWebMessageReceived;
                _coreWebView2.WindowCloseRequested += OnWindowCloseRequested;
                _coreWebView2.NavigationCompleted += OnNavigationCompleted;
                _coreWebView2.Settings.IsStatusBarEnabled = false;

                var checkoutUrl = $"{_authHost}/checkout?token={Uri.EscapeDataString(_accessToken)}";
                _coreWebView2.Navigate(checkoutUrl);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckoutWindow] Failed to initialize checkout: {ex.Message}");
                MessageBox.Show(
                    LanguageManager.GetString("CheckoutInitFailed"),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Close();
            }
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                using var doc = JsonDocument.Parse(args.WebMessageAsJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                if (!root.TryGetProperty("type", out var typeProp))
                {
                    return;
                }

                var messageType = typeProp.GetString();
                if (!string.Equals(messageType, "lingualink_checkout_paid", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var outTradeNo = root.TryGetProperty("out_trade_no", out var tradeNoProp)
                    ? tradeNoProp.GetString() ?? string.Empty
                    : string.Empty;

                PaymentCompleted?.Invoke(outTradeNo);
                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CheckoutWindow] Failed to parse web message: {ex.Message}");
            }
        }

        private void OnWindowCloseRequested(object? sender, object e)
        {
            Dispatcher.Invoke(Close);
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                return;
            }

            Debug.WriteLine($"[CheckoutWindow] Navigation failed: {args.WebErrorStatus}");
            MessageBox.Show(
                LanguageManager.GetString("CheckoutNavigationFailed"),
                LanguageManager.GetString("WarningTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            Loaded -= OnLoaded;

            if (_coreWebView2 != null)
            {
                _coreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _coreWebView2.WindowCloseRequested -= OnWindowCloseRequested;
                _coreWebView2.NavigationCompleted -= OnNavigationCompleted;
            }

            CheckoutWebView.Dispose();
            base.OnClosed(e);
        }
    }
}
