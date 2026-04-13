using lingualink_client.Models;

namespace lingualink_client.Services
{
    public enum UpdateFeedChannel
    {
        None,
        SelfContained,
        FrameworkDependent
    }

    public static class UpdateFeedResolver
    {
        public static string? Resolve(string? overrideUrl, UpdateFeedChannel channel)
        {
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return AppEndpoints.EnsureTrailingSlash(overrideUrl);
            }

            return channel switch
            {
                UpdateFeedChannel.SelfContained => AppEndpoints.SelfContainedUpdateFeedUrl,
                UpdateFeedChannel.FrameworkDependent => AppEndpoints.FrameworkDependentUpdateFeedUrl,
                _ => null
            };
        }
    }
}
