using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using lingualink_client.Models;

namespace lingualink_client.ViewModels
{
    public partial class VocabularyTableEditor : ObservableObject
    {
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString("N");

        [ObservableProperty]
        private string name = "新词表";

        [ObservableProperty]
        private bool enabled = true;

        public ObservableCollection<VocabularyEntryEditor> Entries { get; }

        public int TotalEntries => Entries.Count;

        public int EnabledEntries => Entries.Count(entry => entry.Enabled && !string.IsNullOrWhiteSpace(entry.Term));

        public VocabularyTableEditor()
        {
            Entries = new ObservableCollection<VocabularyEntryEditor>();
        }

        public VocabularyTableEditor(CustomVocabularyTable table)
        {
            Id = string.IsNullOrWhiteSpace(table.Id) ? Guid.NewGuid().ToString("N") : table.Id;
            Name = string.IsNullOrWhiteSpace(table.Name) ? "新词表" : table.Name.Trim();
            Enabled = table.Enabled;
            Entries = new ObservableCollection<VocabularyEntryEditor>(
                (table.Entries ?? new List<CustomVocabularyEntry>()).Select(entry => new VocabularyEntryEditor(entry)));
        }

        public void RefreshCounts()
        {
            OnPropertyChanged(nameof(TotalEntries));
            OnPropertyChanged(nameof(EnabledEntries));
        }

        public CustomVocabularyTable ToModel()
        {
            return new CustomVocabularyTable
            {
                Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
                Name = string.IsNullOrWhiteSpace(Name) ? "新词表" : Name.Trim(),
                Enabled = Enabled,
                Entries = Entries
                    .Select(entry => entry.ToModel())
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Term))
                    .Take(AppSettings.MaxEntriesPerVocabularyTable)
                    .ToList()
            };
        }
    }

    public partial class VocabularyEntryEditor : ObservableObject
    {
        [ObservableProperty]
        private string term = string.Empty;

        [ObservableProperty]
        private string aliasesText = string.Empty;

        [ObservableProperty]
        private string pronunciationsText = string.Empty;

        [ObservableProperty]
        private string note = string.Empty;

        [ObservableProperty]
        private int priority;

        [ObservableProperty]
        private bool enabled = true;

        public VocabularyEntryEditor()
        {
        }

        public VocabularyEntryEditor(CustomVocabularyEntry entry)
        {
            Term = entry.Term ?? string.Empty;
            AliasesText = JoinList(entry.Aliases);
            PronunciationsText = JoinList(entry.Pronunciations);
            Note = entry.Note ?? string.Empty;
            Priority = entry.Priority;
            Enabled = entry.Enabled;
        }

        public CustomVocabularyEntry ToModel()
        {
            return new CustomVocabularyEntry
            {
                Term = AppSettings.NormalizeVocabularyTerm(Term),
                Aliases = AppSettings.NormalizeVocabularyValues(
                    SplitMultiValueText(AliasesText, AppSettings.MaxAliasesPerVocabularyEntry),
                    AppSettings.MaxAliasesPerVocabularyEntry,
                    AppSettings.MaxAliasesCharactersPerVocabularyEntry),
                Pronunciations = AppSettings.NormalizeVocabularyValues(
                    SplitMultiValueText(PronunciationsText, AppSettings.MaxPronunciationsPerVocabularyEntry),
                    AppSettings.MaxPronunciationsPerVocabularyEntry,
                    AppSettings.MaxPronunciationsCharactersPerVocabularyEntry),
                Note = Note?.Trim() ?? string.Empty,
                Priority = Priority,
                Enabled = Enabled
            };
        }

        private static string JoinList(IEnumerable<string>? values)
        {
            return values == null ? string.Empty : string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        }

        private static List<string> SplitMultiValueText(string? raw, int limit)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<string>();
            }

            var separators = new[] { ',', '，', ';', '；', '|', '\n', '\r', '\t' };

            return raw
                .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }
    }
}
