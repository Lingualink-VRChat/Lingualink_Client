using System;

namespace lingualink_client.ViewModels
{
    internal static class AccountOrderStatus
    {
        public static bool IsTerminal(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var normalized = status.Trim();
            return string.Equals(normalized, "paid", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "failed", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "canceled", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "cancelled", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, "expired", StringComparison.OrdinalIgnoreCase);
        }
    }
}
