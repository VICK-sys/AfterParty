$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$env:FREEPLAY_REFERENCE_OUTPUT = Join-Path $projectRoot 'Builds/FreeplayReference'
$env:FREEPLAY_BLUR_SHADER = 'C:/Users/aaron/Downloads/funkin-windows-64bit/assets/shaders/gaussianBlur.frag'
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
New-Item -ItemType Directory -Path $env:FREEPLAY_REFERENCE_OUTPUT -Force | Out-Null
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Freeplay reference build failed.' }
    & (Join-Path $projectRoot 'Builds/FreeplayReference/compiled/neko/bin/FreeplayReference.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Freeplay reference capture failed.' }
}
finally { Pop-Location }
