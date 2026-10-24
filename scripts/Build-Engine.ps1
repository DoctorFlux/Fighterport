[CmdletBinding()]
param([string]$DotNet = 'dotnet',[ValidateSet('Debug','Release')][string]$Configuration='Release',[string]$NativeDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$native=if($NativeDirectory){[IO.Path]::GetFullPath($NativeDirectory)}else{Join-Path $root "BrawlCrate/bin/x86/$Configuration/net472"}
if(!(Test-Path -LiteralPath (Join-Path $native 'BrawlLib.dll'))){throw 'Build the native application first with scripts/Build.ps1.'}
$engine=Join-Path $native 'engine'
Push-Location (Join-Path $root 'engine')
try {
    & $DotNet publish src/FighterPort.EngineHost/FighterPort.EngineHost.csproj -c $Configuration -r win-x64 --self-contained true -o $engine -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false
    if($LASTEXITCODE){throw 'Engine build failed.'}
    & $DotNet publish src/FighterPort.Cli/FighterPort.Cli.csproj -c $Configuration -r win-x64 --self-contained false -o (Join-Path $engine 'cli') -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false
    if($LASTEXITCODE){throw 'CLI build failed.'}
    # Use the same audited self-contained host runtime for the CLI; avoid a
    # second runtime copy and a hidden system .NET 10 installation requirement.
    Copy-Item (Join-Path $root "engine/src/FighterPort.Cli/bin/$Configuration/net10.0/win-x64/FighterPort.Cli.dll") $engine -Force
    Copy-Item (Join-Path $root "engine/src/FighterPort.Cli/bin/$Configuration/net10.0/win-x64/FighterPort.Cli.deps.json") $engine -Force
    & $DotNet build src/FighterPort.BrawlWorker/FighterPort.BrawlWorker.csproj -c $Configuration "/p:BrawlLibraryDir=$native" -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false
    if($LASTEXITCODE){throw 'Native compatibility worker build failed.'}
    $worker=Join-Path $engine 'worker'
    New-Item -ItemType Directory -Force -Path $worker | Out-Null
    Copy-Item (Join-Path $root "engine/src/FighterPort.BrawlWorker/bin/$Configuration/net472/*") $worker -Force
    Copy-Item (Join-Path $root 'THIRD_PARTY_NOTICES.md') $engine -Force
    Copy-Item (Join-Path $root 'LICENSE') $engine -Force
    $packageCache=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'}
    $runtimePackage=Join-Path $packageCache 'microsoft.netcore.app.runtime.win-x64/10.0.13'
    if(Test-Path -LiteralPath $runtimePackage){foreach($notice in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')){if(Test-Path (Join-Path $runtimePackage $notice)){Copy-Item (Join-Path $runtimePackage $notice) (Join-Path $engine ('DOTNET-'+$notice)) -Force}}}
} finally {Pop-Location}
Write-Output $engine
