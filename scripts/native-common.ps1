Set-StrictMode -Version Latest

function Get-NativeContext {
    $projectRoot = Split-Path -Parent $PSScriptRoot
    $shortRoot = (New-Object -ComObject Scripting.FileSystemObject).GetFolder($projectRoot).ShortPath
    if ($shortRoot -match '[^\x00-\x7F]') {
        throw 'MinGW Makefiles requires an ASCII path or an available Windows 8.3 short path.'
    }
    [pscustomobject]@{
        Root = $projectRoot
        ShortRoot = $shortRoot
        Compiler = "$shortRoot\local\tools\w64devkit\bin\g++.exe"
        CompilerBin = "$shortRoot\local\tools\w64devkit\bin"
        CMake = "$shortRoot\local\tools\cmake-4.4.5-windows-x86_64\bin\cmake.exe"
        SDL = "$shortRoot\local\tools\SDL2-2.32.10\x86_64-w64-mingw32"
        Checkout = "$shortRoot\local\upstream\ps1-recomp"
        Build = "$shortRoot\local\candidate-build"
        Logs = "$shortRoot\local\logs"
    }
}

function Invoke-NativeLogged {
    param([string]$Executable, [string[]]$Arguments, [string]$Log)
    & $Executable @Arguments 2>&1 | Out-File -LiteralPath $Log -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $Log -Tail 25 | Write-Host
        throw "Native command failed (exit $LASTEXITCODE). Log: $Log"
    }
}
