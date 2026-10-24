# Reviewed contact authoring, native slot profiles and painting — preview.14

This continues the one incomplete M2 slice. The local app is
`artifacts/next-preview14/native/FighterPortStudioV2.exe`, version
`2.0.0-preview.14-contact-authoring`. Source publication excludes builds, game
inputs, projects, caches, snapshots and local evidence.

## Contact placement and release

**Fighter > Contact Placement / Release...** opens dark native windows. Both
views use the main previewer ModelPanel, native materials/shaders, lighting and
full resource copies. Select an explicitly reviewed direct IK effector. Place
captures a world anchor and orientation at a selected source frame, applies world
position/angle offsets, approaches it, holds the inclusive interval, then blends
back into the current motion. Release removes that effector's later editor
constraint and blends its chain rotations into the exact frozen original clip.
Earlier covered frames and other contacts remain represented.

`reviewed-contact-bake-1` runs offline through the existing isolated engine host.
The geometric seed retains the selected elbow/pole plane; integer native poses
remain fixed while shared Hermite derivatives fit the solved quarter frames.
Bounded derivative projection couples up to four neighboring knots, with a
30,000 pose evaluation budget and at most 16 sweeps. Failure to meet the fixed
bounds rejects the candidate. There are no character/action/name/hash exceptions.
World rotation composition is independently checked against ordered matrices;
normalized double quaternion comparison avoids a false float acos endpoint
failure without changing any existing contact solver's acceptance rules.

Before tuning, acceptance was frozen in current.md at position **0.001 times the
reviewed limb scale** and orientation **0.25 degrees**, evaluated at native
quarter frames. The existing endpoint bound remains 0.0001 times limb scale and
0.001 degrees for the analytic seed. Original Next retargeting and the existing
Core contact solver are unchanged. The new operation changes selected existing
root/middle/effector rotation channels only. S/T, helper joints, other known
native support dependencies and exterior keys remain original. Duration,
timebase and native loop metadata are retained. Original source bytes, deform
hierarchy, bind data and skin weights remain immutable.

Prepare exports and fresh-decodes an independent CHR0, compares all native
keys/tangents/semantic flags, and rechecks contact bounds through a fresh native
import. Explicit result review is required before Add Independent Animation.
Changed native content, control state or provenance rejects stale Prepare/Apply.
The import has one native Undo/Redo. Projects save exact original reference
bytes/hash, options, contact intervals and the receipt, with per-result 8 MiB and
aggregate 32 MiB encoded snapshot budgets. Native scrubbing/playback evaluates
saved curves without workers or solvers. Ordinary pin/gizmo and interpolation
editing remains available; it authors animation keys, not a playback-only pose.

This operation is reviewed **bone-point contact authoring on supported direct
chains**. It does not reconstruct the original Next aggregate support counts
into surface timelines, solve moving supports/body collision/state transitions,
release arbitrary legacy surface constraints, or establish whole-motion/game
quality. Soft/surface/reference/clearance contexts without this adapter reject.
Nonuniform ancestor scale and helper-chain IK remain separate supported-anatomy
gates. This creates body CHR0 output; native companion channels are not copied
from the original container into the independent body animation.

## Source-bound Brawl native motion profiles

**Fighter > Animation Slots / Assign to State... > Load Motion File Profile**
reads the selected registered original Brawl motion container. The profile saves
its authentic SHA256, source registration and every native CHR0 entry path, name,
version, sample count and loop flag. Existing/duplicate slot names are retained
without substitution. New slots start optional and unassigned. Assign / Review,
Required / Optional and Lock / Unlock retain the existing native edit/Undo model.
Native projects save the profile and assignments; native workspace transport
also retains a ProfileKey alongside exact native resource identities. Core
storage verifies that profile key independently of custom/target-owned slots.

**Export Native Slot CHR0...** checks the exact current assigned model/animation
revision and the original template hash and selected native entry. It writes a
new CHR0 with the template slot's native name/version and the assigned clip's
keys, tangents, flags, duration and loop policy, followed by exact fresh native
verification. It never overwrites the original container or existing output.

These are verified **native template inventories**, with game build explicitly
Unverified. Runtime action IDs, required/fallback rules, canonical per-build game
profiles, Fill Missing, PAC repacking, installation and gameplay acceptance are
still unverified. An assigned/renamed CHR0 is not a tested in-game slot.

## Painting lifetime regression

TKContext previously disposed the graphics context but retained its Windows
window/DC wrapper. Cleanup now disposes that wrapper, clears the current/live
context references and handles repeated disposal. Reset registers the replacement
context again. GLPanel detaches its retiring context before owner callbacks can
reenter painting or resizing. No renderer, shader, materials or presentation
route is replaced. These are patches to the existing pinned LGPL BrawlLib/OpenTK
integration, with no new component or package.

Both complete supplied-model directions now exercise the old combined failure
sequence: original Next options/mapping/progress/details, repeated native
bake/reopen and library retarget, contact-aware fitting prepare/import, then
eight further native modal review cycles, followed by pin/curve/native saving.
Each cycle checks two live wrappers, disposal of both wrappers, and restoration
of the exact live context count. Both runs measure contexts **2 -> 2** and GDI
resources **160 -> 168**, within the predeclared drift budget of 8. First/last
model screenshots are retained and inspected; the two cycle captures are byte
identical within each direction. This is bounded regression evidence, not an
unbounded stress guarantee or physical desktop/DPI acceptance.

## Qualification

Fresh qualification passes **655 checks: 373 native and 282 focused engine**.
All **47 final PNGs were actually opened and inspected**. Final native directories
under ignored `artifacts/viewer-parity/`, with prefix `next-preview14-`:

| Directory | Checks | Inspected PNGs |
| --- | ---: | ---: |
| combined-mario-to-snake-r1 | 116 | 15 |
| combined-snake-to-mario-r1 | 116 | 15 |
| authoring-mario-final-r3 | 58 | 7 |
| authoring-snake-final-r3 | 58 | 7 |
| workspace-final | 25 | 3 |

The eight focused engine groups pass contact authoring **15**, contact/slot
contracts **19**, boundary fitting **33**, native/procedural controls **64**,
editing **76**, native frame editing **32**, receipt compatibility **9** and
workspace **34**. Contact authoring uses `next-preview14-engine-contact-authoring-final-r4`;
the other seven use `next-preview14-engine-<scope>-final-r2`. Receipts record
every assertion, private-desktop flags and source hashes. The final native
contact tests follow the world-axis/clip-scope correction; combined painting and
workspace runs precede that isolated new-operation correction. Existing control,
retarget and renderer paths did not change afterward.

Mario's placement receipt records position bound **0.0039407937 native units**,
9,469 projection evaluations and 13 sweeps. Snake records **0.009954113 units**,
9 evaluations and zero projection sweeps; both retain the fixed 0.25 degree
orientation bound. Independent native matrix sampling confirms moved anchors and
3 degree world rotations at all 13 quarter samples of each tested hold. Release
rejoins the exact original hand trajectory after the blend. Both independent
released outputs then pass pin move/rotate/scale, IK/pole, Hermite tangent,
Undo/Redo and fresh CHR0/project persistence tests. The original source containers
and native deform data remain unchanged. Both fresh original Next library
CHR0s are byte-identical to preview.13's accepted outputs.

The reviewed images show native textured Mario/Snake source/result views,
readable dark controls, native slot entries/assignments, live pole/pin gizmos and
interpolation curves. Deliberately scaled/rotated gizmo stress poses can extend
outside the preview framing; those images establish editing/render continuity,
not camera auto-framing or reference raster/DPI parity. The procedural workspace
fixture is gray original geometry, separate from supplied character appearance.
The local `reviewed-images.json` and `qualification.json` seal these exact images,
receipts, logs, source revision and compiled-file hashes.
Local receipts and logs stay under ignored `artifacts/viewer-parity/` and
`artifacts/next-preview14/`; generated captures alone are not visual acceptance.

## Commands and limitations

Build from the repository using the configured .NET 10 SDK and cached packages:

```powershell
scripts/Build.ps1 -DotNet <dotnet10> -NativeDirectory <preview14-native>
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false /p:OutDir=<preview14-native>
# Run engine builds from engine/ so its global.json selects .NET 10:
dotnet build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false
```

Native tests use `scripts/Test-BackgroundCapture.ps1` with `-CombinedStress`
(and `-RetargetSource`), `-ContactAuthoring`, or `-CharacterWorkspace`, plus
`-Model`, `-Motion`, `-NativeDirectory` and a fresh `-OutputDirectory`. The two
combined directions use supplied Mario and Snake models/motions; contact tests
use each model's own Wait1. Captures are serialized, paced and below normal on a
private inactive desktop, with pause/cancel/resume. No active screen copy,
desktop switch, input injection or focus stealing occurs. Native pointer/ring
callbacks and coordinates are exercised; actual physical input and DPI remain
unverified.

Focused engine invocations use `FighterPort.Tests.dll <new-dir>` followed by one
of `--contact-authoring`, `--contact-slots`, `--boundary-fitting`,
`--viewer-controls --native-controls <native-Snake-project>`, `--viewer-editing`,
`--viewer-native`, `--receipt-compatibility`, or
`--workspace-only <native-Snake-project>`. Native cases use the configured audited
Brawl worker. Build logs record the existing AVA3001 warning in the retained
legacy test renderer; the native app/host build has no new warning.

Development runs are excluded: initial native startup timeout; rejected Mario
quarter-frame Euler/tangent candidates; a corrected test-path/argument grouping
mistake; and the noncommuting-world-rotation check that caught the float endpoint
comparison. The mis-grouped engine invocation ran the broader suite and reached
its retained advanced-retarget contact/release assertion failure. No full-suite
pass, advanced 96.2 Apply, generalization, game installation or game test is
claimed. Frozen numerical bounds were not relaxed. Retained advanced motion,
contact, native/display and missing-context gates remain unresolved.

The next single M2 substep is to retain complete original Next native support
timelines/context and extend reviewed placement/releases to coupled surfaces and
moving support frames with independent subframe/conflict evidence. Canonical
per-build runtime profiles/Fill Missing, advanced companion portability, layers,
remaining workspace tools, adapters and games stay queued. M2 and full standalone
integration are incomplete.
