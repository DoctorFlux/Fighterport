# Native viewport poles and batch timing — preview.10

This continues the same incomplete M2 slice after
[preview.9](CONTROL_REVIEW_ACCEPTANCE_2026-10-23.md). The dark standard viewer,
direct startup, native camera and original Next retargeter remain. The runnable
local build is `artifacts/next-preview10/native/FighterPortStudioV2.exe`, version
`2.0.0-preview.10-poles-batch`. Generated builds, inputs and evidence stay ignored.

## Implemented behavior

Reviewed IK chains expose magenta pole diamonds in the viewport and a separate
Pole controls group in Animation Controls. Picking a diamond selects its control
without selecting a bone. The original native Move gizmo edits its world position;
the existing Core IK solver keeps the effector position and commits the bend to
existing native joint channels. Native Undo/Redo restores the pose. Curves opens
the authored middle-joint track; selecting the parent FK control exposes the
other authored track. The evaluated bend regenerates the pole display direction
at one chain length after commit and on scrubbing. Poles edit position only;
ordinary FK, IK effector and pin controls retain Move/Rotate/Scale. Nonuniform
ancestor-scale IK restrictions and all existing solver bounds remain unchanged.
The viewport computes display geometry from native matrices; no solver/worker
runs during playback. Existing bones, hierarchy, binds and weights are preserved.

Library **Batch / Fit** presents up to 200 matches from the current metadata
search. Shared output frames and per-row overrides accompany inclusive source
trim bounds. Zero output frames uses the shared count; shared zero keeps the
selected source range. Identity timing passes the authentic source directly to
the original Next solver. Changed timing resamples original native curves
offline at fractional source times, preserves transform interpretation flags,
and runs that same solver. Such fits explicitly become non-looping. UI frame
numbers start at 1; saved recipes retain zero-based source bounds and exact
output sample counts beside the authentic original library reference.

Preparation produces review candidates. Residuals and complete omitted-track
lists appear in the selected-row report. Explicit review enables import. Changed
target content, project identity, contact mode or source references reject stale
results; editing row/shared settings discards candidates. Successful candidates
become independent native target revisions in one import Undo. Source leases
close between rows; review is bounded to 64 clips and two million native keys.
Adding results uses original ground/model camera framing. The original
single-clip action, options/mapping/progress/details and numerical retargeter
`bind-world-fk-body-contact-ik-v7` remain intact.

## Fresh qualification

Final native receipts total **271 checks**. The five focused engine scopes add
**215**, giving **486** current checks. All listed runs returned success. This
is fresh qualification with the same total as preview.9, not reused evidence.

All directories below are under local `artifacts/viewer-parity/`.

| Native scenario | Final receipt directory | Checks | Inspected PNGs |
| --- | --- | ---: | ---: |
| Original pole/pin/gizmo/curve fixture | `next-preview10-poles-original-final-r3` | 40 | 4 |
| Supplied Mario pole/pin/gizmo/curves | `next-preview10-poles-mario-final-r2` | 40 | 4 |
| Supplied Snake pole/pin/gizmo/curves | `next-preview10-poles-snake-final-r2` | 40 | 4 |
| Mario source to Snake batch | `next-preview10-batch-mario-to-snake-final-r4` | 19 | 2 |
| Snake source to Mario batch | `next-preview10-batch-snake-to-mario-final-r2` | 19 | 2 |
| Mario source to Snake original Next workflow | `next-preview10-retarget-mario-to-snake-final-r2` | 28 | 5 |
| Snake source to Mario original Next workflow | `next-preview10-retarget-snake-to-mario-final-r2` | 28 | 5 |
| Original soft/follow/arbitrary pins and startup | `next-preview10-controls-final-r1` | 41 | 3 |
| Original chain/shared-role review | `next-preview10-review-original-final-r1` | 16 | 3 |

Pole scenarios exercise actual native viewport mouse handlers on real private-
desktop windows: independent diamond picking, changed middle-joint bend, held
effector position, native commit, Undo/Redo, correct interpolation target, exact
fresh CHR0 key/tangent comparison and project control persistence. Existing pin
move/rotation/uniform scaling and independent cubic-Hermite expectations run
alongside them. Both full retarget workflows again compare the ordinary native
bake byte for byte with original Next, move/rotate a pin on the result, scrub,
Undo/Redo, save/reopen CHR0 and reopen the actual project viewer. They also test
cancellation, changed options and authentic source-bound library use.

Both final batch runs use the supplied donor's Wait1/Wait2. They select source
frames 2–5, shared 9 output frames and a per-row 7-frame override, inspect the
full report, reject changed contact dependencies, import, reopen both native
outputs/project recipes and undo the complete import. An original two-key curve
provides independent Hermite expectations for fractional source sampling;
invalid ranges and transform-flag/source immutability are checked separately.

Focused engine results are `next-preview10-engine-{controls,editing,native,
receipts,workspace}-r1/results.json`: respectively **64/76/32/9/34**. They cover
existing-rig controls, soft reach/frozen following, shared editing/interpolation,
native writers, historical receipt compatibility and native-backed workspace
persistence. The full engine suite is not claimed passing; retained numerical
contact/generalization failures remain unresolved.

## Commands and actual visual review

Commands ran from the outer workspace with Next at `.local/next-main`.
Fixture variables represent locally supplied Mario/Snake model and MotionEtc
files or original procedural MDL0/CHR0 fixtures. No private path is published.

```powershell
& .local/next-main/scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $out
& $msbuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj `
  /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false `
  "/p:OutDir=$out/" /nologo /v:minimal
& .local/next-main/scripts/Test-BackgroundCapture.ps1 `
  -Model $marioModel -Motion $marioMotion -ControlGizmos `
  -NativeDirectory $out -OutputDirectory $newEvidenceDirectory
& .local/next-main/scripts/Test-BackgroundCapture.ps1 `
  -Model $snakeModel -Motion $marioMotion -RetargetSource $marioModel `
  -LibraryBatch -NativeDirectory $out -OutputDirectory $newBatchDirectory
```

Reverse fixture substitutions, ordinary retarget mode (omit `-LibraryBatch`),
original controls mode (omit special flags) and `-ControlReview` produced the
remaining table rows. Build/test logs and the local qualification seal are under
`artifacts/next-preview10/`. Native/engine builds succeed without warnings; the
retained engine regression renderer emits its existing AVA3001 private-API
warning. It is not the shipped editor.

```powershell
& $dotnet build .local/next-main/engine/tests/FighterPort.Tests/FighterPort.Tests.csproj `
  -c Release -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false
$env:FIGHTERPORT_BRAWL_WORKER = Join-Path $out 'engine/worker/FighterPort.BrawlWorker.exe'
& $dotnet $tests $freshOutput --viewer-controls --native-controls $nativeSnakeProject
& $dotnet $tests $freshOutput --viewer-editing
& $dotnet $tests $freshOutput --viewer-native
& $dotnet $tests $freshOutput --receipt-compatibility
& $dotnet $tests $freshOutput --workspace-only $nativeBackedOriginalProject
```

Each engine command used a distinct fresh output directory. All **32 final native
PNGs were opened and visually inspected**, including pole diamonds/gizmos,
controls trees, real GL interpolation plots, both batch reports/results, original
Next dialogs, moved/rotated pin models, empty startup and manual review tabs.
Captures were serialized, paced, below normal priority, pause/cancel capable and
on a private desktop never activated. No desktop input was injected or screen
copied. The original fixture has deliberately unweighted boxes separate from
its bones. Stress-test uniform scaling crops the top of supplied models in the
final gizmo overview; this is recorded rather than treated as full framing or
motion-quality acceptance. Batch and ordinary retarget captures show framed
actual supplied models. Private-desktop OS title bars can remain light; editor
content stays dark.

Intermediate attempts remain local: the pole Undo harness initially raced the
real asynchronous reload and was corrected to await it; the batch harness first
omitted lazy library-drawer creation; visual review found missing batch camera
framing; selected-row report verification found the native CurrentCellChanged
refresh requirement. Affected scenarios were rerun above. An engine publish
stalled during network NuGet audit; only that task's publish processes were
stopped and rebuilt using the existing cache with auditing/shared compilation
disabled. Other open apps/builds were preserved. No failed attempt is a pass.

## Remaining boundary

This implements viewport poles and native batch/timing fitting. Retained advanced
source-context/scene/body fitting review, calibrated body preferences and future
transition/slot implementation remain separate work in the one current M2 queue.
Replacement-engine contact/quality failures are not promoted by native outputs.
Physical pointer/capture/DPI behavior, exact reference raster parity, complete
motion/style/contact quality, held-out generalization, other game adapters,
repacking/building and actual game tests remain unverified. Native save/reopen
proves persistence only. M2 remains incomplete. Publication is source only to
authorized Next main; no game inputs, local paths or binaries ship.
