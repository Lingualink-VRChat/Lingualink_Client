using System.Collections.Generic;
using lingualink_client.Models;
using lingualink_client.Services.Events;
using lingualink_client.ViewModels.Components;
using Xunit;

namespace LinguaLink.Client.Tests;

public class ConversationHistoryLogicTests
{
    [Fact]
    public void EntryFilter_MatchesBySourceAndStatus()
    {
        var entry = CreateEntry(source: TranslationSource.Audio, isSuccess: true);

        var result = ConversationHistoryEntryFilter.Matches(
            entry,
            TranslationSource.Audio,
            true,
            null,
            searchInTranslations: true);

        Assert.True(result);
    }

    [Fact]
    public void EntryFilter_RejectsDifferentSource()
    {
        var entry = CreateEntry(source: TranslationSource.Text, isSuccess: true);

        var result = ConversationHistoryEntryFilter.Matches(
            entry,
            TranslationSource.Audio,
            null,
            null,
            searchInTranslations: true);

        Assert.False(result);
    }

    [Fact]
    public void EntryFilter_UsesTranslationsOnlyWhenEnabled()
    {
        var entry = CreateEntry(translations: new Dictionary<string, string> { ["ja"] = "こんにちは LinguaLink" });

        var withoutTranslations = ConversationHistoryEntryFilter.Matches(
            entry,
            null,
            null,
            "lingualink",
            searchInTranslations: false);

        var withTranslations = ConversationHistoryEntryFilter.Matches(
            entry,
            null,
            null,
            "lingualink",
            searchInTranslations: true);

        Assert.False(withoutTranslations);
        Assert.True(withTranslations);
    }

    [Fact]
    public void BuildSummaryExport_SkipsEmptyValues()
    {
        var result = ConversationHistoryTextLogic.BuildSummaryExport(new[] { " one ", "", null, "two" });

        Assert.Equal("one" + System.Environment.NewLine + "two", result);
    }

    [Fact]
    public void BuildEntrySummary_PrefersProcessedTextAndNormalizesWhitespace()
    {
        var result = ConversationHistoryTextLogic.BuildEntrySummary("original", "line1\r\nline2");

        Assert.Equal("line1 line2", result);
    }

    [Fact]
    public void BuildEntrySummary_TruncatesLongText()
    {
        var input = new string('a', 105);

        var result = ConversationHistoryTextLogic.BuildEntrySummary(input, string.Empty, maxLength: 100);

        Assert.Equal(new string('a', 100) + "…", result);
    }

    private static ConversationEntry CreateEntry(
        TranslationSource source = TranslationSource.Unknown,
        bool isSuccess = true,
        string originalText = "hello",
        string processedText = "processed",
        Dictionary<string, string>? translations = null)
    {
        return new ConversationEntry
        {
            Source = source.ToString(),
            IsSuccess = isSuccess,
            OriginalText = originalText,
            ProcessedText = processedText,
            Translations = translations ?? new Dictionary<string, string>()
        };
    }
}
