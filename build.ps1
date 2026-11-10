param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [string]$RuntimeRefs = ''
)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'BepInEx/core/BepInEx.Core.dll') -PathType Leaf)) {
        throw "GameRoot is missing BepInEx/core/BepInEx.Core.dll: $GameRoot"
    }
    $expected = @('AstralParty.Runtime.dll', 'UnityEngine.CoreModule.dll', 'UniTask.dll', 'Google.Protobuf.Runtime.dll')
    $hasRawRefs = -not [string]::IsNullOrWhiteSpace($RuntimeRefs)
    foreach ($name in $expected) {
        if (-not $hasRawRefs -or -not (Test-Path -LiteralPath (Join-Path $RuntimeRefs $name) -PathType Leaf)) {
            $hasRawRefs = $false
            break
        }
    }
    $args = @('build', 'src/AstralPartySimpleStats.csproj', '-c', 'Release', "-p:AstralGameRoot=$GameRoot", '--nologo')
    if ($hasRawRefs) {
        Write-Host '[AstralPartySimpleStats] Building updated 0.4.0 HybridCLR bridge from raw game DLLs.'
        $args += "-p:AstralRuntimeRefs=$RuntimeRefs"
        $args += '-p:AstralUseBundledBridge=false'
    } else {
        $bundled = Join-Path $PSScriptRoot 'bridge/prebuilt/AstralPartySimpleStats.ProfileBridge.dll'
        if (-not (Test-Path -LiteralPath $bundled -PathType Leaf)) {
            throw "Prebuilt 0.3.0 ProfileBridge is missing: $bundled"
        }
        Write-Host '[AstralPartySimpleStats] Raw HybridCLR refs unavailable; reusing the embedded 0.3.0 bridge.'
        Write-Host '[AstralPartySimpleStats] UI/status + native room actions are built in the 0.4.0 main DLL.'
        $args += '-p:AstralUseBundledBridge=true'
    }
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
    Write-Host '[AstralPartySimpleStats] Output: src/bin/Release/net6.0/AstralPartySimpleStats.dll'
} finally { Pop-Location }
