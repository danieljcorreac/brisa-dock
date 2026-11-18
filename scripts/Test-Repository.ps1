param([string]$Root=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$rootPath=[IO.Path]::GetFullPath($Root)
if (!(Test-Path -LiteralPath (Join-Path $rootPath '.git'))) { throw 'Initialize Git and stage only publishable files before auditing the index.' }
$raw=& git -C $rootPath ls-files --cached -z
if ($LASTEXITCODE -ne 0) { throw 'Could not read the Git index.' }
$files=($raw -join "`n").Split([char]0,[StringSplitOptions]::RemoveEmptyEntries)
if ($files.Count -eq 0) { throw 'The Git index is empty.' }
$forbiddenBrand=-join ([char[]]@(108,117,109,97,98,97,114))
$obsoleteVariant=-join ([char[]]@(108,117,109,98,97,114))
$issues=[Collections.Generic.List[string]]::new()
foreach ($relative in $files) {
    $name=$relative.Replace('\','/')
    if ($name -match '[^\x20-\x7e]') { $issues.Add("File name must use English/ASCII characters: $name") }
    if ($name -notmatch '/' -and $name -match '\.(ps1|cmd)$') { $issues.Add("Operational scripts belong under scripts/: $name") }
    if ($name -match '(^|/)(bin|obj|qa|qa-data|data|recovery|dist|dist-v2|bridge-dist|build-review|artifacts|local|node_modules)(/|$)' -or $name -match '(^|/)(auth|credentials|dock|claude-usage)\.json$' -or $name -match '(?i)\.(pfx|p12|pem|key|snk|log|jsonl|pdb|exe|dll)$' -or $name -match '(^|/)\.env($|\.)') { $issues.Add("Unpublishable path: $name"); continue }
    if ($name -match "(?i)$forbiddenBrand|$obsoleteVariant") { $issues.Add("Disallowed name: $name") }
    $path=Join-Path $rootPath $relative
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { $issues.Add("Missing file: $name"); continue }
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { $issues.Add("Disallowed link: $name"); continue }
    if ($name -match '(?i)\.(png|jpe?g|ico|gif|webp)$') { continue }
    $blob=& git -C $rootPath show --no-textconv ":$relative"
    if ($LASTEXITCODE -ne 0) { $issues.Add("Could not read indexed content: $name"); continue }
    $text=$blob -join "`n"
    if ($text -match "(?i)$forbiddenBrand|$obsoleteVariant") { $issues.Add("Disallowed reference: $name") }
    if ($text -match '(?i)[a-z]:[\\/]Users[\\/](?!Public[\\/]|Example[\\/]|TestUser[\\/])' -or $text -match '[a-zA-Z0-9._%+-]+@gmail\.com') { $issues.Add("Personal path or contact: $name") }
    if ($text -match '(?i)(?:gh[pousr]_[a-z0-9]{25,}|github_pat_[a-z0-9_]{30,}|sk-ant-[a-z0-9_-]{20,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)') { $issues.Add("Possible secret: $name") }
}
foreach ($required in @('README.md','LICENSE','CONTRIBUTING.md','SECURITY.md','CHANGELOG.md','AGENTS.md','Brisa.slnx','docs/LAYOUT.md','scripts/Prepare-Package.ps1','scripts/deployment/Install-Brisa.ps1','scripts/deployment/Common.ps1','.github/workflows/ci.yml','.github/workflows/release.yml')) { if ($required -notin $files) { $issues.Add("Missing maintained file: $required") } }
if ($issues.Count) { $issues | ForEach-Object { Write-Output $_ }; throw "The index audit found $($issues.Count) issues." }
Write-Output "Index audit passed: $($files.Count) publishable files; no private paths or retired references detected. Manual review is still required."
