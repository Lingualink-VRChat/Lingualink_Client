using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using lingualink_client.Services.Interfaces;

namespace lingualink_client.Services.Events
{
    /// <summary>
    /// 事件聚合器实现 - 提供线程安全的事件发布订阅机制
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _subscriptions = new();
        private readonly object _lockObject = new object();
        private readonly Dispatcher? _dispatcher;

        public EventAggregator()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public void Publish<T>(T eventData) where T : class
        {
            if (eventData == null)
            {
                return;
            }

            var eventType = typeof(T);
            Debug.WriteLine($"EventAggregator: Publishing event of type {eventType.Name}");

            if (_subscriptions.TryGetValue(eventType, out var handlers))
            {
                List<Delegate> handlersCopy;
                lock (_lockObject)
                {
                    handlersCopy = new List<Delegate>(handlers);
                }

                void ExecuteHandlers()
                {
                    foreach (var handler in handlersCopy)
                    {
                        try
                        {
                            if (handler is Action<T> typedHandler)
                            {
                                typedHandler(eventData);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"EventAggregator: Error executing handler for {eventType.Name}: {ex.Message}");
                            // 继续执行其他处理器，不因为一个处理器出错而影响其他处理器
                        }
                    }

                    Debug.WriteLine($"EventAggregator: Event {eventType.Name} published to {handlersCopy.Count} handlers");
                }

                if (_dispatcher != null && !_dispatcher.CheckAccess())
                {
                    Debug.WriteLine($"EventAggregator: Dispatching event {eventType.Name} to UI thread");
                    _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ExecuteHandlers));
                }
                else
                {
                    ExecuteHandlers();
                }
            }
            else
            {
                Debug.WriteLine($"EventAggregator: No handlers found for event type {eventType.Name}");
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
            {
                return;
            }

            var eventType = typeof(T);
            Debug.WriteLine($"EventAggregator: Subscribing handler for event type {eventType.Name}");

            lock (_lockObject)
            {
                if (!_subscriptions.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _subscriptions[eventType] = handlers;
                }

                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                    Debug.WriteLine($"EventAggregator: Handler subscribed. Total handlers for {eventType.Name}: {handlers.Count}");
                }
                else
                {
                    Debug.WriteLine($"EventAggregator: Handler already subscribed for {eventType.Name}");
                }
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null)
            {
                return;
            }

            var eventType = typeof(T);
            Debug.WriteLine($"EventAggregator: Unsubscribing handler for event type {eventType.Name}");

            lock (_lockObject)
            {
                if (_subscriptions.TryGetValue(eventType, out var handlers))
                {
                    if (handlers.Remove(handler))
                    {
                        Debug.WriteLine($"EventAggregator: Handler unsubscribed. Remaining handlers for {eventType.Name}: {handlers.Count}");

                        // 如果没有处理器了，移除这个事件类型
                        if (handlers.Count == 0)
                        {
                            _subscriptions.TryRemove(eventType, out _);
                            Debug.WriteLine($"EventAggregator: No more handlers for {eventType.Name}, removed event type");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"EventAggregator: Handler not found for unsubscription from {eventType.Name}");
                    }
                }
                else
                {
                    Debug.WriteLine($"EventAggregator: No subscriptions found for event type {eventType.Name}");
                }
            }
        }

        public void Clear()
        {
            Debug.WriteLine("EventAggregator: Clearing all subscriptions");

            lock (_lockObject)
            {
                var eventTypeCount = _subscriptions.Count;
                _subscriptions.Clear();
                Debug.WriteLine($"EventAggregator: Cleared {eventTypeCount} event types");
            }
        }
    }
}
