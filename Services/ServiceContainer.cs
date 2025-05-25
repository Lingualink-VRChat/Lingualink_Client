using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace lingualink_client.Services
{
    /// <summary>
    /// 简单的依赖注入容器 - 提供服务注册和解析功能
    /// </summary>
    public static class ServiceContainer
    {
        private static readonly ConcurrentDictionary<Type, object> _services = new();
        private static readonly ConcurrentDictionary<Type, Func<object>> _factories = new();

        /// <summary>
        /// 注册单例服务
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <typeparam name="TImplementation">实现类型</typeparam>
        /// <param name="implementation">实现实例</param>
        public static void Register<TInterface, TImplementation>(TImplementation implementation)
            where TImplementation : class, TInterface
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            var interfaceType = typeof(TInterface);
            _services[interfaceType] = implementation;
            
            Debug.WriteLine($"ServiceContainer: Registered {typeof(TImplementation).Name} as {interfaceType.Name}");
        }

        /// <summary>
        /// 注册服务工厂
        /// </summary>
        /// <typeparam name="TInterface">接口类型</typeparam>
        /// <param name="factory">创建实例的工厂方法</param>
        public static void RegisterFactory<TInterface>(Func<TInterface> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var interfaceType = typeof(TInterface);
            _factories[interfaceType] = () => factory()!;
            
            Debug.WriteLine($"ServiceContainer: Registered factory for {interfaceType.Name}");
        }

        /// <summary>
        /// 解析服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例</returns>
        public static T Resolve<T>()
        {
            var serviceType = typeof(T);
            
            // 首先尝试从已注册的实例中获取
            if (_services.TryGetValue(serviceType, out var service))
            {
                Debug.WriteLine($"ServiceContainer: Resolved {serviceType.Name} from registered instance");
                return (T)service;
            }
            
            // 然后尝试从工厂中创建
            if (_factories.TryGetValue(serviceType, out var factory))
            {
                var instance = factory();
                Debug.WriteLine($"ServiceContainer: Created {serviceType.Name} from factory");
                return (T)instance;
            }
            
            throw new InvalidOperationException($"Service of type {serviceType.Name} is not registered");
        }

        /// <summary>
        /// 尝试解析服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="service">解析的服务实例</param>
        /// <returns>是否成功解析</returns>
        public static bool TryResolve<T>(out T? service)
        {
            try
            {
                service = Resolve<T>();
                return true;
            }
            catch
            {
                service = default;
                return false;
            }
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>是否已注册</returns>
        public static bool IsRegistered<T>()
        {
            var serviceType = typeof(T);
            return _services.ContainsKey(serviceType) || _factories.ContainsKey(serviceType);
        }

        /// <summary>
        /// 清除所有注册的服务
        /// </summary>
        public static void Clear()
        {
            Debug.WriteLine("ServiceContainer: Clearing all services");
            
            var serviceCount = _services.Count;
            var factoryCount = _factories.Count;
            
            _services.Clear();
            _factories.Clear();
            
            Debug.WriteLine($"ServiceContainer: Cleared {serviceCount} services and {factoryCount} factories");
        }

        /// <summary>
        /// 获取已注册服务的数量
        /// </summary>
        public static int RegisteredServiceCount => _services.Count + _factories.Count;
    }
} 