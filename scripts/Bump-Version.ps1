<#
.SYNOPSIS
    更新客户端版本号，并同步刷新发布指南中的示例版本号。
.DESCRIPTION
    将仓库内的版本号更新为指定的新值，包括：
    - lingualink_client.csproj 中的 Version / AssemblyVersion / FileVersion / ProductVersion
    - docs/ReleaseGuide.md 中的示例版本号与命令行示例
.PARAMETER Version
    新的版本号（例如 3.4.9）。
.PARAMETER DryRun
    仅输出将要修改的文件内容差异，不真正写入文件。
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/Bump-Version.ps1 -Version 3.4.9
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

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

Write-Info "准备将版本号更新为 $Version"

$files = @(
    'lingualink_client.csproj',
    'docs/ReleaseGuide.md'
)

foreach ($relativePath in $files) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path $path)) {
        Write-Warn "跳过（未找到文件）: $relativePath"
        continue
    }

    $content = Get-Content $path -Raw
    Write-Info "处理文件: $relativePath"

    switch -Wildcard ($relativePath) {
        'lingualink_client.csproj' {
            $assemblyVersion = "$Version.0"
            $content = $content `
                -replace '<Version>.*?</Version>', "<Version>$Version</Version>" `
                -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>" `
                -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>" `
                -replace '<ProductVersion>.*?</ProductVersion>', "<ProductVersion>$Version</ProductVersion>"
        }
        'docs/ReleaseGuide.md' {
            $content = $content `
                -replace '示例版本号以 `\d+\.\d+\.\d+` 为例', "示例版本号以 `$Version` 为例" `
                -replace 'Bump-Version\.ps1 -Version \d+\.\d+\.\d+', "Bump-Version.ps1 -Version $Version" `
                -replace '# Release Notes – \d+\.\d+\.\d+', "# Release Notes – $Version" `
                -replace 'Build-Release\.ps1 -Version \d+\.\d+\.\d+', "Build-Release.ps1 -Version $Version" `
                -replace 'Publish-Release\.ps1 -Version \d+\.\d+\.\d+', "Publish-Release.ps1 -Version $Version"
        }
    }

    if ($DryRun) {
        Write-Info "DryRun 模式：不写入修改，预览包含版本号的行："

        $lines = $content.Split("`n")
        $matches = $lines | Where-Object { $_ -match [Regex]::Escape($Version) }

        if (-not $matches -or $matches.Count -eq 0) {
            Write-Warn "预览：未找到包含 $Version 的行（可能该文件中没有直接出现该字符串）。"
        } else {
            $matches | ForEach-Object { Write-Host "    $_" }
        }
    } else {
        # 为避免文件被占用时出现部分写入或清空，先写入临时文件再原子替换
        $tempPath = "$path.bump.tmp"
        try {
            Set-Content -Path $tempPath -Value $content -Encoding UTF8
            Move-Item -Path $tempPath -Destination $path -Force
        } catch {
            if (Test-Path $tempPath) {
                Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
            }
            Write-ErrorAndExit "写入文件失败：$path - $($_.Exception.Message)"
        }
    }
}

if ($DryRun) {
    Write-Info "DryRun 完成，未对任何文件进行实际写入。"
} else {
    Write-Info "版本号更新完成，请运行：dotnet build 和 scripts/Build-Release.ps1 验证。"
}
