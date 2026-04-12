using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using MessageBox = lingualink_client.Services.MessageBox;

namespace lingualink_client.ViewModels
{
    public partial class AccountPageViewModel
    {
        private void LoadSettingsFromModel(AppSettings settings)
        {
            _isLoadingSettings = true;
            try
            {
                _currentSettings = settings;
                UseCustomServer = settings.UseCustomServer;

                if (UseCustomServer)
                {
                    ServerUrl = string.IsNullOrWhiteSpace(settings.CustomServerUrl)
                        ? settings.ServerUrl
                        : settings.CustomServerUrl;
                    ApiKey = string.IsNullOrWhiteSpace(settings.CustomApiKey)
                        ? settings.ApiKey
                        : settings.CustomApiKey;
                }
                else
                {
                    ServerUrl = string.IsNullOrWhiteSpace(settings.OfficialServerUrl)
                        ? AppSettings.OfficialProductionServerUrl
                        : settings.OfficialServerUrl;
                    ApiKey = string.Empty;
                }
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void OnAccountPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == null || _isLoadingSettings)
            {
                return;
            }

            if (!IsAutoSaveProperty(e.PropertyName))
            {
                return;
            }

            _hasPendingChanges = true;

            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            _autoSaveTimer.Start();
        }

        private void AutoSaveTimerOnTick(object? sender, EventArgs e)
        {
            _autoSaveTimer.Stop();

            if (!_hasPendingChanges)
            {
                return;
            }

            _hasPendingChanges = false;
            SaveInternal(showConfirmation: false, changeSource: "AccountPageAutoSave");
        }

        private static bool IsAutoSaveProperty(string propertyName)
        {
            return propertyName == nameof(UseCustomServer)
                   || propertyName == nameof(ServerUrl)
                   || propertyName == nameof(ApiKey);
        }

        partial void OnUseCustomServerChanged(bool value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            if (value)
            {
                _currentSettings.OfficialServerUrl = ServerUrl;

                var customUrl = string.IsNullOrWhiteSpace(_currentSettings.CustomServerUrl)
                    ? _currentSettings.ServerUrl
                    : _currentSettings.CustomServerUrl;
                var customApiKey = string.IsNullOrWhiteSpace(_currentSettings.CustomApiKey)
                    ? _currentSettings.ApiKey
                    : _currentSettings.CustomApiKey;

                ServerUrl = customUrl;
                ApiKey = customApiKey;
            }
            else
            {
                _currentSettings.CustomServerUrl = ServerUrl;
                _currentSettings.CustomApiKey = ApiKey;

                if (string.IsNullOrWhiteSpace(_currentSettings.OfficialServerUrl))
                {
                    _currentSettings.OfficialServerUrl = AppSettings.OfficialProductionServerUrl;
                }

                ServerUrl = _currentSettings.OfficialServerUrl;
                ApiKey = string.Empty;
            }

            TestConnectionCommand.NotifyCanExecuteChanged();

            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            _hasPendingChanges = true;

            if (!value || (!string.IsNullOrWhiteSpace(ServerUrl) && Uri.TryCreate(ServerUrl, UriKind.Absolute, out _)))
            {
                SaveInternal(showConfirmation: false, changeSource: "AccountPageServerModeToggle");
            }
            else
            {
                _autoSaveTimer.Start();
            }
        }

        private bool UpdateSettingsFromView(AppSettings updatedSettings)
        {
            Debug.WriteLine("[AccountPageViewModel] ValidateAndBuildSettings() called");
            Debug.WriteLine($"[AccountPageViewModel] Loaded latest settings base - ServerUrl: '{updatedSettings.ServerUrl}'");

            if (UseCustomServer && (string.IsNullOrWhiteSpace(ServerUrl) || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out _)))
            {
                MessageBox.Show(
                    LanguageManager.GetString("ValidationServerUrlInvalid"),
                    LanguageManager.GetString("ValidationErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            updatedSettings.UseCustomServer = UseCustomServer;

            if (UseCustomServer)
            {
                Debug.WriteLine("[AccountPageViewModel] Using custom server - updating settings");
                Debug.WriteLine($"[AccountPageViewModel] ViewModel values - ServerUrl: '{ServerUrl}'");

                updatedSettings.CustomServerUrl = ServerUrl;
                updatedSettings.CustomApiKey = ApiKey?.Trim() ?? string.Empty;
                updatedSettings.ServerUrl = ServerUrl;
                updatedSettings.ApiKey = updatedSettings.CustomApiKey;
            }
            else
            {
                Debug.WriteLine("[AccountPageViewModel] Using official service");

                updatedSettings.OfficialServerUrl = string.IsNullOrWhiteSpace(updatedSettings.OfficialServerUrl)
                    ? AppSettings.OfficialProductionServerUrl
                    : updatedSettings.OfficialServerUrl;

                updatedSettings.ServerUrl = updatedSettings.OfficialServerUrl;
                updatedSettings.ApiKey = string.IsNullOrWhiteSpace(updatedSettings.CustomApiKey)
                    ? updatedSettings.ApiKey
                    : updatedSettings.CustomApiKey;
            }

            return true;
        }

        private void SaveInternal(bool showConfirmation, string changeSource)
        {
            Debug.WriteLine($"[AccountPageViewModel] SaveInternal() called - Source: {changeSource}, UseCustomServer: {UseCustomServer}");

            if (!_settingsManager.TryUpdateAndSave(changeSource, UpdateSettingsFromView, out var updatedSettings) || updatedSettings == null)
            {
                return;
            }

            _currentSettings = updatedSettings;
            _hasPendingChanges = false;

            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
            }

            Debug.WriteLine("[AccountPageViewModel] Settings saved, raising SettingsChanged event");

            if (showConfirmation)
            {
                MessageBox.Show(
                    LanguageManager.GetString("SettingsSavedSuccess"),
                    LanguageManager.GetString("SuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void Save()
        {
            SaveInternal(showConfirmation: true, changeSource: "AccountPage");
        }

        [RelayCommand(CanExecute = nameof(CanTestConnection))]
        private async Task TestConnectionAsync()
        {
            IsTestingConnection = true;
            OnPropertyChanged(nameof(ConnectionTestLabel));

            ILingualinkApiService? testApiService = null;
            bool success;
            var errorMessage = LanguageManager.GetString("AccountConnectionUnknownError");

            try
            {
                testApiService = LingualinkApiServiceFactory.CreateTestApiService(ServerUrl, ApiKey);
                success = await testApiService.ValidateConnectionAsync();
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
            }
            finally
            {
                testApiService?.Dispose();
                IsTestingConnection = false;
                OnPropertyChanged(nameof(ConnectionTestLabel));
            }

            if (success)
            {
                MessageBox.Show(
                    LanguageManager.GetString("AccountConnectionSuccess"),
                    LanguageManager.GetString("SuccessTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    string.Format(LanguageManager.GetString("AccountConnectionFailedFormat"), errorMessage),
                    LanguageManager.GetString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool CanTestConnection()
        {
            return !IsTestingConnection && !string.IsNullOrWhiteSpace(ServerUrl);
        }
    }
}
