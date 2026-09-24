param(
    [Parameter(Mandatory = $true)][string]$ExportPath,
    [string]$MSBuild = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe'
)

$ErrorActionPreference = 'Stop'
$export = (Resolve-Path -LiteralPath $ExportPath).Path
$project = Join-Path $export 'FridayNight.sln'
if (!(Test-Path -LiteralPath $project)) { throw "Missing Xbox project: $project" }
& $MSBuild $project "/p:SolutionDir=$export\" /m /t:Build /p:Configuration=Master /p:Platform=x64 /p:PlatformToolset=v143 /p:VCToolsVersion=14.44.35207 /p:AppxPackageSigningEnabled=false /p:AppxGeneratePackageRecipeEnabled=false /p:AppxPackageValidationEnabled=false /p:AppxUseResourceIndexerApi=false "/p:MrmSupportLibraryPath=C:/Program Files (x86)/Windows Kits/10/bin/10.0.26100.0/x64/mrmsupport.dll" /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly /verbosity:minimal /nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw "Xbox Master build failed: $LASTEXITCODE" }
$map = Join-Path $export 'build/obj/FridayNight/x64/Master/package.map.txt'
if (!(Test-Path -LiteralPath $map)) { throw "Missing Master package map: $map" }
$player = Get-Content -LiteralPath $map | Where-Object { $_ -match '"UnityPlayer.dll"$' }
if ($player -notmatch '\\x64\\[Mm]aster\\UnityPlayer.dll"') { throw 'Package map does not use the Master Unity player.' }
Write-Output "Xbox Master build and package map verified: $map"
