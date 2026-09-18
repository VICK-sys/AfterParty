param([Parameter(Mandatory = $true)][string]$Source, [switch]$SkipBake)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$referenceRoot = Join-Path $projectRoot 'Temp/CreditsReferenceSource'
$stubs = @{
    'funkin/ui/MusicBeatState.hx' = 'package funkin.ui; class MusicBeatState extends flixel.FlxState { public var controls = {PAUSE:false, BACK_P:false}; }'
    'funkin/ui/FullScreenScaleMode.hx' = 'package funkin.ui; class FullScreenScaleMode { public static var gameNotchSize = flixel.math.FlxPoint.get(); }'
    'funkin/ui/mainmenu/MainMenuState.hx' = 'package funkin.ui.mainmenu; class MainMenuState extends flixel.FlxState {}'
    'funkin/util/TouchUtil.hx' = 'package funkin.util; class TouchUtil {}'
    'funkin/audio/FunkinSound.hx' = 'package funkin.audio; class FunkinSound { public static function playMusic(id:String, options:Dynamic):Void { flixel.FlxG.sound.music = new flixel.sound.FlxSound(); } }'
    'funkin/ui/credits/CreditsDataHandler.hx' = 'package funkin.ui.credits; class CreditsDataHandler { public static var CREDITS_DATA:CreditsData; public static function fetchBackerEntries():Array<String> { return []; } }'
    'Paths.hx' = 'class Paths { public static function image(id:String):openfl.display.BitmapData { return new openfl.display.BitmapData(1, 1, true, 0); } }'
}
foreach ($item in $stubs.GetEnumerator())
{
    $destination = Join-Path $referenceRoot $item.Key
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Set-Content -LiteralPath $destination -Value $item.Value
}
$creditsSource = Get-Content -LiteralPath (Join-Path $Source 'source/funkin/ui/credits/CreditsState.hx') -Raw
$creditsSource = $creditsSource.Replace('package funkin.ui.credits;', "package funkin.ui.credits;`nimport flixel.FlxG;`nimport funkin.ui.MusicBeatState;`nimport Paths;")
Set-Content -LiteralPath (Join-Path $referenceRoot 'funkin/ui/credits/CreditsState.hx') -Value $creditsSource
Copy-Item -LiteralPath (Join-Path $Source 'source/funkin/ui/credits/CreditsData.hx') -Destination (Join-Path $referenceRoot 'funkin/ui/credits/CreditsData.hx') -Force
$dependencies = Get-Content -LiteralPath (Join-Path $Source 'hmm.json') -Raw | ConvertFrom-Json
$flixel = $dependencies.dependencies | Where-Object { $_.name -eq 'flixel' }
$flixelPath = Join-Path $referenceRoot 'flixel/text/FlxText.hx'
New-Item -ItemType Directory -Path (Split-Path $flixelPath) -Force | Out-Null
$flixelUrl = 'https://raw.githubusercontent.com/FunkinCrew/flixel/' + $flixel.ref + '/flixel/text/FlxText.hx'
python -c 'import pathlib,sys,urllib.request; pathlib.Path(sys.argv[2]).write_bytes(urllib.request.urlopen(sys.argv[1], timeout=30).read())' $flixelUrl $flixelPath
if ($LASTEXITCODE -ne 0) { throw 'The pinned FlxText source could not be downloaded.' }
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_ROOT = $projectRoot
$env:UNITY_PARTY_CREDITS_REFERENCE_PATH = Join-Path $projectRoot 'Builds/CreditsReference'
$env:UNITY_PARTY_CREDITS_BAKE = if ($SkipBake) { '0' } else { '1' }
New-Item -ItemType Directory -Path (Join-Path $env:UNITY_PARTY_CREDITS_REFERENCE_PATH 'raw') -Force | Out-Null
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Credits reference build failed.' }
    & (Join-Path $projectRoot 'Builds/CreditsReference/compiled/neko/bin/CreditsReference.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Credits reference capture failed.' }
}
finally { Pop-Location }
