using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Events;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace lingualink_client.ViewModels.Components
{
    public partial class ConversationHistoryViewModel : ViewModelBase, IDisposable
    {
        private const string ClipboardCategory = "Clipboard";
        private const string HistoryCategory = "History";
        private readonly IConversationHistoryService _historyService;
        private readonly Dispatcher _dispatcher;
        private readonly ObservableCollection<FilterOption<TranslationSource?>> _sourceFilterOptions = new();
        private readonly ObservableCollection<FilterOption<bool?>> _statusFilterOptions = new();
        private readonly ReadOnlyObservableCollection<FilterOption<TranslationSource?>> _readOnlySourceFilterOptions;
        private readonly ReadOnlyObservableCollection<FilterOption<bool?>> _readOnlyStatusFilterOptions;
        private readonly ILoggingManager _loggingManager;
        private CancellationTokenSource? _entriesRefreshCts;
        private bool _suppressFilterRefresh;
        private bool _isDisposed;

        public ConversationHistoryViewModel()
        {
            _historyService = ServiceContainer.Resolve<IConversationHistoryService>();
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();
            _dispatcher = Application.Current.Dispatcher;

            StoragePath = _historyService.StorageFolder;

            Sessions = new ObservableCollection<ConversationSessionItemViewModel>();
            Entries = new ObservableCollection<ConversationEntryItemViewModel>();

            _readOnlySourceFilterOptions = new ReadOnlyObservableCollection<FilterOption<TranslationSource?>>(_sourceFilterOptions);
            _readOnlyStatusFilterOptions = new ReadOnlyObservableCollection<FilterOption<bool?>>(_statusFilterOptions);

            RefreshFilterOptions();

            _historyService.EntrySaved += OnEntrySaved;
            _historyService.StoragePathChanged += OnStoragePathChanged;
            LanguageManager.LanguageChanged += OnLanguageChanged;

            _ = InitializeAsync();
        }

        public ObservableCollection<ConversationSessionItemViewModel> Sessions { get; }
        public ObservableCollection<ConversationEntryItemViewModel> Entries { get; }
        public ReadOnlyObservableCollection<FilterOption<TranslationSource?>> SourceFilterOptions => _readOnlySourceFilterOptions;
        public ReadOnlyObservableCollection<FilterOption<bool?>> StatusFilterOptions => _readOnlyStatusFilterOptions;

        [ObservableProperty]
        private ConversationSessionItemViewModel? selectedSession;

        [ObservableProperty]
        private ConversationEntryItemViewModel? selectedEntry;

        [ObservableProperty]
        private string storagePath = string.Empty;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool searchInTranslations = true;

        [ObservableProperty]
        private TranslationSource? selectedSourceFilter;

        [ObservableProperty]
        private bool? selectedStatusFilter;

        [ObservableProperty]
        private bool isSessionsLoading;

        [ObservableProperty]
        private bool isEntriesLoading;

        public string PageTitle => LanguageManager.GetString("ConversationHistory");
        public string SessionsHeader => LanguageManager.GetString("HistorySessionsHeader");
        public string EntriesHeader => LanguageManager.GetString("HistoryEntriesHeader");
        public string StoragePathLabel => LanguageManager.GetString("HistoryStoragePath");
        public string ChangePathLabel => LanguageManager.GetString("HistoryChangePath");
        public string OpenFolderLabel => LanguageManager.GetString("HistoryOpenFolder");
        public string RefreshLabel => LanguageManager.GetString("Refresh");
        public string SearchPlaceholder => LanguageManager.GetString("HistorySearchPlaceholder");
        public string SourceFilterLabel => LanguageManager.GetString("HistoryFilterSource");
        public string StatusFilterLabel => LanguageManager.GetString("HistoryFilterStatus");
        public string IncludeTranslationsLabel => LanguageManager.GetString("HistorySearchInTranslations");
        public string CopySelectedLabel => LanguageManager.GetString("HistoryCopySelected");
        public string CopyAllLabel => LanguageManager.GetString("HistoryCopyAll");
        public string ExportMarkdownLabel => LanguageManager.GetString("HistoryExportMarkdown");
        public string ExportJsonLabel => LanguageManager.GetString("HistoryExportJson");
        public string ColumnTimeLabel => LanguageManager.GetString("HistoryColumnTime");
        public string ColumnSourceLabel => LanguageManager.GetString("HistoryColumnSource");
        public string ColumnStatusLabel => LanguageManager.GetString("HistoryColumnStatus");
        public string ColumnTargetsLabel => LanguageManager.GetString("HistoryColumnTargets");
        public string ColumnDurationLabel => LanguageManager.GetString("HistoryColumnDuration");
        public string ColumnSummaryLabel => LanguageManager.GetString("HistoryColumnSummary");
        public string DetailsOriginalLabel => LanguageManager.GetString("HistoryDetailsOriginal");
        public string DetailsProcessedLabel => LanguageManager.GetString("HistoryDetailsProcessed");

        private async Task InitializeAsync()
        {
            await RefreshSessionsInternalAsync(retainSelection: false).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() =>
            {
                if (Sessions.Count > 0)
                {
                    SelectedSession = Sessions.First();
                }
            }, DispatcherPriority.Background);
        }

        [RelayCommand]
        private async Task RefreshSessionsAsync()
        {
            await RefreshSessionsInternalAsync(retainSelection: true).ConfigureAwait(false);
        }

        private async Task RefreshSessionsInternalAsync(bool retainSelection)
        {
            IsSessionsLoading = true;

            var previousSessionId = retainSelection ? SelectedSession?.SessionId : null;

            IReadOnlyList<ConversationSession> sessions;
            try
            {
                sessions = await _historyService.GetSessionsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _loggingManager.AddMessage($"Failed to load sessions: {ex.Message}", LogLevel.Error, HistoryCategory, ex.Message);
                sessions = Array.Empty<ConversationSession>();
            }

            await _dispatcher.InvokeAsync(() =>
            {
                Sessions.Clear();
                foreach (var session in sessions)
                {
                    Sessions.Add(new ConversationSessionItemViewModel(session));
                }

                if (previousSessionId != null)
                {
                    SelectedSession = Sessions.FirstOrDefault(s => s.SessionId.Equals(previousSessionId, StringComparison.OrdinalIgnoreCase));
                }

                if (SelectedSession == null && Sessions.Count > 0)
                {
                    SelectedSession = Sessions.First();
                }
            });

            IsSessionsLoading = false;
        }

        [RelayCommand]
        private async Task RefreshEntriesAsync()
        {
            await RefreshEntriesInternalAsync().ConfigureAwait(false);
        }

        private async Task RefreshEntriesInternalAsync(CancellationToken cancellationToken = default, string? sessionIdOverride = null)
        {
            var sessionId = sessionIdOverride ?? SelectedSession?.SessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    Entries.Clear();
                    SelectedEntry = null;
                });
                return;
            }

            IsEntriesLoading = true;

            try
            {
                var query = BuildQuery(sessionId);
                var entries = await _historyService.QueryEntriesAsync(query, cancellationToken).ConfigureAwait(false);

                await _dispatcher.InvokeAsync(() =>
                {
                    Entries.Clear();
                    foreach (var entry in entries)
                    {
                        Entries.Add(new ConversationEntryItemViewModel(entry));
                    }

                    SelectedEntry = Entries.FirstOrDefault();
                    CopySelectedCommand.NotifyCanExecuteChanged();
                });
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            finally
            {
                IsEntriesLoading = false;
            }
        }

        [RelayCommand]
        private void OpenFolder()
        {
            if (string.IsNullOrWhiteSpace(StoragePath) || !Directory.Exists(StoragePath))
            {
                MessageBox.Show(LanguageManager.GetString("HistoryFolderMissing"), LanguageManager.GetString("History"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = StoragePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.GetString("HistoryOpenFolderFailed"), ex.Message), LanguageManager.GetString("History"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ChangePathAsync()
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                SelectedPath = StoragePath,
                Description = LanguageManager.GetString("HistorySelectFolderDialog")
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
            {
                return;
            }

            var migrateMessage = LanguageManager.GetString("HistoryPromptMigrateData");
            var migrateTitle = LanguageManager.GetString("History");
            var migrate = MessageBox.Show(migrateMessage, migrateTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

            var success = await _historyService.ChangeStoragePathAsync(dialog.SelectedPath, migrate).ConfigureAwait(false);
            if (!success)
            {
                MessageBox.Show(LanguageManager.GetString("HistoryChangePathFailed"), LanguageManager.GetString("History"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StoragePath = _historyService.StorageFolder;
            await RefreshSessionsInternalAsync(retainSelection: true).ConfigureAwait(false);
        }

        [RelayCommand(CanExecute = nameof(CanCopySelected))]
        private async Task CopySelectedAsync()
        {
            if (SelectedEntry == null)
            {
                return;
            }

            var summary = SelectedEntry.Summary?.Trim();
            if (string.IsNullOrWhiteSpace(summary))
            {
                return;
            }

            await CopyTextToClipboardAsync(summary).ConfigureAwait(false);
        }

        private bool CanCopySelected() => SelectedEntry != null;

        [RelayCommand]
        private async Task CopyAllAsync()
        {
            if (!Entries.Any())
            {
                return;
            }

            var text = BuildSummaryExport(Entries);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            await CopyTextToClipboardAsync(text).ConfigureAwait(false);
        }

        private static string BuildSummaryExport(IEnumerable<ConversationEntryItemViewModel> entries)
        {
            var summaries = entries
                .Select(entry => entry.Summary?.Trim())
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .ToList();

            return summaries.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, summaries);
        }

        private Task CopyTextToClipboardAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.CompletedTask;
            }

            _dispatcher.Invoke(() =>
            {
                try
                {
                    if (!ClipboardHelper.TrySetText(text))
                    {
                        _loggingManager.AddMessage("Clipboard copy failed: Win32 clipboard operation returned false.",
                            LogLevel.Warning, ClipboardCategory);
                    }
                }
                catch (ExternalException ex)
                {
                    _loggingManager.AddMessage($"Clipboard copy failed: {ex.Message}", LogLevel.Warning, ClipboardCategory, ex.ToString());
                }
            });

            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task ExportAsync(string format)
        {
            if (!Entries.Any())
            {
                return;
            }

            var exportFormat = format.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? ConversationExportFormat.Json
                : ConversationExportFormat.Markdown;

            var content = await _historyService.ExportAsync(Entries.Select(e => e.Model), exportFormat).ConfigureAwait(false);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = exportFormat == ConversationExportFormat.Json ? "JSON (*.json)|*.json" : "Markdown (*.md)|*.md",
                FileName = exportFormat == ConversationExportFormat.Json ? "conversation_history.json" : "conversation_history.md"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await File.WriteAllTextAsync(dialog.FileName, content, Encoding.UTF8).ConfigureAwait(false);
        }

        private ConversationHistoryQuery BuildQuery(string sessionId)
        {
            return new ConversationHistoryQuery
            {
                SessionId = sessionId,
                Source = SelectedSourceFilter,
                IsSuccess = SelectedStatusFilter,
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                SearchInTranslations = SearchInTranslations,
                SortDescending = true
            };
        }

        private void ScheduleRefreshEntries(TimeSpan? delay = null)
        {
            if (_suppressFilterRefresh)
            {
                return;
            }

            var previousCts = _entriesRefreshCts;
            previousCts?.Cancel();

            var sessionId = SelectedSession?.SessionId;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                _entriesRefreshCts = null;
                return;
            }

            var cts = new CancellationTokenSource();
            _entriesRefreshCts = cts;

            var delayValue = delay ?? TimeSpan.FromMilliseconds(250);

            _ = Task.Run(async () =>
            {
                try
                {
                   await Task.Delay(delayValue, cts.Token).ConfigureAwait(false);
                    await RefreshEntriesInternalAsync(cts.Token, sessionId).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
                finally
                {
                    if (ReferenceEquals(_entriesRefreshCts, cts))
                    {
                        _entriesRefreshCts = null;
                    }

                    cts.Dispose();
                }
            });
        }

        partial void OnSelectedSessionChanged(ConversationSessionItemViewModel? value)
        {
            ScheduleRefreshEntries(TimeSpan.Zero);
            CopySelectedCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedEntryChanged(ConversationEntryItemViewModel? value)
        {
            CopySelectedCommand.NotifyCanExecuteChanged();
        }

        partial void OnSearchTextChanged(string value)
        {
            ScheduleRefreshEntries(TimeSpan.FromMilliseconds(350));
        }

        partial void OnSearchInTranslationsChanged(bool value)
        {
            ScheduleRefreshEntries();
        }

        partial void OnSelectedSourceFilterChanged(TranslationSource? value)
        {
            ScheduleRefreshEntries();
        }

        partial void OnSelectedStatusFilterChanged(bool? value)
        {
            ScheduleRefreshEntries();
        }

        private void OnEntrySaved(object? sender, ConversationEntrySavedEventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }

            _ = _dispatcher.InvokeAsync(async () =>
            {
                var targetSessionId = e.Entry.SessionId;
                var sessionVm = Sessions.FirstOrDefault(s => s.SessionId.Equals(targetSessionId, StringComparison.OrdinalIgnoreCase));

                if (sessionVm == null)
                {
                    await RefreshSessionsInternalAsync(retainSelection: true);
                    sessionVm = Sessions.FirstOrDefault(s => s.SessionId.Equals(targetSessionId, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    sessionVm.ApplyEntry(e.Entry);
                    ReorderSessions();
                }

                if (SelectedSession?.SessionId.Equals(targetSessionId, StringComparison.OrdinalIgnoreCase) == true && EntryMatchesCurrentFilters(e.Entry))
                {
                    Entries.Insert(0, new ConversationEntryItemViewModel(e.Entry));
                    SelectedEntry ??= Entries.FirstOrDefault();
                    CopySelectedCommand.NotifyCanExecuteChanged();
                }
            });
        }

        private bool EntryMatchesCurrentFilters(ConversationEntry entry)
        {
            if (SelectedSourceFilter.HasValue && !entry.Source.Equals(SelectedSourceFilter.Value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (SelectedStatusFilter.HasValue && entry.IsSuccess != SelectedStatusFilter.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var text = SearchText.Trim();
                if (!entry.OriginalText.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                    !entry.ProcessedText.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                    (!SearchInTranslations || !entry.Translations.Values.Any(v => v.Contains(text, StringComparison.OrdinalIgnoreCase))))
                {
                    return false;
                }
            }

            return true;
        }

        private void ReorderSessions()
        {
            var ordered = Sessions.OrderByDescending(s => s.LastActivityUtc).ToList();
            if (!ordered.SequenceEqual(Sessions))
            {
                Sessions.Clear();
                foreach (var session in ordered)
                {
                    Sessions.Add(session);
                }
            }
        }

        private void OnStoragePathChanged(object? sender, EventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }

            _dispatcher.Invoke(() => StoragePath = _historyService.StorageFolder);
            _ = RefreshSessionsInternalAsync(retainSelection: false);
        }

        private void RefreshFilterOptions()
        {
            _suppressFilterRefresh = true;

            var currentSource = SelectedSourceFilter;
            var currentStatus = SelectedStatusFilter;

            _sourceFilterOptions.Clear();
            _sourceFilterOptions.Add(new FilterOption<TranslationSource?>(LanguageManager.GetString("HistoryFilterAll"), null));
            _sourceFilterOptions.Add(new FilterOption<TranslationSource?>(LanguageManager.GetString("HistoryFilterAudio"), TranslationSource.Audio));
            _sourceFilterOptions.Add(new FilterOption<TranslationSource?>(LanguageManager.GetString("HistoryFilterText"), TranslationSource.Text));

            _statusFilterOptions.Clear();
            _statusFilterOptions.Add(new FilterOption<bool?>(LanguageManager.GetString("HistoryFilterAll"), null));
            _statusFilterOptions.Add(new FilterOption<bool?>(LanguageManager.GetString("HistoryFilterSuccess"), true));
            _statusFilterOptions.Add(new FilterOption<bool?>(LanguageManager.GetString("HistoryFilterFailure"), false));

            SelectedSourceFilter = _sourceFilterOptions.First().Value;
            SelectedStatusFilter = _statusFilterOptions.First().Value;

            if (currentSource.HasValue)
            {
                SelectedSourceFilter = currentSource;
            }

            if (currentStatus.HasValue)
            {
                SelectedStatusFilter = currentStatus;
            }

            _suppressFilterRefresh = false;
        }

        private void OnLanguageChanged()
        {
            RefreshFilterOptions();

            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(SessionsHeader));
            OnPropertyChanged(nameof(EntriesHeader));
            OnPropertyChanged(nameof(StoragePathLabel));
            OnPropertyChanged(nameof(ChangePathLabel));
            OnPropertyChanged(nameof(OpenFolderLabel));
            OnPropertyChanged(nameof(RefreshLabel));
            OnPropertyChanged(nameof(SearchPlaceholder));
            OnPropertyChanged(nameof(SourceFilterLabel));
            OnPropertyChanged(nameof(StatusFilterLabel));
            OnPropertyChanged(nameof(IncludeTranslationsLabel));
            OnPropertyChanged(nameof(CopySelectedLabel));
            OnPropertyChanged(nameof(CopyAllLabel));
            OnPropertyChanged(nameof(ExportMarkdownLabel));
            OnPropertyChanged(nameof(ExportJsonLabel));
            OnPropertyChanged(nameof(ColumnTimeLabel));
            OnPropertyChanged(nameof(ColumnSourceLabel));
            OnPropertyChanged(nameof(ColumnStatusLabel));
            OnPropertyChanged(nameof(ColumnTargetsLabel));
            OnPropertyChanged(nameof(ColumnDurationLabel));
            OnPropertyChanged(nameof(ColumnSummaryLabel));
            OnPropertyChanged(nameof(DetailsOriginalLabel));
            OnPropertyChanged(nameof(DetailsProcessedLabel));

            foreach (var session in Sessions)
            {
                session.UpdateLocalization();
            }

            foreach (var entry in Entries)
            {
                entry.UpdateLocalization();
            }

            CollectionViewSource.GetDefaultView(Sessions)?.Refresh();
            CollectionViewSource.GetDefaultView(Entries)?.Refresh();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            var cts = _entriesRefreshCts;
            _entriesRefreshCts = null;
            cts?.Cancel();

            _historyService.EntrySaved -= OnEntrySaved;
            _historyService.StoragePathChanged -= OnStoragePathChanged;
            LanguageManager.LanguageChanged -= OnLanguageChanged;
        }
    }

    public partial class ConversationSessionItemViewModel : ObservableObject
    {
        public ConversationSessionItemViewModel(ConversationSession model)
        {
            SessionId = model.SessionId;
            StartedUtc = model.StartedUtc;
            LastActivityUtc = model.LastActivityUtc;
            EntryCount = model.EntryCount;
            SuccessCount = model.SuccessCount;
            FailureCount = model.FailureCount;
        }

        public string SessionId { get; }
        public DateTime StartedUtc { get; }

        [ObservableProperty]
        private DateTime lastActivityUtc;

        [ObservableProperty]
        private int entryCount;

        [ObservableProperty]
        private int successCount;

        [ObservableProperty]
        private int failureCount;

        public DateTime StartedLocal => StartedUtc.ToLocalTime();
        public DateTime LastActivityLocal => LastActivityUtc.ToLocalTime();
        public string DisplayName => $"{StartedLocal.ToString("d", CultureInfo.CurrentCulture)} ({EntryCount})";
        public string Tooltip => string.Format(
            LanguageManager.GetString("HistorySessionTooltip"),
            EntryCount,
            SuccessCount,
            FailureCount,
            LastActivityLocal.ToString("f", CultureInfo.CurrentCulture));

        public void ApplyEntry(ConversationEntry entry)
        {
            LastActivityUtc = entry.TimestampUtc;
            EntryCount++;
            if (entry.IsSuccess)
            {
                SuccessCount++;
            }
            else
            {
                FailureCount++;
            }

            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Tooltip));
        }

        public void UpdateLocalization()
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Tooltip));
        }
    }

    public partial class ConversationEntryItemViewModel : ObservableObject
    {
        public ConversationEntryItemViewModel(ConversationEntry model)
        {
            Model = model;
            UpdateLocalization();
        }

        public ConversationEntry Model { get; }

        public DateTime TimestampLocal => Model.TimestampUtc.ToLocalTime();
        public string TimeDisplay => TimestampLocal.ToString("t", CultureInfo.CurrentCulture);
        public string StatusDisplay => Model.IsSuccess ? LanguageManager.GetString("HistoryStatusSuccess") : LanguageManager.GetString("HistoryStatusFailure");
        public string SourceDisplay => Model.Source switch
        {
            nameof(TranslationSource.Audio) => LanguageManager.GetString("HistorySourceAudio"),
            nameof(TranslationSource.Text) => LanguageManager.GetString("HistorySourceText"),
            _ => LanguageManager.GetString("HistorySourceUnknown")
        };
        public string TargetLanguagesDisplay => Model.TargetLanguages.Any() ? string.Join(", ", Model.TargetLanguages) : LanguageManager.GetString("HistoryNoTargets");
        public string DurationDisplay => Model.DurationSeconds > 0 ? $"{Model.DurationSeconds:F2}s" : string.Empty;
        public string Summary => BuildSummary();

        public string OriginalText => Model.OriginalText;
        public string ProcessedText => Model.ProcessedText;

        public IEnumerable<KeyValuePair<string, string>> TranslationItems => Model.Translations;

        public void UpdateLocalization()
        {
            OnPropertyChanged(nameof(TimeDisplay));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(SourceDisplay));
            OnPropertyChanged(nameof(TargetLanguagesDisplay));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(DurationDisplay));
        }

        private string BuildSummary()
        {
            var source = !string.IsNullOrWhiteSpace(Model.ProcessedText) ? Model.ProcessedText : Model.OriginalText;
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            var normalized = source.Replace("\r", string.Empty).Replace('\n', ' ');
            return normalized.Length > 100 ? normalized[..100] + "…" : normalized;
        }
    }

    public class FilterOption<T>
    {
        public FilterOption(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; set; }
        public T Value { get; }
    }
}


