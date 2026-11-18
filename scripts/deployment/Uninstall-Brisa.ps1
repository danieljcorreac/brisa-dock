$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$target = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$state = Read-BrisaInstall $target
if ($null -eq $state -or !$state.PSObject.Properties['Files']) { throw 'The installation file record is missing.' }
Assert-BrisaClosed $target
$paths = @($state.Files | ForEach-Object {
    if ($_ -match '^(data|recovery)[\\/]' -or $_ -eq 'brisa-install.json') { throw 'The manifest contains a disallowed user-data path.' }
    Get-BrisaPath $target $_
})
$exe = Get-BrisaPath $target 'BrisaDock.exe'
if ($state.PSObject.Properties['Shortcut'] -and $state.Shortcut -and (Test-Path -LiteralPath $state.Shortcut)) {
    $shell = New-Object -ComObject WScript.Shell
    try { if ($shell.CreateShortcut($state.Shortcut).TargetPath -eq $exe) { Remove-Item -LiteralPath $state.Shortcut } }
    finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null }
}
$key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BrisaDock'
if (Test-Path -LiteralPath $key) { if ((Get-ItemProperty -LiteralPath $key).InstallLocation -eq $target) { Remove-Item -LiteralPath $key } }
$run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Test-Path -LiteralPath $run) { $value=(Get-ItemProperty -LiteralPath $run -Name BrisaDock -ErrorAction SilentlyContinue); if ($null -ne $value -and $value.BrisaDock -eq ('"'+$exe+'"')) { Remove-ItemProperty -LiteralPath $run -Name BrisaDock } }
foreach ($path in $paths) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force } }
$state.Files=@(); $state.Installed=$false; $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $target 'brisa-install.json') -Encoding utf8
Write-Output 'Brisa uninstalled. User data and backups remain in the data directory.'
