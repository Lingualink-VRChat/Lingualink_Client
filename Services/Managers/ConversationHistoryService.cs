using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Events;

namespace lingualink_client.Services.Managers
{
    public class ConversationHistoryService : IConversationHistoryService
    {
        private const string DatabaseFileName = "conversation_history.db";
        private const string CollectionName = "entries";

        private static readonly string DefaultHistoryRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "lingualink_client",
            "history");

        private const string HistoryLogCategory = "History";

        private readonly IEventAggregator _eventAggregator;
        private readonly SettingsService _settingsService;
        private readonly ILoggingManager _loggingManager;
        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
        private readonly object _initLock = new();

        private AppSettings _settings;
        private LiteDatabase? _database;
        private ILiteCollection<ConversationEntry>? _collection;
        private string _storageFolder = string.Empty;
        private string _databasePath = string.Empty;
        private string _sessionId = string.Empty;
        private bool _disposed;

        public event EventHandler<ConversationEntrySavedEventArgs>? EntrySaved;
        public event EventHandler? StoragePathChanged;

        public ConversationHistoryService()
        {
            _eventAggregator = ServiceContainer.Resolve<IEventAggregator>();
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();
            _settingsService = new SettingsService();
            _settings = _settingsService.LoadSettings();

            InitializeStorage(_settings);

            _eventAggregator.Subscribe<TranslationCompletedEvent>(OnTranslationCompleted);
            _eventAggregator.Subscribe<SettingsChangedEvent>(OnSettingsChanged);
        }

        public string SessionId => _sessionId;
        public string StorageFolder => _storageFolder;
        public string DatabasePath => _databasePath;
        public bool IsEnabled => _settings.ConversationHistoryEnabled;

        private void InitializeStorage(AppSettings settings)
        {
            lock (_initLock)
            {
                var folder = ResolveStorageFolder(settings.ConversationHistoryStoragePath);
                Directory.CreateDirectory(folder);

                _storageFolder = folder;
                _databasePath = Path.Combine(_storageFolder, DatabaseFileName);

                _database?.Dispose();
                _database = CreateDatabase(_databasePath);
                _collection = _database.GetCollection<ConversationEntry>(CollectionName);
                EnsureIndexes();
                ApplyRetentionPolicy();

                _sessionId = CreateSessionId();

                if (string.IsNullOrWhiteSpace(settings.ConversationHistoryStoragePath) ||
                    !PathsEqual(settings.ConversationHistoryStoragePath, _storageFolder))
                {
                    settings.ConversationHistoryStoragePath = _storageFolder;
                    _settingsService.SaveSettings(settings);
                }
            }
        }

        private static LiteDatabase CreateDatabase(string path)
        {
            var mapper = BsonMapper.Global;
            mapper.EnumAsInteger = false;

            var connection = new ConnectionString
            {
                Filename = path,
                Connection = ConnectionType.Shared,
                Upgrade = true
            };

            return new LiteDatabase(connection);
        }

        private void EnsureIndexes()
        {
            if (_collection == null)
            {
                return;
            }

            _collection.EnsureIndex(x => x.TimestampUtc);
            _collection.EnsureIndex(x => x.SessionId);
            _collection.EnsureIndex(x => x.Source);
            _collection.EnsureIndex(x => x.IsSuccess);
        }

        private void ApplyRetentionPolicy()
        {
            if (_collection == null)
            {
                return;
            }

            var retentionDays = _settings.ConversationHistoryRetentionDays;
            if (retentionDays <= 0)
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            _collection.DeleteMany(entry => entry.TimestampUtc < cutoff);
        }

        private static string ResolveStorageFolder(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return Path.GetFullPath(configuredPath);
            }

            return DefaultHistoryRoot;
        }

        private static bool PathsEqual(string? pathA, string? pathB)
        {
            if (string.IsNullOrWhiteSpace(pathA) || string.IsNullOrWhiteSpace(pathB))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(pathA).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(pathB).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateSessionId()
        {
            return $"{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}";
        }

        private void OnTranslationCompleted(TranslationCompletedEvent e)
        {
            if (!_settings.ConversationHistoryEnabled)
            {
                return;
            }

            if (!e.IsSuccess && !_settings.ConversationHistoryIncludeFailures)
            {
                return;
            }

            var entry = BuildEntry(e);
            _ = PersistEntryAsync(entry);
        }

        private ConversationEntry BuildEntry(TranslationCompletedEvent e)
        {
            return new ConversationEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = e.TimestampUtc == default ? DateTime.UtcNow : e.TimestampUtc,
                SessionId = _sessionId,
                Source = e.Source.ToString(),
                TriggerReason = e.TriggerReason,
                IsSuccess = e.IsSuccess,
                OriginalText = e.OriginalText ?? string.Empty,
                ProcessedText = e.ProcessedText ?? string.Empty,
                ErrorMessage = e.ErrorMessage,
                DurationSeconds = e.Duration,
                TargetLanguages = e.TargetLanguages?.ToList() ?? new List<string>(),
                Translations = e.Translations != null
                    ? new Dictionary<string, string>(e.Translations)
                    : new Dictionary<string, string>(),
                Task = e.Task ?? string.Empty,
                RequestId = e.RequestId,
                Metadata = e.Metadata
            };
        }

        private async Task PersistEntryAsync(ConversationEntry entry)
        {
            try
            {
                await _writeSemaphore.WaitAsync().ConfigureAwait(false);

                EnsureDatabaseReady();
                _collection!.Insert(entry);
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Failed to persist conversation entry: {ex.Message}", LogLevel.Error, HistoryLogCategory, ex.Message);
            }
            finally
            {
                _writeSemaphore.Release();
            }

            EntrySaved?.Invoke(this, new ConversationEntrySavedEventArgs(entry));
        }

        private void EnsureDatabaseReady()
        {
            if (_collection != null)
            {
                return;
            }

            InitializeStorage(_settings);
        }

        private void OnSettingsChanged(SettingsChangedEvent e)
        {
            if (e.Settings == null)
            {
                return;
            }

            ReloadSettings(e.Settings);
        }

        public void ReloadSettings(AppSettings settings)
        {
            _settings = settings;

            if (!string.IsNullOrWhiteSpace(settings.ConversationHistoryStoragePath) &&
                !PathsEqual(settings.ConversationHistoryStoragePath, _storageFolder))
            {
                _ = ChangeStoragePathAsync(settings.ConversationHistoryStoragePath, migrateExisting: true);
            }

            ApplyRetentionPolicy();
        }

        public async Task<IReadOnlyList<ConversationSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
        {
            await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureDatabaseReady();
                var entries = _collection!.FindAll().ToList();

                var sessions = entries
                    .GroupBy(entry => entry.SessionId)
                    .Select(group => new ConversationSession
                    {
                        SessionId = group.Key,
                        StartedUtc = group.Min(x => x.TimestampUtc),
                        LastActivityUtc = group.Max(x => x.TimestampUtc),
                        EntryCount = group.Count(),
                        SuccessCount = group.Count(x => x.IsSuccess),
                        FailureCount = group.Count(x => !x.IsSuccess)
                    })
                    .OrderByDescending(session => session.LastActivityUtc)
                    .ToList();

                return sessions;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public async Task<IReadOnlyList<ConversationEntry>> QueryEntriesAsync(ConversationHistoryQuery query, CancellationToken cancellationToken = default)
        {
            await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureDatabaseReady();
                IEnumerable<ConversationEntry> entries = _collection!.FindAll();

                if (!string.IsNullOrWhiteSpace(query.SessionId))
                {
                    entries = entries.Where(entry => string.Equals(entry.SessionId, query.SessionId, StringComparison.OrdinalIgnoreCase));
                }

                if (query.Source.HasValue)
                {
                    var sourceValue = query.Source.Value.ToString();
                    entries = entries.Where(entry => string.Equals(entry.Source, sourceValue, StringComparison.OrdinalIgnoreCase));
                }

                if (query.IsSuccess.HasValue)
                {
                    entries = entries.Where(entry => entry.IsSuccess == query.IsSuccess.Value);
                }

                if (query.FromUtc.HasValue)
                {
                    entries = entries.Where(entry => entry.TimestampUtc >= query.FromUtc.Value);
                }

                if (query.ToUtc.HasValue)
                {
                    entries = entries.Where(entry => entry.TimestampUtc <= query.ToUtc.Value);
                }

                if (query.TargetLanguages is { Count: > 0 })
                {
                    var languageSet = new HashSet<string>(query.TargetLanguages.Select(l => l.ToLowerInvariant()));
                    entries = entries.Where(entry => entry.TargetLanguages.Any(lang => languageSet.Contains(lang.ToLowerInvariant())));
                }

                if (!string.IsNullOrWhiteSpace(query.SearchText))
                {
                    var text = query.SearchText.Trim();
                    entries = entries.Where(entry =>
                        (!string.IsNullOrEmpty(entry.OriginalText) && entry.OriginalText.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(entry.ProcessedText) && entry.ProcessedText.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                        (query.SearchInTranslations && entry.Translations.Values.Any(value => value.Contains(text, StringComparison.OrdinalIgnoreCase))));
                }

                IEnumerable<ConversationEntry> ordered = query.SortDescending
                    ? entries.OrderByDescending(entry => entry.TimestampUtc)
                    : entries.OrderBy(entry => entry.TimestampUtc);

                if (query.Skip > 0)
                {
                    ordered = ordered.Skip(query.Skip);
                }

                if (query.Limit.HasValue)
                {
                    ordered = ordered.Take(query.Limit.Value);
                }

                return ordered.ToList();
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public async Task<string> ExportAsync(IEnumerable<ConversationEntry> entries, ConversationExportFormat format, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var list = entries.ToList();

                return format switch
                {
                    ConversationExportFormat.Json => System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    }),
                    ConversationExportFormat.Markdown => BuildMarkdown(list),
                    _ => BuildPlainText(list)
                };
            }, cancellationToken).ConfigureAwait(false);
        }

        private static string BuildPlainText(IReadOnlyList<ConversationEntry> entries)
        {
            var builder = new StringBuilder();
            foreach (var entry in entries)
            {
                builder.AppendLine($"[{entry.TimestampUtc:u}] ({entry.Source}) {(entry.IsSuccess ? "Success" : "Failure")}");
                if (!string.IsNullOrWhiteSpace(entry.TriggerReason))
                {
                    builder.AppendLine($"Trigger: {entry.TriggerReason}");
                }

                if (!string.IsNullOrWhiteSpace(entry.OriginalText))
                {
                    builder.AppendLine("Original:");
                    builder.AppendLine(entry.OriginalText);
                }

                if (!string.IsNullOrWhiteSpace(entry.ProcessedText))
                {
                    builder.AppendLine("Processed:");
                    builder.AppendLine(entry.ProcessedText);
                }

                if (entry.Translations.Any())
                {
                    builder.AppendLine("Translations:");
                    foreach (var kvp in entry.Translations)
                    {
                        builder.AppendLine($"  [{kvp.Key}] {kvp.Value}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(entry.ErrorMessage))
                {
                    builder.AppendLine($"Error: {entry.ErrorMessage}");
                }

                builder.AppendLine(new string('-', 40));
            }

            return builder.ToString();
        }

        private static string BuildMarkdown(IReadOnlyList<ConversationEntry> entries)
        {
            var builder = new StringBuilder();
            foreach (var entry in entries)
            {
                builder.AppendLine($"### {entry.TimestampUtc:yyyy-MM-dd HH:mm:ss} ({entry.Source}) {(entry.IsSuccess ? "✅" : "❌")}");
                builder.AppendLine();
                builder.AppendLine($"- Session: `{entry.SessionId}`");
                if (!string.IsNullOrWhiteSpace(entry.TriggerReason))
                {
                    builder.AppendLine($"- Trigger: `{entry.TriggerReason}`");
                }
                builder.AppendLine($"- Duration: {entry.DurationSeconds:F2}s");
                if (entry.TargetLanguages.Any())
                {
                    builder.AppendLine($"- Targets: {string.Join(", ", entry.TargetLanguages)}");
                }

                if (!string.IsNullOrWhiteSpace(entry.OriginalText))
                {
                    builder.AppendLine();
                    builder.AppendLine("**Original**");
                    builder.AppendLine();
                    builder.AppendLine($"> {entry.OriginalText.Replace("\n", "\n> ")}");
                }

                if (!string.IsNullOrWhiteSpace(entry.ProcessedText))
                {
                    builder.AppendLine();
                    builder.AppendLine("**Processed**");
                    builder.AppendLine();
                    builder.AppendLine($"> {entry.ProcessedText.Replace("\n", "\n> ")}");
                }

                if (entry.Translations.Any())
                {
                    builder.AppendLine();
                    builder.AppendLine("**Translations**");
                    builder.AppendLine();
                    foreach (var kvp in entry.Translations)
                    {
                        builder.AppendLine($"- **{kvp.Key}**: {kvp.Value}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(entry.ErrorMessage))
                {
                    builder.AppendLine();
                    builder.AppendLine($"**Error:** {entry.ErrorMessage}");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        public async Task<bool> ChangeStoragePathAsync(string newFolder, bool migrateExisting, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(newFolder))
            {
                return false;
            }

            var targetFolder = Path.GetFullPath(newFolder);
            Directory.CreateDirectory(targetFolder);

            await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            var oldDbPath = _databasePath;
            try
            {
                var newDbPath = Path.Combine(targetFolder, DatabaseFileName);

                _database?.Dispose();
                _database = null;
                _collection = null;

                if (migrateExisting && File.Exists(oldDbPath))
                {
                    File.Copy(oldDbPath, newDbPath, overwrite: true);
                }

                _database = CreateDatabase(newDbPath);
                _collection = _database.GetCollection<ConversationEntry>(CollectionName);
                EnsureIndexes();

                _storageFolder = targetFolder;
                _databasePath = newDbPath;

                _settings.ConversationHistoryStoragePath = targetFolder;
                _settingsService.SaveSettings(_settings);

                StoragePathChanged?.Invoke(this, EventArgs.Empty);

                _eventAggregator.Publish(new SettingsChangedEvent
                {
                    Settings = _settings,
                    ChangeSource = nameof(ConversationHistoryService)
                });

                return true;
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Failed to change storage path: {ex.Message}", LogLevel.Error, HistoryLogCategory, ex.Message);

                try
                {
                    _database = CreateDatabase(oldDbPath);
                    _collection = _database.GetCollection<ConversationEntry>(CollectionName);
                    EnsureIndexes();
                    _storageFolder = Path.GetDirectoryName(oldDbPath) ?? _storageFolder;
                    _databasePath = oldDbPath;
                }
                catch (Exception restoreEx)
                {
                    _loggingManager.AddMessage($"Failed to restore previous history database: {restoreEx.Message}", LogLevel.Error, HistoryLogCategory, restoreEx.Message);
                }

                return false;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _eventAggregator.Unsubscribe<TranslationCompletedEvent>(OnTranslationCompleted);
            _eventAggregator.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);

            _database?.Dispose();
            _writeSemaphore.Dispose();
        }
    }
}
