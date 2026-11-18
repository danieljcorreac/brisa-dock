Set-StrictMode -Version Latest
function Get-BrisaPath([string]$Root, [string]$Relative) {
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    if ([IO.Path]::IsPathRooted($Relative)) { throw 'Absolute package paths are not allowed.' }
    $full = [IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
    if (!$full.StartsWith($rootPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside the Brisa directory.' }
    $scan = $full
    while ($scan.Length -ge $rootPath.Length) {
        if ((Test-Path -LiteralPath $scan) -and ((Get-Item -LiteralPath $scan -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Package paths must not contain directory links.' }
        $scan = Split-Path -Parent $scan
        if ([string]::IsNullOrEmpty($scan)) { break }
    }
    return $full
}
function Read-BrisaInstall([string]$Root) {
    $rootPath = [IO.Path]::GetFullPath($Root)
    $marker = Get-BrisaPath $rootPath 'brisa-install.json'
    if (!(Test-Path -LiteralPath $marker)) { return $null }
    $state = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($state.Product -ne 'Brisa Dock' -or [IO.Path]::GetFullPath($state.Path) -ne $rootPath) { throw 'The directory does not match the installation record.' }
    return $state
}
function Assert-BrisaClosed([string]$Root) {
    $exe = Join-Path $Root 'BrisaDock.exe'
    if (Get-Process BrisaDock -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }) { throw 'Close this Brisa instance before updating or uninstalling it.' }
}
function Assert-BrisaPayloadName([string]$Relative) {
    $parts = $Relative.Replace('/','\').Split('\')
    if ([IO.Path]::IsPathRooted($Relative) -or @($parts | Where-Object { $_ -in @('','.','..') -or $_ -match '[:\x00-\x1f]' }).Count -gt 0 -or $parts[0] -in @('data','recovery')) { throw 'Disallowed package path.' }
    if ($Relative -like '.brisa*' -or $Relative -match '^(data|recovery|qa[^\\/]*|\.brisa[^\\/]*)[\\/]' -or $Relative -in @('brisa-install.json','brisa-package.json') -or $Relative -match '^brisa-.*\.(json|jsonl|log)(\.previous)?$') { throw 'The package contains a data or diagnostic path.' }
}
function Read-BrisaPackage([string]$Root) {
    $manifestPath = Get-BrisaPath $Root 'brisa-package.json'
    if (!(Test-Path -LiteralPath $manifestPath)) { return $null }
    if ((Get-Item -LiteralPath $manifestPath).Length -gt 4194304) { throw 'The package manifest is too large.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.Product -ne 'Brisa Dock' -or $manifest.Format -ne 1 -or $manifest.Platform -ne 'win-x64' -or @($manifest.Files).Count -gt 5000 -or @($manifest.Files).Count -eq 0) { throw 'The package is not a compatible Brisa version.' }
    $null = [version]$manifest.Version
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($relative in $manifest.Files) {
        Assert-BrisaPayloadName $relative
        $path = Get-BrisaPath $Root $relative
        if (!$seen.Add($relative.Replace('/','\')) -or !(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'The package contains duplicate paths or missing files.' }
        $hash = $manifest.Hashes.PSObject.Properties[$relative]
        if ($null -eq $hash -or $hash.Value -notmatch '^[0-9a-fA-F]{64}$' -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash.Value) { throw "File does not match the package: $relative" }
    }
    if (!$seen.Contains('BrisaDock.exe')) { throw 'The package does not contain Brisa.' }
    return $manifest
}
function Write-BrisaPackage([string]$Root, [string]$Version) {
    $hashes = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse) {
        $relative = $file.FullName.Substring($Root.TrimEnd('\').Length + 1)
        try { Assert-BrisaPayloadName $relative } catch { continue }
        $null = Get-BrisaPath $Root $relative
        $hashes[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    $manifest = @{ Product='Brisa Dock'; Format=1; Platform='win-x64'; Version=$Version; Files=@($hashes.Keys | Sort-Object); Hashes=$hashes }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Get-BrisaPath $Root 'brisa-package.json') -Encoding utf8
    return $manifest
}
function Test-BrisaRecovery([string]$Root) {
    $state = Read-BrisaInstall $Root
    if ($null -eq $state -or !$state.PSObject.Properties['PreviousRecovery'] -or !$state.PreviousRecovery) { throw 'No recoverable previous installation is available.' }
    $recovery = Get-BrisaPath $Root $state.PreviousRecovery
    $journal = Get-Content -LiteralPath (Get-BrisaPath $recovery 'rollback.json') -Raw | ConvertFrom-Json
    if ($journal.Product -ne 'Brisa Dock' -or $journal.Previous.Path -ne [IO.Path]::GetFullPath($Root)) { throw 'The recovery record belongs to another installation.' }
    foreach ($relative in $journal.Previous.Files) {
        Assert-BrisaPayloadName $relative
        $backup = Get-BrisaPath $recovery $relative
        $recoveryInput = if (Test-Path -LiteralPath $backup -PathType Leaf) { $backup } else { Get-BrisaPath $Root $relative }
        $hash = $journal.Previous.Hashes.PSObject.Properties[$relative]
        if ($null -eq $hash -or !(Test-Path -LiteralPath $recoveryInput -PathType Leaf) -or (Get-FileHash -LiteralPath $recoveryInput -Algorithm SHA256).Hash -ne $hash.Value) { throw "The previous copy is incomplete: $relative" }
    }
    return $journal
}
