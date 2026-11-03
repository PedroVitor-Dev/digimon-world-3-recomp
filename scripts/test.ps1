$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\build.ps1"
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 "/out:$projectRoot\build\Dw3.Tests.exe" /r:System.Web.Extensions.dll "$projectRoot\src\Disc.cs" "$projectRoot\tests\DiscTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$projectRoot\build\Dw3.Tests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
