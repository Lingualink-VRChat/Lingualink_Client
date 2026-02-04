using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using lingualink_client.Models.Auth;

namespace lingualink_client.Services.Auth
{
    /// <summary>
    /// 使用 DPAPI 安全存储 Token
    /// </summary>
    public class SecureTokenStorage
    {
        private readonly string _tokenFilePath;

        public SecureTokenStorage()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "LinguaLink");
            Directory.CreateDirectory(appFolder);
            _tokenFilePath = Path.Combine(appFolder, "auth.dat");
        }

        /// <summary>
        /// 保存 Token（加密存储）
        /// </summary>
        public async Task SaveTokenAsync(TokenResponse token)
        {
            try
            {
                var json = JsonSerializer.Serialize(token, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                var plainBytes = Encoding.UTF8.GetBytes(json);

                // 使用 DPAPI 加密（当前用户范围）
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                await File.WriteAllBytesAsync(_tokenFilePath, encryptedBytes);
                Debug.WriteLine("[SecureTokenStorage] Token saved successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecureTokenStorage] Failed to save token: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 加载 Token（解密读取）
        /// </summary>
        public async Task<TokenResponse?> LoadTokenAsync()
        {
            if (!File.Exists(_tokenFilePath))
            {
                Debug.WriteLine("[SecureTokenStorage] Token file not found");
                return null;
            }

            try
            {
                var encryptedBytes = await File.ReadAllBytesAsync(_tokenFilePath);
                var plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                var json = Encoding.UTF8.GetString(plainBytes);
                var token = JsonSerializer.Deserialize<TokenResponse>(json);
                
                Debug.WriteLine("[SecureTokenStorage] Token loaded successfully");
                return token;
            }
            catch (CryptographicException ex)
            {
                Debug.WriteLine($"[SecureTokenStorage] Failed to decrypt token (may be from different user): {ex.Message}");
                // 加密数据损坏或来自其他用户，删除并返回 null
                ClearToken();
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecureTokenStorage] Failed to load token: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清除存储的 Token
        /// </summary>
        public void ClearToken()
        {
            try
            {
                if (File.Exists(_tokenFilePath))
                {
                    File.Delete(_tokenFilePath);
                    Debug.WriteLine("[SecureTokenStorage] Token cleared");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecureTokenStorage] Failed to clear token: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否存在已存储的 Token
        /// </summary>
        public bool HasStoredToken()
        {
            return File.Exists(_tokenFilePath);
        }
    }
}



