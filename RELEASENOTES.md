# Release Notes – 3.4.9

## 简体中文 (zh-CN)

- **improve:** 为各个页面引入全局页面边距和标题样式（`PageContentMargin` / `PageTitleTextBlockStyle`），统一设置页、历史页等页面的留白和标题视觉风格。
- **improve:** 重构主页（Index）、文本输入、消息模板和语音服务等页面布局，采用卡片式分区展示输入、结果和配置区域，在窄窗口和高 DPI 下可读性更好。
- **improve:** 将导航中的“Service”重命名为“Voice”，并在中英日多语言资源中同步更新标题与图标，使含义更贴近语音输入和 VRChat 使用场景。
- **improve:** 会话历史页面调整为左右分栏布局，新增页面标题和独立的复制/导出工具栏，同时移除不常用的起止日期过滤条件，简化筛选项并改善滚动体验。
- **improve:** 日志页面由表格视图改为卡片式时间线视图，根据日志级别高亮显示，并优化多选复制行为，便于在排查问题时快速阅读和复制关键信息。
- **improve:** 消息模板页面采用卡片式布局和开关控件，支持一键插入占位符和查看实时预览，让自定义 VRChat 输出格式更直观。
- **fix:** 修复主页在嵌套 ScrollViewer 场景下翻译结果文本框内部滚动不生效的问题，现在会在页面加载后禁用外层垂直滚动条，仅在内容区域滚动。

## English (en)

- **improve:** Introduced shared `PageContentMargin` and `PageTitleTextBlockStyle` resources and applied them across key pages to unify page padding and headings.
- **improve:** Reworked the Index, Text Entry, Message Template, and Voice/Service pages into card‑based layouts that group input, output, and configuration areas, improving readability on narrow windows and high‑DPI displays.
- **improve:** Renamed the navigation entry from “Service” to “Voice” (with a microphone icon) and updated all localized strings to better reflect the voice input / VRChat workflow.
- **improve:** Redesigned the conversation history page into a two‑pane layout with a prominent page title, dedicated copy/export toolbar, and simplified filters (removing the rarely used date range) while ensuring scrolling happens inside the content grids instead of the whole page.
- **improve:** Switched the log page from a dense data grid to a card‑style timeline that highlights log levels with color and improves multi‑select copy behavior, making it easier to read and share diagnostics.
- **improve:** Refined the message template page with a card layout, toggle switch, placeholder buttons, and a live preview block to make customizing the VRChat output format more approachable.
- **fix:** Fixed an issue where the Index page’s translation result TextBox would not scroll properly when nested inside the main NavigationView ScrollViewer by explicitly disabling the outer vertical scrollbar after the page is loaded.
