using lingualink_client.Models;
using lingualink_client.Services;
using Xunit;

namespace LinguaLink.Client.Tests;

public class AppEndpointsTests
{
    [Fact]
    public void NormalizeBaseUrl_ReturnsFallback_WhenInputIsEmpty()
    {
        var result = AppEndpoints.NormalizeBaseUrl("   ", AppEndpoints.DefaultAuthServerUrl);

        Assert.Equal(AppEndpoints.DefaultAuthServerUrl, result);
    }

    [Fact]
    public void NormalizeBaseUrl_TrimsAndRemovesTrailingSlash()
    {
        var result = AppEndpoints.NormalizeBaseUrl(" https://example.com/base/ ", AppEndpoints.DefaultAuthServerUrl);

        Assert.Equal("https://example.com/base", result);
    }

    [Fact]
    public void EnsureTrailingSlash_AppendsSlashOnlyOnce()
    {
        Assert.Equal("https://example.com/base/", AppEndpoints.EnsureTrailingSlash("https://example.com/base"));
        Assert.Equal("https://example.com/base/", AppEndpoints.EnsureTrailingSlash("https://example.com/base/"));
    }

    [Fact]
    public void ResolveUpdateFeed_PrefersOverrideUrl()
    {
        var result = UpdateFeedResolver.Resolve(" https://mirror.example.com/feed ", UpdateFeedChannel.SelfContained);

        Assert.Equal("https://mirror.example.com/feed/", result);
    }

    [Theory]
    [InlineData(UpdateFeedChannel.SelfContained, AppEndpoints.SelfContainedUpdateFeedUrl)]
    [InlineData(UpdateFeedChannel.FrameworkDependent, AppEndpoints.FrameworkDependentUpdateFeedUrl)]
    [InlineData(UpdateFeedChannel.None, null)]
    public void ResolveUpdateFeed_UsesBuiltInChannels(UpdateFeedChannel channel, string? expected)
    {
        var result = UpdateFeedResolver.Resolve(null, channel);

        Assert.Equal(expected, result);
    }
}
