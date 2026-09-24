$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $projectRoot 'Builds/PhillyBackgroundReference'
$source = Join-Path $output 'source'
$dependencies = @{
    'flixel/FlxStrip.hx' = 'https://raw.githubusercontent.com/FunkinCrew/flixel/141f23c400c0508c76d5a09a143f5ce6790f8122/flixel/FlxStrip.hx'
    'flixel/addons/display/FlxTiledSprite.hx' = 'https://raw.githubusercontent.com/FunkinCrew/flixel-addons/7628435364c4a45440d1e5db656741b66a8cd2ec/flixel/addons/display/FlxTiledSprite.hx'
    'flixel/addons/display/FlxBackdrop.hx' = 'https://raw.githubusercontent.com/FunkinCrew/flixel-addons/7628435364c4a45440d1e5db656741b66a8cd2ec/flixel/addons/display/FlxBackdrop.hx'
}
foreach ($file in $dependencies.Keys)
{
    $target = Join-Path $source $file
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    if (!(Test-Path -LiteralPath $target)) { Invoke-WebRequest -Uri $dependencies[$file] -OutFile $target }
}
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:PHILLY_REFERENCE_BUNDLES = Join-Path $projectRoot 'Assets/StreamingAssets/Bundles'
$env:PHILLY_REFERENCE_OUTPUT = $output
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Philly reference build failed.' }
    foreach ($stage in @('phillyStreets', 'phillyStreetsErect'))
    {
        $env:PHILLY_REFERENCE_STAGE = $stage
        $process = Start-Process -FilePath (Join-Path $output 'compiled/neko/bin/PhillyBackgroundReference.exe') -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "Philly reference capture failed: $stage" }
    }
}
finally { Pop-Location }
