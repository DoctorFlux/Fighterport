# Preview.65.6 — between-sample pole continuation

Starting commit: `65240305420e7bb825874b1a9ed2172585db55a6`.
Bounded increment inside the ONE incomplete G1/M2; preview.66 finite ledges
remains queued. Preserve the previous failed evidence and all existing gates.

## Cause and implementation

The unchanged .65.5 binary reproduces Fox → Peach `AttackS3S` rejection at
native time **3.213408**, target diagnostic `RArmJ`: **0.5949395976°** versus
the unchanged **0.025°** world-orientation limit. Original files and native
ownership remain unchanged on failure.

Temporary diagnostics show a generated Euler discontinuity across
3.21340752…3.21340847: approximately `(85.3651, -89.22375, -84.54253)` →
`(265.3516, -90.77625, -264.529)`. These coordinates switch equivalent families
while the physical orientation remains smooth. Fitting across that jump creates
enormous artificial slopes and fails even after depth-20 subdivision.

The sequential guide previously checked conditioning at its start/mid/end
samples only. Around this witness its guide runs from pitch -84.94677° to
-97.31918°, crossing -90° between samples even though neither is inside the
existing near-pole cone. No extra physical guide witnesses ran there. Later
nearest-family extraction crosses the guide's family decision boundary and
jumps by 180°.

`AdaptiveRotationCurveBaker.TraceContinuation` now also checks whether either
half-guide spans **any `90 + 180k` pitch pole**, using the interval endpoints
and double floor/ceiling arithmetic. Such intervals receive the same existing
physical quarter-guide orientation checks and bounded recursive refinement.
No new rotation evaluator, solver, quaternion motion interpolation, source
curve rewrite or tolerance is introduced. Actual source FK, original solved
eighth-frame anchors, correction transport and full-turn semantics stay intact.

The numerical rule has no character/action/name/hash/index conditions. Names
above identify diagnostic fixtures only. Depth, per-window work/key bounds,
orientation/translation/scale/contact gates, protected motion controls, target
hierarchy/rest/binds/inverse binds and all skin weights are unchanged. It runs
offline during preparation, never on playback ticks.

## Tests and counterexample

Four additional independent procedural paths put the pole between guide
samples: both signs and charts shifted by ±720°. The supplied equivalent Euler
endpoint is shifted in the same chart, avoiding an invented winding contract.
Each has 4,106 native evaluation witnesses including independent uniform
1/4096-frame sampling and narrow off-grid samples. Existing 720° full turns,
narrow-pole excursions, discontinuous-reference rejection, cancellation,
renamed/permuted unequal rigs, all-weight solving and constant/protected root
checks remain included.

Running the final procedural harness against the old application reproduces a
**0.78071254°** failure on the first new path. It passes with the fix. No real
asset is needed for this regression. The four new paths' independent native
sample maxima are **0.02341726° or lower**, within the unchanged 0.025° bound.
An initial shifted-chart test used an
incorrect equivalent endpoint outside its declared turn chart; that test setup
was corrected without changing any numerical gate or production algorithm.

Final-build procedural acceptance:

| Suite | Passing assertions |
|---|---:|
| Local/native precision, including new pole paths | 1,224 |
| Bounded adaptive baking, constant proofs and native window seams | 19 |
| Coordinate/renaming/storage permutation | 94 |
| Source-owned motion controls | 628 |
| Actual library Use, two source-key options | 56 |
| Total | 2,021 |

Both actual Library Use cases run on private desktops through the normal
source/target mapping/review/import boundary. Each checks original input and
all-weight/bind preservation, the exact chosen representation, native export/
reopen, project Save/reopen, source/costume identity and current target/bone/
playhead/existing Undo owner. They do not inject input or claim physical-input
acceptance. The existing native Undo stack is preserved; no new stack or
document owner is added.

## Fixed native result

The full-quality master now finishes, with fresh whole-master decode checked
at **5,180 witnesses**. It uses **2,485,369 total curve work across five existing
windows**, largest window **814,008 / 2,000,000**. No budget increase. One
recorded run takes 3,487 ms for master creation/export and 25,331 ms for default
preparation; these observations are not a performance benchmark.

| Fixed default decoded trajectory field | Value | Existing limit |
|---|---:|---:|
| Maximum original-anchor world orientation | 0.01554607° | 0.025° |
| Source-owned root-local difference | 0 | 0.0001 |
| Supported-surface penetration | 0.000851676 | 0.007871705 native units |
| Planar lock drift | 0.000530119 | 0.007871705 native units |
| Supported-end orientation versus raw Next | 0.08148587° | 0.25° |

Independent dense/default trajectory oracles use 397 integer/eighth/withheld
times apiece. Both actual Use exports also pass those oracles. The complete
native bake and source-key preparation separately retain broader scalar and
cumulative native quality packets. Anchor-only measurements are not claims
about every arbitrary subframe.

The pre-existing optional support proposal reaches its separate bounded
1,998,515 / 2,000,000 work reserve and retains original correction anchors.
Its unqualified-proposal warning is preserved. Passing the sampled native/
contact oracle does not invent reviewed contacts or promote that proposal.
Source-owned master → prepared control coefficients/flags are exact, and model
ownership fingerprints retain every weight, hierarchy, rest and inverse bind.

## Corpus, rendering and evidence

The full 24-case forward-tilt/all-five-aerial corpus is rerun with identical
Auto contact/default source-channel policy: **24/24 pass**. All **23 previously
passing default exports remain byte-identical** to their .65.5 results; the
remaining case is newly successful. The prior rejection record is retained.

All **48 corpus dense/default native/contact oracles** pass, with **12,960
default-result witness times** and matching dense schedules. Four additional
fixed-result/actual-Use oracles pass. All 24 preparation receipts preserve native
ownership/original files and exact finished-master → prepared root controls.
Final local `summary.json` verifies complete case counts, all oracle exits,
unchanged previous bytes, visual hashes and the frozen application identity
before generating `index.html`. The bounded preview.65.6 increment qualifies;
the ONE G1/M2 remains incomplete.

Local evidence: outer workspace `artifacts/Fighterport-rotation656`.
`starting-commit.txt`, baseline/diagnostic/counterexample logs, final build7,
precision/budget/coordinate/root receipts, `fixed7`, two `use7-*` private-desktop
runs and native oracles, `run7/results.json`, sealed `side-manifest.json`, frozen
renderer source/build receipts, `side7/receipt.json`, decoded GIF timing facts
and `visual-review7` retain the exact run identities. The reused corpus manifest
describes the original .65.5 inventory; this increment's starting revision is
the commit above and its application identity comes from each new run receipt.

One fresh **73-frame orthographic +X side comparison GIF** passes **379 renderer
assertions**. All 73 decoded frames are actually inspected on ten sheets, along
with one full-size action PNG and all eight fresh mapping/Use UI PNGs. Review
hashes and GIF delays are independently checked. The camera is level and fixed
over the full move, at common native units/pixel; no model rotation, root
stripping or per-frame recenter. Half-native-frame samples play at 30 GIF fps
(quarter nominal-60fps speed). Gold guides show each native bind plane; these
are not newly reviewed gameplay terrain.

The GIF repeats for inspection without changing stored non-looping CHR0 timing.
It shows the full move, with no Wait/Fall fitting/append. CHR0-only native default
visibility can show unrelated props; weapon/visibility/material animations and
clothing/body clearance remain unqualified. Source snapshot bytes can be I12
expanded; they are not the original compressed source resource size. This fix
is native full-quality baking, not a new size-optimization claim.

Final application SHA256:
`322DEA3A90F241D1DED8BD5B0963C5DD5BF618CA03B26CE64D23E46A42C21CEB`.
Final rebuilt BrawlLib SHA256:
`29EDD2D2701C7FFF6356B7A1BB6E4BC82B7F367119A4954A8C0229C15B9C4EFB`.
BrawlLib source/codec is unchanged; its rebuilt binary identity is recorded
separately rather than assuming the previous package hash.

## Commands and remaining boundaries

```powershell
MSBuild.exe tests/RetargetAcceptance/RetargetAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:OutDir=$e/native1/ /m:1 /nr:false /v:minimal
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:OutDir=$e/native1/ /p:BuildProjectReferences=false /m:1 /nr:false /v:minimal
RetargetAcceptance.exe --local-precision <fresh-output>
RetargetAcceptance.exe --adaptive-budget <fresh-output>
RetargetAcceptance.exe --coordinate-frames <fresh-output>
RetargetAcceptance.exe --motion-controls <source-model> <target-model> <fresh-output>
RetargetAcceptance.exe --retarget-case <source-model> <target-model> <motion> <fixture> <fresh-output>
RetargetAcceptance.exe --verify-trajectory <source-model> <target-model> <motion> <clip> <export> <fresh-output>
./scripts/Test-NativeTransferCorpus.ps1 -NativeDirectory $e/native1 -Manifest $e/corpus.json -OutputDirectory $e/run7
./scripts/Test-BackgroundCapture.ps1 -Model <target-model> -Motion <source-motion> -RetargetSource <source-model> -Clip <clip> -WorkspaceAuthenticLibraryFloat -RetargetTrajectoryCase -AuthenticFixtureManifest <fixture> -AuthenticKeyMode <mode> -NativeDirectory $e/native1 -OutputDirectory <fresh-output>
& $e/render-tools/Build-SideComparison.ps1 -QualifiedNative $e/native1
& $e/render-tools/Run-SideComparison.ps1 -QualifiedNative $e/native1 -Manifest $e/side-manifest.json -OutputDirectory $e/side7
& $e/Review-Gif.ps1
```

An initial sandboxed MSBuild attempt could not read the SDK registry; the same
build completed with required registry access. Existing reflection-field
warnings remain. Temporary diagnostic instrumentation is removed from final
source. The local Python imaging attempt lacked Pillow; the existing native
System.Drawing decoder produced the inspected review sheets instead.

No universal model/action, broad contact/style, all costumes, moving supports,
Melee, external-editor/runtime, physical input, mixed-monitor DPI, human or actual
game acceptance is inferred. Original Next, complete viewer, native Undo, exact
project saving and independent optimization remain intact. Keep the ONE G1/M2
incomplete and preserve failed historical evidence.
