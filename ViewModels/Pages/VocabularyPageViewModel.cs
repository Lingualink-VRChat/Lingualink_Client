using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UiMessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class VocabularyPageViewModel : ViewModelBase, IDisposable
    {
        private readonly ISettingsManager _settingsManager;
        private readonly DispatcherTimer _vocabularySaveTimer;
        private readonly Dictionary<string, CustomVocabularyTable> _lastValidTableSnapshots = new(StringComparer.OrdinalIgnoreCase);
        private string _pendingVocabularyChangeSource = "VocabularyPage";
        private bool _suppressBudgetRecovery;
        private bool _suppressEnabledSync;
        private bool _suppressExpansionSync;

        public string PageTitle => LanguageManager.GetString("CustomVocabularySectionTitle");
        public string GuidanceTitle => LanguageManager.GetString("CustomVocabularyGuidanceTitle");
        public string GuidanceHint => LanguageManager.GetString("CustomVocabularyGuidanceHint");
        public string GuidanceSummary => LanguageManager.GetString("CustomVocabularyGuidanceSummary");
        public string GuidanceBody => LanguageManager.GetString("CustomVocabularyGuidanceBody");
        public string EntryGuideTitle => LanguageManager.GetString("CustomVocabularyEntryGuideTitle");
        public string TermGuideText => LanguageManager.GetString("CustomVocabularyTermGuideText");
        public string AliasesGuideText => LanguageManager.GetString("CustomVocabularyAliasesGuideText");
        public string PronunciationsGuideText => LanguageManager.GetString("CustomVocabularyPronunciationsGuideText");
        public string NoteGuideText => LanguageManager.GetString("CustomVocabularyNoteGuideText");
        public string ExampleTitle => LanguageManager.GetString("CustomVocabularyExampleTitle");
        public string ExampleBody => LanguageManager.GetString("CustomVocabularyExampleBody");
        public string SectionHint => string.Format(
            LanguageManager.GetString("CustomVocabularySectionHint"),
            AppSettings.MaxCustomVocabularyTables,
            AppSettings.MaxEnabledCustomVocabularyTables,
            AppSettings.MaxEntriesPerVocabularyTable,
            AppSettings.MaxCustomVocabularyTableCharacters);

        public string AddTableLabel => LanguageManager.GetString("CustomVocabularyAddTable");
        public string ImportLabel => LanguageManager.GetString("CustomVocabularyImport");
        public string ExportLabel => LanguageManager.GetString("CustomVocabularyExport");
        public string DeleteTableLabel => LanguageManager.GetString("CustomVocabularyDeleteTable");
        public string TableNameLabel => LanguageManager.GetString("CustomVocabularyTableName");
        public string TableEnabledLabel => LanguageManager.GetString("CustomVocabularyTableEnabled");
        public string AddEntryLabel => LanguageManager.GetString("CustomVocabularyAddEntry");
        public string DeleteEntryLabel => LanguageManager.GetString("CustomVocabularyDeleteEntry");
        public string NoTableSelectedHint => LanguageManager.GetString("CustomVocabularyNoTableSelected");
        public string TableNamePlaceholder => LanguageManager.GetString("CustomVocabularyTableNamePlaceholder");
        public string EntryTermHeader => LanguageManager.GetString("CustomVocabularyEntryTermHeader");
        public string EntryAliasesHeader => LanguageManager.GetString("CustomVocabularyEntryAliasesHeader");
        public string EntryPronunciationsHeader => LanguageManager.GetString("CustomVocabularyEntryPronunciationsHeader");
        public string EntryNoteHeader => LanguageManager.GetString("CustomVocabularyEntryNoteHeader");
        public string EntryActionsHeader => LanguageManager.GetString("CustomVocabularyEntryActionsHeader");
        public string TermPlaceholder => LanguageManager.GetString("CustomVocabularyTermPlaceholder");
        public string AliasesPlaceholder => LanguageManager.GetString("CustomVocabularyAliasesPlaceholder");
        public string PronunciationsPlaceholder => LanguageManager.GetString("CustomVocabularyPronunciationsPlaceholder");
        public string NotePlaceholder => LanguageManager.GetString("CustomVocabularyNotePlaceholder");
        public string EnabledBadgeLabel => LanguageManager.GetString("CustomVocabularyEnabledBadge");
        public string NoTablesTitle => LanguageManager.GetString("CustomVocabularyNoTablesTitle");
        public string NoTablesBody => LanguageManager.GetString("CustomVocabularyNoTablesBody");
        public string MoveEntryUpLabel => LanguageManager.GetString("CustomVocabularyMoveEntryUp");
        public string MoveEntryDownLabel => LanguageManager.GetString("CustomVocabularyMoveEntryDown");
        public string ExpandTableLabel => LanguageManager.GetString("CustomVocabularyExpandTable");
        public string CollapseTableLabel => LanguageManager.GetString("CustomVocabularyCollapseTable");
        
        public string AliasesHint => string.Format(
            LanguageManager.GetString("CustomVocabularyAliasesHint"),
            AppSettings.MaxAliasesPerVocabularyEntry,
            AppSettings.MaxPronunciationsPerVocabularyEntry);

        public string TableCountSummary => string.Format(
            LanguageManager.GetString("CustomVocabularyTableCountSummary"),
            VocabularyTables.Count,
            AppSettings.MaxCustomVocabularyTables,
            VocabularyTables.Sum(table => table.TotalEntries),
            VocabularyTables.Count(table => table.Enabled));

        public string SelectedTableSummary => SelectedVocabularyTable == null
            ? NoTableSelectedHint
            : string.Format(
                LanguageManager.GetString("CustomVocabularySelectedTableSummary"),
                SelectedVocabularyTable.TotalEntries,
                AppSettings.MaxEntriesPerVocabularyTable,
                SelectedVocabularyTable.EnabledEntries,
                SelectedVocabularyTable.EffectiveCharacterCount,
                AppSettings.MaxCustomVocabularyTableCharacters);

        public string SelectedTableBudgetSummary => SelectedVocabularyTable == null
            ? string.Empty
            : string.Format(
                LanguageManager.GetString("CustomVocabularyCharacterBudgetSummary"),
                SelectedVocabularyTable.EffectiveCharacterCount,
                AppSettings.MaxCustomVocabularyTableCharacters);

        public bool HasSelectedVocabularyTable => SelectedVocabularyTable != null;
        public bool IsSelectedTableOverCharacterBudget => SelectedVocabularyTable?.IsOverCharacterBudget == true;

        public ObservableCollection<VocabularyTableEditor> VocabularyTables { get; } = new();

        public ObservableCollection<VocabularyEntryEditor>? SelectedVocabularyEntries => SelectedVocabularyTable?.Entries;

        [ObservableProperty]
        private VocabularyTableEditor? selectedVocabularyTable;

        [ObservableProperty]
        private VocabularyEntryEditor? selectedVocabularyEntry;

        public VocabularyPageViewModel(ISettingsManager? settingsManager = null)
        {
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

            LoadVocabularyTables(_settingsManager.LoadSettings());
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            foreach (var table in VocabularyTables)
            {
                table.RefreshCounts();
            }

            OnPropertyChanged(string.Empty);
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
                Name = BuildUniqueTableName(LanguageManager.GetString("CustomVocabularyDefaultTableName")),
                Enabled = !VocabularyTables.Any(existingTable => existingTable.Enabled)
            };

            VocabularyTables.Add(table);
            if (table.Enabled)
            {
                EnforceSingleEnabledTable(table, queueSave: false);
            }
            CaptureTableSnapshot(table);
            SelectedVocabularyTable = table;
            QueueVocabularySave("VocabularyPageAddTable");
        }

        [RelayCommand]
        private void RemoveVocabularyTable(VocabularyTableEditor? table)
        {
            var targetTable = table ?? SelectedVocabularyTable;
            if (targetTable == null)
            {
                return;
            }

            var confirmMessage = string.Format(
                LanguageManager.GetString("CustomVocabularyDeleteTableConfirm"),
                targetTable.Name);
            if (UiMessageBox.ShowConfirm(confirmMessage) != MessageBoxResult.Yes)
            {
                return;
            }

            var removed = targetTable;
            var removedIndex = VocabularyTables.IndexOf(removed);
            VocabularyTables.Remove(removed);
            _lastValidTableSnapshots.Remove(removed.Id);
            SelectedVocabularyTable = VocabularyTables.Count == 0
                ? null
                : VocabularyTables[Math.Clamp(removedIndex, 0, VocabularyTables.Count - 1)];
            ApplyAccordionState(SelectedVocabularyTable);
            SelectedVocabularyEntry = SelectedVocabularyTable?.Entries.FirstOrDefault();
            QueueVocabularySave("VocabularyPageRemoveTable");
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
                        if (importedTable.Enabled)
                        {
                            EnforceSingleEnabledTable(importedTable, queueSave: false);
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

                        importedTable.Name = BuildUniqueTableName(importedTable.Name);
                        VocabularyTables.Add(importedTable);
                        CaptureTableSnapshot(importedTable);
                        if (importedTable.Enabled)
                        {
                            EnforceSingleEnabledTable(importedTable, queueSave: false);
                        }
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
                    CaptureTableSnapshot(importedTable);
                    if (importedTable.Enabled)
                    {
                        EnforceSingleEnabledTable(importedTable, queueSave: false);
                    }
                }

                SelectedVocabularyTable = VocabularyTables.FirstOrDefault(
                    table => string.Equals(table.Id, importedTable.Id, StringComparison.OrdinalIgnoreCase))
                    ?? VocabularyTables.FirstOrDefault(table => string.Equals(table.Name, importedTable.Name, StringComparison.OrdinalIgnoreCase));
                SelectedVocabularyEntry = SelectedVocabularyTable?.Entries.FirstOrDefault();
                PersistVocabularyTables("VocabularyPageImport");

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

        [RelayCommand]
        private async Task ExportVocabularyTableAsync(VocabularyTableEditor? table)
        {
            var targetTable = table ?? SelectedVocabularyTable;
            if (targetTable == null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON (*.json)|*.json",
                FileName = $"{SanitizeFileName(targetTable.Name)}.json"
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
                    Table = targetTable.ToModel()
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

        [RelayCommand]
        private void AddVocabularyEntry(VocabularyTableEditor? table)
        {
            var targetTable = table ?? SelectedVocabularyTable;
            if (targetTable == null)
            {
                return;
            }

            if (targetTable.Entries.Count >= AppSettings.MaxEntriesPerVocabularyTable)
            {
                UiMessageBox.ShowWarning(
                    string.Format(
                        LanguageManager.GetString("CustomVocabularyMaxEntriesReached"),
                        AppSettings.MaxEntriesPerVocabularyTable));
                return;
            }

            var entry = new VocabularyEntryEditor
            {
                Priority = targetTable.Entries.Count + 1
            };

            targetTable.Entries.Add(entry);
            SelectedVocabularyTable = targetTable;
            ApplyAccordionState(targetTable);
            SelectedVocabularyEntry = entry;
            QueueVocabularySave("VocabularyPageAddEntry");
        }

        [RelayCommand]
        private void RemoveVocabularyEntry(VocabularyEntryEditor? entry)
        {
            if (!TryFindOwnerTable(entry ?? SelectedVocabularyEntry, out var owner, out var targetEntry))
            {
                return;
            }

            owner.Entries.Remove(targetEntry);
            NormalizeEntryPriorities(owner);
            SelectedVocabularyTable = owner;
            ApplyAccordionState(owner);
            SelectedVocabularyEntry = owner.Entries.FirstOrDefault();
            QueueVocabularySave("VocabularyPageRemoveEntry");
        }

        [RelayCommand]
        private void MoveVocabularyEntryUp(VocabularyEntryEditor? entry)
        {
            MoveVocabularyEntry(entry, -1);
        }

        [RelayCommand]
        private void MoveVocabularyEntryDown(VocabularyEntryEditor? entry)
        {
            MoveVocabularyEntry(entry, 1);
        }

        [RelayCommand]
        private void ToggleVocabularyTableExpansion(VocabularyTableEditor? table)
        {
            if (table == null)
            {
                return;
            }

            var shouldExpand = !table.IsExpanded;
            ApplyAccordionState(shouldExpand ? table : null);
            SelectedVocabularyTable = table;
        }

        public void RefreshSettings()
        {
            LoadVocabularyTables(_settingsManager.LoadSettings());
        }

        public Task LoadVocabularyAsync()
        {
            LoadVocabularyTables(_settingsManager.LoadSettings());
            return Task.CompletedTask;
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

            EnforceSingleEnabledTable(VocabularyTables.FirstOrDefault(table => table.Enabled), queueSave: false);

            VocabularyTables.CollectionChanged += OnVocabularyTablesCollectionChanged;
            CaptureAllVocabularySnapshots();

            SelectedVocabularyTable = VocabularyTables.FirstOrDefault();
            ApplyAccordionState(SelectedVocabularyTable);
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
                if (owner != null)
                {
                    if (RecoverTableIfOverBudget(owner))
                    {
                        QueueVocabularySave("VocabularyPageBudgetRecover");
                        return;
                    }

                    CaptureTableSnapshot(owner);
                }
            }

            NotifyVocabularyUiChanged();
            QueueVocabularySave("VocabularyPageEntriesChanged");
        }

        private void OnVocabularyTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is VocabularyTableEditor expandedTable
                && e.PropertyName == nameof(VocabularyTableEditor.IsExpanded)
                && expandedTable.IsExpanded)
            {
                SelectedVocabularyTable = expandedTable;
                ApplyAccordionState(expandedTable);
                return;
            }

            if (e.PropertyName == nameof(VocabularyTableEditor.Name) || e.PropertyName == nameof(VocabularyTableEditor.Enabled))
            {
                if (sender is VocabularyTableEditor table)
                {
                    if (e.PropertyName == nameof(VocabularyTableEditor.Enabled))
                    {
                        EnforceSingleEnabledTable(table.Enabled ? table : null, queueSave: false);
                    }

                    CaptureTableSnapshot(table);
                }

                NotifyVocabularyUiChanged();
                QueueVocabularySave("VocabularyPageTableEdited");
            }
        }

        private void OnVocabularyEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is VocabularyEntryEditor entry)
            {
                var owner = VocabularyTables.FirstOrDefault(table => table.Entries.Contains(entry));
                owner?.RefreshCounts();
                if (owner != null)
                {
                    if (RecoverTableIfOverBudget(owner))
                    {
                        QueueVocabularySave("VocabularyPageBudgetRecover");
                        return;
                    }

                    CaptureTableSnapshot(owner);
                }
            }

            NotifyVocabularyUiChanged();
            QueueVocabularySave("VocabularyPageEntryEdited");
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
            EnforceSingleEnabledTable(VocabularyTables.FirstOrDefault(table => table.Enabled), queueSave: false);
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
            OnPropertyChanged(nameof(TableCountSummary));
            OnPropertyChanged(nameof(SelectedTableSummary));
            OnPropertyChanged(nameof(SelectedTableBudgetSummary));
            OnPropertyChanged(nameof(SelectedVocabularyEntries));
            OnPropertyChanged(nameof(HasSelectedVocabularyTable));
            OnPropertyChanged(nameof(IsSelectedTableOverCharacterBudget));
        }

        partial void OnSelectedVocabularyTableChanged(VocabularyTableEditor? value)
        {
            if (value != null)
            {
                ApplyAccordionState(value);
            }

            SelectedVocabularyEntry = value?.Entries.FirstOrDefault();
            NotifyVocabularyUiChanged();
        }

        partial void OnSelectedVocabularyEntryChanged(VocabularyEntryEditor? value)
        {
        }

        private void EnforceSingleEnabledTable(VocabularyTableEditor? preferredTable, bool queueSave)
        {
            if (_suppressEnabledSync)
            {
                return;
            }

            try
            {
                _suppressEnabledSync = true;

                VocabularyTableEditor? activeTable = preferredTable?.Enabled == true
                    ? preferredTable
                    : VocabularyTables.FirstOrDefault(table => table.Enabled);

                foreach (var table in VocabularyTables)
                {
                    var shouldEnable = activeTable != null && ReferenceEquals(table, activeTable);
                    if (table.Enabled != shouldEnable)
                    {
                        table.Enabled = shouldEnable;
                    }
                }
            }
            finally
            {
                _suppressEnabledSync = false;
            }

            if (queueSave)
            {
                QueueVocabularySave("VocabularyPageSingleEnable");
            }
        }

        private void CaptureAllVocabularySnapshots()
        {
            _lastValidTableSnapshots.Clear();

            foreach (var table in VocabularyTables)
            {
                CaptureTableSnapshot(table);
            }
        }

        private void CaptureTableSnapshot(VocabularyTableEditor table)
        {
            if (_suppressBudgetRecovery || table.IsOverCharacterBudget || string.IsNullOrWhiteSpace(table.Id))
            {
                return;
            }

            _lastValidTableSnapshots[table.Id] = table.ToModel();
        }

        private bool RecoverTableIfOverBudget(VocabularyTableEditor table)
        {
            if (_suppressBudgetRecovery || !table.IsOverCharacterBudget)
            {
                return false;
            }

            if (!_lastValidTableSnapshots.TryGetValue(table.Id, out var snapshot))
            {
                return false;
            }

            try
            {
                _suppressBudgetRecovery = true;

                var restored = new VocabularyTableEditor(snapshot);
                var index = VocabularyTables.IndexOf(table);
                if (index < 0)
                {
                    return false;
                }

                VocabularyTables[index] = restored;
                if (ReferenceEquals(SelectedVocabularyTable, table) || string.Equals(SelectedVocabularyTable?.Id, restored.Id, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedVocabularyTable = restored;
                }
            }
            finally
            {
                _suppressBudgetRecovery = false;
            }

            UiMessageBox.ShowWarning(string.Format(
                LanguageManager.GetString("CustomVocabularyBudgetExceeded"),
                AppSettings.MaxCustomVocabularyTableCharacters));
            return true;
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

            var totalCharacters = 0;
            foreach (var entry in (table.Entries ?? new System.Collections.Generic.List<CustomVocabularyEntry>()).Take(AppSettings.MaxEntriesPerVocabularyTable))
            {
                var normalizedTerm = AppSettings.NormalizeVocabularyTerm(entry.Term);
                if (string.IsNullOrWhiteSpace(normalizedTerm))
                {
                    truncated = true;
                    continue;
                }

                var normalizedAliases = AppSettings.NormalizeVocabularyValues(
                    entry.Aliases ?? new System.Collections.Generic.List<string>(),
                    AppSettings.MaxAliasesPerVocabularyEntry,
                    AppSettings.MaxAliasesCharactersPerVocabularyEntry);
                var normalizedPronunciations = AppSettings.NormalizeVocabularyValues(
                    entry.Pronunciations ?? new System.Collections.Generic.List<string>(),
                    AppSettings.MaxPronunciationsPerVocabularyEntry,
                    AppSettings.MaxPronunciationsCharactersPerVocabularyEntry);
                var entryCharacters = AppSettings.CountVocabularyEntryCharacters(
                    normalizedTerm,
                    normalizedAliases,
                    normalizedPronunciations);

                if (totalCharacters + entryCharacters > AppSettings.MaxCustomVocabularyTableCharacters)
                {
                    truncated = true;
                    break;
                }

                var sanitizedEntry = new VocabularyEntryEditor
                {
                    Term = normalizedTerm,
                    AliasesText = string.Join(", ", normalizedAliases),
                    PronunciationsText = string.Join(", ", normalizedPronunciations),
                    Note = entry.Note?.Trim() ?? string.Empty,
                    Priority = entry.Priority,
                    Enabled = entry.Enabled
                };

                editor.Entries.Add(sanitizedEntry);
                totalCharacters += entryCharacters;

                if (!string.Equals(normalizedTerm, entry.Term?.Trim(), StringComparison.Ordinal)
                    || (entry.Aliases?.Count ?? 0) > AppSettings.MaxAliasesPerVocabularyEntry
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
                CaptureTableSnapshot(incoming);
                return;
            }

            VocabularyTables[index] = incoming;
            CaptureTableSnapshot(incoming);
        }

        private void ApplyAccordionState(VocabularyTableEditor? expandedTable)
        {
            if (_suppressExpansionSync)
            {
                return;
            }

            try
            {
                _suppressExpansionSync = true;

                foreach (var table in VocabularyTables)
                {
                    var shouldExpand = expandedTable != null && ReferenceEquals(table, expandedTable);
                    if (table.IsExpanded != shouldExpand)
                    {
                        table.IsExpanded = shouldExpand;
                    }
                }
            }
            finally
            {
                _suppressExpansionSync = false;
            }

            NotifyVocabularyUiChanged();
        }

        private bool TryFindOwnerTable(VocabularyEntryEditor? entry, out VocabularyTableEditor owner, out VocabularyEntryEditor targetEntry)
        {
            owner = default!;
            targetEntry = default!;

            if (entry == null)
            {
                return false;
            }

            var matchedOwner = VocabularyTables.FirstOrDefault(table => table.Entries.Contains(entry));
            if (matchedOwner == null)
            {
                return false;
            }

            owner = matchedOwner;
            targetEntry = entry;
            return true;
        }

        private void MoveVocabularyEntry(VocabularyEntryEditor? entry, int direction)
        {
            if (!TryFindOwnerTable(entry, out var owner, out var targetEntry))
            {
                return;
            }

            var currentIndex = owner.Entries.IndexOf(targetEntry);
            if (currentIndex < 0)
            {
                return;
            }

            var targetIndex = currentIndex + direction;
            if (targetIndex < 0 || targetIndex >= owner.Entries.Count)
            {
                return;
            }

            owner.Entries.Move(currentIndex, targetIndex);
            NormalizeEntryPriorities(owner);
            SelectedVocabularyTable = owner;
            ApplyAccordionState(owner);
            SelectedVocabularyEntry = targetEntry;
            QueueVocabularySave("VocabularyPageMoveEntry");
        }

        private static void NormalizeEntryPriorities(VocabularyTableEditor table)
        {
            for (var index = 0; index < table.Entries.Count; index++)
            {
                var expectedPriority = index + 1;
                if (table.Entries[index].Priority != expectedPriority)
                {
                    table.Entries[index].Priority = expectedPriority;
                }
            }
        }

        public void Dispose()
        {
            _vocabularySaveTimer.Stop();
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            GC.SuppressFinalize(this);
        }

        private sealed class VocabularyTableImportEnvelope
        {
            public int Version { get; set; } = 1;
            public CustomVocabularyTable? Table { get; set; }
        }
    }
}
