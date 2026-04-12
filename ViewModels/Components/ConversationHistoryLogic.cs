using System;
using System.Collections.Generic;
using System.Linq;
using lingualink_client.Models;
using lingualink_client.Services.Events;

namespace lingualink_client.ViewModels.Components
{
    public static class ConversationHistoryEntryFilter
    {
        public static bool Matches(
            ConversationEntry entry,
            TranslationSource? selectedSource,
            bool? selectedStatus,
            string? searchText,
            bool searchInTranslations)
        {
            if (selectedSource.HasValue
                && !entry.Source.Equals(selectedSource.Value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (selectedStatus.HasValue && entry.IsSuccess != selectedStatus.Value)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            var text = searchText.Trim();
            if (Contains(entry.OriginalText, text) || Contains(entry.ProcessedText, text))
            {
                return true;
            }

            return searchInTranslations && entry.Translations.Values.Any(value => Contains(value, text));
        }

        private static bool Contains(string? source, string value)
        {
            return !string.IsNullOrWhiteSpace(source)
                && source.Contains(value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class ConversationHistoryTextLogic
    {
        public static string BuildSummaryExport(IEnumerable<string?> summaries)
        {
            var normalized = summaries
                .Select(summary => summary?.Trim())
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .Cast<string>()
                .ToList();

            return normalized.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, normalized);
        }

        public static string BuildEntrySummary(string originalText, string processedText, int maxLength = 100)
        {
            var source = !string.IsNullOrWhiteSpace(processedText) ? processedText : originalText;
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var normalized = source.Replace("\r", string.Empty).Replace('\n', ' ');
            return normalized.Length > maxLength ? normalized[..maxLength] + "…" : normalized;
        }
    }
}
