<#
.SYNOPSIS
    将 Build-Release.ps1 生成的 Velopack 产物同步到对象存储。
.DESCRIPTION
    读取 release-settings.json，配置 AWS 兼容凭证，并使用 aws s3 cp 上传自包含版与框架依赖版的发布目录。
.PARAMETER Version
    用于日志输出，可帮助区分上传的目标版本。
.PARAMETER ConfigPath
    指定配置文件路径，默认优先使用 %APPDATA%\LinguaLink\release-settings.json。
.PARAMETER SelfContainedOnly
    仅上传自包含通道。
.PARAMETER FrameworkOnly
    仅上传框架依赖通道。
.PARAMETER DryRun
    预演上传命令，不写入远端。
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -Version 3.4.6
#>
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

function Normalize-Prefix {
    param([string]$Prefix)

    if ([string]::IsNullOrWhiteSpace($Prefix)) {
        return ""
    }

    $normalized = $Prefix -replace '[\\]+','/'
    $segments = $normalized.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
    return ($segments -join '/')
}

function Join-UrlSegments {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [string[]]$Segments
    )

    $result = $BaseUrl.TrimEnd('/')

    foreach ($segment in $Segments) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            continue
        }

        $clean = ($segment -replace '[\\]+','/').Trim('/')
        if ($clean) {
            $result = "$result/$clean"
        }
    }

    return $result
}

function Get-PublicBaseUrls {
    param(
        [Parameter(Mandatory = $true)][Uri]$Endpoint,
        [string]$Bucket
    )

    $candidates = New-Object System.Collections.Generic.List[string]
    $baseUrl = $Endpoint.AbsoluteUri.TrimEnd('/')
    $candidates.Add($baseUrl)

    if ([string]::IsNullOrWhiteSpace($Bucket)) {
        return ($candidates | Select-Object -Unique)
    }

    $bucketTrimmed = $Bucket.Trim()
    if (-not [string]::IsNullOrWhiteSpace($bucketTrimmed)) {
        if (-not $Endpoint.Host.StartsWith("$bucketTrimmed.", [StringComparison]::OrdinalIgnoreCase)) {
            $virtualBuilder = [UriBuilder]::new($Endpoint)
            $virtualBuilder.Host = "$bucketTrimmed.$($Endpoint.Host)"
            $virtualBuilder.Path = '/'
            $candidates.Add($virtualBuilder.Uri.AbsoluteUri.TrimEnd('/'))
        }

        $pathBuilder = [UriBuilder]::new($Endpoint)
        $existingPath = $pathBuilder.Path.Trim('/')
        if (-not [string]::IsNullOrWhiteSpace($existingPath)) {
            $firstSegment = $existingPath.Split('/')[0]
            if (-not $firstSegment.Equals($bucketTrimmed, [StringComparison]::OrdinalIgnoreCase)) {
                $pathBuilder.Path = "$existingPath/$bucketTrimmed"
            }
        } else {
            $pathBuilder.Path = $bucketTrimmed
        }

        $candidates.Add($pathBuilder.Uri.AbsoluteUri.TrimEnd('/'))
    }

    return ($candidates | Where-Object { $_ } | Select-Object -Unique)
}

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

# 解析 Endpoint
try {
    $endpointUri = [Uri]$config.Endpoint
} catch {
    Write-ErrorAndExit "Endpoint 无法解析为合法 URI: $($_.Exception.Message)"
}

if (-not $endpointUri.IsAbsoluteUri) {
    Write-ErrorAndExit "Endpoint 必须是绝对地址，例如 https://example.com"
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

function Invoke-Sync {
    param(
        [string]$Source,
        [string]$Prefix,
        [Uri]$EndpointUri,
        [string]$Bucket,
        [string]$ChannelName
    )

    if (-not (Test-Path $Source)) {
        Write-Warn "跳过: 未找到 $Source"
        return
    }

    $normalizedPrefix = Normalize-Prefix $Prefix
    $target = "s3://$Bucket"
    if ($normalizedPrefix) {
        $target = "$target/$normalizedPrefix"
    }

    $args = @('s3','cp',$Source,$target,'--endpoint-url',$EndpointUri.AbsoluteUri,'--recursive','--no-verify-ssl')

    if ($Version) {
        Write-Info "上传版本 $Version 到 $target ($ChannelName)"
    } else {
        Write-Info "上传到 $target ($ChannelName)"
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

    $publicBaseUrls = Get-PublicBaseUrls -Endpoint $EndpointUri -Bucket $Bucket
    $releaseUrls = @()

    foreach ($baseUrl in $publicBaseUrls) {
        $releaseUrls += (Join-UrlSegments -BaseUrl $baseUrl -Segments @($normalizedPrefix, 'RELEASES'))
    }

    foreach ($url in ($releaseUrls | Select-Object -Unique)) {
        Write-Info "RELEASES 清单 ($ChannelName): $url"
    }

    if (-not $DryRun) {
        $verified = $false
        foreach ($url in ($releaseUrls | Select-Object -Unique)) {
            try {
                $response = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
                Write-Info "验证成功 ($ChannelName): $url (HTTP $($response.StatusCode))"
                $verified = $true
                break
            } catch {
                Write-Warn "验证失败 ($ChannelName): $url - $($_.Exception.Message)"
            }
        }

        if (-not $verified) {
            Write-Warn "未能验证候选 Release URL ($ChannelName)，请稍后手动确认。"
        }
    }
}

# 执行同步操作
try {
    if (-not $FrameworkOnly) {
        Write-Info "开始同步自包含版本..."
        Invoke-Sync -Source $scSource -Prefix $config.SelfContainedPrefix -EndpointUri $endpointUri -Bucket $config.Bucket -ChannelName '自包含'
    }

    if (-not $SelfContainedOnly) {
        Write-Info "开始同步框架依赖版本..."
        Invoke-Sync -Source $fdSource -Prefix $config.FrameworkPrefix -EndpointUri $endpointUri -Bucket $config.Bucket -ChannelName '框架依赖'
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
