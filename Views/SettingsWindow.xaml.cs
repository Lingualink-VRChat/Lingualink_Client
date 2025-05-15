using System.Windows;
using lingualink_client.Models;
using lingualink_client.ViewModels;

namespace lingualink_client
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsWindowViewModel _viewModel;

        // This public property allows IndexWindowViewModel to retrieve the settings
        public AppSettings? UpdatedSettings => _viewModel.SavedAppSettings; 

        public SettingsWindow(AppSettings currentSettings)
        {
            InitializeComponent();
            _viewModel = new SettingsWindowViewModel(currentSettings);
            DataContext = _viewModel;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.TrySaveChanges())
            {
                this.DialogResult = true; // Signal success to the caller
                this.Close();
            }
            // If TrySaveChanges returns false, validation failed and ViewModel already informed user
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Signal cancellation
            this.Close();
        }
    }
}