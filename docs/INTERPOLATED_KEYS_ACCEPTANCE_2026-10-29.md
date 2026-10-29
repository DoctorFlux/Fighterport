# Interpolated animation keys — preview.58

Starting commit: `b288f1a8dbf8da35ba1a21db6b8d4aab24baef93`.
This bounded increment is accepted within the one incomplete G1/M2:
**3,170 checks in seven admitted runs and 48 actually inspected native UI PNGs**.
The launcher selects `Fighterport-interpolation58/native1`; source publication is
the final delivery step, recorded in the local receipt. No new increment is promoted.

## Dense donor keys are not mandatory output keys

**Retarget Options → Keyframes → Interpolated keys** is an additive fourth choice
in the existing dropdown. Original Next Dense remains zero/default. Both source
schedule modes keep their existing semantics. The new mode starts ordinary
target curves from their endpoints, without requiring donor key times, including
when the donor keys every frame. Existing native Hermite value/derivative checks
reinsert target knots when needed. It does not decimate every nth key, replace
native Euler interpolation with quaternions, wrap full turns or resample at a
different FPS. Playback uses already prepared native curves without solving.

New curves are compared with the finished dense target from original Next,
not the donor's different proportions. Source-owned controls retain their exact
native tuples. Unmatched target bones remain absent under preview.57's reviewed
mapping policy. Unknown paired sides, signed-zero evidence and discontinuities
retain their complete authored curves; singular/sign-changing scale remains
exact. Missing geometry, uncertain proof, size increase or exhausted verification
retains the verified dense result. Smaller key counts alone do not prove smaller
files or actual game compatibility.

The retarget representation keeps its existing strict ceilings: 0.01° raw Euler
and local orientation, 0.00001 scale, translation min(0.0002 native units,
0.0001 × bind diagonal L), and 0.0001 × L world/basis/skin distance. Scalar
derivative ceilings remain 0.05°/frame and the corresponding scale/translation
bounds/frame. These are separate from the standalone optimizer's saved Exact /
Error-bounded policies; no existing limit is relaxed.

Native preparation encodes/reopens, compares complete coefficients/metadata and
actual bytes, and checks cumulative hierarchy/all skinned vertices under the
existing shared budgets. Review remains explicit. The additive
`interpolated-key-quality-v1` receipt is coupled to Interpolated mode; legacy
source-key v1/v2 receipts cannot be relabelled as interpolation evidence. Existing
project fields store the chosen representation and its historical dense
comparison. Dense contact statistics remain historical, not new contact claims.
No project-format migration, new solver, renderer or document owner is added.

## Optimizer improvement

The existing optimizer already uses native Hermite equivalence/reduction.
Preview.58 improves its bounded approximate span search: failed-span
value/derivative witnesses can propose non-dyadic native endpoints that the old
farthest/powers-of-two search missed. Samples choose proposals only. Complete
rational Bernstein proofs against the immutable original still accept or reject
every changed polynomial; final native serialization and geometry gates remain.
See [implementation notes](INTERPOLATION_SEARCH_NOTES_2026-10-29.md).

There is no extra optimizer mode or automatic document mutation. Exact permits
only proved equivalence; Error-bounded retains its saved limits. Apply to current
animation and independent export share the same implementation and session
baseline. Protected motion, contacts, joins and middle sections override reduction.
Work/cancellation caps and verified fallback remain; search is not globally optimal.

## Numerical evidence available

Evidence root: ignored `artifacts/Fighterport-interpolation58`.

| Case | Actual result |
| --- | --- |
| Core `core-guided1` | PASS 235 checks: 217 previous plus 18 new guided-span cases. |
| Native optimizer `native-optimization1` | PASS 568 checks against the new Core/native application. |
| Authentic Sonic AttackAirLw → Mario `source-forward2` | PASS 661 checks, including the new dense-donor interpolation fixture and all three non-Dense layouts. |
| Authentic 35-frame Mario AttackAirLw → Sonic `source-reverse2` | PASS 971 checks with the reciprocal native geometry and both legacy layouts. |
| Native UI `workspace5` | PASS 230 checks: all three non-Dense choices, mapping review, cancelled/stale work, native-only playback, export, project reopen, body edits and exact Undo/Redo. |
| Current optimizer `current1` | PASS 468 checks: shared optimizer/export and explicit current Apply, retained original baseline, one-command Undo/Redo, failed/stale work, eviction and Save/reopen. |
| Lifecycle `lifecycle1` | PASS 37 checks: native edits/New, unsaved-change handling, failed/cancelled Save and reopen. |

The independent fixture deliberately gives the **source itself 17 keys**, one on
every frame, for a cubic `t³/4096` and a linear 720° turn. Interpolated retargeting
uses two endpoint keys for each varying body channel and one key for its constant
channel, while Source channel keys retains the 17 source schedule knots. The real
Core Exact optimizer separately produces the same native reduction. Full
animation storage measures **1,088 → 516 bytes**, **70 → 24 scalar keys**, with
protected root tuples unchanged. The native validator records 503 witnesses;
4,097 withheld scalar fractions independently check the cubic/linear functions
without Euler wrapping. This is a synthetic algorithm/codec fixture using real
native target geometry, not an untouched game-animation compression ratio.

The guided optimizer counterexample retains four native rows at `0,5,5,16`
instead of the preceding search's six, under the same tightened 0.005° value /
0.1°-per-frame derivative bounds. Distinct paired sides at the corner remain.
Other corner positions and shifted fractional timing pass. Sampled hints are
not continuous proof; exact rational scalar bounds and sampled composed native
geometry remain separately reported.

In the authentic forward Airborne fixture, Interpolated produces 9,508 decoded
keys / 115,016 bytes from a 20,319-key / 227,168-byte finished dense reference.
This equals the source-channel result for this motion; no additional saving over
that existing representation is claimed. Existing native pairs can legitimately
retain dense knots. Reciprocal Interpolated output measures 9,613 keys /
115,808 bytes versus the 17,909-key / 188,568-byte dense reference, only 12 bytes
below its channel-key result. In the separately reviewed native UI preparation,
the forward result measures 21,190 → 10,379 keys and 237,620 → 125,468 bytes,
also equal to that preparation's channel-key result. These distinct prepared
masters are not interchangeable size baselines. No fixed ratio is promised.

The native app and owned EngineHost/Core were rebuilt for this increment. Owned
BrawlLib bytes remain unchanged. The first synthetic run exposed absent root
translation axes becoming fixed native slots on serialization. Only the fixture
was corrected to author its canonical Y/Z axes; strict baseline verification,
source/root tuples and quality gates stayed unchanged. A UI test also required
the existing source-metadata identity API for the authentic donor's nonzero
scaling rule; generated-target writer checks remain strict. Failed/unattended
runs are retained as excluded evidence rather than counted as passes. The UI
harness also needed its known fixture's normal skeleton-mapping review before
work could begin, explicit Airborne context (no floor/support acceptance), plain
layout export instead of intentionally optimized native-resource export, and a
shared-dialog title check consistent with its explicit interpolation heading.
No motion threshold, root tuple check, decode check or product default was
weakened. `workspace2` was cancelled before Bake at the unattended mapping review;
it is not evidence of contact-solver performance or a completed quality run.

## Build, capture and delivery record

Native1 application SHA-256:
`e123cead4a2df481be3527048d3941d31cef0f08ca257bebea21c96361d74f25`.
EngineHost/Core hashes are respectively
`06dffae16102a51b1820350009acdae2139df3206393aee3b7c583431baf80e1` /
`5870067d3cb7853ddd44b85ef47a70c0182f72fc806b506e53b5b9aadc059b8d`.
Owned BrawlLib remains
`199cdbce8bbd288cfcae211f8e6c4b471ee6600e116afbebe0992e2dac8f00e9`.
The final harness hash is
`b8a70542ae31c6cc67889bd97614d1e577db603c917e377b4395fdf12d1c2d1d`.
Application and all backend bytes stayed unchanged through the test-only fixture
and harness corrections. Native optimizer/numerical executions used the earlier
harness revisions recorded with each build; their test methods and production
paths are unchanged. Final UI/current/lifecycle cases use the final harness.
Original executable hashes and excluded logs remain local; no earlier product
acceptance is substituted for these fresh runs.

VS2022 MSBuild rebuilt the native app/harness Release/x86; the local .NET 10.0.402
SDK rebuilt/published the owned EngineHost, Core, CLI and worker. Native builds
retain the existing CS1690 warnings; Core's existing AVA3001/cache-audit warnings
are recorded separately. All final builds pass. Actual command templates:

```powershell
& $MSBuild BrawlCrate/BrawlCrate.csproj /restore /p:Configuration=Release `
  /p:Platform=x86 /p:BuildProjectReferences=false "/p:OutDir=$Native/" /m:1 /nr:false
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore `
  /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false `
  "/p:OutDir=$Native/" /m:1 /nr:false
& scripts/Build-Engine.ps1 -DotNet $Dotnet -NativeDirectory $Native
# Core runner: --animation-optimization, recorded in implementation notes.
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native -Model $MarioModel `
  -Motion $SonicAttack44 -RetargetSource $SonicModel -Clip AttackAirLw `
  -NativeSourceKeys -OutputDirectory $Forward
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native -Model $SonicModel `
  -Motion $MarioAttack35 -RetargetSource $MarioModel -Clip AttackAirLw `
  -NativeSourceKeys -OutputDirectory $Reverse
# Fresh separate outputs: -WorkspaceSourceKeys, -NativeAnimationOptimization,
# -WorkspaceAnimationOptimization, -ProjectLifecycle.
```

All native work is private-desktop, below-normal priority and serialized through
the shared offscreen/capture boundary, without OS input injection or active-screen
capture. Actual original-resolution image review covers 26 workspace PNGs (two
mapping dialogs and three layouts × eight 100/125/150/175% compact/wide states),
16 optimizer PNGs and six lifecycle PNGs. Controls/reports/actions remain contained;
paired posed/textured previews agree at the review resolution. Long reports
intentionally scroll. Interpolated captures show frame 8 after playback; the old
layouts show frame 3. Both panes agree at each selected frame.

Hash-bound `ui-visual-review.json` receipts under workspace5/current1/lifecycle1
have SHA-256 values
`b835046622479b05146b88cc4d959bf28837f8c24f9cf48015e041e1fe406527`,
`65a1c02aaffea1999605e30d6ef13348c6175179e16a3885948b6f018129561e`,
and `1fa14614832c255c1cbddfc6f6f740ea915db97ccd575c06f812b55e11ffa47a`.
Root motion and all skin coefficients remain data-identical; curve/local/world
and skin errors remain subject to their separate numerical gates. Screenshots
do not establish continuous contact correctness. Build, source hashes, scope,
run summaries, measured CHR0 files and publication receipts remain local under
`artifacts/Fighterport-interpolation58`.

Physical input, monitor DPI, human usability, universal contact quality,
unqualified runtime scale semantics, stock external editor/plugin round trips,
Melee writing and actual game execution remain unverified. Public delivery is
source-only to DoctorFlux/Fighterport/main with DoctorFlux attribution; original
assets, paths/settings, captures and binaries remain local.
