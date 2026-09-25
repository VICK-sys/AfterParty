$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $projectRoot 'Temp/NoteScheduleTests'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../Assets/Scripts/Song Data/NoteBehaviour.cs" />
    <Compile Include="../../Assets/Scripts/FunkinRules.cs" />
    <Compile Include="../../Scripts/NoteScheduleStubs.cs" />
    <Compile Include="../../Scripts/NoteScheduleTests.cs" />
  </ItemGroup>
</Project>
'@
Set-Content -LiteralPath (Join-Path $testRoot 'NoteScheduleTests.csproj') -Value $project
dotnet run --project (Join-Path $testRoot 'NoteScheduleTests.csproj') --configuration Release --verbosity quiet -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Note schedule validation failed.' }
