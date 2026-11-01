# Native Melee target SRT — bounded acceptance

Version: **preview.68-native-target-srt**, file 2.0.0.43.
Starting commit: `36bbb2ce6a626b2936f6a8f3a43c614917387e97`.
ONE G1/M2 remains incomplete; preview.66 ledges and broader parity stay open.

## Qualified surface

Library **Open as target** adopts the exact normal-detail Melee model and selected
animation into the existing full Model Previewer, retaining its window, native
timeline, selection and Undo. The native HSD backend supplies authentic matrices,
materials and all skin influences. It does not use an approximate MDL0 evaluator
for the displayed native model. Low-only and metal-only objects are excluded from
the authoring/display projection; complete original game resources remain intact.

Existing body transform fields edit an independently owned animation master.
Native common part-to-joint metadata authorizes body descendants and protects all
motion controls/body ancestors. Missing or conflicting context stays unavailable.
This is not name-based or character-specific role inference.
Activating a new S/R/T channel through transform fields or interpolation callbacks
preserves its other native bind components in the same existing Undo transaction.

Project Save/reopen retains the exact editable masters and immutable native
model, selected action member, original authoring baseline, fighter/common data
and executable context. Standard animation export offers a verified standalone
native Figatree DAT member or an explicitly labelled editable CHR0 master. DAT
export preserves untouched native streams and checks freshly decoded local poses,
joint positions and the complete native surface against the current edited master
before committing a new destination. Source files remain read-only.

This is **not full MDL0-equivalent model/tool parity**. Geometry, weight, material
and texture writing, shared Next/IK/fitting/contact/geometry-optimizer rig math,
multi-slot ownership and whole AJ/FTData repacking remain unqualified. Those tools
must not consume a display facade as evidence of authentic HSD scale/skin math.
Native model output rejects unqualified changes instead of exporting a substitute.
A standalone member is not an installed/repacked Melee mod.

## Actual background evidence

Evidence root: ignored outer-workspace `artifacts/Fighterport-melee68`.

- `native-policy26`: 77 procedural checks, including frozen Hermite midpoints,
  distinct incoming/outgoing slopes, terminal values, full turns, existing helper
  membership, native scale/envelopes, all 5/9/17 influences, missing/conflicting
  native body roles, immutable original graphs, cancellation, stale dependencies
  and destination races. Fractional new FOBJ waits reject without truncation.
- `roster-edited`: 156/156 indexed costumes pass an isolated native joint scalar
  edit and fresh complete-graph reopen without mutating source. This is writer
  isolation evidence, not qualification of the native geometry/model editor.
- `policy-roster30`: 133/156 costume rigs have an unambiguous body-role policy;
  23 remain unavailable. No per-character numerical exceptions were introduced.
- Final real-field workflows: Samus Wait, Fox Wait1, Mario Wait1 and Mario Wait2
  pass 57/57/61/61 assertions. They attempt edits on every protected control,
  perform an actual body rotation-field edit, export before Save, Undo/Redo,
  Save/reopen both original and edited projects into the main viewer, re-export
  deterministically and change the source browser without changing the target.
  Wait2 retains original archive member offset 4256 independently of its owned
  zero-offset standalone snapshot. Source discs remain unchanged.
- Complete-package `package32-Mario-Wait2`: 63 assertions pass, adding opaque
  future-format project storage, nonfinite-input rejection, early work-budget
  rejection and in-place native dependency invalidation. The packaged engine/CLI
  and native worker were built freshly with the installed .NET 10 SDK.
- Final `native2-34` sparse-group activation checks pass on Mario Wait2, Samus
  Wait and Fox Wait1: 66/62/62 assertions. Final `native3-35-Mario-Wait2` passes
  68, including native interpolation callbacks and exact complete-transaction
  Undo. All 36 Melee images across those final packages match the inspected
  final29 set byte-for-byte. Final native3 Brawl structure/lifecycle pass 182/37;
  five lifecycle PNGs match the reviewed set and the changed path dialog was
  inspected anew. The equivalent native2 path dialog was also inspected.
- Packaged Brawl native structure/export: 182 assertions. Packaged lifecycle:
  37 assertions, including native body Undo/Redo, New/open, cancelled/failed saves
  and immutable authentic hierarchy/binds/source files.
- `ui19`: 68 supporting assertions cover the full source-preview corpus, actual
  Parts and complete source-issues dialogs, source repair and independent mod
  instances. All 24 PNGs were inspected. All 36 final29 PNGs were inspected in
  three sheets, plus full-size Samus edited/reopened and compact Mario reviews.
  All 11 package Melee PNGs are byte-identical to that inspected set. Six fresh
  packaged Brawl lifecycle captures were also inspected in a review sheet.

Earlier key-edit tests bypassed real transform-field authorization; final29 and
package32 supersede that evidence. Failed runs remain local, including the
synthetic table allocation error, early sparse-bind bridge failure, pre-sharing
Fox work rejection and the second-project-replacement test stalled by its expected
unsaved-change dialog. The final test uses the normal private-desktop Discard
control; no desktop input was injected and no app quality gate was weakened.

Measured decoded edits against the same current target master:

| Fixture | DAT bytes | Times | Raw vertices / exact influence equations | Max rotation ° | Max joint / surface displacement |
|---|---:|---:|---:|---:|---:|
| Mario Wait1 | 6,171 | 801 | 18,984 / 3,334 | 0.00000683019 | 0.00000289129 / 0.00000385224 |
| Fox Wait1 | 7,209 | 1,921 | 19,974 / 3,842 | 0.00001366038 | 0.00000244307 / 0.00000382215 |
| Samus Wait | 5,158 | 1,298 | 30,633 / 5,437 | 0.00001366038 | 0.00000305325 / 0.00000476875 |
| Mario Wait2 | 10,899 | 2,405 | 18,984 / 3,334 | 0.00001366038 | 0.00000302520 / 0.00000388347 |

These are standalone DAT lengths, not CHR0 sizes or a compression benchmark.
Unedited members export byte-exact before and after project Save/reopen. Complete
native geometry participates in verification; the viewport shows normal detail.
Exact duplicate surface equations may share evaluation only within the same
mesh/matrix slot and identical ordered influences. No weights are dropped/rounded.

## Bounds, compatibility and remaining work

Bridge bounds remain rotation 0.0003 degrees, translation/scale
`max(0.000002, abs(reference component) * 0.000001)`, and cumulative joint/surface
displacement `0.0001 * L` for the finite positive native bind-surface diagonal.
These are encoding/bridge bounds, not relaxed solver/contact acceptance limits.
Sampling includes editable knots, native eighth frames, endpoints and additional
0.381966 interval witnesses. Work stays bounded at two million joint/time samples
and sixteen million surface equations. Finite sampling is not a continuous proof.

The reader/writer share HSDRaw. Frozen scalar fixtures provide independent numeric
witnesses; no independent full runtime/editor or actual-game test is claimed.
Nonzero native start-offset authoring, fractional new waits, unsupported terminal
tails and edited AnimJoint output remain explicitly unavailable. Runtime looping,
material/shape/visibility animation and gameplay state are not invented.

Future adapters reuse shared source, target, snapshot, evaluation and export
boundaries. Opaque snapshot storage has no Melee extension whitelist. MDL0/CHR0
are current viewer authoring contracts, not universal game formats or proof of
support for a later adapter. Core still contains no native Melee/Brawl types.

Dependencies: HSDLib `85567e40797de3c820a55476eca54c704921a848` (MIT; original
vendored files/notices unchanged); inspected doldecomp/melee
`598987cf347c125c7416517af18bf5c1dc3f15a1` (runtime facts only, no copied decomp
implementation). Native part numbering/role semantics were checked in ftparts.c.

Broader native model/tool parity, real modpacks/builds, source-retarget support,
contact quality, game acceptance, physical input, monitor DPI and human usability
remain open. Earlier keyboard/scaling timeouts remain recorded, not converted to
passes by this increment. See [implementation, commands and open stages](MELEE_TARGET_PROGRESS.md).

Source-only publication uses DoctorFlux attribution; original Next, complete
BrawlCrate viewer, native Undo, source-owned roots and all existing quality gates
remain. Native builds, game data, DOLs, captures, settings and generated reports
stay excluded. After package validation the local launcher selects
`artifacts/Fighterport-melee68/native3`; its check-only run does not open a window.
