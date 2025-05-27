这个 `lingualink_client` 项目是一个基于 WPF (Windows Presentation Foundation) 的桌面应用程序，旨在提供实时语音翻译功能，并与 VRChat 等应用程序进行 OSC (Open Sound Control) 协议集成。

项目的核心设计理念是 **数据驱动 (Data-Driven)**，这意味着用户界面 (UI) 的状态和行为主要由底层数据模型 (Models) 和视图模型 (ViewModels) 驱动，通过数据绑定和事件通知实现松耦合。

---

### 项目详细介绍

#### 1. 核心功能

*   **实时语音识别与翻译**: 捕获麦克风音频，利用语音活动检测 (VAD) 智能识别语音片段，并将其发送到 LinguaLink Server 后端进行翻译。
*   **VRChat OSC 集成**: 将翻译后的文本通过 OSC 协议发送到 VRChat 的聊天框，实现游戏内的实时交流。
*   **多语言支持**: 支持多种目标翻译语言（如英文、日文、中文、韩文、法文、德文、西班牙文、俄文、意大利文），并提供多语言用户界面（中文和英文）。
*   **Opus 音频编码**: 内置 Opus 编码器支持，可将 PCM 音频压缩为 Opus 格式，显著减少网络带宽使用（通常可节省 60-80% 的带宽）。
*   **可配置的消息模板**: 允许用户自定义翻译结果的显示和发送格式，支持使用占位符（如 `{英文}`, `{日文}`）。
*   **丰富的设置选项**: 可配置服务器 URL、API 密钥、VAD 参数（静音阈值、语音时长）、OSC 连接参数（IP、端口、发送方式）、音频编码参数（Opus 比特率、复杂度）等。
*   **实时日志与状态显示**: 提供详细的运行日志和当前工作状态，方便用户监控和排查问题。

#### 2. 架构设计与数据驱动体现

项目采用 **MVVM (Model-View-ViewModel)** 架构模式，并利用 `CommunityToolkit.Mvvm` 库提供的源生成器功能，极大地强化了数据驱动的特性。

**a. 数据层 (Models)**
*   **`AppSettings.cs`**: 应用程序的全局配置，包括服务器URL、API密钥、VAD参数、OSC设置、消息模板设置等。所有这些配置项都是可序列化的数据，直接驱动应用的运行时行为。
*   **`Models.cs`**:
    *   `MMDeviceWrapper`: 对 NAudio 麦克风设备的抽象，作为 UI 绑定数据的载体。
    *   `ServerResponse`, `TranslationData`, `ErrorDetails`: 定义了后端 API 返回的数据结构。`TranslationData` 特别设计支持动态语言字段，以适应不同的翻译结果。
    *   `MessageTemplate`, `TemplateProcessor`: 定义了消息模板的数据结构和处理逻辑。这些都是纯粹的数据和数据处理方法，与 UI 无关。
*   **数据驱动体现**: `AppSettings` 是整个应用状态的核心数据源。UI 控件通过绑定到 ViewModel 的属性来间接操作这些数据，而服务的行为则直接由这些数据决定。

**b. 视图模型层 (ViewModels)**
*   **`ViewModelBase.cs`**: 继承自 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`，为所有 ViewModel 提供 `OnPropertyChanged` 和 `SetProperty` 等通知机制。通过 `[ObservableProperty]` 属性，可以直接将字段转换为属性，并在值变化时自动触发 `PropertyChanged` 事件，从而 **驱动 UI 自动更新**。
*   **组件式 ViewModel**:
    *   **`IndexWindowViewModel.cs`**: 作为主页面 (`IndexPage`) 的 DataContext，它不直接包含所有逻辑，而是 **聚合 (Compose)** 了多个子组件的 ViewModel（`MainControlViewModel`, `MicrophoneSelectionViewModel`, `TargetLanguageViewModel`, `TranslationResultViewModel`）。这意味着每个 UI 组件都有其独立的 ViewModel 来管理其数据和行为，实现了更细粒度的 **数据驱动** 和职责分离。
    *   **`MainControlViewModel.cs`**: 管理主页面的启动/停止按钮状态 (`WorkButtonContent`) 和全局状态文本 (`StatusText`)。这些都是 `[ObservableProperty]`，UI 自动绑定并响应其变化。
    *   **`MicrophoneSelectionViewModel.cs`**: 暴露 `ObservableCollection<MMDeviceWrapper> Microphones` 和 `MMDeviceWrapper? SelectedMicrophone`。UI 直接绑定到这些可观察数据集合和属性。`IsRefreshing` 属性也直接驱动 UI 元素的可见性和启用状态。
    *   **`TargetLanguageViewModel.cs`**: 管理 `ObservableCollection<SelectableTargetLanguageViewModel> LanguageItems`，每个 `SelectableTargetLanguageViewModel` 又管理自己的 `SelectedDisplayLanguage`。`AreLanguagesEnabled` 属性直接控制 UI 元素的启用状态。
    *   **`TranslationResultViewModel.cs`**: 暴露 `OriginalText` 和 `ProcessedText` (`[ObservableProperty]`)，直接显示翻译结果。还通过共享的 `ILoggingManager` 提供了日志数据的绑定。
    *   **`MessageTemplatePageViewModel.cs`**, **`ServicePageViewModel.cs`**, **`SettingPageViewModel.cs`**: 这些 ViewModel 负责管理各自页面上的配置数据。它们内部的属性也大多是 `[ObservableProperty]`，通过双向绑定或单向绑定直接与 UI 同步。
*   **命令绑定**: 使用 `CommunityToolkit.Mvvm` 的 `[RelayCommand]` 属性，将 UI 控件的命令（如按钮点击）直接绑定到 ViewModel 中的方法。这使得 ViewModel 能够响应用户操作，而无需 View 的代码了解 ViewModel 的具体实现。

**c. 服务层 (Services & Managers)**
*   **`ServiceContainer.cs`**: 一个轻量级的依赖注入 (DI) 容器。它负责服务的注册和解析，确保各个组件获得其所需的依赖，而无需硬编码创建。这实现了服务的松耦合和可测试性，是 **数据驱动** 架构中模块化和可维护性的关键。
*   **`ServiceInitializer.cs`**: 在应用程序启动时统一初始化和注册所有核心服务。
*   **`EventAggregator.cs`**: 实现了 `IEventAggregator` 接口，提供发布/订阅机制，用于 ViewModel 之间或 ViewModel 与 Service 之间的 **解耦通信**。例如，`AudioTranslationOrchestrator` 完成翻译后，会发布一个 `TranslationCompletedEvent` 数据事件，`TranslationResultViewModel` 订阅此事件并更新其数据属性，而无需两者直接引用。
*   **`LoggingManager.cs`**: 实现了 `ILoggingManager`，提供集中式日志记录功能。`LogMessages` 是 `ObservableCollection`，其变化会自动通知 UI。
*   **`SettingsService.cs`**: 专门负责 `AppSettings` 对象的持久化（加载和保存到 JSON 文件）。
*   **`SettingsChangedNotifier.cs`**: 一个静态事件，当设置被保存时触发，通知所有订阅者（如 `MainControlViewModel` 或 `IndexWindowViewModel`）重新加载配置，进而 **驱动应用的整体状态更新**。
*   **`LanguageManager.cs`**: 管理应用程序的界面语言，提供字符串本地化功能。`LanguageChanged` 事件在语言切换时触发，所有 ViewModel 订阅此事件来刷新其本地化文本属性。
*   **核心业务逻辑服务**:
    *   **`AudioService.cs`**: 负责麦克风音频捕获和 WebRTC VAD 语音活动检测。它将处理好的音频片段作为数据事件 (`AudioSegmentEventArgs`) 发布。
    *   **`AudioEncoderService.cs`**: 新增的音频编码服务，使用 Concentus 库实现 Opus 编码。支持将 PCM 音频数据压缩为 Opus 格式，可配置比特率、复杂度等参数，并提供压缩比率计算功能。
    *   **`TranslationService.cs`**: 封装了与 LinguaLink Server 后端通信的 HTTP 请求逻辑，支持自动选择音频格式（Opus 或 WAV）并设置相应的 Content-Type，接收并解析翻译响应数据。
    *   **`OscService.cs`**: 封装了通过 UDP 发送 OSC 消息到 VRChat 的逻辑。
    *   **`AudioTranslationOrchestrator.cs`**: 实现了 `IAudioTranslationOrchestrator`，作为整个音频-翻译-OSC 工作流的协调器。它订阅 `AudioService` 的音频片段事件，调用 `TranslationService`，然后调用 `OscService`。它将翻译结果和 OSC 发送状态作为数据事件 (`TranslationResultEventArgs`, `OscMessageEventArgs`) 发布。
*   **`ViewModels/Managers`**: 这一层级的管理器（如 `IMicrophoneManager`, `ITargetLanguageManager`）通常是为了将底层服务返回的原始数据转换为更适合 UI 绑定的可观察数据结构（如 `ObservableCollection`），并处理一些与 UI 相关的状态（如 `IsRefreshing`）。

#### 3. 数据流示例 (以一次语音翻译为例)

1.  **用户操作 (View)**: 用户在 `IndexPage` 点击 "开始工作" 按钮。
2.  **命令触发 (ViewModel)**: `ToggleWorkCommand` (在 `MainControlViewModel` 中) 被执行。
3.  **服务调用 (ViewModel -> Service)**: `MainControlViewModel` 调用 `IAudioTranslationOrchestrator.Start()`。
4.  **数据捕获与处理 (Service)**:
    *   `AudioTranslationOrchestrator` 调用 `AudioService.Start()`，开始从麦克风捕获音频。
    *   `AudioService` 进行 VAD 处理，当检测到完整的语音片段时，会触发 `AudioSegmentReady` 事件，并将 `AudioData` 作为事件参数 (`AudioSegmentEventArgs`)。
5.  **数据转换与发送 (Service)**:
    *   `AudioTranslationOrchestrator` 监听 `AudioSegmentReady` 事件。
    *   它将 `AudioData` 和 `_appSettings.TargetLanguages` (或从模板解析的语言) 等数据传递给 `TranslationService.TranslateAudioSegmentAsync()`。
    *   `TranslationService` 将音频数据和目标语言发送到后端，并接收 `ServerResponse` (包含 `TranslationData`)。
6.  **结果处理与模板应用 (Service)**:
    *   `AudioTranslationOrchestrator` 收到翻译结果 `TranslationData`。
    *   如果 `_appSettings.UseCustomTemplate` 为真，它会使用 `TemplateProcessor.ProcessTemplateWithValidation()` 将 `TranslationData` 应用到用户定义的模板，生成 `ProcessedText`。
7.  **数据事件发布 (Service -> EventAggregator)**:
    *   `AudioTranslationOrchestrator` 触发其 `TranslationCompleted` 事件，将 `OriginalText`, `ProcessedText` 等数据封装到 `TranslationResultEventArgs` 中。
    *   `MainControlViewModel` 接收到此事件后，将其转换为 `ViewModels.Events.TranslationCompletedEvent` 数据事件，并通过 `IEventAggregator.Publish()` 发布出去。
8.  **UI 数据更新 (EventAggregator -> ViewModel -> View)**:
    *   `TranslationResultViewModel` 订阅 `TranslationCompletedEvent`。
    *   当收到事件时，它会更新其 `OriginalText` 和 `ProcessedText` (`[ObservableProperty]`)。
    *   由于 UI (`IndexPage.xaml`) 绑定了这些属性，它们的更改会自动反映在界面上，用户看到翻译结果。
9.  **OSC 数据发送 (Service)**:
    *   如果 OSC 启用且 `ProcessedText` 有效，`AudioTranslationOrchestrator` 会调用 `OscService.SendChatboxMessageAsync()`，将 `ProcessedText` 作为数据发送到 VRChat。

#### 4. 技术栈

*   **框架**: .NET 8.0, WPF
*   **UI 库**: WPF-UI (Modern Fluent Design)
*   **MVVM 库**: CommunityToolkit.Mvvm (用于 `ObservableProperty` 和 `RelayCommand` 的源生成)
*   **音频处理**: NAudio (麦克风捕获, WAV 写入)
*   **音频编码**: Concentus (纯 C# Opus 编码器实现)
*   **语音活动检测**: WebRtcVadSharp (集成 Google WebRTC VAD 算法)
*   **OSC 通信**: OscCore (用于与 VRChat 交互)
*   **JSON 序列化**: System.Text.Json
*   **依赖注入**: 自定义的 `ServiceContainer` (简单实现)
*   **本地化**: .NET Resource Files (`.resx`)

#### 5. 优点

*   **高内聚低耦合**: 通过 MVVM 模式、依赖注入和服务容器、事件聚合器等手段，实现了各层和各组件之间的职责分离和松散耦合，易于开发、测试和维护。
*   **可扩展性**: 新的功能或翻译服务可以很容易地通过添加新的服务和 ViewModel 来集成，而不会影响现有代码。
*   **可测试性**: 业务逻辑被封装在 Service 层，可以独立于 UI 进行单元测试。
*   **响应式 UI**: 数据驱动的特性确保了 UI 能够实时响应底层数据的变化，提供流畅的用户体验。
*   **配置灵活性**: 外部化的设置和可配置的模板系统，增加了应用的灵活性和用户自定义程度。

---

总而言之，`lingualink_client` 是一个精心设计的 WPF 应用程序，通过其数据驱动的 MVVM 架构、模块化的服务层和解耦的通信机制，有效地实现了实时语音翻译及其与 VRChat 的集成，同时保持了良好的可维护性和用户体验。