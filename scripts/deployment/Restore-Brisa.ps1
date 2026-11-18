param([string]$Destination = (Split-Path -Parent $PSScriptRoot), [switch]$Launch)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$target = [IO.Path]::GetFullPath($Destination)
$state = Read-BrisaInstall $target
if ($null -eq $state -or !$state.PSObject.Properties['PreviousRecovery'] -or !$state.PreviousRecovery) { throw 'This installation has no recoverable previous version.' }
Assert-BrisaClosed $target
$recovery = Get-BrisaPath $target $state.PreviousRecovery
$journal = Get-Content -LiteralPath (Get-BrisaPath $recovery 'rollback.json') -Raw | ConvertFrom-Json
if ($journal.Product -ne 'Brisa Dock' -or $journal.Previous.Path -ne $target) { throw 'The recovery belongs to another Brisa installation.' }
$stageName = '.brisa-rollback-' + [Guid]::NewGuid().ToString('N')
$stage = Get-BrisaPath $target $stageName
try {
    foreach ($relative in $journal.Previous.Files) {
        Assert-BrisaPayloadName $relative
        $backup = Get-BrisaPath $recovery $relative
        $recoveryInput = if (Test-Path -LiteralPath $backup -PathType Leaf) { $backup } else { Get-BrisaPath $target $relative }
        $hash = $journal.Previous.Hashes.PSObject.Properties[$relative]
        if ($null -eq $hash -or !(Test-Path -LiteralPath $recoveryInput -PathType Leaf) -or (Get-FileHash -LiteralPath $recoveryInput -Algorithm SHA256).Hash -ne $hash.Value) { throw "The previous copy is incomplete: $relative" }
        $output = Get-BrisaPath $stage $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
        Copy-Item -LiteralPath $recoveryInput -Destination $output
    }
    $null = Write-BrisaPackage $stage $journal.Previous.Version
    $null = Read-BrisaPackage $stage
    & (Join-Path $PSScriptRoot 'Install-Brisa.ps1') -Destination $target -SourceDirectory $stage -RecoverySource -NoShortcut -NoRegistration:(!($state.PSObject.Properties['Registered'] -and $state.Registered)) -NoDataImport -Launch:$Launch
}
finally {
    $checked = Get-BrisaPath $target $stageName
    if ((Split-Path -Parent $checked) -eq $target -and (Split-Path -Leaf $checked) -match '^\.brisa-rollback-[0-9a-f]{32}$' -and (Test-Path -LiteralPath $checked)) { Remove-Item -LiteralPath $checked -Recurse -Force }
}
