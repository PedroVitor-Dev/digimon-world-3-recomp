$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\native-common.ps1"
$context = Get-NativeContext
& "$PSScriptRoot\verify-profile.ps1"
$reference = Get-Content -LiteralPath (Join-Path $context.Root 'dependencies\dw3-decomp.lock.json') -Raw | ConvertFrom-Json
$checkout = Join-Path $context.ShortRoot 'local\upstream\dw3-decomp'
if (!(Test-Path -LiteralPath (Join-Path $checkout '.git'))) {
    & git -c core.longpaths=true clone --no-checkout $reference.repository $checkout
    if ($LASTEXITCODE -ne 0) { throw 'Reference clone failed.' }
    & git -C $checkout checkout --detach $reference.commit
    if ($LASTEXITCODE -ne 0) { throw 'Reference checkout failed.' }
}
$commit = & git -C $checkout rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $commit -ne $reference.commit) { throw 'Reference revision mismatch; existing checkout preserved.' }
foreach ($file in $reference.files) {
    $canonical = [IO.File]::ReadAllText((Join-Path $checkout $file.path)).Replace("`r`n","`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $actual = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
    if ($actual -cne $file.sha256) { throw "Changed reference map: $($file.path)" }
}
$hash = 'cba308798bf768da4c0675f3130a4b380f02b53da1a50a7537ad9be7d8f3da94'
$output = Join-Path $context.ShortRoot "local\analysis\$hash"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$symbols = Get-Content -LiteralPath (Join-Path $checkout 'config\eu\symbols.txt') -Raw
$starts = @{}
foreach ($match in [regex]::Matches($symbols, '(?m)^(\w+)\s*=\s*(0x[0-9A-Fa-f]+);([^\r\n]*)')) {
    $address = [Convert]::ToUInt32($match.Groups[2].Value.Substring(2),16)
    if ($address -ge 0x80010e88L -and $address -lt 0x8003edccL -and
        ($match.Groups[3].Value -match 'type:func' -or $match.Groups[1].Value -match '^func_')) { $starts[$address] = $true }
}
$ordered = @($starts.Keys | Sort-Object)
if ($ordered.Count -ne 913) { throw 'Unexpected reference function/alias count.' }
$builder = New-Object Text.StringBuilder
$boot = "$($context.ShortRoot)/local/prepared/$hash/boot.exe".Replace('\','/')
[void]$builder.AppendLine("[binary]`npath = '$boot'`nentry_point = '0x80010E90'")
for ($i=0; $i -lt $ordered.Count; $i++) {
    $address = [long]$ordered[$i]; $end = 0x8003edccL
    if ($i+1 -lt $ordered.Count) { $end = [long]$ordered[$i+1] }
    $size = $end - $address
    if ($address -eq 0x80010e90L) { $size = 172 } # Exclude the inline CRT stack table.
    $hex = '{0:X8}' -f $address
    [void]$builder.AppendLine("`n[[functions]]`nname = 'func_$hex'`naddress = '0x$hex'`nsize = $size")
}
# Confirmed FIELDSTG references. Until that overlay is integrated, stop rather than simulate success.
foreach ($hex in @('8008B770','8008BFA4','8008C000')) {
    [void]$builder.AppendLine("`n[[functions]]`nname = 'func_$hex'`naddress = '0x$hex'`nsize = 0")
}
[void]$builder.AppendLine("`n[missing_functions]")
foreach ($hex in @('8008B770','8008BFA4','8008C000')) {
    [void]$builder.AppendLine("'0x$hex' = 'fmt::print(stderr, `"[DW3] Unintegrated FIELDSTG target 0x$hex\n`"); throw uint32_t{0x$($hex)u};'")
}
[IO.File]::WriteAllText("$output/eu-reference.toml", $builder.ToString(), (New-Object Text.UTF8Encoding($false)))
$previousPath = $env:PATH
try {
    $env:PATH = $context.CompilerBin + ';' + $previousPath
    Invoke-NativeLogged "$($context.Build)/ps1Recomp/ps1Recomp.exe" @("$output/eu-reference.toml","$output/eu-reference.cpp") "$($context.Logs)/eu-reference-generation.log"
    & "$PSScriptRoot\audit-generated.ps1" "$output/eu-reference.cpp"
    $arguments = @('-std=c++20','-O1','-static','-Wl,--stack,16777216','-I',"$($context.Checkout)/ps1Runtime/include",'-I',"$($context.Checkout)/ps1Analyzer/include",'-I',"$($context.Checkout)/third_party/fmt/include",'-I',"$($context.Checkout)/third_party/toml11/include",'-I',"$($context.SDL)/include/SDL2", "$($context.Checkout)/ps1Runtime/src/main_host.cpp","$output/eu-reference.cpp", "$($context.Build)/ps1Runtime/libps1Runtime_lib.a","$($context.Build)/ps1Analyzer/libps1Analyzer_lib.a","$($context.Build)/third_party/fmt/libfmt.a","$($context.SDL)/lib/libSDL2.dll.a",'-lopengl32','-o',"$output/dw3-boot-diagnostic.exe")
    Invoke-NativeLogged $context.Compiler $arguments "$($context.Logs)/eu-reference-runtime-build.log"
} finally { $env:PATH = $previousPath }
Write-Output 'Built main-entry diagnostic with reference symbols and three explicit FIELDSTG failure gates. Gameplay is unverified.'
