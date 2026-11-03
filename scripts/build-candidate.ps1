param([ValidateRange(1,32)][int]$Jobs = 6)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\native-common.ps1"
& "$PSScriptRoot\prepare-native-tools.ps1"
$context = Get-NativeContext
$dependency = Get-Content -LiteralPath (Join-Path $context.Root 'dependencies\ps1recomp.lock.json') -Raw | ConvertFrom-Json
if ($dependency.schemaVersion -ne 1) { throw 'Unsupported candidate lock schema.' }
New-Item -ItemType Directory -Force -Path $context.Logs,(Join-Path $context.Root 'local\upstream') | Out-Null
if (!(Test-Path -LiteralPath (Join-Path $context.Checkout '.git'))) {
    & git -c core.longpaths=true clone --no-checkout $dependency.repository $context.Checkout
    if ($LASTEXITCODE -ne 0) { throw 'Candidate clone failed.' }
    & git -C $context.Checkout checkout --detach $dependency.commit
    if ($LASTEXITCODE -ne 0) { throw 'Pinned checkout failed.' }
}
$actualCommit = & git -C $context.Checkout rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $dependency.commit) { throw 'Candidate checkout does not match the lock. Existing checkout was preserved.' }
& git -C $context.Checkout -c core.longpaths=true submodule update --init
if ($LASTEXITCODE -ne 0) { throw 'Pinned submodule checkout failed.' }
foreach ($submodule in $dependency.submodules) {
    $actualCommit = & git -C (Join-Path $context.Checkout $submodule.path) rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $submodule.commit) { throw "Submodule mismatch: $($submodule.path)" }
}
$patch = Join-Path $context.Root $dependency.portabilityPatch.path
if ((Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash.ToLowerInvariant() -cne $dependency.portabilityPatch.sha256) { throw 'Portability patch hash mismatch.' }
& git -C $context.Checkout apply --reverse --check $patch 2>$null
if ($LASTEXITCODE -ne 0) {
    & git -C $context.Checkout apply --check $patch
    if ($LASTEXITCODE -ne 0) { throw 'Candidate differs from the expected baseline/patch. No changes applied.' }
    & git -C $context.Checkout apply $patch
    if ($LASTEXITCODE -ne 0) { throw 'Portability patch failed.' }
}
$previousPath = $env:PATH
try {
    $env:PATH = $context.CompilerBin + ';' + $context.SDL + '\bin;' + $previousPath
    Invoke-NativeLogged $context.Compiler @('-std=c++20','-O2','-Wall','-Wextra','-Werror','-static',"$($context.ShortRoot)\native\toolchain_smoke.cpp",'-o',"$($context.ShortRoot)\local\tools\toolchain_smoke.exe") (Join-Path $context.Logs 'toolchain-smoke-build.log')
    Invoke-NativeLogged "$($context.ShortRoot)\local\tools\toolchain_smoke.exe" @() (Join-Path $context.Logs 'toolchain-smoke.log')
    $configure = @('-S',$context.Checkout,'-B',$context.Build,'-G','MinGW Makefiles','-DCMAKE_BUILD_TYPE=Release','-DPS1RECOMP_USE_STUB=ON','-DPS1RECOMP_BUILD_INTERFACE=OFF','-DCMAKE_POLICY_VERSION_MINIMUM=3.5',"-DCMAKE_PREFIX_PATH=$($context.SDL)","-DDW3_SDL2_ROOT=$($context.SDL)")
    Invoke-NativeLogged $context.CMake $configure (Join-Path $context.Logs 'candidate-configure.log')
    Invoke-NativeLogged $context.CMake @('--build',$context.Build,'--parallel',"$Jobs") (Join-Path $context.Logs 'candidate-all-build.log')
} finally { $env:PATH = $previousPath }
Write-Output 'Built pinned Windows diagnostic candidate in stub mode. No game boot is claimed.'
