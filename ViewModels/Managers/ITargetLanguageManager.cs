using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using lingualink_client.Models;

namespace lingualink_client.ViewModels.Managers
{
    /// <summary>
    /// 目标语言管理器接口 - 负责目标语言的管理和配置
    /// </summary>
    public interface ITargetLanguageManager : INotifyPropertyChanged
    {
        /// <summary>
        /// 目标语言项集合
        /// </summary>
        ObservableCollection<SelectableTargetLanguageViewModel> LanguageItems { get; }

        /// <summary>
        /// 目标语言是否启用（模板模式下会禁用）
        /// </summary>
        bool AreLanguagesEnabled { get; set; }

        /// <summary>
        /// 最大语言数量限制
        /// </summary>
        int MaxLanguageCount { get; }

        /// <summary>
        /// 当前语言数量
        /// </summary>
        int CurrentLanguageCount { get; }

        /// <summary>
        /// 是否可以添加更多语言
        /// </summary>
        bool CanAddLanguage { get; }

        /// <summary>
        /// 从设置加载目标语言
        /// </summary>
        /// <param name="settings">应用设置</param>
        void LoadFromSettings(AppSettings settings);

        /// <summary>
        /// 添加新的目标语言
        /// </summary>
        void AddLanguage();

        /// <summary>
        /// 移除指定的目标语言
        /// </summary>
        /// <param name="item">要移除的语言项</param>
        void RemoveLanguage(SelectableTargetLanguageViewModel item);

        /// <summary>
        /// 获取用于请求的目标语言字符串
        /// </summary>
        /// <param name="settings">应用设置</param>
        /// <returns>目标语言CSV字符串</returns>
        string GetTargetLanguagesForRequest(AppSettings settings);

        /// <summary>
        /// 更新语言启用状态（基于模板设置）
        /// </summary>
        /// <param name="useCustomTemplate">是否使用自定义模板</param>
        void UpdateEnabledState(bool useCustomTemplate);

        /// <summary>
        /// 更新语言项属性和可用语言
        /// </summary>
        void UpdateItemPropertiesAndAvailableLanguages();

        /// <summary>
        /// Moves the specified language item up in the list.
        /// </summary>
        /// <param name="item">The item to move up.</param>
        void MoveLanguageUp(SelectableTargetLanguageViewModel item);

        /// <summary>
        /// Moves the specified language item down in the list.
        /// </summary>
        /// <param name="item">The item to move down.</param>
        void MoveLanguageDown(SelectableTargetLanguageViewModel item);

        /// <summary>
        /// 语言配置变更事件
        /// </summary>
        event EventHandler? LanguagesChanged;

        /// <summary>
        /// 语言启用状态变更事件
        /// </summary>
        event EventHandler<bool>? EnabledStateChanged;
    }
} 