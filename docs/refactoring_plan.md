# 代码重构计划 - 降低耦合度优化

## 🎯 重构目标

1. **单一职责原则** - 每个类只负责一个明确的功能
2. **依赖注入** - 通过接口解耦，提高可测试性
3. **事件驱动** - 使用事件/消息机制减少直接依赖
4. **可复用性** - 提取通用组件，减少重复代码

## 📊 当前问题分析

### IndexWindowViewModel (658行) - 职责过多
- ✗ 音频处理流程管理
- ✗ 翻译服务调用
- ✗ OSC消息发送
- ✗ 日志管理
- ✗ 目标语言管理
- ✗ 麦克风管理
- ✗ 设置管理
- ✗ UI状态管理

## 🏗️ 重构方案

### 1. 创建专门的管理器和服务

#### 1.1 日志管理器 (ILoggingManager)
```csharp
// Services/ILoggingManager.cs
public interface ILoggingManager
{
    ObservableCollection<string> LogMessages { get; }
    string FormattedLogMessages { get; }
    void AddMessage(string message);
    void ClearMessages();
    event EventHandler<string> MessageAdded;
}

// Services/LoggingManager.cs
public class LoggingManager : ILoggingManager
{
    // 实现日志管理逻辑
}
```

#### 1.2 目标语言管理器 (ITargetLanguageManager)
```csharp
// ViewModels/Managers/ITargetLanguageManager.cs
public interface ITargetLanguageManager
{
    ObservableCollection<SelectableTargetLanguageViewModel> LanguageItems { get; }
    bool AreLanguagesEnabled { get; set; }
    void LoadFromSettings(AppSettings settings);
    void AddLanguage();
    void RemoveLanguage(SelectableTargetLanguageViewModel item);
    string GetTargetLanguagesForRequest(AppSettings settings);
    event EventHandler LanguagesChanged;
}
```

#### 1.3 音频翻译协调器 (IAudioTranslationOrchestrator)
```csharp
// Services/IAudioTranslationOrchestrator.cs
public interface IAudioTranslationOrchestrator
{
    bool IsWorking { get; }
    void Start(int microphoneIndex);
    void Stop();
    event EventHandler<string> StatusUpdated;
    event EventHandler<TranslationResultEventArgs> TranslationCompleted;
}
```

#### 1.4 麦克风管理器 (IMicrophoneManager)
```csharp
// ViewModels/Managers/IMicrophoneManager.cs
public interface IMicrophoneManager
{
    ObservableCollection<MMDeviceWrapper> Microphones { get; }
    MMDeviceWrapper? SelectedMicrophone { get; set; }
    bool IsRefreshing { get; }
    bool IsEnabled { get; set; }
    Task RefreshAsync();
    event EventHandler<MMDeviceWrapper?> MicrophoneChanged;
}
```

### 2. 拆分ViewModel

#### 2.1 主控制ViewModel (MainControlViewModel)
```csharp
// ViewModels/MainControlViewModel.cs
public partial class MainControlViewModel : ViewModelBase
{
    // 只负责协调各个管理器
    // 处理开始/停止工作的主要逻辑
    // 状态显示
}
```

#### 2.2 目标语言ViewModel (TargetLanguageViewModel)
```csharp
// ViewModels/TargetLanguageViewModel.cs
public partial class TargetLanguageViewModel : ViewModelBase
{
    // 专门负责目标语言的UI逻辑
    // 使用ITargetLanguageManager
}
```

#### 2.3 麦克风选择ViewModel (MicrophoneSelectionViewModel)
```csharp
// ViewModels/MicrophoneSelectionViewModel.cs
public partial class MicrophoneSelectionViewModel : ViewModelBase
{
    // 专门负责麦克风选择的UI逻辑
    // 使用IMicrophoneManager
}
```

#### 2.4 翻译结果ViewModel (TranslationResultViewModel)
```csharp
// ViewModels/TranslationResultViewModel.cs
public partial class TranslationResultViewModel : ViewModelBase
{
    // 专门负责翻译结果显示
    // 监听翻译完成事件
}
```

### 3. 事件聚合器 (Event Aggregator)

```csharp
// Services/IEventAggregator.cs
public interface IEventAggregator
{
    void Publish<T>(T eventData) where T : class;
    void Subscribe<T>(Action<T> handler) where T : class;
    void Unsubscribe<T>(Action<T> handler) where T : class;
}

// 事件定义
public class WorkStartedEvent { }
public class WorkStoppedEvent { }
public class TranslationCompletedEvent 
{
    public string OriginalText { get; set; }
    public string ProcessedText { get; set; }
}
public class SettingsChangedEvent { }
```

### 4. 依赖注入容器

```csharp
// Services/ServiceContainer.cs
public static class ServiceContainer
{
    private static readonly Dictionary<Type, object> _services = new();
    
    public static void Register<TInterface, TImplementation>(TImplementation implementation)
        where TImplementation : class, TInterface
    {
        _services[typeof(TInterface)] = implementation;
    }
    
    public static T Resolve<T>()
    {
        return (T)_services[typeof(T)];
    }
}
```

## 📁 新的文件结构

```
ViewModels/
├── Managers/
│   ├── ITargetLanguageManager.cs
│   ├── TargetLanguageManager.cs
│   ├── IMicrophoneManager.cs
│   └── MicrophoneManager.cs
├── Components/
│   ├── MainControlViewModel.cs
│   ├── TargetLanguageViewModel.cs
│   ├── MicrophoneSelectionViewModel.cs
│   └── TranslationResultViewModel.cs
├── Events/
│   ├── WorkflowEvents.cs
│   └── TranslationEvents.cs
└── IndexWindowViewModel.cs (重构后，主要作为容器)

Services/
├── Interfaces/
│   ├── ILoggingManager.cs
│   ├── IAudioTranslationOrchestrator.cs
│   └── IEventAggregator.cs
├── Managers/
│   ├── LoggingManager.cs
│   └── AudioTranslationOrchestrator.cs
├── Events/
│   └── EventAggregator.cs
└── ServiceContainer.cs
```

## 🔄 重构步骤

### ✅ 阶段1: 基础设施 (已完成)
1. ✅ 创建事件聚合器
2. ✅ 创建依赖注入容器
3. ✅ 创建日志管理器接口和实现

### ✅ 阶段2: 管理器提取 (已完成)
1. ✅ 提取目标语言管理器
2. ✅ 提取麦克风管理器
3. ✅ 提取音频翻译协调器

### ✅ 阶段3: ViewModel拆分 (已完成)
1. ✅ 创建音频翻译协调器
2. ✅ 创建组件ViewModels
3. ⏳ 重构IndexWindowViewModel为容器 (下一步)
4. ⏳ 更新UI绑定

### 📋 阶段4: 测试和优化
1. 单元测试各个组件
2. 集成测试
3. 性能优化

## 🎯 预期收益

1. **可维护性** ⬆️ - 每个类职责明确，易于理解和修改
2. **可测试性** ⬆️ - 通过接口可以轻松进行单元测试
3. **可扩展性** ⬆️ - 新功能可以独立开发，不影响现有代码
4. **代码复用** ⬆️ - 管理器可以在多个地方使用
5. **耦合度** ⬇️ - 组件间通过接口和事件通信
6. **代码量** ⬇️ - 消除重复代码

## ⚠️ 注意事项

1. **渐进式重构** - 分阶段进行，确保每个阶段都能正常工作
2. **向后兼容** - 保持现有功能不变
3. **测试覆盖** - 重构过程中保持测试覆盖率
4. **文档更新** - 及时更新相关文档 