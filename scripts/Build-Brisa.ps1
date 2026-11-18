param([string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/build'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory)
$prefix = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\') + '\'
if (!$output.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Isolated builds must stay under artifacts.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
Push-Location -LiteralPath $repo
try {
    & dotnet publish src/Brisa.WinUI/Brisa.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$repo=/_/" -o $output --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'WinUI build failed.' }
    & dotnet publish src/Brisa.Bridge/Brisa.Bridge.csproj -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false "-p:PathMap=$repo=/_/" -o (Join-Path $output bridge) --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Bridge build failed.' }
    & (Join-Path $repo 'scripts/Prepare-Package.ps1') -Directory $output
}
finally { Pop-Location }
Write-Output "Isolated build ready: $output"
