# Code Quality Roadmap

这份清单基于当前仓库的一次快速扫描，目标是为后续的解耦、去重和拆分提供明确抓手。

## 当前观察

- 2026-04-24 更新：`lingualink_client.sln` 已接入 `tests/LinguaLink.Client.Tests`，`Debug|Any CPU` 不再误映射到 Release；API/Auth 高风险日志已开始脱敏，新增了 `LogSanitizer` 单元测试。
- 仓库中约有 `52` 处 `ServiceContainer.Resolve/TryResolve` 调用，说明依赖注入和服务定位器目前处于混用状态。
- 仓库中约有 `559` 处 `LanguageManager.GetString(...)` 调用，本地化能力完整，但也带来了大量重复标签属性和刷新逻辑。
- 体量最大的热点文件集中在页面级 ViewModel 和流程服务：
  - [ViewModels/Pages/AccountPageViewModel.cs](../ViewModels/Pages/AccountPageViewModel.cs)
  - [Services/Auth/AuthService.cs](../Services/Auth/AuthService.cs)
  - [ViewModels/Components/ConversationHistoryViewModel.cs](../ViewModels/Components/ConversationHistoryViewModel.cs)
  - [Services/Api/LingualinkApiService.cs](../Services/Api/LingualinkApiService.cs)
  - [Services/Audio/AudioService.cs](../Services/Audio/AudioService.cs)

## P1. 先拆页面级“大脑”

### 1. Account 页面职责过载

- [ViewModels/Pages/AccountPageViewModel.cs:24](../ViewModels/Pages/AccountPageViewModel.cs#L24) 同时承担服务器设置、登录态同步、个人资料编辑、邮箱绑定、社交账号绑定、套餐加载、下单、轮询支付状态和连接测试。
- [ViewModels/Pages/AccountPageViewModel.cs:474](../ViewModels/Pages/AccountPageViewModel.cs#L474) 到 [ViewModels/Pages/AccountPageViewModel.cs:597](../ViewModels/Pages/AccountPageViewModel.cs#L597) 这一段初始化和命令刷新逻辑已经接近“页面协调器”。
- [ViewModels/Pages/AccountPageViewModel.cs:1234](../ViewModels/Pages/AccountPageViewModel.cs#L1234) 到 [ViewModels/Pages/AccountPageViewModel.cs:1435](../ViewModels/Pages/AccountPageViewModel.cs#L1435) 又单独承载了一套设置加载、自动保存和校验流程。

建议拆分为：
- `AccountServerSettingsEditor`
- `AccountProfileEditor`
- `SubscriptionCheckoutController`
- `AccountConnectionTester`

### 2. 会话历史页面可继续下沉

- [ViewModels/Components/ConversationHistoryViewModel.cs:26](../ViewModels/Components/ConversationHistoryViewModel.cs#L26) 同时负责过滤器状态、列表刷新、导出、复制、迁移存储目录和 UI 选择同步。
- 复制、导出和路径迁移属于明显的子职责，适合下沉到独立 helper/service。

## P1. 收敛依赖获取方式

- [Services/Infrastructure/ServiceInitializer.cs:40](../Services/Infrastructure/ServiceInitializer.cs#L40) 已经承担集中注册职责，但很多 ViewModel 仍直接用 `ServiceContainer` 自行解析依赖。
- [ViewModels/Managers/MicrophoneManager.cs:98](../ViewModels/Managers/MicrophoneManager.cs#L98) 和 [ViewModels/Managers/TargetLanguageManager.cs:57](../ViewModels/Managers/TargetLanguageManager.cs#L57) 仍在构造函数里混用 `Resolve`、`new SettingsService()` 和具体服务实现。
- [ViewModels/Pages/TextEntryPageViewModel.cs:55](../ViewModels/Pages/TextEntryPageViewModel.cs#L55) 也同时依赖注入、服务定位器和全局 `Application.Current`。

建议路线：
- 新代码优先使用显式构造注入。
- 页面代码后置负责组装页面 ViewModel，避免 ViewModel 自行向全局容器“要东西”。
- 把 `ServiceContainer` 收缩到应用启动和兼容层。

## P1. 统一设置编辑模式

- [ViewModels/Pages/ServicePageViewModel.cs:96](../ViewModels/Pages/ServicePageViewModel.cs#L96) 和 [ViewModels/Pages/AccountPageViewModel.cs:474](../ViewModels/Pages/AccountPageViewModel.cs#L474) 都有“加载设置 -> 监听属性变化 -> 启动自动保存 -> 更新模型 -> 保存”的重复流程。
- [ViewModels/Pages/MessageTemplatePageViewModel.cs:184](../ViewModels/Pages/MessageTemplatePageViewModel.cs#L184) 也有自己的保存路径。

建议抽一个可复用的页面级设置编辑基类或 helper，例如：
- `SettingsEditorSession<TSettings>`
- 统一提供 `Load`, `MarkDirty`, `Validate`, `Save`, `Revert`, `AutoSave`

## P2. 本地化访问去重

- [ViewModels/Pages/AccountPageViewModel.cs:39](../ViewModels/Pages/AccountPageViewModel.cs#L39) 开始是一长串标签属性。
- [ViewModels/Pages/ServicePageViewModel.cs:30](../ViewModels/Pages/ServicePageViewModel.cs#L30)、[ViewModels/Components/ConversationHistoryViewModel.cs:96](../ViewModels/Components/ConversationHistoryViewModel.cs#L96)、[ViewModels/MainWindowViewModel.cs:7](../ViewModels/MainWindowViewModel.cs#L7) 也有相同模式。

建议路线：
- 为高频页面引入局部 `Strings` 包装对象，或者提供 `LocalizedViewModelBase` 统一刷新。
- 优先减少重复 `OnPropertyChanged(nameof(...))` 列表和散落的 `LanguageChanged` 订阅。

## P2. UI 基础设施统一

- 仓库里仍有多处直接使用 `System.Windows.MessageBox`，例如 [ViewModels/Pages/SettingPageViewModel.cs:201](../ViewModels/Pages/SettingPageViewModel.cs#L201)、[ViewModels/Pages/IndexWindowViewModel.cs:153](../ViewModels/Pages/IndexWindowViewModel.cs#L153)、[ViewModels/Components/ConversationHistoryViewModel.cs:231](../ViewModels/Components/ConversationHistoryViewModel.cs#L231)。
- 这会让新旧消息框体验并存，也让后续替换成本变高。

建议路线：
- 收敛到 [Services/Ui/MessageBox.cs](../Services/Ui/MessageBox.cs)
- 再向后演进到可注入的 `IDialogService`

## P2. 生命周期管理补齐

- 多个页面/窗口订阅 `LanguageManager.LanguageChanged`，但释放路径并不统一。
- 这次已经先补了一轮页面级释放，后续仍建议做一次专项排查，把“订阅时必须写释放路径”固化为代码评审规则。

## 推荐推进顺序

1. 先把依赖获取方式收敛，停止新增 `ServiceContainer.Resolve`。
2. 再拆 `AccountPageViewModel`，优先剥离订阅/支付/资料编辑三个子域。
3. 抽设置编辑公共流程，统一自动保存和校验模式。
4. 最后处理本地化包装与统一弹窗服务，减少样板代码。
