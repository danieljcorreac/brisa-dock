param([string]$SourceDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/build'))
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts/deployment/Common.ps1')
$qa=Join-Path $repo ('qa\deployment-'+[Guid]::NewGuid().ToString('N'))
$target=Join-Path $qa 'installed'
$shortcuts=Join-Path $qa 'shortcuts'
$installer=Join-Path $repo 'scripts/deployment/Install-Brisa.ps1'
$script:checks=0
function Check([bool]$Condition,[string]$Name) { if (!$Condition) { throw "FAIL: $Name" }; $script:checks++; Write-Output "PASS: $Name" }
& $installer -Destination $target -SourceDirectory $SourceDirectory -ShortcutDirectory $shortcuts -NoRegistration -NoDataImport
$manifest=Get-Content -LiteralPath (Join-Path $target 'brisa-install.json') -Raw | ConvertFrom-Json
Check ((Test-Path -LiteralPath (Join-Path $target 'BrisaDock.exe')) -and $manifest.Files.Count -gt 100) 'Installer copies the complete runtime and records owned files'
Check (@(Get-ChildItem -LiteralPath $target -File -Filter *.ps1).Count -eq 0 -and @(Get-ChildItem -LiteralPath (Join-Path $target 'maintenance') -File -Filter *.ps1).Count -eq 6) 'Maintenance scripts are grouped outside the runtime root'
Check ((Read-BrisaPackage $target).Files.Count -eq $manifest.Files.Count) 'Installed manifest verifies without duplicate maintenance paths'
$shell=New-Object -ComObject WScript.Shell
try { $linkTarget=$shell.CreateShortcut((Join-Path $shortcuts 'Brisa Dock.lnk')).TargetPath }
finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null }
Check ($linkTarget -eq (Join-Path $target 'BrisaDock.exe')) 'Start shortcut points at the isolated installation'
$data=Join-Path $target 'data\dock.json'
[IO.File]::WriteAllText($data,'{"Version":2,"Note":"preserve-fixture"}')
[IO.File]::WriteAllText((Join-Path $target 'user-file.txt'),'preserve-unknown-file')
$svg=Join-Path $target 'Assets\OpenAI.svg'
[IO.File]::WriteAllText($svg,'old-asset-fixture')
$locked=[IO.File]::Open($svg,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$failed=$false
try { & $installer -Destination $target -SourceDirectory $SourceDirectory -ShortcutDirectory $shortcuts -NoRegistration -NoDataImport }
catch { $failed=$true }
finally { $locked.Dispose() }
Check ($failed -and [IO.File]::ReadAllText($svg) -eq 'old-asset-fixture' -and [IO.File]::ReadAllText($data) -like '*preserve-fixture*') 'Failed update leaves prior files and user data recoverable'
& $installer -Destination $target -SourceDirectory $SourceDirectory -ShortcutDirectory $shortcuts -NoRegistration -NoDataImport
Check ((Get-FileHash -LiteralPath $svg).Hash -eq (Get-FileHash -LiteralPath (Join-Path $SourceDirectory 'Assets\OpenAI.svg')).Hash -and [IO.File]::ReadAllText($data) -like '*preserve-fixture*') 'Successful update repairs package files and preserves configuration'
Check (@(Get-ChildItem -LiteralPath $target -Directory -Force | Where-Object Name -like '.brisa-stage-*').Count -eq 0) 'Staging directories are removed after success and failure'
& (Join-Path $target 'maintenance/Uninstall-Brisa.ps1')
Check (!(Test-Path -LiteralPath (Join-Path $target 'BrisaDock.exe')) -and (Test-Path -LiteralPath $data) -and (Test-Path -LiteralPath (Join-Path $target 'user-file.txt')) -and !(Test-Path -LiteralPath (Join-Path $shortcuts 'Brisa Dock.lnk'))) 'Uninstall removes owned binaries and shortcut while retaining data and unknown files'
& $installer -Destination $target -SourceDirectory $SourceDirectory -ShortcutDirectory $shortcuts -NoRegistration -NoDataImport
Check ((Test-Path -LiteralPath (Join-Path $target 'BrisaDock.exe')) -and [IO.File]::ReadAllText($data) -like '*preserve-fixture*') 'Reinstall recognizes the preserved installation and retains its data'
& (Join-Path $target 'maintenance/Uninstall-Brisa.ps1')
Write-Output "$script:checks deployment checks passed. Artifacts: $qa"
