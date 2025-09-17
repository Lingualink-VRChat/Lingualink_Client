param(
    [string]$Version,
    [string]$ConfigPath,
    [switch]$SelfContainedOnly,
    [switch]$FrameworkOnly,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Write-Warn($message) { Write-Host "[WARN] $message" -ForegroundColor Yellow }
function Write-ErrorAndExit($message, $code = 1) { Write-Host "[ERROR] $message" -ForegroundColor Red; exit $code }

# 获取脚本和仓库根目录
$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent $scriptRoot

# 设置默认配置文件路径
$defaultConfig = Join-Path $env:APPDATA "LinguaLink\release-settings.json"
if (-not $ConfigPath) {
    $ConfigPath = if (Test-Path $defaultConfig) { $defaultConfig } else { Join-Path $scriptRoot "release-settings.json" }
}

# 检查配置文件是否存在
if (-not (Test-Path $ConfigPath)) {
    Write-ErrorAndExit "配置文件未找到: $ConfigPath。请先复制 scripts/release-settings.sample.json 并填入密钥，或在 %APPDATA%\LinguaLink 下创建 release-settings.json。"
}

# 解析配置文件
try {
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
} catch {
    Write-ErrorAndExit "无法解析配置文件 ${ConfigPath}: $($_.Exception.Message)"
}

# 检查必需的配置项
$required = @('AccessKey','SecretKey','Endpoint','Bucket','SelfContainedPrefix','FrameworkPrefix')
foreach ($key in $required) {
    if (-not $config.$key) {
        Write-ErrorAndExit "配置项 $key 缺失，请在 $ConfigPath 中补充。"
    }
}

# 参数验证
if ($SelfContainedOnly -and $FrameworkOnly) {
    Write-ErrorAndExit "SelfContainedOnly 与 FrameworkOnly 参数不可同时使用，请选择其一。"
}

# 检查 AWS CLI 是否可用
if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    Write-ErrorAndExit "未检测到 aws CLI。请安装 AWS CLI v2 并确保命令可用。"
}

# 设置目录路径
$artifactRoot = Join-Path $repoRoot 'artifacts'
$scSource = Join-Path $artifactRoot 'Releases-SelfContained'
$fdSource = Join-Path $artifactRoot 'Releases-FrameworkDependent'

# 检查构建产物目录是否存在
if (-not (Test-Path $artifactRoot)) {
    Write-ErrorAndExit "未找到构建产物目录 $artifactRoot，请先运行 scripts/Build-Release.ps1。"
}

# 设置AWS环境变量
$env:AWS_ACCESS_KEY_ID = $config.AccessKey
$env:AWS_SECRET_ACCESS_KEY = $config.SecretKey
if ($config.PSObject.Properties.Match('SessionToken').Count -gt 0 -and $config.SessionToken) {
    $env:AWS_SESSION_TOKEN = $config.SessionToken
}
if ($config.PSObject.Properties.Match('Region').Count -gt 0 -and $config.Region) {
    $env:AWS_DEFAULT_REGION = $config.Region
}

# 避免 S3 兼容端点因请求校验和失败
if (-not $env:AWS_REQUEST_CHECKSUM_CALCULATION) {
    $env:AWS_REQUEST_CHECKSUM_CALCULATION = 'WHEN_REQUIRED'
}
if (-not $env:AWS_RESPONSE_CHECKSUM_VALIDATION) {
    $env:AWS_RESPONSE_CHECKSUM_VALIDATION = 'WHEN_REQUIRED'
}

# 同步函数
function Invoke-Sync {
    param(
        [string]$Source,
        [string]$Prefix
    )
    
    if (-not (Test-Path $Source)) {
        Write-Warn "跳过: 未找到 $Source"
        return
    }
    
    # 规范化前缀，确保以斜杠结尾，避免部分 S3 兼容端在 ListObjectsV2 时返回 NoSuchKey
    $prefixNormalized = $Prefix
    if ($null -ne $prefixNormalized) {
        $prefixNormalized = ($prefixNormalized -replace '^[\\/]+','') -replace '[\\/]+$',''
        if ($prefixNormalized -ne "" -and -not $prefixNormalized.EndsWith('/')) {
            $prefixNormalized += '/'
        }
    }

    $target = "s3://$($config.Bucket)/$prefixNormalized"
    # 使用 cp 命令避免 sync 的校验和问题
    $args = @('s3','cp',$Source,$target,'--endpoint-url',$config.Endpoint,'--recursive','--no-verify-ssl')
    
    if ($Version) {
        Write-Info "上传版本 $Version 到 $target"
    } else {
        Write-Info "上传到 $target"
    }
    
    if ($DryRun) {
        $args += '--dryrun'
        Write-Info "DryRun 模式：未实际上传。"
    }
    
    try {
        $process = Start-Process -FilePath 'aws' -ArgumentList $args -NoNewWindow -Wait -PassThru
        if ($process.ExitCode -ne 0) {
            throw "aws s3 cp 失败 (exit code $($process.ExitCode))"
        }
    } catch {
        throw "同步到 $target 时失败: $($_.Exception.Message)"
    }
}

# 执行同步操作
try {
    if (-not $FrameworkOnly) {
        Write-Info "开始同步自包含版本..."
        Invoke-Sync -Source $scSource -Prefix $config.SelfContainedPrefix
    }
    
    if (-not $SelfContainedOnly) {
        Write-Info "开始同步框架依赖版本..."
        Invoke-Sync -Source $fdSource -Prefix $config.FrameworkPrefix
    }
    
    Write-Host "[SUCCESS] 发布完成。" -ForegroundColor Green
    exit 0
} catch {
    Write-ErrorAndExit $_.Exception.Message
} finally {
    # 清理AWS环境变量
    Remove-Item Env:AWS_ACCESS_KEY_ID -ErrorAction SilentlyContinue
    Remove-Item Env:AWS_SECRET_ACCESS_KEY -ErrorAction SilentlyContinue
    Remove-Item Env:AWS_SESSION_TOKEN -ErrorAction SilentlyContinue
    Remove-Item Env:AWS_DEFAULT_REGION -ErrorAction SilentlyContinue
    Remove-Item Env:AWS_REQUEST_CHECKSUM_CALCULATION -ErrorAction SilentlyContinue
    Remove-Item Env:AWS_RESPONSE_CHECKSUM_VALIDATION -ErrorAction SilentlyContinue
}

