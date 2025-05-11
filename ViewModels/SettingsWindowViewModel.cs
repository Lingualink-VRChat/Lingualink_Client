using System.Globalization;
using System.Windows;
using lingualink_client.Models;

namespace lingualink_client.ViewModels
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private AppSettings _currentSettings; // To hold the initial settings passed in

        private string _targetLanguages;
        public string TargetLanguages
        {
            get => _targetLanguages;
            set => SetProperty(ref _targetLanguages, value);
        }

        private string _serverUrl;
        public string ServerUrl
        {
            get => _serverUrl;
            set => SetProperty(ref _serverUrl, value);
        }

        private double _silenceThresholdSeconds;
        public double SilenceThresholdSeconds
        {
            get => _silenceThresholdSeconds;
            set => SetProperty(ref _silenceThresholdSeconds, value);
        }

        private double _minVoiceDurationSeconds;
        public double MinVoiceDurationSeconds
        {
            get => _minVoiceDurationSeconds;
            set => SetProperty(ref _minVoiceDurationSeconds, value);
        }

        private double _maxVoiceDurationSeconds;
        public double MaxVoiceDurationSeconds
        {
            get => _maxVoiceDurationSeconds;
            set => SetProperty(ref _maxVoiceDurationSeconds, value);
        }

        // This property will hold the successfully validated and saved settings
        public AppSettings? SavedAppSettings { get; private set; }

        public SettingsWindowViewModel(AppSettings currentSettings)
        {
            _currentSettings = currentSettings; // Keep a reference if needed, or just copy values
            LoadSettingsFromModel(currentSettings);
        }

        private void LoadSettingsFromModel(AppSettings settings)
        {
            TargetLanguages = settings.TargetLanguages;
            ServerUrl = settings.ServerUrl;
            SilenceThresholdSeconds = settings.SilenceThresholdSeconds;
            MinVoiceDurationSeconds = settings.MinVoiceDurationSeconds;
            MaxVoiceDurationSeconds = settings.MaxVoiceDurationSeconds;
        }

        public bool TrySaveChanges()
        {
            if (!ValidateAllSettings())
            {
                return false; // Validation failed, MessageBoxes shown by ValidateAllSettings
            }

            // Validation passed, create the new AppSettings object
            SavedAppSettings = new AppSettings
            {
                TargetLanguages = this.TargetLanguages,
                ServerUrl = this.ServerUrl,
                SilenceThresholdSeconds = this.SilenceThresholdSeconds,
                MinVoiceDurationSeconds = this.MinVoiceDurationSeconds,
                MaxVoiceDurationSeconds = this.MaxVoiceDurationSeconds
            };
            return true;
        }

        private bool ValidateAllSettings()
        {
            if (string.IsNullOrWhiteSpace(TargetLanguages))
            {
                MessageBox.Show("目标翻译语言不能为空。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // In a fuller MVVM, you might use a validation summary or adorners
                return false;
            }
            if (string.IsNullOrWhiteSpace(ServerUrl) || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
            {
                MessageBox.Show("服务器URL无效。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // For numeric values, we assume the UI binding (or a converter later) handles non-numeric input.
            // Here we just check the logic.
            if (SilenceThresholdSeconds <= 0)
            {
                MessageBox.Show("静音检测阈值必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (MinVoiceDurationSeconds <= 0)
            {
                MessageBox.Show("最小语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (MaxVoiceDurationSeconds <= 0)
            {
                MessageBox.Show("最大语音时长必须是正数。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (MinVoiceDurationSeconds >= MaxVoiceDurationSeconds)
            {
                MessageBox.Show("最小语音时长必须小于最大语音时长。", "验证错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }
    }
}