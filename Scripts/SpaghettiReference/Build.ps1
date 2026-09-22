$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$referenceRoot = Join-Path $projectRoot 'Temp/SpaghettiReferenceSource/animate'
New-Item -ItemType Directory -Path (Join-Path $referenceRoot 'internal') -Force | Out-Null
$animatePath = haxelib path flixel-animate | Where-Object { $_ -match '/src/$' } | Select-Object -First 1
foreach ($name in @('FlxAnimateFrames.hx', 'FlxAnimateJson.hx', 'internal/FilterRenderer.hx', 'internal/Timeline.hx'))
{
    $source = Get-Content -LiteralPath (Join-Path $animatePath "animate/$name") -Raw
    $source = $source.Replace('Vector<SymbolJson>', 'Array<SymbolJson>')
    $source = $source.Replace('override function destroy() {}', 'public function putWeak() {} override function destroy() {}')
    $source = $source.Replace('public var frameCount:Int;', 'public var frameCount:Int = 0;')
    if ($name -eq 'internal/FilterRenderer.hx')
    {
        $source = $source.Replace('Context3DGraphics.render(gfx, renderer);', 'context.setBlendFactors(ONE, ONE_MINUS_SOURCE_ALPHA); Context3DGraphics.render(gfx, renderer);')
    }
    Set-Content -LiteralPath (Join-Path $referenceRoot $name) -Value $source
}
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_SPAGHETTI_REFERENCE_PATH = Join-Path $projectRoot 'Builds/SpaghettiReference'
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Spaghetti reference build failed.' }
    $assets = Join-Path $projectRoot 'Builds/SpaghettiReference/compiled/neko/bin/assets'
    foreach ($file in Get-ChildItem -LiteralPath $assets -Filter '*.json' -Recurse -File)
    {
        $json = [IO.File]::ReadAllText($file.FullName)
        [IO.File]::WriteAllText($file.FullName, $json, [Text.UTF8Encoding]::new($false))
    }
    & (Join-Path $projectRoot 'Builds/SpaghettiReference/compiled/neko/bin/SpaghettiReference.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Spaghetti reference capture failed.' }
}
finally { Pop-Location }
