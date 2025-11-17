using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using System;
using System.Diagnostics;

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

            Debug.WriteLine($"[LingualinkApiServiceFactory] Creating API service with settings:");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   ServerUrl: '{settings.ServerUrl}'");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   ApiKey: '{settings.ApiKey}'");
            Debug.WriteLine($"[LingualinkApiServiceFactory]   OpusComplexity: {settings.OpusComplexity}");

            return new LingualinkApiService(
                serverUrl: settings.ServerUrl,
                apiKey: settings.ApiKey,
                opusComplexity: settings.OpusComplexity
            );
        }

        /// <summary>
        /// 创建用于测试连接的临时API服务实例
        /// </summary>
        /// <param name="serverUrl">服务器URL</param>
        /// <param name="apiKey">API密钥</param>
        /// <returns>API服务实例</returns>
        public static ILingualinkApiService CreateTestApiService(string serverUrl, string apiKey = "")
        {
            Debug.WriteLine($"[LingualinkApiServiceFactory] Creating test API service with URL: {serverUrl}");

            return new LingualinkApiService(
                serverUrl: serverUrl,
                apiKey: apiKey,
                opusComplexity: 7 // 使用默认复杂度
            );
        }
    }
}
