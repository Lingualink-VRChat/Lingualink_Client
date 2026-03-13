using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using lingualink_client.Models.Auth;
using Microsoft.Web.WebView2.Core;

namespace lingualink_client.Views
{
    /// <summary>
    /// OAuth 登录窗口
    /// </summary>
    public partial class OAuthLoginWindow : Window
    {
        private static readonly string AuthWebViewUserDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LinguaLink",
            "AuthWebView2Cache");

        private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);
        private static CoreWebView2Environment? SharedEnvironment;

        private readonly string _loginUrl;
        private readonly Uri _callbackUri;
        private readonly string _callbackUrlPrefix;
        private bool _loginCompleted;

        /// <summary>
        /// 登录完成事件
        /// </summary>
        public event EventHandler<OAuthCallbackResult?>? LoginCompleted;

        /// <summary>
        /// 创建 OAuth 登录窗口
        /// </summary>
        /// <param name="loginUrl">登录页面 URL</param>
        /// <param name="callbackUrl">客户端回调 URL</param>
        public OAuthLoginWindow(string loginUrl, string callbackUrl)
        {
            InitializeComponent();
            _loginUrl = loginUrl;
            _callbackUri = new Uri(callbackUrl);
            _callbackUrlPrefix = callbackUrl;

            Debug.WriteLine($"[OAuthLoginWindow] Created with loginUrl: {loginUrl}");
            Debug.WriteLine($"[OAuthLoginWindow] Callback URL prefix: {_callbackUrlPrefix}");

            Loaded += OAuthLoginWindow_Loaded;
        }

        private async void OAuthLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 初始化 WebView2（使用固定 userDataFolder 复用认证会话）
                var environment = await GetSharedEnvironmentAsync();
                await LoginWebView.EnsureCoreWebView2Async(environment);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OAuthLoginWindow] WebView2 initialization failed: {ex.Message}");
                ShowError($"无法初始化登录组件: {ex.Message}");
            }
        }

        private static async Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
        {
            await EnvironmentLock.WaitAsync();
            try
            {
                if (SharedEnvironment != null)
                {
                    return SharedEnvironment;
                }

                Directory.CreateDirectory(AuthWebViewUserDataFolder);
                SharedEnvironment = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: AuthWebViewUserDataFolder);

                return SharedEnvironment;
            }
            finally
            {
                EnvironmentLock.Release();
            }
        }

        public static async Task ClearAuthSessionDataAsync()
        {
            await EnvironmentLock.WaitAsync();
            try
            {
                SharedEnvironment = null;
            }
            finally
            {
                EnvironmentLock.Release();
            }

            if (!Directory.Exists(AuthWebViewUserDataFolder))
            {
                return;
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Directory.Delete(AuthWebViewUserDataFolder, recursive: true);
                    Debug.WriteLine("[OAuthLoginWindow] Cleared auth WebView2 session data");
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == 3)
                    {
                        Debug.WriteLine($"[OAuthLoginWindow] Failed to clear auth WebView2 session data: {ex.Message}");
                        return;
                    }

                    await Task.Delay(250 * attempt);
                }
            }
        }

        private async void LoginWebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Debug.WriteLine("[OAuthLoginWindow] WebView2 initialized successfully");

                // 配置 WebView2
                LoginWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                LoginWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                LoginWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                LoginWebView.CoreWebView2.WebMessageReceived += LoginWebView_WebMessageReceived;
                LoginWebView.CoreWebView2.NewWindowRequested += LoginWebView_NewWindowRequested;

                // 回调桥页可能先 window.close() 再跳转 callback。这里拦截关闭请求并把消息转发给宿主，避免误判“登录已取消”。
                const string bridgeRelayScript = @"
(() => {
  if (!window.chrome || !window.chrome.webview) {
    return;
  }

  window.addEventListener('message', (event) => {
    try {
      window.chrome.webview.postMessage(event.data);
    } catch (_) {
    }
  });

  const originalClose = window.close;
  window.close = function() {
    try {
      window.chrome.webview.postMessage({
        type: 'lingualink_window_close_requested',
        href: window.location.href
      });
    } catch (_) {
    }

    // 不立即关闭，等待客户端完成回调解析后再主动 Close()
  };
})();
";
                try
                {
                    await LoginWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(bridgeRelayScript);
                }
                catch (Exception scriptEx)
                {
                    Debug.WriteLine($"[OAuthLoginWindow] Failed to inject bridge relay script: {scriptEx.Message}");
                }

                // 导航到登录页面
                Debug.WriteLine($"[OAuthLoginWindow] Navigating to: {_loginUrl}");
                LoginWebView.Source = new Uri(_loginUrl);
            }
            else
            {
                Debug.WriteLine($"[OAuthLoginWindow] WebView2 initialization failed: {e.InitializationException?.Message}");
                ShowError($"登录组件初始化失败: {e.InitializationException?.Message}");
            }
        }

        private void LoginWebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            Debug.WriteLine($"[OAuthLoginWindow] Navigation starting: {e.Uri}");

            // 检查是否是回调 URL
            if (IsCallbackUrl(e.Uri))
            {
                Debug.WriteLine("[OAuthLoginWindow] Callback URL detected, processing...");
                e.Cancel = true;
                ProcessCallback(e.Uri);
            }
        }

        private bool IsCallbackUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, _callbackUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(uri.Host, _callbackUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (uri.Port != _callbackUri.Port)
            {
                return false;
            }

            return string.Equals(uri.AbsolutePath, _callbackUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        }

        private void LoginWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            Debug.WriteLine($"[OAuthLoginWindow] Navigation completed, success: {e.IsSuccess}");

            if (e.IsSuccess)
            {
                // 隐藏加载指示器
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ErrorOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 显示错误
                var errorStatus = e.WebErrorStatus;
                Debug.WriteLine($"[OAuthLoginWindow] Navigation failed with status: {errorStatus}");

                if (errorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    ShowError($"页面加载失败: {GetErrorMessage(errorStatus)}");
                }
            }
        }

        private void LoginWebView_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            var targetUri = e.Uri;
            Debug.WriteLine($"[OAuthLoginWindow] Intercepted popup request: {targetUri}");

            if (string.IsNullOrWhiteSpace(targetUri) || LoginWebView.CoreWebView2 == null)
            {
                return;
            }

            e.Handled = true;
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                ErrorOverlay.Visibility = Visibility.Collapsed;
                LoginWebView.CoreWebView2.Navigate(targetUri);
            });
        }

        private void ProcessCallback(string callbackUrl)
        {
            try
            {
                var uri = new Uri(callbackUrl);
                var query = HttpUtility.ParseQueryString(uri.Query);
                if (query.Count == 0 && !string.IsNullOrWhiteSpace(uri.Fragment))
                {
                    query = HttpUtility.ParseQueryString(uri.Fragment.TrimStart('#'));
                }

                var code = query["code"];
                var authCode = query["auth_code"];
                var bindStatus = query["bind_status"];
                var bindProvider = query["bind_provider"];
                var state = query["state"];
                var messageType = query["type"];
                var error = query["error"];
                var errorDescription = query["error_description"];
                var accessToken = query["access_token"];
                var refreshToken = query["refresh_token"];
                var tokenType = query["token_type"];
                var expiresIn = TryParseInt(query["expires_in"]);
                var expiresAt = TryParseExpiresAt(query["expires_at"]);
                var userId = query["user_id"];
                var displayName = query["display_name"];
                var avatarUrl = query["avatar_url"];
                var email = query["email"];

                Debug.WriteLine($"[OAuthLoginWindow] Callback params - code: {(string.IsNullOrEmpty(code) ? "null" : "***")}, auth_code: {(string.IsNullOrEmpty(authCode) ? "null" : "***")}, access_token: {(string.IsNullOrEmpty(accessToken) ? "null" : "***")}, bind_status: {bindStatus}, bind_provider: {bindProvider}, state: {state}, error: {error}");

                if (!string.IsNullOrWhiteSpace(bindStatus))
                {
                    if (string.Equals(bindStatus, "success", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[OAuthLoginWindow] Provider bind successful: provider={bindProvider}");
                        CompleteLogin(new OAuthCallbackResult
                        {
                            MessageType = messageType,
                            BindStatus = bindStatus,
                            BindProvider = bindProvider,
                            State = state ?? string.Empty
                        });
                        return;
                    }

                    Debug.WriteLine($"[OAuthLoginWindow] Provider bind failed: provider={bindProvider}, error={error}, error_description={errorDescription}");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        BindStatus = bindStatus,
                        BindProvider = bindProvider,
                        State = state ?? string.Empty,
                        Error = error,
                        ErrorDescription = errorDescription
                    });
                    return;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.WriteLine($"[OAuthLoginWindow] OAuth error: {error} - {errorDescription}");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        State = state ?? string.Empty,
                        Error = error,
                        ErrorDescription = errorDescription
                    });
                }
                else if (!string.IsNullOrEmpty(accessToken))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Login successful, returning token payload");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresIn = expiresIn,
                        ExpiresAt = expiresAt,
                        TokenType = string.IsNullOrWhiteSpace(tokenType) ? "Bearer" : tokenType,
                        State = state ?? string.Empty,
                        UserId = userId,
                        DisplayName = displayName,
                        AvatarUrl = avatarUrl,
                        Email = email
                    });
                }
                else if (!string.IsNullOrEmpty(authCode))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Auth code received");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        AuthCode = authCode,
                        State = state ?? string.Empty
                    });
                }
                else if (!string.IsNullOrEmpty(code))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Authorization code received");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        Code = code,
                        State = state ?? string.Empty
                    });
                }
                else
                {
                    Debug.WriteLine("[OAuthLoginWindow] Invalid callback payload");
                    CompleteLogin(null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OAuthLoginWindow] Error processing callback: {ex.Message}");
                CompleteLogin(null);
            }
        }

        private void LoginWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                var messageType = GetJsonString(root, "type");
                if (string.IsNullOrWhiteSpace(messageType))
                {
                    return;
                }

                if (string.Equals(messageType, "lingualink_auth_success", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Received auth success payload from bridge postMessage");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        AccessToken = GetJsonString(root, "access_token"),
                        RefreshToken = GetJsonString(root, "refresh_token"),
                        ExpiresAt = TryParseExpiresAt(GetJsonString(root, "expires_at")),
                        TokenType = GetJsonString(root, "token_type"),
                        UserId = GetJsonString(root, "user_id"),
                        DisplayName = GetJsonString(root, "display_name"),
                        AvatarUrl = GetJsonString(root, "avatar_url"),
                        Email = GetJsonString(root, "email")
                    });
                    return;
                }

                if (string.Equals(messageType, "lingualink_auth_error", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Received auth error payload from bridge postMessage");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        Error = GetJsonString(root, "error"),
                        ErrorDescription = GetJsonString(root, "error_description")
                    });
                    return;
                }

                if (string.Equals(messageType, "lingualink_provider_bind_success", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Received provider bind success payload from bridge postMessage");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        BindStatus = "success",
                        BindProvider = GetJsonString(root, "bind_provider")
                    });
                    return;
                }

                if (string.Equals(messageType, "lingualink_provider_bind_error", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Received provider bind error payload from bridge postMessage");
                    CompleteLogin(new OAuthCallbackResult
                    {
                        MessageType = messageType,
                        BindStatus = "failed",
                        BindProvider = GetJsonString(root, "bind_provider"),
                        Error = GetJsonString(root, "error"),
                        ErrorDescription = GetJsonString(root, "error_description")
                    });
                    return;
                }

                if (string.Equals(messageType, "lingualink_window_close_requested", StringComparison.OrdinalIgnoreCase))
                {
                    var href = GetJsonString(root, "href");
                    Debug.WriteLine($"[OAuthLoginWindow] Page requested window.close(), href={href}");

                    if (!string.IsNullOrWhiteSpace(href) && IsCallbackUrl(href))
                    {
                        ProcessCallback(href);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OAuthLoginWindow] Failed to parse WebMessageReceived payload: {ex.Message}");
            }
        }

        private static string? GetJsonString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => value.ToString()
            };
        }

        private void CompleteLogin(OAuthCallbackResult? result)
        {
            if (_loginCompleted)
            {
                return;
            }

            _loginCompleted = true;
            LoginCompleted?.Invoke(this, result);
            Close();
        }

        private static int? TryParseInt(string? value)
        {
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private static DateTime? TryParseExpiresAt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (long.TryParse(value, out var unixSeconds))
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                }
                catch
                {
                    return null;
                }
            }

            if (DateTimeOffset.TryParse(value, out var parsed))
            {
                return parsed.UtcDateTime;
            }

            return null;
        }

        private void ShowError(string message)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            ErrorOverlay.Visibility = Visibility.Visible;
            ErrorMessageText.Text = message;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[OAuthLoginWindow] Close button clicked");
            CompleteLogin(null);
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[OAuthLoginWindow] Retry button clicked");
            ErrorOverlay.Visibility = Visibility.Collapsed;
            LoadingOverlay.Visibility = Visibility.Visible;

            if (LoginWebView.CoreWebView2 != null)
            {
                LoginWebView.Source = new Uri(_loginUrl);
            }
        }

        private static string GetErrorMessage(CoreWebView2WebErrorStatus status)
        {
            return status switch
            {
                CoreWebView2WebErrorStatus.Unknown => "未知错误",
                CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect => "证书名称不匹配",
                CoreWebView2WebErrorStatus.CertificateExpired => "证书已过期",
                CoreWebView2WebErrorStatus.ClientCertificateContainsErrors => "客户端证书错误",
                CoreWebView2WebErrorStatus.CertificateRevoked => "证书已被吊销",
                CoreWebView2WebErrorStatus.CertificateIsInvalid => "证书无效",
                CoreWebView2WebErrorStatus.ServerUnreachable => "服务器无法访问",
                CoreWebView2WebErrorStatus.Timeout => "连接超时",
                CoreWebView2WebErrorStatus.ErrorHttpInvalidServerResponse => "服务器响应无效",
                CoreWebView2WebErrorStatus.ConnectionAborted => "连接已中断",
                CoreWebView2WebErrorStatus.ConnectionReset => "连接被重置",
                CoreWebView2WebErrorStatus.Disconnected => "连接已断开",
                CoreWebView2WebErrorStatus.CannotConnect => "无法连接到服务器",
                CoreWebView2WebErrorStatus.HostNameNotResolved => "无法解析主机名",
                CoreWebView2WebErrorStatus.OperationCanceled => "操作已取消",
                CoreWebView2WebErrorStatus.RedirectFailed => "重定向失败",
                CoreWebView2WebErrorStatus.UnexpectedError => "意外错误",
                _ => $"错误代码: {status}"
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            if (LoginWebView?.CoreWebView2 != null)
            {
                LoginWebView.CoreWebView2.WebMessageReceived -= LoginWebView_WebMessageReceived;
                LoginWebView.CoreWebView2.NewWindowRequested -= LoginWebView_NewWindowRequested;
            }

            base.OnClosed(e);
        }
    }
}
