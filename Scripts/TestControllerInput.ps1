param([string]$UnityPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $UnityPath) {
    $version = ((Get-Content (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -First 1) -split ': ')[1]
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
$probeRoot = Join-Path $projectRoot 'Builds/ControllerInputProbe'
New-Item -ItemType Directory -Force -Path "$probeRoot/Assets/Editor", "$probeRoot/Packages", "$probeRoot/ProjectSettings" | Out-Null
foreach ($name in @('Player', 'VanillaControls', 'FunkinRules')) {
    Copy-Item (Join-Path $projectRoot "Assets/Scripts/$name.cs") "$probeRoot/Assets/$name.cs"
}
Copy-Item "$PSScriptRoot/ControllerInputProbe/Stubs.cs" "$probeRoot/Assets/ProbeStubs.cs"
Copy-Item "$PSScriptRoot/ControllerInputProbe/Validation.cs" "$probeRoot/Assets/Editor/ControllerInputProbe.cs"
Copy-Item "$projectRoot/ProjectSettings/ProjectVersion.txt" "$probeRoot/ProjectSettings/ProjectVersion.txt"
$dependencies = @{}
foreach ($name in @('com.unity.inputsystem', 'com.unity.nuget.newtonsoft-json')) {
    $package = Get-ChildItem "$projectRoot/Library/PackageCache" -Directory -Filter "$name@*" | Select-Object -First 1
    if (-not $package) { throw "Open the main project once to restore $name." }
    $dependencies[$name] = 'file:' + $package.FullName.Replace('\', '/')
}
@{dependencies=$dependencies} | ConvertTo-Json -Depth 4 | Set-Content "$probeRoot/Packages/manifest.json"
$run = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $probeRoot + '"'), '-executeMethod', 'ControllerInputProbe.Run', '-logFile', ('"' + $probeRoot + '/probe.log"')) -WindowStyle Hidden -PassThru
$run.WaitForExit()
if ($run.ExitCode -ne 0) { throw "Controller validation failed. See $probeRoot/probe.log." }
Get-Content "$probeRoot/result.txt"
