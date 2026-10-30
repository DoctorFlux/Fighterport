# Preview.65.1 — bounded retarget trajectory hotfix

Starting commit: `cf2e7a845bdd91ea4e30e77e9442d8fb6421530b` in the active
Fighterport checkout. This is a bounded correction within the ONE incomplete
G1/M2 milestone; preview.66 static ledges remains queued. No character, action,
filename, hash or bone-index exception was added to the algorithm.

## Reproduction and cause

The reported Sonic AttackS3S → Mario failure is reproduced exactly with the
qualified preview.65 application: 257 unresolved intervals, 1,999,798 work,
7,728 samples, first unresolved bone 10 rotation at
`0.5024996..0.5025005`, depth 20, error `0.0868763853780608°`.
Independent probe observations separate source FK, proportion correction and
contact correction. Native Euler reconstruction is within roughly six millionths
of a degree; it is not the cause. At a support activation boundary, independent
contact solves jump between redundant-joint optima at arbitrarily close times.
More subdivision/work cannot represent an actual jump with a continuous span.

An obsolete integer contact-tangent proposal also consumed most preparation time.
The adaptive master overwrites those body channels completely; source-owned
motion controls are replaced independently before the adaptive bake. An internal
test-only A/B entry point retains the old proposal for verification. With it:
49,234 ms; without it: 1,710 ms. **Complete serialized outputs are byte-identical**,
350,596 bytes, and both freshly reopen with exact curves/flags. These are local
descriptive timings, not a general latency guarantee.

## Implemented contract

Original Next still solves every original integer/eighth-frame pose with its
existing contact and proportion mathematics. The offline master retains these
poses as exact reference anchors. When static floor correction actually uses the
body solver and no suspended support is active, a coherent correction trajectory
transports the generated local correction over continuously evaluated source FK.
It interpolates correction quaternions/translations, **not source Euler curves**.
Source-owned motion controls, unmapped locals and all source scales are excluded.
The new inter-anchor reference is explicitly named
`original-next-eighth-correction-trajectory-1` in the preparation report.
Independent noisy raw subframe contact optima are no longer this trajectory's
reference. This is authoring a finished target, not export approximation.

Exact cached anchor reuse avoids repeated solving. Ancestor world-orientation
fit targets reserve descendant headroom so a constant child does not repeatedly
subdivide an inherited parent-error plateau. The public bounds remain 0.025°
world orientation, 0.0002 local translation and 0.00001 scale; depth 20 and
two-million work/key ceilings remain. A terminal unrepresentable interval now
refuses immediately; partial tracks never import. Genuine discontinuities still
refuse. No global codec, contact threshold, solver iteration count, native bind,
hierarchy, scale compensation, skin weight or motion-control policy was changed.

No UI options or project-format changes were added. The full Model Previewer,
original Next, current clip ownership, native Undo and exact Save/export remain.
Nothing solves or hashes on playback ticks.

## Fresh evidence

Local ignored evidence root: `artifacts/Fighterport-retarget65-fix` (workspace
root, outside the source checkout). Originals, failed trials and the qualified
preview.65 package remain untouched. The soft-support activation experiment in
`rejected-soft-activation.patch` failed a later fractional sample and is not shipped.

Final application: `2.0.0-preview.65.1-retarget-trajectory`, SHA-256
`C4EB7C323A615C33CAB3A382A02B54813946B8733CC6B3448EBA33FE05B1F285`.
Native build uses MSBuild 17, Release/x86, BuildProjectReferences=false, /m:1
and /nr:false. App: two existing CS1690 warnings; both harnesses: zero warnings.
Owned engine and BrawlLib binaries remain from the qualified preview.65 package.

Commands actually run (paths abbreviated; original hashes/native member pins
are in the local inventory and fixture manifest):

```powershell
RetargetAcceptance.exe --bake-probe <Sonic model> <Mario model> <Sonic motion> AttackS3S <fresh directory>
RetargetAcceptance.exe --fractional-probe <source> <target> <motion> AttackS3S <json> <explicit times>
RetargetAcceptance.exe --compare-contact-proposal <source> <target> <motion> AttackS3S <fresh directory>
RetargetAcceptance.exe --local-precision <fresh directory>
RetargetAcceptance.exe --motion-controls <Mario model> <Snake model> <fresh directory>
RetargetAcceptance.exe --coordinate-frames <fresh directory>
Test-BackgroundCapture.ps1 -WorkspaceAuthenticLibraryFloat -RetargetTrajectoryCase <pinned fixture arguments>
RetargetAcceptance.exe --verify-trajectory <source> <target> <motion> AttackS3S <actual Use export> <fresh directory>
```

- `final-precision18`: **1,050 checks**, including analytic correction/anchor
  expectations, a narrow source-FK pulse between anchors, renamed rigs, negative
  animated/nonuniform scale, cancellation, inherited-error headroom and fast
  refusal. Existing unequal-proportion/coordinate/storage-permutation tests run.
- `final-roots18`: **628 checks** for authentic and synthetic source-owned
  controls, authored turns, sparse/discontinuous controls and native persistence.
- `final-coordinates18`: **94 checks** for unfamiliar coordinate/rig relationships.
- `final-use18`: **52 checks** through actual `UseLibraryOnTarget` with both
  existing source-key alternatives, verified source preview, exact association,
  immutable source/model hashes, all weights, shared-only tracks, explicit review,
  native export, project Save/reopen and document/selection/frame/Undo ownership.
  It bypasses only the MessageBox error wrapper; no input injection is used.
- `ground21`: **47 checks** requalify the existing Snake DownStandU -> Mario
  own-Wait1 recovery, explicit earlier-body review, contact correction, exact
  original first/final poses and roots, current Apply, Undo/Redo and post-decode
  absolute contact checks. This does not rerun the whole ground matrix.

| Sonic AttackS3S → Mario representation | Bytes | Actual Use elapsed |
|---|---:|---:|
| Full-quality target reference | 350,596 | dense bake 1,667 ms (diagnostic run) |
| Source bone keys | 278,544 | 26,261 ms |
| Source channel keys | 247,148 | 26,750 ms |

Source-key preparation still includes existing cumulative native motion/skin
validation and may take tens of seconds. This hotfix is not a file-size claim.
Both source-key reports have zero measured reduction error against the same
finished target at 3,385 final native samples; protected controls stay exact.

`final-verify20-*` independently reopens both actual exported results. It checks
345 original solved eighth-frame anchors plus 129 withheld fractions
(`.137/.371/.623` per interval), with separate native ApplyCHR/skin evaluation:

| Measurement | Maximum | Unchanged bound / interpretation |
|---|---:|---|
| World orientation at original anchors | 0.020659028° | 0.025° |
| Local translation at anchors | 0.000191280 | 0.0002 native units |
| Local scale vector error at anchors | 0.0000002065 | 0.00001 |
| Root local error | 0 | Own source controls |
| Native bone/adapter position agreement | 0.000006149 | Existing 0.01 native agreement gate |
| Native support surface/adapter agreement | 0.000001297 | Existing 0.01 native agreement gate |
| Fully supported surface penetration | 0.002686590 | 0.003940794 (0.001 × target leg-chain scale) |
| Native fully supported surface penetration | 0.002686314 | Same unchanged bound |
| Stationary planar lock drift | 0 | Same bound |

There are 345 full-support observations. All native skin coefficients remain;
the independent native path does not cap influence count. Anchor world-position
difference is reported separately: 0.001280842 native units. These are sampled
results, **not a continuous-time contact proof or all-body clearance acceptance**.

Additional fresh Auto bakes/reopens succeed for Sonic down-air → Mario (841 ms),
the supplied Mario up-air → Sonic (1,335 ms), and Snake get-up → Mario (5,725 ms).
The requested `AttackS3S1` member is absent in the supplied Snake motion file;
it is recorded as unavailable, not substituted. Mario AttackS3S → Snake still
refuses its direct solved reference at bone 17, frame 0.820903, error 0.122886°.
Snake's inventoried AttackS3S also still refuses at bone 11, frame 0.014007,
error 0.982164 degrees. Their limits stay unchanged and no output is admitted.
The older seven upstream ground failures are not collectively accepted by this hotfix.

## Visual and remaining acceptance

Private-desktop/offscreen tests never use active-desktop input/capture. The
shared renderer verifies **326 assertions**. All 44 decoded frames of the
orthographic +X master/result GIF were inspected on six contact sheets, plus
native frames 1/5/35/44 at full size. Four final UI result captures, six native
mapping captures and four compact/wide contact captures at 100%/175% text
were inspected (14 UI images).
The two target representations visually agree; the floor guide/root travel
remain visible. Unsupported rolling/body/hand clearance is not accepted from
this comparison. No idle or fitting was added to this tilt demonstration.

Physical input, monitor DPI, human usability, external-editor/game/Melee
acceptance, universal contact recovery and queued ledges remain unverified.
The launcher now selects `Fighterport-retarget65-fix/candidate-native`; the parent
launcher `-Check` resolves that exact frozen executable without opening UI. Publish
source only to public DoctorFlux/Fighterport/main with DoctorFlux attribution;
keep private fixtures, generated exports, captures/settings and binaries ignored.

Publication verified: implementation `62dfe99d73c329da72c5481502a9c80db1ebd0cc`
is on public `DoctorFlux/Fighterport/main`. GitHub resolves both author and
committer to DoctorFlux; contributor response lists only DoctorFlux. The parent
launcher check resolves the frozen hotfix and the source checkout is clean.
