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

            var (accessTokenProvider, unauthorizedHandler) = ResolveAuthContext();

            Debug.WriteLine($"[LingualinkApiServiceFactory] Creating API service with settings:");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   ActiveServerUrl: '{settings.ActiveServerUrl}'");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   UseOfficialAuthFlow: true");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   HasApiKey: false");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   HasTokenProvider: {accessTokenProvider != null}");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   OpusComplexity: {settings.OpusComplexity}");

            return new LingualinkApiService(
                serverUrl: settings.ActiveServerUrl,
                apiKey: string.Empty,
                accessTokenProvider: accessTokenProvider,
                unauthorizedHandler: unauthorizedHandler,
                opusComplexity: settings.OpusComplexity
            );
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
