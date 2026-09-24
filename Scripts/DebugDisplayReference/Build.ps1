param([Parameter(Mandatory = $true)][string]$Source, [switch]$ImportFont)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$referenceSource = Join-Path $projectRoot 'Temp/DebugDisplayReferenceSource/funkin'
New-Item -ItemType Directory -Path "$referenceSource/ui/debug/stats", "$referenceSource/util" -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $Source 'source/funkin/ui/debug/FunkinDebugDisplay.hx') -Destination "$referenceSource/ui/debug/FunkinDebugDisplay.hx"
Copy-Item -LiteralPath (Join-Path $Source 'source/funkin/ui/debug/stats/FunkinStatsGraph.hx') -Destination "$referenceSource/ui/debug/stats/FunkinStatsGraph.hx"
Set-Content "$referenceSource/Constants.hx" 'package funkin; class Constants { public static inline var MS_PER_SEC = 1000; }'
Set-Content "$referenceSource/util/MemoryUtil.hx" 'package funkin.util; class MemoryUtil { public static function supportsGCMem() return true; public static function supportsTaskMem() return true; public static function getGCMemory() return 134217728.0; public static function getTaskMemory() return 805306368.0; }'
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:UNITY_PARTY_DEBUG_REFERENCE_PATH = Join-Path $projectRoot 'Builds/DebugDisplayReference'
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Debug display reference build failed.' }
    foreach ($mode in @('advanced', 'simple'))
    {
        $env:UNITY_PARTY_DEBUG_REFERENCE_MODE = $mode
        & (Join-Path $projectRoot 'Builds/DebugDisplayReference/compiled/neko/bin/DebugDisplayReference.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Debug display reference capture failed.' }
    }
    if ($ImportFont)
    {
        $fontDirectory = Join-Path $projectRoot 'Assets/Resources/VanillaDebug'
        New-Item -ItemType Directory -Path $fontDirectory -Force | Out-Null
        Copy-Item "$env:UNITY_PARTY_DEBUG_REFERENCE_PATH/font.png", "$env:UNITY_PARTY_DEBUG_REFERENCE_PATH/font.json" $fontDirectory
    }
}
finally { Pop-Location }
