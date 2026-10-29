# Shared retarget tracks and current-animation optimization — preview.57

Starting commit: `04b133fab04d46be8ace01af0e5e56d5c0588cd7`.
This bounded increment is accepted within the one incomplete G1/M2:
**2,470 checks in five admitted background runs, with 22 inspected native UI
PNGs.** The launcher selects `Fighterport-shared57/native2`;
`Start-FighterPort.ps1 -Check` verifies the package without opening a window.
Source publication is the final delivery step, recorded separately locally.

## Track membership

Original Next still evaluates the authentic source and target rigs. Final native
output includes only target bones with an actual source/target mapping. Reviewed
equivalent bones may have different names. A target-only helper such as a waist
extension receives no CHR0 entry; its native bind and parent inheritance remain.
Mapped body-proportion corrections remain valid even when the source bone has no
authored local key. Source-only/orphan tracks cannot create target bindings.

The dense contact sampler still has its temporary complete bind rows. Optional
contact variables are restricted to mapped bones. After adaptive baking, the
output adapter proves each omitted local channel equals authentic native bind
with finite, bit-exact values, positive-zero tangents and matching transform
flags. Unexpected non-bind motion rejects preparation rather than silently losing
it. Both source-key projections consume this already-pruned dense master. This
changes neither MDL0 hierarchy/rest/inverse binds/weights nor the original Next
retarget equations or source-owned root policy.

## Explicit current-animation Apply

The existing optimizer presents Optimize, Export optimized copy, Apply to current
animation and Close. Analyze and export remain independent. Apply is available
only for a changed verified result and restores that result into the same owned
CHR0 member using one existing native Undo command. Snapshot restoration includes
key times/values/tangents, absent tracks, flags, track order, entry timing and
immutable compact-storage plans. Clip identity, metadata, selection, frame,
camera, siblings and historical fitting/retargeting recipes remain. Review
approval is invalidated by content changes normally. Ordinary Save stores the
current document exactly, including an explicitly applied result.

Both actions use the same qualified export backend and stale/cancellation/busy
guards. Error-bounded Apply keeps the existing saved limits; no new tolerance,
automatic optimization, editor, project format or second Undo stack is added.
The native transaction preallocates its retained baseline/cache before mutation,
rolls back a failed restore, and never publishes an incomplete candidate.

Retained originals are session-only, keyed by actual project/model/clip objects,
content/storage, rig and persisted per-clip protection/recipe evidence. Displayed
pins from another selection are not dependencies. Repeated optimization and
qualified export reuse the original snapshot, including after Undo/Redo or clip
navigation. Authored edits or changed protection/rig establish a new baseline;
reopen starts a new session. New projects release old cache ownership.
The existing two-million-key / 256 MiB cache includes generated-pair evidence.
Eviction retains an identity marker: Exact cleanup/reference remains available,
but an Error-bounded request falls back to Exact until an explicit new baseline
exists. It never silently treats a lossy result as its missing original.

## Verification and delivery

All native execution and captures use private desktops, below-normal priority and
the shared offscreen renderer/capture mutex. Final native2 hashes are:

| Component | SHA-256 |
| --- | --- |
| Application | `998797cec7e0567d4bc11343b19b1159f3602e2e9c519d8d469ba3cca10811d7` |
| Acceptance harness | `c7d3ec621c62e2d1509e8e1875ff1b0f521deed9f254c8108681f5dcffe76929` |
| Owned BrawlLib, unchanged | `199cdbce8bbd288cfcae211f8e6c4b471ee6600e116afbebe0992e2dac8f00e9` |

The EngineHost/Core/worker package is byte-identical to preview.56/native2;
there are no engine or codec edits in this increment. The new application and
harness were rebuilt with VS2022 MSBuild, Release/x86, and executed afresh.
Existing CS1690 warnings remain; both builds pass.

| Final case | Passed checks | Scope |
| --- | ---: | --- |
| `sourcekeys-forward2` | 550 | Synthetic anonymous bindings/absent target leaf, Dense/both source-key modes, authentic Sonic AttackAirLw → Mario, exact root curves and native quality/reopen. |
| `sourcekeys-reverse2` | 847 | Same procedural contracts plus authentic 35-frame Mario AttackAirLw → Sonic. |
| `current2` | 468 | Existing standalone export checks plus current Apply, exact Undo/Redo including storage, one command, identity/frame/camera/siblings, approvals, stale/cancel, repeat baseline, eviction, export and project reopen. |
| `native-optimization2` | 568 | Continuous scalar/native geometry/codec/refit/flags/terminal/root/protection regressions. |
| `lifecycle2` | 37 | New, unsaved changes, failed/cancelled Save, native editing and Save/reopen. |

The current Apply fixture uses authentic Mario geometry and a 51-frame native
clip with independently authored constant body channels and one absent-target
track. Its verified Exact candidate measures **44,620 → 37,540 bytes** and
**3,631 → 2,982 scalar keys**. This is a mixed procedural regression, not a
compression claim for an untouched game animation. Plain export and project
Save/reopen retain the applied candidate exactly, including storage. One native
command is added; Undo/Redo restore both original and optimized content/storage.
Repeated Apply is a no-op, and forced production-cache eviction permits Exact
while an Error-bounded request explicitly falls back without rebasing loss.

A separate bounded big-endian header/dictionary reader inspected eight emitted
authentic CHR0 files. Sonic → Mario has 42 dense entries and 40 entries in both
source-key results; **WaistNb is absent in all three layouts**. Reciprocal output
has 45 entries. Actual forward dense/bone/channel files measure
227,168 / 172,920 / 115,016 bytes; reciprocal files measure
188,568 / 188,568 / 115,820 bytes in these recorded Airborne fixtures. These are
retarget-layout results before fitting/optimization, not like-for-like new
compression benchmarks against earlier differently prepared files. Dictionary
inspection establishes entry membership only; the admitted native tests supply
separate curve, pose/skin and exact-motion checks.

All 16 fresh current2 screenshots were opened at original resolution: compact
and wide layouts at 100/175% text, collapsed/Advanced/details and the new four
footer actions. All six fresh lifecycle2 captures were also inspected. Actions,
fields, posed textured models and focus/recovery dialogs remain visible without
overlap. Long reports intentionally scroll. The hash-bound review receipts are
`current2/ui-visual-review.json` and `lifecycle2/ui-visual-review.json`, with hashes
`416fa0d560db19e6d7bb9135324cfb7fb293ac6eee6c36dac8092452fbeba3f2` and
`dd2b70799d36c8dca948cfcb3943b32327ebde77bb4182456151ea9353a6d1f5`.

Command templates actually used (aliases stand for pinned local paths):

```powershell
& $MSBuild BrawlCrate/BrawlCrate.csproj /restore /p:Configuration=Release `
  /p:Platform=x86 /p:BuildProjectReferences=false "/p:OutDir=$Native/" /m:1 /nr:false
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore `
  /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false `
  "/p:OutDir=$Native/" /m:1 /nr:false
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native -Model $MarioModel `
  -Motion $SonicAttack44 -RetargetSource $SonicModel -Clip AttackAirLw `
  -NativeSourceKeys -OutputDirectory $Forward
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native -Model $SonicModel `
  -Motion $MarioAttack35 -RetargetSource $MarioModel -Clip AttackAirLw `
  -NativeSourceKeys -OutputDirectory $Reverse
# Separate fresh-output cases with MarioModel/MarioMotion:
# -WorkspaceAnimationOptimization, -NativeAnimationOptimization, -ProjectLifecycle
```

Local evidence: `artifacts/Fighterport-shared57`, including source hashes,
build/launcher receipts, per-case logs, `accepted-run-summary.json` and the
read-only `retarget-membership-byte-check.json`. Source-only change lists and
exact publication verification are retained in delivery receipts.

Two first runs are excluded, preserved rather than counted as passes.
The first new synthetic root fixture exposed an absent-axis versus explicit-fixed
axis mismatch in the fixture. Existing native trajectory writing materializes
fixed Y/Z axes when its translation group is authored. The fixture now authors
those canonical source axes before the unchanged strict root tuple comparison;
no product root-motion code or tolerance was weakened. The first current-Apply
run caught a real boundary bug: plain export selected the retained original
instead of the applied document. Native2 separates current Candidate from
retained Master; ordinary export uses the former and explicit optimization the
latter. The complete fresh current2 rerun passes, including export/reopen. No
quality gate or expected output was weakened to pass either correction.

Physical input, monitor DPI, human usability, universal contact quality, external
editor/plugin round trips, Melee writing and actual games remain unverified.
Public delivery is source-only to DoctorFlux/Fighterport/main with DoctorFlux
author/committer attribution and contributor verification. Private inputs,
captures, settings and generated binaries remain local.
