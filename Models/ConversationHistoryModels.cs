using System;
using System.Collections.Generic;
using LiteDB;
using lingualink_client.ViewModels.Events;

namespace lingualink_client.Models
{
    public class ConversationEntry
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string SessionId { get; set; } = string.Empty;
        public string Source { get; set; } = TranslationSource.Unknown.ToString();
        public string TriggerReason { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string OriginalText { get; set; } = string.Empty;
        public string ProcessedText { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public double DurationSeconds { get; set; }
        public List<string> TargetLanguages { get; set; } = new();
        public Dictionary<string, string> Translations { get; set; } = new();
        public string Task { get; set; } = string.Empty;
        public string? RequestId { get; set; }
        public ApiMetadata? Metadata { get; set; }
    }

    public class ConversationSession
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime StartedUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public int EntryCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }

    public class ConversationHistoryQuery
    {
        public string? SessionId { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public TranslationSource? Source { get; set; }
        public bool? IsSuccess { get; set; }
        public IReadOnlyCollection<string>? TargetLanguages { get; set; }
        public string? SearchText { get; set; }
        public bool SearchInTranslations { get; set; } = true;
        public int Skip { get; set; }
        public int? Limit { get; set; }
        public bool SortDescending { get; set; } = true;
    }

    public enum ConversationExportFormat
    {
        PlainText,
        Markdown,
        Json
    }

    public class ConversationEntrySavedEventArgs : EventArgs
    {
        public ConversationEntrySavedEventArgs(ConversationEntry entry)
        {
            Entry = entry;
        }

        public ConversationEntry Entry { get; }
    }
}
