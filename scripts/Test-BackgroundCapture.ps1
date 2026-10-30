[CmdletBinding()]
param([Parameter(Mandatory)][string]$Model,[Parameter(Mandatory)][string]$Motion,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$SuppliedModel,[string]$RetargetSource,[string]$TargetMotion,[string]$Clip='Wait1',[switch]$ModelPose,[string]$NativeDirectory,[switch]$ControlGizmos,[switch]$ControlReview,[switch]$LibraryBatch,[switch]$SceneFitting,[switch]$TransitionFit,[switch]$ContactAuthoring,[switch]$CombinedStress,[string]$ContactTransitionsProject,[switch]$CharacterWorkspace,[switch]$SimpleWorkspace,[switch]$WorkspaceFlow,[switch]$WorkspaceWalkthrough,[switch]$WorkspaceRecent,[switch]$WorkspaceExport,[switch]$WorkspaceExportOptimization,[switch]$WorkspaceExportCurrent,[switch]$WorkspaceExportNonselected,[switch]$WorkspaceExportDirtyModel,[switch]$WorkspaceOptimizationSharedContainer,[switch]$NativeExportStructure,[switch]$NativeChr0Forensics,[switch]$NativeChr0WritePath,[switch]$RequireRetainedStorage,[switch]$NativeExactI12WritePath,[switch]$NativeDenseFloat,[switch]$NativeMotionPacVerification,[string]$QualifiedPacAnimation,[string]$QualifiedPacAnimationSha256,[string]$QualifiedPacMaster,[string]$QualifiedPacMasterSha256,[string]$QualifiedPacSlot,[switch]$WorkspaceComparison,[switch]$WorkspaceLibrary,[switch]$WorkspaceBatchReview,[switch]$WorkspaceSetExport,[switch]$WorkspaceLoopInspection,[switch]$WorkspaceExitRecovery,[switch]$WorkspaceFootRecovery,[switch]$WorkspaceBoundaryFit,[switch]$ExpectFloorRejection,[switch]$MovingRootFixture,[switch]$ReferenceControls,[string]$ReferenceClip='Wait1',[int]$ExitSamples=8,[switch]$ProjectLifecycle,[switch]$WorkspaceKeyboard,[switch]$WorkspaceScaling,[switch]$WorkspaceDialogs,[switch]$WorkspacePerformance,[switch]$WorkspaceNextLayout,[switch]$WorkspaceLibraryTarget,[switch]$WorkspaceGameLibrary,[switch]$WorkspaceLibraryFlow,[switch]$WorkspaceLibraryOrder,[switch]$WorkspaceFitReferences,[string]$OptimizationProject,[switch]$OptimizationRawNext,[switch]$OptimizationHighQuality,[switch]$OptimizationCompactStorage,[switch]$OptimizationSmallFile,[switch]$WorkspaceAnimationOptimization,[ValidateSet("Exact","Bounded","Apply")][string]$WorkspaceOptimizationSegment,[switch]$WorkspaceOptimizationLayout,[switch]$NativeAnimationOptimization,[switch]$WorkspaceSonicPipeline,[switch]$WorkspaceAuthenticFloatDense,[switch]$WorkspaceAuthenticLibraryFloat,[switch]$AuthenticL4ApplyCurrent,[switch]$RetargetTrajectoryCase,[string]$AuthenticFixtureManifest,[ValidateSet("SourceBoneTimes","SourceChannelTimes")][string]$AuthenticKeyMode,[switch]$AuthenticFloatExportCopy,[switch]$WorkspaceSonicFourStages,[switch]$SonicPipelineReciprocal,[switch]$SonicPipelineThorough,[switch]$SpanRefitRequired,[ValidateSet("SourceBoneTimes","SourceChannelTimes")][string]$SonicPipelineMode,[switch]$SonicPipelineFitOnly,[switch]$SonicPipelineFitExportSegment,[string]$ProvidedMario,[switch]$WorkspaceUnifiedOptimization,[switch]$WorkspaceCompactOptimization,[switch]$NativeSourceKeys,[switch]$WorkspaceSourceKeys,[switch]$WorkspaceUseModelKeys,[switch]$WorkspaceNativeAnimationExport,[switch]$WorkspaceAerialRecovery,[switch]$WorkspaceDirectSources,[switch]$WorkspaceGameCapacity,[switch]$WorkspaceGameSources,[switch]$WorkspaceSourceFit,[string]$LibraryIndex,[string]$SourceCharacter='snake',[string]$DiscImage,[string]$CompatibilityProject,[string]$LegacyProject,[string]$LegacyFitProject,[switch]$ReviewedContactInventory,[switch]$ReviewedContacts,[switch]$ReviewedRecoveryCase,[switch]$ReviewedGroundAfterFit,[string]$ReviewedContactReopenProject)
$ErrorActionPreference='Stop'
if($WorkspaceOptimizationSegment -and !$WorkspaceAnimationOptimization){throw 'WorkspaceOptimizationSegment requires WorkspaceAnimationOptimization.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$modelPath=(Resolve-Path -LiteralPath $Model).Path
$motionPath=(Resolve-Path -LiteralPath $Motion).Path
if(($SimpleWorkspace -or $WorkspaceFlow -or $WorkspaceWalkthrough -or $WorkspaceBoundaryFit -or $WorkspacePerformance -or $WorkspaceNextLayout -or $WorkspaceUseModelKeys) -and !$RetargetSource){throw 'Workspace acceptance requires RetargetSource pointing to the authentic donor model.'}
if(($WorkspaceWalkthrough -or $WorkspaceExitRecovery -or $WorkspaceFootRecovery -or $WorkspaceBoundaryFit) -and !$TargetMotion){throw 'Workflow/recovery replay requires TargetMotion for the authentic target reference clips.'}
if($WorkspaceGameLibrary -and !$DiscImage){throw 'Game-library acceptance requires DiscImage.'}
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
    if($WorkspacePerformance){$captureArgs+='--workspace-performance'}
    if($WorkspaceNextLayout){$captureArgs+='--workspace-next-layout'}
    if($WorkspaceLibraryTarget){$captureArgs+='--workspace-library-target'}
    if($OptimizationProject){$captureArgs+=@('--optimization-project',(Resolve-Path -LiteralPath $OptimizationProject).Path)}
    if($OptimizationRawNext){$captureArgs+='--optimization-raw-next'}
    if($OptimizationHighQuality){$captureArgs+='--optimization-high-quality'}
    if($OptimizationCompactStorage){$captureArgs+='--optimization-compact-storage'}
    if($OptimizationSmallFile){if(!$OptimizationProject){throw 'Small file workflow qualification requires OptimizationProject.'};$captureArgs+='--optimization-small-file'}
    if($WorkspaceExportCurrent -and $WorkspaceExportNonselected){throw 'Choose one explicit export regression segment.'}
    if($WorkspaceExportOptimization -or $WorkspaceExportCurrent -or $WorkspaceExportNonselected){$captureArgs+='--workspace-export-optimization'}
    if($WorkspaceExportCurrent){$captureArgs+='--workspace-export-current'}
    if($WorkspaceExportNonselected){$captureArgs+='--workspace-export-nonselected'}
    if($WorkspaceExportDirtyModel){$captureArgs+='--workspace-export-dirty-model'}
    if($WorkspaceOptimizationSharedContainer){$captureArgs+='--workspace-optimization-shared-container'}
    if($NativeExportStructure){$captureArgs+='--native-export-structure'}
    if($NativeChr0Forensics){$captureArgs+='--native-chr0-forensics'}
    if($NativeMotionPacVerification){
        $captureArgs+='--native-motion-pac-verification'
        if($QualifiedPacAnimation){
            if(!$QualifiedPacAnimationSha256 -or !$QualifiedPacMaster -or !$QualifiedPacMasterSha256 -or !$QualifiedPacSlot){throw 'Qualified PAC integration requires both exact standalone paths and SHA256 identities.'}
            $captureArgs+=@('--qualified-pac-animation',(Resolve-Path -LiteralPath $QualifiedPacAnimation).Path,'--qualified-pac-animation-sha256',$QualifiedPacAnimationSha256,'--qualified-pac-master',(Resolve-Path -LiteralPath $QualifiedPacMaster).Path,'--qualified-pac-master-sha256',$QualifiedPacMasterSha256,'--qualified-pac-slot',$QualifiedPacSlot)
        }
    }
    if($WorkspaceAnimationOptimization){$captureArgs+='--workspace-animation-optimization'}
    if($WorkspaceOptimizationSegment){$captureArgs+=@('--workspace-optimization-segment',$WorkspaceOptimizationSegment)}
    if($WorkspaceOptimizationLayout){$captureArgs+='--workspace-optimization-layout'}
    if($NativeAnimationOptimization){$captureArgs+='--native-animation-optimization'}
    if($WorkspaceAuthenticFloatDense -and $WorkspaceAuthenticLibraryFloat){throw 'Choose one authentic workflow path.'}
    if($WorkspaceAuthenticLibraryFloat){
        if(!$RetargetSource -or !$AuthenticFixtureManifest){throw 'Authentic library case requires RetargetSource and externally pinned AuthenticFixtureManifest.'}
        $captureArgs+=@('--workspace-authentic-library-float','--authentic-fixture-manifest',(Resolve-Path -LiteralPath $AuthenticFixtureManifest).Path)
        if($AuthenticKeyMode){$captureArgs+=@('--authentic-key-mode',$AuthenticKeyMode)}
    }
    if($WorkspaceAuthenticFloatDense){
        if(!$RetargetSource -or !$AuthenticFixtureManifest){throw 'Authentic export-copy case requires RetargetSource and externally pinned AuthenticFixtureManifest.'}
        $captureArgs+=@('--workspace-authentic-float-dense','--authentic-fixture-manifest',(Resolve-Path -LiteralPath $AuthenticFixtureManifest).Path)
        if($AuthenticKeyMode){$captureArgs+=@('--authentic-key-mode',$AuthenticKeyMode)}
    }
    if($RetargetTrajectoryCase){if(!$WorkspaceAuthenticLibraryFloat){throw 'Trajectory regression needs the authentic Use path.'};$captureArgs+='--retarget-trajectory-case'}
    if($AuthenticFloatExportCopy){$captureArgs+='--authentic-float-export-copy'}
    if($WorkspaceSonicFourStages){if(!$RetargetSource -or !$TargetMotion){throw 'Four-stage Sonic workflow requires its authentic source model and Mario target motion.'};$captureArgs+=@('--workspace-sonic-four-stages','--target-motion',(Resolve-Path -LiteralPath $TargetMotion).Path)}
    if($WorkspaceSonicPipeline){if(!$LibraryIndex -or !$TargetMotion -or !$ProvidedMario){throw 'Sonic pipeline needs library index, target motion and provided Mario CHR0.'};$captureArgs+=@('--workspace-sonic-pipeline','--library-index',(Resolve-Path -LiteralPath $LibraryIndex).Path,'--target-motion',(Resolve-Path -LiteralPath $TargetMotion).Path,'--provided-mario',(Resolve-Path -LiteralPath $ProvidedMario).Path)}
    if($SonicPipelineThorough){$captureArgs+='--sonic-pipeline-thorough'}
    if($SpanRefitRequired){$captureArgs+='--span-refit-required'}
    if($SonicPipelineMode){$captureArgs+=@('--sonic-pipeline-mode',$SonicPipelineMode)}
    if($SonicPipelineReciprocal){if(!$WorkspaceSonicPipeline){throw 'Reciprocal pipeline requires WorkspaceSonicPipeline.'};$captureArgs+='--sonic-pipeline-reciprocal'}
    if($SonicPipelineFitExportSegment){
        if(!$WorkspaceSonicPipeline -or !$SonicPipelineFitOnly -or !$AuthenticFloatExportCopy -or !$SonicPipelineMode){throw 'Fit/export segment requires the existing pipeline, fit-only gates, explicit key mode and verified export-copy hook.'}
        $captureArgs+='--sonic-pipeline-fit-export-segment'
    }
    if($SonicPipelineFitOnly){if(!$WorkspaceSonicPipeline){throw 'Fit-only pipeline requires WorkspaceSonicPipeline.'};$captureArgs+='--sonic-pipeline-fit-only'}
    if($WorkspaceUnifiedOptimization){$captureArgs+='--workspace-unified-optimization'}
    if($WorkspaceCompactOptimization){$captureArgs+='--workspace-compact-optimization'}
    if($NativeChr0WritePath){$captureArgs+=@('--native-chr0-write-path','--clip',$Clip)}
    if($RequireRetainedStorage){$captureArgs+='--require-retained-storage'}
    if($AuthenticL4ApplyCurrent){
        if(!$WorkspaceAuthenticLibraryFloat){throw 'Current L4 Apply qualification requires the actual Library Use case.'}
        $captureArgs+='--authentic-l4-apply-current'
    }
    if($ReviewedRecoveryCase){if(!$WorkspaceAuthenticLibraryFloat -or !$TargetMotion){throw 'Recovery case needs the actual source-pane case and target motion.'};$captureArgs+=@('--reviewed-recovery-case','--target-motion',(Resolve-Path -LiteralPath $TargetMotion).Path)}
    if($ReviewedGroundAfterFit){if(!$ReviewedRecoveryCase){throw 'Reviewed ground phase requires the recovery case.'};$captureArgs+='--reviewed-ground-after-fit'}
    if($ReviewedContacts){$captureArgs+='--reviewed-contacts'}
    if($ReviewedContactReopenProject){$captureArgs+=@('--reviewed-contact-reopen',(Resolve-Path -LiteralPath $ReviewedContactReopenProject).Path)}
    if($ReviewedContactInventory){if(!$TargetMotion){throw 'Contact inventory needs both exact native motion containers.'};$captureArgs+=@('--reviewed-contact-inventory','--target-motion',(Resolve-Path -LiteralPath $TargetMotion).Path)}
    if($NativeDenseFloat){$captureArgs+='--native-dense-float'}
    if($NativeExactI12WritePath){$captureArgs+='--native-exact-i12-write-path';$captureArgs+=('--exact-i12-clip='+$Clip)}
    if($NativeSourceKeys){$captureArgs+='--native-source-keys'}
    if($WorkspaceSourceKeys){$captureArgs+='--workspace-source-keys'}
    if($WorkspaceUseModelKeys){$captureArgs+='--workspace-use-model-keys'}
    if($WorkspaceNativeAnimationExport){$captureArgs+='--workspace-native-animation-export'}
    if($WorkspaceAerialRecovery){if(!$LibraryIndex -or !$TargetMotion){throw "Aerial fitting needs LibraryIndex and TargetMotion."};$captureArgs+=@("--workspace-aerial-recovery","--library-index",(Resolve-Path -LiteralPath $LibraryIndex).Path,"--target-motion",(Resolve-Path -LiteralPath $TargetMotion).Path)}
    if($LegacyFitProject){$captureArgs+=@("--legacy-fit-project",(Resolve-Path -LiteralPath $LegacyFitProject).Path)}
    if($WorkspaceFitReferences){if(!$LibraryIndex){throw "Fitting references need LibraryIndex."};$captureArgs+=@("--workspace-fit-references","--library-index",(Resolve-Path -LiteralPath $LibraryIndex).Path)}
    if($WorkspaceLibraryOrder){if(!$LibraryIndex){throw "Native order needs LibraryIndex."};$captureArgs+=@("--workspace-library-order","--library-index",(Resolve-Path -LiteralPath $LibraryIndex).Path)}
    if($WorkspaceLibraryFlow){if(!$LibraryIndex){throw "Library flow needs LibraryIndex."};$captureArgs+=@("--workspace-library-flow","--library-index",(Resolve-Path -LiteralPath $LibraryIndex).Path)}
    if($WorkspaceDirectSources -or $WorkspaceGameCapacity){if(!$LibraryIndex){throw "Direct/capacity acceptance needs LibraryIndex."};$captureArgs+=@("--library-index",(Resolve-Path -LiteralPath $LibraryIndex).Path);if($WorkspaceDirectSources){$captureArgs+="--workspace-direct-sources"};if($WorkspaceGameCapacity){$captureArgs+="--workspace-game-capacity"}}
    if($WorkspaceGameSources){if(!$LibraryIndex){throw "Saved-source acceptance needs LibraryIndex."};$captureArgs+=@("--workspace-game-sources","--library-index",(Resolve-Path -LiteralPath $LibraryIndex).Path)}
    if($WorkspaceSourceFit){if(!$LibraryIndex -or !$TargetMotion){throw "Source-fit acceptance needs LibraryIndex and TargetMotion."};$captureArgs+=@("--workspace-source-fit","--source-character",$SourceCharacter,"--library-index",(Resolve-Path -LiteralPath $LibraryIndex).Path,"--target-motion",(Resolve-Path -LiteralPath $TargetMotion).Path)}
    if($WorkspaceGameLibrary){$captureArgs+=@('--workspace-game-library','--disc-image',(Resolve-Path -LiteralPath $DiscImage).Path);if($LibraryIndex){$captureArgs+=@('--verified-library-index',(Resolve-Path -LiteralPath $LibraryIndex).Path)}}
    if($CompatibilityProject){if(!$LegacyProject){throw 'Supply the earlier standalone LegacyProject as well.'};$captureArgs+=@('--saved-project',(Resolve-Path -LiteralPath $CompatibilityProject).Path,'--legacy-project',(Resolve-Path -LiteralPath $LegacyProject).Path)}
    if($RetargetSource){$captureArgs+=@('--retarget-source',(Resolve-Path -LiteralPath $RetargetSource).Path,'--clip',$Clip)}
    & $harness @captureArgs
    if($LASTEXITCODE){throw "Background acceptance failed ($LASTEXITCODE). See $output/background.log."}
    Get-FileHash -Algorithm SHA256 -LiteralPath $modelPath,$motionPath | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'source-hashes.json')
} finally {$mutex.ReleaseMutex();$mutex.Dispose();Remove-Item Env:FIGHTERPORT_CAPTURE_CONTROL -ErrorAction SilentlyContinue}
# Create <output>/pause to pause before each capture; remove it to resume.
# Create <output>/cancel to cancel. Captures remain local for actual inspection.
