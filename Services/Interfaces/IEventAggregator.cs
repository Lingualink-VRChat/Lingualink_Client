using System;

namespace lingualink_client.Services.Interfaces
{
    /// <summary>
    /// 事件聚合器接口 - 提供松耦合的事件通信机制
    /// </summary>
    public interface IEventAggregator
    {
        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        void Publish<T>(T eventData) where T : class;

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        void Subscribe<T>(Action<T> handler) where T : class;

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        void Unsubscribe<T>(Action<T> handler) where T : class;

        /// <summary>
        /// 清除所有订阅
        /// </summary>
        void Clear();
    }
} 