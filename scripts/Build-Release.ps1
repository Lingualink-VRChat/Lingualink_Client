<#
.SYNOPSIS
    构建并打包 LinguaLink Client 的 Velopack 发行目录。
.DESCRIPTION
    清理 artifacts 目录、运行 dotnet publish（自包含与框架依赖配置），并使用 vpk 生成对应的 Velopack 发布包。
.PARAMETER Version
    覆盖默认版本号，缺省时使用脚本内配置。
.PARAMETER SkipSelfContained
    跳过自包含版本的发布与打包。
.PARAMETER SkipFrameworkDependent
    跳过框架依赖版本的发布与打包。
.PARAMETER SkipPublish
    跳过 dotnet publish，仅打包已有输出。
.PARAMETER SkipPackage
    跳过 Velopack 打包，仅执行 dotnet publish。
.PARAMETER DryRun
    仅打印将执行的命令，不实际运行。
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/Build-Release.ps1 -Version 3.4.6
#>
param(
    [string]$Version = "3.4.6",
    [switch]$SkipSelfContained,
    [switch]$SkipFrameworkDependent,
    [switch]$SkipPublish,
    [switch]$SkipPackage,
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

$projectPath = Join-Path $repoRoot 'lingualink_client.csproj'
if (-not (Test-Path $projectPath)) {
    Write-ErrorAndExit "未找到项目文件: $projectPath"
}

$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishSelfDir = Join-Path (Join-Path $artifactsRoot 'ReleaseSelfContained') 'publish'
$publishFxDir = Join-Path (Join-Path $artifactsRoot 'ReleaseFrameworkDependent') 'publish'
$releaseSelfDir = Join-Path $artifactsRoot 'Releases-SelfContained'
$releaseFxDir = Join-Path $artifactsRoot 'Releases-FrameworkDependent'

if (-not $SkipPublish -and -not $DryRun) {
    if (Test-Path $artifactsRoot) {
        Write-Info "清理旧的 artifacts 目录"
        Remove-Item $artifactsRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishSelfDir, $publishFxDir, $releaseSelfDir, $releaseFxDir | Out-Null
}

$packIdBase = 'LinguaLinkClient'
$packTitle = 'LinguaLink Client'
$packAuthors = 'LinguaLink'
$mainExe = 'lingualink_client.exe'
$releaseNotesPath = Join-Path $repoRoot 'RELEASENOTES.md'
if (-not (Test-Path $releaseNotesPath)) {
    Write-ErrorAndExit "缺少 RELEASENOTES.md"
}

if ($env:VPK_EXE) {
    $vpkExe = $env:VPK_EXE
    if (-not (Test-Path $vpkExe)) {
        Write-ErrorAndExit "VPK_EXE 指向的文件不存在: $vpkExe"
    }
} else {
    $cmdInfo = Get-Command vpk -ErrorAction SilentlyContinue
    if ($cmdInfo) {
        $vpkExe = $cmdInfo.Source
    } else {
        Write-ErrorAndExit "未找到 vpk CLI。请先安装 'dotnet tool install --global vpk' 或设置 VPK_EXE"
    }
}

function Invoke-CommandSafe([string]$command, [string[]]$arguments) {
    Write-Info "执行: $command $($arguments -join ' ')"
    if ($DryRun) {
        Write-Info "DryRun 模式：不执行命令"
        return
    }

    & $command @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-ErrorAndExit "命令失败: $command $($arguments -join ' ') (ExitCode=$exitCode)" $exitCode
    }
}

if (-not $SkipPublish) {
    if (-not $SkipSelfContained) {
        Write-Info "dotnet publish 自包含版本"
        Invoke-CommandSafe 'dotnet' @('publish', $projectPath, '-c', 'ReleaseSelfContained', '-r', 'win-x64', '--self-contained', 'true', "-p:Version=$Version", '-o', $publishSelfDir)
    }
    if (-not $SkipFrameworkDependent) {
        Write-Info "dotnet publish 框架依赖版本"
        Invoke-CommandSafe 'dotnet' @('publish', $projectPath, '-c', 'ReleaseFrameworkDependent', '-r', 'win-x64', '--self-contained', 'false', "-p:Version=$Version", '-o', $publishFxDir)
    }
}

if (-not $SkipPackage) {
    function Build-VpkArgs([string]$packId, [string]$outputDir, [string]$sourceDir, [string]$channel) {
        @(
            'pack',
            '--skipVeloAppCheck',
            '--packId', $packId,
            '--packVersion', $Version,
            '--packDir', $sourceDir,
            '--runtime', 'win-x64',
            '--channel', $channel,
            '--outputDir', $outputDir,
            '--mainExe', $mainExe,
            '--packTitle', $packTitle,
            '--packAuthors', $packAuthors,
            '--releaseNotes', $releaseNotesPath,
            '--icon', (Join-Path $repoRoot 'Assets\Icons\icon_128x128.ico')
        )
    }

    if (-not $SkipSelfContained) {
        Write-Info "Velopack 打包自包含版本"
        $args = Build-VpkArgs "$packIdBase-SelfContained" $releaseSelfDir $publishSelfDir 'self-contained'
        Invoke-CommandSafe $vpkExe $args

        $generatedManifest = Join-Path $releaseSelfDir 'RELEASES-self-contained'
        $targetManifest = Join-Path $releaseSelfDir 'RELEASES'
        if (Test-Path $generatedManifest) {
            Write-Info "重命名 $generatedManifest 为 $targetManifest"
            if ($DryRun) {
                Write-Info "DryRun 模式：不执行重命名"
            } else {
                Move-Item -Path $generatedManifest -Destination $targetManifest -Force
            }
        } else {
            Write-Warn "未找到生成的清单文件: $generatedManifest"
        }
    }

    if (-not $SkipFrameworkDependent) {
        Write-Info "Velopack 打包框架依赖版本"
        $args = Build-VpkArgs "$packIdBase-Framework" $releaseFxDir $publishFxDir 'framework'
        Invoke-CommandSafe $vpkExe $args

        $generatedManifest = Join-Path $releaseFxDir 'RELEASES-framework'
        $targetManifest = Join-Path $releaseFxDir 'RELEASES'
        if (Test-Path $generatedManifest) {
            Write-Info "重命名 $generatedManifest 为 $targetManifest"
            if ($DryRun) {
                Write-Info "DryRun 模式：不执行重命名"
            } else {
                Move-Item -Path $generatedManifest -Destination $targetManifest -Force
            }
        } else {
            Write-Warn "未找到生成的清单文件: $generatedManifest"
        }
    }
}

Write-Info "Artifacts generated in $artifactsRoot"

