# LinguaLink Client 发布指南

本文档记录了在本地构建并发布 Windows 客户端的推荐流程，覆盖密钥存储、打包脚本的使用以及将产物推送到对象存储（兼容 S3 的 rains3 桶）。每次发版前请完成以下准备。

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

编辑仓库根目录的 `RELEASENOTES.md`，列出本次版本改动，例如：

```markdown
- feat: 支持多语言字幕导出
- fix: 修复语言包初始化偶发崩溃
- chore: 调整 VRChat OSC 超时配置
```

Velopack 会把该文件嵌入发布包，客户端弹窗能读取最新更新内容。

## 4. 构建与打包

使用 PowerShell 脚本 `scripts/Build-Release.ps1` 自动生成自包含版和框架依赖版安装包及 Velopack 发行目录：

```powershell
# 在仓库根目录执行，-Version 可省略（默认继承 csproj 中的版本号）
powershell -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version 3.4.0
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
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -Version 3.4.0

# 仅上传自包含版本
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -SelfContainedOnly -Version 3.4.0

# 仅上传框架依赖版本
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -FrameworkOnly -Version 3.4.0
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
