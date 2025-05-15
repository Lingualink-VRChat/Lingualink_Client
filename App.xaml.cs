using lingualink_client.ViewModels;
using System.Windows;

namespace lingualink_client
{
    public partial class App : Application
    {
        public IndexWindowViewModel SharedIndexWindowViewModel { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SharedIndexWindowViewModel = new IndexWindowViewModel();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SharedIndexWindowViewModel?.Dispose(); // Ensure ViewModel is disposed
            base.OnExit(e);
        }
    }
}