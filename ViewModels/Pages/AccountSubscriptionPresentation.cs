using System;
using lingualink_client.Models.Auth;
using lingualink_client.Services;

namespace lingualink_client.ViewModels
{
    internal static class AccountSubscriptionPresentation
    {
        public static string BuildSubscriptionStatusText(SubscriptionInfo subscription)
        {
            if (!subscription.IsPaidPlan)
            {
                return LanguageManager.GetString("AccountPlanUnsubscribed");
            }

            if (subscription.IsPaidActiveNow)
            {
                return LanguageManager.GetString("AccountSubscriptionStatusActive");
            }

            var now = DateTime.UtcNow;
            if (subscription.StartDate.HasValue && subscription.StartDate.Value.ToUniversalTime() > now)
            {
                return LanguageManager.GetString("AccountSubscriptionStatusPending");
            }

            if (subscription.EffectiveEndDate.HasValue && subscription.EffectiveEndDate.Value.ToUniversalTime() < now)
            {
                return LanguageManager.GetString("AccountSubscriptionStatusExpired");
            }

            return string.IsNullOrWhiteSpace(subscription.Status)
                ? LanguageManager.GetString("AccountSubscriptionStatusNotOpened")
                : subscription.Status;
        }

        public static string BuildExpiryReminderText(SubscriptionInfo subscription)
        {
            if (!subscription.IsPaidPlan)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptNoSubscription");
            }

            if (subscription.AutoRenew && subscription.IsPaidActiveNow)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptAutoRenew");
            }

            var endDate = subscription.EffectiveEndDate;
            if (!endDate.HasValue)
            {
                return subscription.IsPaidActiveNow
                    ? LanguageManager.GetString("AccountSubscriptionPromptUnavailable")
                    : LanguageManager.GetString("AccountSubscriptionPromptNoActiveSubscription");
            }

            var daysRemaining = (endDate.Value.ToLocalTime().Date - DateTime.Now.Date).Days;
            if (daysRemaining < 0)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptExpired");
            }

            if (!subscription.IsPaidActiveNow)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptPending");
            }

            if (daysRemaining == 0)
            {
                return LanguageManager.GetString("AccountSubscriptionPromptExpireToday");
            }

            if (daysRemaining <= 7)
            {
                return string.Format(LanguageManager.GetString("AccountSubscriptionPromptExpireInDaysFormat"), daysRemaining);
            }

            return string.Empty;
        }

        public static string BuildSubscriptionRemainingText(SubscriptionInfo subscription)
        {
            if (!subscription.IsPaidPlan)
            {
                return LanguageManager.GetString("AccountPlanUnsubscribed");
            }

            var endDate = subscription.EffectiveEndDate;
            if (!endDate.HasValue)
            {
                return subscription.IsPaidActiveNow
                    ? LanguageManager.GetString("AccountRemainingUnknown")
                    : LanguageManager.GetString("AccountRemainingPending");
            }

            var remaining = endDate.Value.ToUniversalTime() - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return LanguageManager.GetString("AccountRemainingExpired");
            }

            if (remaining.TotalDays >= 1)
            {
                return string.Format(LanguageManager.GetString("AccountRemainingDaysFormat"), Math.Floor(remaining.TotalDays));
            }

            if (remaining.TotalHours >= 1)
            {
                return string.Format(LanguageManager.GetString("AccountRemainingHoursFormat"), Math.Floor(remaining.TotalHours));
            }

            return string.Format(LanguageManager.GetString("AccountRemainingMinutesFormat"), Math.Max(1, Math.Floor(remaining.TotalMinutes)));
        }

        public static string FormatDate(DateTime value)
        {
            return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        public static string FormatCurrencyAmount(int amountCents)
        {
            return $"¥{amountCents / 100.0:0.00}";
        }

        public static string MapUserStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return LanguageManager.GetString("AccountStatusUnknown");
            }

            return status.Trim().ToLowerInvariant() switch
            {
                "active" => LanguageManager.GetString("AccountStatusNormal"),
                "inactive" => LanguageManager.GetString("AccountStatusInactive"),
                "disabled" => LanguageManager.GetString("AccountStatusDisabled"),
                _ => status
            };
        }
    }
}
