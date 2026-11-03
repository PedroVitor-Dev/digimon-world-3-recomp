param([string]$ManifestPath, [switch]$ThroughHeap)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\native-common.ps1"
$context = Get-NativeContext
$hash = 'cba308798bf768da4c0675f3130a4b380f02b53da1a50a7537ad9be7d8f3da94'
if (!$ManifestPath) { $ManifestPath = Join-Path $context.Root "local\prepared\$hash\manifest.json" }
& "$PSScriptRoot\verify-profile.ps1" -ManifestPath $ManifestPath
$fs = New-Object -ComObject Scripting.FileSystemObject
$boot = $fs.GetFile((Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $ManifestPath).Path) 'boot.exe')).ShortPath.Replace('\','/')
$output = (Join-Path $context.ShortRoot "local\analysis\$hash").Replace('\','/')
New-Item -ItemType Directory -Force -Path $output | Out-Null
# The entry ends at BREAK 0x80010F38; words from 0x80010F3C are a stack table.
# The two zero-size entries are intentional throwing diagnostic gates, not game implementations.
$heapSize = 0
$heapGate = "'0x8002507C' = 'throw uint32_t{0x8002507Cu};'"
if ($ThroughHeap) { $heapSize = 16; $heapGate = '' }
$config = @"
[binary]
path = '$boot'
entry_point = '0x80010E90'

[[functions]]
name = 'dw3_startup'
address = '0x80010E90'
size = 172

[[functions]]
name = 'func_8002507C'
address = '0x8002507C'
size = $heapSize

[[functions]]
name = 'func_80014524'
address = '0x80014524'
size = 0

[missing_functions]
$heapGate
'0x80014524' = 'throw uint32_t{0x80014524u};'
"@
[IO.File]::WriteAllText("$output/startup-probe.toml", $config, (New-Object System.Text.UTF8Encoding($false)))
$previousPath = $env:PATH
try {
    $env:PATH = $context.CompilerBin + ';' + $previousPath
    Invoke-NativeLogged (Join-Path $context.Build 'ps1Recomp\ps1Recomp.exe') @("$output/startup-probe.toml","$output/startup-probe.cpp") (Join-Path $context.Logs 'startup-generation.log')
    & "$PSScriptRoot\audit-generated.ps1" -SourcePath "$output/startup-probe.cpp"
    $arguments = @('-std=c++20','-O1','-static','-I',"$($context.Checkout)/ps1Runtime/include",'-I',"$($context.Checkout)/third_party/fmt/include", "$($context.ShortRoot)/native/startup_probe.cpp","$output/startup-probe.cpp", "$($context.Build)/ps1Runtime/libps1Runtime_lib.a","$($context.Build)/third_party/fmt/libfmt.a",'-o',"$output/startup_probe.exe")
    Invoke-NativeLogged $context.Compiler $arguments (Join-Path $context.Logs 'startup-probe-build.log')
    $probeArguments = @($boot)
    $probeLog = Join-Path $context.Logs 'startup-probe.log'
    if ($ThroughHeap) { $probeArguments += '--through-heap'; $probeLog = Join-Path $context.Logs 'startup-through-heap.log' }
    Invoke-NativeLogged "$output/startup_probe.exe" $probeArguments $probeLog
    Get-Content -LiteralPath $probeLog
} finally { $env:PATH = $previousPath }
