using System;
using System.Windows;
using lingualink_client.ViewModels.Components;
using lingualink_client.ViewModels.Managers;
using lingualink_client.Services;
using lingualink_client.Models;

namespace lingualink_client.ViewModels
{
    /// <summary>
    /// Index页面主ViewModel - 数据驱动模式的组件容器
    /// 负责管理各个组件ViewModels，确保单一数据源原则
    /// </summary>
    public partial class IndexWindowViewModel : ViewModelBase, IDisposable
    {
        // 组件ViewModels - 每个组件都是独立的数据源
        public MainControlViewModel MainControl { get; }
        public MicrophoneSelectionViewModel MicrophoneSelection { get; }
        public TargetLanguageViewModel TargetLanguage { get; }
        public TranslationResultViewModel TranslationResult { get; }

        // 服务和管理器
        private readonly SettingsService _settingsService;
        private AppSettings _appSettings;
        private readonly ITargetLanguageManager _targetLanguageManager;
        private readonly IMicrophoneManager _microphoneManager;

        public IndexWindowViewModel()
        {
            // 初始化服务/管理器
            _settingsService = new SettingsService();
            _targetLanguageManager = ServiceContainer.Resolve<ITargetLanguageManager>();
            _microphoneManager = ServiceContainer.Resolve<IMicrophoneManager>();

            // 初始化组件ViewModels
            MainControl = new MainControlViewModel();
            MicrophoneSelection = new MicrophoneSelectionViewModel();
            TargetLanguage = new TargetLanguageViewModel();
            TranslationResult = new TranslationResultViewModel();

            // 加载初始设置并应用到管理器
            _appSettings = _settingsService.LoadSettings();
            _targetLanguageManager.LoadFromSettings(_appSettings);
            _targetLanguageManager.UpdateEnabledState(_appSettings.UseCustomTemplate);
            _ = _microphoneManager.RefreshAsync(); // 初始麦克风刷新

            // 通过事件聚合器订阅全局设置变化
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Subscribe<ViewModels.Events.SettingsChangedEvent>(OnGlobalSettingsChanged);

            // 建立组件间的事件连接
            SetupComponentCommunication();
        }

        private void SetupComponentCommunication()
        {
            // 初始化管理器数据
            InitializeManagers();
            
            // 设置组件间的事件通信
            SetupTranslationResultUpdates();
        }

        private void SetupTranslationResultUpdates()
        {
            // 由于orchestrator在MainControl初始化时创建，我们需要延迟连接事件
            // 通过MainControl的事件来间接连接
            // 这里暂时保留为空，实际的事件连接在MainControl中处理
        }

        private void OnTranslationCompleted(object? sender, Services.Interfaces.TranslationResultEventArgs e)
        {
            // 更新翻译结果显示
            TranslationResult.UpdateTranslationResult(e.OriginalText ?? "", e.ProcessedText ?? "");
        }

        private void OnGlobalSettingsChanged(ViewModels.Events.SettingsChangedEvent e)
        {
            // 确保UI更新在UI线程上进行
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 重新加载最新设置
                _appSettings = _settingsService.LoadSettings();

                // The TargetLanguageManager needs to know about the UseCustomTemplate change
                // It will adjust its AreLanguagesEnabled property.
                _targetLanguageManager.UpdateEnabledState(_appSettings.UseCustomTemplate);

                // If not in template mode, ensure the TargetLanguageManager re-loads the manual languages
                // because the _appSettings.TargetLanguages might have been the source of truth.
                if (!_appSettings.UseCustomTemplate) {
                    _targetLanguageManager.LoadFromSettings(_appSettings);
                }

                // MainControlViewModel已经订阅了事件聚合器并重新加载自己的设置
                // 所以这里不需要显式调用MainControl，它会处理自己的内部状态
            });
        }

        private void InitializeManagers()
        {
            // 这个方法现在在构造函数中内联处理
            // 保留为空或删除
        }

        public void Dispose()
        {
            // 取消订阅事件聚合器事件
            var eventAggregator = ServiceContainer.Resolve<Services.Interfaces.IEventAggregator>();
            eventAggregator.Unsubscribe<ViewModels.Events.SettingsChangedEvent>(OnGlobalSettingsChanged);

            MainControl?.Dispose();
            MicrophoneSelection?.Dispose();
            TargetLanguage?.Dispose();
            TranslationResult?.Dispose();
        }
    }
} 