[CmdletBinding()]
param([Parameter(Mandatory)][string]$Model,[Parameter(Mandatory)][string]$Motion,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$SuppliedModel,[string]$RetargetSource,[string]$Clip='Wait1',[switch]$ModelPose,[string]$NativeDirectory,[switch]$ControlGizmos,[switch]$ControlReview,[switch]$LibraryBatch,[switch]$SceneFitting,[switch]$TransitionFit,[switch]$ContactAuthoring,[switch]$CombinedStress,[string]$ContactTransitionsProject,[switch]$CharacterWorkspace,[switch]$SimpleWorkspace,[switch]$WorkspaceFlow,[string]$CompatibilityProject,[string]$LegacyProject)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$modelPath=(Resolve-Path -LiteralPath $Model).Path
$motionPath=(Resolve-Path -LiteralPath $Motion).Path
if(($SimpleWorkspace -or $WorkspaceFlow) -and !$RetargetSource){throw 'Workspace acceptance requires RetargetSource pointing to the authentic donor model.'}
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
    if($WorkspaceFlow){$captureArgs+='--workspace-flow'}
    if($CompatibilityProject){if(!$LegacyProject){throw 'Supply the earlier standalone LegacyProject as well.'};$captureArgs+=@('--saved-project',(Resolve-Path -LiteralPath $CompatibilityProject).Path,'--legacy-project',(Resolve-Path -LiteralPath $LegacyProject).Path)}
    if($RetargetSource){$captureArgs+=@('--retarget-source',(Resolve-Path -LiteralPath $RetargetSource).Path,'--clip',$Clip)}
    & $harness @captureArgs
    if($LASTEXITCODE){throw "Background acceptance failed ($LASTEXITCODE). See $output/background.log."}
    Get-FileHash -Algorithm SHA256 -LiteralPath $modelPath,$motionPath | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'source-hashes.json')
} finally {$mutex.ReleaseMutex();$mutex.Dispose();Remove-Item Env:FIGHTERPORT_CAPTURE_CONTROL -ErrorAction SilentlyContinue}
# Create <output>/pause to pause before each capture; remove it to resume.
# Create <output>/cancel to cancel. Captures remain local for actual inspection.
