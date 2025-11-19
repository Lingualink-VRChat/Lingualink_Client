using System;
using lingualink_client.Models;

namespace lingualink_client.Services.Interfaces
{
    /// <summary>
    /// 高层设置管理接口：负责加载、更新并持久化 <see cref="AppSettings"/>，
    /// 同时在成功保存后广播 SettingsChangedEvent。
    /// </summary>
    public interface ISettingsManager
    {
        /// <summary>
        /// 加载当前的应用设置。
        /// </summary>
        /// <returns>从持久化存储加载的设置实例。</returns>
        AppSettings LoadSettings();

        /// <summary>
        /// 基于最新的设置执行更新和验证，如果成功则保存并广播设置变更事件。
        /// </summary>
        /// <param name="changeSource">变更来源描述，将透传到 SettingsChangedEvent.ChangeSource。</param>
        /// <param name="updateAndValidate">
        /// 负责基于传入的 <see cref="AppSettings"/> 实例应用更新并执行验证。
        /// 返回 true 表示验证通过并应保存，false 表示验证失败且不应保存。
        /// </param>
        /// <param name="updatedSettings">
        /// 如果返回 true，将输出最终保存到磁盘并通过事件广播的设置实例；否则为 null。
        /// </param>
        /// <returns>如果设置被成功更新并保存则返回 true，否则返回 false。</returns>
        bool TryUpdateAndSave(string changeSource, Func<AppSettings, bool> updateAndValidate, out AppSettings? updatedSettings);
    }
}

