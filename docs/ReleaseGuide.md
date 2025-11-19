# LinguaLink Client 发布指南

本文档记录了在本地构建并发布 Windows 客户端的推荐流程，覆盖密钥存储、打包脚本的使用以及将产物推送到对象存储（兼容 S3 的 rains3 桶）。

> 如果你只是“按惯例发一个新版本”，直接看下一节的**一条龙快速发布流程**即可；后面的章节是对各步骤的补充说明。

## 一条龙快速发布流程（推荐）

从已有版本升级到一个新版本的大致步骤如下（示例版本号以 `3.4.7 → 3.4.8` 为例）：

1. **更新版本号（可选，但推荐）**
   - 使用脚本统一修改版本号：
     ```powershell
     # 预览（不写文件）
     powershell -ExecutionPolicy Bypass -File scripts/Bump-Version.ps1 -OldVersion 3.4.7 -NewVersion 3.4.8 -DryRun

     # 确认无误后实际写入
     powershell -ExecutionPolicy Bypass -File scripts/Bump-Version.ps1 -OldVersion 3.4.7 -NewVersion 3.4.8
     ```
   - 该脚本会同步更新 `lingualink_client.csproj`、脚本示例命令、文档和 `RELEASENOTES.md` 标题中的版本号。

2. **编写多语言 Release Notes**
   - 编辑仓库根目录 `RELEASENOTES.md`，按版本 + 语言分节维护更新内容，例如：
     ```markdown
     # Release Notes – 3.4.8

     ## 简体中文 (zh-CN)
     - feat: 新增 XXX 功能
     - fix: 修复 YYY 问题

     ## English (en)
     - feat: Add XXX feature
     - fix: Fix YYY bug
     ```
   - Velopack 会把这份 Markdown 嵌入客户端更新弹窗，用户可以直接在弹窗中选择自己熟悉的语言阅读。

3. **本地构建检查**
   - 在仓库根目录执行：
     ```powershell
     dotnet build lingualink_client.csproj -c Release
     ```
   - 确认没有编译错误后再进入发布步骤。

4. **打包自包含版 + 框架依赖版**
   - 在仓库根目录执行（`-Version` 可以省略，默认使用 csproj 中的版本号）：
     ```powershell
     powershell -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version 3.4.8
     ```
   - 脚本会生成：
     - `artifacts/Releases-SelfContained`
     - `artifacts/Releases-FrameworkDependent`

5. **上传到 rains3（正式发布）**
   - 先预演上传（不真正写入远端）：
     ```powershell
     powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -DryRun -Version 3.4.8
     ```
   - 确认列表无误后，执行实际上传（两个通道都推）：
     ```powershell
     powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -Version 3.4.8
     ```
   - 只上传某一个通道时，可使用：
     ```powershell
     # 仅自包含版
     powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -SelfContainedOnly -Version 3.4.8

     # 仅框架依赖版
     powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -FrameworkOnly -Version 3.4.8
     ```

6. **发版后验证**
   - 打开客户端，确认：
     - 能正常启动并连接后端。
     - 右上角“发现新版本！”等更新提醒工作正常。
   - 访问下载地址（例如 `https://download.cn-nb1.rains3.com/lingualink/stable-self-contained/RELEASES`），确认最新一行版本号与刚发布的版本一致。

以上完成后，即视为一次完整的 Release 流程；更细节的说明请参见下文各小节。

---

## 快速脚本总览

- `scripts/Bump-Version.ps1`：在发版前统一更新仓库内的版本号字符串（csproj、脚本示例命令、文档、RELEASENOTES 标题等）。
- `scripts/Build-Release.ps1`：清理 `artifacts/` 目录，分别运行自包含与框架依赖配置的 `dotnet publish`，随后调用 `vpk pack` 生成 Velopack 发行内容。支持 `-DryRun`、`-Skip*` 等参数，适合本地快速验证或复用现有构建产物。
- `scripts/Publish-Release.ps1`：读取 release-settings 配置，检查 AWS CLI、处理互斥参数，并使用 `aws s3 cp` 将 `artifacts/` 下的发行目录同步到 rains3。`-DryRun` 可预演上传列表，命令结束后会自动清理临时凭证环境变量。

## 1. 环境准备

1. 安装 .NET SDK 8.0 及以上版本（使用 `dotnet --version` 验证）。
2. 安装 Velopack CLI：`dotnet tool install --global vpk`，然后运行 `vpk --version` 确认命令可用。如果你想使用自定义路径，可将可执行文件路径写入环境变量 `VPK_EXE`。
3. 安装 [AWS CLI v2](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html)，用于将生成的发布文件上传到 rains3 兼容的 S3 接口。
4. 在仓库根目录执行 `dotnet build lingualink_client.csproj`，确保基础构建无误。

## 2. 安全地存储 Access Key / Secret Key

不要把密钥写入 Git 仓库。推荐把真实凭证保存在当前用户的应用数据目录：

1. 在 `%APPDATA%\LinguaLink` 下创建 `release-settings.json`（目录若不存在可手动新建）。
2. 文件示例：

   ```json
   {
     "AccessKey": "你的真实AccessKey",
     "SecretKey": "你的真实SecretKey",
     "Endpoint": "https://cn-nb1.rains3.com",
     "Bucket": "lingualink",
     "SelfContainedPrefix": "stable-self-contained",
     "FrameworkPrefix": "stable-framework-dependent"
   }
   ```

3. 若想把配置放在仓库附近，可把 `scripts/release-settings.sample.json` 复制为 `scripts/release-settings.json` 并填好数据——该文件已在 `.gitignore` 中忽略，不会被提交。
4. 每次轮换密钥后请及时更新此文件，并在 rains3 控制台吊销旧的密钥对。

## 3. 生成更新日志

编辑仓库根目录的 `RELEASENOTES.md`，列出本次版本改动。为了兼顾多语言用户，推荐在同一个文件中使用多语言小节，例如：

```markdown
# Release Notes – 3.4.7

## 简体中文 (zh-CN)
- feat: 支持多语言字幕导出
- fix: 修复语言包初始化偶发崩溃

## English (en)
- feat: Support multilingual subtitle export
- fix: Fix intermittent crash during language pack initialization
```

Velopack 会把该文件嵌入发布包，客户端更新弹窗会原样展示整份 Markdown，用户可以在其中找到自己熟悉的语言。

## 4. 构建与打包

使用 PowerShell 脚本 `scripts/Build-Release.ps1` 自动生成自包含版和框架依赖版安装包及 Velopack 发行目录：

```powershell
# 在仓库根目录执行，-Version 可省略（默认继承 csproj 中的版本号）
powershell -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version 3.4.7
```

脚本会：

1. 清理 `artifacts/` 目录并重新创建需要的子目录。
2. 分别运行 `dotnet publish`（`ReleaseSelfContained` / `ReleaseFrameworkDependent` 配置）。
3. 调用 `vpk pack` 生成 Velopack 发行目录，结果存放在：
   - `artifacts/Releases-SelfContained`
   - `artifacts/Releases-FrameworkDependent`

常用参数：

- `-DryRun`：仅打印将执行的命令，不真正运行。
- `-SkipPublish`：跳过 `dotnet publish`，仅打包已有产物。
- `-SkipPackage`：只做 `dotnet publish`，不运行 Velopack。
- `-SkipSelfContained` / `-SkipFrameworkDependent`：跳过指定通道。

## 5. 上传到 rains3 桶

构建完成后，运行 `scripts/Publish-Release.ps1` 将产物同步到对象存储：

```powershell
# 预演（查看将上传的文件但不真正执行）
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -DryRun

# 正式上传两个通道
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -Version 3.4.7

# 仅上传自包含版本
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -SelfContainedOnly -Version 3.4.7

# 仅上传框架依赖版本
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -FrameworkOnly -Version 3.4.7
```

脚本说明：

- 若未显式传入 `-ConfigPath`，会优先读取 `%APPDATA%\LinguaLink\release-settings.json`，否则回退到 `scripts/release-settings.json`。
- 运行期间会临时设置 `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` 等环境变量，命令结束后自动清理。
- 为兼容 rains3 等第三方 S3 端点，脚本会自动设置 `AWS_REQUEST_CHECKSUM_CALCULATION=WHEN_REQUIRED` 与 `AWS_RESPONSE_CHECKSUM_VALIDATION=WHEN_REQUIRED`，避免 AWS CLI 新版默认 CRC 校验导致上传失败。
- 真实上传命令为 `aws s3 cp artifacts/<目录> s3://{Bucket}/{Prefix}/ --endpoint-url {Endpoint} --recursive --no-verify-ssl`，脚本会自动规范化前缀首尾斜杠并保持远端目录结构一致。
- `-DryRun` 可先确认差异，再去掉该参数执行真实上传。

上传完成后，可访问 `https://download.cn-nb1.rains3.com/lingualink/stable-self-contained/RELEASES`

## 6. 客户端验证

1. 运行 `dotnet run --project lingualink_client.csproj -c Debug` 做基础功能检查。
2. 安装自包含版（`Releases-SelfContained` 下的安装包）并确认：
   - 程序根据构建配置选择正确的更新通道。
   - 主界面右上角 “发现新版本！” 按钮可正常弹出更新提示并完成下载。
   - 更新完成后 `RELEASES` 中的版本号与当前版本一致。
3. 对于对外发布，请提供安装包及 `RELEASENOTES.md` 给 QA / 运营验证。

## 7. 故障排查

| 场景 | 解决方案 |
| ---- | -------- |
| `Build-Release.ps1` 找不到 Velopack CLI | 确认已安装 `vpk`，或设置 `VPK_EXE` 指向 Velopack CLI 完整路径后重试。 |
| `Publish-Release.ps1` 提示 `aws` 不存在 | 安装 AWS CLI v2，并重新打开终端确保 PATH 生效。 |
| `aws s3 cp` 返回 401/403 | 检查 `release-settings.json` 中的 AccessKey/SecretKey 是否有效，必要时在控制台重新生成密钥。 |
| `aws s3 cp` 返回 InvalidArgument/"invalid checksum" | 确保使用当前仓库的 `Publish-Release.ps1`（已内置校验和兼容逻辑），或在执行 `aws s3 cp` 前手动设置 `AWS_REQUEST_CHECKSUM_CALCULATION=WHEN_REQUIRED` 与 `AWS_RESPONSE_CHECKSUM_VALIDATION=WHEN_REQUIRED` 后重试。 |
| 客户端不弹更新提示 | 确认 `RELEASES` 最新版本号大于当前版本，并检查 `App.xaml.cs` 中的更新 URL 是否指向对应目录。 |
| 访问下载域名仍是旧版本 | rains3 CDN 可能缓存滞后，可刷新缓存或直接访问源站路径确认。 |

---

完成以上步骤，即可完成 LinguaLink Client 的手动构建与发布流程。后续可将 `scripts/Build-Release.ps1` 与 `Publish-Release.ps1` 融入 CI/CD，并改用平台密钥管理以实现全自动发布。
