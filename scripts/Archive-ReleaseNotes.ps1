<#
.SYNOPSIS
    将当前版本的 Release Notes 归档到 docs/releases 下，并可选从 Git 历史补齐前几版。
.DESCRIPTION
    - 读取仓库根目录的 RELEASENOTES.md，解析当前版本号（或使用 -Version 显式指定）。
    - 将当前内容写入 docs/releases/<Version>.md。
    - 当指定 -IncludePrevious 时，从 Git 历史中的 RELEASENOTES.md 记录中解析更早的版本，
      为每个版本生成对应的 docs/releases/<Version>.md（若不存在或指定 -Force）。
.PARAMETER Version
    当前版本号（例如 3.4.9）。若未指定，则从 RELEASENOTES.md 第一行的标题中解析。
.PARAMETER IncludePrevious
    从 Git 历史中最多补齐的“更早版本”数量（不包含当前版本），默认 0。
.PARAMETER Force
    若目标文件已存在，强制覆盖。
.PARAMETER DryRun
    仅打印将要进行的操作，不真正写文件。
.EXAMPLE
    # 只归档当前版本（从 RELEASENOTES.md 自动解析版本号）
    powershell -ExecutionPolicy Bypass -File scripts/Archive-ReleaseNotes.ps1

.EXAMPLE
    # 归档当前版本，并尝试从历史中补齐前 2 个版本
    powershell -ExecutionPolicy Bypass -File scripts/Archive-ReleaseNotes.ps1 -IncludePrevious 2

.EXAMPLE
    # 显式指定当前版本号，并在 DryRun 模式下查看将生成哪些文件
    powershell -ExecutionPolicy Bypass -File scripts/Archive-ReleaseNotes.ps1 -Version 3.4.9 -IncludePrevious 2 -DryRun
#>
param(
    [string]$Version,

    [int]$IncludePrevious = 0,

    [switch]$Force,

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

$releaseNotesPath = Join-Path $repoRoot 'RELEASENOTES.md'
if (-not (Test-Path $releaseNotesPath)) {
    Write-ErrorAndExit "未找到 RELEASENOTES.md，请在仓库根目录下运行脚本。"
}

$releaseNotesContent = Get-Content $releaseNotesPath -Raw

if (-not $Version) {
    if ($releaseNotesContent -match '#\s*Release Notes\s*–\s*(\d+\.\d+\.\d+)') {
        $Version = $matches[1]
        Write-Info "从 RELEASENOTES.md 解析到当前版本号：$Version"
    } else {
        Write-ErrorAndExit "无法从 RELEASENOTES.md 的标题解析版本号，请使用 -Version 显式指定。"
    }
} else {
    Write-Info "使用参数指定的版本号：$Version"
}

$outputDir = Join-Path $repoRoot 'docs\releases'
if (-not (Test-Path $outputDir)) {
    if ($DryRun) {
        Write-Info "DryRun 模式：将创建目录 $outputDir"
    } else {
        Write-Info "创建目录：$outputDir"
        New-Item -ItemType Directory -Path $outputDir | Out-Null
    }
}

function Write-ArchiveFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetVersion,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $targetPath = Join-Path $outputDir "$TargetVersion.md"
    $relative = "docs/releases/$TargetVersion.md"

    if (Test-Path $targetPath -and -not $Force) {
        Write-Warn "跳过（文件已存在，未指定 -Force）：$relative"
        return
    }

    if ($DryRun) {
        Write-Info "DryRun 模式：将写入 $relative"
    } else {
        Write-Info "写入归档文件：$relative"
        Set-Content -Path $targetPath -Value $Content -Encoding UTF8
    }
}

# 1. 归档当前版本
Write-ArchiveFile -TargetVersion $Version -Content $releaseNotesContent

# 2. 可选：从 Git 历史中补齐前几版
if ($IncludePrevious -gt 0) {
    $gitCmd = Get-Command git -ErrorAction SilentlyContinue
    if (-not $gitCmd) {
        Write-ErrorAndExit "IncludePrevious = $IncludePrevious，但当前环境找不到 git 命令，请安装 Git 或将其添加到 PATH。"
    }

    Write-Info "准备从 Git 历史中补齐前 $IncludePrevious 个不同版本的 Release Notes（不含当前版本 $Version）。"

    # 获取所有修改过 RELEASENOTES.md 的提交（按时间倒序）
    $commitIds = & git log --format='%H' -- RELEASENOTES.md
    if (-not $commitIds) {
        Write-Warn "未找到包含 RELEASENOTES.md 变更的 Git 提交记录，跳过历史版本归档。"
    } else {
        # 使用 HashSet 记录已经处理过的版本号，避免重复
        $seenVersions = [System.Collections.Generic.HashSet[string]]::new()
        [void]$seenVersions.Add($Version)

        $backfilled = 0

        foreach ($sha in $commitIds) {
            if ($backfilled -ge $IncludePrevious) {
                break
            }

            # 读取该提交下的 RELEASENOTES.md 内容
            $historicalContent = & git show "$sha:RELEASENOTES.md" 2>$null
            if (-not $historicalContent) {
                continue
            }

            $historicalText = $historicalContent -join "`n"

            if ($historicalText -match '#\s*Release Notes\s*–\s*(\d+\.\d+\.\d+)') {
                $historicalVersion = $matches[1]
            } else {
                # 无法解析版本号的历史内容，跳过
                continue
            }

            if ($seenVersions.Contains($historicalVersion)) {
                continue
            }

            [void]$seenVersions.Add($historicalVersion)
            Write-Info "发现历史版本 $historicalVersion（commit $sha），准备写入归档。"

            Write-ArchiveFile -TargetVersion $historicalVersion -Content $historicalText
            $backfilled++
        }

        if ($backfilled -lt $IncludePrevious) {
            Write-Warn "仅找到 $backfilled 个额外历史版本可归档（目标为 $IncludePrevious 个）。"
        }
    }
}

Write-Info "Release Notes 归档流程完成。"

