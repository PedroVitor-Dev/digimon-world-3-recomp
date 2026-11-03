param([string]$ManifestPath)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\native-common.ps1"
$context = Get-NativeContext
$research = Get-Content -LiteralPath (Join-Path $context.Root 'profiles\dw3-sles04936-local-translation.json') -Raw | ConvertFrom-Json
$hash = $research.identity.imageSha256
if (!$ManifestPath) { $ManifestPath = Join-Path $context.Root "local\prepared\$hash\manifest.json" }
& "$PSScriptRoot\verify-profile.ps1" -ManifestPath $ManifestPath
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$fs = New-Object -ComObject Scripting.FileSystemObject
$boot = $fs.GetFile((Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $ManifestPath).Path) 'boot.exe')).ShortPath
$image = $fs.GetFile($manifest.image).ShortPath
$output = Join-Path $context.ShortRoot "local\analysis\$hash"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$previousPath = $env:PATH
try {
    $env:PATH = $context.CompilerBin + ';' + $previousPath
    $analyzer = Join-Path $context.Build 'ps1Analyzer\ps1Analyzer.exe'
    Invoke-NativeLogged $analyzer @($boot,"$output\candidate.toml") (Join-Path $context.Logs 'game-analysis.log')
    Invoke-NativeLogged $analyzer @('--scan-overlays',$image,"$output\overlay-candidates.toml") (Join-Path $context.Logs 'overlay-scan.log')
} finally { $env:PATH = $previousPath }
Write-Output "Heuristic analysis saved in $output"
Write-Output 'Review code/data boundaries, PsyQ matches and overlay addresses before generation. No game code was executed.'
