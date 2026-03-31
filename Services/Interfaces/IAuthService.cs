using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using lingualink_client.Models.Auth;

namespace lingualink_client.Services.Interfaces
{
    /// <summary>
    /// 认证服务接口
    /// </summary>
    public interface IAuthService : IDisposable
    {
        /// <summary>
        /// Auth Server 根地址（用于 Checkout 等 Web 页面）
        /// </summary>
        string AuthServerUrl { get; }

        /// <summary>
        /// 当前登录状态
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// 当前用户信息
        /// </summary>
        UserProfile? CurrentUser { get; }

        /// <summary>
        /// 获取当前有效的 Access Token
        /// </summary>
        Task<string?> GetAccessTokenAsync();

        /// <summary>
        /// 执行 OAuth 登录流程
        /// </summary>
        Task<LoginResult> LoginAsync();

        /// <summary>
        /// 退出登录
        /// </summary>
        Task LogoutAsync();

        /// <summary>
        /// 当业务接口返回 401 时，清理本地会话并触发未登录状态
        /// </summary>
        Task HandleUnauthorizedAsync();

        /// <summary>
        /// 刷新用户信息
        /// </summary>
        Task<UserProfile?> RefreshUserProfileAsync();

        /// <summary>
        /// 更新用户资料（username / avatar_url）
        /// </summary>
        Task<ApiOperationResult> UpdateUserProfileAsync(string? username, string? avatarUrl);

        /// <summary>
        /// 发送邮箱绑定验证码
        /// </summary>
        Task<ApiOperationResult> SendBindEmailCodeAsync(string email);

        /// <summary>
        /// 使用验证码和密码确认绑定邮箱
        /// </summary>
        Task<ApiOperationResult> BindEmailAsync(string email, string code, string password);

        /// <summary>
        /// 绑定第三方账号
        /// </summary>
        Task<ApiOperationResult> BindProviderAsync(string provider, string providerUserId);

        /// <summary>
        /// 通过 OAuth 绑定社交账号（QQ / 微信）
        /// </summary>
        Task<ApiOperationResult> BindSocialProviderAsync(string provider);

        /// <summary>
        /// 获取可购买套餐列表
        /// </summary>
        Task<IReadOnlyList<SubscriptionPlanInfo>> GetSubscriptionPlansAsync();

        /// <summary>
        /// 获取当前生效公告
        /// </summary>
        Task<IReadOnlyList<PublicAnnouncement>> GetActiveAnnouncementsAsync();

        /// <summary>
        /// 获取当前用户钱包余额
        /// </summary>
        Task<UserWalletInfo?> GetWalletAsync();

        /// <summary>
        /// 设置当前订阅的自动续费状态
        /// </summary>
        Task<ApiOperationResult> SetSubscriptionAutoRenewAsync(bool enabled);

        /// <summary>
        /// 创建订阅支付订单
        /// </summary>
        Task<CreateSubscriptionOrderResult> CreateSubscriptionOrderAsync(string planId, string paymentMethod, int durationMonths = 1);

        /// <summary>
        /// 查询订单状态
        /// </summary>
        Task<SubscriptionOrderInfo?> GetSubscriptionOrderStatusAsync(string outTradeNo);

        /// <summary>
        /// 尝试从存储恢复会话
        /// </summary>
        Task TryRestoreSessionAsync();

        /// <summary>
        /// 登录状态变化事件
        /// </summary>
        event EventHandler<bool>? LoginStateChanged;

    }
}
