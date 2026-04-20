using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Models.Updates;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using Velopack;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UiMessageBox = lingualink_client.Services.MessageBox;
using WpfMessageBox = System.Windows.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class SettingPageViewModel : ViewModelBase, IDisposable
    {
        private readonly IUpdateService _updateService;
        private readonly ISettingsManager _settingsManager;
        private readonly DispatcherTimer _vocabularySaveTimer;
        private string _pendingVocabularyChangeSource = "SettingPageVocabulary";

        private UpdateSession? _activeSession;
        private UpdateStatus _latestStatus = UpdateStatus.NotChecked;

        public string PageTitle => LanguageManager.GetString("GeneralSettings");
        public string InterfaceLanguage => LanguageManager.GetString("InterfaceLanguage");
        public string LanguageHint => LanguageManager.GetString("LanguageHint");
        public string UpdateSectionTitle => LanguageManager.GetString("UpdateSettings");
        public string CurrentVersionLabel => LanguageManager.GetString("UpdateCurrentVersion");
        public string LatestVersionLabel => LanguageManager.GetString("UpdateLatestVersion");
        public string CheckForUpdatesLabel => LanguageManager.GetString("UpdateCheckButton");
        public string DownloadAndUpdateLabel => LanguageManager.GetString("UpdateDownloadButton");
        public string DownloadProgressLabel => LanguageManager.GetString("UpdateDownloadProgress");
        public string CustomVocabularySectionTitle => LanguageManager.GetString("CustomVocabularySectionTitle");
        public string CustomVocabularySectionHint => string.Format(
            LanguageManager.GetString("CustomVocabularySectionHint"),
            AppSettings.MaxCustomVocabularyTables,
            AppSettings.MaxEntriesPerVocabularyTable,
            AppSettings.MaxCustomVocabularyPayloadEntries);
        public string CustomVocabularyAddTableLabel => LanguageManager.GetString("CustomVocabularyAddTable");
        public string CustomVocabularyImportLabel => LanguageManager.GetString("CustomVocabularyImport");
        public string CustomVocabularyExportLabel => LanguageManager.GetString("CustomVocabularyExport");
        public string CustomVocabularyDeleteTableLabel => LanguageManager.GetString("CustomVocabularyDeleteTable");
        public string CustomVocabularyTableNameLabel => LanguageManager.GetString("CustomVocabularyTableName");
        public string CustomVocabularyTableEnabledLabel => LanguageManager.GetString("CustomVocabularyTableEnabled");
        public string CustomVocabularyEntriesLabel => LanguageManager.GetString("CustomVocabularyEntries");
        public string CustomVocabularyAddEntryLabel => LanguageManager.GetString("CustomVocabularyAddEntry");
        public string CustomVocabularyDeleteEntryLabel => LanguageManager.GetString("CustomVocabularyDeleteEntry");
        public string CustomVocabularyNoTableSelected => LanguageManager.GetString("CustomVocabularyNoTableSelected");
        public string CustomVocabularyAliasesHint => string.Format(
            LanguageManager.GetString("CustomVocabularyAliasesHint"),
            AppSettings.MaxAliasesPerVocabularyEntry,
            AppSettings.MaxPronunciationsPerVocabularyEntry);
        public string CustomVocabularyTableCountSummary => string.Format(
            LanguageManager.GetString("CustomVocabularyTableCountSummary"),
            VocabularyTables.Count,
            AppSettings.MaxCustomVocabularyTables,
            VocabularyTables.Sum(table => table.TotalEntries));
        public string CustomVocabularySelectedTableSummary => SelectedVocabularyTable == null
            ? CustomVocabularyNoTableSelected
            : string.Format(
                LanguageManager.GetString("CustomVocabularySelectedTableSummary"),
                SelectedVocabularyTable.TotalEntries,
                AppSettings.MaxEntriesPerVocabularyTable,
                SelectedVocabularyTable.EnabledEntries);
        public bool HasSelectedVocabularyTable => SelectedVocabularyTable != null;

        public ObservableCollection<VocabularyTableEditor> VocabularyTables { get; } = new();

        public ObservableCollection<VocabularyEntryEditor>? SelectedVocabularyEntries => SelectedVocabularyTable?.Entries;

        [ObservableProperty]
        private string currentVersion = string.Empty;

        [ObservableProperty]
        private string latestVersion = string.Empty;

        [ObservableProperty]
        private bool isCheckingUpdate;

        [ObservableProperty]
        private bool hasUpdate;

        [ObservableProperty]
        private double downloadProgress;

        [ObservableProperty]
        private bool isDownloading;

        [ObservableProperty]
        private string updateNotes = string.Empty;

        [ObservableProperty]
        private VocabularyTableEditor? selectedVocabularyTable;

        [ObservableProperty]
        private VocabularyEntryEditor? selectedVocabularyEntry;

        public SettingPageViewModel(
            ISettingsManager? settingsManager = null,
            IUpdateService? updateService = null)
        {
            _updateService = updateService ?? ServiceContainer.Resolve<IUpdateService>();
            _settingsManager = settingsManager
                               ?? (ServiceContainer.TryResolve<ISettingsManager>(out var resolved) && resolved != null
                                   ? resolved
                                   : new SettingsManager());

            _vocabularySaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _vocabularySaveTimer.Tick += (_, _) =>
            {
                _vocabularySaveTimer.Stop();
                PersistVocabularyTables(_pendingVocabularyChangeSource);
            };

            CurrentVersion = ResolveCurrentVersion();
            LoadVocabularyTables(_settingsManager.LoadSettings());

            if (_updateService.ActiveSession is { HasUpdate: true } existingSession)
            {
                _activeSession = existingSession;
                HasUpdate = true;
                _latestStatus = UpdateStatus.UpdateAvailable;
                UpdateNotes = existingSession.ReleaseNotesMarkdown ?? LanguageManager.GetString("UpdateNotesUnavailable");
            }

            RefreshLatestVersionText();
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            OnPropertyChanged(string.Empty);
            RefreshLatestVersionText();
        }

        [RelayCommand(CanExecute = nameof(CanCheckForUpdate))]
        private async Task CheckForUpdateAsync()
        {
            if (IsCheckingUpdate || IsDownloading)
            {
                return;
            }

            if (!_updateService.IsSupported || string.IsNullOrWhiteSpace(_updateService.FeedUrl))
            {
                _latestStatus = UpdateStatus.Disabled;
                HasUpdate = false;
                UpdateNotes = string.Empty;
                RefreshLatestVersionText();
                return;
            }

            try
            {
                IsCheckingUpdate = true;
                _latestStatus = UpdateStatus.Checking;
                RefreshLatestVersionText();

                var result = await _updateService.CheckForUpdatesAsync();

                if (result.Error is not null)
                {
                    _latestStatus = UpdateStatus.Failed;
                    HasUpdate = false;
                    UpdateNotes = string.Empty;
                    RefreshLatestVersionText();
                    ShowError(LanguageManager.GetString("UpdateErrorCheck"), result.Error);
                    return;
                }

                if (!result.IsSupported)
                {
                    _latestStatus = UpdateStatus.Disabled;
                    HasUpdate = false;
                    UpdateNotes = string.Empty;
                    RefreshLatestVersionText();
                    return;
                }

                CurrentVersion = result.InstalledVersion?.ToString() ?? ResolveCurrentVersion();

                if (result.HasUpdate && result.Session is not null)
                {
                    _activeSession = result.Session;
                    HasUpdate = true;
                    _latestStatus = UpdateStatus.UpdateAvailable;
                    UpdateNotes = string.IsNullOrWhiteSpace(result.ReleaseNotesMarkdown)
                        ? LanguageManager.GetString("UpdateNotesUnavailable")
                        : result.ReleaseNotesMarkdown!;
                }
                else
                {
                    if (_activeSession is not null)
                    {
                        _updateService.ReleaseSession(_activeSession);
                        _activeSession = null;
                    }

                    HasUpdate = false;
                    _latestStatus = UpdateStatus.UpToDate;
                    UpdateNotes = string.Empty;
                }

                RefreshLatestVersionText();
            }
            catch (Exception ex)
            {
                _latestStatus = UpdateStatus.Failed;
                HasUpdate = false;
                UpdateNotes = string.Empty;
                RefreshLatestVersionText();
                ShowError(LanguageManager.GetString("UpdateErrorCheck"), ex);
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
        private async Task DownloadAndUpdateAsync()
        {
            if (_activeSession is null || !_activeSession.HasUpdate)
            {
                return;
            }

            try
            {
                IsDownloading = true;
                DownloadProgress = 0;

                var progress = new Progress<int>(value => { DownloadProgress = value; });
                await _updateService.DownloadAsync(_activeSession, progress, CancellationToken.None);

                var prompt = LanguageManager.GetString("UpdateDialogDownloadPrompt");
                var title = LanguageManager.GetString("UpdateReadyTitle");
                var result = WpfMessageBox.Show(prompt, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _updateService.ApplyAsync(_activeSession, restart: true, silent: false, CancellationToken.None);
                    _activeSession = null;
                    HasUpdate = false;
                    _latestStatus = UpdateStatus.UpToDate;
                    RefreshLatestVersionText();
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                ShowError(LanguageManager.GetString("UpdateErrorDownload"), ex);
            }
            finally
            {
                IsDownloading = false;
                DownloadProgress = 0;
            }
        }

        [RelayCommand]
        private void AddVocabularyTable()
        {
            if (VocabularyTables.Count >= AppSettings.MaxCustomVocabularyTables)
            {
                UiMessageBox.ShowWarning(
                    string.Format(LanguageManager.GetString("CustomVocabularyMaxTablesReached"), AppSettings.MaxCustomVocabularyTables));
                return;
            }

            var table = new VocabularyTableEditor
            {
                Name = BuildUniqueTableName(LanguageManager.GetString("CustomVocabularyDefaultTableName"))
            };

            VocabularyTables.Add(table);
            SelectedVocabularyTable = table;
            QueueVocabularySave("SettingPageVocabularyAddTable");
        }

        [RelayCommand(CanExecute = nameof(CanRemoveVocabularyTable))]
        private void RemoveVocabularyTable()
        {
            if (SelectedVocabularyTable == null)
            {
                return;
            }

            var confirmMessage = string.Format(
                LanguageManager.GetString("CustomVocabularyDeleteTableConfirm"),
                SelectedVocabularyTable.Name);
            if (UiMessageBox.ShowConfirm(confirmMessage) != MessageBoxResult.Yes)
            {
                return;
            }

            var removed = SelectedVocabularyTable;
            var removedIndex = VocabularyTables.IndexOf(removed);
            VocabularyTables.Remove(removed);
            SelectedVocabularyTable = VocabularyTables.Count == 0
                ? null
                : VocabularyTables[Math.Clamp(removedIndex, 0, VocabularyTables.Count - 1)];
            SelectedVocabularyEntry = SelectedVocabularyTable?.Entries.FirstOrDefault();
            QueueVocabularySave("SettingPageVocabularyRemoveTable");
        }

        [RelayCommand]
        private async Task ImportVocabularyTableAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON (*.json)|*.json",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(dialog.FileName).ConfigureAwait(true);
                var envelope = JsonSerializer.Deserialize<VocabularyTableImportEnvelope>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (envelope?.Table == null)
                {
                    UiMessageBox.ShowError(LanguageManager.GetString("CustomVocabularyImportInvalid"));
                    return;
                }

                var importedTable = SanitizeImportedTable(envelope.Table, out var truncated);
                if (importedTable == null)
                {
                    UiMessageBox.ShowError(LanguageManager.GetString("CustomVocabularyImportInvalid"));
                    return;
                }

                var existing = VocabularyTables.FirstOrDefault(
                    table => string.Equals(table.Name, importedTable.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    var conflictResult = UiMessageBox.ShowYesNoCancel(
                        string.Format(LanguageManager.GetString("CustomVocabularyImportConflict"), importedTable.Name));

                    if (conflictResult == MessageBoxResult.Cancel)
                    {
                        return;
                    }

                    if (conflictResult == MessageBoxResult.Yes)
                    {
                        ReplaceTable(existing, importedTable);
                    }
                    else
                    {
                        if (VocabularyTables.Count >= AppSettings.MaxCustomVocabularyTables)
                        {
                            UiMessageBox.ShowWarning(
                                string.Format(LanguageManager.GetString("CustomVocabularyMaxTablesReached"), AppSettings.MaxCustomVocabularyTables));
                            return;
                        }

                        importedTable.Name = BuildUniqueTableName(importedTable.Name);
                        VocabularyTables.Add(importedTable);
                    }
                }
                else
                {
                    if (VocabularyTables.Count >= AppSettings.MaxCustomVocabularyTables)
                    {
                        UiMessageBox.ShowWarning(
                            string.Format(LanguageManager.GetString("CustomVocabularyMaxTablesReached"), AppSettings.MaxCustomVocabularyTables));
                        return;
                    }

                    VocabularyTables.Add(importedTable);
                }

                SelectedVocabularyTable = VocabularyTables.FirstOrDefault(
                    table => string.Equals(table.Id, importedTable.Id, StringComparison.OrdinalIgnoreCase))
                    ?? VocabularyTables.FirstOrDefault(table => string.Equals(table.Name, importedTable.Name, StringComparison.OrdinalIgnoreCase));
                SelectedVocabularyEntry = SelectedVocabularyTable?.Entries.FirstOrDefault();
                PersistVocabularyTables("SettingPageVocabularyImport");

                if (truncated)
                {
                    UiMessageBox.ShowWarning(LanguageManager.GetString("CustomVocabularyImportTruncated"));
                }
            }
            catch (Exception ex)
            {
                UiMessageBox.ShowError(string.Format(LanguageManager.GetString("CustomVocabularyImportFailed"), ex.Message));
            }
        }

        [RelayCommand(CanExecute = nameof(CanExportVocabularyTable))]
        private async Task ExportVocabularyTableAsync()
        {
            if (SelectedVocabularyTable == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON (*.json)|*.json",
                FileName = $"{SanitizeFileName(SelectedVocabularyTable.Name)}.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var payload = new VocabularyTableImportEnvelope
                {
                    Version = 1,
                    Table = SelectedVocabularyTable.ToModel()
                };

                var json = JsonSerializer.Serialize(
                    payload,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                await File.WriteAllTextAsync(dialog.FileName, json).ConfigureAwait(true);
                UiMessageBox.ShowSuccess(LanguageManager.GetString("CustomVocabularyExportSuccess"));
            }
            catch (Exception ex)
            {
                UiMessageBox.ShowError(string.Format(LanguageManager.GetString("CustomVocabularyExportFailed"), ex.Message));
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddVocabularyEntry))]
        private void AddVocabularyEntry()
        {
            if (SelectedVocabularyTable == null)
            {
                return;
            }

            if (SelectedVocabularyTable.Entries.Count >= AppSettings.MaxEntriesPerVocabularyTable)
            {
                UiMessageBox.ShowWarning(
                    string.Format(
                        LanguageManager.GetString("CustomVocabularyMaxEntriesReached"),
                        AppSettings.MaxEntriesPerVocabularyTable));
                return;
            }

            var entry = new VocabularyEntryEditor
            {
                Priority = SelectedVocabularyTable.Entries.Count + 1
            };

            SelectedVocabularyTable.Entries.Add(entry);
            SelectedVocabularyEntry = entry;
            QueueVocabularySave("SettingPageVocabularyAddEntry");
        }

        [RelayCommand(CanExecute = nameof(CanRemoveVocabularyEntry))]
        private void RemoveVocabularyEntry()
        {
            if (SelectedVocabularyTable == null || SelectedVocabularyEntry == null)
            {
                return;
            }

            SelectedVocabularyTable.Entries.Remove(SelectedVocabularyEntry);
            SelectedVocabularyEntry = SelectedVocabularyTable.Entries.FirstOrDefault();
            QueueVocabularySave("SettingPageVocabularyRemoveEntry");
        }

        public void RefreshSettings()
        {
            CurrentVersion = ResolveCurrentVersion();
            LoadVocabularyTables(_settingsManager.LoadSettings());
        }

        private bool CanCheckForUpdate() => !IsCheckingUpdate && !IsDownloading;

        private bool CanDownloadUpdate() => HasUpdate && !IsDownloading && !IsCheckingUpdate;

        private bool CanRemoveVocabularyTable() => SelectedVocabularyTable != null;

        private bool CanExportVocabularyTable() => SelectedVocabularyTable != null;

        private bool CanAddVocabularyEntry() => SelectedVocabularyTable != null;

        private bool CanRemoveVocabularyEntry() => SelectedVocabularyTable != null && SelectedVocabularyEntry != null;

        private void RefreshLatestVersionText()
        {
            switch (_latestStatus)
            {
                case UpdateStatus.NotChecked:
                    LatestVersion = LanguageManager.GetString("UpdateStatusNotChecked");
                    break;
                case UpdateStatus.Checking:
                    LatestVersion = LanguageManager.GetString("UpdateStatusChecking");
                    break;
                case UpdateStatus.UpToDate:
                    LatestVersion = LanguageManager.GetString("UpdateStatusUpToDate");
                    break;
                case UpdateStatus.Failed:
                    LatestVersion = LanguageManager.GetString("UpdateStatusFailed");
                    break;
                case UpdateStatus.Disabled:
                    LatestVersion = LanguageManager.GetString("UpdateStatusUnavailable");
                    break;
                case UpdateStatus.UpdateAvailable:
                    LatestVersion = _activeSession?.TargetVersion?.ToString() ?? LanguageManager.GetString("UpdateStatusUpToDate");
                    break;
            }
        }

        private void LoadVocabularyTables(AppSettings settings)
        {
            VocabularyTables.CollectionChanged -= OnVocabularyTablesCollectionChanged;
            foreach (var existingTable in VocabularyTables.ToList())
            {
                UnsubscribeVocabularyTable(existingTable);
            }

            VocabularyTables.Clear();

            foreach (var table in (settings.CustomVocabularyTables ?? new System.Collections.Generic.List<CustomVocabularyTable>()).Select(model => new VocabularyTableEditor(model)))
            {
                VocabularyTables.Add(table);
                SubscribeVocabularyTable(table);
            }

            VocabularyTables.CollectionChanged += OnVocabularyTablesCollectionChanged;

            SelectedVocabularyTable = VocabularyTables.FirstOrDefault();
            SelectedVocabularyEntry = SelectedVocabularyTable?.Entries.FirstOrDefault();
            NotifyVocabularyUiChanged();
        }

        private void OnVocabularyTablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (VocabularyTableEditor table in e.OldItems)
                {
                    UnsubscribeVocabularyTable(table);
                }
            }

            if (e.NewItems != null)
            {
                foreach (VocabularyTableEditor table in e.NewItems)
                {
                    SubscribeVocabularyTable(table);
                }
            }

            NotifyVocabularyUiChanged();
        }

        private void SubscribeVocabularyTable(VocabularyTableEditor table)
        {
            table.PropertyChanged += OnVocabularyTablePropertyChanged;
            table.Entries.CollectionChanged += OnVocabularyEntriesCollectionChanged;

            foreach (var entry in table.Entries)
            {
                entry.PropertyChanged += OnVocabularyEntryPropertyChanged;
            }

            table.RefreshCounts();
        }

        private void UnsubscribeVocabularyTable(VocabularyTableEditor table)
        {
            table.PropertyChanged -= OnVocabularyTablePropertyChanged;
            table.Entries.CollectionChanged -= OnVocabularyEntriesCollectionChanged;

            foreach (var entry in table.Entries)
            {
                entry.PropertyChanged -= OnVocabularyEntryPropertyChanged;
            }
        }

        private void OnVocabularyEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (VocabularyEntryEditor entry in e.OldItems)
                {
                    entry.PropertyChanged -= OnVocabularyEntryPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (VocabularyEntryEditor entry in e.NewItems)
                {
                    entry.PropertyChanged += OnVocabularyEntryPropertyChanged;
                }
            }

            if (sender is ObservableCollection<VocabularyEntryEditor> entries)
            {
                var owner = VocabularyTables.FirstOrDefault(table => ReferenceEquals(table.Entries, entries));
                owner?.RefreshCounts();
            }

            NotifyVocabularyUiChanged();
            QueueVocabularySave("SettingPageVocabularyEntriesChanged");
        }

        private void OnVocabularyTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VocabularyTableEditor.Name) || e.PropertyName == nameof(VocabularyTableEditor.Enabled))
            {
                NotifyVocabularyUiChanged();
                QueueVocabularySave("SettingPageVocabularyTableEdited");
            }
        }

        private void OnVocabularyEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is VocabularyEntryEditor entry)
            {
                var owner = VocabularyTables.FirstOrDefault(table => table.Entries.Contains(entry));
                owner?.RefreshCounts();
            }

            NotifyVocabularyUiChanged();
            QueueVocabularySave("SettingPageVocabularyEntryEdited");
        }

        private void QueueVocabularySave(string changeSource)
        {
            _pendingVocabularyChangeSource = changeSource;
            _vocabularySaveTimer.Stop();
            _vocabularySaveTimer.Start();
        }

        private void PersistVocabularyTables(string changeSource)
        {
            _vocabularySaveTimer.Stop();
            _settingsManager.TryUpdateAndSave(
                changeSource,
                settings =>
                {
                    settings.CustomVocabularyTables = VocabularyTables
                        .Select(table => table.ToModel())
                        .Take(AppSettings.MaxCustomVocabularyTables)
                        .ToList();
                    return true;
                },
                out _);
        }

        private void NotifyVocabularyUiChanged()
        {
            OnPropertyChanged(nameof(CustomVocabularyTableCountSummary));
            OnPropertyChanged(nameof(CustomVocabularySelectedTableSummary));
            OnPropertyChanged(nameof(SelectedVocabularyEntries));
            OnPropertyChanged(nameof(HasSelectedVocabularyTable));
            RemoveVocabularyTableCommand.NotifyCanExecuteChanged();
            ExportVocabularyTableCommand.NotifyCanExecuteChanged();
            AddVocabularyEntryCommand.NotifyCanExecuteChanged();
            RemoveVocabularyEntryCommand.NotifyCanExecuteChanged();
        }

        private string BuildUniqueTableName(string baseName)
        {
            var normalizedBaseName = string.IsNullOrWhiteSpace(baseName)
                ? LanguageManager.GetString("CustomVocabularyDefaultTableName")
                : baseName.Trim();

            if (!VocabularyTables.Any(table => string.Equals(table.Name, normalizedBaseName, StringComparison.OrdinalIgnoreCase)))
            {
                return normalizedBaseName;
            }

            for (var index = 2; index <= AppSettings.MaxCustomVocabularyTables + 1; index++)
            {
                var candidate = $"{normalizedBaseName} {index}";
                if (!VocabularyTables.Any(table => string.Equals(table.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            return $"{normalizedBaseName} {Guid.NewGuid():N}";
        }

        private static string SanitizeFileName(string fileName)
        {
            var sanitized = fileName;
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "custom_vocabulary" : sanitized;
        }

        private static VocabularyTableEditor? SanitizeImportedTable(CustomVocabularyTable table, out bool truncated)
        {
            truncated = false;

            if (string.IsNullOrWhiteSpace(table.Name))
            {
                return null;
            }

            var editor = new VocabularyTableEditor
            {
                Id = string.IsNullOrWhiteSpace(table.Id) ? Guid.NewGuid().ToString("N") : table.Id.Trim(),
                Name = table.Name.Trim(),
                Enabled = table.Enabled
            };

            foreach (var entry in (table.Entries ?? new System.Collections.Generic.List<CustomVocabularyEntry>()).Take(AppSettings.MaxEntriesPerVocabularyTable))
            {
                if (string.IsNullOrWhiteSpace(entry.Term))
                {
                    truncated = true;
                    continue;
                }

                var sanitizedEntry = new VocabularyEntryEditor
                {
                    Term = entry.Term.Trim(),
                    AliasesText = string.Join(", ", (entry.Aliases ?? new System.Collections.Generic.List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(AppSettings.MaxAliasesPerVocabularyEntry)),
                    PronunciationsText = string.Join(", ", (entry.Pronunciations ?? new System.Collections.Generic.List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(AppSettings.MaxPronunciationsPerVocabularyEntry)),
                    Note = entry.Note?.Trim() ?? string.Empty,
                    Priority = entry.Priority,
                    Enabled = entry.Enabled
                };

                editor.Entries.Add(sanitizedEntry);

                if ((entry.Aliases?.Count ?? 0) > AppSettings.MaxAliasesPerVocabularyEntry
                    || (entry.Pronunciations?.Count ?? 0) > AppSettings.MaxPronunciationsPerVocabularyEntry)
                {
                    truncated = true;
                }
            }

            if ((table.Entries?.Count ?? 0) > AppSettings.MaxEntriesPerVocabularyTable)
            {
                truncated = true;
            }

            editor.RefreshCounts();
            return editor;
        }

        private void ReplaceTable(VocabularyTableEditor existing, VocabularyTableEditor incoming)
        {
            var index = VocabularyTables.IndexOf(existing);
            if (index < 0)
            {
                VocabularyTables.Add(incoming);
                return;
            }

            VocabularyTables[index] = incoming;
        }

        private static void ShowError(string message, Exception exception)
        {
            var errorTitle = LanguageManager.GetString("UpdateErrorTitle");
            WpfMessageBox.Show($"{message}: {exception.Message}", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string ResolveCurrentVersion()
        {
            try
            {
                var installed = _updateService.GetInstalledVersion();
                if (installed is not null)
                {
                    return installed.ToString();
                }

                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var version = assembly?.GetName().Version;
                return version?.ToString() ?? LanguageManager.GetString("UpdateVersionUnknown");
            }
            catch
            {
                return LanguageManager.GetString("UpdateVersionUnknown");
            }
        }

        partial void OnHasUpdateChanged(bool value)
        {
            if (!value)
            {
                UpdateNotes = string.Empty;
                if (_latestStatus == UpdateStatus.UpdateAvailable)
                {
                    _latestStatus = UpdateStatus.UpToDate;
                }
            }

            CheckForUpdateCommand.NotifyCanExecuteChanged();
            DownloadAndUpdateCommand.NotifyCanExecuteChanged();
            RefreshLatestVersionText();
        }

        partial void OnIsCheckingUpdateChanged(bool value)
        {
            if (value)
            {
                HasUpdate = false;
            }

            CheckForUpdateCommand.NotifyCanExecuteChanged();
            DownloadAndUpdateCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsDownloadingChanged(bool value)
        {
            CheckForUpdateCommand.NotifyCanExecuteChanged();
            DownloadAndUpdateCommand.NotifyCanExecuteChanged();
        }

        partial void OnSelectedVocabularyTableChanged(VocabularyTableEditor? value)
        {
            SelectedVocabularyEntry = value?.Entries.FirstOrDefault();
            NotifyVocabularyUiChanged();
        }

        partial void OnSelectedVocabularyEntryChanged(VocabularyEntryEditor? value)
        {
            _ = value;
            NotifyVocabularyUiChanged();
        }

        private sealed class VocabularyTableImportEnvelope
        {
            public int Version { get; set; } = 1;

            public CustomVocabularyTable? Table { get; set; }
        }

        private enum UpdateStatus
        {
            NotChecked,
            Checking,
            UpToDate,
            UpdateAvailable,
            Failed,
            Disabled
        }

        public void Dispose()
        {
            LanguageManager.LanguageChanged -= OnLanguageChanged;
        }
    }
}
