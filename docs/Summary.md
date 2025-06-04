主要语言：中文

项目总结：LinguaLink 客户端 v3.0.0
项目名称: LinguaLink 客户端 (lingualink_client)

项目目标:
LinguaLink 客户端是一个基于 Windows Presentation Foundation (WPF) 的桌面应用程序，旨在提供实时的语音识别和多语言翻译服务。其核心目标是将用户的语音输入快速准确地转换为文本，并将其翻译成一种或多种指定的目标语言。该客户端特别集成了对 VRChat 的 OSC (Open Sound Control) 支持，允许将翻译结果直接发送到 VRChat 的聊天框中，从而促进跨语言交流。

核心功能 (v3.0.0):

实时语音识别与翻译:

利用麦克风进行实时语音捕获。
集成优化的语音活动检测 (VAD) 技术 (WebRtcVadSharp)，包含可配置的说话后追加录音功能，以优化音频片段的捕获，减少不必要的传输和处理。
Opus 音频编码标准: 默认使用 Opus 编码 (Concentus库) 以固定 16kbps 比特率对捕获的音频进行高效压缩，显著减少网络带宽消耗，并支持调节编码复杂度。
新增音频增强处理: 在发送前对音频应用峰值归一化和安静语音增强，提升语音识别的准确性。
支持将处理后的音频发送到后端 LinguaLink API v2.0 服务进行语音转文本 (STT) 和机器翻译 (MT)。
支持多种目标语言的翻译，如英文、日文、中文等。
VRChat OSC 集成:

能够通过 OSC 协议将翻译后的文本发送到 VRChat 聊天框。
用户可以配置 OSC 的 IP 地址、端口以及发送行为（如是否立即发送、是否播放通知音效）。
API 服务交互 (v2.0 核心):

通过 HTTP(S) 与后端的 LinguaLink API v2.0 服务进行通信。
支持 API 密钥认证，确保安全访问。
主要依赖新的 API v2.0 响应模型 (NewApiResponse)，同时保留对旧格式的部分兼容转换。
用户界面与配置:

采用 WPF 和 WPF UI 库 (Wpf.Ui) 构建现代化 Fluent Design 用户界面。
引入了自定义的现代化消息框 (ModernMessageBox) 提升用户体验。
提供多页面导航，包括主工作区 (IndexPage，采用组件化 ViewModel 容器 IndexWindowViewModel)、服务配置 (ServicePage)、消息模板 (MessageTemplatePage)、账户设置 (AccountPage) 和日志查看 (LogPage)。
允许用户选择麦克风设备，并通过专门的 MicrophoneSelectionViewModel 管理。
支持用户配置目标翻译语言（手动选择或通过模板），由 TargetLanguageViewModel 管理。
提供灵活的消息模板系统 (MessageTemplatePageViewModel 和 TemplateProcessor)，用户可以自定义翻译结果的显示格式，支持基于当前界面语言的占位符提示。
可调节 VAD 参数（追加录音时长、最小/最大语音时长、最小录音音量阈值）。
可配置 Opus 编码复杂度。
可配置音频增强参数（峰值归一化目标电平、安静语音增强的 RMS 阈值和增益）。
支持界面语言切换（英文、中文、日文），本地化资源通过 .resx 文件管理。
所有用户配置（如 API 密钥、服务器 URL、语言偏好、VAD 参数、OSC 设置、音频处理参数等）均会保存到本地用户特定目录下的 app_settings.json 文件中。
日志与状态显示:

提供详细的实时运行日志 (LogViewModel 和 LoggingManager)，方便用户查看操作过程和排查问题。
在界面上显示当前的工作状态 (MainControlViewModel) 和翻译结果 (TranslationResultViewModel)。
技术栈与架构:

平台: .NET 8.0, WPF (Windows Presentation Foundation)
UI 框架: WPF UI (Wpf.Ui)
架构模式: MVVM (Model-View-ViewModel)
Models (Models/): 定义数据结构，如 AppSettings (应用配置)、NewApiResponse (API v2.0 响应模型)、TranslationData (翻译数据结构)、MMDeviceWrapper (麦克风设备信息)。
Views (Views/): XAML 文件定义用户界面。
ViewModels (ViewModels/): 处理视图逻辑和数据绑定，使用 CommunityToolkit.Mvvm 实现。采用组件化设计，如 IndexWindowViewModel 作为容器管理 MainControlViewModel, MicrophoneSelectionViewModel, TargetLanguageViewModel, TranslationResultViewModel, LogViewModel 等子组件 ViewModel。引入 ViewModels/Managers/ 管理更复杂的 UI 相关状态集合。
核心服务 (Services/):
服务管理与通信: ServiceContainer (DI), ServiceInitializer, IEventAggregator / EventAggregator, SettingsChangedNotifier.
LingualinkApiService: 核心 API v2.0 通信服务。
LingualinkApiServiceFactory: 创建 LingualinkApiService 实例。
AudioService: 管理麦克风录音、VAD 处理、音频增强。
AudioEncoderService: 使用 Concentus 进行 Opus 音频编码。
SimpleOggOpusWriter: 辅助生成 OGG Opus 文件流。
OscService: 使用 OscCore 处理 OSC 消息发送。
SettingsService: 加载/保存 AppSettings。
LanguageManager: 管理界面语言和本地化字符串。
LanguageDisplayHelper: 辅助语言名称/代码转换。
LoggingManager: 集中管理日志。
AudioTranslationOrchestrator / TextTranslationOrchestrator: 协调音频/文本翻译的完整流程。
本地化: 使用 .resx 文件，由 LanguageManager 动态加载。
第三方库: NAudio, WebRtcVadSharp, OscCore, Concentus, Concentus.OggFile, CommunityToolkit.Mvvm, WPF UI (Wpf.Ui).
项目结构概述:
（与之前版本类似，但 ViewModels 和 Services 内部结构更为清晰，增加了 Managers 和 Components 子目录，体现了更好的模块化设计。）

/Assets/Icons/: 应用程序图标。
/Converters/: WPF 值转换器。
/Models/: 核心数据模型和应用设置。
/Properties/: 本地化资源文件。
/Services/: 核心业务逻辑。
/Services/Events/: 事件聚合器。
/Services/Interfaces/: 服务接口。
/Services/Managers/: 业务流程协调服务。
/ViewModels/: MVVM 的 ViewModel。
/ViewModels/Components/: UI 子组件的 ViewModel。
/ViewModels/Events/: ViewModel 间通信事件。
/ViewModels/Managers/: UI 相关的状态管理器。
/Views/: XAML UI 文件。
/Views/Components/: 可重用 UI 组件。
/Views/Pages/: 主要页面。
Root Directory: 项目文件, 解决方案, 应用入口等。
工作流程简介 (v3.0.0):

初始化: App.xaml.cs 启动时，ServiceInitializer 注册所有核心服务。加载 AppSettings 并应用界面语言。IndexWindowViewModel 被创建，它进而创建并管理其子组件的 ViewModel，如 MicrophoneSelectionViewModel 负责麦克风列表的加载与用户选择。
用户操作: 用户通过各专门的 ViewModel 控制的 UI 组件进行配置（麦克风、目标语言、模板、各项服务参数）。设置更改通过 SettingsService 保存，并由 SettingsChangedNotifier 通知相关组件。EventAggregator 用于组件间的解耦通信。
开始工作: 用户点击 "Start Work" (MainControlViewModel)。
AudioTranslationOrchestrator 被激活，使用 AudioService 监听选定麦克风。
AudioService 使用 VAD 检测语音，并应用配置的音频增强处理。
音频处理与翻译:
AudioService 捕获并处理完一个语音片段后，AudioTranslationOrchestrator 接收音频数据。
AudioEncoderService 将音频编码为 Opus 格式。
LingualinkApiService (v2.0) 将编码后的音频数据和目标语言发送到后端。
收到服务器响应后，解析翻译结果。
结果展示与发送:
翻译结果通过 TranslationCompletedEvent 事件传递给 TranslationResultViewModel 在 UI 显示。
若启用 OSC，OscService 将格式化（可能通过 TemplateProcessor）后的文本发送到 VRChat。
所有重要操作和状态变更由 LoggingManager 记录，并通过 LogViewModel 显示。
总结 (v3.0.0):
LinguaLink 客户端 v3.0.0 在之前版本的基础上进行了显著的架构优化和功能增强。全面迁移至 API v2.0，引入了高效的 Opus 音频编码作为标准配置，并新增了音频增强功能以提升识别准确率。VAD 系统得到进一步优化，ViewModel 层通过组件化和管理器模式提升了模块化和可维护性。这些更新使得客户端在性能、用户体验和代码质量上都有了显著提升，为用户提供了更强大、更稳定的实时语音翻译体验。

English Translation of the Summary:

Project Summary: LinguaLink Client v3.0.0
Project Name: LinguaLink Client (lingualink_client)

Project Goal:
LinguaLink Client is a Windows Presentation Foundation (WPF) based desktop application designed to provide real-time speech recognition and multilingual translation services. Its core objective is to quickly and accurately convert user's speech input into text and translate it into one or more specified target languages. The client notably integrates OSC (Open Sound Control) support for VRChat, allowing translation results to be sent directly to VRChat's chatbox, thereby facilitating cross-language communication.

Core Features (v3.0.0):

Real-time Speech Recognition & Translation:

Real-time speech capture using a microphone.
Integrated optimized Voice Activity Detection (VAD) technology (WebRtcVadSharp), including configurable post-speech recording duration, to refine audio segment capture, reducing unnecessary transmission and processing.
Opus Audio Encoding Standard: Defaults to using Opus encoding (Concentus library) with a fixed 16kbps bitrate for efficient audio compression, significantly reducing network bandwidth consumption, and supports adjustable encoding complexity.
New Audio Enhancement Processing: Applies peak normalization and quiet speech boost to audio before sending, improving speech recognition accuracy.
Supports sending processed audio to the backend LinguaLink API v2.0 service for Speech-to-Text (STT) and Machine Translation (MT).
Supports translation into multiple target languages such as English, Japanese, Chinese, etc.
VRChat OSC Integration:

Capable of sending translated text to the VRChat chatbox via the OSC protocol.
Users can configure OSC IP address, port, and sending behavior (e.g., send immediately, play notification sound).
API Service Interaction (v2.0 Core):

Communicates with the backend LinguaLink API v2.0 service via HTTP(S).
Supports API key authentication for secure access.
Primarily relies on the new API v2.0 response model (NewApiResponse), while retaining partial backward compatibility for older formats.
User Interface & Configuration:

Built with WPF and the WPF UI library (Wpf.Ui) for a modern Fluent Design user interface.
Introduced a custom ModernMessageBox for an improved user experience.
Provides multi-page navigation, including a main workspace (IndexPage using a componentized ViewModel container IndexWindowViewModel), service configuration (ServicePage), message templates (MessageTemplatePage), account settings (AccountPage), and log viewing (LogPage).
Allows users to select a microphone device, managed by a dedicated MicrophoneSelectionViewModel.
Supports user configuration of target translation languages (manual selection or via templates), managed by TargetLanguageViewModel.
Offers a flexible message template system (MessageTemplatePageViewModel and TemplateProcessor), allowing users to customize the display format of translation results, with placeholder hints based on the current UI language.
Adjustable VAD parameters (post-speech recording duration, min/max voice duration, min recording volume threshold).
Configurable Opus encoding complexity.
Configurable audio enhancement parameters (peak normalization target level, quiet speech boost RMS threshold and gain).
Supports UI language switching (English, Chinese, Japanese), with localization resources managed via .resx files.
All user configurations (API key, server URL, language preferences, VAD parameters, OSC settings, audio processing parameters, etc.) are saved locally to an app_settings.json file in a user-specific directory.
Logging & Status Display:

Provides detailed real-time operational logs (LogViewModel and LoggingManager) for users to view processes and troubleshoot issues.
Displays current working status (MainControlViewModel) and translation results (TranslationResultViewModel) in the UI.
Technology Stack & Architecture:

Platform: .NET 8.0, WPF (Windows Presentation Foundation)
UI Framework: WPF UI (Wpf.Ui)
Architecture Pattern: MVVM (Model-View-ViewModel)
Models (Models/): Defines data structures like AppSettings, NewApiResponse (API v2.0), TranslationData, MMDeviceWrapper.
Views (Views/): XAML files defining the UI.
ViewModels (ViewModels/): Handles view logic and data binding using CommunityToolkit.Mvvm. Employs a componentized design, e.g., IndexWindowViewModel as a container for MainControlViewModel, MicrophoneSelectionViewModel, etc. Introduces ViewModels/Managers/ for more complex UI-related state management.
Core Services (Services/):
Service Management & Communication: ServiceContainer (DI), ServiceInitializer, IEventAggregator / EventAggregator, SettingsChangedNotifier.
LingualinkApiService: Core API v2.0 communication service.
LingualinkApiServiceFactory: Creates LingualinkApiService instances.
AudioService: Manages microphone recording, VAD, and audio enhancements.
AudioEncoderService: Opus audio encoding using Concentus.
SimpleOggOpusWriter: Helper for generating OGG Opus streams.
OscService: OSC message sending using OscCore.
SettingsService: Loads/saves AppSettings.
LanguageManager: Manages UI language and localization.
LanguageDisplayHelper: Assists with language name/code conversions.
LoggingManager: Centralized logging.
AudioTranslationOrchestrator / TextTranslationOrchestrator: Coordinates the full audio/text translation workflow.
Localization: Uses .resx files, dynamically loaded by LanguageManager.
Third-party Libraries: NAudio, WebRtcVadSharp, OscCore, Concentus, Concentus.OggFile, CommunityToolkit.Mvvm, WPF UI (Wpf.Ui).
Project Structure Overview:
(Similar to previous versions, but ViewModels and Services have clearer internal structures with added Managers and Components subdirectories, reflecting better modularity.)

/Assets/Icons/: Application icons.
/Converters/: WPF value converters.
/Models/: Core data models and application settings.
/Properties/: Localization resource files.
/Services/: Core business logic.
/Services/Events/: Event aggregator.
/Services/Interfaces/: Service interfaces.
/Services/Managers/: Business process coordination services.
/ViewModels/: MVVM ViewModels.
/ViewModels/Components/: ViewModels for UI sub-components.
/ViewModels/Events/: Events for ViewModel communication.
/ViewModels/Managers/: UI-related state managers.
/Views/: XAML UI files.
/Views/Components/: Reusable UI components.
/Views/Pages/: Main application pages.
Root Directory: Project files, solution, application entry points, etc.
Workflow Brief (v3.0.0):

Initialization: On App.xaml.cs startup, ServiceInitializer registers all core services. AppSettings are loaded, and UI language is applied. IndexWindowViewModel is created, which in turn creates and manages ViewModels for its sub-components (e.g., MicrophoneSelectionViewModel for microphone list loading and selection).
User Interaction: Users configure settings (microphone, target languages, templates, service parameters) via UI components controlled by their respective ViewModels. Changes are saved via SettingsService and broadcast by SettingsChangedNotifier. EventAggregator facilitates decoupled communication.
Start Work: User clicks "Start Work" (MainControlViewModel).
AudioTranslationOrchestrator is activated, using AudioService to listen to the selected microphone.
AudioService uses VAD to detect speech and applies configured audio enhancements.
Audio Processing & Translation:
When AudioService captures and processes a speech segment, AudioTranslationOrchestrator receives the audio data.
AudioEncoderService encodes the audio to Opus format.
LingualinkApiService (v2.0) sends the encoded audio and target languages to the backend.
Server response is received and translation results are parsed.
Result Display & Sending:
Translation results are passed via TranslationCompletedEvent to TranslationResultViewModel for UI display.
If OSC is enabled, OscService sends the formatted text (possibly via TemplateProcessor) to VRChat.
All significant operations and state changes are logged by LoggingManager and displayed via LogViewModel.
Conclusion (v3.0.0):
LinguaLink Client v3.0.0 introduces significant architectural optimizations and feature enhancements over previous versions. It fully migrates to API v2.0, incorporates efficient Opus audio encoding as a standard feature, and adds new audio enhancement capabilities to improve recognition accuracy. The VAD system has been further refined, and the ViewModel layer has been refactored with componentization and manager patterns for improved modularity and maintainability. These updates provide users with a more powerful, stable,