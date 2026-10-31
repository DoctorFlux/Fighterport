# Preview.65.3 — constrained contact orientation

Starting commit: `d705a7801236d12294268d214c07b20709ee8bc0`.
Active checkout: `.local/Fighterport`; source publication destination remains
public `DoctorFlux/Fighterport/main`. This is a bounded continuation within the
ONE incomplete G1/M2; preview.66 static ledges remains queued.

## Cause and correction

The .65.2 native baseline reproduces **0.793463626°** of supported right-foot
orientation difference at native time **27.623** in Sonic AttackS3S → Mario.
Nearby independent coupled solves switch orientation while reducing skin-height
error. The final two-bone projection preserved the orientation *after* that
drift, rather than the intended orientation prepared before the coupled solve.
The original solved eighth-frame anchors were already within their native bound;
fitting more native keys to the noisy independent optima was not the remedy.

`ContactMotion.FitBodyContacts` now freezes intended end world orientations at
entry, after the existing source roll/flatness preparation. These are hard
constraints throughout trial FK, line search, skin-height projection and body
coordinate search. End rotations are removed from free variables. The full-skin
Jacobian and positional prior account for the compensating end rotation: a
weight below a constrained end inherits the moving end pivot, not an additional
rotation of its whole offset. Every influence remains included.

The original Next mapping, FK/IK goals, floor thresholds, contact position bound
`0.001 × reviewed limb scale`, orientation bound **0.25°**, 180 coupled iterations
and native curve/work limits remain. No model/action/name/index exceptions,
replacement skeletons, changed binds, weight truncation or new UI were added.
Protected source-owned local motion remains exact.

Offline support preparation also checks the intended end orientation, reserving
**0.125°** before native representation error. It derives this goal through the
same existing analytical orientation preparation without repeating a coupled
solve at every witness. Orientation-only curvature can therefore request more
original-solver anchors even when position alone would not. Original anchors
stay exact reference objects. Existing atomic proposal failure/cancellation and
explicit unqualified-contact fallback remain; no partial proposal is published.
Neither solving nor diagnostic recomputation runs during playback.

Policy `coupled-end-world-orientation-1` is included in the existing complete,
hashed original support evidence. The envelope version and legacy reader remain
unchanged; the original source poses, geometry, contact tracks and dependencies
remain present. Old evidence is not rewritten. Project state/Undo ownership,
the complete native viewer, ordinary exact Save/export and codecs are unchanged.

## Fresh verification

Ignored local evidence: `artifacts/Fighterport-orientation653`. Baseline failure,
diagnostic witnesses and trial builds are retained alongside the final package.

- `precision6`: **1,216 checks**, including eight opaque/renamed, permuted,
  unequal-size, rotated-coordinate procedural combinations. Independent
  central FK differences check all five influences of constrained skin patches.
  Simultaneous end constraints meet existing position bounds while preserving
  root transforms and scale. Independent orientation-only curves exercise
  adaptive preparation at 1,025 held-out times per combination. Existing
  sparse/paired/native curves, poles, 720° root turns, negative/nonuniform scale,
  budget/cancellation and source-snapshot tests remain passing.
- `roots6`: **628 checks**; `coordinates6`: **94 checks**.
- `use6-*`: **222 checks**, six actual library Use paths (both source-key options
  in each of three directions), explicit review/import, target-owned Wait1
  first-frame ending, native Undo/Redo, export/redecode and project Save/reopen.
  Sources, model hashes/all weights, exact beginning/protected middle,
  attack-owned translation, native selection/playhead and document ownership pass.
- Nine independent fresh native oracles cover three dense exports and all six
  actual source-key exports. They compare original solved anchors, native bone
  matrices and complete skin support at integer/eighth and withheld
  `.137/.371/.623` times. A separate intended-goal consistency check also passes.
- Held-out Sonic down-air → Mario, supplied Mario up-air → Sonic and Mario Wait1
  → Snake bake/export/reopen successfully. These runs are regression evidence,
  not new airborne, ledge or broad contact acceptance.

The first three groups total **2,160 checks**. Commands actually run:

```text
MSBuild 17: Release / Platform=x86 / BuildProjectReferences=false /m:1 /nr:false
RetargetAcceptance --local-precision
RetargetAcceptance --motion-controls
RetargetAcceptance --coordinate-frames
RetargetAcceptance --fractional-probe
RetargetAcceptance --bake-probe
RetargetAcceptance --verify-trajectory
Test-BackgroundCapture.ps1 -WorkspaceAuthenticLibraryFloat -RetargetTrajectoryCase -ReviewedRecoveryCase
Test-ReviewedRecoveryMatrix.ps1 -GroundContacts -OnlyCases SnakeToMario-DownStandU
Build-SideComparison.ps1 / Run-SideComparison.ps1 / Review-SideComparison.ps1
```

The existing reviewed earlier-body Snake DownStandU → Mario recovery also passes
**52 fresh checks** (`artifacts/oc653g9`), including current Apply/Undo/Redo,
decoded optimized-copy absolute contact replay, complete metadata, exact export
and project Save/reopen. Five additional recovery/compact-wide contact UI images
were inspected at 100%/175% text; its two mapping PNGs match prior inspected
images exactly. This preserves that existing bounded recovery acceptance; it
does not accept the whole ground corpus.

An initial, much deeper evidence-directory run passed its numerical/corrected/
decoded contact gates but failed project asset `File.Move` in the unchanged
legacy `FighterProject.CopyNewFile` save path. The identical regression passes
with a shorter evidence path. Failed evidence remains under `reviewed-ground8`;
long-path project publication is a retained limitation, not a passed save claim.

Actual-use fixtures pin exact original hashes and native members. Sonic's
44-frame, Snake's 32-frame first-part and Mario's 33-frame AttackS3S are used;
no missing AttackS3S1 is substituted. Ending fitting uses ten samples and only
the target's own frozen Wait1 frame 1. Original beginnings remain, with no idle
appended and no replacement of the attack's TransN.

| Native retarget oracle | Sonic → Mario | Snake → Mario | Mario → Snake |
|---|---:|---:|---:|
| Sample times | 474 | 342 | 353 |
| Maximum contact orientation vs raw Next, degrees | 0.039494049 | 0.026575808 | 0.034362754 |
| Contact orientation bound, degrees | 0.25 | 0.25 | 0.25 |
| Maximum foot penetration, native units | 0.000432387 | 0.000687503 | 0.001215512 |
| Reviewed contact position bound | 0.003940793 | 0.003940793 | 0.009954112 |
| Protected root-local error | 0 | 0 | 0 |

Both actual source-key alternatives pass these same oracles. The final Sonic
intended-goal error is **0.039497670°**. Position/rotation bounds were frozen
before implementation; neither was loosened. Complete support observations are
sampled and do not prove continuous-time correctness or every body surface.

| Direction | Source-key option | Retargeted CHR0 bytes | Fitted bytes | Actual Use milliseconds |
|---|---|---:|---:|---:|
| Sonic → Mario | Bone | 305,640 | 272,036 | 21,126 |
| Sonic → Mario | Channel | 274,340 | 248,408 | 22,725 |
| Snake → Mario | Bone | 170,596 | 140,120 | 12,574 |
| Snake → Mario | Channel | 165,508 | 136,872 | 13,863 |
| Mario → Snake | Bone | 183,496 | 146,580 | 15,912 |
| Mario → Snake | Channel | 156,468 | 128,884 | 15,957 |

These are measured physical standalone exports of finished editable motions.
Stricter orientation preparation adds some authoring keys; it is not compression.
Compared with .65.2, Snake/Mario retargeted files add 1,248/660 bytes respectively;
their fitted physical lengths are unchanged. No size savings or donor-size
equivalence is claimed. Explicit export optimization remains separate and must
compare against this finished corrected master under existing protections.

## Media, package and remaining boundaries

Fresh `media7-*` runs pass **1,141 renderer assertions** on serialized private
desktops using the shared native offscreen renderer, without OS input injection
or active-desktop capture. All **218 decoded GIF frames / 28 sheets** and three
full comparison endpoints were inspected. Six new Sonic UI images were inspected;
twelve fresh Mario/Snake UI images have exact SHA-256 continuity with the actual
images inspected for .65.2. Local image hashes and continuity evidence are retained.

The GIFs show level +X orthographic side views, reviewed gold floor guides,
retargeted/fitted panels and only the frozen target Wait1 endpoint. No camera
motion, root-travel removal or appended idle is used. Sonic's early low/rolling
body pose still intersects the floor around the head/body. This is a retained
body-clearance limitation, not a successful all-body contact result. The current
increment qualifies the supported-end orientation defect only; broader clearance,
contacts, style and universal animation recovery remain open.

Frozen package `Fighterport-orientation653/native1`, version
`2.0.0-preview.65.3-contact-orientation`:

- Application SHA-256: `35283D9CB8D2789BEDD74CF5A811904C8B1D811A74EE39E438377AC269FDC20F`.
- BrawlLib SHA-256: `D930DBF9DE21F1CEF7F2606A181F4BB66EA830D529F940B52B116A0E8038F458`.

BrawlLib/engine/worker retain the qualified .65.2 bytes. The application and
acceptance harnesses were rebuilt. Two existing application CS1690 warnings
remain; acceptance builds pass without warnings. The renderer has existing
JSON-populated-field warnings. No generated binaries/assets are for publication.

This generic mechanism uses reviewed semantic bindings and complete geometry,
not character/action exceptions. Finite fixtures do not prove every possible
model/anatomy or animation. Missing/contradictory context, moving supports and
finite ledges remain outside this hotfix. Full G1/M2, the 18-case corpus, physical
input, monitor DPI, human usability, external editors, Melee and game acceptance
remain open. Historical failed evidence and the prior qualified package remain.
