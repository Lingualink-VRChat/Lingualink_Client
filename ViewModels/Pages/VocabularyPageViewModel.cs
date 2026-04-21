using System;
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
using Microsoft.Win32;
using UiMessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class VocabularyPageViewModel : ViewModelBase, IDisposable
    {
        private readonly ISettingsManager _settingsManager;
        private readonly DispatcherTimer _vocabularySaveTimer;
        private string _pendingVocabularyChangeSource = "VocabularyPage";

        public string PageTitle => LanguageManager.GetString("CustomVocabularySectionTitle");
        public string GuidanceTitle => LanguageManager.GetString("CustomVocabularyGuidanceTitle");
        public string SectionHint => string.Format(
            LanguageManager.GetString("CustomVocabularySectionHint"),
            AppSettings.MaxCustomVocabularyTables,
            AppSettings.MaxEntriesPerVocabularyTable,
            AppSettings.MaxCustomVocabularyPayloadEntries);

        public string AddTableLabel => LanguageManager.GetString("CustomVocabularyAddTable");
        public string ImportLabel => LanguageManager.GetString("CustomVocabularyImport");
        public string ExportLabel => LanguageManager.GetString("CustomVocabularyExport");
        public string DeleteTableLabel => LanguageManager.GetString("CustomVocabularyDeleteTable");
        public string TableNameLabel => LanguageManager.GetString("CustomVocabularyTableName");
        public string TableEnabledLabel => LanguageManager.GetString("CustomVocabularyTableEnabled");
        public string AddEntryLabel => LanguageManager.GetString("CustomVocabularyAddEntry");
        public string DeleteEntryLabel => LanguageManager.GetString("CustomVocabularyDeleteEntry");
        public string NoTableSelectedHint => LanguageManager.GetString("CustomVocabularyNoTableSelected");
        
        public string AliasesHint => string.Format(
            LanguageManager.GetString("CustomVocabularyAliasesHint"),
            AppSettings.MaxAliasesPerVocabularyEntry,
            AppSettings.MaxPronunciationsPerVocabularyEntry);

        public string TableCountSummary => string.Format(
            LanguageManager.GetString("CustomVocabularyTableCountSummary"),
            VocabularyTables.Count,
            AppSettings.MaxCustomVocabularyTables,
            VocabularyTables.Sum(table => table.TotalEntries));

        public string SelectedTableSummary => SelectedVocabularyTable == null
            ? NoTableSelectedHint
            : string.Format(
                LanguageManager.GetString("CustomVocabularySelectedTableSummary"),
                SelectedVocabularyTable.TotalEntries,
                AppSettings.MaxEntriesPerVocabularyTable,
                SelectedVocabularyTable.EnabledEntries);

        public bool HasSelectedVocabularyTable => SelectedVocabularyTable != null;

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
                Name = BuildUniqueTableName(LanguageManager.GetString("CustomVocabularyDefaultTableName"))
            };

            VocabularyTables.Add(table);
            SelectedVocabularyTable = table;
            QueueVocabularySave("VocabularyPageAddTable");
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
            QueueVocabularySave("VocabularyPageRemoveTable");
        }

        [RelayCommand]
        private async Task ImportVocabularyTableAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
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

        [RelayCommand(CanExecute = nameof(CanExportVocabularyTable))]
        private async Task ExportVocabularyTableAsync()
        {
            if (SelectedVocabularyTable == null)
            {
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
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
            QueueVocabularySave("VocabularyPageAddEntry");
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
            QueueVocabularySave("VocabularyPageRemoveEntry");
        }

        public void RefreshSettings()
        {
            LoadVocabularyTables(_settingsManager.LoadSettings());
        }

        public async Task LoadVocabularyAsync()
        {
            await Task.Run(() => LoadVocabularyTables(_settingsManager.LoadSettings()));
        }

        private bool CanRemoveVocabularyTable() => SelectedVocabularyTable != null;

        private bool CanExportVocabularyTable() => SelectedVocabularyTable != null;

        private bool CanAddVocabularyEntry() => SelectedVocabularyTable != null;

        private bool CanRemoveVocabularyEntry() => SelectedVocabularyTable != null && SelectedVocabularyEntry != null;

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
            QueueVocabularySave("VocabularyPageEntriesChanged");
        }

        private void OnVocabularyTablePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VocabularyTableEditor.Name) || e.PropertyName == nameof(VocabularyTableEditor.Enabled))
            {
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
