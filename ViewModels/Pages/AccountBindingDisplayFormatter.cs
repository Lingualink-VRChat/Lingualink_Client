using lingualink_client.Models.Auth;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    internal static class AccountBindingDisplayFormatter
    {
        public static string BuildSocialBindingStatusDisplay(SocialBindingInfo? binding)
        {
            if (binding?.Bound != true)
            {
                return LanguageManager.GetString("AccountUnbound");
            }

            var masked = binding.AccountMasked?.Trim();
            if (string.IsNullOrWhiteSpace(masked))
            {
                return LanguageManager.GetString("AccountBound");
            }

            return string.Format(LanguageManager.GetString("AccountBoundMaskedFormat"), masked);
        }
    }
}
