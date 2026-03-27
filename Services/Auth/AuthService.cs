using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using lingualink_client.Models.Auth;
using lingualink_client.Services.Interfaces;
using lingualink_client.Views;

namespace lingualink_client.Services.Auth
{
    /// <summary>
    /// OAuth2 认证服务实现
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly SecureTokenStorage _tokenStorage;
        private readonly string _authServerUrl;
        private readonly string _loginPageUrl;
        private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);

        private TokenResponse? _currentToken;
        private UserProfile? _currentUser;
        private bool _disposed = false;

        private const string ClientCallbackUrl = "http://localhost:23456/callback";
        private const string OAuthLoginEndpoint = "/api/v1/auth/oauth/login";
        private const string OAuthBindLoginEndpoint = "/api/v1/auth/oauth/bind/login";
        private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(45);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public bool IsLoggedIn => _currentToken != null && !IsTokenExpired();
        public UserProfile? CurrentUser => _currentUser;
        public string AuthServerUrl => _authServerUrl;

        public event EventHandler<bool>? LoginStateChanged;

        /// <summary>
        /// 创建认证服务实例
        /// </summary>
        /// <param name="authServerUrl">Auth Server API 地址，如 https://auth.lingualink.aiatechco.com</param>
        /// <param name="loginPageUrl">登录页面地址（可选，用于测试）</param>
        public AuthService(string authServerUrl, string? loginPageUrl = null)
        {
            _authServerUrl = authServerUrl.TrimEnd('/');
            // 测试用登录页面地址
            _loginPageUrl = loginPageUrl ?? $"{_authServerUrl}/auth";

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _tokenStorage = new SecureTokenStorage();

            Debug.WriteLine($"[AuthService] Initialized with AuthServerUrl: {_authServerUrl}, LoginPageUrl: {_loginPageUrl}, ClientCallbackUrl: {ClientCallbackUrl}");
        }

        /// <summary>
        /// 尝试从存储恢复会话
        /// </summary>
        public async Task TryRestoreSessionAsync()
        {
            Debug.WriteLine("[AuthService] Attempting to restore session...");

            _currentToken = await _tokenStorage.LoadTokenAsync();
            if (_currentToken == null)
            {
                Debug.WriteLine("[AuthService] No stored token found");
                return;
            }

            if (IsTokenExpired())
            {
                var refreshed = await TryRefreshTokenAsync();
                if (!refreshed)
                {
                    Debug.WriteLine("[AuthService] Stored token expired and refresh failed, clearing local session");
                    ClearLocalSession();
                    return;
                }
            }

            if (IsLoggedIn)
            {
                // 令牌有效时先广播登录态，避免 UI 必须等待用户资料请求结束后才进入已登录流程。
                LoginStateChanged?.Invoke(this, true);
            }

            Debug.WriteLine("[AuthService] Token is valid, fetching user profile...");
            _currentUser = await FetchUserProfileAsync();
            if (_currentUser != null)
            {
                LoginStateChanged?.Invoke(this, true);
                Debug.WriteLine($"[AuthService] Session restored for user: {DescribeUser(_currentUser)}");
            }
            else
            {
                Debug.WriteLine("[AuthService] Profile fetch failed during restore, will keep token and retry later");
            }
        }

        /// <summary>
        /// OAuth2 登录
        /// </summary>
        public async Task<LoginResult> LoginAsync()
        {
            try
            {
                Debug.WriteLine("[AuthService] Starting login flow...");

                var (resolvedLoginUrl, loginUrlError) = await ResolveOAuthLoginUrlAsync(
                    endpointPath: OAuthLoginEndpoint,
                    provider: null,
                    requireAuthorization: false);

                var loginUrl = resolvedLoginUrl;
                if (string.IsNullOrWhiteSpace(loginUrl))
                {
                    Debug.WriteLine($"[AuthService] Resolve login_url failed, fallback to redirect URL. reason={loginUrlError}");
                    // fallback：直接打开 AuthServer 托管登录入口
                    loginUrl = BuildRedirectLoginUrl();
                }

                Debug.WriteLine($"[AuthService] Login URL: {loginUrl}");

                var callbackResult = await ShowLoginWindowAsync(loginUrl);
                if (callbackResult == null)
                {
                    Debug.WriteLine("[AuthService] Login cancelled by user");
                    return new LoginResult { Success = false, IsCancelled = true };
                }

                if (!string.IsNullOrWhiteSpace(callbackResult.Error) ||
                    string.Equals(callbackResult.MessageType, "lingualink_auth_error", StringComparison.OrdinalIgnoreCase))
                {
                    var errorMessage = string.IsNullOrWhiteSpace(callbackResult.ErrorDescription)
                        ? "登录失败"
                        : callbackResult.ErrorDescription;

                    Debug.WriteLine($"[AuthService] Login failed in callback: {errorMessage}");
                    return new LoginResult
                    {
                        Success = false,
                        ErrorMessage = errorMessage
                    };
                }

                if (!string.IsNullOrWhiteSpace(callbackResult.BindStatus))
                {
                    Debug.WriteLine($"[AuthService] Unexpected bind callback in login flow: status={callbackResult.BindStatus}, provider={callbackResult.BindProvider}");
                    return new LoginResult
                    {
                        Success = false,
                        ErrorMessage = "登录流程异常：收到了账号绑定回调"
                    };
                }

                if (string.IsNullOrWhiteSpace(callbackResult.AccessToken) &&
                    !string.IsNullOrWhiteSpace(callbackResult.AuthCode))
                {
                    Debug.WriteLine("[AuthService] Callback contains auth_code, exchanging for token...");
                    var exchangeResult = await ExchangeAuthCodeAsync(callbackResult.AuthCode);
                    if (exchangeResult != null && !string.IsNullOrWhiteSpace(exchangeResult.AccessToken))
                    {
                        callbackResult.AccessToken = exchangeResult.AccessToken;
                        callbackResult.RefreshToken = exchangeResult.RefreshToken;
                        callbackResult.ExpiresAt ??= exchangeResult.ExpiresAt;
                        callbackResult.UserId ??= exchangeResult.User?.Id;
                        callbackResult.Username ??= exchangeResult.User?.Username;
                        callbackResult.AvatarUrl ??= exchangeResult.User?.AvatarUrl;
                        callbackResult.Email ??= exchangeResult.User?.Email;
                    }
                }

                if (string.IsNullOrWhiteSpace(callbackResult.AccessToken))
                {
                    Debug.WriteLine("[AuthService] Callback payload missing access token");
                    return new LoginResult { Success = false, ErrorMessage = "登录回调缺少令牌信息" };
                }

                Debug.WriteLine("[AuthService] Received access token from callback");
                var tokenResult = BuildTokenResponseFromCallback(callbackResult);
                if (tokenResult == null)
                {
                    Debug.WriteLine("[AuthService] Token exchange failed");
                    return new LoginResult { Success = false, ErrorMessage = "Token 获取失败" };
                }

                _currentToken = tokenResult;
                await _tokenStorage.SaveTokenAsync(tokenResult);
                Debug.WriteLine("[AuthService] Token saved");

                _currentUser = BuildUserProfileFromCallback(callbackResult);

                var profileFromApi = await FetchUserProfileAsync();
                if (profileFromApi != null)
                {
                    _currentUser = profileFromApi;
                }

                Debug.WriteLine($"[AuthService] User profile fetched: {DescribeUser(_currentUser)}");

                LoginStateChanged?.Invoke(this, true);

                return new LoginResult
                {
                    Success = true,
                    User = _currentUser
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Login failed with exception: {ex.Message}");
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 构建登录跳转地址（fallback）
        /// </summary>
        private string BuildRedirectLoginUrl()
        {
            var callback = ClientCallbackUrl.Trim().Trim('"');
            var separator = _loginPageUrl.Contains('?') ? "&" : "?";
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            return $"{_loginPageUrl}{separator}client_callback={Uri.EscapeDataString(callback)}&_ts={Uri.EscapeDataString(ts)}";
        }

        private string BuildOAuthLoginApiUrl(string endpointPath, string? provider)
        {
            var callback = ClientCallbackUrl.Trim().Trim('"');
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            var queryParts = new List<string>
            {
                $"client_callback={Uri.EscapeDataString(callback)}",
                "redirect=0",
                $"_ts={Uri.EscapeDataString(ts)}"
            };

            if (!string.IsNullOrWhiteSpace(provider))
            {
                queryParts.Add($"provider={Uri.EscapeDataString(provider.Trim())}");
            }

            var endpoint = endpointPath.StartsWith("/", StringComparison.Ordinal)
                ? endpointPath
                : "/" + endpointPath;

            return $"{_authServerUrl}{endpoint}?{string.Join("&", queryParts)}";
        }

        private async Task<(string? LoginUrl, string? ErrorMessage)> ResolveOAuthLoginUrlAsync(
            string endpointPath,
            string? provider,
            bool requireAuthorization)
        {
            var requestUrl = BuildOAuthLoginApiUrl(endpointPath, provider);
            try
            {
                HttpResponseMessage? response = requireAuthorization
                    ? await SendAuthorizedWithRefreshRetryAsync(token =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        return request;
                    })
                    : await _httpClient.GetAsync(requestUrl);

                if (response == null)
                {
                    return (null, string.Equals(endpointPath, OAuthBindLoginEndpoint, StringComparison.OrdinalIgnoreCase)
                        ? LanguageManager.GetString("BindRequireLogin")
                        : "登录状态已失效，请重新登录");
                }

                using (response)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = string.IsNullOrWhiteSpace(responseContent)
                        ? null
                        : JsonSerializer.Deserialize<LoginUrlResponse>(responseContent, JsonOptions);

                    if (response.IsSuccessStatusCode
                        && result?.Code == 0
                        && !string.IsNullOrWhiteSpace(result.Data?.LoginUrl))
                    {
                        return (result.Data.LoginUrl, null);
                    }

                    var errorMessage = string.Equals(endpointPath, OAuthBindLoginEndpoint, StringComparison.OrdinalIgnoreCase)
                        ? ResolveBindLoginUrlErrorMessage(
                            response.StatusCode,
                            result?.Code,
                            result?.Error,
                            result?.Message,
                            responseContent)
                        : ResolveApiErrorMessage(
                            responseContent,
                            "获取登录地址失败",
                            result?.Error,
                            result?.Message);

                    Debug.WriteLine($"[AuthService] Resolve login_url failed: HTTP={(int)response.StatusCode}, Message={errorMessage}, Endpoint={endpointPath}, Provider={provider}");
                    return (null, errorMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Resolve login_url exception: {ex.Message}, Endpoint={endpointPath}, Provider={provider}");
                return (null, string.Equals(endpointPath, OAuthBindLoginEndpoint, StringComparison.OrdinalIgnoreCase)
                    ? LanguageManager.GetString("BindStartFailedRetry")
                    : ex.Message);
            }
        }

        private static TokenResponse? BuildTokenResponseFromCallback(OAuthCallbackResult callbackResult)
        {
            if (string.IsNullOrWhiteSpace(callbackResult.AccessToken))
            {
                return null;
            }

            DateTime? expiresAt = callbackResult.ExpiresAt;
            if (!expiresAt.HasValue && callbackResult.ExpiresIn.HasValue)
            {
                expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, callbackResult.ExpiresIn.Value - 60));
            }

            if (!expiresAt.HasValue)
            {
                Debug.WriteLine("[AuthService] Callback missing expiry info, using a short-lived default");
                expiresAt = DateTime.UtcNow.AddMinutes(55);
            }

            var expiresIn = callbackResult.ExpiresIn ??
                            Math.Max(0, (int)(expiresAt.Value - DateTime.UtcNow).TotalSeconds);

            return new TokenResponse
            {
                AccessToken = callbackResult.AccessToken,
                RefreshToken = callbackResult.RefreshToken ?? string.Empty,
                ExpiresIn = expiresIn,
                TokenType = string.IsNullOrWhiteSpace(callbackResult.TokenType) ? "Bearer" : callbackResult.TokenType,
                ExpiresAt = expiresAt.Value
            };
        }

        private static UserProfile? BuildUserProfileFromCallback(OAuthCallbackResult callbackResult)
        {
            if (string.IsNullOrWhiteSpace(callbackResult.UserId) &&
                string.IsNullOrWhiteSpace(callbackResult.Username) &&
                string.IsNullOrWhiteSpace(callbackResult.Email))
            {
                return null;
            }

            var username = !string.IsNullOrWhiteSpace(callbackResult.Username)
                ? callbackResult.Username
                : callbackResult.Email ?? callbackResult.UserId ?? "用户";

            return new UserProfile
            {
                Id = callbackResult.UserId ?? string.Empty,
                Username = username,
                Email = callbackResult.Email,
                AvatarUrl = callbackResult.AvatarUrl,
                Status = "active"
            };
        }

        /// <summary>
        /// 显示 WebView 登录窗口
        /// </summary>
        private async Task<OAuthCallbackResult?> ShowLoginWindowAsync(string loginUrl)
        {
            var tcs = new TaskCompletionSource<OAuthCallbackResult?>();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var loginWindow = new OAuthLoginWindow(loginUrl, ClientCallbackUrl);
                loginWindow.Owner = Application.Current.MainWindow;
                loginWindow.LoginCompleted += (sender, result) =>
                {
                    tcs.TrySetResult(result);
                };
                loginWindow.ShowDialog();

                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(null);
                }
            });

            return await tcs.Task;
        }

        private async Task<ExchangeAuthCodeData?> ExchangeAuthCodeAsync(string? authCode)
        {
            var code = authCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            try
            {
                var payload = JsonSerializer.Serialize(new { code });
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/auth/exchange")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ExchangeAuthCodeResponse>(responseContent, JsonOptions);

                if (response.IsSuccessStatusCode && result?.Code == 0 && result.Data != null)
                {
                    return result.Data;
                }

                var message = ResolveApiErrorMessage(responseContent, "auth_code 兑换失败", result?.Error, result?.Message);
                Debug.WriteLine($"[AuthService] Failed to exchange auth_code: HTTP={(int)response.StatusCode}, Message={message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Exchange auth_code exception: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 获取有效的 Access Token
        /// </summary>
        public async Task<string?> GetAccessTokenAsync()
        {
            if (_currentToken == null)
            {
                return null;
            }

            if (IsTokenExpired())
            {
                if (!await TryRefreshTokenAsync())
                {
                    await HandleUnauthorizedAsync();
                    return null;
                }
            }
            else if (IsTokenExpiringSoon())
            {
                // 临近过期时后台静默刷新，失败时先继续用当前 token，避免阻塞业务请求。
                _ = TryRefreshTokenAsync();
            }

            return _currentToken?.AccessToken;
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        private async Task<UserProfile?> FetchUserProfileAsync()
        {
            try
            {
                Debug.WriteLine("[AuthService] Fetching user profile...");
                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{_authServerUrl}/api/v1/user/profile");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    return request;
                });

                if (response == null)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UserProfileResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode && result?.Code == 0 && result.Data != null)
                {
                    var profile = result.Data;
                    var subscription = await FetchSubscriptionAsync();
                    if (subscription != null)
                    {
                        profile.Subscription = subscription;
                    }

                    Debug.WriteLine($"[AuthService] User profile fetched: {DescribeUser(profile)}");
                    return profile;
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var message = ResolveApiErrorMessage(json, "获取用户信息失败", result?.Error, result?.Message);
                    Debug.WriteLine($"[AuthService] Failed to fetch profile: HTTP={(int)response.StatusCode}, Message={message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to fetch profile: {ex.Message}");
            }

            return null;
        }

        private async Task<SubscriptionInfo?> FetchSubscriptionAsync()
        {
            try
            {
                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{_authServerUrl}/api/v1/user/subscription");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    return request;
                });

                if (response == null)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UserSubscriptionResponse>(json, JsonOptions);

                if (response.IsSuccessStatusCode && result?.Code == 0 && result.Data != null)
                {
                    return MapSubscriptionInfo(result.Data);
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var message = ResolveApiErrorMessage(json, "获取订阅信息失败", result?.Error, result?.Message);
                    Debug.WriteLine($"[AuthService] Failed to fetch subscription: HTTP={(int)response.StatusCode}, Message={message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to fetch subscription: {ex.Message}");
            }

            return null;
        }

        private static SubscriptionInfo MapSubscriptionInfo(UserSubscriptionSummary summary)
        {
            var status = summary.Subscription?.Status;
            if (string.IsNullOrWhiteSpace(status))
            {
                status = summary.IsActive ? "active" : "inactive";
            }

            return new SubscriptionInfo
            {
                Status = status,
                StartDate = summary.Subscription?.StartDate,
                EndDate = summary.Subscription?.EndDate,
                AutoRenew = summary.Subscription?.AutoRenew ?? false,
                Plan = summary.Plan,
                LegacyPlanName = summary.Plan?.Name
            };
        }

        /// <summary>
        /// 刷新用户信息
        /// </summary>
        public async Task<UserProfile?> RefreshUserProfileAsync()
        {
            _currentUser = await FetchUserProfileAsync();
            return _currentUser;
        }

        /// <summary>
        /// 更新用户资料（username/avatar_url）
        /// </summary>
        public async Task<ApiOperationResult> UpdateUserProfileAsync(string? username, string? avatarUrl)
        {
            var trimmedUsername = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
            var trimmedAvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();

            if (trimmedUsername != null && !TryValidateUsername(trimmedUsername, out var usernameError))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = usernameError ?? "用户名不合法"
                };
            }

            if (trimmedUsername == null && trimmedAvatarUrl == null)
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = "至少填写一个字段"
                };
            }

            var payload = new UpdateUserProfileRequest
            {
                Username = trimmedUsername,
                AvatarUrl = trimmedAvatarUrl
            };

            return await ExecuteUserMutationAsync<UserProfile>(HttpMethod.Put, "/api/v1/user/profile", payload, "资料更新失败");
        }

        private static bool TryValidateUsername(string value, out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "用户名不能为空";
                return false;
            }

            if (value.Length > 100)
            {
                errorMessage = "用户名长度不能超过 100";
                return false;
            }

            foreach (var c in value)
            {
                if (c == '/')
                {
                    errorMessage = "用户名不能包含 /";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private static string DescribeUser(UserProfile? user)
        {
            if (user == null)
            {
                return string.Empty;
            }

            return user.Username ?? user.Email ?? user.Id;
        }

        /// <summary>
        /// 发送邮箱绑定验证码
        /// </summary>
        public async Task<ApiOperationResult> SendBindEmailCodeAsync(string email)
        {
            var trimmedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
            if (string.IsNullOrWhiteSpace(trimmedEmail))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindEmailInvalidFormat")
                };
            }

            try
            {
                var payloadJson = JsonSerializer.Serialize(new SendBindEmailCodeRequest
                {
                    Email = trimmedEmail
                });

                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/user/bind-email/send-code");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    return request;
                });

                if (response == null)
                {
                    return new ApiOperationResult
                    {
                        Success = false,
                        ErrorMessage = LanguageManager.GetString("BindRequireLogin")
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var envelope = string.IsNullOrWhiteSpace(responseContent)
                    ? null
                    : JsonSerializer.Deserialize<ApiEnvelope<SendBindEmailCodeResult>>(responseContent, JsonOptions);

                if (response.IsSuccessStatusCode && envelope?.Code == 0)
                {
                    return new ApiOperationResult
                    {
                        Success = true,
                        Message = LanguageManager.GetString("BindEmailCodeSent")
                    };
                }

                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = ResolveSendBindEmailCodeErrorMessage(
                        response.StatusCode,
                        envelope?.Code,
                        envelope?.Error,
                        envelope?.Message,
                        responseContent)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Send bind email code failed: {ex.Message}");
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindEmailCodeSendFailed")
                };
            }
        }

        /// <summary>
        /// 绑定邮箱
        /// </summary>
        public async Task<ApiOperationResult> BindEmailAsync(string email, string code, string password)
        {
            var trimmedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
            var trimmedCode = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();

            if (string.IsNullOrWhiteSpace(trimmedEmail))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindEmailInvalidFormat")
                };
            }

            if (string.IsNullOrWhiteSpace(trimmedCode))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindEmailCodeInvalid")
                };
            }

            try
            {
                var payloadJson = JsonSerializer.Serialize(new BindEmailRequest
                {
                    Email = trimmedEmail,
                    Code = trimmedCode,
                    Password = password ?? string.Empty
                });

                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/user/bind-email");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    return request;
                });

                if (response == null)
                {
                    return new ApiOperationResult
                    {
                        Success = false,
                        ErrorMessage = LanguageManager.GetString("BindRequireLogin")
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var envelope = string.IsNullOrWhiteSpace(responseContent)
                    ? null
                    : JsonSerializer.Deserialize<ApiEnvelope<UserProfile>>(responseContent, JsonOptions);

                if (response.IsSuccessStatusCode && envelope?.Code == 0)
                {
                    _currentUser = envelope.Data ?? await RefreshUserProfileAsync();
                    if (_currentUser != null)
                    {
                        LoginStateChanged?.Invoke(this, true);
                    }

                    return new ApiOperationResult
                    {
                        Success = true,
                        Message = LanguageManager.GetString("BindEmailSuccess")
                    };
                }

                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = ResolveBindEmailErrorMessage(
                        response.StatusCode,
                        envelope?.Code,
                        envelope?.Error,
                        envelope?.Message,
                        responseContent)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Bind email failed: {ex.Message}");
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindEmailFailedLater")
                };
            }
        }

        /// <summary>
        /// 绑定第三方账号
        /// </summary>
        public async Task<ApiOperationResult> BindProviderAsync(string provider, string providerUserId)
        {
            var trimmedProvider = string.IsNullOrWhiteSpace(provider) ? string.Empty : provider.Trim().ToLowerInvariant();
            var trimmedProviderUserId = string.IsNullOrWhiteSpace(providerUserId) ? string.Empty : providerUserId.Trim();

            if (!UserBindProviderCatalog.IsAllowed(trimmedProvider))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindUnsupportedType")
                };
            }

            if (string.IsNullOrWhiteSpace(trimmedProviderUserId))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindMissingProviderAccountId")
                };
            }

            try
            {
                var payloadJson = JsonSerializer.Serialize(new BindProviderRequest
                {
                    Provider = trimmedProvider,
                    ProviderUserId = trimmedProviderUserId
                });

                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/user/bind-provider");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    return request;
                });

                if (response == null)
                {
                    return new ApiOperationResult
                    {
                        Success = false,
                        ErrorMessage = LanguageManager.GetString("BindRequireLogin")
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var envelope = string.IsNullOrWhiteSpace(responseContent)
                    ? null
                    : JsonSerializer.Deserialize<ApiEnvelope<ProviderBindingResult>>(responseContent, JsonOptions);

                if (response.IsSuccessStatusCode && envelope?.Code == 0)
                {
                    await RefreshUserProfileAsync();

                    return new ApiOperationResult
                    {
                        Success = true,
                        Message = envelope.Message
                    };
                }

                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = ResolveProviderBindingErrorMessage(
                        response.StatusCode,
                        envelope?.Code,
                        trimmedProvider,
                        envelope?.Error,
                        envelope?.Message,
                        responseContent)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Manual provider bind failed: provider={trimmedProvider}, message={ex.Message}");
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindFailedLater")
                };
            }
        }

        /// <summary>
        /// 通过 OAuth 绑定 QQ / 微信
        /// </summary>
        public async Task<ApiOperationResult> BindSocialProviderAsync(string provider)
        {
            var normalizedProvider = NormalizeSocialProvider(provider);
            if (string.IsNullOrWhiteSpace(normalizedProvider))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindUnsupportedType")
                };
            }

            var (loginUrl, loginUrlError) = await ResolveOAuthLoginUrlAsync(
                endpointPath: OAuthBindLoginEndpoint,
                provider: normalizedProvider,
                requireAuthorization: true);

            if (string.IsNullOrWhiteSpace(loginUrl))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = loginUrlError ?? LanguageManager.GetString("BindStartFailedRetry")
                };
            }

            var callbackResult = await ShowLoginWindowAsync(loginUrl);
            if (callbackResult == null)
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindCancelled")
                };
            }

            var bindStatus = callbackResult.BindStatus?.Trim();
            var isBindSuccess = string.Equals(bindStatus, "success", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(callbackResult.MessageType, "lingualink_provider_bind_success", StringComparison.OrdinalIgnoreCase);

            var isBindFailed = string.Equals(bindStatus, "failed", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(callbackResult.MessageType, "lingualink_provider_bind_error", StringComparison.OrdinalIgnoreCase);

            if (isBindFailed || !string.IsNullOrWhiteSpace(callbackResult.Error))
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = ResolveSocialBindingCallbackError(normalizedProvider, callbackResult)
                };
            }

            if (!isBindSuccess)
            {
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = LanguageManager.GetString("BindInvalidCallbackResult")
                };
            }

            await RefreshUserProfileAsync();

            var providerDisplay = string.Equals(normalizedProvider, "qq", StringComparison.OrdinalIgnoreCase)
                ? "QQ"
                : "微信";

            return new ApiOperationResult
            {
                Success = true,
                Message = $"{providerDisplay} 绑定成功"
            };
        }

        private static string NormalizeSocialProvider(string? provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return string.Empty;
            }

            var normalized = provider.Trim().ToLowerInvariant();
            return normalized is "qq" or "wechat" ? normalized : string.Empty;
        }

        private static string ResolveBindLoginUrlErrorMessage(
            System.Net.HttpStatusCode statusCode,
            int? code,
            string? error,
            string? message,
            string responseContent)
        {
            var detail = BuildNormalizedErrorDetail(error, message, responseContent);

            if (statusCode == System.Net.HttpStatusCode.Unauthorized || code == 40101)
            {
                if (ContainsAny(detail, "missing user context", "unauthorized"))
                {
                    return LanguageManager.GetString("BindRequireLogin");
                }
            }

            if (statusCode == System.Net.HttpStatusCode.BadRequest || code == 40001)
            {
                if (ContainsAny(detail, "provider must be one of", "invalid_bind_provider"))
                {
                    return LanguageManager.GetString("BindUnsupportedType");
                }

                if (ContainsAny(detail, "user_id is required", "invalid_bind_user_id"))
                {
                    return LanguageManager.GetString("BindMissingUserInfo");
                }

                if (ContainsAny(
                    detail,
                    "invalid callback url",
                    "callback url must use http or https scheme",
                    "callback url host is required",
                    "callback url must use https for non-localhost",
                    "callback url is not in allowlist"))
                {
                    return LanguageManager.GetString("BindInvalidCallbackUrl");
                }
            }

            if ((int)statusCode >= 500 || code == 50001)
            {
                return LanguageManager.GetString("BindStartFailedRetry");
            }

            return LanguageManager.GetString("BindStartFailedRetry");
        }

        private static string ResolveSocialBindingCallbackError(string provider, OAuthCallbackResult callbackResult)
        {
            var detail = BuildNormalizedErrorDetail(
                callbackResult.Error,
                callbackResult.ErrorDescription,
                callbackResult.MessageType,
                callbackResult.BindStatus);

            if (ContainsAny(detail, "provider_already_bound", "already bound to another user"))
            {
                return string.Equals(provider, "qq", StringComparison.OrdinalIgnoreCase)
                    ? LanguageManager.GetString("BindQqAlreadyBoundOther")
                    : LanguageManager.GetString("BindWechatAlreadyBoundOther");
            }

            if (ContainsAny(detail, "provider_account_empty", "provider account id is empty"))
            {
                return LanguageManager.GetString("BindMissingProviderAccount");
            }

            if (ContainsAny(detail, "provider must be one of", "invalid_bind_provider"))
            {
                return LanguageManager.GetString("BindUnsupportedType");
            }

            if (ContainsAny(detail, "bind user id is required", "user_id is required", "invalid_bind_user_id"))
            {
                return LanguageManager.GetString("BindMissingUserInfo");
            }

            if (ContainsAny(detail, "missing code/state"))
            {
                return LanguageManager.GetString("BindMissingCallbackParams");
            }

            if (ContainsAny(detail, "invalid_state"))
            {
                return LanguageManager.GetString("BindStateExpired");
            }

            if (ContainsAny(detail, "login_failed"))
            {
                return LanguageManager.GetString("BindFailedRetry");
            }

            if (ContainsAny(
                detail,
                "read provider user id",
                "apply provider binding",
                "sync local user",
                "bind_failed"))
            {
                return LanguageManager.GetString("BindFailedLater");
            }

            return LanguageManager.GetString("BindFailedLater");
        }

        private static string ResolveProviderBindingErrorMessage(
            System.Net.HttpStatusCode statusCode,
            int? code,
            string provider,
            string? error,
            string? message,
            string responseContent)
        {
            var detail = BuildNormalizedErrorDetail(error, message, responseContent);

            if (statusCode == System.Net.HttpStatusCode.Unauthorized || code == 40101)
            {
                return LanguageManager.GetString("BindRequireLogin");
            }

            if (statusCode == System.Net.HttpStatusCode.Conflict || code == 40901)
            {
                if (ContainsAny(detail, "provider_already_bound", "already bound"))
                {
                    return string.Equals(provider, "qq", StringComparison.OrdinalIgnoreCase)
                        ? LanguageManager.GetString("BindQqAlreadyBoundOther")
                        : LanguageManager.GetString("BindWechatAlreadyBoundOther");
                }
            }

            if (statusCode == System.Net.HttpStatusCode.BadRequest || code == 40001)
            {
                if (ContainsAny(detail, "provider_user_id is required", "invalid provider bind id"))
                {
                    return LanguageManager.GetString("BindMissingProviderAccountId");
                }

                if (ContainsAny(detail, "unsupported provider", "provider="))
                {
                    return LanguageManager.GetString("BindUnsupportedType");
                }

                if (ContainsAny(detail, "invalid character", "cannot unmarshal", "json"))
                {
                    return LanguageManager.GetString("BindRequestInvalid");
                }
            }

            if ((int)statusCode >= 500 || code == 50001)
            {
                return LanguageManager.GetString("BindFailedLater");
            }

            return LanguageManager.GetString("BindFailedLater");
        }

        private static string ResolveSendBindEmailCodeErrorMessage(
            System.Net.HttpStatusCode statusCode,
            int? code,
            string? error,
            string? message,
            string responseContent)
        {
            var detail = BuildNormalizedErrorDetail(error, message, responseContent);

            if (statusCode == System.Net.HttpStatusCode.Conflict || code == 40901)
            {
                if (ContainsAny(detail, "email is already bound to another user"))
                {
                    return LanguageManager.GetString("BindEmailAlreadyBoundOther");
                }
            }

            if ((int)statusCode == 429 || code == 42901)
            {
                if (ContainsAny(detail, "verification code sent too recently"))
                {
                    return LanguageManager.GetString("BindEmailCodeTooFrequent");
                }
            }

            if (statusCode == System.Net.HttpStatusCode.BadRequest || code == 40001)
            {
                if (ContainsAny(detail, "email format is invalid"))
                {
                    return LanguageManager.GetString("BindEmailInvalidFormat");
                }
            }

            if ((int)statusCode >= 500 || code == 50001)
            {
                return LanguageManager.GetString("BindEmailCodeSendFailed");
            }

            return LanguageManager.GetString("BindEmailCodeSendFailed");
        }

        private static string ResolveBindEmailErrorMessage(
            System.Net.HttpStatusCode statusCode,
            int? code,
            string? error,
            string? message,
            string responseContent)
        {
            var detail = BuildNormalizedErrorDetail(error, message, responseContent);

            if (statusCode == System.Net.HttpStatusCode.Conflict || code == 40901)
            {
                if (ContainsAny(detail, "email is already bound to another user"))
                {
                    return LanguageManager.GetString("BindEmailAlreadyBoundOther");
                }
            }

            if (statusCode == System.Net.HttpStatusCode.BadRequest || code == 40001)
            {
                if (ContainsAny(detail, "invalid verification code"))
                {
                    return LanguageManager.GetString("BindEmailCodeInvalid");
                }

                if (ContainsAny(detail, "password must be at least 8 characters"))
                {
                    return LanguageManager.GetString("BindEmailPasswordTooShort");
                }

                if (ContainsAny(detail, "password must not exceed 128 characters"))
                {
                    return LanguageManager.GetString("BindEmailPasswordTooLong");
                }

                if (ContainsAny(detail, "email format is invalid"))
                {
                    return LanguageManager.GetString("BindEmailInvalidFormat");
                }
            }

            if ((int)statusCode >= 500 || code == 50001)
            {
                return LanguageManager.GetString("BindEmailFailedLater");
            }

            return LanguageManager.GetString("BindEmailFailedLater");
        }

        private static string BuildNormalizedErrorDetail(params string?[] values)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", values)
                .Trim()
                .ToLowerInvariant();
        }

        private static bool ContainsAny(string source, params string[] patterns)
        {
            if (string.IsNullOrWhiteSpace(source) || patterns == null || patterns.Length == 0)
            {
                return false;
            }

            foreach (var pattern in patterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern)
                    && source.Contains(pattern.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<ApiOperationResult> ExecuteUserMutationAsync<T>(HttpMethod method, string relativeUrl, object payload, string fallbackErrorMessage)
        {
            try
            {
                var payloadJson = JsonSerializer.Serialize(payload);
                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(method, $"{_authServerUrl}{relativeUrl}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    return request;
                });

                if (response == null)
                {
                    return new ApiOperationResult
                    {
                        Success = false,
                        ErrorMessage = "登录状态已失效，请重新登录"
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                ApiEnvelope<T>? envelope = null;
                if (!string.IsNullOrWhiteSpace(responseContent))
                {
                    envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(responseContent, JsonOptions);
                }

                if (response.IsSuccessStatusCode && envelope?.Code == 0)
                {
                    await RefreshUserProfileAsync();

                    return new ApiOperationResult
                    {
                        Success = true,
                        Message = envelope.Message
                    };
                }

                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = ResolveApiErrorMessage(
                        responseContent,
                        fallbackErrorMessage,
                        envelope?.Error,
                        envelope?.Message)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] User mutation request failed ({method} {relativeUrl}): {ex.Message}");
                return new ApiOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 获取可购买套餐列表
        /// </summary>
        public async Task<IReadOnlyList<SubscriptionPlanInfo>> GetSubscriptionPlansAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_authServerUrl}/api/v1/public/plans");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[AuthService] Failed to load plans: {response.StatusCode}, {responseContent}");
                    return Array.Empty<SubscriptionPlanInfo>();
                }

                var result = JsonSerializer.Deserialize<SubscriptionPlansResponse>(responseContent, JsonOptions);

                if (result?.Code == 0 && result.Data != null)
                {
                    return FilterPurchasablePlans(result.Data);
                }

                Debug.WriteLine($"[AuthService] Plans response invalid: {responseContent}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to load plans: {ex.Message}");
            }

            return Array.Empty<SubscriptionPlanInfo>();
        }

        public async Task<IReadOnlyList<PublicAnnouncement>> GetActiveAnnouncementsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_authServerUrl}/api/v1/public/announcements");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[AuthService] Failed to load announcements: {response.StatusCode}, {responseContent}");
                    return Array.Empty<PublicAnnouncement>();
                }

                var envelope = JsonSerializer.Deserialize<ApiEnvelope<List<PublicAnnouncement>>>(responseContent, JsonOptions);
                if (envelope?.Code == 0 && envelope.Data != null)
                {
                    return envelope.Data;
                }

                Debug.WriteLine($"[AuthService] Announcements response invalid: {responseContent}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to load announcements: {ex.Message}");
            }

            return Array.Empty<PublicAnnouncement>();
        }

        private static IReadOnlyList<SubscriptionPlanInfo> FilterPurchasablePlans(IReadOnlyList<SubscriptionPlanInfo> plans)
        {
            if (plans.Count == 0)
            {
                return Array.Empty<SubscriptionPlanInfo>();
            }

            var filtered = new List<SubscriptionPlanInfo>(plans.Count);
            foreach (var plan in plans)
            {
                if (plan == null)
                {
                    continue;
                }

                if (plan.IsActive.HasValue && !plan.IsActive.Value)
                {
                    continue;
                }

                if (plan.IsFreePlan)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(plan.Id))
                {
                    continue;
                }

                filtered.Add(plan);
            }

            return filtered;
        }

        /// <summary>
        /// 创建订阅订单
        /// </summary>
        public async Task<CreateSubscriptionOrderResult> CreateSubscriptionOrderAsync(string planId, string paymentMethod, int durationMonths = 1)
        {
            if (string.IsNullOrWhiteSpace(planId))
            {
                return new CreateSubscriptionOrderResult
                {
                    Success = false,
                    ErrorMessage = "缺少套餐 ID"
                };
            }

            if (durationMonths <= 0)
            {
                durationMonths = 1;
            }

            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    plan_id = planId,
                    payment_method = paymentMethod,
                    duration_months = durationMonths
                });

                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/user/subscription/orders");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    return request;
                });

                if (response == null)
                {
                    return new CreateSubscriptionOrderResult
                    {
                        Success = false,
                        ErrorMessage = "登录状态已失效，请重新登录"
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<CreateSubscriptionOrderResponse>(responseContent, JsonOptions);

                    if (result?.Code == 0 && result.Data?.Order != null)
                    {
                        return new CreateSubscriptionOrderResult
                        {
                            Success = true,
                            Order = result.Data.Order,
                            Payment = result.Data.Payment
                        };
                    }

                    return new CreateSubscriptionOrderResult
                    {
                        Success = false,
                        ErrorMessage = result?.Error ?? result?.Message ?? "下单失败"
                    };
                }

                return new CreateSubscriptionOrderResult
                {
                    Success = false,
                    ErrorMessage = ResolveApiErrorMessage(responseContent, "下单失败")
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to create order: {ex.Message}");
                return new CreateSubscriptionOrderResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 查询订单状态
        /// </summary>
        public async Task<SubscriptionOrderInfo?> GetSubscriptionOrderStatusAsync(string outTradeNo)
        {
            if (string.IsNullOrWhiteSpace(outTradeNo))
            {
                return null;
            }

            try
            {
                var encodedOutTradeNo = Uri.EscapeDataString(outTradeNo);
                var response = await SendAuthorizedWithRefreshRetryAsync(token =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{_authServerUrl}/api/v1/user/subscription/orders/{encodedOutTradeNo}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    return request;
                });

                if (response == null)
                {
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<SubscriptionOrderStatusResponse>(responseContent, JsonOptions);

                    if (result?.Code == 0)
                    {
                        return result.Data;
                    }

                    Debug.WriteLine($"[AuthService] Query order status failed: {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to query order status: {ex.Message}");
            }

            return null;
        }

        private async Task<bool> TryRefreshTokenAsync()
        {
            if (_currentToken == null || string.IsNullOrWhiteSpace(_currentToken.RefreshToken))
            {
                return false;
            }

            await _tokenRefreshLock.WaitAsync();
            try
            {
                var currentToken = _currentToken;
                if (currentToken == null || string.IsNullOrWhiteSpace(currentToken.RefreshToken))
                {
                    return false;
                }

                if (_currentToken != null && !IsTokenExpiringSoon())
                {
                    return true;
                }

                var payload = JsonSerializer.Serialize(new
                {
                    refresh_token = currentToken.RefreshToken
                });

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/auth/refresh")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RefreshTokenResponse>(responseContent, JsonOptions);

                if (response.IsSuccessStatusCode
                    && result?.Code == 0
                    && result.Data != null
                    && !string.IsNullOrWhiteSpace(result.Data.AccessToken))
                {
                    var nowUtc = DateTime.UtcNow;
                    DateTime expiresAtUtc;
                    if (result.Data.ExpiresAt.HasValue)
                    {
                        expiresAtUtc = DateTime.SpecifyKind(result.Data.ExpiresAt.Value, DateTimeKind.Utc).ToUniversalTime();
                    }
                    else if (result.Data.ExpiresIn > 0)
                    {
                        expiresAtUtc = nowUtc.AddSeconds(result.Data.ExpiresIn);
                    }
                    else
                    {
                        expiresAtUtc = nowUtc.AddHours(1);
                    }

                    if (expiresAtUtc <= nowUtc.AddSeconds(5))
                    {
                        expiresAtUtc = nowUtc.AddHours(1);
                    }

                    var expiresIn = Math.Max(1, (int)Math.Round((expiresAtUtc - nowUtc).TotalSeconds));
                    var refreshToken = string.IsNullOrWhiteSpace(result.Data.RefreshToken)
                        ? currentToken.RefreshToken
                        : result.Data.RefreshToken;

                    _currentToken = new TokenResponse
                    {
                        AccessToken = result.Data.AccessToken,
                        RefreshToken = refreshToken ?? string.Empty,
                        TokenType = currentToken.TokenType,
                        ExpiresIn = expiresIn,
                        ExpiresAt = expiresAtUtc
                    };

                    await _tokenStorage.SaveTokenAsync(_currentToken);
                    return true;
                }

                Debug.WriteLine($"[AuthService] Refresh token failed: HTTP={(int)response.StatusCode}, Payload={responseContent}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Refresh token exception: {ex.Message}");
                return false;
            }
            finally
            {
                _tokenRefreshLock.Release();
            }
        }

        private async Task<HttpResponseMessage?> SendAuthorizedWithRefreshRetryAsync(Func<string, HttpRequestMessage> requestFactory)
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var response = await _httpClient.SendAsync(requestFactory(token));
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                return response;
            }

            response.Dispose();

            var refreshed = await TryRefreshTokenAsync();
            if (!refreshed)
            {
                await HandleUnauthorizedAsync();
                return null;
            }

            token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            response = await _httpClient.SendAsync(requestFactory(token));
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                await HandleUnauthorizedAsync();
                return null;
            }

            return response;
        }

        private static string ResolveApiErrorMessage(string responseContent, string fallbackMessage, string? error = null, string? message = null)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                return error;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return fallbackMessage;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorProperty))
                {
                    var parsedError = errorProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(parsedError))
                    {
                        return parsedError;
                    }
                }

                if (root.TryGetProperty("message", out var messageProperty))
                {
                    var parsedMessage = messageProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(parsedMessage))
                    {
                        return parsedMessage;
                    }
                }
            }
            catch
            {
            }

            return fallbackMessage;
        }

        /// <summary>
        /// 退出登录
        /// </summary>
        public async Task LogoutAsync()
        {
            Debug.WriteLine("[AuthService] Logging out...");

            if (_currentToken != null)
            {
                try
                {
                    // 通知服务器注销
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/auth/logout");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _currentToken.AccessToken);

                    await _httpClient.SendAsync(request);
                    Debug.WriteLine("[AuthService] Server logout completed");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AuthService] Server logout failed (ignoring): {ex.Message}");
                }
            }

            ClearLocalSession();

            try
            {
                await OAuthLoginWindow.ClearAuthSessionDataAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to clear OAuth WebView session data: {ex.Message}");
            }

            Debug.WriteLine("[AuthService] Logged out");
        }

        /// <summary>
        /// 业务接口返回 401 时，仅清理本地会话并触发重新登录
        /// </summary>
        public Task HandleUnauthorizedAsync()
        {
            Debug.WriteLine("[AuthService] Unauthorized response received, clearing local session");
            ClearLocalSession();
            return Task.CompletedTask;
        }

        private void ClearLocalSession()
        {
            var hadSession = _currentToken != null || _currentUser != null;

            _currentToken = null;
            _currentUser = null;
            _tokenStorage.ClearToken();

            if (hadSession)
            {
                LoginStateChanged?.Invoke(this, false);
            }
        }

        private bool IsTokenExpired()
        {
            return _currentToken == null || DateTime.UtcNow >= _currentToken.ExpiresAt;
        }

        private bool IsTokenExpiringSoon()
        {
            return _currentToken == null || DateTime.UtcNow >= _currentToken.ExpiresAt - TokenRefreshSkew;
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                    _tokenRefreshLock.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
