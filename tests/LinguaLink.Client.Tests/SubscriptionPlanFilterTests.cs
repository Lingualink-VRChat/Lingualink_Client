using System.Collections.Generic;
using System.Linq;
using lingualink_client.Models.Auth;
using lingualink_client.Services.Auth;
using Xunit;

namespace LinguaLink.Client.Tests;

public class SubscriptionPlanFilterTests
{
    [Fact]
    public void FilterPurchasablePlans_RemovesFreeInactiveAndInvalidPlans()
    {
        var paid = new SubscriptionPlanInfo
        {
            Id = "paid",
            Name = "Paid",
            PriceMonthlyCents = 100,
            IsActive = true
        };

        var plans = new List<SubscriptionPlanInfo>
        {
            paid,
            new() { Id = "free", Name = "Free", PriceMonthlyCents = 0, IsActive = true },
            new() { Id = "inactive", Name = "Inactive", PriceMonthlyCents = 100, IsActive = false },
            new() { Id = "", Name = "Missing Id", PriceMonthlyCents = 100, IsActive = true }
        };

        var result = SubscriptionPlanFilter.FilterPurchasablePlans(plans);

        var only = Assert.Single(result);
        Assert.Same(paid, only);
    }

    [Fact]
    public void FilterPurchasablePlans_AllowsPlansWithUnknownActiveState()
    {
        var plan = new SubscriptionPlanInfo
        {
            Id = "legacy",
            Name = "Legacy",
            PriceMonthlyCents = 100,
            IsActive = null
        };

        var result = SubscriptionPlanFilter.FilterPurchasablePlans(new[] { plan });

        Assert.Equal("legacy", result.Single().Id);
    }
}
