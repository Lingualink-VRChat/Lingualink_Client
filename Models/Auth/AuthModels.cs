using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace lingualink_client.Models.Auth
{
    /// <summary>
    /// OAuth2 Token 响应
    /// </summary>
    public class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        /// <summary>
        /// 本地计算的过期时间 (UTC)
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// 用户信息
    /// </summary>
    public class UserProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("casdoor_name")]
        public string? CasdoorName { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "active";

        /// <summary>
        /// 订阅/配额信息
        /// </summary>
        [JsonPropertyName("subscription")]
        public SubscriptionInfo? Subscription { get; set; }
    }

    /// <summary>
    /// 订阅信息
    /// </summary>
    public class SubscriptionInfo
    {
        [JsonPropertyName("plan_name")]
        public string PlanName { get; set; } = "Free";

        [JsonPropertyName("quota_remaining")]
        public long QuotaRemaining { get; set; }

        [JsonPropertyName("quota_total")]
        public long QuotaTotal { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// 登录结果
    /// </summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public UserProfile? User { get; set; }
    }

    /// <summary>
    /// OAuth 回调结果
    /// </summary>
    public class OAuthCallbackResult
    {
        // Authorization Code 流程
        public string? Code { get; set; }
        public string? State { get; set; }

        // Token 直接返回（Auth Server 重定向时携带）
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? ExpiresAt { get; set; }
        public int ExpiresIn { get; set; }

        // 用户信息
        public string? UserId { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? AvatarUrl { get; set; }

        // 错误信息
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// 是否成功获取到 Token
        /// </summary>
        public bool HasToken => !string.IsNullOrEmpty(AccessToken);

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(Error);
    }

    #region API 响应模型

    /// <summary>
    /// 登录 URL 响应
    /// </summary>
    public class LoginUrlResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public LoginUrlData? Data { get; set; }
    }

    public class LoginUrlData
    {
        [JsonPropertyName("login_url")]
        public string? LoginUrl { get; set; }
    }

    /// <summary>
    /// Auth 回调响应
    /// </summary>
    public class AuthCallbackResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public AuthCallbackData? Data { get; set; }
    }

    public class AuthCallbackData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "Bearer";
    }

    /// <summary>
    /// 刷新 Token 响应
    /// </summary>
    public class RefreshTokenResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public RefreshTokenData? Data { get; set; }
    }

    public class RefreshTokenData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    /// <summary>
    /// 用户信息响应
    /// </summary>
    public class UserProfileResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public UserProfile? Data { get; set; }
    }

    #endregion

    #region API Key 相关模型

    /// <summary>
    /// API Key 信息
    /// </summary>
    public class ApiKeyInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("prefix")]
        public string Prefix { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "active";

        [JsonPropertyName("last_used_at")]
        public DateTime? LastUsedAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// 显示用的掩码密钥（如 "sk-xxxx...xxxx"）
        /// </summary>
        public string MaskedKey => $"{Prefix}...";
    }

    /// <summary>
    /// 创建 API Key 的结果（包含完整密钥，仅返回一次）
    /// </summary>
    public class CreateApiKeyResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public ApiKeyInfo? KeyInfo { get; set; }
        
        /// <summary>
        /// 完整的 API Key（仅在创建时返回一次）
        /// </summary>
        public string? FullKey { get; set; }
    }

    /// <summary>
    /// API Key 列表响应
    /// </summary>
    public class ApiKeyListResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public List<ApiKeyInfo>? Data { get; set; }
    }

    /// <summary>
    /// 创建 API Key 响应
    /// </summary>
    public class CreateApiKeyResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public CreateApiKeyResponseData? Data { get; set; }
    }

    public class CreateApiKeyResponseData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("prefix")]
        public string Prefix { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 删除 API Key 响应
    /// </summary>
    public class DeleteApiKeyResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    #endregion
}



