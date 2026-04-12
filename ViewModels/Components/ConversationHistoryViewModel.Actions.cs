using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using Forms = System.Windows.Forms;
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels.Components
{
    public partial class ConversationHistoryViewModel
    {
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

            var text = ConversationHistoryTextLogic.BuildSummaryExport(Entries.Select(entry => entry.Summary));
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            await CopyTextToClipboardAsync(text).ConfigureAwait(false);
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
                        _loggingManager.AddMessage(
                            "Clipboard copy failed: Win32 clipboard operation returned false.",
                            LogLevel.Warning,
                            ClipboardCategory);
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
    }
}
