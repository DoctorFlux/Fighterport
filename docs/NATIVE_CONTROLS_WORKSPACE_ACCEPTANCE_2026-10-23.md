# Native controls, interpolation and character workspaces — preview.8

This continues the ONE M2 integration slice. Next remains the primary app,
opens directly in its dark standard model viewer, and retains the original
`bind-world-fk-body-contact-ik-v7` retargeter. This receipt establishes the
operations below; it does not complete the standalone roadmap or rate complete
retargeted motion, contact generalization, other adapters or game results.

## Implemented behavior

The Animation Controls window displays the existing FK hierarchy, structurally
reviewed IK chains and independent saved pins. Its bone-picking option routes
the native Bones list and viewport bone selection to an FK handle. Pins retain
their separate identities, diamond picking and optional pins-only selection.
Move, Rotate and Scale reuse native axes, rings, triangles, coordinate modes
and hit testing. Camera gesture code is unchanged. FK numeric fields use native
local SRT; IK/pin fields use world position/rotation and local scale. Limb IK and
soft pins require positive uniform scale; unsupported results reject explicitly.
Escape cancels. Cut/Copy/Paste of the native frame fields remains disabled for
handles pending its own control-aware clipboard implementation; ordinary bone
clipboard operations remain available.

Edits commit to existing native bone channels through the shared gesture and
native Undo/Redo path. The original interpolation editor targets those same
channels, with dark graph/background colors. Tangent edits create native undo
commands and invalidate stale engine snapshots. Fractional samples interpolate
from saved native curves without running a solver. Native discontinuities and
separate incoming/outgoing tangents are preserved by supported control edits;
fractional native key indices and more than two sides at one index reject.
The inherited Core adapter's separate export operation retains its stricter
unique-time/single-tangent requirements. No broader writer acceptance is implied.

Workspace manages Core characters, shared scenes, game/build target metadata,
independent target copies, native scene opening and Save/Save As. Native editor
packages retain resources, pin state and keys, while ordinary project Save
keeps its original destination. Existing standalone scenes open through exact
native model/resource identities and saved setup data. Unchanged motion
containers are reused rather than exporting hundreds of redundant overlays.
Legacy Core clip IDs remain explicitly linked to native resource identities
through save and upgrade. Earlier cached geometry is retained; if its checksum
differs, saving requires an unchanged skeleton and exact deform agreement with
a fresh read of the hash-verified authentic native model, with an explicit
diagnostic. An actually changed native model still rejects. Unrepresented Core sources, layers and contracts
survive native updates; previous retarget acceptance becomes stale. Edited
legacy companion contracts not projected into this preview produce a diagnostic.
Foreign target and preserved-input output paths are rejected. Safe package
extraction rejects escaping paths and enforces file/count/size limits.

The supplied pin tests exposed x86 float payload corruption in BrawlLib's
big-endian single reader. Integer bit swapping now avoids loading reversed
bytes through a floating-point intermediate. Five independent exact-bit cases
and all authored native IK key values/tangents fresh-reopen exactly. The
original Next retarget numerical code is unchanged. Explicit GL disposal stays
on its owner thread; finalization no longer tries to create a window/context.

## Build and commands actually run

Commands ran from the containing FighterPort workspace. `$MSBuild` denotes
VS2022 Community MSBuild, `$dotnet` the local .NET SDK 10.0.402 executable,
and NuGet/CLI homes use local ignored caches. `$mario` and `$snake` refer to
the user-supplied asset folders, which remain private and unpublished.

```powershell
& .local/next-main/scripts/Build.ps1 -DotNet $dotnet -NativeDirectory artifacts/next-preview8/native
$native = Join-Path (Get-Location) 'artifacts/next-preview8/native/'
& $MSBuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native" /nologo /v:minimal
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$mario/FitMario00.pac" -Motion "$mario/FitMarioMotionEtc.pac" -NativeDirectory $native -ControlGizmos -OutputDirectory artifacts/viewer-parity/next-preview8-gizmo-mario-r1
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$snake/FitSnake00.pac" -Motion "$snake/FitSnakeMotionEtc.pac" -NativeDirectory $native -ControlGizmos -OutputDirectory artifacts/viewer-parity/next-preview8-gizmo-snake-r1
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model artifacts/viewer-parity/native/original.mdl0 -Motion artifacts/viewer-parity/native/original.chr0 -NativeDirectory $native -OutputDirectory artifacts/viewer-parity/next-preview8-controls-r1
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$snake/FitSnake00.pac" -Motion "$mario/FitMarioMotionEtc.pac" -RetargetSource "$mario/FitMario00.pac" -Clip Wait1 -NativeDirectory $native -OutputDirectory artifacts/viewer-parity/next-preview8-mario-to-snake-r2
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$mario/FitMario00.pac" -Motion "$snake/FitSnakeMotionEtc.pac" -RetargetSource "$snake/FitSnake00.pac" -Clip Wait1 -NativeDirectory $native -OutputDirectory artifacts/viewer-parity/next-preview8-snake-to-mario-r1
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model artifacts/viewer-parity/native/original.mdl0 -Motion artifacts/viewer-parity/native/original.chr0 -NativeDirectory $native -CharacterWorkspace -OutputDirectory artifacts/viewer-parity/next-preview8-workspace-final-r1
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$snake/FitSnake00.pac" -Motion "$snake/FitSnakeMotionEtc.pac" -NativeDirectory $native -CompatibilityProject artifacts/viewer-parity/next-pose-mario-to-snake-r1/retarget-pins.fpstudio -LegacyProject artifacts/native-v2-snake.fighterport -OutputDirectory artifacts/viewer-parity/next-preview8-project-compatibility-final-r2
```

The three `-ModelPose` runs use the same original/Mario/Snake model-motion
pairs and fresh `next-preview8-model-pose-{original,mario,snake}-r1` folders.
The native application, host, CLI and worker build successfully as
`2.0.0-preview.8-controls-workspace`. Final local logs are
`artifacts/next-preview8/final-build.log` and `final-harness-build.log`.
The isolated output preserves the earlier executable still open on the PC.

## Acceptance matrix

Receipts live under ignored `artifacts/viewer-parity/`. Repeated intermediate
runs are not added again to the counts below.

| Current run | Checks | Evidence |
| --- | ---: | --- |
| `next-preview8-gizmo-mario-r1` | 31 | Bone/FK/tree selection; actual native move/ring/scale hit tests; numeric fields; arbitrary pins; interpolation/tangent/Undo; transient IK and all-key fresh CHR0 reopen |
| `next-preview8-gizmo-snake-r1` | 31 | Same independent expectations on supplied Snake |
| `next-preview8-controls-r1` | 41 | Original procedural fixture, startup, pin gesture, cancellation/stale guards, native Undo and save/reopen |
| `next-preview8-mario-to-snake-r2` | 28 | Actual native retarget windows, original-solver equality, moved/rotated pin keys, project/CHR0 reopen and immutable sources |
| `next-preview8-snake-to-mario-r1` | 28 | Same workflow in reverse |
| `next-preview8-workspace-final-r1` | 19 | Shared plus two independent targets; native packages; descriptor reopen; actual viewer replacement; source/output guards |
| `next-preview8-project-compatibility-final-r2` | 12 | Earlier Next pin project, exact native keys, 355-clip standalone Snake open/update/upgrade with unchanged Core clip IDs; authentic model proof and changed-geometry rejection |
| `next-preview8-model-pose-original-r1` | 11 | Model-only pose and control ownership through first-animation creation and fresh reopen |
| `next-preview8-model-pose-mario-r1` | 11 | Supplied Mario model-only continuity |
| `next-preview8-model-pose-snake-r1` | 11 | Supplied Snake model-only continuity |

These are **223 native checks**, plus **215 focused engine checks** below,
for **438 checks**. The original Core numerical/default suite is not claimed
passing: its previously recorded contact/generalization failures remain open.

The focused engine executable was rebuilt in Release, with the retained
Avalonia regression renderer's existing AVA3001 warning; that renderer is not
shipped as the editor. The native product build reports zero warnings/errors.
Tests ran below normal priority with the freshly published native worker:

```powershell
& $dotnet .local/next-main/engine/tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll artifacts/viewer-parity/next-preview8-engine-controls-r1 --viewer-controls --native-controls artifacts/native-v2-snake.fighterport
& $dotnet .local/next-main/engine/tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll artifacts/viewer-parity/next-preview8-engine-editing-r1 --viewer-editing
& $dotnet .local/next-main/engine/tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll artifacts/viewer-parity/next-preview8-engine-native-r1 --viewer-native
& $dotnet .local/next-main/engine/tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll artifacts/viewer-parity/next-preview8-engine-compatibility-r1 --receipt-compatibility
& $dotnet .local/next-main/engine/tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll artifacts/viewer-parity/next-preview8-engine-workspace-r2 --workspace-only artifacts/viewer-parity/native/source.fighterport
```

Their result counts are respectively **64/76/32/9/34**. The workspace command
uses the native-backed original fixture; an earlier procedural-only invocation
failed its positive native-export requirement and is retained as failed evidence.

Six fresh headless native probes also bake Attack11, Run and JumpF in both
Mario/Snake directions and decode the saved CHR0 timing/loop metadata. They
use `tests/RetargetAcceptance --bake-probe <source> <target> <motion> <clip>
<new-output>` and retain results in `next-preview8-native-action-batch-r1`.
This is additional native-output evidence, not complete displayed action quality.
Reported maximum limb residuals include approximately 0.482 for Mario-to-Snake
Run and 0.737 for Snake-to-Mario JumpF; neither is relabeled as a quality pass.

## Actual visual review and limits

All 30 PNGs belonging to the current native matrix were opened for inspection:
six control/tree/curve images, three original controls images, ten retarget
window/model images, three workspace images, two compatibility images and six
model-only images. Capture is serialized, paced and below normal priority on
a private desktop that is never activated. Actual native handlers and GL readback
are exercised; no OS input is injected, no screen is copied, and background tests
do not write the user's viewer settings. A pause file pauses between captures;
a cancel file aborts. Old failed/intermediate captures remain local.

The dark controls, actual textured Mario/Snake, native graph, stacked viewer
lists and target tree were reviewed. The gizmo batch's final 1.78 root scale
crops the top of the models because that test does not reframe after scaling;
this is retained framing evidence, not a complete motion-quality inspection.
The original procedural fixture has deliberately unweighted separate boxes;
their separation from edited bones is not evidence about supplied skinning.
The legacy compatibility capture follows its deliberate single-vertex rejection
case; the changed preview is not saved into the upgraded workspace or original.
Physical mouse/keyboard, per-monitor DPI and complete reference-window pixel
parity remain unverified. Advanced fitting/transition/slot/layer/media GUIs,
remaining adapters, cooking/building and actual games remain unfinished.

Earlier failed probes remain recorded: two legacy-preview attempts timed out
while exporting every original clip; unchanged-container reuse fixed that path.
A subsequent save attempt correctly rejected an older cached geometry checksum;
the final path proves the native model against the exact authentic resource,
keeps the old Core scene, and separately rejects an actual native geometry edit.
A two-frame retarget cancellation test could finish before its timer; the
current test cancels at the actual progress-window Shown boundary. No solver
numerics or acceptance limits changed to make those checks pass.

The next task in the same M2 slice is to expose the retained expert editing
contracts through native windows, starting with transition fitting/slots and
explicit per-operation support, while retaining the original retarget workflow
and all existing failed numerical and game gates. M2 remains incomplete.
