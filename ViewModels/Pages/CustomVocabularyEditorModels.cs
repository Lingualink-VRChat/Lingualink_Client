using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using lingualink_client.Models;
using lingualink_client.Services;

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

        [ObservableProperty]
        private bool isExpanded;

        public ObservableCollection<VocabularyEntryEditor> Entries { get; }

        public int TotalEntries => Entries.Count;

        public int EnabledEntries => Entries.Count(entry => entry.Enabled && !string.IsNullOrWhiteSpace(entry.Term));

        public int EffectiveCharacterCount => Entries
            .Select(entry => entry.ToNormalizedModel())
            .Where(entry => entry.Enabled && !string.IsNullOrWhiteSpace(entry.Term))
            .Sum(entry => AppSettings.CountVocabularyEntryCharacters(entry.Term));

        public bool IsOverCharacterBudget => EffectiveCharacterCount > AppSettings.MaxCustomVocabularyTableCharacters;

        public string HeaderSummary => string.Format(
            LanguageManager.GetString("CustomVocabularyTableStatusSummary"),
            TotalEntries,
            AppSettings.MaxEntriesPerVocabularyTable,
            EffectiveCharacterCount,
            AppSettings.MaxCustomVocabularyTableCharacters);

        public string ExpandGlyph => IsExpanded ? "▼" : "▶";

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
            OnPropertyChanged(nameof(EffectiveCharacterCount));
            OnPropertyChanged(nameof(IsOverCharacterBudget));
            OnPropertyChanged(nameof(HeaderSummary));
            OnPropertyChanged(nameof(ExpandGlyph));
        }

        partial void OnIsExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(ExpandGlyph));
        }

        public CustomVocabularyTable ToModel()
        {
            var normalizedEntries = new List<CustomVocabularyEntry>();
            var totalCharacters = 0;

            foreach (var entry in Entries.Select(item => item.ToNormalizedModel()).Where(item => !string.IsNullOrWhiteSpace(item.Term)))
            {
                var entryCharacters = AppSettings.CountVocabularyEntryCharacters(entry.Term);
                if (normalizedEntries.Count >= AppSettings.MaxEntriesPerVocabularyTable
                    || totalCharacters + entryCharacters > AppSettings.MaxCustomVocabularyTableCharacters)
                {
                    break;
                }

                normalizedEntries.Add(entry);
                totalCharacters += entryCharacters;
            }

            return new CustomVocabularyTable
            {
                Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id.Trim(),
                Name = string.IsNullOrWhiteSpace(Name) ? "新词表" : Name.Trim(),
                Enabled = Enabled,
                Entries = normalizedEntries
            };
        }
    }

    public partial class VocabularyEntryEditor : ObservableObject
    {
        [ObservableProperty]
        private string term = string.Empty;

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
            Priority = entry.Priority;
            Enabled = entry.Enabled;
        }

        public CustomVocabularyEntry ToNormalizedModel()
        {
            return new CustomVocabularyEntry
            {
                Term = AppSettings.NormalizeVocabularyTerm(Term),
                Priority = Priority,
                Enabled = Enabled
            };
        }

        public CustomVocabularyEntry ToModel()
        {
            return ToNormalizedModel();
        }
    }
}
