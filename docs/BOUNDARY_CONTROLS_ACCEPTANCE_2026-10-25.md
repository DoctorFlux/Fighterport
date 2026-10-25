# Boundary controls and equivalent rotations — 25 October 2026

Preview.31 (`2.0.0-preview.31-boundary-controls`) extends preview.30 within the
ONE incomplete G1/M2 slice. Bounded background qualification passes **2,883
assertions in 15 final runs, with all 77 final native PNGs inspected**. A separate
authentic-animation supplement passes **2,468 checks, five GIFs, all 241 decoded
frames and all 35 supplemental PNGs inspected**. Three actual legacy projects
reopen unchanged. These are scoped acceptance figures, not game/quality scores.

## Behavior and policy

The Sephiroth-to-Snake diagnostic exposed two independent problems: a coupled
Euler branch missed by per-axis wrapping, and orientation/height controls held
at their attack values through the Wait1 handoff. The latest user correction
authorizes matching X/Y rotation controls while preserving attack TransN placement.

`MatchReferenceControls` is additive to existing fitting options and receipts.
Core defaults it to false; legacy saved settings remain false. A new native dialog
enables it only when the exact loaded setup reviews unique, distinct ancestor
roles (SceneRoot/RootMotion/RootOrientation/BodyOrientation) covering all body
ancestors, with exactly one motion-position role. Existing generic role review
supports equivalent names; no name guessing selects a control. Missing or
ambiguous coverage disables the UI option and rejects Core requests.

When enabled, the complete RootMotion local S/R/T curves, keys, paired tangents
and flags remain source-authored. Other reviewed ancestor controls join the
explicit component/bone mask and fit only inside the entry/exit windows. Their
scene-root translation can fit despite ordinary skeleton-root translation
protection; the explicit option describes this exception. Manual posing and
original Next keep their existing protected-control policy. No skeleton, rest,
weights, timebase, duration, loop flag or source asset is changed.

Reference preview follows the selected policy: authentic first reference pose
under the retained source motion-position local sample. Disabled retains all
original protected locals as in preview.30. Playback/scrubbing only sample
already owned native curves, with no hashing/loading/solving.

Native XYZ branch selection considers `(x,y,z)+360*n` and the coupled family
`(x+180,180-y,z+180)+360*n`, choosing minimum squared channel distance from the
original join. Ties prefer the original family. Choice is frozen for the complete
triplet before editing axes; alternate-family Y derivatives reverse for moving
references. Both boundaries share the algorithm. Off preserves authored turns.
This is native Euler Hermite fitting, not a quaternion trajectory solver.

Receipts advance to `fixed-duration-fk-hermite-2`, `contact-trajectory-preserve-2`,
`contact-exit-recovery-3` and `contact-floor-recovery-2`; actual older recipes
remain readable. New receipts record fitted control identities and placement
policy. Native validation checks option/receipt agreement and the selected floor
baseline. These bind existing review/export recipes; no project schema migration
or persistent secondary document is introduced.

## Frozen constraints and expectations

Expectations were frozen in the sole current plan before implementation:
(166,141,175) toward (-2,-46,-1) chooses (178,226,179); 170 toward -170 chooses
190. Tests cover both boundaries, coupled/whole-turn branches, deterministic ties,
+/-90 middle-axis singularities, moving derivatives, sparse/rest channels, paired
keys and renamed/permuted bindings. A reviewed orientation-control Y=13 reaches
reference Y=12 while motion-position travel remains exact. This fixes the correct
control rather than adding a body offset to conceal an incorrect retained height.

Position .0002 native units, scale .00001 and orientation .01 degrees remain.
Floor recovery retains the .20 shorter-leg lift, 8192 hip-key and 200000 pose-
evaluation limits. Complete positive-influence foot regions retain all authentic
weights/inverse binds. The actual fitted parent inverse drives body-normal
correction. An independent procedural fixture exercises a fitted rotating parent
with nonuniform retained actor motion, complete world/skin evaluation and old gates.

The new per-region floor baseline is min(0, original, authentic reference under
retained motion-position control). Disabled mode keeps its previous all-source-
control baseline. Independent 1/64-frame and held-out witnesses check added
penetration. This is sampled clearance, not continuous certification, contact
locking, minimum-lift optimization or automatic quality scoring.

Fixed pins, protected intervals, unsupported contact paths and preserved support
trajectories reject contradictions. Baked-contact adaptation remains non-looping,
exit-only, all S/R/T, frozen first reference frame and explicit opt-in. A diagnostic
baked-contact ENTRY attempt remains a recorded rejection; an authentic donor-free
native entry exercises the shared algorithm without stripping provenance.

## Fresh verification

The final native cohort contains independent Core boundary/floor/role checks
(365), contact/slot checks (19), original Next motion-control checks in both
native rig directions (628 each), ordinary transitions (80), contact-preserving
native transition/gizmos (62), workspace workflow (61), keyboard (174), scaling
(410), disabled-policy ground recovery (83), its moving-root variant (114),
disabled-policy aerial fit (18), enabled reference-control recovery (92), its
moving-TransN variant (123), and enabled aerial fit (26): 2,883 total.

Tests cover exact source/target/clip identities, original keys and paired tangent
bits through joins, retained action/world/skin samples at fractional times,
authentic first-reference endpoints, plane/pin/protected-range contradictions,
policy changes clearing both candidate and baseline review, stale Apply, native
Undo/Redo, recipe approval invalidation, precise export before Save and fresh
project reopen. Caption width checks and captures verify complete native option
labels. Compact/wide 100/200% app text, native editor and existing keyboard routes
remain; this is no physical-input or monitor-DPI claim.

The final source package was built with `scripts/Build.ps1` using the installed
local .NET toolchain and an isolated `-NativeDirectory`, followed by Release/x86
ControlsAcceptance and RetargetAcceptance builds. Core tests were built/run from
`engine` with its .NET 10 `global.json`. Actual native commands used
`scripts/Test-BackgroundCapture.ps1` with `-WorkspaceFootRecovery`,
`-WorkspaceExitRecovery`, `-TransitionFit`, `-WorkspaceKeyboard`, `-SimpleWorkspace`,
`-WorkspaceScaling` and `-ContactTransitionsProject`. The new policy cases add
`-ReferenceControls`; changing-source travel adds `-MovingRootFixture`. Authentic
model/motion arguments, exact commands, logs, binary/source hashes and the sole
final folder cohort are retained privately in parent
`artifacts/Fighterport-workspace31` (`final-cohort.json`, `qualification.json`,
`review-final/inspection-ledger.json`). No earlier failed/superseded run is counted.

The supplement uses the existing private desktop and shared offscreen renderer.

| Case | Boundary / samples | Checks | Decoded frames |
|---|---|---:|---:|
| Sephiroth AttackS3S on Snake | Exit to Wait1 frame 1 / 8 | 597 | 59 |
| Snake AttackS3S on Mario | Exit to Wait1 frame 1 / 8 | 363 | 33 |
| Mario AttackAirF on Snake | Exit to Fall frame 1 / 12 | 737 | 76 |
| Waluigi AttackDash on Cloud | Exit to Wait1 frame 1 / 8 | 436 | 40 |
| Authentic Snake AttackS3S | Native entry from Wait1 frame 1 / 8 | 335 | 33 |

All use the reviewed reference-control policy. Four retargeted cases retain
original Next provenance; the donor-free entry retains its native ownership and
never bypasses baked-support rejection. Independent endpoint locals, full world
matrices and every skinned vertex match the authentic first reference under the
retained motion-position control. Original prefix/future curves and source assets
remain exact. Existing baked-contact entry adaptation remains unavailable.

Sephiroth→Snake previously retained XRotN local Y=13.25512, while its authentic
Wait1 uses 11.9866209: a 1.2684991-unit offset. Returning that reviewed control to
the reference removes the hovering finish without a compensating hip offset.
The diagnostic 0.8-weight foot subset drops from native clearance 1.29748189 /
1.28867626 to 0.0289823711 / 0.0201769322, matching the authentic first Wait1.
The full positive-influence independent floor oracle is the penetration gate;
these subset heights are separately labeled observations. The independent local
hip-curve witness reduces accumulated orientation travel from 341.265259 to
85.574011 degrees with the same endpoint orientation (85.388812 degrees apart).
This is a reproduced witness, not a universal quality score.

Waluigi's dash retains TransN.Z approximately 32.0007 at the final/held reference
pose; Wait1 never replaces that attack travel. Visual review also records remaining
limits: the conservative Snake→Mario interior clearance envelope can lift both
feet; ordinary native entry has no planted-foot guarantee; the authentic aerial
Fall pose can extend below the Y=0 guide because it is not a grounded reference or
an actor-physics simulation. Endpoint agreement and sampled no-added-penetration
do not establish interior contact/sliding quality. No tolerance or original action
is altered to conceal these observations.

Private `artifacts/Fighterport-recovery31` retains all final `*-verified-final`
inputs/receipts, exact exports and projects, floor/world/skin/root witnesses,
`final-build.json`, `final-supplement.json`, decoded GIF manifests and inspected
PNG ledger. `results/index.html` links the five fresh GIFs; old preview.30 artifacts
remain unchanged. The legacy probe validates actual floor-v1 and ordinary-v1
projects with original file hashes retained (`legacy-verified-final.json`).

Development failures are excluded: an overly strict raw sparse-track comparison
rejected valid added bind-fallback keys; its correction checks original key/tangent
bits and independent whole world/skin samples. A contact test initially selected a
later fitted revision; it now explicitly selects the preserved original Next
revision. Two short-lived probe attempts used nonexistent local paths and were
repeated with verified existing paths. A binary-lock build attempt was retried
after the demo exited. An initial final capture cohort exposed clipped labels;
those were shortened, review invalidation was tightened, and the complete final
cohort and all five GIFs were rerun. Cancelled/superseded evidence is preserved
locally and excluded. No contact guard, numeric limit or source protection was
removed to make the tests pass.

The development launcher selects the qualified preview.31 package. Verified source
only is published to public Fighterport/main under DoctorFlux attribution, with
outgoing author/committer/trailers and the contributor list checked separately.

## Boundaries

Complete BrawlCrate native ownership, original Next, immutable source bytes and
Undo remain. Original Next source-local controls and the explicit fitted result's
reference controls are separate recorded operations. Nonzero motion-position
travel stays authored, including vertical travel; game actor integration is not
inferred. The supplied fixtures do not establish all-character generality.
Physical input, monitor DPI, human usability, broad contact/continuity quality and
actual game acceptance remain unverified. No new solver, adapter or replacement
skeleton is included. Source-only DoctorFlux publication is authorized; assets,
settings, captures, GIFs and binaries stay local and ignored.
