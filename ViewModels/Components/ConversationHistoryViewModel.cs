using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Events;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.ViewModels.Components
{
    public partial class ConversationHistoryViewModel : ViewModelBase, IDisposable
    {
        private const string ClipboardCategory = "Clipboard";
        private const string HistoryCategory = "History";

        private static readonly string[] LocalizedPropertyNames =
        {
            nameof(PageTitle),
            nameof(SessionsHeader),
            nameof(EntriesHeader),
            nameof(StoragePathLabel),
            nameof(ChangePathLabel),
            nameof(OpenFolderLabel),
            nameof(RefreshLabel),
            nameof(SearchPlaceholder),
            nameof(SourceFilterLabel),
            nameof(StatusFilterLabel),
            nameof(IncludeTranslationsLabel),
            nameof(CopySelectedLabel),
            nameof(CopyAllLabel),
            nameof(ExportMarkdownLabel),
            nameof(ExportJsonLabel),
            nameof(ColumnTimeLabel),
            nameof(ColumnSourceLabel),
            nameof(ColumnStatusLabel),
            nameof(ColumnTargetsLabel),
            nameof(ColumnDurationLabel),
            nameof(ColumnSummaryLabel),
            nameof(DetailsOriginalLabel),
            nameof(DetailsProcessedLabel)
        };

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
            : this(
                ServiceContainer.Resolve<IConversationHistoryService>(),
                ServiceContainer.Resolve<ILoggingManager>())
        {
        }

        public ConversationHistoryViewModel(
            IConversationHistoryService historyService,
            ILoggingManager loggingManager,
            Dispatcher? dispatcher = null)
        {
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _loggingManager = loggingManager ?? throw new ArgumentNullException(nameof(loggingManager));
            _dispatcher = dispatcher ?? Application.Current.Dispatcher;

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
        public string Summary => ConversationHistoryTextLogic.BuildEntrySummary(Model.OriginalText, Model.ProcessedText);

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
    }

    public class FilterOption<T>
    {
        public FilterOption(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public T Value { get; }
    }
}
