param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.6.1f1/Editor')
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$output = Join-Path $project 'Builds/ShutdownValidation'
$mono = Join-Path $UnityEditor 'Data/MonoBleedingEdge'
New-Item -ItemType Directory -Force $output | Out-Null
Copy-Item (Join-Path $project 'Assets/Discord RPC/Plugins/DiscordRPC.dll') $output
& "$mono/bin/mono.exe" "$mono/lib/mono/4.5/csc.exe" /nologo /define:UNITY_STANDALONE,UNITY_STANDALONE_WIN "/reference:$mono/lib/mono/4.5/Facades/netstandard.dll" "/reference:$project/Assets/Discord RPC/Plugins/DiscordRPC.dll" "/out:$output/DiscordPipeValidation.exe" "$PSScriptRoot/DiscordPipeValidation.cs" "$project/Assets/Discord RPC/Scripts/Control/UnityNamedPipe.cs"
if ($LASTEXITCODE -ne 0) { throw 'Pipe validation compilation failed' }
& "$output/DiscordPipeValidation.exe"
if ($LASTEXITCODE -ne 0) { throw 'Pipe validation failed' }
