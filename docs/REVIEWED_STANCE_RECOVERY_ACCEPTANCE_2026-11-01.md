# Preview.65.8 — planted holds and reviewed recovery steps

Starting commit: `372769c3ee1ca41740185ac4d3637a432fc06d63`.
**preview.65.8-reviewed-stance-recovery** qualifies this bounded static-floor
increment inside the ONE incomplete G1/M2. Preview.66 static ledges stays queued.
Original Next, the full BrawlCrate Model Previewer, native Undo, original binds,
hierarchy, every skin weight and source-owned motion controls remain intact.

## Behavior and review

The user explicitly selected: keep planted feet fixed where possible and allow
a reviewed step when the ending stance requires relocation. The existing
**Fix foot contacts** dialog now offers one opt-in **Recover fitted stance ·
review steps** checkbox for an exact, non-looping, end-only, frozen-first-frame
fit with sufficient recovery time. The exact fitted-result hash gate remains.

Review the static plane, choose Suggest, inspect every proposed hold/step and
anchor, check all rows, then Preview and Apply. A proposal never implies approval.
The whole schedule is frozen; changing its timings requires adjusting the fit
window and suggesting again. Preview owns independent snapshots. Apply updates
the same current animation with one existing native Undo transaction. Previous
contact recipes, authored pins and protected action intervals cannot be silently
released. No alternate document, renderer or Undo stack is introduced.

The geometry-based proposal lands the higher/moving foot, keeps the supporting
foot at its native starting anchor, and proposes a separate step if the ending
stance differs. Already matching native stances retain planted holds. The exact
original first pose, recovery join, retained beginning/middle, final native pose
and all protected motion-control S/R/T data remain unchanged. The target reference
is its own Wait1 **first frame only** in these fixtures; no idle is appended.

Native Y-height review now carries an explicit plane normal instead of deriving
one from a potentially leaning character basis. Null normals retain legacy
behavior and serialized hashes. Per-foot clearance retains the minimum of the
reviewed plane and the authentic starting/ending sole heights; it does not erase
authored reference penetration or force an asymmetric native idle onto a new
flat pose. Supported non-looping native Euler/Hermite semantics remain required.

## Generic implementation

`ReviewedRecoverySteps` extends the existing reviewed-contact workflow. It uses
reviewed body/leg chains, authentic geometry, frozen anchors and complete ordered
skin influences. No character, action, filename, hash or bone-index exceptions
are used by the numerical algorithm. Foot-shape correction includes reviewed
deforming descendants and their dependency paths; it retains local translation
and scale. Unrelated helpers and protected controls remain unchanged.

The complete reviewed window includes swing. An inferred plant must not be
reinstated at the zero-weight midpoint between two contact rows. Authored pins
remain exact or cause a clear conflict, including where they intersect a proposed
step. Full contact context, phase timing, anchors and dependencies roundtrip in
the existing project envelope with `reviewed-native-stance-steps-1` metadata;
legacy reads and hashes are retained.

Native quarter/adaptive refinement is followed by the complete 1/64-frame
recovery grid and additional fractional witnesses. Newly chosen knots are placed
on native float times **before solving**; authored times are never rounded.
Internal margins are stricter than acceptance. Bounds remain position
`0.001 × reviewed shorter leg length`, orientation `0.25°`, and added floor
penetration `0.0002` native units. The 240-sample window, 30,000-pose,
two-million-skin-work and 12-sweep limits remain.

Exact repeated skin-position equations are evaluated once per pose while every
original region/sole witness is still checked. Keys include position bits and
every ordered influence coefficient. One-bit coefficient changes prevent reuse;
dynamic geometry retains mesh/vertex identity. No weights are capped, normalized
or discarded. The cumulative budget counts actual computed equations without
resetting between refinement and acceptance. Legacy counting remains unchanged.

## Native measurements

All four AttackS3S transfers use the target's own frozen Wait1 frame 1. Native
ApplyCHR/WeightMeshes evaluation is independent of Core pose evaluation. It
includes every positive foot-path influence, all receipt/key witnesses, integer
and quarter frames, **1/128-frame recovery samples**, and withheld `.137/.623`
fractions. The same checks pass for the corrected master, independently decoded
verified export and actual reopened native project.

Drift/lift below describe planned **holds**, not intentional movement during a
reviewed step. Horizontal drift is maximum displacement from the frozen sole
anchor, not accumulated path length. Lift is above the authentic anchor height.

| Direction | Hold slide before → after | Hold lift before → after | Added penetration | Orientation | Native witnesses per stage |
|---|---:|---:|---:|---:|---:|
| Peach → Fox | 9.191784 → 0.004427 | 6.114073 → 0.002348 | 0.000116125 | 0.078926° | 1,547 |
| Fox → Peach | 1.493883 → 0.005813 | 0.607799 → 0.001215 | 0.000125065 | 0.104683° | 1,386 |
| Link → Samus | 7.099613 → 0.008416 | 1.737458 → 0.001007 | 0.000120208 | 0.058618° | 1,413 |
| Samus → Link | 5.366550 → 0.007239 | 0.676987 → 0.000648 | 0.000111863 | 0.106716° | 1,410 |

Position bounds respectively: `0.005961334`, `0.007871706`, `0.010665228`,
`0.008184567`. Complete patch drift also passes these bounds. All quantities
except angles are native units. Finite witnesses are not a continuous-time proof.

### Orientation measurement counterexample

BrawlLib's `GetRotationMatrix()` copies the native 3×3 and retains scale/shear.
Converting it directly to a quaternion produced a false `0.257199°` reading for
Fox → Peach, unchanged by denser curve refinement. The independent oracle now
extracts the agreed orthonormal constraint frame in double precision: normalized
world X, orthogonalized Y, X cross Y. This is read-only measurement; model/clip
transforms are never normalized or rewritten. Frozen synthetic 30°, −75° and
180° frames with known scale/shear verify the orientation independently. A
counterexample explicitly proves the old raw conversion exceeds 0.25° for an
otherwise exact frame. Raw scaled-matrix readings remain in reports alongside
the corrected metric. The acceptance limit is unchanged, not waived.

### Real standalone bytes

Optimization remains independent from the editable corrected master. Existing
absolute contact/floor gates keep these exports **Exact-only**; no approximate
contact compression or PAC/container savings are claimed.

| Direction | Fitted master | Corrected master | Verified Exact export | Project-reopened master |
|---|---:|---:|---:|---:|
| Peach → Fox | 170,724 | 275,148 | 266,568 | 275,148 |
| Fox → Peach | 164,188 | 215,572 | 205,280 | 215,572 |
| Link → Samus | 166,768 | 231,112 | 226,864 | 231,112 |
| Samus → Link | 127,800 | 184,008 | 175,236 | 184,008 |

Contact correction adds native curves. Compression savings compare the **same
finished corrected animation**, never the donor or the earlier uncorrected fit.
All four optimized copies are smaller than their correct reference masters.

## Verification and retained failures

Local evidence: `artifacts/Fighterport-plant658` outside the source checkout.
`native-final3`, `summary.json`, sealed render manifests, `media1`,
`visual-review/inspection.json` and `index.html` retain exact private identities,
hashes, metrics, export files and project evidence. Game assets stay local.

- 175 Core reviewed-contact checks: actual sequential steps, stationary holds,
  unequal proportions, renamed bones, permuted mesh storage, root travel,
  stale masters, authored-pin conflicts, cancellation and complete context reads.
- 31 complete-skin checks, including 5/9/17 ordered influences, one-bit changes,
  dynamic geometry and cumulative skin-equation budgets.
- 59 existing control/root checks and 436 boundary-fitting checks pass.
- Four final native workflows pass 30 checks each. They retain exact owner,
  member, selection, bone selection, frame, camera, original key/paired sides,
  root controls, first/join/end poses, sibling resources, one Undo/Redo,
  unchanged fitting history, normal Save, actual native reopen and verified export.
- 182 native structure/orientation checks and 37 legacy contact-workflow checks
  pass, including the existing failed-Apply and cancellation paths.
- Four fresh side GIFs use the shared offscreen native renderer on private
  desktops. All **318 decoded frames** were inspected in complete storyboards;
  recovery intervals were also inspected in larger sheets. Four compact/wide
  100%/175% UI captures were inspected. Costumes can obscure soles, particularly
  Peach's skirt; independent full-skin measurements remain essential.

The broader keyboard suite is **not** a pass: candidate and unchanged qualified
preview.65.7 both time out at first source registration after the same 152 early
routing checks. Identical final checkpoint: `F6 skips a disabled region`.
`keyboard1` and `keyboard-baseline1` retain both failures and four layouts each.
Keep that existing registration/harness investigation open; it is not contact
acceptance or physical keyboard evidence.

Earlier failed probes remain: inferred leaning-plane mismatch, conflicting
simultaneous anchors, missing foot-descendant locks during fractional projection,
full-skin budget exhaustion, reinstated inferred plants at swing midpoint,
nonrepresentable newly generated knots, between-sample margins, raw scaled-matrix
orientation and the short gesture-harness timeout. No failed candidate qualifies.
The new offline harness waits up to the existing 120-second client boundary;
solver/numeric/work limits are unchanged.

The older corpus called descriptor-only `FighterProject.Save` without flushing
live native buffers. It was not accepted as the edited master. Tests reran the
actual fitting UI and normal `SaveFighterProject`, then reused those properly
saved masters only with the exact result-hash guard. No original recipe hash was
rewritten to admit a stale fixture. That guard remains in the application.

Commands actually run included `scripts/Build.ps1`, `scripts/Build-Engine.ps1`,
Release/x86 `ControlsAcceptance.csproj`, and the pinned .NET 10.0.402 test runner:
`--reviewed-contact-sets`, `--complete-skin`, `--viewer-controls`,
`--boundary-fitting`. Native scripts used `-ReviewedStanceProject`,
`-StanceCurrentFitted`, `-FittingReferenceFixture`, `-StanceLayouts`,
`-NativeExportStructure`, `-ReviewedContacts`, and both candidate/baseline
`-WorkspaceKeyboard`. Sealed shared renderer build/run/review scripts generated
and decoded the media. No OS input injection, active-desktop capture or game run.

## Package and boundaries

Qualified local package/launcher: `Fighterport-plant658/native1`.
Application SHA256:
`F83A871C543E0755EDAED3B845F2EC0ADDFC0E50573A7910D51262D83A8A8B1B`.
Core SHA256:
`BC403B3F5052AB77F03D30751234EE5EB3ECFE62244CC23051F53659ADF475A6`.
BrawlLib SHA256:
`17D348D9721A368FB548A7B181158FC8B0BB3BE379D898C36C883E42C9AF726B`.
No BrawlLib or original Next retargeter source changed. Rebuilt BrawlLib includes
product-version revision `42.0.1+372769c3ee1ca41740185ac4d3637a432fc06d63`;
the earlier package recorded `+65240305420e7bb825874b1a9ed2172585db55a6`.
The rebuilt binary is pinned separately, not described as byte-identical.

This qualifies reviewed static biped stance recovery on these exact rigs and
procedural variants. Missing bindings/context, authored-pin conflicts,
unreachable anchors, unsupported scales/semantics, insufficient timing and budget
exhaustion still fail closed. General anatomical/style quality, further costumes,
continuous contact/velocity guarantees, moving supports, ledges, other adapters,
physical input, monitor DPI, human usability and actual game acceptance remain
open. Publish source only with DoctorFlux attribution and contributor verification.
