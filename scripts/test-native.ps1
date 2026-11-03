$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\native-common.ps1"
$context = Get-NativeContext
$previousPath = $env:PATH
$failed = @()
try {
    $env:PATH = $context.CompilerBin + ';' + $context.SDL + '\bin;' + $previousPath
    Push-Location (Join-Path $context.Build 'ps1Test')
    try {
        foreach ($name in @('ps1Analyzer_tests','ps1Recomp_tests','ps1Runtime_tests','ps1Runtime_e2e_tests')) {
            $testLog = Join-Path $context.Logs "$name.log"
            & ".\$name.exe" "--gtest_output=json:$($context.Logs)/$name.json" 2>&1 | Out-File -LiteralPath $testLog -Encoding utf8
            $exitCode = $LASTEXITCODE
            Get-Content -LiteralPath $testLog -Tail 8 | Write-Host
            if ($exitCode -ne 0) { $failed += "$name (exit $exitCode)" }
        }
    } finally { Pop-Location }
} finally { $env:PATH = $previousPath }
if ($failed.Count) { throw ('Failing native suites: ' + ($failed -join ', ')) }
Write-Output 'Native suites completed successfully. Check JSON reports for skipped tests; these tests do not verify Digimon gameplay.'
