param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [Parameter(Mandatory=$true)][string]$RuntimeRefs
)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & dotnet build 'src/AstralPartySimpleStats.csproj' -c Release "-p:AstralGameRoot=$GameRoot" "-p:AstralRuntimeRefs=$RuntimeRefs" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
} finally { Pop-Location }
