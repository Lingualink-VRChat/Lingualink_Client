using System;
using lingualink_client.Models;
using lingualink_client.Services.Events;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.Services
{
    /// <summary>
    /// SettingsManager 封装了常见的设置加载、验证、保存和事件广播流程，
    /// 用于减少各个 ViewModel 中重复的样板代码。
    /// </summary>
    public class SettingsManager : ISettingsManager
    {
        private readonly SettingsService _settingsService;
        private readonly IEventAggregator? _eventAggregator;

        public SettingsManager()
        {
            _settingsService = new SettingsService();

            // 设计时或早期启动阶段可能尚未注册事件聚合器，这里采用 TryResolve 以避免抛异常。
            if (ServiceContainer.TryResolve<IEventAggregator>(out var aggregator) && aggregator != null)
            {
                _eventAggregator = aggregator;
            }
        }

        public AppSettings LoadSettings()
        {
            return _settingsService.LoadSettings();
        }

        public bool TryUpdateAndSave(string changeSource, Func<AppSettings, bool> updateAndValidate, out AppSettings? updatedSettings)
        {
            if (updateAndValidate == null)
            {
                throw new ArgumentNullException(nameof(updateAndValidate));
            }

            var settings = _settingsService.LoadSettings();

            var isValid = updateAndValidate(settings);
            if (!isValid)
            {
                updatedSettings = null;
                return false;
            }

            // 确保当前界面语言一并持久化，避免语言切换相关的状态漂移。
            AppLanguageHelper.CaptureCurrentLanguage(settings);

            _settingsService.SaveSettings(settings);

            // 通过事件聚合器通知全局设置变更（如果可用）。
            _eventAggregator?.Publish(new SettingsChangedEvent
            {
                Settings = settings,
                ChangeSource = changeSource
            });

            updatedSettings = settings;
            return true;
        }
    }
}

