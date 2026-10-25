# Bounded first-pose exit recovery — 25 October 2026

Preview.28 (`2.0.0-preview.28-exit-recovery`) extends source commit
`9a0b99261a455da5937ad4eeb196cee97997f618` within the ONE incomplete G1/M2
milestone. It addresses the protected hip/leg mismatch recorded in the preceding
[two requested examples](REQUESTED_TRANSITION_EXAMPLES_2026-10-25.md).

## Behavior and boundaries

The existing **Fit start or end** dialog has an explicit, initially disabled
**Allow hip / leg contact recovery on a non-looping exit** option. Whole-clip
contact-trajectory preservation remains the default. Recovery requires an exact
current reviewed body/leg binding, the complete affected body dependency mask,
all scale/rotation/translation components, no entry fit and the frozen first
native frame of an exact target-owned exit reference. A read-only pin review
identifies the current animation's editor pins before preparation.

The selected final recovery window adapts the permitted hip/leg paths. Original
motion through the join, including original key/tangent bits and paired key sides,
is retained. Inserted Hermite joins preserve the original value and derivative;
the final body channels reach the authentic reference first pose with zero
endpoint slope. Free selected body branches use the same existing FK fitting
algorithm. No character, animation name, file, hash or index exception is used.

Reviewed global/motion controls retain their exact source-authored channels for
the entire clip. A contradictory protected-root endpoint rejects recovery rather
than rotating, moving or scaling those controls to force a match. Protected
intervals, fixed/soft authored pins, overlapping timed contacts and existing-bone
editor pins reject conflicting recovery. Constraints are never deleted to obtain
a successful result. Hand/surface/other unsupported contact dependencies reject.

Stored-loop recovery is explicitly unavailable in this bounded increment:
changing an endpoint can alter early sparse/absent channels through the native
wrap bridge. Ordinary existing fitting behavior remains separately available.
Recovery retains duration, native timebase, source/reference content and original
Next provenance. Its explicit permitted release paths and join/end span are
recorded as `contact-exit-recovery-1` in the existing options and receipt fields.
This adds no project-format version or replacement skeleton.

Preparation and native import remain separate, with explicit result/loss review.
Prepare and Apply reject changed content, controls, references and source recovery
provenance. One existing native Undo removes the independent result; Redo restores
it without solving. Review approvals and single/set export dependency snapshots
now also bind the transition policy and receipt. Changing that policy invalidates
approval/export even when native animation content is unchanged.

Transition source/reference staging now uses independent precise native snapshots,
preserving edited imported curves before project Save. It does not promote or
recompress the live clip. The complete BrawlCrate Model Previewer and original Next
retargeter remain intact. Playback performs no recovery calculation.

## Fresh qualification

**1,822 scoped assertions across 14 passing runs**, with **66 fresh native PNGs**
visually inspected. The supplementary requested GIFs have separate local receipts;
their per-render-frame checks are not added to this total.

| Final case | Assertions |
| --- | ---: |
| Native Snake first forward tilt on Mario → Wait1 frame 1 | 23 |
| Native Mario forward aerial on Snake → Fall frame 1 | 17 |
| Existing transition/native control/paired-key workflow | 79 |
| Batch review / verified set export | 66 + 152 |
| Loop inspection / verified single export | 185 + 23 |
| Lifecycle / keyboard / native workspace workflow | 37 + 174 + 61 |
| Source-local root policy, both authentic rig directions | 464 + 464 |
| Procedural boundary/recovery / contact-boundary regression | 58 + 19 |
| Total | 1,822 |

Frozen numeric expectations use independent Hermite polynomials and authored
constants: the procedural recovery midpoint is 40.625 degrees / 31.625 translation
units; the paired-join example retains incoming/outgoing slopes 2/3 and midpoint
46.875. Complete prejoin local/world poses are checked at thirty-second-frame
samples. Rename/reversed-storage, missing bind channels, zero endpoint slope,
root contradictions/full-turn equivalence, pin/interval conflicts, unsupported
paths and saved exact receipts are covered. An independently calculated native
wrap example demonstrates why stored-loop recovery rejects.

Authentic native tests compare every endpoint local transform and evaluated world
matrix against the target reference first pose, with no root/camera compensation.
They check all original prefix keys/tangents and scale-compensation flags exactly,
sample the prefix at quarter frames, reject active pins/wrong phases/moving
references/protected windows, and exercise native Undo/Redo, precise export before
Save and project reopen. A live imported high-precision middle key/tangent verifies
lossless transition snapshots. Existing legacy paired-key/gizmo fixtures were
corrected to edit the reviewed body instead of protected motion controls; their
numeric expectations were retained. The earlier root-edit rejection is preserved
as a superseded harness failure, not an application regression or passing run.

The position/scale/orientation comparison gates remain 0.0002 native units,
0.00001 scale and 0.01 degrees, with tighter existing procedural gates. No bounds
or timeouts were relaxed. Relevant compact/wide and 100/200% workspace keyboard,
export, review and inspector cases run on the new package. Private-desktop
receipts record no active-desktop windows, input injection or screen copying.

## Commands and local evidence

Build with `scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $native`, then
MSBuild `tests/ControlsAcceptance/ControlsAcceptance.csproj` and
`tests/RetargetAcceptance/RetargetAcceptance.csproj` with Release/x86 and the same
`OutDir`. Build the Core test project from the `engine` directory so its .NET 10
SDK selection applies. Local build logs are retained alongside the artifacts.

The following variables represent user-supplied local fixture paths; no game
inputs are distributed in the repository:

```powershell
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ReferenceClip Wait1 -ExitSamples 8 -WorkspaceExitRecovery -NativeDirectory $native -OutputDirectory "$evidence/recovery-mario-final2"
./scripts/Test-BackgroundCapture.ps1 -Model $snake -Motion $marioMotion -RetargetSource $mario -TargetMotion $snakeMotion -Clip AttackAirF -ReferenceClip Fall -ExitSamples 12 -WorkspaceExitRecovery -NativeDirectory $native -OutputDirectory "$evidence/recovery-snake-final2"
```

The same official background runner exercises `-TransitionFit`,
`-WorkspaceBatchReview`, `-WorkspaceSetExport`, `-WorkspaceLoopInspection`,
`-WorkspaceExport`, `-ProjectLifecycle`, `-WorkspaceKeyboard` and
`-SimpleWorkspace`. Root regressions use `RetargetAcceptance.exe --motion-controls`
in both model directions. Core executable cases are `--boundary-fitting` and
`--contact-slots`.

The ignored parent-workspace `artifacts/Fighterport-workspace28` retains the exact
10 native `*-final2` runs, two `roots-*-final` runs, `core-final2` and
`contact-core-final2`. `review-regressions-final2.json` identifies all 66 inspected
PNG hashes. Build logs, source/binary hashes and receipts are sealed locally;
failed/development runs are excluded. The launcher selects this qualified package
only after acceptance. Assets, settings, captures and generated binaries remain
excluded from source-only DoctorFlux publication to public Fighterport/main.

## Requested visual retests

Both demos use the authentic target's **first reference frame only**, frozen,
following the complete fitted attack. No Wait/Fall playback follows the attack.
Mario `AttackAirF` on Snake uses the final 12 samples and reaches all 70 native
local transforms of Snake `Fall` frame 1. Snake `AttackS3S` on Mario uses the final
8 samples, recovers nine hip/leg paths and reaches all 63 local transforms of
Mario `Wait1` frame 1. `AttackS3S2` is excluded. Native world matrices and skinned
vertices are also checked against independently reopened authentic inputs;
protected root curves, original Next revisions and source containers remain exact.

Mario's Wait1 first pose before and after an explicit renderer reset agrees with
the fresh original. Its native hip rotation is authored data, not leftover attack
state. The recovery now reaches that actual pose. The requested foot inspection
uses a fixed level orthographic side camera with a native Y=0 guide and full feet
visible; camera changes never rotate or translate the model or reference pose.

All 109 decoded GIF frames were inspected. Independent decoding verifies dimensions,
frame counts and every GIF delay. The accepted aerial has 76 frames / 2.75 seconds;
the accepted Mario side view has 33 frames / 2.03 seconds. Each contains every
native attack frame followed by only a 1.5-second frozen reference-frame-1 hold.
The separate local demo receipts pass 104 + 203 checks, including endpoint world/
skin agreement against fresh originals and native final/reference pixel agreement.

| Local result | SHA-256 |
| --- | --- |
| `Mario_Forward_Air_on_Snake_to_Fall_FIRST_FRAME.gif` | `26fe78224f253d0729f057394afd4aa75a8d77d14309c10f15a61fcf72e46065` |
| `Snake_First_Forward_Tilt_on_Mario_to_Idle_ORTHOGRAPHIC_SIDE.gif` | `3a7b0cb1a37b5f782acbb09897834c5fc944b6e87a616b2f8a7307b0d2e22259` |

These remain in the ignored parent `artifacts/Fighterport-requested-transitions28`
folder: aerial `mario-attackairf-snake-fall-dev1`, ground
`snake-attacks3s-mario-wait1-dev4`, and friendly copies in `results`.
Local manifests identify the exact receipts, native captures and GIF hashes.
View-development failures and superseded cameras are excluded from delivery.

This qualifies bounded endpoint recovery and preservation. It does not establish
planted-foot/contact quality throughout the adapted window, joint limits, clearance,
arbitrary-character compatibility, state/moveset behavior or game acceptance.
The ground guide is a native-coordinate observation, not a collision solver.
The side retest measures minimum Y on the reviewed weighted foot regions at
integer frames and quarter frames through recovery. It observes approximately
-0.1950 (left) and -0.30127 (right) relative to that guide during recovery. The
authentic Wait1 first frame itself measures -0.03813 / +0.07343. These observations
expose foot dipping; ground-clearance acceptance remains open. No model offset or
root rotation conceals it. Wrong-angle and a blank projection-reset development
capture were rejected; the accepted GIF reasserts and verifies orthographic
projection, fixed camera, complete projected bounds and visible model pixels on
every frame after native selection/frame/render handling.
Physical input, monitor DPI, human usability and broader contact/game gates remain
open in the ONE incomplete G1/M2 milestone.
