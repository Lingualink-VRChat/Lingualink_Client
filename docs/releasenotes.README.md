# Release Notes 维护约定

为了支持多语言更新日志，同时兼容 Velopack 的单文件 `RELEASENOTES.md` 要求，推荐按以下约定维护发布说明。

## 文件位置

- 根目录 `RELEASENOTES.md`：主发布渠道使用的更新日志，供 Velopack 打包和 QA / 运营查阅。
- `docs/`：如需记录更详细的发布说明或历史版本，可以在此目录下按需要增加子文档。

## 多语言结构建议

在单个 `RELEASENOTES.md` 中用标题分隔多种语言，例如：

```markdown
# Release Notes – 3.4.7

## 简体中文 (zh-CN)

- feat: 支持多语言字幕导出
- fix: 修复语言包初始化偶发崩溃

## English (en)

- feat: Support multilingual subtitle export
- fix: Fix intermittent crash during language pack initialization
```

Velopack 只会将整个 Markdown 作为一个长文本嵌入更新弹窗，因此同时包含多种语言是可行的：客户端用户可以直接在弹窗中找到自己熟悉的语言；官网或其他系统也可以直接复用同一份文件。

如果未来需要更复杂的多语言方案（例如按不同渠道打包不同语言的 Release Notes），可以在 `scripts/Build-Release.ps1` 外再包一层脚本，根据语言选择或生成对应版本的 `RELEASENOTES.md` 然后再调用打包脚本。

