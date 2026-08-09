[CmdletBinding()]
param(
    [string]$Sts2Dir = "",
    [string]$GodotExe = "",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ProjectRoot = Split-Path -Parent $PSScriptRoot

function Get-LocalPropsValue {
    param([string]$Name)

    $PropsPath = Join-Path $ProjectRoot "local.props"
    if (-not (Test-Path $PropsPath -PathType Leaf)) {
        return $null
    }

    try {
        [xml]$Props = Get-Content $PropsPath -Raw
        return $Props.Project.PropertyGroup.$Name |
            Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
            Select-Object -First 1
    }
    catch {
        Write-Warning "无法读取 local.props：$($_.Exception.Message)"
        return $null
    }
}

function Resolve-Sts2Dir {
    param([string]$ExplicitPath)

    $Candidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $Candidates.Add($ExplicitPath)
    }

    $FromProps = Get-LocalPropsValue -Name "Sts2Dir"
    if ($FromProps) {
        $Candidates.Add([string]$FromProps)
    }

    if (Test-Path "HKCU:\Software\Valve\Steam") {
        $SteamPath = (Get-ItemProperty "HKCU:\Software\Valve\Steam").SteamPath
        if ($SteamPath) {
            $Candidates.Add((Join-Path $SteamPath "steamapps\common\Slay the Spire 2"))
        }
    }

    $Candidates.Add("D:\SteamLibrary\steamapps\common\Slay the Spire 2")
    $Candidates.Add("C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2")

    return $Candidates |
        Where-Object { Test-Path $_ -PathType Container } |
        Select-Object -First 1
}

function Resolve-GodotExe {
    param([string]$ExplicitPath)

    $Candidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $Candidates.Add($ExplicitPath)
    }

    $FromProps = Get-LocalPropsValue -Name "GodotExe"
    if ($FromProps) {
        $Candidates.Add([string]$FromProps)
    }

    foreach ($CommandName in @("godot4", "godot")) {
        $Command = Get-Command $CommandName -ErrorAction SilentlyContinue
        if ($Command) {
            $Candidates.Add($Command.Source)
        }
    }

    return $Candidates |
        Where-Object { Test-Path $_ -PathType Leaf } |
        Select-Object -First 1
}

function Assert-RequiredFile {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path $Path -PathType Leaf)) {
        throw "缺少 $Description：$Path"
    }
}

$ResolvedSts2Dir = Resolve-Sts2Dir -ExplicitPath $Sts2Dir
if (-not $ResolvedSts2Dir) {
    throw "未找到《杀戮尖塔 2》安装目录。请使用 -Sts2Dir 指定路径。"
}
$ResolvedSts2Dir = [System.IO.Path]::GetFullPath($ResolvedSts2Dir)

$ResolvedGodotExe = Resolve-GodotExe -ExplicitPath $GodotExe
if (-not $ResolvedGodotExe) {
    throw "未找到 Godot 4.5.1 .NET。请使用 -GodotExe 指定可执行文件。"
}
$ResolvedGodotExe = [System.IO.Path]::GetFullPath($ResolvedGodotExe)

$DotNet = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $DotNet) {
    throw "未找到 .NET SDK。请安装 .NET 9 SDK 并确保 dotnet 位于 PATH。"
}

$SdkVersion = (& $DotNet.Source --version).Trim()
$SdkMajor = [int]($SdkVersion -split '\.')[0]
if ($SdkMajor -lt 9) {
    throw "需要 .NET 9 SDK；当前版本为 $SdkVersion。"
}

$DataDir = Join-Path $ResolvedSts2Dir "data_sts2_windows_x86_64"
Assert-RequiredFile (Join-Path $DataDir "sts2.dll") "游戏程序集 sts2.dll"
Assert-RequiredFile (Join-Path $DataDir "0Harmony.dll") "Harmony 程序集"
Assert-RequiredFile (Join-Path $DataDir "Steamworks.NET.dll") "Steamworks.NET 程序集"

$ModsDir = Join-Path $ResolvedSts2Dir "mods"
$RitsuDll = Join-Path $ModsDir "STS2-RitsuLib\STS2-RitsuLib.dll"
Assert-RequiredFile $RitsuDll "STS2-RitsuLib 0.5.11+ 运行时"

$ManifestPath = Join-Path $ProjectRoot "GuZhenRen.json"
$Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$Version = [string]$Manifest.version
$SolutionPath = Join-Path $ProjectRoot "GuZhenRen.sln"

Write-Host "构建 GuZhenRen $Version ($Configuration)"
Write-Host "游戏目录：$ResolvedSts2Dir"
Write-Host "Godot：$ResolvedGodotExe"
$BuildArguments = @(
    "build",
    $SolutionPath,
    "-c", $Configuration,
    "-p:Sts2Dir=$ResolvedSts2Dir",
    "-p:Sts2DataDir=$DataDir",
    "-p:GodotExe=$ResolvedGodotExe",
    "-p:CopyModOnBuild=true",
    "-p:RunPckExport=true"
)

& $DotNet.Source @BuildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build 失败，退出代码 $LASTEXITCODE。"
}

$ModDir = Join-Path $ModsDir "GuZhenRen"
$RequiredArtifacts = @(
    (Join-Path $ModDir "GuZhenRen.dll"),
    (Join-Path $ModDir "GuZhenRen.pck"),
    (Join-Path $ModDir "GuZhenRen.json")
)
foreach ($Artifact in $RequiredArtifacts) {
    Assert-RequiredFile $Artifact "构建产物"
}

$ArtifactDir = Join-Path $ProjectRoot "artifacts"
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ArtifactDir ("GuZhenRen-{0}-Windows.zip" -f $Version)
}
$ResolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$OutputDirectory = Split-Path $ResolvedOutputPath -Parent
New-Item $OutputDirectory -ItemType Directory -Force | Out-Null

$StagingDir = Join-Path $ProjectRoot (".build\package-{0}" -f $Version)
if (Test-Path $StagingDir) {
    Remove-Item $StagingDir -Recurse -Force
}
New-Item $StagingDir -ItemType Directory -Force | Out-Null

try {
    foreach ($Artifact in $RequiredArtifacts) {
        Copy-Item $Artifact $StagingDir -Force
    }

    $LocalizationDir = Join-Path $ModDir "GuZhenRen"
    if (Test-Path $LocalizationDir -PathType Container) {
        Copy-Item $LocalizationDir $StagingDir -Recurse -Force
    }

    if (Test-Path $ResolvedOutputPath) {
        Remove-Item $ResolvedOutputPath -Force
    }
    $CompressParameters = @{
        Path = Join-Path $StagingDir "*"
        DestinationPath = $ResolvedOutputPath
        CompressionLevel = "Optimal"
    }
    Compress-Archive @CompressParameters
}
finally {
    if (Test-Path $StagingDir) {
        Remove-Item $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "构建与安装完成：$ModDir"
Write-Host "可发布安装包：$ResolvedOutputPath"
