[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release',[string]$DotNet='dotnet',[switch]$NativeOnly,[string]$NativeDirectory)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'Restore-Dependencies.ps1')
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio 2022 MSBuild and the .NET Framework 4.7.2 targeting pack are required.' }
$builder = & $vswhere -latest -version '[17.0,18.0)' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $builder) { throw 'Visual Studio 2022 MSBuild was not found.' }
$native=if($NativeDirectory){[IO.Path]::GetFullPath($NativeDirectory)}else{Join-Path $projectRoot "BrawlCrate/bin/x86/$Configuration/net472"}
$buildArguments=@((Join-Path $projectRoot 'BrawlCrate/BrawlCrate.csproj'),'/restore','/t:Build',"/p:Configuration=$Configuration",'/p:Platform=x86','/nologo','/v:minimal')
if($NativeDirectory){$buildArguments+="/p:OutDir=$native/"}
& $builder @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
if(-not $NativeOnly){& (Join-Path $PSScriptRoot 'Build-Engine.ps1') -DotNet $DotNet -Configuration $Configuration -NativeDirectory $native}
Write-Output (Join-Path $native 'FighterPortStudioV2.exe')
