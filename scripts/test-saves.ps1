$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\build-saves.ps1"
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 "/out:$projectRoot\build\Dw3.SaveTests.exe" /r:System.Web.Extensions.dll "$projectRoot\src\SaveVault.cs" "$projectRoot\tests\SaveVaultTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Save test compilation failed.' }
& "$projectRoot\build\Dw3.SaveTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Save tests failed.' }
