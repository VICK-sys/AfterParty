$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_FREEPLAY_FONT_PATH = Join-Path $projectRoot 'Assets/Resources/VanillaFreeplay/fonts/header'
$env:UNITY_PARTY_FREEPLAY_REFERENCE_PATH = Join-Path $projectRoot 'Builds/FreeplayHeaderReference'
New-Item -ItemType Directory -Force $env:UNITY_PARTY_FREEPLAY_FONT_PATH, $env:UNITY_PARTY_FREEPLAY_REFERENCE_PATH | Out-Null
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Header font build failed.' }
    $process = Start-Process -FilePath (Join-Path $projectRoot 'Builds/FreeplayHeaderFont/neko/bin/FreeplayHeaderFont.exe') -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw 'Header font export failed.' }
}
finally { Pop-Location }
