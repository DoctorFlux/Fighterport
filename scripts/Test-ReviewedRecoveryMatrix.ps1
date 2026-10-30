param(
    [Parameter(Mandatory)][string]$MarioModel,[Parameter(Mandatory)][string]$MarioMotion,
    [Parameter(Mandatory)][string]$SnakeModel,[Parameter(Mandatory)][string]$SnakeMotion,
    [Parameter(Mandatory)][string]$Inventory,[Parameter(Mandatory)][string]$NativeDirectory,
    [Parameter(Mandatory)][string]$ExpectedApplicationSha256,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$GroundContacts,[string[]]$OnlyCases=@()
)
$ErrorActionPreference='Stop'
[Diagnostics.Process]::GetCurrentProcess().PriorityClass='BelowNormal'
$taskOutput=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $taskOutput){throw 'Choose a new matrix output directory; prior evidence is preserved.'}
$taskInventoryFile=(Resolve-Path -LiteralPath $Inventory).Path
if((Get-Item -LiteralPath $taskInventoryFile).Length -gt 1MB){throw 'Inventory exceeds bounded fixture input.'}
$taskInventory=Get-Content -LiteralPath $taskInventoryFile -Raw | ConvertFrom-Json
$taskNative=(Resolve-Path -LiteralPath $NativeDirectory).Path
$taskApplication=Join-Path $taskNative 'FighterPortStudioV2.exe'
if((Get-FileHash -LiteralPath $taskApplication -Algorithm SHA256).Hash -ne $ExpectedApplicationSha256){throw 'Application is not the externally pinned baseline.'}
$taskFixtures=@{Mario=@{Model=(Resolve-Path -LiteralPath $MarioModel).Path;Motion=(Resolve-Path -LiteralPath $MarioMotion).Path};Snake=@{Model=(Resolve-Path -LiteralPath $SnakeModel).Path;Motion=(Resolve-Path -LiteralPath $SnakeMotion).Path}}
foreach($taskCharacter in $taskFixtures.Keys){
    $taskFixture=$taskFixtures[$taskCharacter]
    $taskFixture.ModelHash=(Get-FileHash -LiteralPath $taskFixture.Model -Algorithm SHA256).Hash
    $taskFixture.MotionHash=(Get-FileHash -LiteralPath $taskFixture.Motion -Algorithm SHA256).Hash
    $taskCatalogue=@($taskInventory.Cases | Where-Object {$_.ModelContainerSha256 -eq $taskFixture.ModelHash -and $_.MotionContainerSha256 -eq $taskFixture.MotionHash})
    if($taskCatalogue.Count -ne 1){throw 'Exact fixture containers are absent or ambiguous in the sealed inventory.'}
    $taskFixture.Catalogue=$taskCatalogue[0]
    # Explicit fixture member, already enumerated before testing. This is not
    # an algorithmic character/bone/index exception or shadow substitution.
    $taskBody=@($taskCatalogue[0].Models | Where-Object {$_.Entry -eq 'root/0/0/0'})
    if($taskBody.Count -ne 1){throw 'The reviewed fixture model member is unavailable.'}
    $taskFixture.Rig=$taskBody[0].Rig
}
[IO.Directory]::CreateDirectory($taskOutput) | Out-Null
$taskRows=[Collections.Generic.List[object]]::new()
foreach($taskPair in @(@{Source='Snake';Target='Mario'},@{Source='Mario';Target='Snake'})){
    $taskMoves=if($GroundContacts){@('AttackS3S','DownStandU','DownStandD','DownAttackU','DownAttackD')}else{@('AttackS3S','DownStandU','DownStandD','DownAttackU','DownAttackD','CliffClimbQuick','CliffClimbSlow','CliffAttackQuick','CliffAttackSlow')}
    foreach($taskName in $taskMoves){
        $taskSource=$taskFixtures[$taskPair.Source];$taskTarget=$taskFixtures[$taskPair.Target]
        $taskId=$taskPair.Source+'To'+$taskPair.Target+'-'+$taskName
        if($OnlyCases.Count -gt 0 -and $taskId -notin $OnlyCases){continue}
        $taskCase=Join-Path $taskOutput $taskId
        $taskMembers=@($taskSource.Catalogue.Clips | Where-Object {$_.Name -eq $taskName})
        $taskReference=@($taskTarget.Catalogue.Clips | Where-Object {$_.Name -eq 'Wait1'})
        $taskError=$null;$taskPass=$false;$taskChecks=0;$taskSeconds=0
        if($taskMembers.Count -ne 1 -or $taskReference.Count -ne 1){$taskError='Exact requested native member is missing or ambiguous. No substitute.'}
        elseif($taskMembers[0].SnapshotFailure -or !$taskMembers[0].SnapshotFile){$taskError='Original requested member exists, but safe standalone snapshot is unavailable: '+$taskMembers[0].SnapshotFailure}
        else{
            $taskMember=$taskMembers[0]
            $taskSnapshot=Join-Path ([IO.Path]::GetDirectoryName($taskInventoryFile)) $taskMember.SnapshotFile
            if((Get-FileHash -LiteralPath $taskSnapshot -Algorithm SHA256).Hash -ne $taskMember.SnapshotSha256){throw 'The sealed original native snapshot changed.'}
            $taskManifest=Join-Path $taskOutput ($taskId+'-fixture.json')
            @{Schema='fighterport-authentic-export-fixture-1';SourceClipSha256=$taskMember.SnapshotSha256;SourceClipName=$taskName;SourceFrames=$taskMember.FrameCount;SourceVersion=$taskMember.Version;SourceLoop=$taskMember.Loop;SourceModelSha256=$taskSource.ModelHash;TargetModelSha256=$taskTarget.ModelHash;SourceRigFingerprint=$taskSource.Rig;TargetRigFingerprint=$taskTarget.Rig} | ConvertTo-Json | Set-Content -LiteralPath $taskManifest -Encoding UTF8
            $taskClock=[Diagnostics.Stopwatch]::StartNew()
            try{
                & (Join-Path $PSScriptRoot 'Test-BackgroundCapture.ps1') -Model $taskTarget.Model -Motion $taskSnapshot -RetargetSource $taskSource.Model -TargetMotion $taskTarget.Motion -Clip $taskName -AuthenticFixtureManifest $taskManifest -AuthenticKeyMode SourceChannelTimes -WorkspaceAuthenticLibraryFloat -ReviewedRecoveryCase -ReviewedGroundAfterFit:$GroundContacts -NativeDirectory $taskNative -OutputDirectory $taskCase
                $taskReceipt=Get-Content -LiteralPath (Join-Path $taskCase 'receipt.json') -Raw | ConvertFrom-Json
                $taskPass=$true;$taskChecks=@($taskReceipt.Checks).Count
            }catch{$taskError=$_.Exception.Message}
            $taskSeconds=$taskClock.Elapsed.TotalSeconds
        }
        $taskRows.Add([ordered]@{Case=$taskId;Passed=$taskPass;Checks=$taskChecks;Seconds=$taskSeconds;Error=$taskError;SourceMember=@($taskMembers | Select-Object Entry,MemberIdentitySha256,SnapshotSha256);Reference=@($taskReference | Select-Object Entry,MemberIdentitySha256);End=($taskPair.Target+' Wait1 frame 1');OriginalBeginning=$true;AppendedIdle=$false;GroundPhase=[bool]$GroundContacts;ReviewedFloor=(Test-Path -LiteralPath (Join-Path $taskCase 'SourceChannelTimes/frozen-ground.json'));ReviewedLedge=$false;ContactAcceptance=[bool]($GroundContacts -and $taskPass)})
        foreach($taskFixture in $taskFixtures.Values){if((Get-FileHash -LiteralPath $taskFixture.Model -Algorithm SHA256).Hash -ne $taskFixture.ModelHash -or (Get-FileHash -LiteralPath $taskFixture.Motion -Algorithm SHA256).Hash -ne $taskFixture.MotionHash){throw 'An original native fixture changed.'}}
        if((Get-FileHash -LiteralPath $taskApplication -Algorithm SHA256).Hash -ne $ExpectedApplicationSha256){throw 'Frozen baseline app changed.'}
        [ordered]@{Schema='reviewed-recovery-matrix-1';GroundPhase=[bool]$GroundContacts;ApplicationSha256=$ExpectedApplicationSha256;InventorySha256=(Get-FileHash -LiteralPath $taskInventoryFile -Algorithm SHA256).Hash;Cases=@($taskRows);Note='Actual source-pane Use and end-only fitting. Ground phase explicitly reviews static geometry after the unchanged no-support-context retarget baseline. Failures are preserved. No ledge, runtime or physical-input acceptance.'} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $taskOutput 'matrix.json') -Encoding UTF8
        Write-Output ('RECOVERY BASELINE '+$taskId+': passed='+$taskPass+'; '+$taskError)
    }
}
if($OnlyCases.Count -gt 0 -and @($OnlyCases | Where-Object {$_ -notin $taskRows.Case}).Count -gt 0){throw 'A requested test case does not exist in this matrix; no substitute was run.'}
