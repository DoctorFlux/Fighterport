[CmdletBinding()]
param(
    [string]$UpstreamCheckout,
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dependencyPath = Join-Path $projectRoot 'BrawlLib\OpenTK.dll'
$upstreamCommit = 'd56cbac89dc2cd0e5c3f923e1d03041b16902add'
$expectedSha256 = '931E38B47B824C316A55B5A54D55650C978CA90DDE1DAEA02299EDDD94E719C0'
$downloadUrl = "https://raw.githubusercontent.com/soopercool101/BrawlCrate/$upstreamCommit/BrawlLib/OpenTK.dll"

function Test-PinnedDependency([string]$Path) {
    return (Test-Path -LiteralPath $Path -PathType Leaf) -and
        ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $expectedSha256)
}

if (Test-PinnedDependency $dependencyPath) {
    Write-Output "Verified existing OpenTK.dll: $expectedSha256"
    return
}

if ($Offline -and [string]::IsNullOrWhiteSpace($UpstreamCheckout)) {
    throw 'Offline restore needs -UpstreamCheckout pointing to a pristine BrawlCrate checkout.'
}

$stagingDirectory = Join-Path $projectRoot '.local\dependency-restore'
[IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null
$stagingPath = Join-Path $stagingDirectory ([Guid]::NewGuid().ToString('N') + '.download')
try {
    if (-not [string]::IsNullOrWhiteSpace($UpstreamCheckout)) {
        $sourcePath = Join-Path ([IO.Path]::GetFullPath($UpstreamCheckout)) 'BrawlLib\OpenTK.dll'
        if (-not (Test-PinnedDependency $sourcePath)) {
            throw 'The local upstream OpenTK.dll is absent or does not match the pinned SHA-256.'
        }
        Copy-Item -LiteralPath $sourcePath -Destination $stagingPath
    } else {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $stagingPath -UseBasicParsing
    }

    if (-not (Test-PinnedDependency $stagingPath)) {
        throw 'OpenTK.dll verification failed; the existing dependency was left unchanged.'
    }

    [IO.Directory]::CreateDirectory((Split-Path -Parent $dependencyPath)) | Out-Null
    Move-Item -LiteralPath $stagingPath -Destination $dependencyPath -Force
    Write-Output "Restored app-local OpenTK.dll: $expectedSha256"
} finally {
    if (Test-Path -LiteralPath $stagingPath -PathType Leaf) {
        Remove-Item -LiteralPath $stagingPath -Force
    }
}
