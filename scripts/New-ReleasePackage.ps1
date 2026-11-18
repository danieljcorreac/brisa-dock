param([string]$PackageDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'scripts/deployment/Common.ps1')
$version=([xml](Get-Content (Join-Path $repo 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$null=& (Join-Path $PSScriptRoot 'Get-ReleaseNotes.ps1') -Version $version
$release=Join-Path $repo "artifacts/releases/$version"
if (Test-Path -LiteralPath $release) { throw 'The version directory already exists. Archive it before preparing another candidate.' }
if (!$PackageDirectory) {
    $PackageDirectory=Join-Path $repo ('artifacts/release-build-'+[Guid]::NewGuid().ToString('N'))
    & (Join-Path $PSScriptRoot 'Build-Brisa.ps1') -OutputDirectory $PackageDirectory
}
$packageRoot=[IO.Path]::GetFullPath($PackageDirectory)
$null=Read-BrisaPackage $packageRoot
if ($null -eq (Read-BrisaPackage $packageRoot)) { throw 'The source is not a prepared package.' }
[IO.Directory]::CreateDirectory($release) | Out-Null
[IO.File]::WriteAllText((Join-Path $repo 'artifacts/release-build-path.txt'),$packageRoot)
$license=Join-Path $packageRoot 'Assets/Licenses/Brisa-MIT.txt'
Copy-Item -LiteralPath (Join-Path $repo LICENSE) -Destination $license
Copy-Item -LiteralPath (Join-Path $repo THIRD_PARTY_NOTICES.md) -Destination (Join-Path $packageRoot THIRD_PARTY_NOTICES.md)
$launcher='@echo off'+"`r`n"+'powershell.exe -NoProfile -File "%~dp0maintenance\Install-Brisa.ps1" -SourceDirectory "%~dp0." -Launch'+"`r`n"+'if errorlevel 1 pause'+"`r`n"
[IO.File]::WriteAllText((Join-Path $packageRoot 'Install-Brisa.cmd'),$launcher,[Text.ASCIIEncoding]::new())
& (Join-Path $repo 'scripts/Prepare-Package.ps1') -Directory $packageRoot
$manifest=Read-BrisaPackage $packageRoot
if ($manifest.Version -ne $version) { throw 'The executable version does not match the project.' }
$zip=Join-Path $release "Brisa-$version-win-x64.zip"
Add-Type -AssemblyName System.IO.Compression
$stream=[IO.File]::Create($zip)
try {
    $archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
    try {
        foreach ($relative in @($manifest.Files)+@('brisa-package.json')) {
            $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Get-BrisaPath $packageRoot $relative),$relative.Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
$setupBuild=Join-Path $repo ('artifacts/setup-build-'+[Guid]::NewGuid().ToString('N'))
& dotnet publish (Join-Path $repo 'tools/Brisa.Setup/Brisa.Setup.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$repo=/_/" "-p:BrisaPayloadZip=$zip" -o $setupBuild --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$setup=Join-Path $release "Brisa-Setup-$version-win-x64.exe"
Copy-Item -LiteralPath (Join-Path $setupBuild 'Brisa-Setup.exe') -Destination $setup
$probe=Start-Process -FilePath $setup -ArgumentList '--check-payload' -WindowStyle Hidden -PassThru
if (!$probe.WaitForExit(60000) -or $probe.ExitCode -ne 0) { throw 'Installer payload verification failed.' }
& (Join-Path $PSScriptRoot 'Get-ReleaseNotes.ps1') -Version $version -OutputFile (Join-Path $release 'RELEASE-NOTES.md')
$hashes=Get-ChildItem -LiteralPath $release -File | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_.Name }
[IO.File]::WriteAllLines((Join-Path $release SHA256SUMS.txt),$hashes,[Text.UTF8Encoding]::new($false))
Write-Output "Distribution prepared and verified at $release. Nothing has been uploaded."
