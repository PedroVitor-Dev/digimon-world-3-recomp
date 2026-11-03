$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolRoot = Join-Path $projectRoot 'local\tools'
New-Item -ItemType Directory -Force -Path $toolRoot | Out-Null
$lock = Get-Content -LiteralPath (Join-Path $projectRoot 'dependencies\native-tools.lock.json') -Raw | ConvertFrom-Json
if ($lock.schemaVersion -ne 1) { throw 'Unsupported tool lock schema.' }
foreach ($package in $lock.packages) {
    $archivePath = Join-Path $toolRoot $package.archive
    if (!(Test-Path -LiteralPath $archivePath)) {
        Write-Host "Downloading pinned $($package.name) $($package.version)..."
        Invoke-WebRequest -Uri $package.url -OutFile $archivePath -UseBasicParsing
    }
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne $package.sha256) { throw "Archive hash mismatch: $($package.archive)" }
    if (!(Test-Path -LiteralPath (Join-Path $toolRoot $package.marker))) {
        & tar.exe -xf $archivePath -C $toolRoot
        if ($LASTEXITCODE -ne 0) { throw "Extraction failed: $($package.archive)" }
    }
    if (!(Test-Path -LiteralPath (Join-Path $toolRoot $package.marker))) { throw "Missing tool: $($package.marker)" }
    Write-Output "Verified archive and tool availability: $($package.name) $($package.version)"
}
