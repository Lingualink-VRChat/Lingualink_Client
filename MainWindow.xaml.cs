using System;
using System.Windows;
using lingualink_client.ViewModels;

namespace lingualink_client
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            // Loaded event is no longer strictly needed as ViewModel constructor handles init
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _viewModel.Dispose(); // Ensure ViewModel cleans up its resources
        }

        // Removed:
        // - All service fields and event handlers; logic now in ViewModel
    }
}