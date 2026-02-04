using System;
using System.Diagnostics;
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
        private readonly string _loginUrl;
        private readonly string _expectedState;
        private readonly int _callbackPort;
        private readonly string _callbackPath;
        private readonly string _callbackUrlPrefix;

        /// <summary>
        /// 登录完成事件
        /// </summary>
        public event EventHandler<OAuthCallbackResult?>? LoginCompleted;

        /// <summary>
        /// 创建 OAuth 登录窗口
        /// </summary>
        /// <param name="loginUrl">登录页面 URL</param>
        /// <param name="expectedState">期望的 state 参数</param>
        /// <param name="callbackPort">回调端口</param>
        /// <param name="callbackPath">回调路径</param>
        public OAuthLoginWindow(string loginUrl, string expectedState, int callbackPort, string callbackPath)
        {
            InitializeComponent();
            _loginUrl = loginUrl;
            _expectedState = expectedState;
            _callbackPort = callbackPort;
            _callbackPath = callbackPath;
            _callbackUrlPrefix = $"http://localhost:{callbackPort}{callbackPath}";

            Debug.WriteLine($"[OAuthLoginWindow] Created with loginUrl: {loginUrl}");
            Debug.WriteLine($"[OAuthLoginWindow] Callback URL prefix: {_callbackUrlPrefix}");

            Loaded += OAuthLoginWindow_Loaded;
        }

        private async void OAuthLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 初始化 WebView2
                await LoginWebView.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OAuthLoginWindow] WebView2 initialization failed: {ex.Message}");
                ShowError($"无法初始化登录组件: {ex.Message}");
            }
        }

        private void LoginWebView_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Debug.WriteLine("[OAuthLoginWindow] WebView2 initialized successfully");

                // 配置 WebView2
                LoginWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                LoginWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                LoginWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

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
            if (e.Uri.StartsWith(_callbackUrlPrefix, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("[OAuthLoginWindow] Callback URL detected, processing...");
                e.Cancel = true;
                ProcessCallback(e.Uri);
            }
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

        private void ProcessCallback(string callbackUrl)
        {
            Debug.WriteLine($"[OAuthLoginWindow] Processing callback URL: {callbackUrl}");

            try
            {
                var uri = new Uri(callbackUrl);
                var query = HttpUtility.ParseQueryString(uri.Query);

                // 解析所有可能的参数
                var error = query["error"];
                var errorDescription = query["error_description"];
                var state = query["state"];
                var code = query["code"];
                var accessToken = query["access_token"];
                var refreshToken = query["refresh_token"];
                var expiresAt = query["expires_at"];
                var userId = query["user_id"];
                var displayName = query["display_name"];
                var email = query["email"];
                var avatarUrl = query["avatar_url"];

                Debug.WriteLine($"[OAuthLoginWindow] Callback params:");
                Debug.WriteLine($"  - error: {error ?? "null"}");
                Debug.WriteLine($"  - state: {state ?? "null"} (expected: {_expectedState})");
                Debug.WriteLine($"  - code: {(string.IsNullOrEmpty(code) ? "null" : "***")}");
                Debug.WriteLine($"  - access_token: {(string.IsNullOrEmpty(accessToken) ? "null" : "***")}");
                Debug.WriteLine($"  - refresh_token: {(string.IsNullOrEmpty(refreshToken) ? "null" : "***")}");
                Debug.WriteLine($"  - user_id: {userId ?? "null"}");
                Debug.WriteLine($"  - display_name: {displayName ?? "null"}");

                // 检查错误
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.WriteLine($"[OAuthLoginWindow] OAuth error: {error} - {errorDescription}");
                    LoginCompleted?.Invoke(this, new OAuthCallbackResult
                    {
                        Error = error,
                        ErrorDescription = errorDescription
                    });
                    Close();
                    return;
                }

                // 验证 state（如果存在）
                if (!string.IsNullOrEmpty(state) && state != _expectedState)
                {
                    Debug.WriteLine($"[OAuthLoginWindow] State mismatch! Expected: {_expectedState}, Got: {state}");
                    LoginCompleted?.Invoke(this, new OAuthCallbackResult
                    {
                        Error = "state_mismatch",
                        ErrorDescription = "State 参数不匹配"
                    });
                    Close();
                    return;
                }

                // 情况1：Auth Server 直接返回 access_token（推荐）
                if (!string.IsNullOrEmpty(accessToken))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Login successful with access_token!");
                    
                    // 解析 expires_in
                    int expiresIn = 0;
                    if (int.TryParse(query["expires_in"], out var parsedExpiresIn))
                    {
                        expiresIn = parsedExpiresIn;
                    }

                    LoginCompleted?.Invoke(this, new OAuthCallbackResult
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresAt = expiresAt,
                        ExpiresIn = expiresIn,
                        UserId = userId,
                        DisplayName = displayName,
                        Email = email,
                        AvatarUrl = avatarUrl,
                        State = state
                    });
                    Close();
                    return;
                }

                // 情况2：返回 authorization code（需要再换 token）
                if (!string.IsNullOrEmpty(code))
                {
                    Debug.WriteLine("[OAuthLoginWindow] Login successful with code!");
                    LoginCompleted?.Invoke(this, new OAuthCallbackResult
                    {
                        Code = code,
                        State = state
                    });
                    Close();
                    return;
                }

                // 没有有效数据
                Debug.WriteLine("[OAuthLoginWindow] Invalid callback - no code or access_token");
                LoginCompleted?.Invoke(this, new OAuthCallbackResult
                {
                    Error = "invalid_response",
                    ErrorDescription = "回调中没有 code 或 access_token"
                });
                Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OAuthLoginWindow] Error processing callback: {ex.Message}");
                LoginCompleted?.Invoke(this, new OAuthCallbackResult
                {
                    Error = "parse_error",
                    ErrorDescription = ex.Message
                });
                Close();
            }
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
            LoginCompleted?.Invoke(this, null);
            Close();
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
    }
}



