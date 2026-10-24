> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

> Supporting supplied design. Governing plans: [MASTER_PLAN.md](MASTER_PLAN.md) and the ONE [plans/current.md](plans/current.md). Its inspected 5605bf7 baseline is historical; current measurements are in [RETARGET_VALIDATION.md](RETARGET_VALIDATION.md).

# FighterPort retargeting rewrite plan

## 1 Purpose and decision

Rewrite FighterPort's retargeting subsystem around independent rig descriptions, source motion analysis, target interaction planning and temporal constraint fitting. Integrate it into the existing standalone C# application. The source library, native document model, editor, rendering, asynchronous jobs and verified native codecs provide the integration boundary.

The product goal is to select an authentic source model and one or many of its animations, choose an existing target character, obtain motion fitted to that character's proportions, adjust useful controls and save through the target document's native adapter. The same pipeline must work with either character as the source. Its automatic behavior must cover grounded motion, airborne motion, hands on surfaces, sitting, lying, rolling, ledges, self-contact and shared two-hand props when the required rig and scene information is available.

This handoff is based on a source inspection of `DoctorFlux/fighterport-studio`, branch `main`, commit `5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d`, dated 19 October 2026. Inspect the actual checkout again before implementation. Repository test results below are recorded results, not tests executed during this review. [S1, S2]

### Meaning of any source and any target

Build a general architecture with explicit anatomy and format capabilities. A supported humanoid pair must not need a character-specific algorithm or a preferred direction. Different names, native axes, rest poses, heights, limb ratios, torso widths, fingers, spine counts and helper arrangements belong in data and mapping policies.

Animation retargeting cannot manufacture an absent limb or satisfy contradictory geometric requirements. Unsupported anatomy, missing interaction context, an unresolved numerical solve and a proven reach contradiction are distinct outcomes. A successful file write does not establish that the target performs the intended action.

General rig and contact graphs are required from the start. Humanoid acceptance is the first complete capability set because it matches the available FighterPort fixtures. Additional anatomy profiles must be explicit extensions with their own acceptance cases; a generic interface alone does not complete quadrupeds, wings, tails or arbitrary topology.

### Meaning of both directions

For A to B, analyze A's authentic rig and clip and fit to B's geometry. For B to A, analyze B's authentic rig and clip and fit to A's geometry. Recompute target goals, reach, surface calibration, scale policies and limits for the new direction. Share a source analysis only while its complete source dependencies remain unchanged.

Do not implement reverse transfer by transposing a saved bone-pair list, negating offsets, inverting the final bake or feeding an already adapted clip back as the original. A to B to A is not generally lossless: unequal chain counts, constraint adaptation, curve fitting and native quantization can discard information. Identity and restricted reversible cases need exact invariants; general transfers need independent acceptance in both directions.

## 2 What the current repository already establishes

The current application is independent Windows x64 C# with .NET 10 and Avalonia. Core owns scene, curves, timing, document transactions and source revisions; a narrow Brawl worker performs native preparation and writing. Existing controls attach to imported deform bones without replacing the skeleton. [S1, S3]

The recorded v6 matrix contains 26 directional transfers, 22 native-correct outputs and four unresolved bakes. Only `Wait1` in both directions has clean status under the frozen contact profile. Review outputs remain investigation results and normal batch Apply blocks them. The four unresolved rows are Mario to Snake `SlipWait` and `DownWaitU`, and Snake to Mario `CliffAttackSlow` and `DownWaitD`. Large down-motion contact residuals also remain among review rows. [S2]

The repository records 339 original Core checks, 592 original/private native checks, fitting and directional GUI checks and fresh build receipts. These counts overlap and must not be added. Reproduce the relevant available checks and record new evidence for the rewrite. Native acceptance, displayed behavior and actual game acceptance stay separate. [S1, S2]

### Source locations and rewrite consequences

| Current component | Observed role | Rewrite consequence |
| --- | --- | --- |
| `Retargeting.cs` | Characterization, bind-relative transfer, source contacts, target pins, per-frame fitting and baking | Separate these stages behind the existing transfer entry point. |
| `Contacts.cs` | Analytic chain seeds and finite-difference damped least-squares correction for an individual pose | Preserve its useful contracts and same-rig editing behavior; add a temporal retarget solve with reviewed limits and action objectives. |
| `SurfaceContacts.cs` | Skinned region preparation, ground and support analysis | Separate geometry, source evidence, contact state and target adaptation. |
| `ContactBakeRefinement.cs` | Fractional contact and native-tangent refinement | Retain encoding-aware checks; make baking consume a solved motion plan. |
| `RetargetWorkflow.cs` | Settings, source-analysis keys, validation, jobs, row states and Apply | Reuse the transaction and stale-result protection boundary. Version changed recipes and receipts. |
| `ContactReferences.cs` | Reviewed support frames, shared grips and deforming-surface references | Extend existing representations into the interaction graph. |
| `AnimationLibrary.cs` and `ProjectStorage.cs` | Authentic source revisions, persistent library and pooled source data | Preserve identity and old project readability; invalidate derived caches by version. |

These are inspected code responsibilities, not a claim that every implemented path passes the full feature contract. [S3, S4]

`RigCharacterization.Describe` currently requires characterized left and right legs, and several calculations use a humanoid leg-length reference. The new generic preparation layer must expose such requirements as capability rules rather than treating two legs as a universal property. The current bake carries earlier corrections forward between frames and then refines curves; that does not provide a joint optimization of contact transitions over time. [S4]

The validation document records an earlier exponential-map solver experiment that regressed private cases and was reverted. Replacing Euler increments with quaternions alone is not the rewrite. Better source interpretation, permitted degrees of freedom, constraints, temporal objectives, initialization and independent validation are required. [S2]

## 3 Integration and migration rules

Keep `docs/MASTER_PLAN.md` and `docs/plans/current.md` as the sole master and executable current plan. Reconcile this design into `docs/AUTOMATIC_RETARGETING.md` and link supporting research or architecture notes from it. Preserve a single active M2 substep. Do not create a competing master plan or restart already completed library and native work.

Implement on a dedicated rewrite branch, suggested name `feature/retarget-v2`. Inspect the worktree, upstream and local changes before selecting its base; use an isolated worktree if needed. Preserve the repository's existing source-publication authorization and commit policy. This handoff itself does not modify the remote repository.

Introduce a versioned backend behind the existing `RetargetService` and workflow entry points. Keep the previous implementation available temporarily for baseline comparisons. Never silently fall back to it while labeling a result as V2. Switch the normal path when the required acceptance set passes, then remove obsolete implementation deliberately.

Preserve imported native BoneIds, names, ordering, parent hierarchy, bind and inverse-bind data, skin weights and helper identities. Solver coordinates, virtual chains and controls are derived data. They must not become a replacement exported skeleton. Authored animated translation and scale require explicit per-channel policies; target rest lengths must not be stretched to hide a failed solve.

FK and IK editing remain usable without a donor animation. The new retarget backend may share math utilities with these tools, but a clip-wide solver must not run during an ordinary playback tick. Preserve current command transactions, Undo/Redo, source proofs, cancellation and dependency guards.

## 4 Open source references and selective reuse

### Godot for rig mapping and rest-relative transfer

Study Godot's `BoneMap`, `SkeletonProfile`, `retarget_modifier_3d.cpp` and importer rest fixer. The useful pattern is a semantic profile driving a rig with its own rest transforms, with cached reference-space conversion. The pinned source provides a modifier-based path as well as axis-overwrite behavior. FighterPort should bind semantic roles to native IDs and preserve native rests; its normalized representation stays virtual. [R1, R2]

Godot's documented global-pose option requires matching bone lengths and can stretch unequal targets. Its importer can remove position or unmapped tracks. These policies must not be copied into FighterPort's native data contract. Study current small-chain IK components separately; they do not infer chairs, lying support or two-hand grip intervals. [R2, R3]

### ozz animation for small-chain IK and bake error

Study `IKTwoBoneJob`, its degenerate-chain tests, the foot IK sample and the offline animation optimizer. The useful contracts include a supplied goal, bend axis, pole, local rotation corrections and a reach result distinct from successful execution. The optimizer considers errors propagated through the joint hierarchy. [R4]

The foot sample explicitly omits complete planting activation and reach handling. It is a math reference, not a complete contact detector. Port only justified components with independent tests; a C++ runtime dependency is not required merely to use those ideas. [R4]

### HSDLib for actual C# retargeting and Melee baking

Study `RetargetUtil.cs`, `AnimationRemapper.cs`, `IKSolver.cs`, `AnimationBakery.cs` and `FOBJ_Encoder.cs`. These contain real mapping, bind-delta transfer, optional two-bone IK, batch conversion and FigaTree encoding. The existing batch path uses the older retarget method, not automatically the newer IK remapper. Its source translation/scale and local-axis assumptions need review before reuse. [R5]

Use the code as a targeted reference for C# implementation and the future Melee adapter. Do not transplant its batch orchestration, mapping assumptions or proportion policy unchanged. It does not supply automatic whole-body contact interpretation. [R5]

### WickedEngine for semantic mapping and independent output

`Scene::RetargetAnimation` separates semantic humanoid mapping, reference transforms and copied or source-linked output data. Its fixed humanoid roles and unmatched-channel handling are narrower than FighterPort's intended contract. Runtime foot placement is separate from baked retargeting. This is an additional architectural comparison, not the final contact solver. [R6]

### MeshRet for body geometry and interaction descriptors

Study skinned surface sensors, body-region relationships and contact/penetration evaluation. Its released custom-character workflow requires the Mixamo skeleton structure. A learned model with that assumption cannot be inserted as the universal native-rig backend. Weighted training terms are not hard constraints, and the checked default configuration does not enable every implemented contact loss. Use its geometric ideas and metrics as research references. [R7]

### Native modding libraries

Retain BrawlLib behind the existing adapter and verified codec fixes. `ssbh_lib` is a strong Ultimate data-layer reference. Its animation recompression is documented as lossy for non-Boolean tracks, and the inspected uncompressed conversion API contains unimplemented 2.0/2.1 branches. Native encoding must be tested at the exact selected version. A format library does not establish retarget quality or game acceptance. [R8, R9]

### Pinned source and license ledger

| Component | Inspected revision | Actual root license |
| --- | --- | --- |
| Godot | `ed1daf0bf001b61586d9930840f2f1394092c079` | MIT in `LICENSE.txt` |
| ozz-animation | `744eb9d99f606eda849acb0b1204f7a3dc20bca1` | MIT in `LICENSE.md` |
| HSDLib | `85567e40797de3c820a55476eca54c704921a848` | MIT in `LICENSE` |
| WickedEngine | `4f93f3fd4f337aeb38dbfe410701b83024ccdd42` | MIT in `LICENSE.txt` |
| MeshRet | `96ccd8c8b5ee102a1b067780c0300c30da7d925d` | MIT in `LICENSE` |
| BrawlCrate | `d56cbac89dc2cd0e5c3f923e1d03041b16902add` | LGPL v3 in `LICENSE` |
| ssbh_lib | `08f8bc4470a7d480dfc9b64d7e08e97f3820fed7` | MIT in `LICENSE` |

Record exact reused files, revision, component dependencies, modifications and applicable notices in the existing license audit before copying code. Root licenses do not establish the terms of every bundled dependency, dataset or downloaded model. Research inspection is distinct from adopting code. [R1, R4-R9]

## 5 Proposed data and service boundaries

Keep these in Core under a cohesive retargeting area. Suggested names describe responsibilities; adapt them to existing contracts instead of creating empty abstractions.

| Record or service | Responsibility |
| --- | --- |
| `RigDescriptor` | Native identity, reference transforms, coordinate convention, units, topology, deformation data and capabilities. |
| `SemanticRigBinding` | Roles, chains, twist distribution, surface regions, reviewed limits and mapping evidence on one exact rig. |
| `SourceMotionAnalysis` | Target-independent sampled evidence, contacts, phases, interaction relationships and uncertainty. |
| `InteractionGraph` | Time-dependent relationships between body regions, surfaces, props and actor frames. |
| `TargetMotionPlan` | Target-specific anchors, reach choices, permitted changes, reference motion and constraint priorities. |
| `TemporalRetargetSolver` | Coupled pose and interaction fitting over deterministic windows or a clip. |
| `NativeCurveBaker` | Converts solved motion to the destination's supported curve representation and precision. |
| `RetargetValidationReport` | Independent source-mode, solver, baked, decoded-native and displayed evidence. |

Keep the existing source snapshot, recipe, batch and document identities. Version and migrate their changed schemas. Serialize durable constraints and settings, not a live numerical solver. Cache reusable geometry and analysis separately from target solutions.

The processing order is: prepare authentic rigs and motion; characterize each rig; analyze the source clip; build the target plan; solve; fit native curves; independently evaluate those curves; publish a guarded preview; Apply as one transaction; save and freshly decode through the target adapter. Preview renders prepared curves only.

## 6 Rig preparation and baseline motion transfer

### Coordinate and transformation contract

Document FighterPort's actual matrix multiplication order, row or column vector convention, local/world definitions, handedness, unit conversion, native rest interpretation, pre/post rotations and inverse-bind semantics. Derive source-to-target rest-relative conversion for that convention. Godot formulas cannot be pasted into `System.Numerics` without checking order.

Keep three concepts separate: file-unit conversion, anatomical motion scaling and authored animated scale. Convert units exactly once. Root travel scaling, pelvis displacement, arm length, leg length and contact offsets need separate policies; one global height multiplier is insufficient.

Preserve proper rotations, reflections, nonuniform scale and singular transforms as distinct cases. A quaternion cannot represent a reflection. Unsupported negative or singular scales should produce a specific capability result while preserving source data. Reuse the existing bounded positive nonuniform-scale behavior as a regression target.

### Mandatory baseline invariants

- Source rest maps to target rest when fitting preferences and scene contacts are disabled for the transform test.
- Same rig identity transfer, with cleanup and intentional fitting changes disabled, preserves evaluated motion, timing, winding and supported detailed channels within native precision; it must not erase a valid full turn.
- Rotating the whole coordinate convention produces an equivalent physical result after converting it back.
- Scaling file units consistently changes representation, not the physical action or contact classification.
- Missing optional roles and unmatched channels follow explicit policies and appear in coverage results.
- Mapping many source joints onto fewer target joints is tested separately from distributing motion over extra target joints.

### Semantic mapping

Use exact reviewed fingerprints first. For unfamiliar rigs, combine hierarchy, relative geometry, symmetry, branch structure, bone axes and names to generate scored candidates. Validate candidates globally for chain order and role conflicts. Names are useful evidence, not proof. Request a focused mapping correction only when the available evidence cannot resolve a required role.

Represent arbitrary-length chains and explicit intermediate/helper/twist behavior. A chain may use an analytic two-segment seed when appropriate, but the model must not require every limb to contain exactly three adjacent bones. Preserve fingers, root helpers, attachment bones and unmapped native tracks with explicit transfer or preservation policies.

## 7 Automatic source analysis

Analyze authentic source motion before target fitting or user pose preferences. Sample native curves with their real timebase and include active geometry layers. Work in the correct composed actor and support frame; motion extracted from a game may be in-place even when the actor is traveling.

For in-place locomotion, require known actor travel or a clearly labeled inferred virtual trajectory before rating world-space planting. An unknown actor trajectory cannot earn a verified world-plant result. Compare root-motion and equivalent in-place representations at the declared nominal speed. Baking for that speed does not guarantee planting under arbitrary runtime movement or speed changes.

Prepare calibrated skinned surface regions for soles, toes, heels, palms, knees, elbows, pelvis, thighs, chest and back as available. Use weighted geometry or surface samples rather than assuming a bone origin lies on the character's skin. Cache spatial acceleration and sample provenance.

Infer candidate support surfaces from sustained source evidence with confidence and residuals. Use explicit scene geometry when provided. A floor, seat, wall, bounded ledge and moving platform are different references. Do not infer an infinite floor across a ledge-hanging phase or fabricate a chair and mark it observed.

Use support-relative position, distance, normal and tangential velocity, orientation, duration, neighboring frames and patch changes as features. Derive velocities in seconds and normalize distances by the appropriate anatomical scale. Use hysteresis or an explicit temporal state model to avoid frame-by-frame flicker. Include brief impacts and rapid releases; a minimum-duration rule alone must not erase them.

| Contact state | Preserved meaning |
| --- | --- |
| Free or airborne | No ground lock without supporting evidence. |
| Plant | A patch remains fixed relative to its support during the interval. |
| Slide | Maintain surface contact while preserving intended tangential movement. |
| Pivot | Preserve the supported point or axis and allow the evidenced rotation. |
| Roll | Transfer support through changing patches without welding the entire body. |
| Touch or impact | Respect the short interaction and its release timing. |
| Grip | Preserve a hand's relationship to a prop or bounded support. |
| Body support | Maintain evidenced seat, hip, thigh, torso or other support. |
| Unknown | Retain candidates and show focused review; do not count as detected absence. |

Store intervals, confidence, source evidence, anchor frames, uncertainty and release transitions in `SourceMotionAnalysis`. Authored annotations override ambiguous inference. Clip names can be weak hints or catalogue labels, never the deciding detector or the test oracle.

Validation labels must be independent of the detector. Include true negatives such as a low swinging hand, an airborne foot near the floor, a sliding attack and a weapon release. A detector returning zero contacts must not gain a clean score on a clip with required support. Report source-existing penetration separately so defects in source assets are not mistaken for target-only failures.

## 8 Target planning and temporal fitting

### Construct goals for the actual target

Build target goals from source relationships and the target's limb lengths, skin regions, torso dimensions and limits. Transfer a sole or palm relationship to the equivalent target surface; do not attach the target's bone origin to the source's contact point.

Plan required support and grip intervals before numerical solving. Determine which references can move: an authored chair may be fixed, a held weapon may be hand-driven, and a free prop may have an independently animated trajectory. Preserve the chosen root/actor policy. Record each permitted adaptation and its bounds.

For known incompatible fixed-parent reaches, report the constraint and geometric bound. More general necessary reach tests can reject obvious contradictions, but they do not prove full feasibility. An optimizer exceeding its budget is `Unresolved`, not `Incompatible`.

### Solve interacting constraints together

Use the baseline retarget as the reference action. Optimize permitted root/pelvis, spine, shoulder, limb and prop variables across intervals that include contact transitions. Use deterministic overlapping windows with explicit boundary conditions, or a whole-clip solve when appropriate. Window overlap must preserve shared pose and derivative constraints; simply blending two solved windows can break contacts.

The objective should retain normalized joint orientation, limb trajectories, body-relative relationships, facing, rhythm and action landmarks while minimizing unnecessary correction, correction velocity and correction acceleration. Preserve deliberate fast motion. Penalizing all animation acceleration would flatten attacks and landings; regularize the correction or its difference from source-derived motion instead.

Keep structural invariants and authored hard pins outside any user-adjustable weighted preference. Enforce required contacts, joint limits, supported collision constraints and root policy through explicit constraints, hierarchical solving or an appropriate constrained optimizer. A very large weight is not a hard constraint. Check required residuals after each accepted solve and after baking.

Distinguish imported or reviewed target joint limits from pose extrema observed in a source animation. Observed ranges are estimates useful for priors; they are not the target's anatomical limits and cannot alone prove a transfer infeasible. Record the origin and confidence of every limit.

Inferred relationships may have bounded adaptation, such as a shifted stance or changed patch on a surface. The permitted adaptation must be stated in the recipe and measured separately. Do not silently release a required contact, move a fixed socket, grow a limb or raise the whole character until a clearance score becomes green.

Use analytic IK for suitable initialization, calibrated bend directions and alternative deterministic seeds. Test continuation through intermediate proportions and longer windows for difficult poses. If local rotational increments use quaternion or exponential-map math, verify retraction, Jacobians and native Euler reconstruction against the known failed experiment. Rotation parameterization is one numerical choice within the complete solve.

Use sparse contact samples and spatial acceleration during optimization. Re-evaluate the actual deformed target mesh for acceptance. Classify eligible collision regions: body support, clothing, attached rigid items and source-existing overlap may need different explicit policies. Report maximum depth, affected area or vertex fraction and the worst time. Never turn off troublesome geometry invisibly.

### Numerical and performance requirements

Make iteration limits, tolerances, damping, line search, seeds and window schedule explicit and versioned. Prefer sparse or block-structured linear algebra as window size grows. Avoid building a dense whole-character-by-whole-clip normal matrix by default. Verify analytical or automatic Jacobians against finite differences on small independent fixtures before using them for performance.

Tie warm-start state to the clip, source/target dependencies, settings and solve generation. Sequential solve, resumed solve, batch solve and repeated solve should produce equivalent motion within documented numerical tolerance. Random scrubbing reads a prepared result and must not change the result according to playback history.

Measure preparation time, source-analysis time, solve time, curve fitting time, peak memory and native verification separately. Bound batch concurrency and memory. Do not promise interactive full-clip solving before measurement. Sliders can launch cancellable background previews with an explicit draft state; Apply requires the final accepted result for the exact current dependencies.

## 9 Required interaction behavior

### Feet and grounded locomotion

Preserve left and right stance intervals independently, including double support, heel-to-toe progression, toe pivots, slides and takeoff. Allow pelvis and articulated pose changes to fit reach. Use slope normals and the correct finite support geometry. Preserve jump height and airborne phases under the selected motion policy.

Separate static foot placement from preventing drift over time. Ground clearance, sole separation, plant drift and orientation are different metrics. Check the complete interval and the encoded interpolation between keys.

### Hands on the ground and body support

Solve the actual palm region, its orientation when constrained, arm reach and torso together. Handstands and crawling may transfer body support from feet to hands; no two-foot balance rule should force feet down in those phases.

For sitting, fit buttock and thigh regions to the actual seat reference. Add back, hand and foot constraints only when evidenced. A fixed high seat and short target legs may conflict with required foot-floor contact. Expose the conflicting relationship instead of inventing a footrest or stretching the legs.

For lying and rolling, preserve support across torso, hip, shoulder, arm or leg patches as indicated by the source. A body can be supported by a changing subset of patches. Do not pin every low body point simultaneously or lift the character until all contact has disappeared.

### Ledges and moving supports

Use bounded ledge geometry, grip location and below-edge phases. Separate actor travel from local limb motion. For a moving support, keep contact in support-local coordinates and transform it through the support trajectory. An apparently moving world-space hand can remain correctly planted on a moving platform.

### Two-hand weapons and objects

Represent one prop with two local grip frames and hand-contact intervals. For an independently animated prop, fit both arms to that same prop trajectory. For a prop driven by a primary hand or native attachment, derive the prop from that relationship and solve the other hand to its socket. When permitted, optimize a single shared prop transform with both arms and the torso.

Detect candidate shared grips from explicit attachment/socket tracks first, then sustained relative hand/prop motion and geometry. Two hands moving together are not proof of holding a weapon. If the item is absent, an inferred interaction frame can be offered for review with uncertainty; it is not recovered geometry or a verified native article.

Support release and regrip intervals and hand orientation. Both grips move coherently when the user moves or aims the weapon. Fixed socket spacing is fixed. Only a prop explicitly defining an adjustable grip permits a grip-distance slider. Finger closure requires a separate supported finger/contact capability.

### Self-contact and paired actions

Use skin or barycentric surface anchors for contacts such as a hand resting on a hip or touching a shoulder. Both sides can move as the body changes pose. World-locking that hand would destroy the intended relationship.

Throws, grapples or interactions with a second actor require the partner's trajectory and an appropriate paired-actor capability. A single imported skeleton cannot establish missing partner motion. Preserve unknown relationship data and report the unsupported portion precisely.

## 10 Sliders options and authoring

Keep the existing compact comparison workflow: authentic source beside editable target, independent clocks with optional linkage, source animation selection, basic fit controls and collapsed advanced settings. Native technical details belong in diagnostics and adapter policy, not repeated format-selection steps.

All controls must affect the real prepared target result, persist in the recipe, invalidate the correct jobs and pass Undo/save/reopen checks. Neutral pose-slider values must preserve the default automatically fitted result with the same contact/root options; this differs from the raw transfer used in transform-only identity tests. Keep established numeric ranges where valid and calibrate new ranges against fixtures before advertising them.

| Control | Required semantics |
| --- | --- |
| Automatic fitting and contacts | Enable the relevant planning stages with explicit capability results. |
| Arm spacing | A target pose preference; shared grips and required palm contacts remain constraints. |
| Stance width | Adjust the target stance plan while maintaining supported foot intervals. |
| Pelvis height and body lean | Bounded pose preferences subject to reach, support and collision. |
| Free-hand reach | Change unconstrained reach while keeping contact-bound hands correct. |
| Motion fidelity | Implement a calibrated action-preservation objective before exposing the slider. |
| Contact mode and interval | Inspect and correct plant, slide, roll, support, grip and release meaning. |
| Contact transition | Change allowed blending without moving protected action events silently. |
| Weapon position and aim | Transform the shared object and its fixed grips coherently. |
| Root travel or in-place | Use an explicit actor/root representation and target capability. |
| Mirror | Reflect motion, roles, contacts, bend directions and supported prop/event semantics together. |
| Preview speed | Change playback only; source data, exported timing and Apply validity remain unchanged. |
| Export speed and trim | Explicitly remap source time, contacts, events and output duration. |
| Correction layer | Persist target-side pose and contact adjustments without modifying the original source. |

Contact strength must not become a way to weaken required constraints and still label the result validated. In a constrained case, show which relationship limits the requested slider value. Keep source-rig settings, target-rig settings, shared batch preferences and per-clip overrides distinguishable.

Ordinary bone posing and automatic key creation must continue to use the existing editor command path. Solver-generated keys must not be mistaken for a new user edit during their own Apply transaction. A manual correction invalidates its affected validation receipt and must be rechecked.

## 11 Source library batches and dependency safety

Group the initial batch by one exact source model/rig revision, with multiple selected authentic clips. Prepare geometry and mapping once, but analyze each clip's contact intervals independently. Share per-source clip analysis across targets; share per-target rig preparation across clips.

Cache keys must include source model/skin/geometry and animation revisions, coordinate/time conventions, relevant layers, scene/prop/actor references, profile/calibration and detector version. Target results additionally include target deform/geometry revision, limits, interaction bindings, root policy, effective settings, solver/baker versions and native encoding policy. Display names are not stable identity.

Preserve per-row settings inheritance and mixed values. A row override invalidates that row; a shared setting invalidates dependent rows. Target edits, Undo/Redo, source re-registration, support edits and version changes invalidate affected results. Reject obsolete asynchronous completions both when publishing previews and at Apply.

Keep distinct states for queued, analyzing, solving, draft, validated, review, unresolved, incompatible, stale, cancelled and applied. Native status is separate. Apply all selected must require all selected results to be ready; applying passed rows is an explicit user choice. Commit selected target clips, recipes and receipts in one undoable document transaction. Redo restores stored results rather than rerunning the solver.

Resolve output names and collisions before Apply. Batch cancellation must leave completed source assets intact and never publish a late target result. Durable output receipts must preserve the existing interrupted-save recovery behavior. Do not claim cross-file atomicity when only a single-container transaction is implemented.

## 12 Native evaluation baking and saving

The universal Core is an evaluation and editing model linked to authentic native documents. Importing a Brawl resource keeps its Brawl identity and all preserved resource families. A Melee animation used on that character is interpreted through its source adapter, fitted in Core and baked through the target document's supported adapter. Ordinary Save follows the target document context; users should not repeatedly choose an unrelated intermediate format.

Separate source decoding, Core evaluation, retargeting, destination encoding, container repacking, game build and game testing by format/version capability. Future adapters for Melee, Ultimate, Rivals II and the requested external games must use these contracts; the retarget rewrite itself does not implement every parser.

Use rational timing and preserve the difference between sample count, last displayed sample, last key, end-exclusive duration and a loop-closing key. Keep preview looping separate from native loop flags. Preserve step boundaries, Euler winding, quaternion continuity, additive reference semantics and supported animated translations/scales. Dense samples alone can miss a fast rotation if sampling aliases the source motion.

Preserve FighterPort's existing Brawl native precision corrections, including the integer-float decoder, fixed/I12 correction and editor-frame-zero versus native ApplyCHR-plus-one convention. The current source identifies these as established invariants. [S1, S2]

Bake with the destination's actual interpolation and representability. Fit keys and tangents against solved motion, then evaluate intermediate times. Key reduction must budget end-effector, body-surface and contact error, including propagation through parent bones. Recheck after native compression, not merely before it.

Use adaptive subframe refinement around fast motion, impacts, contact boundaries, high curve curvature and large residual changes. Retain existing fixed withheld samples as regression probes, not the only sampling strategy. Report minimum temporal resolution, tested coverage and any unbounded intervals. A sampled pass is not a mathematical guarantee for every continuous instant unless a conservative interval bound is established.

If an integer-key native format cannot express a timing boundary or required contact accuracy, return an encoding/timing limitation under the chosen policy. Do not silently round timing, relax the product tolerance or claim that denser internal samples solve an unrepresentable output.

Freshly decode every accepted native output using an independent native path where available. Compare motion, contacts, sample/time metadata and supported channels. Preserve unrelated resources by native name/type/index/length/hash; include empty placeholders. Edited target data can change, but immutable source bytes and untouched native siblings cannot.

Material, visibility, texture, morph, attachment, actor and event tracks have independent compatibility. Retain original source companions even when a target cannot use them. A correct bone animation does not establish working native weapon articles, hitboxes, events or full fighter gameplay.

## 13 Acceptance contract

Retain the current frozen contact and penetration bounds and their scale definitions. Source `RetargetAcceptance` currently uses position and penetration scale fractions of `0.0001` and an orientation bound of `0.001` radians. Do not silently substitute a larger tolerance to improve the pass count. Check the exact checkout's definitions and native error budget before implementation. [S4]

New detector or action-quality metrics need independently labeled fixtures, declared units and thresholds frozen before solver tuning. Until calibrated, report them as measured but unrated. A changed contact interpretation needs evidence and a versioned annotation, not an automatic weakening of expected behavior.

### Independent measurement layers

1. Source interpretation: contact presence, type, onset/release, reference identity and confidence calibration against independent labels.
2. Baseline transfer: rest identity, coordinate equivariance, root policy, detailed motion and topology mapping.
3. Solved target motion: required support/grip residuals, limits, penetration, action fidelity and temporal continuity.
4. Baked Core curves: the same checks at withheld fractional times, seams and event boundaries.
5. Fresh native decode: supported values/tangents/flags, timing, evaluated motion, contacts and preserved resources.
6. Displayed product behavior: actual deformed meshes from low/side/front/rear views, source isolation, sliders, clocks, Apply, Undo and save/restart.

Use maximum and percentile errors, worst time, per-region measurements and expected-contact coverage. A zero residual with zero relevant samples cannot pass a required-contact case. Report solver non-convergence, unavailable context and unsupported representation separately.

Migrate the current readiness rule that requires `requiredSamples > 0`. Distinguish required-contact acceptance, confirmed absence of required contacts and unknown coverage. A supported entirely free or airborne clip can become Apply-ready when source-mode coverage, action, relevant collisions, timing and native validation pass. Its contact result is `NotApplicable`, not a fabricated grounding pass. Empty detection without supporting evidence remains `Review`. Preserve all frozen numeric bounds wherever contacts are required.

### Required fixtures

| Group | Acceptance cases |
| --- | --- |
| Identity and coordinates | Same rig; different file units; rotated global axes; A/T poses; nontrivial parent rests; full-turn animation. |
| Proportions both ways | Short/wide to tall/narrow and back; unequal arm/leg ratios; long arms; short legs; altered shoulder width and torso depth. |
| Topology | Renamed bones; extra spine/twist/helper joints; missing optional fingers; target-only and source-only branches. |
| Contacts | Walk/run; double support; jump/landing; slide; toe pivot; crouch; crawling; handstand; brief impact and release. |
| Body interactions | Fixed seat; back support; prone/supine lying; roll; stand-up transition; bounded ledge. |
| Objects | Two fixed grips; one-hand release/regrip; moving prop; attached prop; missing prop context; unreachable fixed grips. |
| Native behavior | Actual writer/reader interpolation, compression, held boundaries, rational rate conversion, loop seam and preserved siblings. |
| Workflow | Single/batch equivalence; reverse direction; settings inheritance; stale result rejection; cancellation; one Undo/Redo; restart. |

Procedural fixtures must be independently authored and redistributable. Expected contacts and target observations cannot be generated by the same detector or solver being tested. Use the available private Mario/Snake assets only through their existing local registration and ignored outputs. Exact game/build identity remains a separate claim.

Retain the entire 26-case baseline. Prioritize its four unresolved rows and large down-motion review residuals. Add the new contact modes with proper fixtures instead of assuming the existing clips cover them. Verify strict clean status for each direction; a forward-only fix is incomplete.

## 14 Implementation sequence

Keep one current implementation step at a time inside M2. These gates order the work; they are not reasons to stop after writing documents.

| Gate | Concrete work | Exit evidence |
| --- | --- | --- |
| V0 Baseline | Reproduce available results; freeze failures; connect a versioned backend seam. | Existing paths still work; baseline and unsupported capabilities recorded. |
| V1 Rig and transfer | Derived rig descriptors, chain bindings, reference-space conversion and detailed channels. | Identity, coordinate, topology and both-direction proportion tests pass. |
| V2 Source meaning | Independently labeled contact corpus, state analysis and interaction graph. | Positive/negative mode and interval results, with confidence and missing context. |
| V3 Temporal fitting | Target planning, limits, action objectives, whole-body contacts and temporal windows. | Grounded and down-motion improvements meet unchanged bounds in both directions. |
| V4 Interactions and controls | Seat/lying/ledge/prop/self-contact, real controls and corrections. | Appropriate procedural/native cases and displayed controls pass. |
| V5 Bake and release gate | Native curve fitting, batch integration, migration, output recovery and final review. | Full stated capability matrix, fresh native receipts and working product flow. |

The first execution should create a real vertical slice: use the new backend from the existing UI/CLI, preserve identity transfer, and fit a meaningful grounded or down-motion case in both directions through bake and native reopen. Do not spend an entire implementation turn adding empty interfaces or repeating this specification.

Keep the old backend for comparison until the new backend passes the V5 acceptance gate. At each gate update the current plan, actual validation record and development status with commands, measured results, open cases and the next concrete step. Do not mark M2 complete while its required feature and fixture coverage remains open.

## 15 Implementation and deliverables

Use the accompanying implementation brief in the real FighterPort checkout. It requires current-source inspection, selective reuse, implementation and validation. This plan supplies the detailed design and references.

Expected implementation deliverables are working Core/CLI/Desktop integration, versioned data migrations, meaningful fixtures, source and target previews, live functional controls, same-source batches, native save/reopen evidence and synchronized repository documentation. Retain ordinary editor features and the native format ownership model.

If a native worker, private fixture or platform dependency is unavailable, complete the portable and independently testable work and state the exact remaining acceptance gap. Never fabricate a game run or treat an unavailable fixture as passed. The finished handoff should report behavior changed, evidence, remaining conflicts and the next current task.

## 16 Source references

Repository observations are pinned to the inspected revision. Re-check the checkout and external component versions before adopting code.

- [S1] [FighterPort current milestone](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/docs/plans/current.md) and [repository instructions](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/AGENTS.md).
- [S2] [FighterPort retarget validation](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/docs/RETARGET_VALIDATION.md).
- [S3] [FighterPort architecture](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/docs/ARCHITECTURE.md) and [automatic retargeting contract](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/docs/AUTOMATIC_RETARGETING.md).
- [S4] [Current transfer](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/src/FighterPort.Core/Retargeting.cs), [contact solver](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/src/FighterPort.Core/Contacts.cs) and [workflow and acceptance](https://github.com/DoctorFlux/fighterport-studio/blob/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d/src/FighterPort.Core/RetargetWorkflow.cs).
- [R1] [Godot retarget modifier source](https://github.com/godotengine/godot/blob/ed1daf0bf001b61586d9930840f2f1394092c079/scene/3d/retarget_modifier_3d.cpp), [importer rest fixer](https://github.com/godotengine/godot/blob/ed1daf0bf001b61586d9930840f2f1394092c079/editor/import/3d/post_import_plugin_skeleton_rest_fixer.cpp) and [license](https://github.com/godotengine/godot/blob/ed1daf0bf001b61586d9930840f2f1394092c079/LICENSE.txt).
- [R2] [Godot RetargetModifier3D documentation](https://docs.godotengine.org/en/stable/classes/class_retargetmodifier3d.html) and [retargeting tutorial](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/retargeting_3d_skeletons.html).
- [R3] [Godot BoneMap](https://docs.godotengine.org/en/stable/classes/class_bonemap.html), [TwoBoneIK3D](https://docs.godotengine.org/en/stable/classes/class_twoboneik3d.html) and [IterateIK3D](https://docs.godotengine.org/en/stable/classes/class_iterateik3d.html).
- [R4] [ozz two-bone IK contract](https://github.com/guillaumeblanc/ozz-animation/blob/744eb9d99f606eda849acb0b1204f7a3dc20bca1/include/ozz/animation/runtime/ik_two_bone_job.h), [tests](https://github.com/guillaumeblanc/ozz-animation/blob/744eb9d99f606eda849acb0b1204f7a3dc20bca1/test/animation/runtime/ik_two_bone_job_tests.cc), [foot sample](https://github.com/guillaumeblanc/ozz-animation/blob/744eb9d99f606eda849acb0b1204f7a3dc20bca1/samples/foot_ik/README.md), [optimizer](https://github.com/guillaumeblanc/ozz-animation/blob/744eb9d99f606eda849acb0b1204f7a3dc20bca1/include/ozz/animation/offline/animation_optimizer.h) and [license](https://github.com/guillaumeblanc/ozz-animation/blob/744eb9d99f606eda849acb0b1204f7a3dc20bca1/LICENSE.md).
- [R5] [HSDLib retarget and batch](https://github.com/Ploaj/HSDLib/blob/85567e40797de3c820a55476eca54c704921a848/HSDRawViewer/Tools/Animation/RetargetUtil.cs), [remapper](https://github.com/Ploaj/HSDLib/blob/85567e40797de3c820a55476eca54c704921a848/HSDRawViewer/Tools/Animation/AnimationRemapper.cs), [IK solver](https://github.com/Ploaj/HSDLib/blob/85567e40797de3c820a55476eca54c704921a848/HSDRawViewer/Tools/Animation/IKSolver.cs), [bakery](https://github.com/Ploaj/HSDLib/blob/85567e40797de3c820a55476eca54c704921a848/HSDRawViewer/Tools/Animation/AnimationBakery.cs), [native encoder](https://github.com/Ploaj/HSDLib/blob/85567e40797de3c820a55476eca54c704921a848/HSDRaw/Tools/FOBJ_Encoder.cs) and [license](https://github.com/Ploaj/HSDLib/blob/85567e40797de3c820a55476eca54c704921a848/LICENSE).
- [R6] [WickedEngine retargeting](https://github.com/turanszkij/WickedEngine/blob/4f93f3fd4f337aeb38dbfe410701b83024ccdd42/WickedEngine/wiScene.cpp) and [license](https://github.com/turanszkij/WickedEngine/blob/4f93f3fd4f337aeb38dbfe410701b83024ccdd42/LICENSE.txt).
- [R7] [MeshRet released workflow](https://github.com/abcyzj/MeshRet/blob/96ccd8c8b5ee102a1b067780c0300c30da7d925d/README.md), [model](https://github.com/abcyzj/MeshRet/blob/96ccd8c8b5ee102a1b067780c0300c30da7d925d/model/retnet.py), [armature assumptions](https://github.com/abcyzj/MeshRet/blob/96ccd8c8b5ee102a1b067780c0300c30da7d925d/utils/body_armatures.py), [configuration](https://github.com/abcyzj/MeshRet/blob/96ccd8c8b5ee102a1b067780c0300c30da7d925d/meshret_config.yaml) and [license](https://github.com/abcyzj/MeshRet/blob/96ccd8c8b5ee102a1b067780c0300c30da7d925d/LICENSE).
- [R8] [BrawlCrate CHR0 implementation](https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlLib/SSBB/ResourceNodes/Animations/CHR0Node.cs), [animation codec](https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlLib/Wii/Animations/AnimationConverter.cs) and [license](https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/LICENSE).
- [R9] [ssbh_lib animation data and codec boundary](https://github.com/ultimate-research/ssbh_lib/blob/08f8bc4470a7d480dfc9b64d7e08e97f3820fed7/ssbh_data/src/anim_data.rs), [binary animation format](https://github.com/ultimate-research/ssbh_lib/blob/08f8bc4470a7d480dfc9b64d7e08e97f3820fed7/ssbh_lib/src/formats/anim.rs) and [license](https://github.com/ultimate-research/ssbh_lib/blob/08f8bc4470a7d480dfc9b64d7e08e97f3820fed7/LICENSE).
