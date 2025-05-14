using System;
using System.Windows;
using System.Windows.Media.Animation;
using lingualink_client.ViewModels;

namespace lingualink_client
{
    public partial class IndexWindow : Window
    {
        private readonly IndexWindowViewModel _viewModel;

        public IndexWindow()
        {
            InitializeComponent();
            _viewModel = new IndexWindowViewModel();
            DataContext = _viewModel;
            // Loaded event is no longer strictly needed as ViewModel constructor handles init
        }

        private void IndexWindow_Closed(object? sender, EventArgs e)
        {
            _viewModel.Dispose(); // Ensure ViewModel cleans up its resources
        }
    }
}