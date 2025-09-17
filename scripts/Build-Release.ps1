param(
    [string]$Version = "3.3.0",
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
    Write-ErrorAndExit "未找到項目文件: $projectPath"
}

$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishSelfDir = Join-Path (Join-Path $artifactsRoot 'ReleaseSelfContained') 'publish'
$publishFxDir = Join-Path (Join-Path $artifactsRoot 'ReleaseFrameworkDependent') 'publish'
$releaseSelfDir = Join-Path $artifactsRoot 'Releases-SelfContained'
$releaseFxDir = Join-Path $artifactsRoot 'Releases-FrameworkDependent'

if (-not $SkipPublish -and -not $DryRun) {
    if (Test-Path $artifactsRoot) {
        Write-Info "清理舊的 artifacts 目錄"
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
        Write-ErrorAndExit "未找到 vpk CLI。請先安裝 'dotnet tool install --global vpk' 或設置 VPK_EXE"
    }
}

function Invoke-CommandSafe([string]$command, [string[]]$arguments) {
    Write-Info "執行: $command $($arguments -join ' ')"
    if ($DryRun) {
        Write-Info "DryRun 模式：不執行命令"
        return
    }

    & $command @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-ErrorAndExit "命令失敗: $command $($arguments -join ' ') (ExitCode=$exitCode)" $exitCode
    }
}

if (-not $SkipPublish) {
    if (-not $SkipSelfContained) {
        Write-Info "dotnet publish 自包含版本"
        Invoke-CommandSafe 'dotnet' @('publish', $projectPath, '-c', 'ReleaseSelfContained', '-r', 'win-x64', '--self-contained', 'true', "-p:Version=$Version", '-o', $publishSelfDir)
    }
    if (-not $SkipFrameworkDependent) {
        Write-Info "dotnet publish 框架依賴版本"
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
    }

    if (-not $SkipFrameworkDependent) {
        Write-Info "Velopack 打包框架依賴版本"
        $args = Build-VpkArgs "$packIdBase-Framework" $releaseFxDir $publishFxDir 'framework'
        Invoke-CommandSafe $vpkExe $args
    }
}

Write-Info "Artifacts generated in $artifactsRoot"
