# Original Next retargeter and integrated pin authoring

The later [preview.8 control/workspace receipt](NATIVE_CONTROLS_WORKSPACE_ACCEPTANCE_2026-10-23.md)
records shared native gizmos/text fields, interpolation, workspace migration
and fresh two-direction Mario/Snake regression evidence. Earlier checkpoints
below retain their original scope and limitations.

The model-only continuity follow-up below records the later preview.7 changes
and fresh regression runs. The preview.6 retarget checkpoint remains historical
evidence for the unchanged original Next retargeter.

The user corrected the retargeting implementation choice: use the older
Next/BrawlCrate-fork retargeter. The native UI now uses Next's existing
`bind-world-fk-body-contact-ik-v7` implementation from `96a4e0e` for both direct
retargeting and source-bound library reuse. The numerical implementation is
unchanged; the only solver-file addition enumerates already excluded tracks.
The imported engine still supplies independent editor pin/FK/IK controls and
retains its CLI functionality. Previously saved 96.2 drafts keep their reports.

Native options again offer Auto, Grounded, Airborne and Source contacts. Original
mapping and progress windows retain their layout and use dark controls. Input
snapshots, cancellation and dependency guards protect native publication. The
details window visibly exposes Save Result As and names untransferred tracks.
Native list refresh retains resource identity when source/result names match.

## Commands actually executed

Commands ran from the containing FighterPort workspace. `$MSBuild` is installed
VS2022 Community MSBuild; `$dotnet` is the local SDK 10.0.402 executable. NuGet
and CLI homes point to workspace-local caches. Fixture directory variables below
refer to the two user-supplied Brawl Mario and Brawl Snake folders. Private paths,
models, animation containers, project copies, generated CHR0 and PNGs remain local.

```powershell
& $MSBuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false /nologo /v:minimal
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$snake/FitSnake00.pac" -Motion "$mario/FitMarioMotionEtc.pac" -RetargetSource "$mario/FitMario00.pac" -Clip Wait1 -OutputDirectory artifacts/viewer-parity/next-native-retarget-r7
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$mario/FitMario00.pac" -Motion "$snake/FitSnakeMotionEtc.pac" -RetargetSource "$snake/FitSnake00.pac" -Clip Wait1 -OutputDirectory artifacts/viewer-parity/next-native-retarget-snake-to-mario-r1
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model artifacts/viewer-parity/native/original.mdl0 -Motion artifacts/viewer-parity/native/original.chr0 -OutputDirectory artifacts/viewer-parity/next-native-controls-r4
& .local/next-main/scripts/Build.ps1 -DotNet $dotnet
```

The complete native application, engine host, CLI and compatibility worker build
successfully as `2.0.0-preview.6-next-retarget`. The full build log is local
`artifacts/viewer-parity/next-preview6-build.log`.

## Current acceptance evidence

Mario to Snake passes **28 checks** and Snake to Mario passes **28 checks**.
Both use the actual native animation-list selection and modal options, mapping,
progress and details. The direct native bake matches the original solver byte
for byte; the library bake agrees on every native key/tangent/semantic flag/time
and also has the same file hash. Tests independently expect a 0.5-unit pin move
and 15-degree world-Y rotation on the retargeted native animation. Timeline
changes, Undo/Redo, CHR0 export/fresh decode and actual native viewer project
reopening retain the pose and pin. Model bind/skin bytes and every supplied
source container remain unchanged. Cancel publishes nothing; changing settings
during a bake rejects the stale result. An original two-frame ownership probe
verifies explicit exclusion/reporting of an absent accessory track.

The original procedural native fixture passes **40 checks** again: independent
pin identity, actual native pointer handlers, numeric move/rotation, soft reach,
frozen animation following, rest-axis materialization, stale gesture rejection,
exact Undo/Redo and direct empty-viewer startup with the extra Fighter toolbar.
Thus **96 checks** ran in this increment. Earlier imported-engine regression
receipts remain historical evidence and are not counted as new runs.

At the user's explicit fixture request, six additional serialized, below-normal
priority native probes bake and freshly decode Attack11, Run and JumpF in both
directions, preserving frame count and loop metadata. The existing command is
`RetargetAcceptance.exe --bake-probe <source-model> <target-model> <source-motion>
<clip> <new-output-directory>`. The project was built with the same Release/x86
MSBuild command. Local receipts/logs/native outputs are under
`artifacts/viewer-parity/next-native-action-batch-r1/`.

| Source to target | Attack11 frames | Run frames | JumpF frames |
| --- | ---: | ---: | ---: |
| Mario to Snake | 16 | 23 | 56 |
| Snake to Mario | 23 | 31 | 39 |

All six native probes succeed. This is additional native bake/reopen evidence;
it does not rate complete displayed action/contact quality.

Receipts are `receipt.json` in the three local output directories above. Failed
and cancelled diagnostic runs remain local. Final native tests select through
the actual ListView and wait for automatic setup rather than racing it with a
second test-only reload. Same-name source/target refresh has a dedicated guard.

All **13 current PNGs** were actually inspected: both directions' options,
mapping, progress, details and retargeted pin-edited model (five each), plus the
original fixture's complete dark viewer, native model image and empty startup.
These use native OpenGL readback and control printing on a private desktop that
is never activated. Capture is serialized, paced and below normal priority;
pause/cancel controls remain available. No screen pixels or OS input are used.

## Limits and next work

These checks establish the original native retarget route and editable, savable
pin results for the supplied cases. They do not certify general motion/contact/
clearance quality, unfamiliar anatomy, packaging or in-game behavior. Physical
mouse input and per-monitor DPI acceptance remain unverified. System nonclient
title bars and some native arrow glyphs retain the inactive desktop's OS theme.
Full advanced standalone workspace UI ports and future game/format writers remain
queued in the sole current plan; retained CLI contracts are not GUI acceptance.
No asset installation or binary release was performed. M2 remains open.

## Model-only continuity follow-up — preview.7

Creating the first animation from an owned model pose now carries FK-pin and
soft-pin identities into its native animation. Core creates the animation and
transfers the model-only pin ownership in the same Undo transaction; anchors,
native hierarchy/rests/weights and the saved original pose are preserved. The
native frontend freezes dependencies, writes precise native keys and saves the
new animation's independent control state before automatic reload. Engine state
contains the selected animation's contacts/handles, avoiding orphan references
when the native frontend opens a single selected clip.

The complete native/engine/CLI/worker build succeeds as
`2.0.0-preview.7-pose-controls` using `scripts/Build.ps1 -DotNet $dotnet`.
The local build log is `artifacts/viewer-parity/next-preview7-build.log`.

The new acceptance case exposed a separate editor precision defect: a
dot-product shortcut discarded real direction corrections below about 0.08
degrees, leaving an IK endpoint residual above the existing bound. A shared
cross/dot `atan2` rotation now retains these corrections, including near-opposite
directions. Reach policy, endpoint tolerances, native binds and the original Next
retarget solver are unchanged. Rejected preview diagnostics remain visible after
release rather than being replaced by a misleading selection message.

Fresh runs after these changes pass **129 native checks**: the original 40,
Mario-to-Snake 28, Snake-to-Mario 28, original model-only fixture 11 and supplied
Mario and Snake model-only 11 each. Both retarget routes still agree exactly with the original
Next bake. The model-only cases import no motion, pose an arbitrary FK handle
with four soft limb pins, create a native 60-frame clip, retain the same five pin
identities, check the first and final displayed pose, reload the engine, and
freshly decode CHR0/project output. Expected root edits are +1.5 X, +0.75 Y and
15 degrees Y. Original model-pose and new animation states both remain saved.

Fresh engine runs pass **64 controls**, **76 editing**, **32 native writing** and
**9 receipt compatibility** checks: **181**, or **310 checks** with the native
frontend runs. The new independent angular fixtures use −0.04, +0.04 and 179.96
degrees at 0.1, 1 and 3 world units, with unchanged acceptance tolerances and
anonymous bone names. Core first-animation Undo/Redo and project reopen retain
exact pose/anchor ownership. The retained regression renderer still emits its
known AVA3001 build warning; it is not the shipped frontend.

Commands use the same local SDK/MSBuild/cache configuration as above:

```powershell
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model artifacts/viewer-parity/native/original.mdl0 -Motion artifacts/viewer-parity/native/original.chr0 -ModelPose -OutputDirectory artifacts/viewer-parity/next-model-pose-r5
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$mario/FitMario00.pac" -Motion "$mario/FitMarioMotionEtc.pac" -ModelPose -OutputDirectory artifacts/viewer-parity/next-model-pose-mario-r1
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$snake/FitSnake00.pac" -Motion "$snake/FitSnakeMotionEtc.pac" -ModelPose -OutputDirectory artifacts/viewer-parity/next-model-pose-snake-r1
& $dotnet build .local/next-main/engine/tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --nologo -v minimal /p:NuGetAudit=false
& $dotnet $tests artifacts/viewer-parity/next-pose-engine-controls-r1 --viewer-controls --native-controls artifacts/native-v2-snake.fighterport
& $dotnet $tests artifacts/viewer-parity/next-pose-engine-editing-r1 --viewer-editing
& $dotnet $tests artifacts/viewer-parity/next-pose-engine-native-r1 --viewer-native
& $dotnet $tests artifacts/viewer-parity/next-pose-engine-compatibility-r1 --receipt-compatibility
```

`$tests` is the built `engine/tests/FighterPort.Tests/bin/Release/net10.0/
FighterPort.Tests.dll`. The native-writing run selects the current Next worker
with `FIGHTERPORT_BRAWL_WORKER`. The original controls output is
`next-pose-controls-native-r1`; both complete retarget workflows were rerun with
the same commands into `next-pose-mario-to-snake-r1` and
`next-pose-snake-to-mario-r1`. Each directory contains its actual receipt.
All **19 new native PNGs** were inspected (three model-only pairs, three original
control/startup images and both five-image retarget sets). The procedural marker
fixture is numeric/control evidence; the supplied Mario/Snake captures separately
show the actual textured native models and retained handles. Earlier failed
diagnostic runs remain local. No current motion/game/generalization gate is
promoted, and full advanced GUI migration remains open in the one current slice.
