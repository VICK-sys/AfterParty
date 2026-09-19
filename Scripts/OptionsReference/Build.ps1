param([Parameter(Mandatory = $true)][string]$Source)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$referenceSource = Join-Path $projectRoot 'Temp/OptionsReferenceSource/funkin/ui'
New-Item -ItemType Directory -Path $referenceSource -Force | Out-Null
$atlasSource = Get-Content -LiteralPath (Join-Path $Source 'source/funkin/ui/AtlasText.hx') -Raw
$atlasSource = $atlasSource.Replace('package funkin.ui;', "package funkin.ui;`nimport flixel.FlxG;`nimport Paths;")
Set-Content -LiteralPath (Join-Path $referenceSource 'AtlasText.hx') -Value $atlasSource
$checkboxSource = Join-Path $referenceSource 'options/items'
New-Item -ItemType Directory -Path $checkboxSource -Force | Out-Null
$checkbox = Get-Content -LiteralPath (Join-Path $Source 'source/funkin/ui/options/items/CheckboxPreferenceItem.hx') -Raw
$checkbox = $checkbox.Replace('package funkin.ui.options.items;', "package funkin.ui.options.items;`nimport Paths;")
Set-Content -LiteralPath (Join-Path $checkboxSource 'CheckboxPreferenceItem.hx') -Value $checkbox
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_OPTIONS_REFERENCE_PATH = Join-Path $projectRoot 'Builds/OptionsReference'
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Options reference build failed.' }
    foreach ($page in @('root', 'preferences', 'controls'))
    {
        $env:UNITY_PARTY_OPTIONS_REFERENCE_PAGE = $page
        & (Join-Path $projectRoot 'Builds/OptionsReference/compiled/neko/bin/OptionsReference.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Options reference capture failed.' }
    }
}
finally { Pop-Location }
