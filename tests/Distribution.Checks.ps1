param([string]$SourceDirectory,[switch]$RequireRelease)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts/deployment/Common.ps1')
$script:checks=0
function Check([bool]$Value,[string]$Name) { if (!$Value) { throw "FAIL: $Name" }; $script:checks++; Write-Output "PASS: $Name" }
if (!$SourceDirectory) {
    $marker=Join-Path $repo 'artifacts/release-build-path.txt'
    $SourceDirectory=if (Test-Path -LiteralPath $marker) { (Get-Content -LiteralPath $marker -Raw).Trim() } else { Join-Path $repo 'artifacts/build' }
}
$package=Read-BrisaPackage ([IO.Path]::GetFullPath($SourceDirectory))
Check ($null -ne $package) 'Distribution has a complete verified package manifest'
Check (@($package.Files | Where-Object { $_ -match '(?i)(^|[\\/])(data|qa-data|recovery|auth\.json|credentials\.json)([\\/]|$)|\.(pdb|pfx|pem|key|log|jsonl)$' }).Count -eq 0) 'Distribution excludes account data, signing keys, debug symbols and diagnostics'
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$notes=& (Join-Path $repo 'scripts/Get-ReleaseNotes.ps1') -Version $version
Check (($notes -join "`n") -match '(?m)^### (Added|Changed|Fixed|Removed|Security|Deprecated)\r?$') 'Version has curated changelog notes'
$badVersion=$false
try { & (Join-Path $repo 'scripts/Get-ReleaseNotes.ps1') -Version 'invalid/version' | Out-Null } catch { $badVersion=$true }
Check $badVersion 'Release notes reject invalid version input'
& (Join-Path $repo 'scripts/Test-Repository.ps1')
Check ($LASTEXITCODE -eq 0) 'Repository publication audit passes'
$ignored=& git -C $repo check-ignore 'qa/private.json' 'artifacts/example.zip' 'local/app/data/dock.json' '.env' 'test.pfx'
Check ($ignored.Count -eq 5) 'Git excludes local evidence, installations, account configuration and keys'

# A self-contained package installs without finding a global dotnet executable.
$qa=Join-Path $repo ('qa/distribution-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($qa) | Out-Null
$target=Join-Path $qa installed
$system=[Environment]::GetFolderPath('System')
$info=[Diagnostics.ProcessStartInfo]::new((Join-Path $system 'WindowsPowerShell/v1.0/powershell.exe'))
$info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
foreach ($arg in @('-NoProfile','-File',(Join-Path $repo 'scripts/deployment/Install-Brisa.ps1'),'-SourceDirectory',[IO.Path]::GetFullPath($SourceDirectory),'-Destination',$target,'-NoShortcut','-NoRegistration','-NoDataImport')) { $info.ArgumentList.Add($arg) }
$info.Environment['PATH']=''
$info.Environment['PSModulePath']=Join-Path $system 'WindowsPowerShell/v1.0/Modules'
$process=[Diagnostics.Process]::Start($info)
$output=$process.StandardOutput.ReadToEndAsync(); $errorOutput=$process.StandardError.ReadToEndAsync()
if (!$process.WaitForExit(60000)) { throw 'The isolated installation test timed out.' }
$null=$output.GetAwaiter().GetResult(); $errorText=$errorOutput.GetAwaiter().GetResult()
Check ($process.ExitCode -eq 0 -and (Test-Path -LiteralPath (Join-Path $target BrisaDock.exe))) 'Self-contained package installs without global .NET or user registration'
$process.Dispose()

$release=Join-Path $repo "artifacts/releases/$version"
if ($RequireRelease -or (Test-Path -LiteralPath $release)) {
    $setup=Join-Path $release "Brisa-Setup-$version-win-x64.exe"
    $zip=Join-Path $release "Brisa-$version-win-x64.zip"
    Check ((Test-Path -LiteralPath $setup) -and (Test-Path -LiteralPath $zip)) 'Release contains installer and ZIP'
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $release SHA256SUMS.txt))) {
        if ($line -notmatch '^([0-9a-f]{64})  ([^\\/:]+)$' -or !$seen.Add($Matches[2])) { throw 'The checksum list contains an invalid entry.' }
        Check ((Get-FileHash -LiteralPath (Join-Path $release $Matches[2]) -Algorithm SHA256).Hash -eq $Matches[1]) ('Release hash matches '+$Matches[2])
    }
    Check ($seen.Count -eq 3) 'All installer, ZIP and release-note hashes are present'
    $probe=Start-Process -FilePath $setup -ArgumentList '--check-payload' -WindowStyle Hidden -PassThru
    Check ($probe.WaitForExit(30000) -and $probe.ExitCode -eq 0) 'Installer validates its embedded archive without installing or showing a window'
    $probe.Dispose()
    $layoutProbe=Start-Process -FilePath $setup -ArgumentList '--check-layout' -WindowStyle Hidden -PassThru
    Check ($layoutProbe.WaitForExit(30000) -and $layoutProbe.ExitCode -eq 0) 'Installer validates supported layouts and busy states without displaying a window'
    $layoutProbe.Dispose()
    # Exercise the real batch launcher with an inert installer in a path containing spaces.
    $launcherRoot=Join-Path $qa 'package with spaces'
    $maintenance=Join-Path $launcherRoot 'maintenance'
    [IO.Directory]::CreateDirectory($maintenance) | Out-Null
    Add-Type -AssemblyName System.IO.Compression
    $zipInput=[IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $entry=$zipInput.GetEntry('Install-Brisa.cmd')
        if (!$entry) { throw 'The release ZIP has no English installer launcher.' }
        $reader=[IO.StreamReader]::new($entry.Open())
        try { $launcherText=$reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $zipInput.Dispose() }
    [IO.File]::WriteAllText((Join-Path $launcherRoot 'Install-Brisa.cmd'),$launcherText,[Text.ASCIIEncoding]::new())
    $stub=@'
param([string]$SourceDirectory,[switch]$Launch)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot '../launcher-arguments.json'),(@{ Source=$SourceDirectory; Launch=[bool]$Launch } | ConvertTo-Json))
'@
    [IO.File]::WriteAllText((Join-Path $maintenance 'Install-Brisa.ps1'),$stub,[Text.UTF8Encoding]::new($true))
    $launcherInfo=[Diagnostics.ProcessStartInfo]::new((Join-Path $system 'cmd.exe'))
    $launcherInfo.UseShellExecute=$false; $launcherInfo.CreateNoWindow=$true; $launcherInfo.WorkingDirectory=$launcherRoot
    foreach ($argument in @('/d','/c','Install-Brisa.cmd')) { $launcherInfo.ArgumentList.Add($argument) }
    $launcherProcess=[Diagnostics.Process]::Start($launcherInfo)
    if (!$launcherProcess.WaitForExit(30000)) { $launcherProcess.Kill($true); throw 'The batch launcher fixture timed out.' }
    $arguments=Get-Content -LiteralPath (Join-Path $launcherRoot 'launcher-arguments.json') -Raw | ConvertFrom-Json
    Check ($launcherProcess.ExitCode -eq 0 -and [IO.Path]::GetFullPath($arguments.Source).TrimEnd('\') -eq $launcherRoot -and $arguments.Launch) 'English batch launcher preserves package paths containing spaces'
    $launcherProcess.Dispose()
}
Write-Output "$script:checks distribution checks passed."
