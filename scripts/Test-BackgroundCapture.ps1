[CmdletBinding()]
param([Parameter(Mandatory)][string]$Model,[Parameter(Mandatory)][string]$Motion,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$SuppliedModel,[string]$RetargetSource,[string]$TargetMotion,[string]$Clip='Wait1',[switch]$ModelPose,[string]$NativeDirectory,[switch]$ControlGizmos,[switch]$ControlReview,[switch]$LibraryBatch,[switch]$SceneFitting,[switch]$TransitionFit,[switch]$ContactAuthoring,[switch]$CombinedStress,[string]$ContactTransitionsProject,[switch]$CharacterWorkspace,[switch]$SimpleWorkspace,[switch]$WorkspaceFlow,[switch]$WorkspaceWalkthrough,[switch]$WorkspaceRecent,[switch]$WorkspaceExport,[switch]$WorkspaceComparison,[switch]$WorkspaceLibrary,[switch]$WorkspaceBatchReview,[switch]$WorkspaceSetExport,[switch]$WorkspaceLoopInspection,[switch]$WorkspaceExitRecovery,[switch]$WorkspaceFootRecovery,[switch]$WorkspaceBoundaryFit,[switch]$ExpectFloorRejection,[switch]$MovingRootFixture,[switch]$ReferenceControls,[string]$ReferenceClip='Wait1',[int]$ExitSamples=8,[switch]$ProjectLifecycle,[switch]$WorkspaceKeyboard,[switch]$WorkspaceScaling,[switch]$WorkspaceDialogs,[string]$CompatibilityProject,[string]$LegacyProject)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$modelPath=(Resolve-Path -LiteralPath $Model).Path
$motionPath=(Resolve-Path -LiteralPath $Motion).Path
if(($SimpleWorkspace -or $WorkspaceFlow -or $WorkspaceWalkthrough -or $WorkspaceBoundaryFit) -and !$RetargetSource){throw 'Workspace acceptance requires RetargetSource pointing to the authentic donor model.'}
if(($WorkspaceWalkthrough -or $WorkspaceExitRecovery -or $WorkspaceFootRecovery -or $WorkspaceBoundaryFit) -and !$TargetMotion){throw 'Workflow/recovery replay requires TargetMotion for the authentic target reference clips.'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Choose a new output directory; prior evidence is preserved.'}
$harness=Join-Path $root 'tests/ControlsAcceptance/bin/x86/Release/net472/ControlsAcceptance.exe'
if($NativeDirectory){$native=[IO.Path]::GetFullPath($NativeDirectory);$harness=Join-Path $native 'ControlsAcceptance.exe'}else{$native=Join-Path $root 'BrawlCrate/bin/x86/Release/net472'}
if(!(Test-Path -LiteralPath $harness)){throw 'Build tests/ControlsAcceptance/ControlsAcceptance.csproj with Release/x86 first.'}
$mutex=New-Object Threading.Mutex($false,'Local\FighterPortBackgroundCapture')
if(!$mutex.WaitOne(0)){throw 'Another FighterPort background capture owns the serialized viewport.'}
try {
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $env:FIGHTERPORT_ENGINE_HOST=Join-Path $native 'engine/FighterPort.EngineHost.exe'
    $env:FIGHTERPORT_CAPTURE_CONTROL=$output
    # The harness creates its child directly on a new private desktop. It never
    # switches desktops, attaches user input, or reads screen pixels.
    $captureArgs=@($modelPath,$motionPath,$output,'--private-desktop')
    if($SuppliedModel){$captureArgs+='--capture-model'}
    if($ModelPose){$captureArgs+='--model-pose'}
    if($ControlGizmos){$captureArgs+='--control-gizmos'}
    if($TransitionFit){$captureArgs+='--transition-fit'}
    if($ContactAuthoring){$captureArgs+='--contact-authoring'}
    if($CombinedStress){$captureArgs+='--combined-stress'}
    if($ContactTransitionsProject){$captureArgs+=@('--contact-transitions-project',(Resolve-Path -LiteralPath $ContactTransitionsProject).Path)}
    if($SceneFitting){$captureArgs+='--scene-fitting'};if($LibraryBatch){$captureArgs+='--library-batch'};if($ControlReview){$captureArgs+='--control-review'}
    if($CharacterWorkspace){$captureArgs+='--character-workspace'}
    if($SimpleWorkspace){$captureArgs+='--simple-workspace'}
    if($WorkspaceWalkthrough){$captureArgs+=@('--workspace-walkthrough','--target-motion',(Resolve-Path -LiteralPath $TargetMotion).Path)}
    if($WorkspaceBatchReview){$captureArgs+='--workspace-batch-review'}
    if($WorkspaceSetExport){$captureArgs+='--workspace-set-export'}
    if($WorkspaceLoopInspection){$captureArgs+='--workspace-loop-inspection'}
    if($WorkspaceExitRecovery -or $WorkspaceFootRecovery -or $WorkspaceBoundaryFit){$captureArgs+=@('--workspace-exit-recovery','--target-motion',(Resolve-Path -LiteralPath $TargetMotion).Path,'--reference-clip',$ReferenceClip,'--exit-samples',[string]$ExitSamples)}
    if($WorkspaceBoundaryFit){$captureArgs+='--workspace-boundary-fit'}
    if($ExpectFloorRejection){if(!$WorkspaceBoundaryFit -or !$WorkspaceFootRecovery){throw 'Expected floor rejection needs the two-ended floor case.'};$captureArgs+='--boundary-floor-rejection'}
    if($WorkspaceFootRecovery){$captureArgs+='--workspace-foot-recovery'}
    if($ReferenceControls){$captureArgs+='--reference-controls'}
    if($MovingRootFixture){$captureArgs+='--moving-root-fixture'}
    if($WorkspaceLibrary){$captureArgs+='--workspace-library'}
    if($WorkspaceComparison){$captureArgs+='--workspace-comparison'}
    if($WorkspaceExport){$captureArgs+='--workspace-export'}
    if($WorkspaceRecent){$captureArgs+='--workspace-recent'}
    if($WorkspaceFlow){$captureArgs+='--workspace-flow'}
    if($ProjectLifecycle){$captureArgs+='--project-lifecycle'}
    if($WorkspaceKeyboard){$captureArgs+='--workspace-keyboard'}
    if($WorkspaceScaling){$captureArgs+='--workspace-scaling'}
    if($WorkspaceDialogs){$captureArgs+='--workspace-dialogs'}
    if($CompatibilityProject){if(!$LegacyProject){throw 'Supply the earlier standalone LegacyProject as well.'};$captureArgs+=@('--saved-project',(Resolve-Path -LiteralPath $CompatibilityProject).Path,'--legacy-project',(Resolve-Path -LiteralPath $LegacyProject).Path)}
    if($RetargetSource){$captureArgs+=@('--retarget-source',(Resolve-Path -LiteralPath $RetargetSource).Path,'--clip',$Clip)}
    & $harness @captureArgs
    if($LASTEXITCODE){throw "Background acceptance failed ($LASTEXITCODE). See $output/background.log."}
    Get-FileHash -Algorithm SHA256 -LiteralPath $modelPath,$motionPath | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'source-hashes.json')
} finally {$mutex.ReleaseMutex();$mutex.Dispose();Remove-Item Env:FIGHTERPORT_CAPTURE_CONTROL -ErrorAction SilentlyContinue}
# Create <output>/pause to pause before each capture; remove it to resume.
# Create <output>/cancel to cancel. Captures remain local for actual inspection.
