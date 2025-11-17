using CommunityToolkit.Mvvm.ComponentModel;
using lingualink_client.Services;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Events;

namespace lingualink_client.ViewModels
{
    /// <summary>
    /// 全局共享状态ViewModel，作为多个页面间共享数据的单一数据源。
    /// </summary>
    public partial class SharedStateViewModel : ViewModelBase, System.IDisposable
    {
        private readonly IEventAggregator _eventAggregator;

        [ObservableProperty]
        private string _lastSentMessage = string.Empty;

        public SharedStateViewModel()
        {
            _eventAggregator = ServiceContainer.Resolve<IEventAggregator>();

            // 订阅所有可能产生VRChat输出的事件
            _eventAggregator.Subscribe<TranslationCompletedEvent>(OnTranslationCompleted);
        }

        private void OnTranslationCompleted(TranslationCompletedEvent e)
        {
            // 无论来源是语音还是文本，都更新最后发送的消息
            LastSentMessage = e.ProcessedText;
        }

        public void Dispose()
        {
            _eventAggregator.Unsubscribe<TranslationCompletedEvent>(OnTranslationCompleted);
        }
    }
}
