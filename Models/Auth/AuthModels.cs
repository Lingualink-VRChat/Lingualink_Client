using System;
using System.Collections.Generic;
using System.Text.Json;
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

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "active";

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 订阅/配额信息
        /// </summary>
        [JsonPropertyName("subscription")]
        public SubscriptionInfo? Subscription { get; set; }

        /// <summary>
        /// 社交账号绑定信息（微信 / QQ）
        /// </summary>
        [JsonPropertyName("social_bindings")]
        public SocialBindingsInfo? SocialBindings { get; set; }
    }

    public class SocialBindingsInfo
    {
        [JsonPropertyName("wechat")]
        public SocialBindingInfo? Wechat { get; set; }

        [JsonPropertyName("qq")]
        public SocialBindingInfo? Qq { get; set; }
    }

    public class SocialBindingInfo
    {
        [JsonPropertyName("bound")]
        public bool Bound { get; set; }

        [JsonPropertyName("account_masked")]
        public string? AccountMasked { get; set; }
    }

    /// <summary>
    /// 订阅信息（包月/包年有效期模式）
    /// </summary>
    public class SubscriptionInfo
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "inactive";

        [JsonPropertyName("start_date")]
        public DateTime? StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public DateTime? EndDate { get; set; }

        [JsonPropertyName("auto_renew")]
        public bool AutoRenew { get; set; }

        [JsonPropertyName("renew_url")]
        public string? RenewUrl { get; set; }

        [JsonPropertyName("plan")]
        public SubscriptionPlanInfo? Plan { get; set; }

        // 兼容旧字段，避免后端灰度期导致显示异常
        [JsonPropertyName("plan_name")]
        public string? LegacyPlanName { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime? LegacyExpiresAt { get; set; }

        [JsonIgnore]
        public string DisplayPlanName => !string.IsNullOrWhiteSpace(Plan?.Name)
            ? Plan!.Name
            : (LegacyPlanName ?? "Free");

        [JsonIgnore]
        public DateTime? EffectiveEndDate => EndDate ?? LegacyExpiresAt;

        [JsonIgnore]
        public bool IsActiveNow
        {
            get
            {
                if (!string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                if (StartDate.HasValue && StartDate.Value.ToUniversalTime() > now)
                {
                    return false;
                }

                var end = EffectiveEndDate;
                if (end.HasValue && end.Value.ToUniversalTime() < now)
                {
                    return false;
                }

                return true;
            }
        }

        [JsonIgnore]
        public bool IsPaidPlan
        {
            get
            {
                if (Plan != null)
                {
                    return !Plan.IsFreePlan;
                }

                if (string.IsNullOrWhiteSpace(LegacyPlanName))
                {
                    return false;
                }

                return !SubscriptionPlanInfo.IsFreePlanLabel(LegacyPlanName);
            }
        }

        [JsonIgnore]
        public bool IsPaidActiveNow => IsActiveNow && IsPaidPlan;
    }

    /// <summary>
    /// 套餐核心信息
    /// </summary>
    public class SubscriptionPlanInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("price_monthly_cents")]
        public int PriceMonthlyCents { get; set; }

        [JsonPropertyName("price_yearly_cents")]
        public int? PriceYearlyCents { get; set; }

        [JsonPropertyName("features")]
        public JsonElement Features { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }

        [JsonIgnore]
        public bool IsFreePlan
        {
            get
            {
                if (PriceMonthlyCents <= 0 && (!PriceYearlyCents.HasValue || PriceYearlyCents.Value <= 0))
                {
                    return true;
                }

                return IsFreePlanLabel(Code) || IsFreePlanLabel(Name);
            }
        }

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                var monthly = PriceMonthlyCents / 100m;
                var label = $"{Name}（¥{monthly:0.##}/月";

                if (PriceYearlyCents.HasValue)
                {
                    var yearly = PriceYearlyCents.Value / 100m;
                    label += $"，¥{yearly:0.##}/年";
                }

                return label + "）";
            }
        }

        public static bool IsFreePlanLabel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            return normalized.Contains("free")
                   || normalized.Contains("trial")
                   || normalized.Contains("免费")
                   || normalized.Contains("试用");
        }
    }

    /// <summary>
    /// 支付方式选项（用于下单 UI 绑定）
    /// </summary>
    public class PaymentMethodOption
    {
        public PaymentMethodOption()
        {
        }

        public PaymentMethodOption(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// 登录结果
    /// </summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public bool IsCancelled { get; set; }
        public string? ErrorMessage { get; set; }
        public UserProfile? User { get; set; }
    }

    /// <summary>
    /// OAuth 回调结果
    /// </summary>
    public class OAuthCallbackResult
    {
        public string Code { get; set; } = string.Empty;
        public string? AuthCode { get; set; }
        public string? BindStatus { get; set; }
        public string? BindProvider { get; set; }
        public string State { get; set; } = string.Empty;
        public string? MessageType { get; set; }
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int? ExpiresIn { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? TokenType { get; set; }
        public string? UserId { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Email { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        [JsonPropertyName("casdoor_name")]
        public string? CasdoorName { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }
    }

    public class BindPhoneRequest
    {
        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;
    }

    public class SendBindEmailCodeRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    public class BindEmailRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class SendBindEmailCodeResult
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    public class BindProviderRequest
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("provider_user_id")]
        public string ProviderUserId { get; set; } = string.Empty;
    }

    public class ProviderBindingResult
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("provider_user_id")]
        public string ProviderUserId { get; set; } = string.Empty;

        [JsonPropertyName("bound")]
        public bool Bound { get; set; }
    }

    public static class UserBindProviderCatalog
    {
        public static readonly IReadOnlyList<string> SocialOAuthProviders = new[]
        {
            "qq", "wechat"
        };

        public static readonly IReadOnlyList<string> AllowedProviders = new[]
        {
            "github", "google", "qq", "wechat", "wecom", "lark", "gitee", "gitlab",
            "facebook", "linkedin", "apple", "discord", "twitter", "douyin", "tiktok"
        };

        private static readonly HashSet<string> AllowedSet = new(AllowedProviders, StringComparer.OrdinalIgnoreCase);

        public static bool IsAllowed(string? provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return false;
            }

            return AllowedSet.Contains(provider.Trim());
        }

        public static bool IsSocialOAuthProvider(string? provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return false;
            }

            foreach (var candidate in SocialOAuthProviders)
            {
                if (string.Equals(candidate, provider.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class ApiOperationResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #region API 响应模型

    public class ApiEnvelope<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    /// <summary>
    /// 登录 URL 响应
    /// </summary>
    public class LoginUrlResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public LoginUrlData? Data { get; set; }
    }

    public class LoginUrlData
    {
        [JsonPropertyName("login_url")]
        public string? LoginUrl { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }
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

    public class ExchangeAuthCodeResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public ExchangeAuthCodeData? Data { get; set; }
    }

    public class ExchangeAuthCodeData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("user")]
        public UserProfile? User { get; set; }
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

        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        // 兼容旧实现（如果服务端仍返回 expires_in）
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

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public UserProfile? Data { get; set; }
    }

    /// <summary>
    /// 用户订阅信息响应
    /// </summary>
    public class UserSubscriptionResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public UserSubscriptionSummary? Data { get; set; }
    }

    public class UserSubscriptionSummary
    {
        [JsonPropertyName("subscription")]
        public UserSubscriptionRecord? Subscription { get; set; }

        [JsonPropertyName("plan")]
        public SubscriptionPlanInfo? Plan { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
    }

    public class UserSubscriptionRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("plan_id")]
        public string PlanId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("auto_renew")]
        public bool AutoRenew { get; set; }
    }

    /// <summary>
    /// 订阅套餐列表响应
    /// </summary>
    public class SubscriptionPlansResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public List<SubscriptionPlanInfo>? Data { get; set; }
    }

    /// <summary>
    /// 支付订单信息
    /// </summary>
    public class SubscriptionOrderInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("plan_id")]
        public string PlanId { get; set; } = string.Empty;

        [JsonPropertyName("subscription_id")]
        public string? SubscriptionId { get; set; }

        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; } = string.Empty;

        [JsonPropertyName("trade_no")]
        public string? TradeNo { get; set; }

        [JsonPropertyName("payment_method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "CNY";

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("duration_months")]
        public int DurationMonths { get; set; }

        [JsonPropertyName("paid_at")]
        public DateTime? PaidAt { get; set; }

        [JsonPropertyName("expire_at")]
        public DateTime? ExpireAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [JsonIgnore]
        public string AmountDisplay => $"¥{AmountCents / 100m:0.##}";
    }

    public class PaymentInstructionInfo
    {
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("integration_status")]
        public string IntegrationStatus { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("notify_endpoint")]
        public string? NotifyEndpoint { get; set; }

        [JsonPropertyName("code_url")]
        public string? CodeUrl { get; set; }

        [JsonPropertyName("order_expire_at")]
        public DateTime? OrderExpireAt { get; set; }
    }

    public class CreateSubscriptionOrderData
    {
        [JsonPropertyName("order")]
        public SubscriptionOrderInfo? Order { get; set; }

        [JsonPropertyName("payment")]
        public PaymentInstructionInfo? Payment { get; set; }
    }

    public class CreateSubscriptionOrderResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public CreateSubscriptionOrderData? Data { get; set; }
    }

    public class SubscriptionOrderStatusResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("data")]
        public SubscriptionOrderInfo? Data { get; set; }
    }

    public class CreateSubscriptionOrderResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public SubscriptionOrderInfo? Order { get; set; }
        public PaymentInstructionInfo? Payment { get; set; }
    }

    #endregion

}
