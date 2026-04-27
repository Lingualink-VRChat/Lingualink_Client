using lingualink_client.Models;

namespace lingualink_client.ViewModels
{
    internal static class AccountCheckoutHostResolver
    {
        public static string Resolve(string? authServerUrl)
        {
            if (string.IsNullOrWhiteSpace(authServerUrl))
            {
                return AppSettings.GetEffectiveAuthServerUrl();
            }

            return authServerUrl.Trim().TrimEnd('/');
        }
    }
}
