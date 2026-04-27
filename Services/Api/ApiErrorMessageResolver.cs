using System;
using System.Net;

namespace lingualink_client.Services
{
    internal static class ApiErrorMessageResolver
    {
        public static string? ResolveKnownErrorCode(HttpStatusCode statusCode, string? errorCode)
        {
            var normalized = errorCode?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (string.Equals(normalized, "free_trial_quota_exhausted", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageManager.GetString("FreeTrialQuotaExhausted");
            }

            if (statusCode == HttpStatusCode.TooManyRequests
                && string.Equals(normalized, "rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
            {
                return LanguageManager.GetString("RateLimitExceeded");
            }

            return null;
        }

        public static string ResolveHttpStatus(HttpStatusCode statusCode, string responseContent)
        {
            return statusCode switch
            {
                HttpStatusCode.Unauthorized => LanguageManager.GetString("ApiUnauthorized"),
                HttpStatusCode.Forbidden => LanguageManager.GetString("ApiForbidden"),
                HttpStatusCode.NotFound => LanguageManager.GetString("ApiEndpointNotFound"),
                HttpStatusCode.RequestEntityTooLarge => LanguageManager.GetString("ApiRequestTooLarge"),
                HttpStatusCode.TooManyRequests => LanguageManager.GetString("FreeTrialQuotaExhausted"),
                _ => string.Format(LanguageManager.GetString("ErrorServer"), (int)statusCode, responseContent)
            };
        }
    }
}
