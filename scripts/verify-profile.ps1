param(
    [string]$ProfilePath,
    [string]$ManifestPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$ProfilePath) { $ProfilePath = Join-Path $projectRoot 'profiles\dw3-sles04936-local-translation.json' }
$researchProfile = Get-Content -LiteralPath $ProfilePath -Raw | ConvertFrom-Json
if ($researchProfile.schemaVersion -ne 1 -or $researchProfile.status -ne 'research-only') {
    throw 'This verifier requires a schema v1 research profile.'
}
$identity = $researchProfile.identity
foreach ($hashName in @('imageSha256', 'executableSha256')) {
    if ($identity.$hashName -cnotmatch '^[a-f0-9]{64}$') { throw "Invalid profile hash: $hashName" }
}
if (!$ManifestPath) {
    $ManifestPath = Join-Path $projectRoot ('local\prepared\' + $identity.imageSha256 + '\manifest.json')
}
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.status -ne 'analyzed-not-recompiled') {
    throw 'Expected a schema v1 preparation manifest.'
}
foreach ($field in @('imageSha256', 'executableSha256', 'volume', 'bootFile', 'sectorSize', 'entryPoint', 'loadAddress', 'payloadSize')) {
    if ([string]$manifest.$field -cne [string]$identity.$field) { throw "Profile mismatch: $field" }
}
if (@($manifest.files).Count -ne $identity.inventoryEntryCount) { throw 'Profile mismatch: inventoryEntryCount' }
$image = Get-Item -LiteralPath $manifest.image
if ($image.Length -ne $identity.imageSize) { throw 'Profile mismatch: imageSize' }
if ((Get-FileHash -LiteralPath $image.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -cne $identity.imageSha256) {
    throw 'Actual disc image does not match the profile hash.'
}
$executablePath = Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $ManifestPath).Path) 'boot.exe'
$executable = Get-Item -LiteralPath $executablePath
if ($executable.Length -ne $identity.executableSize) { throw 'Profile mismatch: executableSize' }
if ((Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $identity.executableSha256) {
    throw 'Actual boot executable does not match the profile hash.'
}
Write-Output "Verified research-copy identity: $($researchProfile.id)"
Write-Output 'Native boot and gameplay remain unverified. No code-generation profile is available yet.'
