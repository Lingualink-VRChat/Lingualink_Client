using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
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

        private TokenResponse? _currentToken;
        private UserProfile? _currentUser;
        private bool _disposed = false;

        // 回调监听端口
        private const int CallbackPort = 23456;
        private const string CallbackPath = "/callback";

        public bool IsLoggedIn => _currentToken != null && !IsTokenExpired();
        public UserProfile? CurrentUser => _currentUser;

        public event EventHandler<bool>? LoginStateChanged;

        /// <summary>
        /// 创建认证服务实例
        /// </summary>
        /// <param name="authServerUrl">Auth Server API 地址，如 http://localhost:8080</param>
        /// <param name="loginPageUrl">登录页面地址（可选，用于测试）</param>
        public AuthService(string authServerUrl, string? loginPageUrl = null)
        {
            _authServerUrl = authServerUrl.TrimEnd('/');
            // 测试用登录页面地址
            _loginPageUrl = loginPageUrl ?? $"{_authServerUrl}/login";

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _tokenStorage = new SecureTokenStorage();

            Debug.WriteLine($"[AuthService] Initialized with AuthServerUrl: {_authServerUrl}, LoginPageUrl: {_loginPageUrl}");
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

            if (!IsTokenExpired())
            {
                // Token 有效，获取用户信息
                Debug.WriteLine("[AuthService] Token is valid, fetching user profile...");
                _currentUser = await FetchUserProfileAsync();
                if (_currentUser != null)
                {
                    LoginStateChanged?.Invoke(this, true);
                    Debug.WriteLine($"[AuthService] Session restored for user: {_currentUser.DisplayName}");
                }
            }
            else
            {
                // Token 过期，尝试刷新
                Debug.WriteLine("[AuthService] Token expired, attempting refresh...");
                var refreshed = await TryRefreshTokenAsync();
                if (refreshed)
                {
                    _currentUser = await FetchUserProfileAsync();
                    if (_currentUser != null)
                    {
                        LoginStateChanged?.Invoke(this, true);
                        Debug.WriteLine($"[AuthService] Session restored after token refresh for user: {_currentUser.DisplayName}");
                    }
                }
                else
                {
                    Debug.WriteLine("[AuthService] Token refresh failed, clearing stored token");
                    _tokenStorage.ClearToken();
                    _currentToken = null;
                }
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

                // 1. 生成 state 用于 CSRF 防护
                var state = GenerateRandomString(32);
                Debug.WriteLine($"[AuthService] Generated state: {state}");

                // 2. 构建登录 URL（不再需要 PKCE，因为 Auth Server 直接返回 token）
                var loginUrl = BuildLoginUrl(string.Empty, state);
                Debug.WriteLine($"[AuthService] Login URL: {loginUrl}");

                // 3. 打开 WebView 登录窗口
                var callbackResult = await ShowLoginWindowAsync(loginUrl, state);
                
                // 检查是否取消或出错
                if (callbackResult == null)
                {
                    Debug.WriteLine("[AuthService] Login cancelled - no callback result");
                    return new LoginResult { Success = false, ErrorMessage = "登录已取消" };
                }

                if (callbackResult.HasError)
                {
                    Debug.WriteLine($"[AuthService] Login failed with error: {callbackResult.Error} - {callbackResult.ErrorDescription}");
                    return new LoginResult { Success = false, ErrorMessage = callbackResult.ErrorDescription ?? callbackResult.Error };
                }

                // 4. 处理回调结果
                TokenResponse? tokenResult = null;

                // 情况1：Auth Server 直接返回 access_token（推荐）
                if (callbackResult.HasToken)
                {
                    Debug.WriteLine("[AuthService] Received access_token directly from callback");
                    
                    // 计算过期时间
                    DateTime expiresAt;
                    if (!string.IsNullOrEmpty(callbackResult.ExpiresAt))
                    {
                        // 尝试解析 ISO 8601 格式的时间
                        if (DateTime.TryParse(callbackResult.ExpiresAt, out var parsedExpiresAt))
                        {
                            expiresAt = parsedExpiresAt.ToUniversalTime();
                        }
                        else
                        {
                            expiresAt = DateTime.UtcNow.AddSeconds(callbackResult.ExpiresIn > 0 ? callbackResult.ExpiresIn : 86400);
                        }
                    }
                    else
                    {
                        expiresAt = DateTime.UtcNow.AddSeconds(callbackResult.ExpiresIn > 0 ? callbackResult.ExpiresIn : 86400);
                    }

                    tokenResult = new TokenResponse
                    {
                        AccessToken = callbackResult.AccessToken!,
                        RefreshToken = callbackResult.RefreshToken ?? string.Empty,
                        ExpiresIn = callbackResult.ExpiresIn > 0 ? callbackResult.ExpiresIn : 86400,
                        ExpiresAt = expiresAt
                    };

                    // 从回调中构建用户信息
                    if (!string.IsNullOrEmpty(callbackResult.UserId))
                    {
                        _currentUser = new UserProfile
                        {
                            Id = callbackResult.UserId,
                            DisplayName = callbackResult.DisplayName ?? callbackResult.UserId,
                            Email = callbackResult.Email,
                            AvatarUrl = callbackResult.AvatarUrl
                        };
                    }
                }
                // 情况2：返回 authorization code（需要再换 token）
                else if (!string.IsNullOrEmpty(callbackResult.Code))
                {
                    Debug.WriteLine("[AuthService] Received authorization code, exchanging for token...");
                    tokenResult = await ExchangeCodeForTokenAsync(callbackResult.Code, string.Empty);
                }

                if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
                {
                    Debug.WriteLine("[AuthService] Failed to obtain token");
                    return new LoginResult { Success = false, ErrorMessage = "Token 获取失败" };
                }

                // 5. 保存 Token
                _currentToken = tokenResult;
                await _tokenStorage.SaveTokenAsync(tokenResult);
                Debug.WriteLine("[AuthService] Token saved");

                // 6. 获取/更新用户信息（如果回调中没有提供完整信息）
                if (_currentUser == null)
                {
                    _currentUser = await FetchUserProfileAsync();
                }
                Debug.WriteLine($"[AuthService] User: {_currentUser?.DisplayName}");

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
        /// 构建登录 URL
        /// </summary>
        private string BuildLoginUrl(string _, string state)
        {
            var clientCallback = $"http://localhost:{CallbackPort}{CallbackPath}";

            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            // client_callback: 告诉 Auth Server 登录成功后重定向到客户端的地址
            queryParams["client_callback"] = clientCallback;
            queryParams["state"] = state;

            return $"{_loginPageUrl}?{queryParams}";
        }

        /// <summary>
        /// 显示 WebView 登录窗口
        /// </summary>
        private async Task<OAuthCallbackResult?> ShowLoginWindowAsync(string loginUrl, string expectedState)
        {
            var tcs = new TaskCompletionSource<OAuthCallbackResult?>();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var loginWindow = new OAuthLoginWindow(loginUrl, expectedState, CallbackPort, CallbackPath);
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

        /// <summary>
        /// 用 code 换取 token
        /// </summary>
        private async Task<TokenResponse?> ExchangeCodeForTokenAsync(string code, string codeVerifier)
        {
            try
            {
                var requestUrl = $"{_authServerUrl}/api/v1/auth/casdoor/callback";
                var redirectUri = $"http://localhost:{CallbackPort}{CallbackPath}";

                var payload = new
                {
                    code = code,
                    code_verifier = codeVerifier,
                    redirect_uri = redirectUri
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                Debug.WriteLine($"[AuthService] Exchanging code for token at: {requestUrl}");
                var response = await _httpClient.PostAsync(requestUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AuthService] Token exchange response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<AuthCallbackResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Code == 0 && result.Data != null)
                    {
                        return new TokenResponse
                        {
                            AccessToken = result.Data.AccessToken,
                            RefreshToken = result.Data.RefreshToken,
                            ExpiresIn = result.Data.ExpiresIn,
                            TokenType = result.Data.TokenType,
                            ExpiresAt = DateTime.UtcNow.AddSeconds(result.Data.ExpiresIn - 60) // 提前60秒过期
                        };
                    }
                    else
                    {
                        Debug.WriteLine($"[AuthService] Token exchange failed: {result?.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[AuthService] Token exchange HTTP error: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Token exchange exception: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 获取有效的 Access Token（自动刷新）
        /// </summary>
        public async Task<string?> GetAccessTokenAsync()
        {
            if (_currentToken == null)
                return null;

            if (IsTokenExpired())
            {
                var refreshed = await TryRefreshTokenAsync();
                if (!refreshed)
                    return null;
            }

            return _currentToken.AccessToken;
        }

        /// <summary>
        /// 刷新 Token
        /// </summary>
        private async Task<bool> TryRefreshTokenAsync()
        {
            if (string.IsNullOrEmpty(_currentToken?.RefreshToken))
                return false;

            try
            {
                var requestUrl = $"{_authServerUrl}/api/v1/auth/refresh";
                var payload = new { refresh_token = _currentToken.RefreshToken };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                Debug.WriteLine("[AuthService] Refreshing token...");
                var response = await _httpClient.PostAsync(requestUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<RefreshTokenResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Code == 0 && result.Data != null)
                    {
                        _currentToken.AccessToken = result.Data.AccessToken;
                        _currentToken.ExpiresAt = DateTime.UtcNow.AddSeconds(result.Data.ExpiresIn - 60);

                        if (!string.IsNullOrEmpty(result.Data.RefreshToken))
                        {
                            _currentToken.RefreshToken = result.Data.RefreshToken;
                        }

                        await _tokenStorage.SaveTokenAsync(_currentToken);
                        Debug.WriteLine("[AuthService] Token refreshed successfully");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Token refresh failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        private async Task<UserProfile?> FetchUserProfileAsync()
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_authServerUrl}/api/v1/user/profile");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                Debug.WriteLine("[AuthService] Fetching user profile...");
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<UserProfileResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Code == 0)
                    {
                        Debug.WriteLine($"[AuthService] User profile fetched: {result.Data?.DisplayName}");
                        return result.Data;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Failed to fetch profile: {ex.Message}");
            }

            return null;
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

            _currentToken = null;
            _currentUser = null;
            _tokenStorage.ClearToken();

            LoginStateChanged?.Invoke(this, false);
            Debug.WriteLine("[AuthService] Logged out");
        }

        private bool IsTokenExpired()
        {
            return _currentToken == null || DateTime.UtcNow >= _currentToken.ExpiresAt;
        }

        #region API Key 管理

        /// <summary>
        /// 获取用户的 API Key 列表
        /// </summary>
        public async Task<List<ApiKeyInfo>> GetApiKeysAsync()
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("[AuthService] Cannot get API keys: not logged in");
                return new List<ApiKeyInfo>();
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_authServerUrl}/api/v1/user/api-keys");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                Debug.WriteLine("[AuthService] Fetching API keys...");
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AuthService] API keys response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ApiKeyListResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Code == 0 && result.Data != null)
                    {
                        Debug.WriteLine($"[AuthService] Retrieved {result.Data.Count} API keys");
                        return result.Data;
                    }
                }

                Debug.WriteLine($"[AuthService] Failed to get API keys: {responseContent}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Exception getting API keys: {ex.Message}");
            }

            return new List<ApiKeyInfo>();
        }

        /// <summary>
        /// 创建新的 API Key
        /// </summary>
        public async Task<CreateApiKeyResult> CreateApiKeyAsync(string name)
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new CreateApiKeyResult
                {
                    Success = false,
                    ErrorMessage = "未登录"
                };
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_authServerUrl}/api/v1/user/api-keys");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var payload = new { name = name };
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                Debug.WriteLine($"[AuthService] Creating API key with name: {name}");
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AuthService] Create API key response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<CreateApiKeyResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Code == 0 && result.Data != null)
                    {
                        Debug.WriteLine($"[AuthService] API key created: {result.Data.Prefix}...");
                        return new CreateApiKeyResult
                        {
                            Success = true,
                            FullKey = result.Data.Key,
                            KeyInfo = new ApiKeyInfo
                            {
                                Id = result.Data.Id,
                                Name = result.Data.Name,
                                Prefix = result.Data.Prefix,
                                Status = "active",
                                CreatedAt = result.Data.CreatedAt
                            }
                        };
                    }
                    else
                    {
                        return new CreateApiKeyResult
                        {
                            Success = false,
                            ErrorMessage = result?.Message ?? "创建失败"
                        };
                    }
                }

                Debug.WriteLine($"[AuthService] Failed to create API key: {responseContent}");
                return new CreateApiKeyResult
                {
                    Success = false,
                    ErrorMessage = $"请求失败: {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Exception creating API key: {ex.Message}");
                return new CreateApiKeyResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 删除（吊销）API Key
        /// </summary>
        public async Task<bool> DeleteApiKeyAsync(string keyId)
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("[AuthService] Cannot delete API key: not logged in");
                return false;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_authServerUrl}/api/v1/user/api-keys/{keyId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                Debug.WriteLine($"[AuthService] Deleting API key: {keyId}");
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AuthService] Delete API key response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<DeleteApiKeyResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Code == 0)
                    {
                        Debug.WriteLine($"[AuthService] API key deleted: {keyId}");
                        return true;
                    }
                }

                Debug.WriteLine($"[AuthService] Failed to delete API key: {responseContent}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Exception deleting API key: {ex.Message}");
            }

            return false;
        }

        #endregion

        #region PKCE Helpers

        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(bytes);
        }

        private static string GenerateRandomString(int length)
        {
            var bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
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



