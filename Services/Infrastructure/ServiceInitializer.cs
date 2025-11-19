using lingualink_client.Services.Events;
using lingualink_client.Services.Interfaces;
using lingualink_client.Services.Managers;
using lingualink_client.ViewModels;
using lingualink_client.ViewModels.Managers;
using System.Diagnostics;

namespace lingualink_client.Services
{
    /// <summary>
    /// 服务初始化器 - 负责注册和初始化所有服务
    /// </summary>
    public static class ServiceInitializer
    {
        /// <summary>
        /// 初始化所有服务
        /// </summary>
        public static void Initialize()
        {
            Debug.WriteLine("ServiceInitializer: Starting service initialization");

            try
            {
                // 注册基础服务
                RegisterCoreServices();

                Debug.WriteLine("ServiceInitializer: Service initialization completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ServiceInitializer: Error during initialization: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 注册核心服务
        /// </summary>
        private static void RegisterCoreServices()
        {
            // 注册事件聚合器
            var eventAggregator = new EventAggregator();
            ServiceContainer.Register<IEventAggregator, EventAggregator>(eventAggregator);

            // 注册设置管理器（基于 SettingsService 封装加载/保存/事件逻辑）
            var settingsManager = new SettingsManager();
            ServiceContainer.Register<ISettingsManager, SettingsManager>(settingsManager);

            // 注册日志管理器
            var loggingManager = new LoggingManager();
            ServiceContainer.Register<ILoggingManager, LoggingManager>(loggingManager);

            // 注册对话历史管理器
            var conversationHistoryService = new ConversationHistoryService();
            ServiceContainer.Register<IConversationHistoryService, ConversationHistoryService>(conversationHistoryService);

            // 注册目标语言管理器
            var targetLanguageManager = new TargetLanguageManager();
            ServiceContainer.Register<ITargetLanguageManager, TargetLanguageManager>(targetLanguageManager);

            // 注册麦克风管理器
            var microphoneManager = new ViewModels.Managers.MicrophoneManager();
            ServiceContainer.Register<IMicrophoneManager, ViewModels.Managers.MicrophoneManager>(microphoneManager);

            // 注册全局共享状态ViewModel
            var sharedStateViewModel = new SharedStateViewModel();
            ServiceContainer.Register<SharedStateViewModel, SharedStateViewModel>(sharedStateViewModel);

            // 注册自动更新服务
            var updateService = new VelopackUpdateService();
            ServiceContainer.Register<IUpdateService, VelopackUpdateService>(updateService);

            // 注册新的API服务（延迟初始化，因为需要配置参数）
            // LingualinkApiService 将在需要时通过工厂方法创建

            Debug.WriteLine("ServiceInitializer: Core services registered");
        }

        /// <summary>
        /// 清理所有服务
        /// </summary>
        public static void Cleanup()
        {
            Debug.WriteLine("ServiceInitializer: Starting service cleanup");

            try
            {
                // 清理事件聚合器
                if (ServiceContainer.TryResolve<IEventAggregator>(out var eventAggregator) && eventAggregator != null)
                {
                    eventAggregator.Clear();
                }

                if (ServiceContainer.TryResolve<IConversationHistoryService>(out var conversationHistoryService) && conversationHistoryService != null)
                {
                    conversationHistoryService.Dispose();
                }

                // 清理服务容器
                ServiceContainer.Clear();

                Debug.WriteLine("ServiceInitializer: Service cleanup completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ServiceInitializer: Error during cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取服务状态信息
        /// </summary>
        /// <returns>服务状态描述</returns>
        public static string GetServiceStatus()
        {
            var registeredCount = ServiceContainer.RegisteredServiceCount;
            var eventAggregatorRegistered = ServiceContainer.IsRegistered<IEventAggregator>();
            var loggingManagerRegistered = ServiceContainer.IsRegistered<ILoggingManager>();
            var targetLanguageManagerRegistered = ServiceContainer.IsRegistered<ITargetLanguageManager>();
            var microphoneManagerRegistered = ServiceContainer.IsRegistered<IMicrophoneManager>();

            return $"Services: {registeredCount} registered, " +
                   $"EventAggregator: {(eventAggregatorRegistered ? "✓" : "✗")}, " +
                   $"LoggingManager: {(loggingManagerRegistered ? "✓" : "✗")}, " +
                   $"TargetLanguageManager: {(targetLanguageManagerRegistered ? "✓" : "✗")}, " +
                   $"MicrophoneManager: {(microphoneManagerRegistered ? "✓" : "✗")}";
        }
    }
} 

