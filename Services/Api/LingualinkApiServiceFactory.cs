using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace lingualink_client.Services
{
    /// <summary>
    /// Lingualink API服务工厂
    /// 根据应用配置创建API服务实例
    /// </summary>
    public static class LingualinkApiServiceFactory
    {
        /// <summary>
        /// 根据应用设置创建API服务实例
        /// </summary>
        /// <param name="settings">应用设置</param>
        /// <returns>API服务实例</returns>
        public static ILingualinkApiService CreateApiService(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var apiKey = ResolveCustomServerApiKey(settings);
            var useCustomServerAuth = settings.UseCustomServer;
            var (accessTokenProvider, unauthorizedHandler) = useCustomServerAuth
                ? (null, null)
                : ResolveAuthContext();

            Debug.WriteLine($"[LingualinkApiServiceFactory] Creating API service with settings:");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   ServerUrl: '{settings.ServerUrl}'");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   UseCustomServer: {settings.UseCustomServer}");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   HasApiKey: {useCustomServerAuth && !string.IsNullOrWhiteSpace(apiKey)}");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   HasTokenProvider: {accessTokenProvider != null}");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   OpusComplexity: {settings.OpusComplexity}");

            return new LingualinkApiService(
                serverUrl: settings.ServerUrl,
                apiKey: useCustomServerAuth ? apiKey : null,
                accessTokenProvider: accessTokenProvider,
                unauthorizedHandler: unauthorizedHandler,
                opusComplexity: settings.OpusComplexity
            );
        }

        /// <summary>
        /// 创建用于测试连接的临时API服务实例
        /// </summary>
        /// <param name="serverUrl">服务器URL</param>
        /// <returns>API服务实例</returns>
        public static ILingualinkApiService CreateTestApiService(string serverUrl, string? apiKey = null)
        {
            Debug.WriteLine($"[LingualinkApiServiceFactory] Creating test API service with URL: {serverUrl}");

            return new LingualinkApiService(
                serverUrl: serverUrl,
                apiKey: apiKey,
                opusComplexity: 7 // 使用默认复杂度
            );
        }

        private static string ResolveCustomServerApiKey(AppSettings settings)
        {
            return string.IsNullOrWhiteSpace(settings.CustomApiKey)
                ? settings.ApiKey
                : settings.CustomApiKey;
        }

        private static (Func<Task<string?>>? accessTokenProvider, Func<Task>? unauthorizedHandler) ResolveAuthContext()
        {
            if (ServiceContainer.TryResolve<IAuthService>(out var authService) && authService != null)
            {
                return (authService.GetAccessTokenAsync, authService.HandleUnauthorizedAsync);
            }

            return (null, null);
        }
    }
}
