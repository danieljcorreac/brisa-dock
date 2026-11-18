param([Parameter(Mandatory)][string]$RequestFile)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') { $env:PSModulePath = Join-Path $PSHOME 'Modules' }
. (Join-Path $PSScriptRoot 'Common.ps1')
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$requestPath = Get-BrisaPath $root (Split-Path -Leaf $RequestFile)
if ($requestPath -ne [IO.Path]::GetFullPath($RequestFile) -or (Split-Path -Leaf $RequestFile) -notmatch '^\.brisa-maintenance-[0-9a-f]{32}\.json$') { throw 'Invalid maintenance request.' }
$request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
$ready = [IO.Path]::ChangeExtension($requestPath, 'ready')
$cancel = [IO.Path]::ChangeExtension($requestPath, 'cancel')
$resultPath = Get-BrisaPath $root 'brisa-maintenance-result.json'
$parent = $null
$success = $false
$message = ''
try {
    if ($request.Action -notin @('update','restore') -or [IO.Path]::GetFullPath($request.Destination) -ne $root) { throw 'The request belongs to another Brisa installation.' }
    $parent = Get-Process -Id $request.ProcessId
    if ($parent.Path -ne (Get-BrisaPath $root 'BrisaDock.exe') -or $parent.StartTime.ToUniversalTime().Ticks -ne [long]$request.StartTicks) { throw 'The Brisa process no longer matches the request.' }
    if ($request.Action -eq 'update') {
        if (!$request.PSObject.Properties['PackageHash'] -or !$request.PackageHash -or (Get-FileHash -LiteralPath (Get-BrisaPath $request.Source 'brisa-package.json') -Algorithm SHA256).Hash -ne $request.PackageHash) { throw 'The package changed after review. Select it again.' }
        if ($null -eq (Read-BrisaPackage $request.Source)) { throw 'The selected directory is not a Brisa package.' }
    }
    else { $null = Test-BrisaRecovery $root }
    if (Test-Path -LiteralPath $cancel) { throw 'Preparation was cancelled before applying changes.' }
    [IO.File]::WriteAllText($ready, 'ready')
    if (!$parent.WaitForExit(60000)) { throw 'Brisa is still running. Its files have not been changed.' }
    if ($request.Action -eq 'update') {
        if ((Get-FileHash -LiteralPath (Get-BrisaPath $request.Source 'brisa-package.json') -Algorithm SHA256).Hash -ne $request.PackageHash) { throw 'The package changed after review. Select it again.' }
        & (Join-Path $PSScriptRoot 'Update-Brisa.ps1') -SourceDirectory $request.Source -Destination $root
    }
    else { & (Join-Path $PSScriptRoot 'Restore-Brisa.ps1') -Destination $root }
    $success = $true
}
catch { $message = $_.Exception.Message }
finally {
    @{ Succeeded=$success; Message=$message; RequestId=$request.Id; Time=[DateTimeOffset]::UtcNow.ToString('O') } | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding utf8
    if ($null -ne $parent -and $parent.HasExited -and (Test-Path -LiteralPath (Get-BrisaPath $root 'BrisaDock.exe'))) {
        if ($request.Qa) { Start-Process -FilePath (Get-BrisaPath $root 'BrisaDock.exe') -ArgumentList '--check-visual' -WindowStyle Hidden }
        else { Start-Process -FilePath (Get-BrisaPath $root 'BrisaDock.exe') -WindowStyle Hidden }
    }
    if (Test-Path -LiteralPath $ready) { Remove-Item -LiteralPath $ready -Force }
    if (Test-Path -LiteralPath $requestPath) { Remove-Item -LiteralPath $requestPath -Force }
    if (Test-Path -LiteralPath $cancel) { Remove-Item -LiteralPath $cancel -Force }
}
if (!$success) { throw $message }
