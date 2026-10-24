[CmdletBinding()]
param(
    [string]$NativeDirectory,
    [switch]$Check,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ViewerArguments = @()
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$launchSettings = Join-Path $projectRoot '.local/development-launch.json'

if (-not $PSBoundParameters.ContainsKey('NativeDirectory')) {
    if (Test-Path -LiteralPath $launchSettings -PathType Leaf) {
        $settings = Get-Content -LiteralPath $launchSettings -Raw | ConvertFrom-Json
        $NativeDirectory = $settings.NativeDirectory
    } else {
        $NativeDirectory = 'BrawlCrate/bin/x86/Release/net472'
    }
}
if ([string]::IsNullOrWhiteSpace($NativeDirectory)) {
    throw 'Specify a native build directory with -NativeDirectory or in .local/development-launch.json.'
}
if (-not [IO.Path]::IsPathRooted($NativeDirectory)) {
    $NativeDirectory = Join-Path $projectRoot $NativeDirectory
}
$nativeRoot = [IO.Path]::GetFullPath($NativeDirectory)
$viewerExecutable = Join-Path $nativeRoot 'FighterPortStudioV2.exe'
if (-not (Test-Path -LiteralPath $viewerExecutable -PathType Leaf)) {
    throw "Native application not found at '$viewerExecutable'. Run scripts/Build.ps1 -NativeDirectory '$nativeRoot', or select a complete native build with -NativeDirectory."
}
if ($Check) {
    Write-Output $viewerExecutable
    return
}

Push-Location -LiteralPath $nativeRoot
try {
    & $viewerExecutable @ViewerArguments
} finally {
    Pop-Location
}
