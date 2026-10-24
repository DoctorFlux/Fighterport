# Retarget to This Model

Open a Brawl target model, add a donor motion PAC and select a body animation.
Click **Retarget to This Model**, then choose that animation's actual source
model. The choice is reused for other clips from the same motion asset. A new
**clip â†’ target model** appears in the native animation list. The original
motion remains available. Undo removes the last retarget/import. The Fighter
menu's **Retarget Details** shows the mapping, measured proportions and contact
diagnostics; **Change Retarget Source** lets you correct a source choice.
Retargeting runs with progress and Cancel while the editor protects its inputs.
Previously generated motions keep their baked poses. To apply a solver update
or a different contact mode, select the original donor clip and retarget again;
the newly selected result contains the correction.

Ground-supported results enable the native floor and an oblique camera fitted
to the actual posed mesh. **Fighter -> Ground Contact View** reframes the current
pose, including seated and lying poses. Its **Three-quarter**, **Low side** and
**Opposite side** presets make sole height and knee direction visible. Low side
frames the feet and knees with a level camera close to the measured sole plane;
the other views retain whole-pose framing. The native camera and Floor controls
remain available; this adds no viewport panel.

Both directions use this workflow: Mario motions onto Snake, and Snake motions
onto Mario. The solver operates on semantic roles and measured geometry, with
no character-name branches. The native adapter extracts the actual hierarchy and bind transforms, then
the shared characterizer identifies humanoid anatomy. It contains no character
identity tests. Other games still require their own readers/evaluators/writers. Arbitrary-model retargeting
is the product goal, not a claim that those integrations already exist.

Play, scrub, edit with the native tools and save the fighter normally. Generated
CHR0 files are self-contained: donor references and project metadata are not
required to preview exported motions. Projects retain source hashes, the mapping,
algorithm, target rig fingerprint and contact diagnostics. Native root movement
is retained in the generated clip. The inspection camera stays fixed throughout
playback, including root motion; use the native manual framing controls when
desired. Shadows stay out of the
scene. Native editors, timeline and resource functions remain available.

**Export** writes a new CHR0, or a new `.pac` using a target motion PAC template.
Exactly one matching named CHR0 slot is replaced. Skeleton validation and fresh
native decoding run before committing the new output. Existing destinations and
original templates are protected. This is an animation-slot export, not a complete
playable fighter build; no in-game validation is claimed.

## Automatic characterization

The characterizer consumes format-neutral joints, not a character identifier.
Common English bone conventions (including namespace prefixes and left/right
suffixes) contribute evidence. Parent/child topology and rest positions infer
pelvis, trunk, head and bilateral limb chains when names are anonymous. Segment
lengths and mapped ancestry are validated before generation. Different arm/leg
proportions are measured from each actual skeleton.

**Fighter -> Review Skeleton Mapping** shows body roles, proposed model bones and
the evidence for each choice in one compact dialog. Ambiguous/incomplete source
or target characterization opens this review automatically. Use Mapping rejects
duplicates, missing bones, invalid torso/limb ancestry and zero-length segments.
Confirmed source/target bindings travel with generated project assets and are
reused after restart. Original skeletons and files are never renamed by this UI.

Geometry alone cannot establish anatomical left/right for every symmetric rig,
or identify arbitrary creature anatomy. The geometry fallback assumes a humanoid
rest pose in normalized Y-up adapter coordinates and requests review of inferred
roles. It is not a universal creature-rig classifier or a replacement for future
format adapters, rest-pose editing and rig authoring.

The design was checked against [Godot's official retargeting documentation](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/retargeting_3d_skeletons.html):
anatomical bone mapping, rest calibration, proportion normalization and explicit
correction of uncertain maps are separate concerns. This implementation is
independent; it does not embed Godot or copy its retargeting code.

## Mapping, FK and contact IK

The neutral solver requires 17 core humanoid roles. A separate root bone,
motion/pivot/turn helpers,
shoulders, toes and fingers are optional there. The supplied example skeletons automatically yield
47 shared semantic pairs including Mario `WaistN` â†” Snake `BodyN`, mirrored limbs, fingers
and compatible attachment bones. Bind-world quaternion calibration handles local
bone-axis differences. Target-only helpers retain their own bind transforms.
Authored translation deltas and scales transfer relative to the target bind pose.
Leg, arm and head proportions are measured independently, in either direction.
Analytic two-bone IK fits body-relative goals and nearby hand/head gestures.

Actor trajectory and anatomical displacement are separate. Mapped ancestors
above the pelvis retain movement in normalized adapter world axes and units;
leg length and anatomical rest tilt do not rescale or rotate that route.
Equal, unambiguous ancestor paths can also pair unnamed trajectory helpers.
Pelvis proportion correction operates relative to this trajectory. Native
translation curves retain authored knots, discontinuities and cubic tangents;
contact fitting never edits these ancestor translations. Different local bone
axes use a fixed coordinate conversion. See [research notes](RETARGET_RESEARCH.md)
for the Xenoverse-tool and engine references and the adapter contract.

The contact pass measures posed skin geometry, including finger/toe influences,
so an ankle/wrist offset alone cannot make a sole or palm float. Source height
and velocity identify floor support; low planar velocity identifies stationary
locks. Smooth support/lock transitions preserve heel/toe rolls and avoid a snap.
Near-floor foot lifts transfer their measured source skin clearance using the
leg ratio, so a small intentional lift does not become a much larger floating
foot on a longer-legged target. Fully supported soles still fit the floor.
Persistent upright stance is inferred across the whole source clip from actual
floor evidence, sole-height range, hip movement and foot travel. This corrects
small source sole drift that otherwise grows with the target's leg ratio.
Supported hip height comes from the source's thigh-to-ankle height, the target
sole offset and the target hip offset. Hip bounce remains proportional while
the coupled leg solve keeps the soles grounded and preserves knee bend planes.
It is an automatic stance interpretation, not a character or animation profile.
Stance is also detected within upright portions of a motion. An earlier hanging,
airborne or seated portion does not disable hip/foot fitting when the source
later settles into standing support.
For a quiet low foot that never enters the strict floor band, sustained support
under the opposite sole can establish the common plane. This inference also
requires small local height variation and low travel. It prevents small source
FK gaps from becoming larger gaps on a target with longer legs. Higher held
feet remain lifted; Source contacts disables the additional stance inference
when a subtle intentional lift should be retained.
Short moving upright segments also provide support evidence: both legs extend
downward, vertical leg extension stays stable, both soles are near the common
plane and at least one establishes it. This runs throughout the motion without
an idle reference or an ending rule. It separates floor support from stationary
locking, so horizontal travel and bounded source FK drift during hip bounce do
not undo a confirmed stance. Higher held feet remain outside the inference;
ambiguous subtle lifts can use Source contacts.
Nearly complete support smoothly converges to the plane in both the end-only
and coupled body solvers. A 99% supported palm must not retain a visible
fraction of a large initial FK gap simply because the body solver was inactive.
There is no forced idle ending or pose substitution. Contact evidence is sampled
per body part and time, including the final portion of a motion. A planted sole
receives correction there; an intentionally raised foot or departing hand keeps
its source-relative motion. Standing support does not require a match to Wait1.
Hands can support the body in landing, get-up and down-smash motions. Source
elbow/knee bend planes guide limb IK; pelvis translation restores reach without
stretching segments or rewriting the authored root tracks. Contact tracks are
immutable, so backwards scrubbing gives the same result as forward playback.

Auto can infer an initial suspended pose from raised hands near the source's
reference plane and a pelvis below it. The hands establish a support frame;
proportion correction centers the target's body on those grips and fits both
arms within their reach. Grip influence fades as the source releases its hands
and rises. Body parts moving upward from below the reference plane are not
treated as landings. This uses joint geometry and motion, never an animation
name or a ledge-specific mode. The preview retains the ordinary flat floor;
there is no generated platform or edge object. Missing real terrain/prop context
still limits what can be inferred automatically.

Each skin region has its own history of moving below and through the plane.
Floor support resumes after it clears the contact band or settles. A rising
shin therefore cannot become a false planted constraint competing with a hand.
Emerging moving feet still receive source-scaled lift correction, without being
locked to the floor. These immutable tracks also support random scrubbing.

Support also covers the trunk, head and full arm/leg skin patches, rather than
assuming every motion stands on its feet. A simultaneous damped least-squares
solve adjusts hip translation and mapped joint rotations for seated, kneeling,
lying and changing-support poses. Contiguous trunk/limb patches let the lowest
supporting skin region move with the pose; they do not require a differently
shaped target to put every individual torso bone's skin on the same plane.
New stationary body anchors are calibrated in time order on one reachable pose,
while respecting contacts already held. Independently calibrated hand/forearm or
trunk/head anchors can contradict each other on different target proportions.
Long unmapped head/neck branches outside the measured head envelope are
excluded from anatomical support, independently of attachment/bone names.

Body-contact baking uses analytic skin Jacobians, a sparse coupled pose prior,
Cholesky factorization and batched FK updates. A conservative skin-support tree
finds the same lowest original vertex without repeatedly skinning every vertex.
The solve starts with the main body chains and adds digit controls only when
measured end contacts need them. Convergence checks stop satisfied contacts;
endpoint and pelvis reprojection preserve support after the body solve. Quarter
poses are cached without using playback or seek order as solver state.

Ground correction uses a flat plane inferred from the rest soles. The native
floor is drawn at that same measured height. In **Fighter -> Retarget Options**,
**Auto** requires sustained source skin-contact evidence, **Grounded** explicitly analyzes source floor support,
and **Airborne** disables floor correction while retaining FK, proportions and
limb IK. **Source contacts** retains strict source contact evidence and disables
persistent stance stabilization, preserving small authored foot lifts. Use it
when Auto interprets a deliberate subtle lift as stance drift.
Grounded preserves intentional lifts and takeoffs; it does not pin every
limb throughout a jump. The next-retarget setting is saved with the project;
each result records its own mode and remains unchanged when the setting changes.

Auto uses geometry and motion, never clip names. A locally posed airborne clip
can be indistinguishable from standing when its source floor context is absent;
choose Airborne in that case. Grounded still detects support per body part and
time rather than forcing all limbs down. Terrain, props, inter-character contacts,
collision, manual contact editing and general control-rig authoring remain future
work. Deep source penetration fades contact correction continuously into FK,
rather than snapping when a static-floor threshold is crossed. Unreachable goals
clamp to reach and are reported; contacts are not guaranteed
physically feasible for every rig or pose.

## Native curve preservation

The internal pose is format-neutral SRT/quaternion/FK data. CHR0 is an adapter,
not the required intermediate for future formats. Output retains native integer
key times, duration and loop flags. Quarter-frame evaluation unwraps Euler branches
and fits shared Hermite tangents. A bounded native-curve refinement minimizes
skinned contact height and stationary planar drift while penalizing pose changes.
Integer poses and root keys are preserved. Native I12 output/decoding preserves
authored tangents through editing and re-export.
Fitting indexes native keys once, reuses unaffected local transforms, and skips
converged keys. Both height error and stationary drift trigger refinement, so a
contact that slides along the floor still receives correction.
Refinement has a bounded 40-pass budget, progressing from one key to two and
three neighbors. A stalled stage still attempts the next coupled neighborhood;
it does not treat a single-key local minimum as complete convergence.
Suspended hand goals also participate in native curve fitting. Large fractional
pose errors can bring otherwise unsupported limb chains into refinement.
Contact-bearing chains retain contact priority. Authored nonuniform scale is
preserved; rotation below such scale can change a segment's world-space length
without changing its local bone offset or inserting a stretch track.

Native cubic Euler curves approximate the quaternion/IK solution between keys.
Small floor gaps and larger pose deviations during fast transitions can remain;
these are measured below, not presented as perfect arbitrary-rig correction.
Only body/skeletal channels transfer. Facial, material, visibility, morph and
weapon semantics are not transferred onto another character.

## Real native measurements: previous idle/ledge checkpoint

The current Release/x86 review checks **Wait1, CliffClimbQuick,
CliffClimbSlow, CliffAttackQuick and CliffAttackSlow in both directions**.
Other idle variants and the broader motion suite were not rerun. Mario has
63 bones and Snake 70, with 47 shared semantic mappings. Measured forward
ratios are legs 2.525920, arms 1.611464 and head 0.778889; reverse ratios are
recomputed. These are local acceptance fixtures, never product profiles.

| Direction / motion | Supported sole height | Supported palm height | Supported body height | Suspended grip error | Complete bake |
| --- | ---: | ---: | ---: | ---: | ---: |
| Mario → Snake / Wait1 | 0.005202 | 0.000000 | 0.000000 | 0.000000 | 0.54 s |
| Mario → Snake / CliffClimbQuick | 0.080151 | 0.000000 | 0.165844 | 0.089047 | 109.53 s |
| Mario → Snake / CliffClimbSlow | 0.057756 | 0.048560 | 0.078793 | 0.020581 | 87.44 s |
| Mario → Snake / CliffAttackQuick | 0.030323 | 0.000000 | 0.000000 | 0.168806 | 94.39 s |
| Mario → Snake / CliffAttackSlow | 0.046990 | 0.049132 | 0.030229 | 0.021703 | 71.15 s |
| Snake → Mario / Wait1 | 0.001217 | 0.000000 | 0.000000 | 0.000000 | 1.11 s |
| Snake → Mario / CliffClimbQuick | 0.019539 | 0.096557 | 0.085621 | 0.032955 | 126.31 s |
| Snake → Mario / CliffClimbSlow | 0.018177 | 0.107247 | 0.051531 | 0.016928 | 136.14 s |
| Snake → Mario / CliffAttackQuick | 0.015960 | 0.012892 | 0.046105 | 0.031563 | 127.81 s |
| Snake → Mario / CliffAttackSlow | 0.012383 | 0.148610 | 0.108660 | 0.006703 | 232.08 s |

Units are native target-model units. Supported contact errors include every
quarter frame with source support at least 0.99. Zero means no qualifying
contact sample. Suspended grips use inferred hand anchors, separately from
floor contacts. Bake times include the full solve and native fitting; other
acceptance work ran concurrently. No contact tolerance was relaxed.

Across **2,286 whole-clip quarter samples**, both soles are compared
independently with BrawlLib weighted vertices, including unsupported phases.
Maximum independent foot-skin disagreement: **0.00000594**.
Maximum authored trajectory error: **0.00000000**.
Direct raw source-versus-native **TransN Z error is 0.00000000**
through all ten motions, independently of the solver conversion helper.
Sparse cubic/discontinuous native trajectory fixtures also pass quarter and
withheld fractional samples.

Wait1 checks both soles over the entire native clip without support filtering:

- Mario → Snake: maximum absolute sole clearance **0.005202** (limit 0.04).
- Snake → Mario: maximum absolute sole clearance **0.001217** (limit 0.04).

| Direction / slow motion ending | Left target sole | Right target sole | Right source sole |
| --- | ---: | ---: | ---: |
| Mario → Snake / CliffClimbSlow | 0.000001 | 0.000000 | 0.245312 |
| Mario → Snake / CliffAttackSlow | -0.000000 | 0.000001 | 0.255266 |
| Snake → Mario / CliffClimbSlow | 0.000000 | 0.000000 | -0.060954 |
| Snake → Mario / CliffAttackSlow | 0.000001 | -0.000001 | -0.013461 |

The source slow endings return to an upright standing posture. Their small
sole gaps fall inside the source idle clearance range; movement alone does not
establish intentional lifting. The earlier detector amplified these into
0.619638/0.644780 target gaps. Bilateral posture evidence now restores support
even in a short moving segment, with no named idle reference or pose substitution.
Both soles finish within 0.03 of the plane in all ten idle/quick/slow cases.
A short unrelated numerical segment in the middle of motion also verifies
floor support during travel without stationary locking. Higher held
feet stay lifted in independent regressions and Source contacts retains the
stricter low-lift interpretation.

Maximum solver end-contact height error is 0.005600;
integer end-contact height error 0.005602;
integer end-contact planar drift 0.009668;
body integer height error 0.012376.
Unchanged limits are 0.2 for supported quarter-frame height/planar/grip errors,
0.03 for integer end contacts/locks, 0.02 for solver end-contact height, and
0.1 for body integer/solver height. Local bone offsets and authored scales
remain unchanged; rotations under nonuniform scale may change world length.

**189 focused retarget checks pass**, plus **60 native/project checks**.
They cover anonymous/alternate naming, independently varied proportions and
local axes, rootless 17-role rigs, scales 0.01–100, grounded hip bounce, plane
crossing, held lifts, renamed-motion invariance, deterministic scrubbing, native
edits and saved provenance. Both output PACs retain 355/297 slots and sampled
timing/interpolation in all 354/296 untouched motions. All **43 current source
files are unchanged**.

The same executable passes six actual-GUI retargets: Wait1 plus slow get-up
and attack in both directions. Each has first/quarter/middle/three-quarter/last
three-quarter and low-foot captures, an opposite view, normal floor, complete
non-looping playback to natural stop, independent CHR0 export/reimport and both
project restarts with normal exit. Ending low views also assert actor pixels.
The ordinary floor can occlude hanging body parts below it; numerical native
skin checks include those phases even when a fixed low camera cannot see them.
The earlier ordinary-workflow and three-size layout receipts cover the prior
published build; those separate scripts were not rerun for this solver change.

These are sampled acceptance results, not universal rig or motion guarantees.
Native cubic Euler interpolation remains approximate. Unknown anatomy, missing
terrain/props and infeasible contacts still require review or Airborne/Source
contacts settings. See [TESTING](TESTING.md) and [research](RETARGET_RESEARCH.md).

## Release preview locomotion validation (18 October 2026)

Algorithm v7 adds per-time supporting-patch evidence in Auto mode. A sole must
have an aligned patch or credible upright stance; palms/body patches also need
their anatomical joint close to the plane. Quiet stance cannot explain a foot
that repeatedly travels substantially below the source floor. Validated support
continues through a connected near-floor heel/toe roll, including native loop
seams, and ends at a lift/deep crossing. A short fade avoids an abrupt switch.
Grounded and Source contacts remain explicit alternatives for ambiguous data.
Fractional native fitting now escalates planar sliding penalties by rig size,
as it already does for contact height. Neither change writes trajectory bones.

**275 locomotion checks pass**, using Run, JumpSquat, JumpF, Fall and LandingAirN
in both Mario-to-Snake and Snake-to-Mario directions, all with Auto. Ten fresh
native exports are measured at **1,030 quarter-frame times** against independent
BrawlLib weighted skin. Source/native actor Z error is **exactly zero** in all
ten motions. All **43 source files retain their SHA-256**.

| Source -> target / motion | Fully supported foot samples | Maximum supported native sole error | Maximum stationary planar error | Bake seconds |
| --- | ---: | ---: | ---: | ---: |
| Mario -> Snake / Run | 12 | 0.0513 | 0 | 5.58 |
| Snake -> Mario / Run | 89 | 0.0576 | 0.1653 | 55.48 |
| Mario -> Snake / JumpSquat | 25 | 0.0030 | 0 | 0.01 |
| Snake -> Mario / JumpSquat | 73 | 0.0058 | 0.0078 | 0.11 |
| Mario -> Snake / LandingAirN | 242 | 0.0478 | 0.0433 | 0.76 |
| Snake -> Mario / LandingAirN | 242 | 0.0293 | 0.0249 | 20.24 |

Both JumpF and Fall files in both directions have **zero support/locks** and
zero solved-pose deviation from proportion-corrected FK/limb IK. Their largest
integer native pose round-trip deviation is 0.01485. These source clips omit
gameplay jump height: some source/target feet cross below the static preview
floor. Zero locks means they are not pinned there; it does not mean a missing
gameplay arc has been reconstructed. No motion-name rule controls production
inference. An ambiguous toe-only pose can still require an explicit mode.

Independent continuous takeoff/flight/landing controls on both real rigs verify
25 clearly airborne and 34 grounded times per direction. Air support/locks,
airborne FK deviation and trajectory error are all zero. Both grounded soles
return within 0.000004 of the floor. Existing renamed/anonymous anatomy,
proportion, local-axis, held-foot, hip-bounce and middle-segment cases also pass.
Low-foot proportional-height assertions apply where the floor-fitting weight
is complete; unsupported flight is checked against FK instead of being forced
to satisfy an unrelated floor constraint. Native contact tolerances remain
0.2 quarter-frame, 0.03 integer end/locks, 0.02 solved ends and 0.1 solved/integer
body contact. These are sampled tolerances, not a universal exactness claim.

Release candidate executable SHA-256:
`CDBC17F795EF5EC4A60EBCD4BF4103221005FEFCD532DD6F87B19CEBA79FE6D0`.
Locomotion receipts: `artifacts/locomotion-acceptance-v7/`.
The same executable also passes 60 native/project persistence checks in
`artifacts/native-locomotion-v7/` and 301 idle/quick/slow ledge regression checks
in `artifacts/cliff-regression-locomotion-v7/`. The latter measures ten native
motions at 2,286 quarter-frame times, retains exact actor Z and all grounded
endings within 0.03 of the floor. Complete-cliff bake times on this run range
from 57.59 to 126.18 seconds. All ten locomotion GUI retargets pass full playback,
camera scrubbing, native export/reimport and two project restarts from the
portable runtime. Each GUI native output is byte-identical to its measured
acceptance bake. Saved provenance records Auto/v7 throughout. See development
status for artifact hashes and final package verification.

## Complete-clip performance

The contact table includes full baking, not cached-pose evaluation. Separate
complete Mario-to-Snake body benchmarks on the same host measured:

- Face-down / DownWaitD: 71 frames, **111.36 s**, fresh native export/decode passed; maximum solver contact residual 0.085186.
- Seated / SlipWait: 111 frames, **66.70 s**, fresh native export/decode passed; maximum solver contact residual 0.013944.

The earlier seated complete-bake baseline was 233.00 s. These measurements used
the optimized v5 body solver before the final support inference and trajectory
changes, with other acceptance work running. These two body clips were not
rerun on the current revision. Current complete-cliff bake times, including
native curve fitting, are recorded above and can take several minutes.
Performance is not instantaneous or guaranteed. Progress,
elapsed time and Cancel remain available. Full skin geometry, coupled pose priors,
original timing and contact tolerances were retained.
