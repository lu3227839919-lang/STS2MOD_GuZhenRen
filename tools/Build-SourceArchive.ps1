param(
    [string]$OutputPath = "",
    [switch]$IncludeGitMetadata
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $ManifestPath = Join-Path $ProjectRoot "GuZhenRenPersonal.json"
    $Manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
    $OutputPath = Join-Path $ProjectRoot ("GuZhenRenPersonal-source-{0}.zip" -f $Manifest.version)
}

$ResolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("GuZhenRenPersonal-source-" + [Guid]::NewGuid().ToString("N"))

$ExcludedDirectoryNames = @(
    ".godot", ".idea", ".vs", ".build", ".vscode",
    "bin", "obj", "build", "steam", "Slay the Spire 2"
)

$ExcludedFileNames = @(
    "local.props"
)

$ExcludedExtensions = @(
    ".user", ".log", ".zip", ".patch"
)

try {
    New-Item -ItemType Directory -Path $TempRoot | Out-Null

    Get-ChildItem -Path $ProjectRoot -Recurse -File | ForEach-Object {
        $File = $_
        $RelativePath = [System.IO.Path]::GetRelativePath($ProjectRoot, $File.FullName)
        $Segments = $RelativePath -split '[\\/]'

        if (-not $IncludeGitMetadata -and $Segments -contains ".git") {
            return
        }

        foreach ($DirectoryName in $ExcludedDirectoryNames) {
            if ($Segments -contains $DirectoryName) {
                return
            }
        }

        if ($ExcludedFileNames -contains $File.Name) {
            return
        }

        if ($ExcludedExtensions -contains $File.Extension.ToLowerInvariant()) {
            return
        }

        if ([System.IO.Path]::GetFullPath($File.FullName) -eq $ResolvedOutput) {
            return
        }

        $Destination = Join-Path $TempRoot $RelativePath
        $DestinationDirectory = Split-Path -Parent $Destination
        New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
        Copy-Item $File.FullName $Destination -Force
    }

    if (Test-Path $ResolvedOutput) {
        Remove-Item $ResolvedOutput -Force
    }

    Compress-Archive -Path (Join-Path $TempRoot '*') -DestinationPath $ResolvedOutput -CompressionLevel Optimal
    Write-Host "Source archive created: $ResolvedOutput"
}
finally {
    Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
