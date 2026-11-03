param([ValidateRange(1,30)][int]$Seconds = 10)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\native-common.ps1"
$context = Get-NativeContext
& "$PSScriptRoot\verify-profile.ps1"
$hash = 'cba308798bf768da4c0675f3130a4b380f02b53da1a50a7537ad9be7d8f3da94'
$manifest = Get-Content -LiteralPath (Join-Path $context.Root "local\prepared\$hash\manifest.json") -Raw | ConvertFrom-Json
$image = (New-Object -ComObject Scripting.FileSystemObject).GetFile($manifest.image).ShortPath
$run = Join-Path $context.ShortRoot ('local\diagnostics\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $run | Out-Null
$process = $null
$saved = @{}
foreach ($name in @('PATH','PS1_VRAM_DUMP_PATH','PS1_VRAM_DUMP_FRAME','SDL_AUDIODRIVER','PS1_DISPATCH_PERMISSIVE')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $env:PATH = $context.CompilerBin + ';' + $context.SDL + '\bin;' + $env:PATH
    $env:PS1_VRAM_DUMP_PATH = "$run/vram.ppm"
    $env:PS1_VRAM_DUMP_FRAME = '1'
    $env:SDL_AUDIODRIVER = 'dummy'
    $env:PS1_DISPATCH_PERMISSIVE = '0'
    $executable = "$($context.ShortRoot)/local/analysis/$hash/dw3-boot-diagnostic.exe"
    $arguments = @(('"' + "$($context.ShortRoot)/local/prepared/$hash/boot.exe" + '"'),('"' + $image + '"'))
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $run -WindowStyle Hidden -RedirectStandardOutput "$run/stdout.log" -RedirectStandardError "$run/stderr.log" -PassThru
    $timedOut = !$process.WaitForExit($Seconds * 1000)
    if ($timedOut) { [void]$process.CloseMainWindow(); if (!$process.WaitForExit(3000)) { $process.Kill(); $process.WaitForExit() } }
    $summary = [ordered]@{schemaVersion=1;status='diagnostic-only-not-gameplay-verified';imageSha256=$hash;referenceCommit='791932b757b358bb8228f851ce96f4d22ae112dd';secondsLimit=$Seconds;timedOut=$timedOut;exitCode=$process.ExitCode;vramDumpExists=(Test-Path -LiteralPath "$run/vram.ppm")}
    [IO.File]::WriteAllText("$run/report.json", ($summary | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    Get-Content "$run/stdout.log" -Tail 8
    Get-Content "$run/stderr.log" -Tail 8
    Write-Output "Diagnostic report: $run/report.json"
    if ($timedOut) { throw 'Diagnostic reached its time limit; no successful game boot is claimed.' }
    if ($process.ExitCode -ne 0) { throw "Diagnostic exited with code $($process.ExitCode)." }
} finally { if ($process -and !$process.HasExited) { $process.Kill(); $process.WaitForExit() }; foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') } }
