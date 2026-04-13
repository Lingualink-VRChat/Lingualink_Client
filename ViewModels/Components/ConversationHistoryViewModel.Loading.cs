using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;

namespace lingualink_client.ViewModels.Components
{
    public partial class ConversationHistoryViewModel
    {
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
            }
            finally
            {
                IsEntriesLoading = false;
            }
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

                if (SelectedSession?.SessionId.Equals(targetSessionId, StringComparison.OrdinalIgnoreCase) == true
                    && EntryMatchesCurrentFilters(e.Entry))
                {
                    Entries.Insert(0, new ConversationEntryItemViewModel(e.Entry));
                    SelectedEntry ??= Entries.FirstOrDefault();
                    CopySelectedCommand.NotifyCanExecuteChanged();
                }
            });
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
    }
}
