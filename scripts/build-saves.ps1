$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'build') | Out-Null
& $compiler /nologo /target:exe /platform:x64 /optimize+ "/out:$projectRoot\build\Dw3.Saves.exe" /r:System.Web.Extensions.dll "$projectRoot\src\SaveVault.cs" "$projectRoot\src\SaveCli.cs"
if ($LASTEXITCODE -ne 0) { throw 'Save tool compilation failed.' }
Write-Output "Built $projectRoot\build\Dw3.Saves.exe (offline utility; runtime integration pending)."
