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
        /// 当前登录状态
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// 当前用户信息
        /// </summary>
        UserProfile? CurrentUser { get; }

        /// <summary>
        /// 获取当前有效的 Access Token（自动刷新）
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
        /// 刷新用户信息
        /// </summary>
        Task<UserProfile?> RefreshUserProfileAsync();

        /// <summary>
        /// 尝试从存储恢复会话
        /// </summary>
        Task TryRestoreSessionAsync();

        /// <summary>
        /// 登录状态变化事件
        /// </summary>
        event EventHandler<bool>? LoginStateChanged;

        #region API Key 管理

        /// <summary>
        /// 获取用户的 API Key 列表
        /// </summary>
        Task<List<ApiKeyInfo>> GetApiKeysAsync();

        /// <summary>
        /// 创建新的 API Key
        /// </summary>
        /// <param name="name">API Key 名称</param>
        Task<CreateApiKeyResult> CreateApiKeyAsync(string name);

        /// <summary>
        /// 删除（吊销）API Key
        /// </summary>
        /// <param name="keyId">API Key ID</param>
        Task<bool> DeleteApiKeyAsync(string keyId);

        #endregion
    }
}



