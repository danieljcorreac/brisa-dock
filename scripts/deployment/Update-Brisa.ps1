param([Parameter(Mandatory)][string]$SourceDirectory, [string]$Destination = (Split-Path -Parent $PSScriptRoot), [switch]$Launch)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$target = [IO.Path]::GetFullPath($Destination)
$state = Read-BrisaInstall $target
if ($null -eq $state) { throw 'This installation has no Brisa update record.' }
if ($null -eq (Read-BrisaPackage ([IO.Path]::GetFullPath($SourceDirectory)))) { throw 'Select a Brisa package directory.' }
& (Join-Path $PSScriptRoot 'Install-Brisa.ps1') -Destination $target -SourceDirectory $SourceDirectory -NoShortcut -NoRegistration:(!($state.PSObject.Properties['Registered'] -and $state.Registered)) -NoDataImport -Launch:$Launch
