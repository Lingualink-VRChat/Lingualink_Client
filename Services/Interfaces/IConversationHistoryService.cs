using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using lingualink_client.Models;

namespace lingualink_client.Services.Interfaces
{
    public interface IConversationHistoryService : IDisposable
    {
        event EventHandler<ConversationEntrySavedEventArgs>? EntrySaved;
        event EventHandler? StoragePathChanged;

        string SessionId { get; }
        string StorageFolder { get; }
        string DatabasePath { get; }
        bool IsEnabled { get; }

        Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ConversationEntry>> QueryEntriesAsync(ConversationHistoryQuery query, CancellationToken cancellationToken = default);
        Task<string> ExportAsync(IEnumerable<ConversationEntry> entries, ConversationExportFormat format, CancellationToken cancellationToken = default);
        Task<bool> ChangeStoragePathAsync(string newFolder, bool migrateExisting, CancellationToken cancellationToken = default);

        void ReloadSettings(AppSettings settings);
    }
}
