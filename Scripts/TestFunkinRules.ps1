$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $projectRoot 'Temp/FunkinRuleTests'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../../Assets/Scripts/FunkinRules.cs" />
    <Compile Include="../../Assets/Scripts/FunkinHudState.cs" />
    <Compile Include="../../Assets/Scripts/PlayModes.cs" />
    <Compile Include="../../Scripts/FunkinRuleTests.cs" />
  </ItemGroup>
</Project>
'@
Set-Content -LiteralPath (Join-Path $testRoot 'FunkinRuleTests.csproj') -Value $project
dotnet run --project (Join-Path $testRoot 'FunkinRuleTests.csproj') --verbosity quiet -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Funkin rule validation failed.' }
