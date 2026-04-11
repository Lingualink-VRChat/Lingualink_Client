namespace lingualink_client.Models
{
    /// <summary>
    /// Centralizes the client's built-in service endpoints and download URLs.
    /// </summary>
    public static class AppEndpoints
    {
        public const string OfficialApiBaseUrl = "https://api.lingualink.aiatechco.com/api/v1/";
        public const string LegacyCustomApiBaseUrl = "https://api2.lingualink.aiatechco.com/api/v1/";
        public const string LegacyLocalOfficialApiBaseUrl = "http://localhost:8080/api/v1/";

        public const string DefaultAuthServerUrl = "https://auth.lingualink.aiatechco.com";
        public const string DownloadBaseUrl = "https://download.cn-nb1.rains3.com/lingualink";
        public const string SelfContainedUpdateFeedUrl = DownloadBaseUrl + "/stable-self-contained/";
        public const string FrameworkDependentUpdateFeedUrl = DownloadBaseUrl + "/stable-framework-dependent/";
        public const string WebView2RuntimeDownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        public static string NormalizeBaseUrl(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().TrimEnd('/');
        }

        public static string EnsureTrailingSlash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.EndsWith("/", System.StringComparison.Ordinal) ? trimmed : trimmed + "/";
        }
    }
}
