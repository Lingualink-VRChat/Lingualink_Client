using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using lingualink_client.Models;
using lingualink_client.Services;
using lingualink_client.ViewModels.Managers;

namespace lingualink_client.ViewModels.Components
{
    /// <summary>
    /// 麦克风选择ViewModel - 专门负责麦克风选择的UI逻辑
    /// </summary>
    public partial class MicrophoneSelectionViewModel : ViewModelBase
    {
        private readonly IMicrophoneManager _microphoneManager;

        public ObservableCollection<MMDeviceWrapper> Microphones => _microphoneManager.Microphones;
        
        public MMDeviceWrapper? SelectedMicrophone
        {
            get => _microphoneManager.SelectedMicrophone;
            set => _microphoneManager.SelectedMicrophone = value;
        }

        public bool IsRefreshing => _microphoneManager.IsRefreshing;
        public bool IsEnabled => _microphoneManager.IsEnabled;
        
        // UI绑定兼容属性
        public bool IsRefreshingMicrophones => _microphoneManager.IsRefreshing;
        public bool IsMicrophoneComboBoxEnabled => _microphoneManager.IsEnabled;

        // 本地化标签
        public string SelectMicrophoneLabel => LanguageManager.GetString("SelectMicrophone");
        public string RefreshLabel => LanguageManager.GetString("Refresh");
        public string RefreshingMicrophonesLabel => LanguageManager.GetString("RefreshingMicrophones");

        public MicrophoneSelectionViewModel()
        {
            _microphoneManager = ServiceContainer.Resolve<IMicrophoneManager>();

            // 订阅管理器事件
            _microphoneManager.MicrophoneChanged += OnMicrophoneChanged;
            _microphoneManager.PropertyChanged += OnManagerPropertyChanged;

            // 订阅语言变更事件
            LanguageManager.LanguageChanged += OnLanguageChanged;

            // 初始刷新麦克风列表
            _ = RefreshMicrophonesAsync();
        }

        private void OnMicrophoneChanged(object? sender, MMDeviceWrapper? microphone)
        {
            OnPropertyChanged(nameof(SelectedMicrophone));
        }

        private void OnManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IMicrophoneManager.IsRefreshing):
                    OnPropertyChanged(nameof(IsRefreshing));
                    OnPropertyChanged(nameof(IsRefreshingMicrophones));
                    RefreshMicrophonesCommand.NotifyCanExecuteChanged();
                    break;
                case nameof(IMicrophoneManager.IsEnabled):
                    OnPropertyChanged(nameof(IsEnabled));
                    OnPropertyChanged(nameof(IsMicrophoneComboBoxEnabled));
                    break;
                case nameof(IMicrophoneManager.Microphones):
                    OnPropertyChanged(nameof(Microphones));
                    break;
            }
        }

        private void OnLanguageChanged()
        {
            // 更新所有语言相关的标签
            OnPropertyChanged(nameof(SelectMicrophoneLabel));
            OnPropertyChanged(nameof(RefreshLabel));
            OnPropertyChanged(nameof(RefreshingMicrophonesLabel));
        }

        [RelayCommand(CanExecute = nameof(CanExecuteRefreshMicrophones))]
        private async Task RefreshMicrophonesAsync()
        {
            await _microphoneManager.RefreshAsync();
        }

        private bool CanExecuteRefreshMicrophones() => !_microphoneManager.IsRefreshing;

        public void Dispose()
        {
            // 取消订阅事件
            LanguageManager.LanguageChanged -= OnLanguageChanged;
            _microphoneManager.MicrophoneChanged -= OnMicrophoneChanged;
            _microphoneManager.PropertyChanged -= OnManagerPropertyChanged;
        }
    }
} 