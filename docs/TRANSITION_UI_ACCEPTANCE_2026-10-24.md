# Native fixed-duration boundary fitting — preview.12

This continues the same incomplete M2 integration slice. Local executable:
`artifacts/next-preview12/native/FighterPortStudioV2.exe`, version
`2.0.0-preview.12-transitions`. Builds, native assets and captures remain ignored.
The original Next retargeter `bind-world-fk-body-contact-ik-v7` is unchanged and
remains the direct/library default. Advanced 96.2 scene fitting remains review-only.

## Implemented operation

**Fit / Transitions...** on the top toolbar, or the Fighter menu, opens native
dark controls and two views using the shared model previewer `ModelPanel`, MDL0
materials/shaders, lighting and reference-resource lookup. Preview copies do not
pose the authoring model. Preparation is offline; scrubbing/playback samples
prepared native curves. Playback uses elapsed time at the native 60 Hz timebase.

Choose entry and/or exit references already authored for the current rig, exact
fractional reference frames, frozen poses or moving reference speed, existing
fit sample counts, transform component groups, existing bones and inclusive
protected ranges. UI frames are one-based; the recipe stores zero-based values.
Review the native binding/units explicitly. Donor clips must be retargeted first;
this operation does not infer another rig from matching bone names.

The owned Core operation `fixed-duration-fk-hermite-1` fits Hermite component
curves within the existing duration. It matches selected endpoint values and
per-channel derivatives; reference speed accounts for both timebases. New join
knots analytically split the original curve. Protected middle keys, duplicate
sides and unselected channels remain intact; the native bridge retains their
original bits rather than round-tripping them through JSON. Missing axes of an
authored native group use authentic rest constants. Native transform flags and
unselected groups remain owned by the original resource. Skeleton roots retain
translation by default. Euler turn policy is explicit: nearest equivalent to the
original join, or original authored reference turn counts. Duration, rate and loop
flag are retained; loop-seam quality still needs inspection.

Overlapping/one-sample windows, conflicting protected intervals/events, another
rig fingerprint, invalid reference phases and moving reference value jumps reject
without mutation. Quaternion rotation fitting is unsupported by this Euler-native
operation. Active Core contact/soft-pin constraints and composed companion layers
block FK-only fitting. Actual Next retarget results with recorded baked support
samples also block it: no post-IK endpoint change silently breaks known support.
Imported body clips without declared constraints still have unvalidated contacts.
This is a supported FK component editing operation, not completed state-aware,
world-trajectory, angular-velocity, contact/clearance or game transition acceptance.
SCN0 and other game adapters remain unchanged.

Review the prepared result before adding it as an independent native animation.
Native content, control state, source provenance, reference hashes and options
guard asynchronous publication and Apply. The exact native result is exported
and decoded before publication, checking keys, tangents, flags, duration and loop.
One native Undo/Redo restores the animation list, selection and independent FK
pin state without recomputation. Native model-list rebuilding preserves that
history. Existing bones, hierarchy, binds and weights are never replaced.

Each result stores source/entry/exit CHR0 snapshots with exact hashes, native
identities, reviewed options and the Core recipe. These are portable frozen baked
references, not live recipe links; they cannot form a dependency cycle. Review
retains at most eight MiB of native snapshots, 200000 returned native keys and a
32 MiB encoded project snapshot budget. No game inputs enter the source repository.

**Transition Fit Details / Frozen References** shows the recipe and whether the
current animation matches the initial result or has later edits. Export Frozen
CHR0 writes exact preserved bytes to a new file. Reopening Fit restores saved
settings against the CURRENT animation, preserving later edits outside the new
fit ranges, and requires live binding review again. Unavailable live references
are disabled; their frozen originals remain exportable. This is not a portable
bundle for the advanced scene review's external Core companion project.

## Qualification

The final matrix passes **440 native + 248 focused engine = 688 checks**. All
**61 final PNGs were opened and actually inspected**. Local evidence is under
`artifacts/viewer-parity/`; each native directory below contains `receipt.json`,
and engine directories contain `results.json`. These local receipts, native
snapshots, captures and executable outputs are ignored and not published.

| Final native directory (prefix `next-preview12-`) | Checks | Inspected PNGs |
| --- | ---: | ---: |
| transition-original-final-r3 | 68 | 9 |
| transition-mario-final-r3 | 68 | 9 |
| transition-snake-final-r3 | 68 | 9 |
| retarget-mario-to-snake-final-r1 | 29 | 6 |
| retarget-snake-to-mario-final-r1 | 29 | 6 |
| controls-final-r1 | 41 | 3 |
| review-original-final-r1 | 16 | 3 |
| batch-mario-to-snake-final-r1 | 19 | 2 |
| batch-snake-to-mario-final-r1 | 19 | 2 |
| fitting-original-final-r1 | 31 | 4 |
| fitting-mario-to-snake-final-r1 | 26 | 4 |
| fitting-snake-to-mario-final-r1 | 26 | 4 |

Focused engine directories use `next-preview12-engine-<scope>-r1`: boundary 33,
controls 64, editing 76, native 32, receipts 9 and workspace 34. The new boundary
checks use independent polynomial/derivative expectations, exact duplicate sides,
fractional protected samples, timebase/rate conversion, explicit Euler turns,
renaming/storage permutation and negative rig/contact/protected-window cases.
These are numerical operation checks, not general action-quality acceptance.

Each final transition run exercises real async preparation/cancellation, no late
candidate, explicit review, changed-reference rejection, native import, exact
protected/unselected keys, independent FK pin state, single Undo/Redo, frozen
snapshot corruption rejection/export, fresh project reopen and restored options.
It then runs the existing native FK/IK/pin/pole and interpolation checks on the
FITTED CHR0 itself. Move, rotation rings, scale fields, independently picked pins,
Hermite subframe values and exact native keys/tangents/flags survive fresh decode
and project reopen. Both original Next retarget directions still compare against
the unchanged v7 output, then move/rotate pins and save/reopen. Each also verifies
that known baked support rejects FK-only boundary fitting.

The reference fixture is a test-authored constant 20-degree rotation on a copied
existing root track. It exercises boundary authoring with the supplied native
Mario/Snake Wait1 clips and a procedural rig; it does not establish real
Idle/Fall/state-transition quality. Actual captures show dark native controls,
textured Mario/Snake source/results, the native interpolation curve, independent
pin/pole handles, frozen reference details and reopened settings. Reopened Fit
has no automatically restored candidate (the result view starts at rest). Large
deliberate root-scale/rotation tests can crop the figure in the editing capture;
these images are not camera or reference-layout parity evidence. The original
fixture is the procedural untextured body. Advanced scene review still displays
its failed gates and blocked Apply. No numerical/contact gate was relaxed.

Development failures were corrected before the final matrix: the overlap fixture
initially did not overlap, native test compilation needed tuple/JSON access
corrections, SHA comparison needed case-insensitive hex handling, and rebuilding
the native model list erased Redo history. The implementation now preserves exact
native history lists/cursor across rebuilding. Protected native keys retain their
original bits, including signed zero and duplicate sides. Earlier failed or
superseded capture directories are excluded from the final counts.

Build logs are `artifacts/next-preview12/build-final.log`,
`harness-build-final.log` and `engine-tests-build.log`. The ignored
`reviewed-images.json` enumerates the inspected final PNGs; `qualification.json`
seals the committed source revision, actual receipt/image/build hashes and nine
native/engine binary hashes. This is source-update qualification, not a binary
release or game result. The same sole M2 milestone remains incomplete.

## Commands and limits

Commands run from the outer workspace with Next at `.local/next-main`. Fixture
variables represent local original procedural or supplied Mario/Snake assets;
private paths are excluded from published source/evidence documentation.

```powershell
& .local/next-main/scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $out
& $msbuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj `
  /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$out"
& .local/next-main/scripts/Test-BackgroundCapture.ps1 `
  -Model $model -Motion $motion -TransitionFit `
  -NativeDirectory $out -OutputDirectory $newDirectory
```

The same background script runs retarget pairs with RetargetSource, scene review
with SceneFitting, batch timing with LibraryBatch, chain review with ControlReview,
and original controls with no scope switch. Each uses a new output directory.
Engine test scopes are `FighterPort.Tests <new-dir> --boundary-fitting`,
`--viewer-controls --native-controls <native-project>`, `--viewer-editing`,
`--viewer-native`, `--receipt-compatibility`, and
`--workspace-only <native-backed-project>`.

All capture work is serialized, paced and below normal priority on an inactive
private desktop, using actual ModelPanel captures. No desktop activation, OS
input injection or CopyFromScreen occurs. Physical pointer/DPI, exact reference
raster parity, complete displayed sequences, automatic contact/generalization,
state-machine behavior, packaging and games remain unverified. The full engine
suite is not claimed passing. The test-only legacy Avalonia renderer retains
its existing AVA3001 warning; native build has no warnings. Advanced body knobs,
contact-coupled transitions, slots/Fill Missing and other remaining interfaces
are still open in the single M2 plan.
