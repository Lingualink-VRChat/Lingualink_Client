using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace lingualink_client.Services
{
    public static class DeviceFingerprintService
    {
        public static string Generate()
        {
            var parts = new List<string>();

            TryCollect(parts, "SELECT ProcessorId FROM Win32_Processor", "ProcessorId", takeFirstOnly: false);
            TryCollect(parts, "SELECT SerialNumber FROM Win32_BaseBoard", "SerialNumber", takeFirstOnly: false);
            TryCollect(parts, "SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType LIKE '%fixed%'", "SerialNumber", takeFirstOnly: true);
            TryCollect(parts, "SELECT SerialNumber FROM Win32_BIOS", "SerialNumber", takeFirstOnly: false);

            var raw = string.Join("|", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static void TryCollect(List<string> parts, string query, string propertyName, bool takeFirstOnly)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                foreach (var obj in searcher.Get())
                {
                    var value = obj[propertyName]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        parts.Add(value);
                    }
                    if (takeFirstOnly)
                    {
                        break;
                    }
                }
            }
            catch
            {
            }
        }
    }
}
