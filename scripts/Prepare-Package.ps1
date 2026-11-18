param([string]$Directory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/build'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'deployment/Common.ps1')
$root = [IO.Path]::GetFullPath($Directory)
$exe = Get-BrisaPath $root 'BrisaDock.exe'
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Brisa was not found in the package directory.' }
$runtimeConfig = Get-BrisaPath $root 'BrisaDock.runtimeconfig.json'
if (Test-Path -LiteralPath $runtimeConfig) {
    $runtime = Get-Content -LiteralPath $runtimeConfig -Raw | ConvertFrom-Json
    if ($runtime.runtimeOptions.PSObject.Properties['framework'] -and (Test-Path -LiteralPath (Get-BrisaPath $root 'hostfxr.dll'))) {
        throw 'The directory mixes publish modes. Run scripts/Build-Brisa.ps1 in a fresh output directory.'
    }
}
[IO.Directory]::CreateDirectory((Get-BrisaPath $root 'maintenance')) | Out-Null
foreach ($name in @('Install-Brisa.ps1','Update-Brisa.ps1','Restore-Brisa.ps1','Uninstall-Brisa.ps1','Common.ps1','Invoke-Maintenance.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('deployment/'+$name)) -Destination (Get-BrisaPath $root ('maintenance/'+$name)) -Force
}
$manifest = Write-BrisaPackage $root (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion
$null = Read-BrisaPackage $root
Write-Output "Brisa $($manifest.Version) package prepared and verified."
