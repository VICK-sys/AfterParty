param(
    [string]$ReferencePath = 'C:/Users/aaron/Downloads/funkin-windows-64bit'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$probeRoot = Join-Path $projectRoot 'Builds/FreeplayOriginalProbe'
robocopy $ReferencePath $probeRoot /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1 /XD mods screenshots logs
if ($LASTEXITCODE -ge 8) { throw 'Reference copy failed.' }
$moduleRoot = Join-Path $probeRoot 'mods/freeplay-probe'
New-Item -ItemType Directory -Path (Join-Path $moduleRoot 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '_polymod_meta.json') -Destination $moduleRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FreeplayProbe.hxc') -Destination (Join-Path $moduleRoot 'scripts')
$probe = Start-Process -FilePath (Join-Path $probeRoot 'Funkin.exe') -WorkingDirectory $probeRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $probeRoot 'verification-stdout.txt') `
    -RedirectStandardError (Join-Path $probeRoot 'verification-stderr.txt')
$probeStarted = $probe.StartTime
if (-not $probe.WaitForExit(60000)) { throw 'The reference capture did not finish within 60 seconds.' }
if ($probe.ExitCode -ne 0) { throw 'The reference capture failed.' }
$captures = Get-ChildItem (Join-Path $probeRoot 'captures') -Filter '*.json'
if ($captures.Count -ne 11 -or ($captures | Where-Object LastWriteTime -lt $probeStarted)) { throw 'Reference captures are missing or stale.' }
