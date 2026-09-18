param([Parameter(Mandatory = $true)][string]$Source)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$referenceSource = Join-Path $projectRoot 'Temp/TitleReferenceSource/funkin/ui'
New-Item -ItemType Directory -Path $referenceSource -Force | Out-Null
$atlasSource = Get-Content -LiteralPath (Join-Path $Source 'source/funkin/ui/AtlasText.hx') -Raw
$atlasSource = $atlasSource.Replace('package funkin.ui;', "package funkin.ui;`nimport flixel.FlxG;`nimport Paths;")
Set-Content -LiteralPath (Join-Path $referenceSource 'AtlasText.hx') -Value $atlasSource
$filterPath = Join-Path $projectRoot 'Temp/TitleReferenceSource/animate/internal'
New-Item -ItemType Directory -Path $filterPath -Force | Out-Null
$animatePath = haxelib path flixel-animate | Where-Object { $_ -match '/src/$' } | Select-Object -First 1
$filterSource = Get-Content -LiteralPath (Join-Path $animatePath 'animate/internal/FilterRenderer.hx') -Raw
$filterSource = $filterSource.Replace('override function destroy() {}', 'public function putWeak() {} override function destroy() {}')
Set-Content -LiteralPath (Join-Path $filterPath 'FilterRenderer.hx') -Value $filterSource
$timelineSource = Get-Content -LiteralPath (Join-Path $animatePath 'animate/internal/Timeline.hx') -Raw
$timelineSource = $timelineSource.Replace('public var frameCount:Int;', 'public var frameCount:Int = 0;')
Set-Content -LiteralPath (Join-Path $filterPath 'Timeline.hx') -Value $timelineSource
foreach ($name in @('FlxAnimateFrames.hx', 'FlxAnimateJson.hx'))
{
    $animateSource = Get-Content -LiteralPath (Join-Path $animatePath "animate/$name") -Raw
    $animateSource = $animateSource.Replace('Vector<SymbolJson>', 'Array<SymbolJson>')
    Set-Content -LiteralPath (Join-Path $projectRoot "Temp/TitleReferenceSource/animate/$name") -Value $animateSource
}
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_TITLE_REFERENCE_PATH = Join-Path $projectRoot 'Builds/TitleReference'
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Title reference build failed.' }
    & (Join-Path $projectRoot 'Builds/TitleReference/compiled/neko/bin/TitleReference.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Title reference capture failed.' }
}
finally { Pop-Location }
