param([string]$SourceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/build'), [string]$BaselineDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/build'))
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts/deployment/Common.ps1')
$root=Join-Path $repo ('qa\updates-'+[Guid]::NewGuid().ToString('N'))
$first=Join-Path $root 'first'; $second=Join-Path $root 'second'; $target=Join-Path $root 'installed'
$installer=Join-Path $repo 'scripts/deployment/Install-Brisa.ps1'
$script:checks=0
function Check([bool]$Value,[string]$Name) { if (!$Value) { throw "FAIL: $Name" }; $script:checks++; Write-Output "PASS: $Name" }
function Package([string]$Folder,[string]$Exe,[string]$Text) {
    [IO.Directory]::CreateDirectory($Folder) | Out-Null
    Copy-Item -LiteralPath $Exe -Destination (Join-Path $Folder 'BrisaDock.exe')
    [IO.File]::WriteAllText((Join-Path $Folder 'version-note.txt'),$Text)
}
Package $first (Join-Path $BaselineDirectory 'BrisaDock.exe') 'first'
Package $second (Join-Path $SourceDirectory 'BrisaDock.exe') 'second'
[IO.File]::WriteAllText((Join-Path $first 'obsolete-owned.dll'),'obsolete fixture; not executed')
[IO.File]::WriteAllText((Join-Path $second 'new-owned.dll'),'new fixture; not executed')
& (Join-Path $repo 'scripts/Prepare-Package.ps1') -Directory $first
& (Join-Path $repo 'scripts/Prepare-Package.ps1') -Directory $second
& $installer -Destination $target -SourceDirectory $first -NoRegistration -NoShortcut -NoDataImport
& (Join-Path $target 'maintenance/Update-Brisa.ps1') -SourceDirectory $first -Destination $target
$noop=Read-BrisaInstall $target
Check (Test-Path -LiteralPath (Get-BrisaPath $target ($noop.PreviousRecovery+'\rollback.json'))) 'Identical update without user data creates a valid recovery journal'
& (Join-Path $target 'maintenance/Restore-Brisa.ps1') -Destination $target
Check ([IO.File]::ReadAllText((Join-Path $target 'version-note.txt')) -eq 'first') 'Identical update can be recovered'
$data=Join-Path $target 'data\dock.json'; [IO.File]::WriteAllText($data,'{"Note":"current user data"}')
[IO.File]::WriteAllText((Join-Path $target 'unknown-user-file.txt'),'user file')
$before=(Get-FileHash -LiteralPath (Join-Path $target 'BrisaDock.exe')).Hash
$oldMarker=[IO.File]::ReadAllText((Join-Path $target 'brisa-install.json'))
$oldEnv=$env:BRISA_ISOLATED_TESTS
try {
    $env:BRISA_ISOLATED_TESTS='1'; $failed=$false
    try { & $installer -Destination $target -SourceDirectory $second -NoRegistration -NoShortcut -NoDataImport -TestFailAfter 1 } catch { $failed=$true }
    Check ($failed -and (Get-FileHash -LiteralPath (Join-Path $target 'BrisaDock.exe')).Hash -eq $before -and [IO.File]::ReadAllText((Join-Path $target 'version-note.txt')) -eq 'first') 'Interrupted copy restores the old application payload'
    Check ([IO.File]::ReadAllText((Join-Path $target 'brisa-install.json')) -eq $oldMarker -and [IO.File]::ReadAllText($data) -like '*current user data*') 'Interrupted copy preserves manifest and current user data'
}
finally { $env:BRISA_ISOLATED_TESTS=$oldEnv }
& (Join-Path $target 'maintenance/Update-Brisa.ps1') -SourceDirectory $second -Destination $target
$state=Read-BrisaInstall $target
Check ([IO.File]::ReadAllText((Join-Path $target 'version-note.txt')) -eq 'second' -and $state.PreviousRecovery) 'Update records a recoverable prior installation'
Check (!(Test-Path -LiteralPath (Join-Path $target 'obsolete-owned.dll')) -and (Test-Path -LiteralPath (Join-Path $target 'unknown-user-file.txt'))) 'Update removes obsolete owned files and preserves unknown user files'
Check ((Test-Path -LiteralPath (Get-BrisaPath $target ($state.PreviousRecovery+'\data-before-update\dock.json'))) -and [IO.File]::ReadAllText($data) -like '*current user data*') 'Update creates a data copy without replacing live data'
[IO.File]::WriteAllText($data,'{"Note":"edited after update"}')
& (Join-Path $target 'maintenance/Restore-Brisa.ps1') -Destination $target
Check ([IO.File]::ReadAllText((Join-Path $target 'version-note.txt')) -eq 'first' -and (Test-Path -LiteralPath (Join-Path $target 'obsolete-owned.dll')) -and !(Test-Path -LiteralPath (Join-Path $target 'new-owned.dll'))) 'Rollback restores old owned files and removes newly added files'
Check ([IO.File]::ReadAllText($data) -like '*edited after update*' -and (Test-Path -LiteralPath (Join-Path $target 'unknown-user-file.txt'))) 'Rollback keeps edits made after the update'
& (Join-Path $target 'maintenance/Restore-Brisa.ps1') -Destination $target
Check ([IO.File]::ReadAllText((Join-Path $target 'version-note.txt')) -eq 'second') 'Rollback itself remains reversible'
$saved=[IO.File]::ReadAllText((Join-Path $second 'version-note.txt')); [IO.File]::WriteAllText((Join-Path $second 'version-note.txt'),'tampered')
$failed=$false; try { & (Join-Path $target 'maintenance/Update-Brisa.ps1') -SourceDirectory $second -Destination $target } catch { $failed=$true }
Check ($failed -and [IO.File]::ReadAllText((Join-Path $target 'version-note.txt')) -eq 'second') 'Tampered package is rejected before application files change'
[IO.File]::WriteAllText((Join-Path $second 'version-note.txt'),$saved)
$state=Read-BrisaInstall $target; $backup=Get-BrisaPath $target ($state.PreviousRecovery+'\version-note.txt'); [IO.File]::WriteAllText($backup,'damaged backup')
$failed=$false; try { & (Join-Path $target 'maintenance/Restore-Brisa.ps1') -Destination $target } catch { $failed=$true }
Check ($failed -and [IO.File]::ReadAllText((Join-Path $target 'version-note.txt')) -eq 'second') 'Damaged recovery is rejected before current payload changes'
foreach ($relative in @('data\dock.json','.\data\dock.json','bridge\..\data\dock.json','recovery\copy','C:\outside','brisa-install.json','.brisa-request.json')) {
    $failed=$false; try { Assert-BrisaPayloadName $relative } catch { $failed=$true }; Check $failed ('Protected payload path rejected: '+$relative)
}
Check (@(Get-ChildItem -LiteralPath $target -Directory -Force | Where-Object Name -like '.brisa-*').Count -eq 0) 'Update and rollback leave no staging directories'
Write-Output "$script:checks update checks passed. Artifacts: $root"
