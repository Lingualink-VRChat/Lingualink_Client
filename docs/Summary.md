# LinguaLink Client - Architecture Summary

## 1. 项目现状

LinguaLink Client 是一个基于 `net8.0-windows` 的 WPF MVVM 桌面客户端，核心目标是把语音识别、文本翻译和 VRChat OSC 输出整合成一个可配置、可持久化、可本地化的工作流。

当前主功能包括：
- 实时麦克风监听、VAD 分段与音频增强
- 音频翻译与文本翻译两条独立流程
- VRChat OSC 输出与自定义模板格式化
- 账户登录、用户资料同步与订阅支付入口
- 会话历史记录与日志中心
- 多语言界面、自动更新与现代化消息框

## 2. 代码结构

仓库主要分为以下几层：

- `Views/`
  - 页面与窗口的 XAML，以及少量 UI 事件桥接逻辑
- `ViewModels/`
  - 页面级、组件级和管理器级 ViewModel
- `Models/`
  - `AppSettings`、认证模型、翻译结果、更新模型、历史记录模型
- `Services/`
  - API、音频、认证、更新、日志、本地化、基础设施、UI 封装
- `docs/`
  - 架构说明、开发规范、发布说明、重构路线图和参考资料

和旧版本相比，当前代码已经逐步从“直接在页面里保存设置和广播事件”收敛到：
- 用 `ISettingsManager` 统一处理设置加载、校验、保存和 `SettingsChangedEvent`
- 用 `IEventAggregator` 做跨模块通信
- 用组件化 ViewModel 组织主页面

## 3. 当前关键模块

### 3.1 应用入口

- [App.xaml.cs](../App.xaml.cs)
  - 启动 Velopack
  - 初始化服务容器
  - 恢复认证会话
  - 应用界面语言
  - 创建共享的 `IndexWindowViewModel`
  - 触发自动更新检查

- [MainWindow.xaml.cs](../MainWindow.xaml.cs)
  - 承载根导航框架
  - 在窗口加载时应用主题与主页面导航

### 3.2 主工作流

- [ViewModels/Pages/IndexWindowViewModel.cs](../ViewModels/Pages/IndexWindowViewModel.cs)
  - 作为主页面容器，持有 `MainControl`、`MicrophoneSelection`、`TargetLanguage`、`TranslationResult`
  - 在启动后负责语言初始化、麦克风刷新和更新提示

- [ViewModels/Components/MainControlViewModel.cs](../ViewModels/Components/MainControlViewModel.cs)
  - 驱动音频监听启停
  - 响应设置变化并重建 `AudioTranslationOrchestrator`

- [Services/Managers/AudioTranslationOrchestrator.cs](../Services/Managers/AudioTranslationOrchestrator.cs)
  - 串联音频采集、编码、API 请求和 OSC 输出

- [Services/Managers/TextTranslationOrchestrator.cs](../Services/Managers/TextTranslationOrchestrator.cs)
  - 负责文本输入页的翻译与输出流程

### 3.3 设置与持久化

- [Models/AppSettings.cs](../Models/AppSettings.cs)
  - 统一承载服务器、模板、OSC、历史记录、音频参数和更新源覆写等配置

- [Services/Infrastructure/SettingsService.cs](../Services/Infrastructure/SettingsService.cs)
  - 负责 `app_settings.json` 的读写与旧配置归一化

- [Services/Infrastructure/SettingsManager.cs](../Services/Infrastructure/SettingsManager.cs)
  - 封装页面常见的“读取最新设置 -> 应用当前页面修改 -> 保存 -> 广播设置变更”流程

### 3.4 认证与支付

- [Services/Auth/AuthService.cs](../Services/Auth/AuthService.cs)
  - 管理登录状态、令牌持久化、用户资料同步和未授权恢复

- [ViewModels/Pages/AccountPageViewModel.cs](../ViewModels/Pages/AccountPageViewModel.cs)
  - 当前仍然是较大的页面级协调器，负责：
  - 官方/自定义服务切换
  - 登录态展示与资料编辑
  - 邮箱绑定和社交绑定
  - 套餐加载、支付窗口打开与订单状态同步

### 3.5 历史记录与日志

- [Services/Managers/ConversationHistoryService.cs](../Services/Managers/ConversationHistoryService.cs)
  - 负责 LiteDB 存储、历史查询、迁移和导出支撑

- [ViewModels/Components/ConversationHistoryViewModel.cs](../ViewModels/Components/ConversationHistoryViewModel.cs)
  - 负责会话筛选、复制、导出和目录切换

- [Services/Managers/LoggingManager.cs](../Services/Managers/LoggingManager.cs)
  - 集中管理 UI 侧日志条目

## 4. 当前配置与端点策略

目前仓库已经把内建端点集中到：

- [Models/AppEndpoints.cs](../Models/AppEndpoints.cs)

其中包括：
- 官方 API 地址
- Auth Server 地址
- 更新下载源
- WebView2 Runtime 下载链接

这样做的目的，是减少散落在 ViewModel、窗口和服务里的重复常量，降低迁移地址时的修改面。

更新服务当前支持两层来源：
- 编译目标决定的默认 feed
- 用户设置里的 `UpdateFeedOverride`

## 5. 当前推荐开发方式

优先参考以下文档：

- [DevelopmentConventions.md](./DevelopmentConventions.md)
  - 日常开发规范、换行策略、依赖注入约定、页面生命周期约定
- [CodeQualityRoadmap.md](./CodeQualityRoadmap.md)
  - 当前值得优先治理的耦合、去重和拆分热点
- [ReleaseGuide.md](./ReleaseGuide.md)
  - 构建、打包、上传和发布核对流程

开发实践上，建议优先遵守：

- 新代码优先显式注入依赖，不新增 `ServiceContainer.Resolve<T>()`
- 页面保存设置优先复用 `ISettingsManager.TryUpdateAndSave(...)`
- 用户可见字符串统一进入 `Lang*.resx`
- UI 弹窗优先走 [Services/Ui/MessageBox.cs](../Services/Ui/MessageBox.cs)
- 页面和 ViewModel 只要订阅了事件、定时器或 `LanguageChanged`，就必须有释放路径

## 6. 当前最需要继续优化的区域

以下区域仍然值得持续治理：

- [ViewModels/Pages/AccountPageViewModel.cs](../ViewModels/Pages/AccountPageViewModel.cs)
  - 职责过多，适合继续拆成资料编辑、服务设置、订阅支付等子域
- [ViewModels/Components/ConversationHistoryViewModel.cs](../ViewModels/Components/ConversationHistoryViewModel.cs)
  - 导出、复制和目录迁移可以继续下沉到更独立的服务
- `ServiceContainer` 使用仍偏多
  - 老代码和新代码的依赖获取方式尚未完全统一
- 本地化标签属性仍有明显样板
  - 后续可考虑统一的本地化包装层

## 7. 总结

当前仓库已经从“功能堆叠式迭代”逐步过渡到“有明确基础设施边界的桌面客户端”。最适合下一阶段做的事情，不是一次性重写，而是持续推进：

1. 端点与配置集中化
2. 页面级大 ViewModel 拆分
3. 设置编辑流程复用
4. UI 基础设施统一

这也是当前文档、提交和代码质量治理在对齐的方向。
