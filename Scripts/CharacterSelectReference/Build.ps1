$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$referenceRoot = Join-Path $projectRoot 'Builds/CharacterSelectReferenceSource/animate'
New-Item -ItemType Directory -Path (Join-Path $referenceRoot 'internal/elements') -Force | Out-Null
$animatePath = haxelib path flixel-animate | Where-Object { $_ -match '/src/$' } | Select-Object -First 1
foreach ($name in @('FlxAnimateFrames.hx', 'FlxAnimateJson.hx', 'internal/FilterRenderer.hx', 'internal/Timeline.hx'))
{
    $source = Get-Content -LiteralPath (Join-Path $animatePath "animate/$name") -Raw
    $source = $source.Replace('Vector<SymbolJson>', 'Array<SymbolJson>')
    $source = $source.Replace('override function destroy() {}', 'public function putWeak() {} override function destroy() {}')
    $source = $source.Replace('public var frameCount:Int;', 'public var frameCount:Int = 0;')
    Set-Content -LiteralPath (Join-Path $referenceRoot $name) -Value $source
}
$atlasSource = Get-Content -LiteralPath (Join-Path $animatePath 'animate/internal/elements/AtlasInstance.hx') -Raw
$atlasSource = $atlasSource.Replace('public var frame:FlxFrame;', 'public static var overlayPass:Int = 0; public static var overlaySeen:Bool = false; public var frame:FlxFrame;')
$atlasSource = $atlasSource.Replace('if (frame == null || frame.frame == null)', 'if (blend == OVERLAY) overlaySeen = true; if (overlayPass == 3 && (!overlaySeen || blend == OVERLAY)) return; if (overlayPass == 1 && blend == OVERLAY) return; if (overlayPass == 2 && blend != OVERLAY) return; if (overlayPass == 2) blend = NORMAL; if (frame == null || frame.frame == null)')
Set-Content -LiteralPath (Join-Path $referenceRoot 'internal/elements/AtlasInstance.hx') -Value $atlasSource
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_CHARACTER_REFERENCE_PATH = Join-Path $projectRoot 'Builds/CharacterSelectReference'
New-Item -ItemType Directory -Path $env:UNITY_PARTY_CHARACTER_REFERENCE_PATH -Force | Out-Null
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Character select reference build failed.' }
    $assets = Join-Path $projectRoot 'Builds/CharacterSelectReference/compiled/neko/bin/assets'
    foreach ($file in Get-ChildItem -LiteralPath $assets -Filter '*.json' -Recurse -File)
    {
        $json = [IO.File]::ReadAllText($file.FullName)
        [IO.File]::WriteAllText($file.FullName, $json, [Text.UTF8Encoding]::new($false))
    }
    & (Join-Path $projectRoot 'Builds/CharacterSelectReference/compiled/neko/bin/CharacterSelectReference.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Character select reference capture failed.' }
}
finally { Pop-Location }
