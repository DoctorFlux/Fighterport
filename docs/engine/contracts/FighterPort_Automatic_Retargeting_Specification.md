> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# FighterPort Automatic Animation Retargeting

Implementation specification for automatic contacts, body fitting, motion controls and batch reuse

Revision 1 | 19 October 2026 | Existing standalone application | Current milestone M2

## 1 Purpose and required outcome

We want to select animations from their authentic source character, choose another already-rigged target, and obtain editable target animations that fit the target's proportions. The normal workflow must detect contacts automatically, preserve the action, and expose a compact set of useful sliders and options. It must also process multiple selected animations from the same source model in one batch.

This specification extends the existing FighterPort library, retarget recipe, shared contact solver, comparison views and native output path. It defines the remaining product behavior and acceptance work. Add it to the existing repository as `docs/AUTOMATIC_RETARGETING.md` and link it from the amended master plan and the single current execution task.

The required outcome is reliable automatic adaptation for supported anatomy and interactions, with measured contact accuracy and clear exceptions. Some target bodies cannot satisfy a fixed combination of contacts. Missing chairs, weapons, ledges or actor trajectories can also make the source ambiguous. The system must retain uncertainty and distinguish an unresolved numerical solve from a demonstrated geometric impossibility.

### Core requirements

| ID | Required behavior |
| --- | --- |
| RT01 | Preserve imported bone identities, hierarchy, rest and inverse-bind transforms, skin weights and target proportions. |
| RT02 | Keep authentic source models, animations, native identities, timebases and immutable ancestry. |
| RT03 | Automatically identify body regions and temporal contacts using geometry and motion evidence. |
| RT04 | Preserve grounded soles, palms, knees, sitting support, lying support and intentional releases. |
| RT05 | Distinguish planted, pivoting, sliding, rolling, gripping and brief-impact behavior. |
| RT06 | Solve target pelvis, torso, shoulders, limbs and supported prop motion together. |
| RT07 | Preserve both grips on a shared two-handed object and respect its native attachment model. |
| RT08 | Provide intuitive fitting sliders, motion options, reusable presets and per-clip overrides. |
| RT09 | Retarget selected clips from one source character as an inspectable, cancellable batch. |
| RT10 | Preserve timing and gameplay locks unless an explicit motion-edit policy changes them. |
| RT11 | Bake ordinary target keys and validate actual reopened native output. |
| RT12 | Keep preview, library, solving, native writing and game acceptance as distinct capability claims. |

Routine operation should not require annotating every clip. Known rig and prop profiles should be reusable. Manual review should focus on uncertain mappings, missing context and genuinely conflicting requirements.

## 2 Reconcile the current implementation first

The current records describe substantial working functionality. The first implementation action is to inspect the actual standalone repository, source definitions and acceptance artifacts, then identify the next incomplete behavior. Historical bootstrap instructions must not recreate the application.

### Recorded baseline

| Area | Recorded state | Implementation consequence |
| --- | --- | --- |
| Desktop and Core | .NET 10 x64, Avalonia, owned rendering, typed scene and curve evaluation | Extend the existing solution and interfaces. |
| Native boundary | Data-only Brawl adapter and optional net472 x86 worker | Keep native types and synchronous IPC outside the modern UI and playback. |
| Rig controls | Reviewed Mario and Snake profiles, FK, four-limb IK, explicit support constraints | Reuse the current controls, calibration and shared solver. |
| Contact solve | Analytic initialization plus damped least squares; bake-only correction | Extend detection, temporal constraints and interaction planning around this solver. |
| Source library | Persistent reviewed Brawl pairs and authored projects, immutable caches, independent previews | Extend the existing library; do not build a second catalogue or viewer. |
| Detailed transfer | Reviewed fingers and compatible shared branches in `RetargetRecipe.BoneMapping` | Preserve detailed supported rotation, translation and scale policies. |
| Automatic support | Whole-body inference is experimental on recorded Mario and Snake cases | Improve and measure it; do not present it as absent or universally complete. |
| Native output | Precise CHR0 writing, fresh reopen and bounded insertion into new outputs | Retain the exact-float path and separate export success from contact success. |
| Other games | Independent planned adapters and limited toolchain discovery | A configured source or target does not establish import or export support. |

These states come from the current architecture, library, testing and capability records [D1-D6]. The testing record reports 327 checks for `artifacts/library-final-regression/results.json`. That is a recorded historical run, not a new result or a sum to combine with older suites.

### Authority and stale instructions

The newest explicit user amendments in `docs/MASTER_PLAN.md` govern the retained older handoff. The master snapshot includes the source browsing, detailed-bone, comparison/support and execution-order amendments that the earlier existing-rig master lacks.

The historical `FIGHTERPORT_STANDALONE_CODEX_START_HERE.md` still says to create a new workspace, copy the earlier master over `docs/MASTER_PLAN.md`, and start M0 then M1. Those instructions are obsolete for this existing application. Preserve its standalone and existing-skeleton rules, while continuing the current implementation.

`ANIMATION_LIBRARY.md` records substantial implemented library functionality and experimental whole-body transfer, while contact conflicts, fractional drift and broader support coverage remain unresolved. The current task's remaining acceptance work is still relevant. The older development status sends work to GPU recovery and DPI, while the later explicit user amendment defers both. Reconcile completed behavior and remaining failures against code and receipts. Keep GPU/DPI open and deferred, the backend provisional, further SHP0 authoring deferred, and SCN0 inactive.

`RETARGET_VALIDATION.md` is a required repository input because the supplied documents reference its native and contact measurements. Read its actual local copy before using those measurements. If it is absent, reconstruct the evidence through the existing tests and record the gap without inventing previous results.

The broad statement that animated scale and non-body translation are losses also needs refinement: the newer library record reports supported detailed scale and proportion-scaled local translation. Retain supported channels and identify the precise remaining body-scale, helper and native-format limitations.

## 3 Preserve the current architectural boundaries

Keep the existing six modern components: `FighterPort.Core`, `FighterPort.Desktop`, `FighterPort.Rendering`, `FighterPort.Adapters.Brawl`, `FighterPort.Cli` and `FighterPort.Tests`. Retain the separately built `FighterPort.BrawlWorker` only for the legacy native dependency boundary. Rigging and retargeting remain Core modules unless a real dependency or deployment boundary justifies another assembly [D1, D2, D5].

The imported deform skeleton, editor control overlay, semantic profile, retarget recipe and native output must retain separate identities. Automatic setup creates virtual controls on existing bones. It does not generate a replacement skeleton, force a common Goku or Brawl rig, change bind poses or reweight vertices.

One editable target `EditorDocument` owns its editing state and Undo history. Read-only source and result documents use the same Core evaluator and owned renderer but have independent cameras, clip selections and clocks. Optional linking uses actual elapsed seconds and rational rates. Sharing implementation does not mean sharing mutable playheads or selections [D1-D3, D11].

Rendering consumes prepared data and evaluated poses. Native decoding, contact analysis, solving, baking, hashing and synchronous worker calls stay outside playback ticks and animation filtering. New controls must preserve this boundary.

New type names in this specification describe responsibilities. Codex must inspect existing definitions and extend them instead of adding parallel abstractions with equivalent state.

## 4 Retain authentic sources and separate derived data

Each library clip must remain tied to its reviewed source model, skeleton, native resource path and index, game and build labels, verification status, decoder identity, raw hashes, coordinate conventions, timebase and geometry-affecting companion layers. A build label is not verified merely because it was entered during registration.

Preserve `AnimationLibraryStore`, content-addressed raw blobs, prepared projects, relocation proofs and ancestry. Intact cached originals can support offline original locations. Corrupt or missing trusted inputs must fail explicitly. Retargeting one source onto several targets must never change that source or another target's result [D3].

Source contact analysis is a versioned derivative of authentic source motion. It records what the source appears to do. Target body shape influences target planning and correction, not the historical source observation. Store target-specific slider settings and corrections in a recipe rather than editing source contact evidence.

Use the existing `native.brawl.v2.integer-floats` provider identity. Reject older v1 prepared entries according to the established re-registration policy. Do not relabel already altered float data as corrected. Reprepare from preserved original inputs [D3, D7].

### Dependency layers

| Derived data | Inputs that must invalidate it |
| --- | --- |
| Prepared source | Raw inputs, provider/decode version, preparation schema and policies |
| Source contact analysis | Source clip and geometry revisions, active geometric layers, calibration, support evidence, detector configuration |
| Target contact plan | Source analysis, target rig and geometry, mapping, target supports and props, fitting preset |
| Target bake | Contact plan, destination content revision, motion policies, slider overrides, solver version |
| Native validation | Bake, writer/encoder version, native target identity, container dependencies |

Cache keys must distinguish a source-only analysis from a target-specific result. Changing one target's stance width must not invalidate or mutate the authentic source cache.

## 5 Anatomical mapping and body surfaces

Resolve exact verified profiles by skeleton and calibration fingerprints. Otherwise combine names, hierarchy, parent correspondence, rest axes, segment lengths, symmetry, bind geometry and skin weights. Display uncertain suggestions for review and preserve the evidence for each correspondence.

Extend `RetargetRecipe.BoneMapping`. Keep fingers and compatible shared branches beyond the main body roles. Preserve target-only native helpers and report source-only, target-only and incompatible branches. Retargeting a body must not erase the current reviewed detailed-channel behavior [D1, D3].

Generate contact patches on the actual skinned surface: heel, toe, sole, palm, finger pads, knee, elbow, buttocks, front/back/side torso and head. A patch needs stable surface identity, local coordinates or weighted vertices, an orientation frame, calibration and confidence.

Use skin-weight regions and anatomical geometry to transfer patches between different mesh topologies. Vertex numbers are not correspondence. Exclude unrelated weapons, capes and large hair from anatomical stature and limb calibration. Retain ambiguous segmentation as uncertain rather than forcing it into a body region.

Evaluate the same supported geometric layers used by the source or target preview. If SHP0 changes a body surface, its active revision and phase must affect analysis and collision validation. Untransferred source morphs remain source data; do not assume a target has matching morph topology.

Bone lengths and rest data are known from the rig. True articulation limits often require reviewed profile data. Observed source angles provide working-range evidence, not complete proof of the target's joint limits.

## 6 Contact observations and target plans

Extend the existing interval and surface model to record the following concepts.

| Record | Required information |
| --- | --- |
| Contact observation | Source patch, other participant, interval, mode, local anchor or region, orientation, evidence, confidence and provenance |
| Support definition | Plane, bounded surface, mesh or moving object; transform over time; valid extent and phases; known or inferred status |
| Target contact plan | Mapped target patch, target support/prop binding, tolerance, permitted adjustment region and reach diagnostics |
| Constraint policy | Authored locks, inferred-contact policy, root/timing policy, allowed movement and priority |
| Retarget receipt | Input fingerprints, effective settings, mapping and solver versions, baked channels, residuals, adaptations and losses |

Authored pins, reviewed explicit contacts and inferred observations need separate provenance. Authored pins remain hard under automatic fitting. A user can intentionally edit or release a pin, but the solver cannot silently relax it.

Approving an inferred interval or contact mode does not automatically author an exact fixed anchor. Store semantic acceptance, permitted anchor adaptation and an authored position/orientation lock as distinct decisions.

Contact modes must include free, approaching, brief impact, planted, pivoting, sliding, rolling, gripping and releasing. Their constraints differ. A flat sole constrains more movement than a toe pivot; a sliding hand preserves surface contact while permitting tangential travel.

Store stationary anchors in the contacted object's coordinates. A foot on a moving platform moves in world space but remains stable on the platform. A self-contact anchor follows the relevant deforming surface rather than an unrelated rigid torso frame.

Keep an anchor stable throughout its interval. Recreating it from each corrected frame would allow accumulated drift. For rolling, explicitly migrate support across the body instead of pretending that a fixed patch remains planted.

## 7 Automatic detection algorithm

Use the authentic evaluated source to generate candidate patch/support relationships. Prefer provided geometry, attachments and explicit intervals. Infer missing supports only with recorded confidence.

For every candidate, calculate normalized surface distance, normal alignment, relative normal and tangential velocity, angular motion, persistence, approach/departure behavior and supporting pose evidence. Use time units rather than frame-dependent thresholds. Choose reference scale from anatomical dimensions or the relevant limb/patch, not an arbitrary whole-scene bounding box.

A stationary hand in midair is a negative example. A foot moving near the floor during a kick is not necessarily supporting the body. A brief contact must not disappear because a standing-contact detector imposes a long minimum duration.

Use temporal scoring with hysteresis and lookahead. A state machine or semi-Markov model can stabilize transitions such as heel contact, sole support, toe pivot and release. Record uncertainty near ambiguous transitions. Whole-clip evidence should improve detection without changing the source keys.

Fit support hypotheses across the relevant interval. Do not estimate a fresh floor from the lowest vertex every frame; that would make the floor follow a jump or inverted pose. A sitting pose without a chair may remain ambiguous between seated support, crouching and unsupported motion.

For contacts against a moving object, transform source patch samples into that object's local frame before evaluating relative motion. For self-contact, compare corresponding skinned surface samples. Keep contact preservation and unintended penetration as separate measurements.

Use authored overrides as explicit constraints on classification. A forced release interval should remove an inferred plant there. An authored hand pin must remain active even when the detector's confidence is low.

### Detection procedure

```text
evaluate authentic source pose and supported geometry layers
resolve source profile and calibrated surface patches
resolve known trajectory and scene evidence
generate nearby patch to surface or patch to patch candidates
compute distance orientation relative motion and temporal features
decode contact states with lookahead and persistence
apply explicit interval overrides with recorded precedence
store observations confidence and source dependency signature
```

Calibrate the detector against separately annotated fixtures, including negative cases. Contact validation cannot simply trust whichever intervals the detector happened to return.

## 8 Whole body solving and continuity

Retain the current proportion-aware pose transport and analytic direct-chain initialization. Extend the existing damped least-squares correction with temporal planning, explicit constraint priorities and shared prop variables where supported. Introduce a different numerical backend only for a demonstrated limitation.

Solve permitted pelvis/root movement, spine, shoulders, limbs and prop motion together. Use the initial retarget as the reference for pose, action and rhythm. Enforce native structural invariants, authored locks, supported joint limits, required contacts, scene restrictions and selected root/timing policies.

A large scalar weight does not guarantee a hard constraint. Preserve required pins through the existing hard-variable/constraint mechanisms, or add explicit prioritized residual handling. Validate every required constraint afterward. Inferred adaptation can use bounded slack, but that slack must be measured and reported.

Plan target footprints or support placement consistently over each interval. Once a stationary anchor is selected, keep it fixed relative to its support. Root-locked motion and reachable foot placements must be planned jointly; neither may silently change to conceal a conflict.

Optimize overlapping temporal windows with stable boundary conditions and a final sequence refinement when necessary. Penalize unwanted variation in the correction while retaining intentional impacts, held poses and full spins. Do not smooth away a punch to improve a generic continuity score.

Maintain elbow/knee bend direction through near-straight configurations using reviewed anatomical axes, source bend evidence and a deterministic branch established in canonical offline bake order. Never derive the branch from the previous GUI pose or the user's scrub direction. Random-order seeking must evaluate the same baked pose. Independent per-frame pole guesses can create flips.

Use efficient collision proxies during solving and actual evaluated mesh checks for acceptance. Refine sampling near active contacts, rapid motion and suspected intersections. Finite sampling establishes checked accuracy within the recorded coverage; it is not a proof against every possible continuous-time intersection.

If a solve fails, try appropriate alternative seeds, smaller proportion-change steps or longer windows. Report solver non-convergence separately from an actual reach bound or structural contradiction. Do not label every numerical failure physically impossible.

### Numerical procedure

1. Evaluate the source at canonical rational times, including supported geometry layers, and construct the reviewed proportion-aware FK seed on the target.
2. Build a contact graph whose participants are target body patches, supports and props. Plan adaptable inferred anchors within their allowed semantic regions; retain authored exact anchors.
3. Form positional and angular residuals in each contact's proper frame and project them onto only the degrees of freedom constrained by its mode. Add collision, joint-limit, motion-fidelity and correction-continuity terms.
4. Linearize target forward kinematics around the current pose. Use the existing analytic seeds and damped least-squares updates; extend the constraint treatment where needed so authored locks, allowed root variables and limits are respected explicitly.
5. Apply bounded rotation and translation updates with a line search or trust-region safeguard. Re-evaluate the actual nonlinear contacts and penetration after each candidate update. Stop only under the recorded convergence and residual criteria.
6. Carry stable overlap boundaries through temporal windows, refine difficult intervals, then bake supported native channels and validate independently sampled output.

For a rigid moving support, relative patch velocity must subtract both support translation and the velocity induced by support rotation at the patch. Constant world height or small world speed alone is insufficient. Detector features and solver residuals must be normalized consistently so a model's unit scale does not change classification or priority.

Style controls contribute preferences within the feasible set. Increasing a soft weight is not an acceptable substitute for implementing the required constraint hierarchy. Keep optimizer state transient and store reproducible inputs, settings and baked results.

## 9 Grounding sitting lying and ledges

### Feet and locomotion

Use target sole geometry, including shoes, when planning ground contact. Solve pelvis, knees and ankles together. Preserve heel strikes, toe-off, turns, deliberate slides and airborne release.

An in-place walk needs a supplied or inferred virtual world trajectory: supporting feet normally move backward relative to the character. Validate the composed world motion, then remove that trajectory only when the selected output mode and target adapter support it. Existing actor-controlled trajectory packaging remains an explicit capability gap until implemented and tested.

Offline retargeting cannot guarantee planted feet at arbitrary runtime speeds. Record the assumed trajectory and speed. Gameplay movement must match it or provide its own compatible runtime adjustment.

### Palms and body support

For push-ups and handstands, support the palm or finger patches and solve shoulders, torso, pelvis and limbs. Do not force grounded motion to remain upright or require foot support.

For sitting, support the buttocks and relevant thigh surfaces on an actual or inferred seat. Add backrest, hand and foot contacts only when evidenced. Fixed seat height and short legs can make simultaneous seat and floor contacts incompatible.

For lying, use the appropriate back/chest/hip/head/limb patches. Account for target body thickness. During rolling, the active support patch changes. During a slide, tangential motion remains permitted.

### Ledges and bounded surfaces

Preserve the master plan's phase distinction. A hanging body may legitimately lie below a ledge top. During get-up, relevant surfaces must clear the ledge edge and top. An infinite floor applied throughout the clip would lift or distort the hanging pose.

A top plane without reviewed extent does not define the ledge's full geometry. Store bounded extent when known, separate hanging and above-ground phases, and report missing extent. Do not invent a wall or edge from a motion label. Moving supports and reviewed ledge geometry must graduate through explicit acceptance stages.

## 10 Two handed objects and body contact

A two-handed item is one shared object with two grip frames. Both target hands must satisfy their relationship to that object. Solve torso, shoulders, elbows and wrists together, along with permitted prop movement. Preserve intended aim, swing and release timing according to the selected policy.

Detect grasp intervals from geometry or known sockets, stable hand-to-prop transforms, hand orientation and motion history. Use palm/grip frames rather than wrist-joint centers. Map the source's meaningful grip regions onto the target prop's actual grip definitions.

| Prop behavior | Required solve and output behavior |
| --- | --- |
| Attached to a native hand socket | Derive the prop from that attachment and solve the other hand against the resulting grip. |
| Independently animated | Include supported prop channels in the solve and preserve them through target export. |
| Fixed in the world | Keep the object fixed and adapt the character within its movement policy. |
| Missing from the input | Permit an inferred virtual interaction frame with uncertainty; do not claim recovered weapon geometry or native article output. |

Two hand positions alone do not uniquely establish a rigid object's twist or geometry. Stable hands can also represent clasped hands or a guard. Orientations and motion history help, but uncertainty remains.

Respect grip changes and releases. Keep finger animation where mapping supports it; a two-hand wrist solve alone does not prove correct finger wrapping. Add per-prop grip profiles and finger constraints as supported anatomy expands.

For self-contact, preserve the target body region and clearance. A hand touching a larger abdomen must reach the target abdomen surface. A bounded semantic region, such as the cheek, may permit small anchor relocation. Fixed world pins and rigid sockets do not gain that freedom automatically.

The current compatible grip/throw bone mapping is useful groundwork, but it does not establish complete two-hand object support. Native body export, prop/attachment export and actual game behavior require separate results.

## 11 Mixamo style sliders and options

Use a preview-and-adjust workflow: choose a source clip, view it on its authentic character, view the fitted target, adjust understandable controls and apply the result. Adobe documents speed and arm-spacing adjustments in Mixamo [R1]. The controls, ranges and semantics below are proposed FighterPort requirements, not a claim about Mixamo's complete feature set or slider defaults.

Default retargeting preserves the full source action and its timing. Neutral sliders mean no additional artistic change beyond automatic proportion and contact fitting. The target's skeleton and proportions remain unchanged at every setting.

### Fit to this body

These ranges are initial UI design limits to calibrate against the acceptance set. A range does not guarantee that every value is feasible for every pose.

| Control | Proposed range and default | Meaning |
| --- | --- | --- |
| Automatic fitting | On | Apply supported proportion and contact correction. Off gives the existing FK comparison and retains ordinary playback. |
| Arm spacing | Minus 15 to plus 30 degrees; 0 | Bias shoulder abduction around characterized axes. Solve around required grips and hand support. |
| Stance width | 50 to 150 percent; 100 | Scale the automatically planned target stance around the pelvis. Select a consistent anchor per support interval. |
| Pelvis height | Minus 10 to plus 10 percent of target leg length; 0 | Add a preferred pelvis offset, then solve the complete body and contacts. |
| Body lean | Minus 20 to plus 20 degrees; 0 | Add a torso/pelvis pose preference while retaining required support. |
| Free hand reach | 80 to 120 percent; 100 | Adjust intended free-hand reach relative to the shoulder without changing bone lengths. |
| Motion fidelity | 0 to 100; 100 | Prefer the baseline transferred motion among feasible solutions. This does not promise unchanged joint angles. |

Arm spacing must not break a weapon grip. Stance width must not move a reviewed world pin. A pelvis adjustment must not lift a planted foot. Show that a requested preference is constrained, or report the exact conflict when the selected policy requires an unattainable value. The displayed applied offset and residual must agree with the result.

### Contacts and objects

Default to automatic detection with explicit authored locks preserved. Provide named per-interval edits: Force contact, Release, Allow slide, Allow roll and Change support. Display the edited interval and affected patch in the viewport.

An advanced detection-sensitivity control may change inferred candidates, but it must show the resulting contact changes. Keep it separate from fitting influence. A proposed 0 to 100 percent inferred-contact influence, default 100, changes only soft inferred objectives. It cannot weaken an authored pin or convert a failed required contact into a pass.

Allow bounded pelvis, spine and shoulder adjustment by default within the reviewed profile. Expose limits and root restrictions in advanced settings. Use calibrated physical surface clearance; a manual offset is an explicit target-space or normalized adjustment, not a hidden gap under all contact patches.

For a shared object, expose Move weapon and Aim weapon, with zero offsets initially. Move its complete frame and both grips coherently. Fixed native sockets remain fixed relative to the prop. Grip-spacing edits are available only when an explicit adjustable grip definition permits them; they are unavailable for two fixed sockets.

Quality presets may change preview sampling or iteration budgets. They must not weaken final acceptance tolerances. Label an approximate preview until the corresponding final result has been validated.

### Timing and motion

| Control | Default | Contract |
| --- | --- | --- |
| Preview speed | 1 times; proposed 0.25 to 2 times | Change playback only. Keys, contact intervals, duration, source FPS and native loop flags remain unchanged. |
| Export speed | 1 times; proposed 0.25 to 2 times | Explicitly retime the derived motion, contacts, props and supported events together. Show resulting duration and sample count. |
| Trim | Full original interval | Explicitly select source start and end, including the closing-key policy. Show protected event conflicts. |
| Mirror | Off | Use reviewed left/right mapping and mirror relevant trajectories, contacts, props and supported events. |
| Root motion | Existing reviewed policy | Separate actor travel, scene/body orientation and pelvis pose. Persist the exact policy. |
| Travel distance | 100 percent; proposed 50 to 150 | Explicitly adjust supported locomotion travel and inferred footfall planning together. |
| In place | Off | Extract selected actor-travel channels and retain their trajectory as paired data; validate reconstructed world motion. |
| Additional smoothing | 0; proposed 0 to 100 | Apply constrained motion editing without silently smearing held poses, impacts, releases or protected action frames. |

An unsupported operation is disabled with a concrete reason. Do not expose a working-looking toggle that changes only preview while implying a valid export. Mirror is a semantic transform of the animation and relationships, not negative mesh scale. Asymmetric props or event semantics can block it.

Preserve vertical jumps when extracting horizontal actor travel unless another explicit policy changes them. In-place preview can show the reconstructed world trajectory or a clearly labeled moving-ground view. A static character with walking feet cannot simultaneously satisfy stationary world foot anchors without the corresponding trajectory.

### Presets and editing scope

Use the following explicit inheritance: verified rig defaults, saved source-to-target fitting profile, batch overrides, then per-clip overrides. Authored locks and protected timing remain constraints outside this preference hierarchy.

A saved fitting profile is keyed to reviewed source/target rig revisions and relevant prop/root policies. Same character names are not sufficient. Reuse a skeleton mapping across a compatible outfit only after validating the mapping; the changed mesh still needs current surface calibration.

Show Current animation or Selected animations beside the controls. Mixed selected values display Mixed. Apply to selected creates explicit row overrides; Edit shared preset updates inherited values; Use shared value removes an override. Reset fitting restores neutral artistic offsets without removing reviewed contact locks.

Freeze each effective recipe in the result. Later edits to a saved preset must not silently alter an already applied animation. Persist presets, overrides, contact edits and dependency versions through project save and restart.

### Slider processing

On a slider change, mark the corresponding uncommitted result stale and disable Apply. Coalesce the drag into one recipe edit. Debounce or supersede previous preview requests, solve in bounded background work, and publish only a result matching the current source, target and settings fingerprints.

The last result may remain visible with a stale label while work runs. The source pane continues to show the authentic source. If recipe-edited source preview is useful for mirror or retiming, label that view explicitly and retain Original source as an available comparison.

Run the final bake after the edit settles or when requested. Ordinary playback samples prepared motion. It must not launch a contact optimization on every frame.

## 12 Multiple animation batches

Batch retargeting is a first-class extension of Use on This Fighter. It applies reviewed shared preparation and a versioned fitting recipe to selected immutable source clips, with separate contact analysis and results for each clip.

### User workflow

1. Open a source character and model revision in Animation Library.
2. Multi-select animations or select all compatible animations within the current source.
3. Choose the target fighter and shared fitting options.
4. Run Analyze and retarget with the selected animation count.
5. Review each row's fitted result, settings, contact confidence and conflicts.
6. Apply the selected ready results as one target-document Undo transaction.
7. Save the project and, when requested, export new native outputs through the target adapter.

The initial batch scope is multiple clips bound to the same verified source model revision. A future mixed-source queue can group work by source revision. It must not assume that similarly named source characters share skeletons, props or native formats.

| Reused across compatible selected clips | Kept separate for each clip |
| --- | --- |
| Source preparation and source rig calibration | Contact intervals, modes, support phases and confidence |
| Target rig setup, geometry and reviewed limits | Root interpretation, timebase, holds and protected events |
| Reviewed detailed bone mapping | Prop participation, grip/release intervals and scene bindings |
| Shared fitting preset and destination policy | Effective overrides, target bake, residuals and native receipts |
| Immutable source input verification | Source clip identity, curve revision and active geometric layers |

Analyze a walk, jump, sit, lie and weapon clip independently. Do not reuse a walk's planted-foot timeline for the other clips. Source analysis remains reusable for another target when its complete source dependencies are unchanged.

### Batch records and row states

Capture a batch manifest before work starts. Include stable source/model/clip IDs and revisions, target content/profile/geometry revisions, reviewed mappings, shared settings, per-clip effective recipes, software versions, planned names, destinations and validation policy.

The batch table should expose clip name, native indexed identity, source/target timing, inherited or overridden settings, contact types, status, principal issue and proposed target name. Friendly names are labels rather than identity.

Use distinct states for queued, analyzing, solving, preview available, validated, needs review, solver unresolved, incompatible constraints, stale, cancelled and applied. Track native export verified or failed separately from body/contact validation. A green native export does not conceal an unresolved body contact.

Only the selected row requires active source/target GPU views. Use bounded background concurrency and the existing worker ownership rules. A hundred selected clips must not open a hundred preview documents or flood the legacy worker with conflicting writes.

### Invalidation and cancellation

A per-clip override invalidates that row. A shared setting invalidates rows that inherit it. A target geometry change invalidates affected target plans and bakes. A source geometry, provider or detector revision invalidates the relevant source analysis and its dependent results.

Before publishing a result and again before Apply, verify the request generation and dependency snapshot. Target edits, Undo, a new source revision, changed settings, target replacement, closure or cancellation must prevent a late result from being applied.

Conservatively reject Apply if the target document revision has changed. A finer dependency check may preserve unaffected candidates only when it is implemented and tested. Never let an asynchronous completion overwrite newer target edits.

Cancellation before commit leaves the committed target unchanged. Prepared successful candidates may remain available for inspection or verified cache reuse. A cancelled or stale candidate is not an applied result.

### Apply and naming

Default to applying the complete selected set only when its required rows validate. If one row fails, retain successful candidates and expose Apply passed rows as an explicit action showing the actual subset and count. Do not silently discard failed clips and report the entire batch successful.

Resolve names and collisions before committing. Derived animations default to new independent names. Preserve native indexed source identities when display names collide. A target slot requiring a specific native name needs an explicit assignment decision; an automatic suffix must not silently change gameplay meaning.

Creating retargeted clips does not overwrite target clips, assign gameplay slots or run Fill Missing. Those remain explicit separate operations.

Commit selected keys, recipes, source ancestry, contacts, receipts and resulting selection through one document command. Undo restores the complete prior target state. Redo restores the validated stored result, rather than rerunning a possibly changed solver. Exported files are outside document Undo.

### Native batch output

Prefer a single new target container when the supported adapter can insert the selected motions into its existing group. Stage the output, reopen it in a fresh worker, validate every inserted motion and all preserved siblings, then commit the new file. Existing single-motion insertion evidence does not establish multi-motion insertion support.

For multiple output files, plan destinations and prevent overwrite races, stage and validate all outputs, then commit each new file with a durable result manifest. Multi-file filesystem commits are not one atomic transaction. An interruption can leave a partial batch; report the exact committed paths and hashes and support safe recovery of newly created outputs.

Preserve source/cache export guards throughout. Never use an original source or immutable prepared input as a destination. Unknown partial export outcomes must be resolved before retrying.

## 13 Exact timing curves and native preservation

The library stores authentic native motion plus Core's typed curve representation. It is not a catalogue of animations prematurely converted to CHR0. Each adapter owns source decoding and target encoding [D1, D3, D5-D7].

Retain the established Brawl time conversion: editor sample zero evaluates through the native ApplyCHR convention at native time plus one; native zero is the bind-pose convention. Sample count, last displayed sample, last key, end-exclusive duration and optional loop-closing key are separate fields and tests.

Convert source to target rates using rational time. Preserve duration when the destination can represent it; report nonintegral sample-count conflicts under the selected native policy. Do not round silently. Native playback-loop flags remain separate from preview looping.

Keep Euler winding, including intentional rotations beyond one revolution, Hermite behavior, quaternion/spherical channels and holds. Resampling must not turn stepped animation into continuous interpolation. Unsupported held, mixed-interpolation, split-tangent or scale policies need exact capability reports.

For explicit export speed factor r, evaluate source time at r times the new elapsed time, adjusted by the selected trim origin. Transform contact boundaries, prop motion and supported events consistently. Recompute tangents in the correct destination time units. Protected fixed game-frame events can conflict with this operation; require an explicit supported timing policy rather than moving them silently.

Linked comparison of a derived clip must use the recipe's source-time function, such as `sourceTime = trimStart + exportSpeed * targetTime`. Equal elapsed times alone are incorrect after trim or retiming. Keep authentic source keys and timebase unchanged. Store the time domain of each override explicitly: source seconds, target seconds or protected target game frame. Transform source-semantic intervals consistently; retain or report conflicts for target-time locks according to their policy. Test trim and retime together so the original pose, fitted pose, contact markers and prop phase align.

Retain `NativeChrFloats.cs` integer-based decoding and `PreciseChrEntry.cs` fixed/I12 payload correction after native encoding. Do not route corrected keys through dense L1/L4 regeneration that changes tangents, or revive x86 float conversions that alter signaling-NaN bit patterns. The recorded bounded decode paths cover fixed, I4, I6, I12, L1, L2 and L4 cases [D3, D7].

Exact expected native key/value/tangent preservation and independent evaluated motion tolerance are different checks. Record both. Compare reopened native output at withheld fractional and closure samples, not just the generated bake keys.

Retain every unrelated resource's native name, type, index, byte length and content hash. Include zero-byte redirects. Current original BRRES and supplied PAC insertion evidence does not establish arbitrary PCS editing or creation of every missing native group.

Cross-rig material, visibility, texture and morph channels must be preserved as source data and reported precisely when not transferred. Existing source playback support is not target transfer support. Keep SCN0 inactive. A virtual weapon visualization is not a native article binding.

## 14 Numerical contracts and measured acceptance

Define a versioned acceptance profile before tuning. Read current tolerances and failures from the actual `RETARGET_VALIDATION.md` and test definitions. Do not loosen thresholds to turn the current matrix green.

Every report must state target/native units, anatomical reference scale, sampled times, active geometry layers, contact provenance, reviewed or inferred support and exact policy. Report the worst sample and per-clip failures as well as aggregate statistics. A new measurement without an established numeric bound is measured but unrated, rather than passed.

### Required measurements

| Measurement | Definition and interpretation |
| --- | --- |
| Contact separation | Distance between the evaluated target surface patch and its planned contact surface/anchor. Bone-center proximity is insufficient. |
| Plant drift | Maximum tangential displacement from the interval's fixed support-local anchor; exclude permitted pivot, slide or roll degrees of freedom. |
| Unintended penetration | Maximum signed penetration depth, affected surface fraction and duration against defined scene/body geometry. Do not test a ledge as an infinite plane. |
| Grip error | Position and orientation residual of each hand against its own frame on the same prop; include the worst simultaneous two-hand error. |
| Orientation error | Angular residual only for axes constrained by the contact mode. Do not lock free pivot axes accidentally. |
| Pose and action fidelity | Deviation from the proportion-aware baseline, protected action samples and meaningful end-effector/aim paths. |
| Temporal behavior | Contact onset/release error, correction continuity and unintentional limb-branch flips, with intentional discontinuities retained. |
| Native fidelity | Exact supported values/tangents/flags, evaluated pose and mesh residuals, timing and preserved resource hashes after independent reopen. |
| Source and target integrity | Source/cache hashes and target hierarchy, bind, inverse-bind, bone-length and skin-weight checksums. |
| Repeatability | The same evaluated pose under random seek order; repeatable canonical bakes under the recorded deterministic configuration. |
| Performance | Preparation/analysis/solve/bake times, memory, queue depth and normal playback behavior measured on named hardware and fixture sizes. |

Published numeric thresholds must be justified by native precision, actual target scale and fixture requirements. As an initial calibration experiment, evaluate contact-position budgets of 0.1 to 0.5 percent of target anatomical height and orientation budgets of 1 to 3 degrees where orientation is constrained. These are proposed investigation ranges, not existing FighterPort tolerances or a declaration that visible errors in that range are acceptable. Small hand/weapon features may require tighter local budgets.

Choose and freeze the accepted profile using independently annotated positive and negative examples and visible native output. Required authored pins may need stricter tolerances than inferred relationships. Full-mesh collision acceptance uses its own budget rather than inheriting a loose contact-fitting weight.

Keep solver accuracy, encoding degradation and final decoded contact accuracy separate. A native encoder's quantization does not automatically widen the product contact tolerance. If an output format cannot retain required accuracy under the selected policy, report the limitation.

For detection, measure interval precision/recall per mode and body region, false plants, missed brief impacts and onset/release errors against reviewed labels. Report difficult negative cases separately so long idle intervals cannot dominate the score. A detector returning no contacts must not receive excellent contact accuracy merely because the solver has nothing to enforce.

For numerical solving, report accepted, accepted with explicitly permitted adaptation, missing context, unsupported operation, unresolved solve and proven incompatible constraints separately. Record distributions and worst frame/subframe, not only averages.

### Required fixture matrix

| Group | Cases and required evidence |
| --- | --- |
| Anatomy | Renamed bones, unequal limb/torso ratios, rotated bind axes, shoes, body thickness, reviewed detailed branches and unsupported-helper diagnostics. |
| Ground motion | Walk, run, stop, turn, toe pivot, crouch, jump, landing, in-place reconstruction and intentional foot slide. |
| Body support | Sit/stand with fixed seats, missing-seat ambiguity, lying front/back/side, rolling, crawling, kneeling, push-ups and handstands. |
| Interactions | Moving platform, bounded ledge phases, two-hand weapon, fixed prop, changing grip, release, self-contact and missing object ambiguity. |
| Timing | Held poses, Euler spins, rational rate changes, final fractional samples, loop closure, preview-speed isolation and protected-event conflicts. |
| Sliders | Neutral equivalence, actual target change, hard-pin preservation, mixed values, reset, constrained extrema and two-grip preservation. |
| Batch | Shared preparation with separate timelines, per-clip overrides, single/batch equivalence, mixed success, duplicate names, cancellation, stale completion and one Undo. |
| Native and UI | Fresh decode and exact floats, all preserved siblings, actual displayed meshes, independent clocks, low/side/front/rear views, save/restart and new-output recovery. |

Use original redistributable fixtures for automated regression and reviewed local private inputs for bounded native evidence. Maintain Mario-to-Snake and Snake-to-Mario coverage across the existing nine-motion matrix. Add the new modes with suitable fixtures instead of assuming those nine clips contain every required interaction.

At native acceptance, use existing quarter-sample all-vertex checks and withheld samples, then refine near fast transitions and contacts. Separate solver-space, baked Core, fresh native decode and displayed geometry results. A good solve can be damaged by baking or encoding; a numerically good file can still have a rendering problem.

Test batch export failure between file commits and verify the recovery manifest. Test a target edit during solving, a setting change during a slider drag, source re-registration, Undo and window closure. Late work must never regain Apply eligibility.

Do not create tests that merely repeat implementation formulas. Use independently constructed geometry, expected contacts, analytic motions, actual native reopening and observable document/UI state. Broaden testing to resolve concrete remaining risks.

## 15 Implementation sequence and completion

These descriptive stages remain inside the current M2 work. They do not reset the existing project or resolve the older master plan's inconsistent historical milestone numbering.

| Stage | Work | Exit evidence |
| --- | --- | --- |
| RT0 Establish baseline | Read current code, amendments, tests and receipts; reconcile the single current plan; identify existing failures. | Current capabilities and gaps recorded with reproducible commands. |
| RT1 Recipes and useful controls | Extend existing recipe persistence, neutral fitting controls, timing separation, per-clip overrides and stale-result protection. | Real target changes, source isolation, save/restart and preview-speed invariance. |
| RT2 Batch reuse | Add same-source multi-selection, shared preparation, independent clip jobs, row diagnostics and one-command Apply. | Single/batch equivalence, mixed-result handling, cancellation, collisions and one Undo. |
| RT3 Automatic support | Improve source patch/contact classification, target planning, temporal correction and full-mesh validation. | Measured grounded, hand, seat, lying, rolling and bounded ledge behavior; unresolved cases retained. |
| RT4 Interactions and motion options | Add reviewed shared props, self-contact, moving supports and supported explicit retime/mirror/root options. | Both grips, attachment output, releases, timing policies and motion-option conflicts verified. |
| RT5 Native and product acceptance | Validate new multi-motion native output and all supported slices through real controls and fresh reopening. | Per-clip receipts, source/native integrity, displayed evidence and synchronized docs. |

Implementation can interleave these slices when dependencies require it. Sliders and a queue are not completion of contact correctness. An improved numerical detector is not completion of the library, batch controls or native output.

The default path should run automatically on supported confident cases. Low-confidence scene inference, incompatible props and unsupported anatomy produce focused review. Do not require repetitive per-frame annotation, and do not fabricate missing scene context to maintain the appearance of full automation.

A release is complete for a stated capability set only when the user can register the source, browse authentic clips, select one or many, adjust effective settings, inspect correct target results, apply and undo them, save/reopen, and obtain supported native outputs with receipts. State remaining gaps by feature and fixture.

## 16 Keep project documentation consistent

Add this specification as `docs/AUTOMATIC_RETARGETING.md`. Link it from the governing amendments in `docs/MASTER_PLAN.md`. Retain one `AGENTS.md` and one `docs/plans/current.md`; update their relevant instructions without creating a second competing execution plan.

Update `ANIMATION_LIBRARY.md` for controls, batch behavior, inheritance, source identity and reproducible CLI/UI usage. Update `RETARGET_VALIDATION.md` with actual measured per-clip contact/native results. Update `TESTING.md`, `FORMAT_MATRIX.md` and `DEVELOPMENT_STATUS.md` with the same bounded capability state.

Update `ARCHITECTURE.md` only where real boundaries or persisted contracts change. Keep `UI_REFERENCE.md`, `TRANSITION_FITTING.md` and target-profile documentation consistent with the new controls and output semantics. Retain existing licenses and audit any new dependency before adding it.

Replace stale historical handoff instructions that would recreate the application or overwrite the amended master. Preserve the existing rules and reference history without promoting old priority statements over newer explicit user direction.

Keep GPU recovery/per-monitor DPI acceptance visibly deferred and the rendering backend provisional. Further SHP0 authoring remains deferred; SCN0 remains skipped. Transition fitting, slot assignment, Fill Missing, other native adapters and actual game acceptance remain separate product work unless explicitly included in a later task.

## 17 Repository verification commands

The following command forms are documented in the supplied project records. Resolve actual repository paths and inspect current script usage before running them. Use new ignored artifact directories so fresh results cannot be confused with historical receipts.

```powershell
./scripts/Build.ps1 -Native -Publish
$env:FIGHTERPORT_BRAWL_WORKER = Join-Path $PWD `
  'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
$fighterPortTests = './tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe'
& $fighterPortTests artifacts/retarget-current-core
& $fighterPortTests artifacts/retarget-current-native --native
$fighterPortCli = './src/FighterPort.Cli/bin/Release/net10.0/FighterPort.Cli.exe'
& $fighterPortCli library-list artifacts/private-library
./scripts/Test-Library.ps1 -OutputDirectory artifacts/retarget-current-library -InspectAngles
```

`-Publish` in this build command creates the existing local self-contained application output. It does not authorize a remote repository or public release.

The tests accept repeated `--private <model-package> <separate-motion-package>` and the documented `--retarget-matrix <prepared-Mario> <prepared-Snake>` mode. Use real locally supplied inputs and inspect the current harness contract. Do not invent private paths or put game data into version control.

Use `retarget-map <source-project> <target-project>` to inspect detailed mapping. Existing single-clip library reuse has this form:

```powershell
& $fighterPortCli library-use artifacts/private-library $sourceId $clipId `
  artifacts/target.fighterport artifacts/new-target.fighterport
```

Obtain the IDs from the actual catalogue. New batch CLI commands, if added, require explicit documentation and tests; this specification does not claim that batch flags already exist.

Run actual desktop acceptance sequentially using `Test-Desktop.ps1` and the relevant documented `-VerifyNativeControls`, `-VerifyBodyContacts`, `-VerifyTransport`, `-VerifyAuthoring` and `-VerifyNativeMaterials` options. Reuse the existing appropriate Mario `Wait2 [` and Snake `Wait3 [` display fixtures. Use low, side, front and rear views and save/restart checks for the new fitting and batch controls.

Only report commands actually run, fresh artifact paths and observed outcomes. The supplied documents establish a recorded baseline; this design handoff is not an execution of FighterPort's Windows application or native tests.

## 18 Research basis and scope of claims

The architecture in this document is a FighterPort implementation proposal grounded in the project's existing services. The following primary sources support specific design choices. They do not establish that FighterPort already implements them or that any method perfectly handles every body and animation.

| Ref | Primary source and supported point |
| --- | --- |
| R1 | Adobe, Animate 3D characters with Mixamo. Describes animation selection, preview, speed and arm-spacing adjustments. |
| R2 | Adobe, Mixamo FAQ. Establishes the service's humanoid-character scope and rigging constraints. |
| R3 | Contact-Aware Retargeting of Skinned Motion, 2021. Supports using surface geometry and contact relationships in retargeting. |
| R4 | ReConForM, 2025. Studies contact-aware optimization with geometric relationships; supports contact and penetration evaluation across changed proportions. |
| R5 | Epic, Fix Foot Sliding with IK Retargeter. Documents speed-based foot-plant workflow, supporting temporal contact evidence. |
| R6 | Epic, Full-Body IK. Documents coupled body solving and per-bone control rather than isolated end-effector corrections alone. |
| R7 | Autodesk, Character hand floor contact. Illustrates calibrated hand-floor contact markers rather than treating a wrist center as a palm surface. |
| R8 | MeshRet, 2024. Studies mesh-aware interaction preservation; its research setup is not a universal native-game animation importer. |
| R9 | Skinned Motion Retargeting with Spatially Adaptive Interaction Guidance, 2026. Supports bounded target contact-anchor adaptation; its reported scope does not establish full external-prop support. |
| R10 | Spatial Relationship Preserving Character Motion Adaptation, 2010. Supports retaining relationships among body and environment features. |

Primary URLs reviewed on 19 October 2026:

- [R1] https://helpx.adobe.com/creative-cloud/help/animate-characters-mixamo.html
- [R2] https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
- [R3] https://arxiv.org/abs/2109.07431
- [R4] https://arxiv.org/html/2502.21207v1
- [R5] https://dev.epicgames.com/documentation/en-us/unreal-engine/fix-foot-sliding-with-ik-retargeter-in-unreal-engine
- [R6] https://dev.epicgames.com/documentation/unreal-engine/control-rig-full-body-ik-in-unreal-engine
- [R7] https://help.autodesk.com/cloudhelp/2022/ENU/Maya-CharacterAnimation/files/GUID-94E431AE-381F-4F09-8C55-9B0A4BAF8CB4.htm
- [R8] https://abcyzj.github.io/MeshRet/
- [R9] https://arxiv.org/html/2605.19355v1
- [R10] https://www.research.ed.ac.uk/en/publications/spatial-relationship-preserving-character-motion-adaptation/

### Supplied project records

These reference labels point to the user's uploaded snapshots. Files with a numbered upload suffix correspond to their canonical repository names; inspect the repository versions before implementation.

| Ref | Supplied file | Role |
| --- | --- | --- |
| D1 | MASTER_PLAN(1).md | Governing amended scope, architecture and priority |
| D2 | ARCHITECTURE(1).md | Current application boundaries and solver baseline |
| D3 | ANIMATION_LIBRARY.md | Source identity, existing workflow, cache and transfer evidence |
| D4 | current(1).md | Single current-task snapshot and active acceptance scope |
| D5 | TESTING.md | Existing commands, fixtures, recorded runs and limits |
| D6 | FORMAT_MATRIX.md | Bounded import, preview, edit, transfer and export capabilities |
| D7 | LICENSE_AUDIT.md | Native dependency and float-preservation audit |
| D8 | DEVELOPMENT_STATUS(1).md | Historical implementation state requiring priority reconciliation |
| D9 | SHP0_VALIDATION.md | Supported morph geometry and native acceptance boundaries |
| D10 | VIEWPORT_VALIDATION.md | Actual display evidence and open backend/DPI gates |
| D11 | UI_REFERENCE.md | Compact editing and independent comparison UI |
| D12 | TRANSITION_FITTING.md | State-aware fitting and boundary-time policies |
| D13 | TARGET_PROFILES.md | Target configuration and supported toolchain distinctions |
| D14 | TARGET_RIVALS2.md | Separate Rivals II target/toolchain requirements |
| D15 | BRAWL_PARITY.md | Brawl native behavior and parity scope |
| D16 | FIGHTERPORT_STANDALONE_EXISTING_RIG_MASTER_PLAN(1).md | Earlier master retained as history |
| D17 | FIGHTERPORT_STANDALONE_CODEX_START_HERE(1).md | Earlier bootstrap handoff requiring correction |

`RETARGET_VALIDATION.md` was referenced by these records but was not included among the supplied snapshots. Do not infer its contents or treat this document's proposed metrics as its existing results.
