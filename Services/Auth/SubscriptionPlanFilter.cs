using System;
using System.Collections.Generic;
using lingualink_client.Models.Auth;

namespace lingualink_client.Services.Auth
{
    internal static class SubscriptionPlanFilter
    {
        public static IReadOnlyList<SubscriptionPlanInfo> FilterPurchasablePlans(IReadOnlyList<SubscriptionPlanInfo>? plans)
        {
            if (plans == null || plans.Count == 0)
            {
                return Array.Empty<SubscriptionPlanInfo>();
            }

            var filtered = new List<SubscriptionPlanInfo>(plans.Count);
            foreach (var plan in plans)
            {
                if (plan == null)
                {
                    continue;
                }

                if (plan.IsActive.HasValue && !plan.IsActive.Value)
                {
                    continue;
                }

                if (plan.IsFreePlan)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(plan.Id))
                {
                    continue;
                }

                filtered.Add(plan);
            }

            return filtered;
        }
    }
}
