using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.ViewModels.Components
{
    public partial class LogViewModel : ViewModelBase, IDisposable
    {
        private const string ClipboardCategory = "Clipboard";
        private readonly ILoggingManager _loggingManager;
        private readonly ObservableCollection<FilterOption<string?>> _categoryOptions = new();
        private readonly ReadOnlyObservableCollection<FilterOption<string?>> _readOnlyCategoryOptions;
        private bool _suppressRefresh;

        public LogViewModel()
        {
            _loggingManager = ServiceContainer.Resolve<ILoggingManager>();

            EntriesView = CollectionViewSource.GetDefaultView(_loggingManager.LogEntries);
            EntriesView.SortDescriptions.Add(new SortDescription(nameof(LogEntry.Timestamp), ListSortDirection.Ascending));
            EntriesView.Filter = FilterEntry;

            _readOnlyCategoryOptions = new ReadOnlyObservableCollection<FilterOption<string?>>(_categoryOptions);

            _loggingManager.EntryAdded += OnEntryAdded;
            _loggingManager.MessagesCleared += OnMessagesCleared;
            LanguageManager.LanguageChanged += OnLanguageChanged;

            UpdateCategories();
        }

        public ICollectionView EntriesView { get; }
        public ReadOnlyObservableCollection<FilterOption<string?>> CategoryOptions => _readOnlyCategoryOptions;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string? selectedCategory;

        [ObservableProperty]
        private bool showTrace;

        [ObservableProperty]
        private bool showDebug;

        [ObservableProperty]
        private bool showInfo = true;

        [ObservableProperty]
        private bool showWarning = true;

        [ObservableProperty]
        private bool showError = true;

        [ObservableProperty]
        private bool showCritical = true;

        [ObservableProperty]
        private bool autoScroll = true;

        public string RunningLogLabel => LanguageManager.GetString("RunningLog");
        public string ClearLogLabel => LanguageManager.GetString("ClearLog");
        public string SearchPlaceholder => LanguageManager.GetString("LogSearchPlaceholder");
        public string CategoryLabel => LanguageManager.GetString("LogCategoryLabel");
        public string LevelLabel => LanguageManager.GetString("LogLevelLabel");
        public string CopySelectedLabel => LanguageManager.GetString("HistoryCopySelected");
        public string CopyAllLabel => LanguageManager.GetString("HistoryCopyAll");
        public string ExportJsonLabel => LanguageManager.GetString("HistoryExportJson");
        public string ExportMarkdownLabel => LanguageManager.GetString("HistoryExportMarkdown");
        public string AutoScrollLabel => LanguageManager.GetString("LogAutoScroll");
        public string TraceLabel => LanguageManager.GetString("LogLevelTrace");
        public string DebugLabel => LanguageManager.GetString("LogLevelDebug");
        public string InfoLabel => LanguageManager.GetString("LogLevelInfo");
        public string WarningLabel => LanguageManager.GetString("LogLevelWarning");
        public string ErrorLabel => LanguageManager.GetString("LogLevelError");
        public string CriticalLabel => LanguageManager.GetString("LogLevelCritical");
        public string ColumnTimeLabel => LanguageManager.GetString("LogColumnTime");
        public string ColumnLevelLabel => LanguageManager.GetString("LogColumnLevel");
        public string ColumnCategoryLabel => LanguageManager.GetString("LogColumnCategory");
        public string ColumnMessageLabel => LanguageManager.GetString("LogColumnMessage");
        public string ColumnDetailsLabel => LanguageManager.GetString("LogColumnDetails");

        public event EventHandler<LogEntry>? EntryAppended;

        [RelayCommand]
        private void ClearLog()
        {
            _loggingManager.ClearMessages();
        }

        [RelayCommand]
        private async Task CopySelectedAsync(IList? items)
        {
            var entries = (items?.OfType<LogEntry>() ?? Enumerable.Empty<LogEntry>()).ToList();
            if (!entries.Any())
            {
                entries = EntriesView.Cast<LogEntry>().ToList();
            }

            if (!entries.Any())
            {
                return;
            }

            var text = string.Join(Environment.NewLine, entries.Select(entry => entry.ToDisplayString()));
            await CopyTextToClipboardAsync(text).ConfigureAwait(false);
        }

        [RelayCommand]
        private async Task CopyAllAsync()
        {
            var entries = EntriesView.Cast<LogEntry>().ToList();
            if (!entries.Any())
            {
                return;
            }

            var text = string.Join(Environment.NewLine, entries.Select(entry => entry.ToDisplayString()));
            await CopyTextToClipboardAsync(text).ConfigureAwait(false);
        }

        private Task CopyTextToClipboardAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Task.CompletedTask;
            }

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            try
            {
                dispatcher.Invoke(() => Forms.Clipboard.SetText(text, Forms.TextDataFormat.Text));
            }
            catch (ExternalException ex)
            {
                _loggingManager.AddMessage($"Clipboard copy failed: {ex.Message}", LogLevel.Warning, ClipboardCategory, ex.ToString());
            }

            return Task.CompletedTask;
        }

        [RelayCommand]
        private void Export(string format)
        {
            var entries = EntriesView.Cast<LogEntry>().ToList();
            if (!entries.Any())
            {
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = format.Equals("json", StringComparison.OrdinalIgnoreCase)
                    ? "JSON (*.json)|*.json"
                    : "Markdown (*.md)|*.md",
                FileName = format.Equals("json", StringComparison.OrdinalIgnoreCase)
                    ? "app_logs.json"
                    : "app_logs.md"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var content = format.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true })
                : string.Join(Environment.NewLine + Environment.NewLine, entries.Select(entry => entry.ToDisplayString()));

            System.IO.File.WriteAllText(dialog.FileName, content);
        }

        private bool FilterEntry(object obj)
        {
            if (obj is not LogEntry entry)
            {
                return false;
            }

            if (!IsLevelVisible(entry.Level))
            {
                return false;
            }

            if (SelectedCategory != null && !entry.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var text = SearchText.Trim();
                if (!(entry.Message.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                      (!string.IsNullOrWhiteSpace(entry.Details) && entry.Details!.Contains(text, StringComparison.OrdinalIgnoreCase))))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLevelVisible(LogLevel level) => level switch
        {
            LogLevel.Trace => ShowTrace,
            LogLevel.Debug => ShowDebug,
            LogLevel.Info => ShowInfo,
            LogLevel.Warning => ShowWarning,
            LogLevel.Error => ShowError,
            LogLevel.Critical => ShowCritical,
            _ => true
        };

        private void RefreshView()
        {
            if (_suppressRefresh)
            {
                return;
            }

            Application.Current.Dispatcher.Invoke(() => EntriesView.Refresh());
        }

        private void UpdateCategories()
        {
            var previous = SelectedCategory;

            _suppressRefresh = true;

            _categoryOptions.Clear();
            _categoryOptions.Add(new FilterOption<string?>(LanguageManager.GetString("LogCategoryAll"), null));

            var categories = _loggingManager.LogEntries
                .Select(entry => entry.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.OrdinalIgnoreCase);

            foreach (var category in categories)
            {
                _categoryOptions.Add(new FilterOption<string?>(category, category));
            }

            if (previous != null && _categoryOptions.Any(option => option.Value != null && option.Value.Equals(previous, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedCategory = previous;
            }
            else
            {
                SelectedCategory = null;
            }

            _suppressRefresh = false;
            RefreshView();
        }

        private void OnEntryAdded(object? sender, LogEntry entry)
        {
            UpdateCategories();

            if (AutoScroll)
            {
                EntryAppended?.Invoke(this, entry);
            }
        }

        private void OnMessagesCleared(object? sender, EventArgs e)
        {
            UpdateCategories();
        }

        private void OnLanguageChanged()
        {
            OnPropertyChanged(nameof(RunningLogLabel));
            OnPropertyChanged(nameof(ClearLogLabel));
            OnPropertyChanged(nameof(SearchPlaceholder));
            OnPropertyChanged(nameof(CategoryLabel));
            OnPropertyChanged(nameof(LevelLabel));
            OnPropertyChanged(nameof(CopySelectedLabel));
            OnPropertyChanged(nameof(CopyAllLabel));
            OnPropertyChanged(nameof(ExportJsonLabel));
            OnPropertyChanged(nameof(ExportMarkdownLabel));
            OnPropertyChanged(nameof(AutoScrollLabel));
            OnPropertyChanged(nameof(TraceLabel));
            OnPropertyChanged(nameof(DebugLabel));
            OnPropertyChanged(nameof(InfoLabel));
            OnPropertyChanged(nameof(WarningLabel));
            OnPropertyChanged(nameof(ErrorLabel));
            OnPropertyChanged(nameof(CriticalLabel));
            OnPropertyChanged(nameof(ColumnTimeLabel));
            OnPropertyChanged(nameof(ColumnLevelLabel));
            OnPropertyChanged(nameof(ColumnCategoryLabel));
            OnPropertyChanged(nameof(ColumnMessageLabel));
            OnPropertyChanged(nameof(ColumnDetailsLabel));

            UpdateCategories();

            RefreshView();
        }

        partial void OnSearchTextChanged(string value) => RefreshView();
        partial void OnSelectedCategoryChanged(string? value) => RefreshView();
        partial void OnShowTraceChanged(bool value) => RefreshView();
        partial void OnShowDebugChanged(bool value) => RefreshView();
        partial void OnShowInfoChanged(bool value) => RefreshView();
        partial void OnShowWarningChanged(bool value) => RefreshView();
        partial void OnShowErrorChanged(bool value) => RefreshView();
        partial void OnShowCriticalChanged(bool value) => RefreshView();

        public void Dispose()
        {
            _loggingManager.EntryAdded -= OnEntryAdded;
            _loggingManager.MessagesCleared -= OnMessagesCleared;
            LanguageManager.LanguageChanged -= OnLanguageChanged;
        }
    }
}


