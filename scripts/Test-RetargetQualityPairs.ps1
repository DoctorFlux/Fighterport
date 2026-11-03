[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$NativeDirectory,
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$SameRigCaseId,
    [Parameter(Mandatory)][string]$ForwardCaseId,
    [Parameter(Mandatory)][string]$ReverseCaseId,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$native = (Resolve-Path -LiteralPath $NativeDirectory).Path
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
if ((Get-Item -LiteralPath $manifestPath).Length -gt 1048576) { throw 'Unbounded private corpus manifest.' }
$corpus = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($corpus.Schema -ne 'fighterport-native-transfer-corpus-1' -or $corpus.Cases.Count -lt 3 -or $corpus.Cases.Count -gt 128) {
    throw 'Invalid private native transfer corpus.'
}
$selected = @($SameRigCaseId, $ForwardCaseId, $ReverseCaseId)
if (@($selected | Where-Object { $_ -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,95}$' }).Count) {
    throw 'Choose safe case identities.'
}
$cases = @{}
foreach ($name in $selected) {
    $matches = @($corpus.Cases | Where-Object { $_.Id -eq $name })
    if ($matches.Count -ne 1) { throw "Expected exactly one exact fixture for $name." }
    $cases[$name] = $matches[0]
}
$forward = $cases[$ForwardCaseId]; $reverse = $cases[$ReverseCaseId]; $same = $cases[$SameRigCaseId]
if ($forward.SourceModel -ne $reverse.TargetModel -or $forward.TargetModel -ne $reverse.SourceModel -or
    $same.SourceModel -ne $forward.SourceModel -or $forward.SourceModel -eq $forward.TargetModel) {
    throw 'The selected fixtures do not form an exact same-rig and bidirectional-proportion pair.'
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new evidence directory; existing results are retained.' }
New-Item -ItemType Directory -Path $output | Out-Null
$runner = Join-Path $native 'RetargetAcceptance.exe'
if (-not (Test-Path -LiteralPath $runner)) { throw 'Build and copy the current RetargetAcceptance.exe beside the native application.' }
$rows = @()
$identity = Join-Path $output 'same-rig'
& $runner --same-rig-quality $same.SourceModel $same.Motion $same.Fixture $identity *> (Join-Path $output 'same-rig.log')
$identityExit = $LASTEXITCODE
$identityReceipt = Join-Path $identity 'same-rig-quality.json'
$rows += [pscustomobject]@{ Id = $SameRigCaseId; Direction = 'same-rig'; Exit = $identityExit; Receipt = $(if (Test-Path -LiteralPath $identityReceipt) { $identityReceipt } else { $null }) }
foreach ($direction in @(@('forward', $ForwardCaseId), @('reverse', $ReverseCaseId))) {
    $case = $cases[$direction[1]]
    $folder = Join-Path $output $direction[0]
    & $runner --retarget-case $case.SourceModel $case.TargetModel $case.Motion $case.Fixture $folder *> (Join-Path $output ($direction[0] + '-bake.log'))
    $bakeExit = $LASTEXITCODE
    $denseExit = $null; $defaultExit = $null
    $receiptPath = Join-Path $folder 'case.json'
    if (Test-Path -LiteralPath $receiptPath) {
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($receipt.MasterReady) {
            & $runner --verify-trajectory $case.SourceModel $case.TargetModel $case.Motion $case.Clip (Join-Path $folder 'dense-target.chr0') (Join-Path $folder 'dense-oracle') *> (Join-Path $output ($direction[0] + '-dense.log'))
            $denseExit = $LASTEXITCODE
        }
        if ($receipt.DefaultReady) {
            & $runner --verify-trajectory $case.SourceModel $case.TargetModel $case.Motion $case.Clip (Join-Path $folder 'current-target.chr0') (Join-Path $folder 'default-oracle') *> (Join-Path $output ($direction[0] + '-default.log'))
            $defaultExit = $LASTEXITCODE
        }
    }
    $rows += [pscustomobject]@{ Id = $direction[1]; Direction = $direction[0]; Exit = $bakeExit; DenseOracleExit = $denseExit; DefaultOracleExit = $defaultExit; Receipt = $(if (Test-Path -LiteralPath $receiptPath) { $receiptPath } else { $null }) }
}
$pass = $identityExit -eq 0 -and @($rows | Where-Object { $_.Direction -ne 'same-rig' -and ($_.Exit -ne 0 -or $_.DenseOracleExit -ne 0 -or $_.DefaultOracleExit -ne 0) }).Count -eq 0
[ordered]@{
    Schema = 'fighterport-retarget-quality-pair-results-1'
    Passed = $pass
    ManifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    AppSha256 = (Get-FileHash -LiteralPath (Join-Path $native 'FighterPortStudioV2.exe') -Algorithm SHA256).Hash
    HarnessSha256 = (Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash
    Cases = $rows
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $output 'results.json')
if (-not $pass) { exit 1 }
