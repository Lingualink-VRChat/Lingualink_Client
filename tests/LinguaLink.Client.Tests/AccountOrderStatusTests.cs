using lingualink_client.ViewModels;
using Xunit;

namespace LinguaLink.Client.Tests;

public sealed class AccountOrderStatusTests
{
    [Theory]
    [InlineData("paid")]
    [InlineData("FAILED")]
    [InlineData("canceled")]
    [InlineData("cancelled")]
    [InlineData(" expired ")]
    public void IsTerminal_TerminalStatuses_ReturnsTrue(string status)
    {
        Assert.True(AccountOrderStatus.IsTerminal(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pending")]
    [InlineData("processing")]
    public void IsTerminal_NonTerminalStatuses_ReturnsFalse(string? status)
    {
        Assert.False(AccountOrderStatus.IsTerminal(status));
    }
}
