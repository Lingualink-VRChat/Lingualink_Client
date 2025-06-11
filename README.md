**主要语言：中文** (English version follows the Chinese changelog entry)

```markdown
# LinguaLink Client

一个基于 WPF 的实时语音翻译客户端，支持 VRChat OSC 集成。

## 功能特性

- 🎤 **实时语音识别**: 自动检测和处理语音输入，集成优化的 VAD（语音活动检测）和追加录音功能。
- 🌍 **多语言翻译**: 支持英文、日文、中文等多种语言翻译。
- ✨ **动态语言加载**: 启动时从服务器动态获取支持的语言列表，无需硬编码。
- 🔐 **API 密钥认证**: 支持安全的后端 API v2.0 认证，并提供连接测试功能。
- 🔊 **Opus 音频编码标准**: 默认启用 Opus (16kbps) 高效压缩音频，支持调节编码复杂度，显著减少带宽使用。
- ✨ **音频增强处理**: 内置峰值归一化和安静语音增强功能，提升识别准确率。
- 🎮 **VRChat 集成**: 直接发送翻译结果到 VRChat 聊天框。
- 📝 **自定义模板**: 灵活的消息格式模板系统，支持语言代码和中文名两种占位符，并提供实时预览和验证。
- 🎛️ **参数调节**: 可调节的 VAD、Opus 编码、音频增强及 OSC 参数。
- 📊 **实时日志**: 详细的运行状态和错误日志。
- 🌐 **多语言界面**: 支持中文、英文、日文界面。
- 💄 **现代化UI**: 基于 WPF UI 构建的 Fluent Design 界面，包含自定义消息框。

## 系统要求

- Windows 10/11
- .NET 8.0 Runtime
- 麦克风设备
- LinguaLink Server 后端 (推荐 v2.0+)

## 快速开始

### 1. 安装运行时

确保系统已安装 .NET 8.0 Runtime：
- 下载：[.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### 2. 配置后端

启动 LinguaLink Server (推荐 v2.0 或更高版本以完全兼容所有功能) 并获取 API 密钥：

```bash
# 示例：生成 API 密钥 (具体命令请参考您的 LinguaLink Server 文档)
# python -m src.lingualink.utils.key_generator --name "lingualink-client-v3"

# 示例：启动服务器
# python3 manage.py start
```

### 3. 配置客户端

1.  启动 LinguaLink Client。
2.  进入 **"账户 (Account)"** 页面：
    * 如果使用自建服务器，勾选 **"使用自定义服务器 (Use custom server instead)"**。
    * **服务器 URL (Server URL)**: 输入您的 LinguaLink Server API v2.0 的基础 URL (例如: `http://localhost:8080/api/v1/`)。请确保 URL 指向 API 的根路径。
    * **API 密钥 (API Key)**: 输入从后端获取的 API 密钥。
    * 点击 **"测试连接 (Test Connection)"** 按钮验证配置是否正确。
    * 点击 **"保存 (Save)"**。
3.  进入 **"服务 (Service)"** 页面：
    * **语音识别设置 (Voice Recognition Settings)**: 根据需要调整 VAD 参数，如追加录音时长、最小/最大语音时长、最小音量阈值。
    * **VRChat 集成 (VRChat Integration)**: 如果需要，启用 OSC 并配置 IP 地址 (通常为 `127.0.0.1`) 和端口 (VRChat 默认为 `9000`)。
    * **音频处理 (Audio Processing)**: Opus 编码默认启用。可调整 **Opus 压缩级别 (Opus Compression Level)** (范围 5-10，默认 7，影响 CPU 和压缩率，比特率固定为 16kbps)。
    * **音频增强 (Audio Enhancement)**: 根据需要启用峰值归一化和安静语音增强，并调整相关参数。
    * 点击 **"保存 (Save)"**。
4.  进入 **"设置 (Settings)"** 页面，选择合适的 **界面语言 (Interface Language)**。

### 4. 开始使用

1.  在 **"启动 (Start)"** 页面：
    * 选择一个有效的 **麦克风设备 (Select Microphone)**。
    * 如果未使用模板，请在 **目标语言 (Target Languages)** 部分选择一至三个翻译目标语言。如果启用了自定义模板，目标语言将由模板内容决定。
2.  点击 **"开始监听 (Start Listening)"** 按钮开始语音监听。
3.  说话时系统会自动识别、处理并翻译。
4.  翻译结果会显示在界面上（原始响应和 VRChat 输出），并可根据配置发送到 VRChat。

## 主要功能详解

### 语音识别与翻译
（与项目总结部分类似，强调 API v2.0, Opus, 音频增强, VAD 优化）

### VRChat 集成
（与项目总结部分类似）

### 界面功能
（与项目总结部分类似，强调组件化 ViewModel 和现代化 UI）

## 配置说明

### 账户设置 (Account Page)

* **使用自定义服务器 (Use custom server instead)**: 勾选此项以连接到您自己部署的 LinguaLink 服务器。
    * **服务器 URL (Server URL)**: 您的 LinguaLink 服务器 API v2.0 的基础 URL (例如 `http://localhost:8080/api/v1/`)。
    * **API 密钥 (API Key)**: 用于访问您的自定义服务器的 API 密钥。
    * **测试连接 (Test Connection)**: 点击此按钮可验证您填写的服务器URL和API密钥是否能成功连接到后端服务。
* 官方服务登录功能即将推出。

### 服务设置 (Service Page)

#### 语音识别设置 (Voice Recognition Settings)

* **追加录音时长 (Post-Speech Recording Duration)**: VAD 检测到语音结束后继续录音的时长，用于捕捉尾音 (0.1-0.7秒, 推荐0.5秒)。
* **最小语音时长 (Minimum Voice Duration)**: 有效语音片段的最短时间 (0.1-0.7秒, 推荐0.5秒)。短于此时长的片段将被忽略。
* **最大语音时长 (Maximum Voice Duration)**: 单个语音片段的最长录制时间 (1-10秒, 推荐10秒)。超长会自动分段。
* **最小录音音量阈值 (Minimum Recording Volume Threshold)**: 麦克风输入音量超过此阈值才开始处理 (0%-100%)。0% 表示禁用此过滤。

#### VRChat 集成 (VRChat Integration)

* **启用 OSC 发送 (Enable OSC Sending)**: 是否将翻译结果发送到 VRChat。
* **OSC IP 地址 (OSC IP Address)**: VRChat 监听的 IP 地址 (通常为 `127.0.0.1`)。
* **OSC 端口 (OSC Port)**: VRChat 监听的端口 (默认为 `9000`)。
* **立即发送 (Send Immediately)**: 是否绕过 VRChat 键盘输入框直接发送消息。
* **播放通知音效 (Play Notification Sound)**: 发送消息到 VRChat 时是否播放提示音。

#### 音频处理 (Audio Processing)
* **Opus 音频编码**: 默认启用，使用固定 16kbps 比特率。
* **Opus 压缩级别 (Opus Compression Level)**: 调整编码复杂度 (范围 5-10，默认 7)。级别越高，压缩效果越好，但 CPU 占用也越高。

#### 音频增强 (Audio Enhancement)
* **启用峰值归一化 (Enable Peak Normalization)**: 是否将音频峰值调整到目标电平。
    * **归一化目标电平 (Normalization Target Level)**: dBFS 为单位，推荐 -3.0 dBFS。
* **启用安静语音增强 (Enable Quiet Speech Enhancement)**: 是否自动增益音量较小的语音片段。
    * **安静片段 RMS 阈值 (Quiet Segment RMS Threshold)**: 当片段 RMS 低于此值 (dBFS) 时应用增益，推荐 -25.0 dBFS。
    * **安静片段增益 (Quiet Segment Gain)**: 对安静片段应用的增益量 (dB)，推荐 6.0 dB。


## 模板系统 (Message Templates Page)

### 预设模板 (通过取消勾选 "Use Custom Template" 来使用原始服务器输出)

* **默认行为 (不使用自定义模板)**: 显示服务器返回的完整原始文本。

### 自定义模板

通过勾选 **"使用自定义模板 (Use Custom Template)"** 来启用。支持使用占位符创建自定义模板。

模板示例：
```
English: {en}
Japanese: {ja}
Chinese: {zh}
```
**注意**:
- 模板同时支持新式语言代码 (`{en}`, `{ja}`)和旧式中文名 (`{日文}`, `{英文}`)占位符，以确保向后兼容。
- 当使用自定义模板时，目标翻译语言将由模板中包含的占位符决定。为保证性能，系统最多只会请求翻译模板中的**前3种语言**。如果模板中包含超过3种语言，界面会显示警告信息。

## 故障排除
（与原 README 基本一致，可按需更新）

## 开发
（与原 README 基本一致）

## 更新日志

### v3.1.0 (2025-06-09)
- ✨ **动态语言加载与初始化**: 重构了语言管理系统，现在客户端启动时会从API动态获取支持的语言列表，使语言支持与后端实时同步。如果API加载失败，则会回退到内置的语言列表。
- 🚀 **模板系统增强**: 模板引擎现在同时支持新式语言代码 (`{en}`)和旧式中文名 (`{日文}`)占位符，确保了向后兼容性。增加了模板验证功能，当模板中包含超过3种语言时会向用户显示警告。
- 🔧 **新增API连接测试**: 在"账户"页面增加了"测试连接"按钮，方便用户验证服务器URL和API密钥的有效性。
- 🌐 **本地化系统完善**: 根据新的API响应更新了所有内置的语言资源文件（例如，将"德文"统一为"德语"），解决了部分语言显示为英文的问题。同时添加了API连接验证和模板警告的本地化文本。
- 🧹 **架构重构**: 优化了 `ApiResultProcessor` 和多个ViewModel，利用事件聚合器 (`LanguagesInitializedEvent`)实现了模块间的解耦，提升了代码的健壮性和可维护性。

---

**English Translation of v3.1.0 Changelog:**

### v3.1.0 (2025-06-09)
- ✨ **Dynamic Language Loading & Initialization**: Rearchitected the language management system. The client now dynamically fetches the list of supported languages from the API on startup, ensuring real-time synchronization with the backend. A fallback mechanism to a built-in list is in place if the API call fails.
- 🚀 **Template System Enhancement**: The template engine now supports both modern language codes (`{en}`) and legacy Chinese names (`{日文}`) as placeholders, ensuring backward compatibility. Added template validation to warn users when more than three languages are included in the template.
- 🔧 **New API Connection Test Feature**: Added a "Test Connection" button on the "Account" page for users to easily validate their server URL and API key.
- 🌐 **Localization System Improvement**: Updated all built-in language resource files to align with the new API response format (e.g., standardizing on "德语" for German), fixing the issue where some languages were displayed in English. Added localized text for API connection validation and template warnings.
- 🧹 **Architectural Refactoring**: Optimized `ApiResultProcessor` and various ViewModels. Utilized the `EventAggregator` (`LanguagesInitializedEvent`) to decouple modules, enhancing code robustness and maintainability.

### v3.0.0 (2025-06-04)
- ✨ **全面迁移至 LinguaLink API v2.0**: 优化了与后端服务的交互逻辑和数据模型。
- 🔊 **Opus 音频编码成为标准**: 默认启用 Opus (16kbps 固定码率) 以大幅减少带宽占用，并支持调节编码复杂度。
- 💪 **新增音频增强功能**: 包括峰值归一化和安静语音增强，旨在提升语音识别的准确性。
- ⚙️ **VAD 系统优化**: 改进了语音活动检测 (VAD) 逻辑，增加了可配置的说话后追加录音功能，并使 VAD 参数更精细化。
- 🏗️ **架构重构与组件化**:
    - 引入了简单的依赖注入容器 (`ServiceContainer`) 和服务初始化器 (`ServiceInitializer`)。
    - 实现了事件聚合器 (`EventAggregator`) 以促进模块间的松耦合通信。
    - ViewModel 层进行了显著的组件化重构，例如 `IndexWindowViewModel` 作为多个子组件 ViewModel（如 `MainControlViewModel`, `MicrophoneSelectionViewModel`, `TargetLanguageViewModel`, `TranslationResultViewModel`, `LogViewModel`）的容器。
    - 引入了 ViewModel 层的管理器 (`ViewModels/Managers/`)，如 `MicrophoneManager` 和 `TargetLanguageManager`，用于处理更复杂的UI相关状态和逻辑。
- 💄 **UI 改进与用户体验提升**:
    - 引入了基于 WPF UI 的现代化消息框 (`ModernMessageBox`)，替换了系统默认对话框。
    - 账户页面 (`AccountPage`) 和服务配置页面 (`ServicePage`) UI 布局和逻辑调整，配置项更清晰。
    - 改进了麦克风和目标语言选择组件的交互和状态管理。
- 🔧 **配置项扩展**: `AppSettings` 模型增加了更多可配置参数，包括音频增强选项、Opus 编码复杂度等。
- 🧹 **代码质量提升**: 更广泛和规范地使用了 `CommunityToolkit.Mvvm`，加强了 MVVM 模式的应用，服务分层更清晰。
- 🌐 **本地化完善**: 增加了更多UI文本的本地化支持。

---

**English Translation of v3.0.0 Changelog:**

### v3.0.0 (2025-06-04)
- ✨ **Full Migration to LinguaLink API v2.0**: Optimized interaction logic and data models with the backend service.
- 🔊 **Opus Audio Encoding as Standard**: Opus encoding (fixed 16kbps bitrate) is now enabled by default to significantly reduce bandwidth usage, with support for adjusting encoding complexity.
- 💪 **New Audio Enhancement Features**: Added Peak Normalization and Quiet Speech Enhancement to improve speech recognition accuracy.
- ⚙️ **VAD System Optimization**: Improved Voice Activity Detection (VAD) logic, including a configurable post-speech recording feature and more granular VAD parameters.
- 🏗️ **Architectural Refactoring & Componentization**:
    - Introduced a simple Dependency Injection container (`ServiceContainer`) and a `ServiceInitializer`.
    - Implemented an `EventAggregator` for loosely coupled communication between modules.
    - Significantly refactored the ViewModel layer into components, e.g., `IndexWindowViewModel` now acts as a container for sub-component ViewModels (`MainControlViewModel`, `MicrophoneSelectionViewModel`, `TargetLanguageViewModel`, `TranslationResultViewModel`, `LogViewModel`).
    - Introduced ViewModel-layer managers (`ViewModels/Managers/`), such as `MicrophoneManager` and `TargetLanguageManager`, for handling more complex UI-related states and logic.
- 💄 **UI Improvements & Enhanced User Experience**:
    - Introduced a modern `ModernMessageBox` based on WPF UI, replacing default system dialogs.
    - Adjusted UI layout and logic for the Account Page (`AccountPage`) and Service Configuration Page (`ServicePage`) for clearer settings.
    - Improved interaction and state management for microphone and target language selection components.
- 🔧 **Configuration Options Expanded**: The `AppSettings` model now includes more configurable parameters, such as audio enhancement options and Opus encoding complexity.
- 🧹 **Code Quality Improvements**: More extensive and standardized use of `CommunityToolkit.Mvvm`, strengthening the MVVM pattern application, and clearer service layering.
- 🌐 **Localization Enhancements**: Added localization support for more UI texts.


### v2.1.0 (2025-05-27)
- (Previous changelog entry, kept for history)
- 🔊 新增 Opus 音频编码支持，显著减少带宽使用
- ⚡ 优化音频传输性能，支持可配置的压缩参数
- 🎛️ 增强的音频编码设置界面
- 🛠️ 改进的错误处理和回退机制

### v2.0.0 (2025-05-26)
- (Previous changelog entry, kept for history)
- 🔐 添加 API 密钥认证支持
- 🔄 更新 API 端点到 v1
- 🌐 支持新的后端响应格式

### v1.0.0
- 🎉 初始版本发布
- 🎤 基础语音识别和翻译功能
- 🎮 VRChat OSC 集成
- 📝 模板系统
- 🌍 多语言界面支持

---

如有问题或需要支持，请提交 Issue。
```