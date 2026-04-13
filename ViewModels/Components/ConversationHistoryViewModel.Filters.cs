using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Events;

namespace lingualink_client.ViewModels.Components
{
    public partial class ConversationHistoryViewModel
    {
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

        private bool EntryMatchesCurrentFilters(ConversationEntry entry)
        {
            return ConversationHistoryEntryFilter.Matches(
                entry,
                SelectedSourceFilter,
                SelectedStatusFilter,
                SearchText,
                SearchInTranslations);
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

            foreach (var propertyName in LocalizedPropertyNames)
            {
                OnPropertyChanged(propertyName);
            }

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
    }
}
