using lingualink_client.Services;
using lingualink_client.Services.Events;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Ui;
using lingualink_client.ViewModels;
using lingualink_client.Views;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Ui.Appearance;

namespace lingualink_client
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly SettingsService _settingsService;
        private readonly IEventAggregator? _eventAggregator;
        private HwndSource? _hwndSource;

        private const int RecognitionHotkeyId = 0x5A01;
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();
            _settingsService = new SettingsService();

            if (ServiceContainer.TryResolve<IEventAggregator>(out var eventAggregator) && eventAggregator != null)
            {
                _eventAggregator = eventAggregator;
                _eventAggregator.Subscribe<SettingsChangedEvent>(OnSettingsChanged);
            }

            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate(typeof(IndexPage));

            var appSettings = _settingsService.LoadSettings();

            // 确保语言设置正确应用
            AppLanguageHelper.ApplyLanguage(appSettings);

            // 强制刷新UI以确保语言更改生效
            _viewModel.RefreshLanguageBindings();

            // 根据系统主题设置应用主题
            var systemTheme = GetSystemTheme();
            ApplicationThemeManager.Apply(systemTheme);
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
            RegisterRecognitionHotkey();
        }

        private ApplicationTheme GetSystemTheme()
        {
            try
            {
                // 检查系统是否使用深色主题
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var appsUseLightTheme = key?.GetValue("AppsUseLightTheme");

                if (appsUseLightTheme is int lightTheme)
                {
                    return lightTheme == 0 ? ApplicationTheme.Dark : ApplicationTheme.Light;
                }
            }
            catch
            {
                // 如果无法读取注册表，默认使用浅色主题
            }

            return ApplicationTheme.Light;
        }

        private void ThemesChanged(object sender, RoutedEventArgs e)
        {
            if (ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark)
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
            }
            else
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            UnregisterRecognitionHotkey();
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            _eventAggregator?.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);
            _viewModel.Dispose();
        }

        private void OnSettingsChanged(SettingsChangedEvent e)
        {
            Dispatcher.Invoke(() => RegisterRecognitionHotkey(e.Settings));
        }

        private void RegisterRecognitionHotkey(Models.AppSettings? settings = null)
        {
            UnregisterRecognitionHotkey();

            if (_hwndSource == null)
            {
                return;
            }

            var currentSettings = settings ?? _settingsService.LoadSettings();
            if (!HotkeyGesture.TryParse(currentSettings.ToggleRecognitionHotkey, out var gesture) || gesture == null)
            {
                return;
            }

            var windowHandle = new WindowInteropHelper(this).Handle;
            RegisterHotKey(windowHandle, RecognitionHotkeyId, gesture.GetNativeModifiers() | ModNoRepeat, gesture.GetVirtualKey());
        }

        private void UnregisterRecognitionHotkey()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            if (windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(windowHandle, RecognitionHotkeyId);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey && wParam.ToInt32() == RecognitionHotkeyId)
            {
                handled = true;
                TriggerRecognitionToggle();
            }

            return IntPtr.Zero;
        }

        private void TriggerRecognitionToggle()
        {
            if (Keyboard.FocusedElement is FrameworkElement focusedElement &&
                string.Equals(focusedElement.Name, "RecognitionHotkeyTextBox", StringComparison.Ordinal))
            {
                return;
            }

            var indexViewModel = (Application.Current as App)?.SharedIndexWindowViewModel;
            var toggleCommand = indexViewModel?.MainControl?.ToggleWorkCommand;
            if (toggleCommand?.CanExecute(null) == true)
            {
                toggleCommand.Execute(null);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
