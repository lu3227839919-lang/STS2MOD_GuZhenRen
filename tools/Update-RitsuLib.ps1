[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$GameDir,
    [string]$Version = "0.5.8",
    [switch]$Latest
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-ProjectGameDir {
    param([string]$ProjectRoot)

    $propsPath = Join-Path $ProjectRoot "local.props"
    if (Test-Path $propsPath) {
        try {
            [xml]$props = Get-Content $propsPath -Raw
            $value = $props.Project.PropertyGroup.Sts2Dir |
                Where-Object { $_ } |
                Select-Object -First 1
            if ($value) {
                return [string]$value
            }
        }
        catch {
            Write-Warning "无法读取 local.props：$($_.Exception.Message)"
        }
    }

    $common = @(
        "D:\SteamLibrary\steamapps\common\Slay the Spire 2",
        "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
    )

    return $common | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Get-ManifestVersion {
    param([string]$Directory)

    $manifest = Get-ChildItem $Directory -File -Recurse |
        Where-Object {
            $_.Name -in @("mod_manifest.json", "STS2-RitsuLib.json")
        } |
        Select-Object -First 1

    if (-not $manifest) {
        return $null
    }

    try {
        return (Get-Content $manifest.FullName -Raw | ConvertFrom-Json).version
    }
    catch {
        return $null
    }
}

$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $GameDir) {
    $GameDir = Get-ProjectGameDir -ProjectRoot $projectRoot
}

if (-not $GameDir -or -not (Test-Path $GameDir)) {
    throw "未找到游戏目录。请使用 -GameDir 指定 Slay the Spire 2 安装目录。"
}

$releaseUri = if ($Latest) {
    "https://api.github.com/repos/BAKAOLC/STS2-RitsuLib/releases/latest"
}
else {
    "https://api.github.com/repos/BAKAOLC/STS2-RitsuLib/releases/tags/v$Version"
}

Write-Host "读取 RitsuLib 发布信息：$releaseUri"
$headers = @{
    "Accept" = "application/vnd.github+json"
    "User-Agent" = "GuZhenRenPersonal-RitsuLib-Updater"
    "X-GitHub-Api-Version" = "2022-11-28"
}
$release = Invoke-RestMethod -Uri $releaseUri -Headers $headers

if ($release.draft -or $release.prerelease) {
    throw "目标版本不是稳定正式版：$($release.tag_name)"
}

$zipAssets = @($release.assets | Where-Object {
    $_.name -match '\.zip$'
})

# Variant pack 会根据游戏 API 自动选择兼容 DLL，是最安全的运行时安装包。
$asset = $zipAssets | Where-Object {
    $_.name -match '(?i)variant[-_. ]?pack'
} | Select-Object -First 1

# 如果某个版本没有 variant pack，则只接受明确标注 0.110.x 的兼容包。
if (-not $asset) {
    $asset = $zipAssets | Where-Object {
        $_.name -match '(?i)(compat.*0[._-]110|0[._-]110.*compat)'
    } | Select-Object -First 1
}

if (-not $asset) {
    $names = ($zipAssets | ForEach-Object name) -join "`n  - "
    throw @"
发布 $($release.tag_name) 中没有找到 variant pack 或 0.110.x 兼容包。
为避免安装错误游戏 API 的 DLL，更新已中止。
可用 ZIP：
  - $names
"@
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) (
    "RitsuLib-update-" + [Guid]::NewGuid().ToString("N")
)
$archive = Join-Path $tempRoot $asset.name
$extractDir = Join-Path $tempRoot "extract"
$installDir = Join-Path $GameDir "mods\STS2-RitsuLib"
$backupDir = "$installDir.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

New-Item $tempRoot -ItemType Directory -Force | Out-Null
New-Item $extractDir -ItemType Directory -Force | Out-Null

try {
    Write-Host "下载 $($asset.name)……"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive -Headers $headers

    if ($asset.digest -and $asset.digest -match '^sha256:(.+)$') {
        $expectedHash = $Matches[1].ToLowerInvariant()
        $actualHash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash) {
            throw "下载文件 SHA-256 不匹配。预期 $expectedHash，实际 $actualHash。"
        }
        Write-Host "SHA-256 校验通过。"
    }

    Expand-Archive $archive -DestinationPath $extractDir -Force

    $dll = Get-ChildItem $extractDir -Filter "STS2-RitsuLib.dll" -File -Recurse |
        Sort-Object { $_.FullName.Length } |
        Select-Object -First 1

    if (-not $dll) {
        throw "压缩包中未找到 STS2-RitsuLib.dll。"
    }

    $sourceDir = $dll.Directory.FullName
    $sourceVersion = Get-ManifestVersion -Directory $sourceDir

    if ($PSCmdlet.ShouldProcess($installDir, "安装 RitsuLib $sourceVersion")) {
        if (Test-Path $installDir) {
            Copy-Item $installDir $backupDir -Recurse -Force
            Write-Host "旧版已备份到：$backupDir"
            Remove-Item $installDir -Recurse -Force
        }

        New-Item $installDir -ItemType Directory -Force | Out-Null
        Copy-Item (Join-Path $sourceDir "*") $installDir -Recurse -Force
    }

    $installedVersion = Get-ManifestVersion -Directory $installDir
    Write-Host "RitsuLib 更新完成：$installedVersion"
    Write-Host "安装目录：$installDir"
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
