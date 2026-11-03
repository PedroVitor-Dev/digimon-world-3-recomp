$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\native-common.ps1"
$context = Get-NativeContext
$previousPath = $env:PATH
$scratch = Join-Path $context.ShortRoot ('local\save-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
try {
    $env:PATH = $context.CompilerBin + ';' + $previousPath
    $arguments = @('-std=c++20','-O1','-static','-I',"$($context.Checkout)/ps1Runtime/include",'-I',"$($context.Checkout)/third_party/fmt/include", "$($context.ShortRoot)/native/memcard_io_tests.cpp", "$($context.Checkout)/ps1Runtime/src/input/input.cpp", "$($context.Build)/ps1Runtime/libps1Runtime_lib.a", "$($context.Build)/third_party/fmt/libfmt.a",'-o',"$scratch/test.exe")
    Invoke-NativeLogged $context.Compiler $arguments (Join-Path $context.Logs 'native-save-test-build.log')
    Invoke-NativeLogged "$scratch/test.exe" @("$scratch/cards") (Join-Path $context.Logs 'native-save-test.log')
    Get-Content -LiteralPath (Join-Path $context.Logs 'native-save-test.log')
} finally { $env:PATH = $previousPath }
Write-Output "Synthetic test artifacts retained in $scratch"
