using System.Windows;
using System.Windows.Controls;
using lingualink_client.Models;
using lingualink_client.ViewModels.Components;

namespace lingualink_client.Views
{
    public partial class LogPage : Page
    {
        private LogViewModel? _viewModel;

        public LogPage()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null)
            {
                _viewModel = new LogViewModel();
                _viewModel.EntryAppended += OnEntryAppended;
            }

            DataContext = _viewModel;
        }

        private void OnEntryAppended(object? sender, LogEntry entry)
        {
            if (_viewModel == null)
            {
                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (!_viewModel.AutoScroll)
                {
                    return;
                }

                LogListBox.ScrollIntoView(entry);
                LogListBox.SelectedItem = entry;
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DataContext = null;

            if (_viewModel == null)
            {
                return;
            }

            _viewModel.EntryAppended -= OnEntryAppended;
            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}
