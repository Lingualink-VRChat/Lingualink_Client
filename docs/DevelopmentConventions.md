# Development Conventions

这份文档面向日常开发，目标是让功能迭代、代码评审和跨环境协作尽量保持一致。

## 1. 推荐工作流

- 代码编辑可以在 WSL 或 Windows 下进行，但同一个工作区尽量固定使用一套 Git 工作流，不要交替用 Windows Git 和 WSL Git 提交同一批改动。
- 当前仓库通过 [`.gitattributes`](../.gitattributes) 和 [`.editorconfig`](../.editorconfig) 固定换行策略。
- 源码类文本文件默认使用 `LF`，Windows 入口文件如 `*.sln`、`*.ps1`、`*.bat`、`*.cmd` 保持 `CRLF`。
- 如果你在 WSL 下改代码、在 Visual Studio 下编译看效果，这是支持的常规工作流；VS 可以正常处理 `LF` 源码文件。
- 推荐在 Windows 和 WSL 两边都设置：

```bash
git config --global core.autocrlf false
git config --global core.safecrlf warn
```

## 2. 日常开发原则

- 优先保持 MVVM 分层清晰：页面负责展示，ViewModel 管状态与命令，服务层承载业务流程和外部交互。
- 新功能先判断应落在 `Views/Pages`、`ViewModels/Pages`、`ViewModels/Components`、`Services/*` 的哪一层，避免直接把逻辑堆进页面代码后置文件。
- 能通过构造参数注入的依赖，不要新增 `ServiceContainer.Resolve<T>()` 调用；服务定位器只作为兼容旧代码的过渡手段。
- 页面级设置修改优先走 `ISettingsManager.TryUpdateAndSave(...)`，避免每个页面自己拼装加载、保存、广播逻辑。
- 内建服务地址、下载源和运行时下载链接统一放在 `Models/AppEndpoints.cs`，不要把环境常量散落到窗口、ViewModel 或服务实现里。
- 所有面向用户的字符串都进入 `Properties/Lang*.resx`，不要把界面文本硬编码在 ViewModel、服务或 XAML 后置代码里。
- 剪贴板写入统一使用 `ClipboardHelper.TrySetText(...)`，不要直接调用 `System.Windows.Clipboard` 或 `System.Windows.Forms.Clipboard`。
- UI 提示优先使用 `Services/Ui/MessageBox.cs` 的封装，而不是散落的 `System.Windows.MessageBox`。

## 3. ViewModel 约定

- 新的可绑定状态优先使用 CommunityToolkit 的 `[ObservableProperty]`。
- 页面或组件如果订阅了 `LanguageManager.LanguageChanged`、事件聚合器、`PropertyChanged`、定时器等事件，必须提供释放路径并在页面卸载时解绑。
- 单个 ViewModel 如果同时承担设置编辑、认证流程、网络请求、定时轮询和弹窗交互，通常已经超过合理边界，应优先考虑拆成协调器、子状态对象或专门服务。
- 涉及自动保存时，优先复用“脏标记 + 节流计时器 + 统一保存入口”的模式，避免每个属性单独保存。

## 4. 服务层约定

- 服务负责流程编排、I/O 和跨模块通信；不要让 ViewModel 直接 new 出底层网络/音频对象并维护其生命周期。
- 服务之间需要广播状态变化时，优先用事件聚合器或明确的接口，而不是跨层直接持有具体 ViewModel。
- 对 `IDisposable` 依赖要显式释放，尤其是 `HttpClient` 包装、音频设备、数据库连接、轮询 `CancellationTokenSource` 和定时器。

## 5. 本地化与 UI

- 页面上的标签型属性较多时，优先集中管理刷新逻辑，不要在多个地方手写零散的 `OnPropertyChanged(nameof(...))`。
- 对话框、错误提示和确认框尽量统一标题、图标和用词，减少用户体验上的割裂。
- 页面代码后置只保留真正的 UI 交互桥接逻辑，例如控件事件、焦点处理、导航生命周期。

## 6. 提交与验证

- 保持一个提交只做一类事情，例如 `docs:`、`refactor:`、`fix:` 分开处理。
- 偏结构整理的重构，优先拆成“ViewModel/页面结构”、“基础设施注入”、“测试/文档”几个阶段提交，方便回滚和评审。
- 提交前至少检查：
  - `git diff --check`
  - `dotnet build lingualink_client.csproj -c Debug -p:EnableWindowsTargeting=true`
  - 纯逻辑改动对应的 `dotnet test tests/LinguaLink.Client.Tests/LinguaLink.Client.Tests.csproj`
  - 受影响页面的手动流程
  - 本地化字符串是否补齐
  - 页面切换、重复打开、退出应用时是否存在资源未释放的问题
- 当前仓库只有轻量级纯逻辑自动化测试。涉及音频、翻译、OSC、更新、认证、支付和本地化改动时，请在 PR 里附上手动验证步骤。
