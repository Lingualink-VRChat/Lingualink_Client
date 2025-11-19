<#
.SYNOPSIS
    批量更新客户端版本号相关字符串。
.DESCRIPTION
    将仓库内的版本号从旧值更新为新值，包括：
    - lingualink_client.csproj 中的 Version / AssemblyVersion / FileVersion / ProductVersion
    - scripts/Build-Release.ps1 中的默认 Version 和示例命令
    - scripts/Publish-Release.ps1 中的示例命令
    - docs/ReleaseGuide.md 和 docs/reference/WebDownloadLinkGuide.md 中的示例版本
    - RELEASENOTES.md 标题中的版本号
.PARAMETER OldVersion
    旧的版本号（例如 3.4.7），用于做安全校验。
.PARAMETER NewVersion
    新的版本号（例如 3.4.8）。
.PARAMETER DryRun
    仅输出将要修改的文件内容差异，不真正写入文件。
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/Bump-Version.ps1 -OldVersion 3.4.7 -NewVersion 3.4.8
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$OldVersion,

    [Parameter(Mandatory = $true)]
    [string]$NewVersion,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($message) { Write-Host "[INFO] $message" -ForegroundColor Cyan }
function Write-Warn($message) { Write-Host "[WARN] $message" -ForegroundColor Yellow }
function Write-ErrorAndExit($message, $code = 1) { Write-Host "[ERROR] $message" -ForegroundColor Red; exit $code }

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = Split-Path -Parent $scriptDir
Set-Location $repoRoot

Write-Info "准备将版本号从 $OldVersion 更新为 $NewVersion"

$files = @(
    'lingualink_client.csproj',
    'RELEASENOTES.md',
    'scripts/Build-Release.ps1',
    'scripts/Publish-Release.ps1',
    'docs/ReleaseGuide.md',
    'docs/reference/WebDownloadLinkGuide.md'
)

foreach ($relativePath in $files) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path $path)) {
        Write-Warn "跳过（未找到文件）: $relativePath"
        continue
    }

    $content = Get-Content $path -Raw
    if ($content -notmatch [Regex]::Escape($OldVersion)) {
        Write-Warn "跳过（未发现旧版本号 $OldVersion）: $relativePath"
        continue
    }

    Write-Info "处理文件: $relativePath"

    switch -Wildcard ($relativePath) {
        'lingualink_client.csproj' {
            $assemblyVersion = "$NewVersion.0"
            $content = $content `
                -replace '<Version>.*?</Version>', "<Version>$NewVersion</Version>" `
                -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>" `
                -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>" `
                -replace '<ProductVersion>.*?</ProductVersion>', "<ProductVersion>$NewVersion</ProductVersion>"
        }
        'RELEASENOTES.md' {
            $pattern = '(Release Notes – )\d+\.\d+\.\d+'
            $replacement = "`${1}$NewVersion"
            $content = [Regex]::Replace($content, $pattern, { param($m) "Release Notes – $NewVersion" })
        }
        'scripts/Build-Release.ps1' {
            $content = $content `
                -replace 'powershell -ExecutionPolicy Bypass -File scripts/Build-Release\.ps1 -Version \d+\.\d+\.\d+', "powershell -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version $NewVersion" `
                -replace '\[string\]\$Version = "\d+\.\d+\.\d+"', "[string]`$Version = `"$NewVersion`""
        }
        'scripts/Publish-Release.ps1' {
            $content = $content `
                -replace 'powershell -ExecutionPolicy Bypass -File scripts/Publish-Release\.ps1 -Version \d+\.\d+\.\d+', "powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1 -Version $NewVersion"
        }
        'docs/ReleaseGuide.md' {
            $content = $content `
                -replace 'Build-Release\.ps1 -Version \d+\.\d+\.\d+', "Build-Release.ps1 -Version $NewVersion" `
                -replace 'Publish-Release\.ps1 -Version \d+\.\d+\.\d+', "Publish-Release.ps1 -Version $NewVersion"
        }
        'docs/reference/WebDownloadLinkGuide.md' {
            $content = $content `
                -replace 'LinguaLinkClient-SelfContained-\d+\.\d+\.\d+-self-contained-full\.nupkg', "LinguaLinkClient-SelfContained-$NewVersion-self-contained-full.nupkg" `
                -replace '\t\d+\.\d+\.\d+', "`t$NewVersion"
        }
    }

    if ($DryRun) {
        Write-Info "DryRun 模式：不写入修改，仅显示预览差异（前 5 行）"
        $preview = $content.Split("`n") | Select-Object -First 5
        $preview | ForEach-Object { Write-Host "    $_" }
    } else {
        Set-Content -Path $path -Value $content -Encoding UTF8
    }
}

if ($DryRun) {
    Write-Info "DryRun 完成，未对任何文件进行实际写入。"
} else {
    Write-Info "版本号更新完成，请运行：dotnet build 和 scripts/Build-Release.ps1 验证。"
}
