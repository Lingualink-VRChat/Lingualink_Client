# LinguaLink Client

一个基于 WPF 的实时语音翻译客户端，支持 VRChat OSC 集成。

## 功能特性

- 🎤 **实时语音识别与监听**: 自动检测和处理语音输入，集成优化的 VAD（语音活动检测）。用户可点击“开始监听”启动该功能。
- 📝 **手动文本输入**: 新增独立的文本输入页面，允许用户手动键入消息并直接发送到 VRChat，同时支持在输入期间暂停音频处理，提升用户体验。
- 🌍 **多语言翻译**: 支持英文、日文、中文等多种语言翻译。
- ✨ **动态语言加载**: 启动时从服务器动态获取支持的语言列表，无需硬编码。
- ↕️ **目标语言排序**: 用户可以通过“上移”和“下移”按钮，自由调整目标翻译语言的显示顺序。
- 🔐 **API 密钥认证**: 支持安全的后端 API v2.0 认证，并提供连接测试功能。
- 🔊 **Opus 音频编码标准**: 默认启用 Opus (16kbps) 高效压缩音频，支持调节编码复杂度，显著减少带宽使用。
- ✨ **音频增强处理**: 内置峰值归一化和安静语音增强功能，提升识别准确率。
- 🎮 **VRChat 集成**: 直接发送翻译结果到 VRChat 聊天框。
- 📝 **增强的自定义模板**: 灵活的消息格式模板系统，不仅支持各语言占位符，还新增了对语音识别原文（`{transcription}`）的占位符支持。提供实时预览和验证。
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
    * 如果未使用模板，请在 **目标语言 (Target Languages)** 部分选择一至三个翻译目标语言，并可使用上下按钮调整顺序。如果启用了自定义模板，目标语言将由模板内容决定。
2.  点击 **"开始监听 (Start Listening)"** 按钮开始语音监听。
3.  说话时系统会自动识别、处理并翻译。
4.  您也可以切换到 **"文本输入 (Text Entry)"** 页面，手动输入文字并发送。
5.  翻译结果会显示在界面上（原始响应和 VRChat 输出），并可根据配置发送到 VRChat。

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

通过勾选 **"使用自定义模板 (Use Custom Template)"** 来启用。支持使用占位符创建自定义模板。新增了对语音识别原文的占位符支持。

**可用占位符**:
- **语言代码**: 如 `{en}`, `{ja}`, `{zh}` 等。
- **语音识别原文**: `{transcription}`，用于显示未经翻译的原始识别文本。
- **旧式中文名 (兼容)**: 如 `{日文}`, `{英文}` 等，为保证向后兼容性依然可用。

**模板示例**：
```
识别原文: {transcription}
English: {en}
日本語: {ja}
```
**注意**:
- 当使用自定义模板时，目标翻译语言将由模板中包含的占位符决定。为保证性能，系统最多只会请求翻译模板中的**前3种语言**。如果模板中包含超过3种语言，界面会显示警告信息。

## 故障排除
（与原 README 基本一致，可按需更新）

## 开发
（与原 README 基本一致）

## 更新日志

### v3.2.0 (2025-06-11)
- ✨ **新增文本输入功能**: 引入了全新的文本输入页面 (`TextEntryPage`)，允许用户手动输入并发送消息至 VRChat。该功能集成了多语言本地化支持，并在输入时自动暂停/恢复音频处理，以提供流畅的用户体验。
- 🚀 **占位符系统增强**: 重构了占位符管理，新增了对语音识别原文的特殊占位符支持 (`{transcription}`), 允许用户在自定义消息模板中直接嵌入未经翻译的识别结果，提高了模板的灵活性和实用性。
- ↕️ **新增目标语言排序功能**: 在主界面为目标语言列表添加了“上移”和“下移”按钮，使用户能够方便地调整语言的显示和发送顺序，优化了多语言交流时的操作便利性。
- ✏️ **术语统一与优化**: 将整个应用中的“Start Work”相关术语统一更新为“Start Listening”（开始监听），包括 README、界面文本及内部代码，使功能描述更加直观和准确。
- 🌐 **UI与本地化完善**: 更新了中、日、英三语的本地化资源，以支持文本输入、占位符增强等新功能。改进了多个页面的UI绑定，确保界面能够准确反映后台数据的变化。
- 🔧 **架构与代码改进**: 优化了 `ApiResultProcessor` 和相关业务流程，以处理用户选择的后端名称。`PlaceholderItem` 类的引入分离了显示文本和插入值，提升了代码的清晰度和可维护性。

---

**English Translation of v3.2.0 Changelog:**

### v3.2.0 (2025-06-11)
- ✨ **feat: Add Text Entry Page and Enhance Message Handling**: Introduced a new `TextEntryPage` for user input, allowing text to be sent directly to VRChat. This feature includes full localization support and automatically pauses/resumes audio processing for a smoother user experience.
- 🚀 **feat: Enhance Placeholder Management**: Refactored the placeholder system to add support for a special source text placeholder (`{transcription}`). This allows users to embed the original, untranslated speech recognition result directly into their custom message templates, enhancing flexibility.
- ↕️ **feat: Implement Language Item Reordering**: Added "Move Up" and "Move Down" buttons for the target language list on the main page, enabling users to easily reorder languages for display and output, improving usability in multi-language scenarios.
- ✏️ **refactor: Update Terminology for Voice Recording**: Changed all references of "Start Work" to "Start Listening" throughout the application, including the README, UI text, and internal code, to more accurately reflect the functionality.
- 🌐 **feat: UI and Localization Improvements**: Updated localization resources in English, Japanese, and Chinese to support new features like text entry and enhanced placeholders. Improved UI binding on multiple pages to ensure the interface accurately reflects underlying data changes.
- 🔧 **chore: Architectural and Code Enhancements**: Optimized `ApiResultProcessor` and related orchestrators to handle user-selected backend names. The new `PlaceholderItem` class decouples display text from insertion values, improving code clarity and maintainability.
