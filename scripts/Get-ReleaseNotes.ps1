param([Parameter(Mandatory)][string]$Version,[string]$OutputFile)
$ErrorActionPreference='Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version.' }
$repo=Split-Path -Parent $PSScriptRoot
$text=[IO.File]::ReadAllText((Join-Path $repo 'CHANGELOG.md'))
$pattern='(?ms)^## \['+[regex]::Escape($Version)+'\] - \d{4}-\d{2}-\d{2}\s*\r?\n(.*?)(?=^## \[|\z)'
$match=[regex]::Match($text,$pattern)
if (!$match.Success -or [string]::IsNullOrWhiteSpace($match.Groups[1].Value)) { throw 'The version has no release notes in CHANGELOG.md.' }
$notes=$match.Groups[1].Value.Trim()+[Environment]::NewLine
if ($OutputFile) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),$notes,[Text.UTF8Encoding]::new($false)) }
else { Write-Output $notes }
