param([Parameter(Mandatory = $true)][string]$Source)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$referenceSource = Join-Path $projectRoot 'Temp/PauseReferenceSource/funkin/ui'
New-Item -ItemType Directory -Path $referenceSource -Force | Out-Null
$atlasSource = Get-Content -LiteralPath (Join-Path $Source 'source/funkin/ui/AtlasText.hx') -Raw
$atlasSource = $atlasSource.Replace('package funkin.ui;', "package funkin.ui;`nimport flixel.FlxG;`nimport Paths;")
Set-Content -LiteralPath (Join-Path $referenceSource 'AtlasText.hx') -Value $atlasSource
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_PAUSE_REFERENCE_PATH = Join-Path $projectRoot 'Builds/PauseReference'
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Pause reference build failed.' }
    & (Join-Path $projectRoot 'Builds/PauseReference/compiled/neko/bin/PauseReference.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Pause reference capture failed.' }
}
finally { Pop-Location }
