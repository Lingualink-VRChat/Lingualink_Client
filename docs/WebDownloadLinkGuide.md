# Web 前端发布下载链接指引

本指南面向维护官网/下载页的前端同学，帮助你们在 LinguaLink 客户端出新版本时，第一时间把下载按钮指向最新构建，同时保证历史链接可回溯。

## 1. 文件在哪里
- 客户端发布脚本会把产物同步到 rains3（S3 兼容）桶 `lingualink`。
- 自包含版与框架依赖版分别对应两个前缀：
  - `https://download.cn-nb1.rains3.com/lingualink/stable-self-contained/`
  - `https://download.cn-nb1.rains3.com/lingualink/stable-framework-dependent/`
- 两个前缀下都会有 Velopack 标准的 `RELEASES` 元数据、`.nupkg` 增量包、`Setup.exe` 或 `Setup-with-runtime.exe` 全量安装包。
- 若已为域名 `dl.aiatechco.com` 做 CNAME，可用该域名替换 host，其余路径保持一致。

## 2. 如何判定“最新版本”
`RELEASES` 文件是一个使用制表符分隔的文本清单，每行代表一个版本。最后一行即为最新版本。例如：
```
<sha1>	<filesize>	LinguaLinkClient-SelfContained-3.3.0-full.nupkg	3.3.0
```
字段含义：
- 列 1：包的 SHA1 哈希（Velopack 用于校验）。
- 列 2：文件字节数。
- 列 3：包文件名（`.nupkg`）。安装器一般与 `.nupkg` 同版号，命名为 `Setup.exe` 或 `LinguaLinkClient-<channel>-<version>-Setup.exe`。
- 列 4：语义化版本号。

前端只需读取最后一行的列 3/列 4，即可拼出新版本链接与展示的版本号。

## 3. 推荐的前端实现流程
1. 根据用户所需渠道（默认推荐自包含版）选定前缀 URL。
2. 以 `GET` 请求拉取 `RELEASES`，注意添加 `cache-control: no-cache` 或拼接时间戳避免 CDN 缓存。
3. 把响应文本按换行拆分，过滤空行，选取最后一行。
4. 以制表符分割该行，解析版本号与 `.nupkg` 文件名。
5. 将文件名中的 `.nupkg` 替换为 `Setup.exe`（或按后端约定的安装包名称）得到下载地址。
6. 在页面上展示版本号、更新时间，并把下载按钮链接指向该地址。
7. 可选：提供“需要更小包体？”的链接跳转到框架依赖版。

### TypeScript 示例
```ts
const S3_BASE = 'https://dl.aiatechco.com/lingualink';
const CHANNEL = 'stable-self-contained'; // or 'stable-framework-dependent'

async function fetchLatestInstaller() {
  const releasesUrl = `${S3_BASE}/${CHANNEL}/RELEASES?ts=${Date.now()}`;
  const res = await fetch(releasesUrl, { headers: { 'cache-control': 'no-cache' } });
  if (!res.ok) throw new Error(`Failed to load RELEASES: ${res.status}`);

  const body = await res.text();
  const lines = body.trim().split(/\r?\n/).filter(Boolean);
  if (!lines.length) throw new Error('RELEASES is empty');

  const latest = lines.at(-1)!;
  const [, , packageName, version] = latest.split('\t');
  const installer = packageName.replace(/\.nupkg$/, '.exe');

  return {
    version,
    packageName,
    installerUrl: `${S3_BASE}/${CHANNEL}/${installer}`
  };
}
```

## 4. 兼容性与缓存策略
- 若担心 CDN 仍缓存旧 `RELEASES`，可在网页构建时注入一个手动刷新按钮，调用上方逻辑重新拉取。
- 需要提供“历史版本”页面时，可把 `RELEASES` 全量解析为数组并按版本号排序渲染列表。
- 请在页面上标注“发布通道”字样，避免用户混淆自包含版与框架依赖版。

## 5. 发布日检查清单
- [ ] 确认客户端团队已完成 `Publish-Release.ps1` 上传。
- [ ] 访问两条 `RELEASES` 链接查看最新行版本号是否一致。
- [ ] 手动打开自包含安装器链接验证文件存在并可下载。
- [ ] 如果页面使用缓存层（如 SW、CDN 自建缓存），记得刷新。
- [ ] 更新页面上的发布日期或“Last updated”字段。

完成以上步骤即可让下载页在客户端发版后快速更新，减少手动上传的重复工作。
