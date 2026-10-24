[CmdletBinding()]
param([string]$OutputDirectory,[string]$BuildDirectory,[switch]$AllowDirty,[string]$DotNet='dotnet')

$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$projectInfo=Get-Content -LiteralPath (Join-Path $root 'BrawlCrate/BrawlCrate.csproj')
$versionNode=$projectInfo.SelectSingleNode('/Project/PropertyGroup/InformationalVersion')
$fileVersionNode=$projectInfo.SelectSingleNode('/Project/PropertyGroup/FileVersion')
if(-not $versionNode -or -not $fileVersionNode){throw 'The project must declare its preview and file versions.'}
$version=$versionNode.InnerText
$fileVersion=$fileVersionNode.InnerText
$name="FighterPortStudioV2-$version-win-x64"
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts\releases'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
$package=Join-Path $output $name
$archive="$package.zip"
if((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $archive)){throw 'Package destination already exists; choose a new output directory.'}
$revision=(& git -C $root rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'Cannot identify source revision.'}
$dirty=@(& git -C $root status --porcelain).Count -ne 0
if($dirty -and -not $AllowDirty){throw 'Commit source changes before packaging, or explicitly use -AllowDirty for a development package.'}
if(-not $BuildDirectory){
    $BuildDirectory=Join-Path $root ('artifacts\release-build-'+[Guid]::NewGuid().ToString('N'))
    & (Join-Path $PSScriptRoot 'Restore-Dependencies.ps1')
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $builder=& $vswhere -latest -version '[17.0,18.0)' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if(-not $builder){throw 'Visual Studio 2022 MSBuild is required.'}
    & $builder (Join-Path $root 'BrawlCrate\BrawlCrate.csproj') /restore /t:Build /p:Configuration=Release /p:Platform=x86 "/p:OutDir=$BuildDirectory/" /nologo /v:minimal
    if($LASTEXITCODE -ne 0){throw 'Release build failed.'}
    & (Join-Path $PSScriptRoot 'Build-Engine.ps1') -DotNet $DotNet -NativeDirectory $BuildDirectory
}
$build=(Resolve-Path -LiteralPath $BuildDirectory).Path
foreach($file in @('engine/FighterPort.EngineHost.exe','engine/FighterPort.Core.dll','engine/FighterPort.Cli.dll','engine/worker/FighterPort.BrawlWorker.exe')){if(!(Test-Path -LiteralPath (Join-Path $build $file))){throw "Incomplete native integration output: $file"}}
$required=@('FighterPortStudioV2.exe','BrawlLib.dll','OpenTK.dll','IronPython.dll','IronPython.Modules.dll',
    'IronPython.SQLite.dll','IronPython.Wpf.dll','Microsoft.Dynamic.dll','Microsoft.Scripting.dll',
    'Microsoft.Scripting.Metadata.dll','System.Buffers.dll','System.IO.Hashing.dll','System.Memory.dll',
    'System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll','FighterPortStudioV2.exe.config',
    'BrawlLib.dll.config','OpenTK.dll.config','Changelog.txt')
foreach($file in $required){if(-not (Test-Path -LiteralPath (Join-Path $build $file) -PathType Leaf)){throw "Missing runtime file: $file"}}
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $build 'FighterPortStudioV2.exe'))
if($info.FileVersion -ne $fileVersion -or $info.ProductVersion -ne $version){throw 'Build is not the expected preview version.'}
[IO.Directory]::CreateDirectory($package) | Out-Null
foreach($file in $required){Copy-Item -LiteralPath (Join-Path $build $file) -Destination (Join-Path $package $file)}
foreach($directory in @('licenses','lib','CustomLists','engine')){
    $source=Join-Path $build $directory
    if(-not (Test-Path -LiteralPath $source -PathType Container)){throw "Missing runtime directory: $directory"}
    if(@(Get-ChildItem -LiteralPath $source -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Runtime trees may not contain links.'}
    foreach($file in Get-ChildItem -LiteralPath $source -File -Recurse){
        # Embedded Python does not use distutils' standalone installer launchers.
        if($file.Extension -in @('.exe','.pyc','.pyo')){continue}
        $destination=Join-Path (Join-Path $package $directory) $file.FullName.Substring($source.Length+1)
        [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
foreach($file in @('LICENSE','UPSTREAM.md','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination $package}
Copy-Item -LiteralPath (Join-Path $root 'docs\RELEASE_PREVIEW.md') -Destination (Join-Path $package 'README.md')
$sourceUrl="https://github.com/DoctorFlux/Fighterport/tree/$revision"
[IO.File]::AppendAllText((Join-Path $package 'README.md'),"`r`nSource revision: $revision`r`nSource: $sourceUrl`r`n")
# Include the exact committed source separately; never archive the workspace,
# ignored user projects, models, downloaded dependencies or local credentials.
& git -C $root archive --format=zip "--output=$(Join-Path $package 'FighterPortStudioV2-source.zip')" $revision
if($LASTEXITCODE -ne 0){throw 'Source archive failed.'}
if($dirty){[IO.File]::AppendAllText((Join-Path $package 'README.md'),"`r`nDEVELOPMENT PACKAGE: working source differs from the included committed source. This is not a publishable release.`r`n")}
$forbidden=@('.pac','.pcs','.brres','.chr0','.mdl0','.fpstudio','.pdb','.snk','.pfx','.p12')
foreach($file in Get-ChildItem -LiteralPath $package -File -Recurse -Force){
    if($file.Extension.ToLowerInvariant() -in $forbidden){throw "Unexpected private/game/debug file: $($file.Name)"}
    if($file.Extension -eq '.exe' -and $file.Name -ne 'FighterPortStudioV2.exe'){throw 'Unexpected executable in runtime package.'}
}
$manifest=[ordered]@{Product='FighterPort Studio V2';Version=$version;Configuration='Release';Platform='win-x86';
    Framework='.NET Framework 4.7.2';SourceRevision=$revision;SourceUrl=$sourceUrl;DirtySource=$dirty;
    PackagedUtc=[DateTime]::UtcNow.ToString('o');ApplicationSha256=(Get-FileHash -LiteralPath (Join-Path $package 'FighterPortStudioV2.exe')).Hash;
    LibrarySha256=(Get-FileHash -LiteralPath (Join-Path $package 'BrawlLib.dll')).Hash}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'release-manifest.json') -Encoding UTF8
$sums=@(Get-ChildItem -LiteralPath $package -File -Recurse | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()+'  '+$_.FullName.Substring($package.Length+1).Replace('\','/')
})
$sums | Set-Content -LiteralPath (Join-Path $package 'SHA256SUMS.txt') -Encoding ASCII
Compress-Archive -LiteralPath $package -DestinationPath $archive -CompressionLevel Optimal
((Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($archive)) | Set-Content -LiteralPath "$archive.sha256" -Encoding ASCII
Write-Output (Join-Path $package 'FighterPortStudioV2.exe')
Write-Output $archive
