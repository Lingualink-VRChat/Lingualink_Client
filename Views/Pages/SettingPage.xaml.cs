using lingualink_client.Services;
using lingualink_client.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using lingualink_client.Models;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Ui;
using UiMessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.Views
{
    /// <summary>
    /// SettingPage.xaml 的交互逻辑
    /// </summary>
        public partial class SettingPage : Page
        {
            private SettingPageViewModel? _viewModel;
            private readonly SettingsService _settingsService;
            private readonly ISettingsManager _settingsManager;
            private AppSettings _appSettings;
            private string _pendingRecognitionHotkey = string.Empty;

        public List<CultureInfo> Languages { get; set; }

        public SettingPage()
        {
            InitializeComponent();

            this.Loaded += SettingPage_Loaded;
            this.Unloaded += SettingPage_Unloaded;

            _settingsService = new SettingsService();
            _settingsManager = ServiceContainer.TryResolve<ISettingsManager>(out var resolvedSettingsManager) && resolvedSettingsManager != null
                ? resolvedSettingsManager
                : new SettingsManager(_settingsService, ServiceContainer.TryResolve<IEventAggregator>(out var eventAggregator) ? eventAggregator : null);
            _appSettings = _settingsService.LoadSettings();

            Languages = LanguageManager.GetAvailableLanguages();
            LanguageComboBox.ItemsSource = Languages;

            var currentCulture = Thread.CurrentThread.CurrentUICulture;
            LanguageComboBox.SelectedItem = LanguageManager.GetAvailableLanguages()
                .FirstOrDefault(c => c.Name == _appSettings.GlobalLanguage);
        }

        private void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel ??= new SettingPageViewModel();
            DataContext = _viewModel;
            _appSettings = _settingsService.LoadSettings();
            _viewModel.RefreshSettings();
            LoadRecognitionHotkeyFromSettings();
        }

        private void SettingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DataContext = null;
            _viewModel?.Dispose();
            _viewModel = null;
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is CultureInfo cultureInfo)
            {
                _appSettings.GlobalLanguage = cultureInfo.Name;
                AppLanguageHelper.ApplyLanguage(_appSettings);
                _settingsService.SaveSettings(_appSettings);
            }
        }

        private void RecognitionHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (HotkeyGesture.IsModifierKey(key))
            {
                e.Handled = true;
                return;
            }

            if (!HotkeyGesture.TryCreate(Keyboard.Modifiers, key, out var gesture))
            {
                UiMessageBox.ShowWarning(LanguageManager.GetString("RecognitionHotkeyInvalid"));
                e.Handled = true;
                return;
            }

            if (gesture == null)
            {
                e.Handled = true;
                return;
            }

            _pendingRecognitionHotkey = gesture.ToConfigString();
            RecognitionHotkeyTextBox.Text = gesture.ToDisplayString();
            e.Handled = true;
        }

        private void SaveRecognitionHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (!_settingsManager.TryUpdateAndSave(
                    "SettingPageRecognitionHotkey",
                    settings =>
                    {
                        settings.ToggleRecognitionHotkey = _pendingRecognitionHotkey;
                        return true;
                    },
                    out var updatedSettings)
                || updatedSettings == null)
            {
                return;
            }

            _appSettings = updatedSettings;
            LoadRecognitionHotkeyFromSettings();
            UiMessageBox.ShowSuccess(LanguageManager.GetString("RecognitionHotkeySaved"));
        }

        private void ClearRecognitionHotkey_Click(object sender, RoutedEventArgs e)
        {
            _pendingRecognitionHotkey = string.Empty;
            RecognitionHotkeyTextBox.Text = _viewModel?.RecognitionHotkeyNotSetLabel ?? LanguageManager.GetString("RecognitionHotkeyNotSet");
        }

        private void LoadRecognitionHotkeyFromSettings()
        {
            _pendingRecognitionHotkey = _appSettings.ToggleRecognitionHotkey ?? string.Empty;
            RecognitionHotkeyTextBox.Text = HotkeyGesture.TryParse(_pendingRecognitionHotkey, out var gesture) && gesture != null
                ? gesture.ToDisplayString()
                : _viewModel?.RecognitionHotkeyNotSetLabel ?? LanguageManager.GetString("RecognitionHotkeyNotSet");
        }
    }
}
