[CmdletBinding()]
param([Parameter(Mandatory)][string]$NativeDirectory,[Parameter(Mandatory)][string]$Manifest,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
[Diagnostics.Process]::GetCurrentProcess().PriorityClass='BelowNormal'
$native=(Resolve-Path -LiteralPath $NativeDirectory).Path
$inputFile=(Resolve-Path -LiteralPath $Manifest).Path
if((Get-Item -LiteralPath $inputFile).Length -gt 1048576){throw 'Unbounded fitting manifest.'}
$plan=Get-Content -LiteralPath $inputFile -Raw | ConvertFrom-Json
if($plan.Schema -ne 'fighterport-fitting-corpus-1' -or $plan.Cases.Count -lt 1 -or $plan.Cases.Count -gt 128 -or $plan.ExitSamples -ne 10 -or $plan.EntryFitting){throw 'Unsupported bounded fitting corpus.'}
if(@($plan.Cases.Id | Select-Object -Unique).Count -ne $plan.Cases.Count -or @($plan.Cases | Where-Object {$_.Id -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,95}$'}).Count){throw 'Case identities must be unique safe names.'}
$manifestHash=(Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use fresh evidence; earlier outputs are retained.'}
New-Item -ItemType Directory -Path $output | Out-Null
$rows=@();$complete=$false
function Save-FittingReceipt {
    if((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash -ne $manifestHash){throw 'Frozen fitting manifest changed.'}
    [ordered]@{Schema='fighterport-fitting-corpus-results-1';Complete=$complete;ManifestSha256=$manifestHash;AppSha256=(Get-FileHash -LiteralPath (Join-Path $native 'FighterPortStudioV2.exe') -Algorithm SHA256).Hash;Cases=$rows} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
}
Save-FittingReceipt
for($index=0;$index -lt $plan.Cases.Count;$index++){
    while(Test-Path -LiteralPath (Join-Path $output 'pause')){if(Test-Path -LiteralPath (Join-Path $output 'cancel')){break};Start-Sleep -Milliseconds 200}
    if(Test-Path -LiteralPath (Join-Path $output 'cancel')){break}
    $case=$plan.Cases[$index]
    # Short evidence folders avoid duplicating display names in nested native
    # project asset paths. This affects storage only, never fitting parameters.
    $folder=Join-Path $output ('c'+($index+1).ToString('00'))
    $failure=$null
    try {
        & (Join-Path $PSScriptRoot 'Test-BackgroundCapture.ps1') -Model $case.TargetModel -Motion $case.Motion -RetargetSource $case.SourceModel -Clip $case.Clip -WorkspaceAuthenticLibraryFloat -RetargetTrajectoryCase -ReviewedRecoveryCase -TargetMotion $case.TargetMotion -ReferenceClip $case.Reference -FittingReferenceFixture $case.ReferenceFixture -AuthenticFixtureManifest $case.Fixture -AuthenticKeyMode SourceChannelTimes -NativeDirectory $native -OutputDirectory $folder *> (Join-Path $output ($case.Id+'.log'))
    } catch {$failure=$_.Exception.Message}
    $passed=$false;$checks=0
    $receipt=Join-Path $folder 'receipt.json'
    if(Test-Path -LiteralPath $receipt){$details=Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json;$passed=$details.Passed;$checks=$details.Checks.Count}
    $oracle=Join-Path $folder 'SourceChannelTimes/fit-native-oracle.json'
    $nativePassed=$false
    if(Test-Path -LiteralPath $oracle){$nativePassed=(Get-Content -LiteralPath $oracle -Raw | ConvertFrom-Json).Passed}
    $rows+=[pscustomobject]@{Id=$case.Id;Reference=$case.Reference;ReferenceFrame=1;Folder=$folder;Passed=($passed -and $nativePassed -and !$failure);NativeFitPassed=$nativePassed;Assertions=$checks;Failure=$failure}
    Save-FittingReceipt
    Write-Output ($case.Id+' -> '+$case.Reference+' frame 1: '+$rows[-1].Passed+' ('+$checks+' checks)')
}
$complete=$rows.Count -eq $plan.Cases.Count
Save-FittingReceipt
