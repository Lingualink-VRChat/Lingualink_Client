using System;
using System.Net.Mail;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    internal static class AccountProfileInputValidator
    {
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var trimmed = email.Trim();
            try
            {
                var parsed = new MailAddress(trimmed);
                return string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool HasValidBindEmailPassword(string? password, string? confirmPassword)
        {
            var normalizedPassword = password ?? string.Empty;
            if (normalizedPassword.Length < 8 || normalizedPassword.Length > 128)
            {
                return false;
            }

            return string.Equals(normalizedPassword, confirmPassword ?? string.Empty, StringComparison.Ordinal);
        }

        public static bool TryValidateUsername(string? value, out string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = LanguageManager.GetString("AccountUsernameRequired");
                return false;
            }

            if (value.Length > 100)
            {
                errorMessage = LanguageManager.GetString("AccountUsernameTooLong");
                return false;
            }

            foreach (var c in value)
            {
                if (c == '/')
                {
                    errorMessage = LanguageManager.GetString("AccountUsernameSlashInvalid");
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
