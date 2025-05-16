// File: Views/Pages/LogPage.xaml.cs
using lingualink_client.ViewModels; // Ensure this is present for IndexWindowViewModel
using System.Diagnostics; // Add this
using System.Windows;
using System.Windows.Controls;

namespace lingualink_client.Views
{
    public partial class LogPage : Page
    {
        public LogPage()
        {
            InitializeComponent();
            var appInstance = Application.Current as App;
            if (appInstance == null)
            {
                Debug.WriteLine("LogPage Constructor: Application.Current is not of type App OR is null!");
                return;
            }

            var sharedViewModel = appInstance.SharedIndexWindowViewModel;
            if (sharedViewModel == null)
            {
                Debug.WriteLine("LogPage Constructor: appInstance.SharedIndexWindowViewModel is NULL!");
            }
            else
            {
                Debug.WriteLine($"LogPage Constructor: Assigning DataContext. SharedViewModel LogMessages count: {sharedViewModel.LogMessages.Count}");
                foreach(var msg in sharedViewModel.LogMessages)
                {
                    Debug.WriteLine($"  Existing Log: {msg}");
                }
            }
            DataContext = sharedViewModel;

            // Verify DataContext after assignment
            if (this.DataContext == null)
            {
                Debug.WriteLine("LogPage DataContext is STILL NULL after assignment!");
            }
            else
            {
                Debug.WriteLine($"LogPage DataContext successfully SET to type: {this.DataContext.GetType().FullName}");
                if (this.DataContext is IndexWindowViewModel vm)
                {
                    Debug.WriteLine($"LogPage DataContext (as VM) LogMessages count: {vm.LogMessages.Count}");
                }
            }
        }
    }
}