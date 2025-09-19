# LinguaLink Client - 项目总结与开发者指南

## 1. 项目概述

**LinguaLink Client** 是一个基于 C# 和 WPF 构建的桌面应用程序，旨在提供实时语音识别和翻译功能。它深度集成了 VRChat，可以通过 OSC 协议将翻译结果直接发送到游戏聊天框中。

此客户端设计为一个功能全面、高度可配置的工具，其核心特性包括：
- **实时音频处理**: 集成了高效的语音活动检测 (VAD)。
- **多语言翻译**: 通过 LinguaLink 后端 API (v2.0) 实现多种语言的互译。
- **高效音频传输**: 默认使用 Opus 音频编码 (16kbps)，并结合音频增强技术，以最小的带宽占用实现最高的识别准确率。
- **模块化和可扩展性**: 采用现代化的 MVVM 架构，结合依赖注入和服务分层，易于维护和扩展。
- **现代化 UI**: 使用 WPF-UI 库构建，提供流畅的、符合 Fluent Design 的用户界面。
- **会话历史记录**: 借助 LiteDB 持久化所有成功会话，支持按会话/来源/时间筛选、复制与导出。
- **高级日志中心**: 新的日志面板提供等级和分类过滤、全文搜索、复制与导出等能力。

## 2. 核心技术栈

- **框架**: .NET 8.0
- **UI**: WPF (Windows Presentation Foundation)
- **UI 库**: [WPF-UI (Fluent for WPF)](https://github.com/lepoco/wpfui) - 用于实现现代化、流畅的界面。
- **MVVM 框架**: [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) - 用于实现模型-视图-视图模型 (MVVM) 设计模式。
- **音频处理**:
    - **NAudio**: 用于音频输入/输出 (I/O) 和设备管理。
    - **WebRtcVadSharp**: 用于高效的语音活动检测 (VAD)。
    - **Concentus**: 用于 Opus 音频编码，以实现高效的音频压缩。
- **网络与通信**:
    - **HttpClient**: 用于与后端 LinguaLink API v2.0 进行通信。
    - **OscCore**: 用于通过 OSC (Open Sound Control) 协议与 VRChat 进行通信。

## 3. 架构深度解析

项目采用了清晰的分层和模块化架构，遵循了 MVVM 设计模式和 SOLID 原则。

### 3.1. MVVM 模式 (Model-View-ViewModel)

- **Views (`/Views`)**: 负责界面的呈现。XAML 文件非常"轻"，几乎不包含业务逻辑。后台代码 (`.xaml.cs`) 主要用于处理纯粹的 UI 事件（如窗口加载、控件交互）并将它们连接到 ViewModel。
- **ViewModels (`/ViewModels`)**: 负责 UI 的状态和逻辑。这是应用的核心，包含了所有的数据绑定属性和命令。`CommunityToolkit.Mvvm` 的 `ObservableObject` 和 `RelayCommand` 被广泛使用以减少样板代码。
- **Models (`/Models`)**: 负责数据结构和业务实体，如 `AppSettings` (应用配置) 和 `NewApiResponse` (API 响应)。

### 3.2. 服务层 (`/Services`)

服务层封装了所有的业务逻辑、外部通信和核心功能，使 ViewModel 保持整洁。

- **依赖注入 (`ServiceContainer`)**: 项目使用一个简单的静态依赖注入容器 `ServiceContainer` 来管理服务的生命周期和解析。在应用启动时，`ServiceInitializer` 会注册所有核心服务。
- **事件聚合器 (`EventAggregator`)**: `IEventAggregator` 接口及其实现 `EventAggregator` 用于模块间的松耦合通信。这避免了组件之间的直接引用，使得系统更加灵活。例如，当翻译完成时，`AudioTranslationOrchestrator` 会发布一个 `TranslationCompletedEvent`，而 `TranslationResultViewModel` 会订阅并响应此事件以更新UI。
- **核心服务**:
    - `SettingsService`: 负责加载和保存 `app_settings.json` 文件。
    - `AudioService`: 封装了麦克风录音、VAD 处理、音频增强（峰值归一化、安静语音增强）和音频分段逻辑。
    - `LingualinkApiService`: 负责与后端 API v2.0 通信，包括音频编码 (Opus)、发送请求和解析响应。
    - `OscService`: 封装了向 VRChat 发送 OSC 消息的逻辑。
    - `LoggingManager`: 提供一个集中的、线程安全的日志记录系统。
- **协调器 (`Orchestrators`)**:
    - `AudioTranslationOrchestrator`: 这是一个关键类，它协调了从音频输入到最终 OSC 输出的完整流程。它监听 `AudioService` 的事件，调用 `LingualinkApiService` 进行翻译，并使用 `OscService` 发送结果。

### 3.3. 组件化 ViewModel 架构

这是一个重要的架构特点。主界面 (`IndexPage`) 并非由一个庞大的 ViewModel 控制，而是由一个容器 ViewModel (`IndexWindowViewModel`) 和多个独立的、可复用的组件 ViewModel 构成。

- **`IndexWindowViewModel`**: 作为容器，它持有其他组件 ViewModel 的实例，并负责协调它们之间的数据流。
- **组件 ViewModels (`/ViewModels/Components`)**:
    - `MainControlViewModel`: 控制核心工作流程（开始/停止），并显示状态文本。
    - `MicrophoneSelectionViewModel`: 管理麦克风列表和选择。
    - `TargetLanguageViewModel`: 管理目标语言的选择。
    - `TranslationResultViewModel`: 显示翻译结果和日志。
    - `LogViewModel`: 为独立的日志页面提供支持。
- **管理器 (`/ViewModels/Managers`)**:
    - 为了进一步分离关注点，`MicrophoneManager` 和 `TargetLanguageManager` 被引入，用于处理与UI相关的复杂状态逻辑（如麦克风刷新、可用语言列表动态更新等），使组件 ViewModel 更加轻量。

这种设计使得每个部分都高度内聚，易于独立测试和修改。

## 4. 关键工作流程

### 4.1. 应用启动流程

1.  `App.xaml.cs` 的 `OnStartup` 方法被调用。
2.  `ServiceInitializer.Initialize()` 注册所有单例服务（如 `ILoggingManager`, `IEventAggregator`）。
3.  `SettingsService` 加载用户设置，并应用全局语言。
4.  主窗口 `MainWindow` 被创建，`IndexWindowViewModel` 被实例化，进而创建所有组件 ViewModel。
5.  `IndexWindowViewModel` 从 `SettingsService` 加载配置，并初始化其管理的组件（如 `TargetLanguageManager`）。

### 4.2. 实时音频翻译流程

这是一个核心的、异步的、事件驱动的流程：

1.  **用户操作**: 用户在 `IndexPage` 上选择麦克风，然后点击 "开始监听" 按钮。
2.  **ViewModel (UI -> Logic)**:
    - `MainControlViewModel` 的 `ToggleWorkCommand` 被执行。
    - 它调用 `AudioTranslationOrchestrator.Start()`，并传入所选麦克风的设备索引。
3.  **音频服务 (录音与VAD)**:
    - `AudioService.Start()` 初始化 `NAudio` 的 `WaveInEvent` 和 `WebRtcVad`。
    - `OnVadDataAvailable` 事件处理器持续接收音频数据。
    - VAD 算法检测语音的开始和结束。`PostSpeechRecordingDuration` 确保捕捉到完整的语音尾音。
    - 当一个完整的语音片段形成后（满足最小时长且静音超时），`AudioService` 会对音频应用增强处理（`ProcessAndNormalizeAudio`），然后触发 `AudioSegmentReady` 事件，并附带音频数据。
4.  **协调器 (核心逻辑)**:
    - `AudioTranslationOrchestrator` 监听到 `AudioSegmentReady` 事件。
    - 它从 `AppSettings` 获取目标语言（或从模板中提取）。
    - 音频数据被传递给 `AudioEncoderService` 进行 Opus 编码，得到压缩后的字节数组。
    - `LingualinkApiService.ProcessAudioAsync()` 被调用，将编码后的音频数据发送到后端。
5.  **API 服务 (网络通信)**:
    - `LingualinkApiService` 发送 HTTP POST 请求到后端 `/process_audio` 端点。
    - 它异步等待服务器的 JSON 响应 (`NewApiResponse`)。
6.  **结果处理 (返回路径)**:
    - `AudioTranslationOrchestrator` 接收到 `ApiResult`。
    - 如果成功，它会根据用户的模板设置 (`UseCustomTemplate`) 格式化翻译文本。
    - 它通过 `IEventAggregator` 发布一个 `TranslationCompletedEvent`，其中包含原始文本和处理后的文本。
    - 如果 `EnableOsc` 为 `true`，它会调用 `OscService.SendChatboxMessageAsync()` 将处理后的文本发送到 VRChat。
7.  **ViewModel (Logic -> UI)**:
    - `TranslationResultViewModel` 订阅了 `TranslationCompletedEvent`。
    - 当事件被接收时，它会更新其 `OriginalText` 和 `ProcessedText` 属性，UI 会通过数据绑定自动刷新。
    - `MainControlViewModel` 也会更新 `StatusText` 以向用户反馈当前状态（如 "翻译成功"、"发送中..."）。

### 4.3. 设置更改流程

1.  用户在 `ServicePage` 或 `AccountPage` 等页面上修改设置。
2.  对应 ViewModel (`ServicePageViewModel`, `AccountPageViewModel`) 的属性通过双向绑定更新。
3.  用户点击 "保存" 按钮，触发 `SaveCommand`。
4.  ViewModel 验证输入，然后从 `SettingsService` 加载最新的 `AppSettings` 对象（以避免覆盖其他页面的更改）。
5.  ViewModel 将当前页面管理的设置更新到这个 `AppSettings` 对象中。
6.  `SettingsService.SaveSettings()` 将更新后的对象序列化为 JSON 并保存到磁盘。
7.  `SettingsChangedNotifier.RaiseSettingsChanged()` 被调用，这是一个全局静态事件。
8.  `IndexWindowViewModel` 和 `MainControlViewModel` 等关心设置变化的组件会监听到此事件，并重新加载配置以应用更改（例如，重新创建 `AudioTranslationOrchestrator` 以应用新的 API 密钥或 OSC 地址）。

## 5. 重要类及其职责

| 类别       | 类/接口                                    | 职责                                                               |
|------------|--------------------------------------------|--------------------------------------------------------------------|
| **Models** | `AppSettings.cs`                           | 定义所有用户可配置的设置项，是 `app_settings.json` 的C#映射。        |
|            | `Models.cs` (`NewApiResponse`, `ApiResult`) | 定义与后端 API v2.0 交互的数据模型和统一的 API 结果封装。          |
| **Services** | `IEventAggregator.cs` / `EventAggregator.cs` | 提供松耦合的发布/订阅事件总线。                                    |
|            | `ILingualinkApiService.cs` / `LingualinkApiService.cs` | 封装与后端 API v2.0 的所有 HTTP 通信，包括认证、编码和请求。   |
|            | `AudioService.cs`                          | 核心音频处理：录音、VAD、分段、音频增强。                          |
|            | `AudioTranslationOrchestrator.cs`          | 流程协调器，粘合音频输入、API翻译和OSC输出。                       |
|            | `SettingsService.cs`                       | 负责 `app_settings.json` 的读写操作。                              |
|            | `ServiceContainer.cs` / `ServiceInitializer.cs` | 实现简单的依赖注入和服务生命周期管理。                             |
| **ViewModels**| `IndexWindowViewModel.cs`                    | 作为组件容器，管理所有主界面上的子 ViewModel。                     |
|            | `MainControlViewModel.cs`                  | 控制核心工作流程（启停）、状态显示，并持有 `Orchestrator` 实例。 |
|            | `AccountPageViewModel.cs`                  | 管理账户和自定义服务器（URL、API Key）的设置。                       |
|            | `ServicePageViewModel.cs`                  | 管理服务相关的详细参数（VAD、OSC、音频处理等）。                   |
|            | `TargetLanguageManager.cs`                 | 封装目标语言选择的复杂UI逻辑，如动态更新可用语言列表。           |

## 6. 未来开发指南

### 6.1. 如何添加一个新的设置项

1.  **Model**: 在 `Models/AppSettings.cs` 中添加新的属性。
2.  **View**: 在相应的设置页面 XAML (如 `ServicePage.xaml`) 中添加 UI 控件（如 `Slider`, `CheckBox`）。
3.  **ViewModel**: 在对应的 ViewModel (如 `ServicePageViewModel.cs`) 中：
    -   添加一个与新属性同名的 `[ObservableProperty]`。
    -   在 `LoadSettingsFromModel` 方法中，从 `AppSettings` 加载值到 ViewModel 属性。
    -   在 `ValidateAndBuildSettings` 方法中，将 ViewModel 属性的值写回 `AppSettings` 对象。
    -   添加对应的本地化标签字符串到 `.resx` 文件和 ViewModel。
4.  **Service**: 如果新设置影响了某个服务（如 `AudioService`），请确保在服务的构造函数中接收并使用该值。

### 6.2. 如何添加一个新的UI页面

1.  **View**: 在 `Views/Pages/` 目录下创建一个新的 `Page` (e.g., `NewFeaturePage.xaml`)。
2.  **ViewModel**: 在 `ViewModels/` 目录下创建一个对应的 ViewModel (`NewFeaturePageViewModel.cs`)，继承自 `ViewModelBase`。
3.  **MainWindow**: 在 `MainWindow.xaml` 的 `ui:NavigationView.MenuItems` 中添加一个新的 `ui:NavigationViewItem`，并将其 `TargetPageType` 指向你的新页面。
4.  **Localization**: 为新的导航项在 `.resx` 文件中添加内容，并在 `MainWindowViewModel.cs` 中添加对应的属性绑定。

### 6.3. 开发最佳实践

- **保持 ViewModel 简洁**: 复杂的业务逻辑应移至服务层。ViewModel 的主要职责是管理UI状态和响应用户交互。
- **使用 `ServiceContainer`**: 需要使用服务时，通过 `ServiceContainer.Resolve<T>()` 获取。新的单例服务应在 `ServiceInitializer` 中注册。
- **使用 `EventAggregator`**: 当模块间需要通信但又不希望产生强引用时（如 ViewModel A 需要通知 ViewModel B 某事发生），请使用事件聚合器。定义一个新的事件类，然后发布和订阅它。
- **利用组件化**: 对于复杂界面，优先考虑将其拆分为多个子组件 ViewModel，而不是创建一个庞大的单体 ViewModel。`IndexPage` 是一个很好的例子。
- **本地化**: 所有面向用户的字符串都应通过 `LanguageManager.GetString("ResourceKey")` 获取，并添加到所有 `.resx` 文件中。

## 7. 结论

LinguaLink Client v3.0 是一个架构清晰、功能强大且易于扩展的应用程序。通过采用现代化的 MVVM 模式、服务分层和事件驱动设计，项目具备了良好的可维护性。未来的开发者可以基于此坚实的基础，轻松地添加新功能或进行修改。
