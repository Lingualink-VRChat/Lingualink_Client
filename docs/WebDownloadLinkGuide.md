# Web 前端发布下载链接指引

本指南面向维护官网/下载页的前端同学，帮助你们在 LinguaLink 客户端出新版本时，第一时间把下载按钮指向最新构建，同时保证历史链接可回溯。

## 1. 文件在哪里
- 客户端发布脚本会把产物同步到 rains3（S3 兼容）桶 `lingualink`。
- 自包含版与框架依赖版分别对应两个前缀：
  - `https://download.cn-nb1.rains3.com/lingualink/stable-self-contained/`
  - `https://download.cn-nb1.rains3.com/lingualink/stable-framework-dependent/`
- 每个前缀都会包含三类清单/产物文件：
  - `RELEASES-self-contained` / `RELEASES-framework`：Velopack 的主清单，记录版本号与 `.nupkg` 包。
  - `releases.self-contained.json` / `releases.framework.json`：包含版本、SHA、更新日志等元数据。
  - `assets.self-contained.json` / `assets.framework.json`：列出安装器、便携包等具体文件名，可直接拼成下载链接。

## 2. 如何判定“最新版本”
`RELEASES-<channelSuffix>` 文件是一个使用制表符分隔的文本清单，每行代表一个版本。最后一行即为最新版本。例如：
```
<sha1>\t<filesize>\tLinguaLinkClient-SelfContained-3.4.0-self-contained-full.nupkg\t3.4.0
```
字段含义：
- 列 1：包的 SHA1 哈希（Velopack 用于校验）。
- 列 2：文件字节数。
- 列 3：`.nupkg` 文件名。
- 列 4：语义化版本号。

前端只需读取最后一行的列 3/列 4，即可得到最新版本号与包名。

## 3. 推荐的前端实现流程
1. 根据用户所需渠道（默认推荐自包含版）选定前缀 URL，并取出对应的清单文件名（`RELEASES-self-contained` 或 `RELEASES-framework`）。
2. 以 `GET` 请求拉取该清单，注意添加 `cache-control: no-cache` 或拼接时间戳避免 CDN 缓存。
3. 把响应按换行拆分，过滤空行，选取最后一行，再以制表符分割得到 `.nupkg` 文件名和版本号。
4. 可选：根据 `releases.<suffix>.json` 获取发布说明，在下载页展示版本说明或更新日志摘要。
5. 调用 `assets.<suffix>.json` 并查找 `Type === "Installer"` 的条目，直接拼出安装器完整 URL（该文件名包含通道信息，不能简单地用 `.nupkg` 名字替换扩展名）。
6. 在页面上更新版本号、下载链接，以及“需要更小包体？”等提示，指向另一通道的安装器。

### TypeScript 示例
```ts
const S3_BASE = 'https://download.cn-nb1.rains3.com/lingualink';
const CHANNEL = 'stable-self-contained'; // 或 'stable-framework-dependent'

const MANIFESTS = {
  'stable-self-contained': {
    releases: 'RELEASES-self-contained',
    assets: 'assets.self-contained.json',
  },
  'stable-framework-dependent': {
    releases: 'RELEASES-framework',
    assets: 'assets.framework.json',
  },
} as const;

type ChannelKey = keyof typeof MANIFESTS;

async function fetchLatestInstaller(channel: ChannelKey = 'stable-self-contained') {
  const { releases, assets } = MANIFESTS[channel];
  const cacheBust = `ts=${Date.now()}`;

  const releasesUrl = `${S3_BASE}/${channel}/${releases}?${cacheBust}`;
  const res = await fetch(releasesUrl, { headers: { 'cache-control': 'no-cache' } });
  if (!res.ok) throw new Error(`Failed to load ${releases}: ${res.status}`);

  const body = await res.text();
  const lines = body.trim().split(/\r?\n/).filter(Boolean);
  if (!lines.length) throw new Error(`${releases} is empty`);

  const latest = lines.at(-1)!;
  const [, , packageName, version] = latest.split('\t');

  const assetsUrl = `${S3_BASE}/${channel}/${assets}?${cacheBust}`;
  const assetList: Array<{ RelativeFileName: string; Type: string }> = await fetch(assetsUrl, {
    headers: { 'cache-control': 'no-cache' },
  }).then((r) => (r.ok ? r.json() : Promise.reject(new Error(`Failed to load ${assets}: ${r.status}`))));

  const installer = assetList.find((item) => item.Type === 'Installer');
  if (!installer) throw new Error('Installer asset missing');

  return {
    version,
    packageName,
    installerUrl: `${S3_BASE}/${channel}/${installer.RelativeFileName}`,
  };
}
```

## 4. 兼容性与缓存策略
- 若担心 CDN 仍缓存旧清单，可在页面提供“刷新版本”按钮，重新调用上述逻辑。
- 如需展示历史版本，可把 `RELEASES-<suffix>` 全量解析为数组并结合 `releases.<suffix>.json` 渲染表格。
- 页面上请标注“自包含版 / 框架依赖版”等字样，避免用户混淆两个渠道。

## 5. 发布日检查清单
- [ ] 确认客户端团队已完成 `Publish-Release.ps1` 上传。
- [ ] 访问两个前缀的 `RELEASES-<suffix>`，核对最后一行版本号是否一致。
- [ ] 打开 `assets.<suffix>.json` 并验证安装器文件存在且可下载。
- [ ] 如果页面使用缓存层（如 Service Worker、CDN 自建缓存），记得刷新缓存。
- [ ] 更新页面上的发布日期或 “Last updated” 字段。

完成以上步骤即可让下载页在客户端发版后快速更新，减少手动上传的重复工作。
