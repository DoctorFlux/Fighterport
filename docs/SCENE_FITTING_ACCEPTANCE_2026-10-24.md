# Native scene/body fitting review — preview.11

This continues the same incomplete M2 integration slice after
[preview.10](POLES_BATCH_ACCEPTANCE_2026-10-24.md). The local executable is
`artifacts/next-preview11/native/FighterPortStudioV2.exe`, version
`2.0.0-preview.11-scene-fitting`. Generated builds, captures and native inputs
remain ignored. Original Next `bind-world-fk-body-contact-ik-v7` stays the default
for direct and source-bound retargeting; its numerical implementation is unchanged.

## Behavior and renderer

Select an authentic library clip, then **Advanced Fitting...**, or use Fighter >
Advanced Scene / Body Fitting. Native dark controls expose reviewed source/target
coordinates, native-unit floors, perpendicular ledges and their orientation,
support modes, root policy, body-relative reach, overlap policy, limb tolerance,
source-seconds trims and export speed. Body preference knobs without measured
calibration remain visibly disabled. These are preview/measurement support
surfaces, not native stage output; SCN0 remains inactive.

Both scene views use the model previewer's actual native `ModelPanel`, MDL0 draw
calls, material/texture lookup and shader generator. Each inherits the current
viewer's shader flag, render attributes, lighting and camera factors through its
native viewport settings. The attributes are copied independently. Native model
overlay drawing is shared with the ordinary previewer. Full model roots retain
materials/textures, with private copies of the target viewer's additional resource
references. Native material colors/constants and inactive texture/morph state
initialize before drawing, and cameras frame the evaluated pose. The views do not
pose or mutate authoring models. This does not add animated companion-channel
evaluation to the body-only fitting review.

Preparation calls the retained advanced Core fitting pipeline offline in an
isolated host. It retains diagnostics, channel losses, recipe and measured
receipts. A candidate uses at most 121 native-frame samples as an explicitly
labelled read-only proxy. Preview playback samples this prepared native curve;
it does not invoke the host or solve contacts. **Preview Original Next** uses
the existing native retargeter/mapping gate for comparison without importing its
result. The original fixture has no chest/neck joints, so that comparison reports
its real mapping requirement; supplied Mario/Snake comparisons produce baselines.

The advanced `native-body-surface-96.2` replacement is still a review draft.
Motion/action, joint-limit, collision, contact, native and displayed gates remain
unresolved. Apply is disabled in the window and rejected by the host independently.
Measured output or native reopening does not establish acceptance. Ordinary
Next retargeting and subsequent pin editing remain available.

An optional exact Core `.fighterport` companion supplies authentic source
context. Native deform checksum and selected clip curves/rate/loop must match;
no missing bones are fabricated or keyed source channels removed. The whole
companion, including unselected clips, contacts, layers and pooled library
contexts, stays intact in the host. The saved review references that external
companion with a file hash; it does not embed a portable copy. The full context
fingerprint and preserved native inputs are rechecked before/after preparation
and before saving a receipt. Native model/animation identity/content, reviewed
bindings and verified library references also guard asynchronous publication and
save. Settings changes or cancellation discard late results.

Save Review Settings stores exact source ownership, options and unresolved
receipt as separate project metadata, with one native Undo/Redo. Reopening the
window restores controls and the companion clip selector but does not resurrect
a candidate or enable Apply. Source assets and authoring animation remain intact.

## Qualification

Fresh final native receipts pass **354 checks**. The five focused engine scopes
pass **215**, giving **569** current checks. All **44 final PNGs** were actually
opened for inspection. All directories below are under local
`artifacts/viewer-parity/`; earlier failed/intermediate runs are excluded.

| Scenario | Final receipt directory | Checks | Inspected PNGs |
| --- | --- | ---: | ---: |
| Original fitting/context review | `next-preview11-fitting-original-final-r3` | 31 | 4 |
| Mario to Snake fitting review | `next-preview11-fitting-mario-to-snake-final-r2` | 26 | 4 |
| Snake to Mario fitting review | `next-preview11-fitting-snake-to-mario-final-r1` | 26 | 4 |
| Original poles/pins/gizmos/curves | `next-preview11-poles-original-final-r1` | 40 | 4 |
| Mario poles/pins/gizmos/curves | `next-preview11-poles-mario-final-r1` | 40 | 4 |
| Snake poles/pins/gizmos/curves | `next-preview11-poles-snake-final-r1` | 40 | 4 |
| Mario to Snake original Next retarget | `next-preview11-retarget-mario-to-snake-final-r1` | 28 | 5 |
| Snake to Mario original Next retarget | `next-preview11-retarget-snake-to-mario-final-r1` | 28 | 5 |
| Original controls and dark startup | `next-preview11-controls-final-r1` | 41 | 3 |
| Original reviewed chain/shared roles | `next-preview11-review-original-final-r1` | 16 | 3 |
| Mario to Snake batch/timing | `next-preview11-batch-mario-to-snake-final-r1` | 19 | 2 |
| Snake to Mario batch/timing | `next-preview11-batch-snake-to-mario-final-r1` | 19 | 2 |

Engine result directories are `next-preview11-engine-{controls,editing,native,
receipts,workspace}-r1`, with respectively **64/76/32/9/34** checks. Local
`artifacts/next-preview11/qualification.json` seals exact receipt, inspected-image,
binary and build-log hashes against the published source head.

Fitting checks exercise live native windows with authentic source leases, separate
preview copies, actual inherited shader/light attributes, native material colors,
review-only candidates, saved settings/Undo/Redo, rig-dependency changes and
cancellation. The original companion fixture retains a second unselected clip,
rejects changed context, detects changed preserved native input despite unchanged
project JSON, and verifies the host Apply rejection independently of the UI.
Saved settings reopen with the exact companion selection and no applied draft.
Original source bytes, native bones, binds and weights remain unchanged.

Both supplied-model gizmo runs exercise actual native mouse handlers on the
private desktop: independent pin/pole picking, move/ring rotation/scale, transform
fields, interpolation editing and independent cubic-Hermite expectations. All
authored native key values, duplicate key sides and tangents fresh-reopen exactly.
Both full retarget runs again match the original Next bake byte for byte, then
move/rotate a pin, scrub, Undo/Redo, save/reopen CHR0 and reopen the project in the
native viewer. Batch timing and manual control-review regressions also pass.

Visual review confirms dark fitting fields, readable scrollable diagnostics,
correctly framed supplied models with textured native materials, visible ledges,
explicit draft captions and disabled Apply. Original fixtures use procedural
geometry markers rather than supplied-character assets. Deliberate whole-root
rotation/scaling in gizmo tests can crop the enlarged pose until reframed; those
captures verify editing presentation, not a camera-parity claim. No full action
sequence or physical pointer/DPI acceptance is inferred from these screenshots.

Development failures were retained and corrected: initial bare previews omitted
native material initialization and framed before posing; constructor events fired
before all fields existed; a callback hid Form.Load; stale bindings/context and
asynchronous context selection needed guards. The minimal original source needed
test-authored rest constants on existing pelvis/spine channels to satisfy the real
library association gate. Its missing chest/neck mapping stays a real limitation.
A legacy test JSON rewrite rounded authentic float representations; the final
fixture preserves the original clip JSON exactly while adding its second clip.
An early capture launch hit the serialization mutex and produced no overlapping
capture; the final matrix ran serially. No production numerical gate was relaxed.

## Commands and limits

Commands run from the outer workspace with Next at `.local/next-main`. Fixture
variables denote locally supplied Mario/Snake model/MotionEtc files or original
procedural native fixtures; private paths are excluded from this document.

```powershell
& .local/next-main/scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $out
& $msbuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj `
  /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$out"
& .local/next-main/scripts/Test-BackgroundCapture.ps1 `
  -Model $targetModel -Motion $donorMotion -RetargetSource $donorModel `
  -SceneFitting -NativeDirectory $out -OutputDirectory $newEvidenceDirectory
```

The original fitting run omits RetargetSource. Other native scenarios use the
same script with ControlGizmos, LibraryBatch, ControlReview or no scope switch.
Focused engine scopes use `FighterPort.Tests <new-dir>` with `--viewer-controls
--native-controls <native-project>`, `--viewer-editing`, `--viewer-native`,
`--receipt-compatibility`, and `--workspace-only <native-backed-project>`.
The full engine suite's retained contact/generalization failures are not claimed
passing. Native build has no warnings; the test-only Avalonia renderer retains
its existing AVA3001 private-API warning.

Captures use the serialized, paced, below-normal-priority private desktop through
the shared native ModelPanel screenshot route. No desktop activation, OS input
injection or CopyFromScreen occurs. Actual PNGs must be opened for visual review;
capture generation alone is not evidence. Physical pointer/DPI, exact reference
raster parity, full motion/contact quality, broader anatomy/generalization, other
adapters, packaging and games remain unverified. M2 is incomplete. Transition/slot
records and remaining expert interfaces retain their implementation queue.
