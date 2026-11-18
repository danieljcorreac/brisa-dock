param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\BrisaDock'),
    [Parameter(Mandatory)][string]$SourceDirectory,
    [string]$ShortcutDirectory = (Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'),
    [switch]$NoShortcut, [switch]$NoRegistration, [switch]$NoDataImport, [switch]$Launch,
    [switch]$AdoptPortable, [switch]$RecoverySource, [int]$TestFailAfter = 0
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$source = [IO.Path]::GetFullPath($SourceDirectory)
$target = [IO.Path]::GetFullPath($Destination)
if ($target.TrimEnd('\') -eq [IO.Path]::GetPathRoot($target).TrimEnd('\') -or $target -eq $source) { throw 'Choose a dedicated directory for Brisa.' }
$insideTarget = $source.StartsWith($target.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)
$ownRollback = $RecoverySource -and (Split-Path -Parent $source) -eq $target -and (Split-Path -Leaf $source) -match '^\.brisa-rollback-[0-9a-f]{32}$'
if ($target.StartsWith($source.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or ($insideTarget -and !$ownRollback)) { throw 'Source and destination must be separate directories.' }
if ($TestFailAfter -ne 0 -and $env:BRISA_ISOLATED_TESTS -ne '1') { throw 'Failure injection is only available in isolated tests.' }
$sourceManifest = Read-BrisaPackage $source
if (!(Test-Path -LiteralPath (Join-Path $source 'BrisaDock.exe'))) { throw 'Build Brisa with scripts/Build-Brisa.ps1 first.' }
$runtimeConfig=Join-Path $source 'BrisaDock.runtimeconfig.json'
$selfContained=$false
if (Test-Path -LiteralPath $runtimeConfig) {
    $runtime=Get-Content -LiteralPath $runtimeConfig -Raw | ConvertFrom-Json
    $selfContained=$null -ne $runtime.runtimeOptions.PSObject.Properties['includedFrameworks'] -and (Test-Path -LiteralPath (Join-Path $source 'hostfxr.dll')) -and (Test-Path -LiteralPath (Join-Path $source 'hostpolicy.dll')) -and (Test-Path -LiteralPath (Join-Path $source 'coreclr.dll'))
}
if (!$selfContained) {
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'This package needs .NET Runtime 10 x64. Use the self-contained package or install the official runtime.' }
    $runtimes = & dotnet --list-runtimes
    if ($LASTEXITCODE -ne 0 -or !($runtimes -match '^Microsoft.NETCore.App 10\.')) { throw 'This edition requires .NET Runtime 10 x64.' }
}
$old = Read-BrisaInstall $target
Assert-BrisaClosed $target
if ($AdoptPortable -and $null -eq $old) {
    $currentExe = Get-BrisaPath $target 'BrisaDock.exe'
    if (!(Test-Path -LiteralPath $currentExe) -or (Get-Item -LiteralPath $currentExe).VersionInfo.ProductName -ne 'BrisaDock') { throw 'The directory is not a recognized portable Brisa installation.' }
    $baseline = Write-BrisaPackage $target (Get-Item -LiteralPath $currentExe).VersionInfo.ProductVersion
    $old = [pscustomobject]@{ Product='Brisa Dock'; Path=$target; Version=$baseline.Version; Installed=$true; Registered=$false; Files=$baseline.Files; Hashes=$baseline.Hashes; Shortcut=$null }
}
if ((Test-Path -LiteralPath $target) -and $null -eq $old -and @(Get-ChildItem -LiteralPath $target -Force).Count -gt 0) { throw 'The directory contains files unrelated to this installation.' }
[IO.Directory]::CreateDirectory($target) | Out-Null
$marker = Get-BrisaPath $target 'brisa-install.json'
$stageName = '.brisa-stage-' + [Guid]::NewGuid().ToString('N')
$stage = Get-BrisaPath $target $stageName
$recoveryName = 'recovery\' + [DateTime]::Now.ToString('yyyyMMdd-HHmmssfff')
$recovery = Get-BrisaPath $target $recoveryName
$package = @{}
if ($null -ne $sourceManifest) {
    foreach ($relative in $sourceManifest.Files) { $package[$relative] = Get-BrisaPath $source $relative }
}
else {
    foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse) {
        $relative = $file.FullName.Substring($source.TrimEnd('\').Length + 1)
        try { Assert-BrisaPayloadName $relative } catch { continue }
        $package[$relative] = $file.FullName
    }
}
foreach ($name in @('Install-Brisa.ps1','Update-Brisa.ps1','Restore-Brisa.ps1','Uninstall-Brisa.ps1','Common.ps1','Invoke-Maintenance.ps1')) { $relative='maintenance\'+$name; if (!$package.ContainsKey($relative)) { $package[$relative] = Join-Path $PSScriptRoot $name } }
$changed = [Collections.Generic.List[object]]::new()
$oldMarker = if (Test-Path -LiteralPath $marker) { Get-Content -LiteralPath $marker -Raw } else { $null }
$packageMarker = Get-BrisaPath $target 'brisa-package.json'
$oldPackageMarker = if (Test-Path -LiteralPath $packageMarker) { Get-Content -LiteralPath $packageMarker -Raw } else { $null }
$hashes = @{}
$previous = $null
try {
    if ($null -ne $old -and $old.PSObject.Properties['Hashes']) {
        $previous = $old | ConvertTo-Json -Depth 12 | ConvertFrom-Json
        foreach ($relative in $old.Files) {
            Assert-BrisaPayloadName $relative
            $priorFile = Get-BrisaPath $target $relative
            if (!(Test-Path -LiteralPath $priorFile -PathType Leaf)) { $previous = $null; break }
            $previous.Hashes.PSObject.Properties[$relative].Value = (Get-FileHash -LiteralPath $priorFile -Algorithm SHA256).Hash
        }
    }
    if ($null -ne $old -and (Test-Path -LiteralPath (Get-BrisaPath $target 'data\dock.json'))) {
        $dataSnapshot = Get-BrisaPath $recovery 'data-before-update\dock.json'
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dataSnapshot)) | Out-Null
        Copy-Item -LiteralPath (Get-BrisaPath $target 'data\dock.json') -Destination $dataSnapshot
    }
    foreach ($relative in $package.Keys) {
        $output = Get-BrisaPath $stage $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
        Copy-Item -LiteralPath $package[$relative] -Destination $output
        $expectedHash = (Get-FileHash -LiteralPath $package[$relative] -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash -ne $expectedHash) { throw "Copied file does not match: $relative" }
        $hashes[$relative] = $expectedHash
    }
    foreach ($relative in $package.Keys) {
        $output = Get-BrisaPath $target $relative
        $existed = Test-Path -LiteralPath $output
        if ($existed -and (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash -eq $hashes[$relative]) { continue }
        if ($existed) {
            $backup = Get-BrisaPath $recovery $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
            Copy-Item -LiteralPath $output -Destination $backup
        }
        $changed.Add(@{ Relative = $relative; Existed = $existed })
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output)) | Out-Null
        Copy-Item -LiteralPath (Get-BrisaPath $stage $relative) -Destination $output -Force
        if ($TestFailAfter -gt 0 -and $changed.Count -ge $TestFailAfter) { throw 'Simulated update failure in an isolated test.' }
    }
    if ($null -ne $old) {
        foreach ($relative in $old.Files) {
            if ($package.ContainsKey($relative)) { continue }
            Assert-BrisaPayloadName $relative
            $output = Get-BrisaPath $target $relative
            if (!(Test-Path -LiteralPath $output -PathType Leaf)) { continue }
            $backup = Get-BrisaPath $recovery $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
            Copy-Item -LiteralPath $output -Destination $backup
            $changed.Add(@{ Relative=$relative; Existed=$true })
            Remove-Item -LiteralPath $output -Force
        }
    }
    $data = Get-BrisaPath $target 'data'
    [IO.Directory]::CreateDirectory($data) | Out-Null
    $config = Get-BrisaPath $target 'data\dock.json'
    if (!$NoDataImport -and !(Test-Path -LiteralPath $config) -and (Test-Path -LiteralPath (Join-Path $source 'data\dock.json'))) { Copy-Item -LiteralPath (Join-Path $source 'data\dock.json') -Destination $config }
    $version = if ($null -ne $sourceManifest) { $sourceManifest.Version } else { (Get-Item -LiteralPath (Join-Path $source 'BrisaDock.exe')).VersionInfo.ProductVersion }
    if ($null -ne $previous) {
        [IO.Directory]::CreateDirectory($recovery) | Out-Null
        @{ Product='Brisa Dock'; Previous=$previous; Changes=@($changed.ToArray()) } | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Get-BrisaPath $recovery 'rollback.json') -Encoding utf8
    }
    $priorShortcut = if ($null -ne $old -and $old.PSObject.Properties['Shortcut']) { $old.Shortcut } else { $null }
    $manifest = @{ Product='Brisa Dock'; Path=$target; Version=$version; Registered=(!$NoRegistration); PreviousVersion=$(if ($null -ne $previous) { $previous.Version } else { $null }); PreviousRecovery=$(if ($null -ne $previous) { $recoveryName } else { $null }); UpdatedAt=[DateTimeOffset]::UtcNow.ToString('O'); Installed=$true; Files=@($package.Keys | Sort-Object); Hashes=$hashes; Shortcut=$priorShortcut }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $marker -Encoding utf8
    @{ Product='Brisa Dock'; Format=1; Platform='win-x64'; Version=$version; Files=$manifest.Files; Hashes=$hashes } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $packageMarker -Encoding utf8
}
catch {
    foreach ($entry in $changed) {
        $output = Get-BrisaPath $target $entry.Relative
        if ($entry.Existed) { Copy-Item -LiteralPath (Get-BrisaPath $recovery $entry.Relative) -Destination $output -Force }
        elseif (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }
    }
    if ($null -ne $oldMarker) { [IO.File]::WriteAllText($marker, $oldMarker) } elseif (Test-Path -LiteralPath $marker) { Remove-Item -LiteralPath $marker }
    if ($null -ne $oldPackageMarker) { [IO.File]::WriteAllText($packageMarker, $oldPackageMarker) } elseif (Test-Path -LiteralPath $packageMarker) { Remove-Item -LiteralPath $packageMarker }
    throw
}
finally {
    $checkedStage = Get-BrisaPath $target $stageName
    if ((Split-Path -Parent $checkedStage) -eq $target -and (Split-Path -Leaf $checkedStage) -match '^\.brisa-stage-[0-9a-f]{32}$' -and (Test-Path -LiteralPath $checkedStage)) { Remove-Item -LiteralPath $checkedStage -Recurse -Force }
}
$installedExe = Get-BrisaPath $target 'BrisaDock.exe'
if (!$NoShortcut) {
    [IO.Directory]::CreateDirectory($ShortcutDirectory) | Out-Null
    $linkPath = Join-Path $ShortcutDirectory 'Brisa Dock.lnk'
    $shell = New-Object -ComObject WScript.Shell
    try { $link=$shell.CreateShortcut($linkPath); $link.TargetPath=$installedExe; $link.WorkingDirectory=$target; $link.IconLocation=$installedExe+',0'; $link.Save() }
    finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null }
    $manifest.Shortcut = [IO.Path]::GetFullPath($linkPath)
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $marker -Encoding utf8
}
if (!$NoRegistration) {
    $key='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BrisaDock'
    New-Item -Path $key -Force | Out-Null
    $uninstall = 'powershell.exe -NoProfile -File "' + (Get-BrisaPath $target 'maintenance/Uninstall-Brisa.ps1') + '"'
    foreach ($entry in @{ DisplayName='Brisa Dock'; DisplayVersion=$version; Publisher='Brisa Dock'; InstallLocation=$target; DisplayIcon=$installedExe; UninstallString=$uninstall }.GetEnumerator()) { New-ItemProperty -LiteralPath $key -Name $entry.Key -Value $entry.Value -PropertyType String -Force | Out-Null }
    New-ItemProperty -LiteralPath $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -LiteralPath $key -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
}
Write-Output "Brisa $version installed. User data is preserved at $target\data."
if ($Launch) { Start-Process -FilePath $installedExe -WindowStyle Hidden }
