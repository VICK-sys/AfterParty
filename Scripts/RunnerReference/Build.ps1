param([Parameter(Mandatory = $true)][string]$Assets)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $projectRoot 'Builds/RunnerReference'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$env:PROCESSOR_ARCHITECTURE = 'AMD64'
$env:RUNNER_REFERENCE_ASSETS = (Resolve-Path -LiteralPath $Assets).Path
$env:RUNNER_REFERENCE_OUTPUT = $output
Push-Location $PSScriptRoot
try
{
    haxelib run lime build project.xml neko -64
    if ($LASTEXITCODE -ne 0) { throw 'Runner reference build failed.' }
    $process = Start-Process -FilePath (Join-Path $output 'compiled/neko/bin/RunnerReference.exe') -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw 'Runner reference capture failed.' }
}
finally { Pop-Location }
