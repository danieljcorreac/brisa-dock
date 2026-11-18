param([switch]$Review)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$relative = if ($Review) { 'artifacts/build' } else { 'local/app' }
$directory = Join-Path $repo $relative
$executable = Join-Path $directory 'BrisaDock.exe'
if (!(Test-Path -LiteralPath $executable -PathType Leaf)) {
    if ($Review) { throw 'Build the review copy first with scripts/Build-Brisa.ps1.' }
    throw 'No personal installation exists under local/app. Install a prepared package or use -Review after building.'
}
if ($Review) { Start-Process -FilePath $executable -WorkingDirectory $directory -ArgumentList '--check-visual --review-fixtures' -WindowStyle Hidden }
else { Start-Process -FilePath $executable -WorkingDirectory $directory -WindowStyle Hidden }
Write-Output "Started Brisa from $relative."
