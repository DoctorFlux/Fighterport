> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# FighterPort Standalone — Updated Full Product Handoff

**Revision:** 2026-10-18 — existing-skeleton IK/FK controls are the authoritative design.  
**Basis:** User-provided `FIGHTERPORT_STANDALONE_CODEX_HANDOFF_2026-10-18(1).md`, retained in full below except conflicting rig-creation language.  
**Status:** Requirements, not a claim of implementation.  

## Governing amendment — no skeleton creation in the normal workflow

**Imported game characters already have their own deform skeleton and skin weights.** FighterPort's `Set Up IK/FK` operation must recognize and use **those existing bones**, adding an optional editor-only **control layer**, not creating/replacing a deform rig, changing weights or forcing a shared Brawl/Goku skeleton. Retargeting between two already-rigged characters is separate from IK controls and FK posing. The three operations can be run individually or composed. Known, verified skeleton families get automatic control setup and mapping; ambiguous ones get a very short guided review. Model assets with no skeleton can be shown as static meshes, but building/skinning a new skeleton is *not* a core dependency or automatic surprise.

This amendment governs interpretation of every remaining section, including references to general rig authoring, generic project scaffolding and long-term optional skinning capabilities. **Preserve the original game skeletons, native hierarchy, bind/inverse bind transforms and weights.**

---

# FighterPort standalone Codex handoff

**Date:** 18 October 2026  
**Purpose:** Build a new desktop application for complete platform fighter character authoring, including native asset editing, source animation reuse, retargeting, transition fitting and target-specific packaging.  
**Status:** Implementation specification with researched integration candidates. It does not claim that the new application or integrations have been implemented or tested.  
**Relationship to existing plans:** Carries forward FIGHTERPORT_GREENFIELD_MASTER_PLAN.md dated 18 October 2026. The latest explicit requirement adds first/last-frame transition fitting to the target fighter's idle, fall or another chosen animation. Older plans that start from a BrawlCrate application shell are superseded for this new project.

## 1. Governing product decision

Create a genuinely independent FighterPort Studio application in a new codebase. Do not fork, copy the complete source tree of, or continue the application shell from DoctorFlux/fighterport-studio, DoctorFlux/fighterport-studio-next or BrawlCrate. Existing repositories remain useful read-only references for behavior, native formats, defects and regression expectations. Selective use of a library or algorithm is allowed after its dependencies, license and upstream revision are recorded.

The application must own its project model, user interface, viewport, animation clock, commands, edit history and build orchestration. It must not require Godot or run as a Godot project. Godot can inform the design of bone mapping, rest-pose alignment, animation evaluation and constraints. Its MIT license permits reuse subject to its terms; code portability and third-party dependencies still need inspection. [S7][S8]

A fighter project is the main user-facing document. Models, costumes, motion assignments, gameplay, sound, effects, portraits and destination builds belong to that project. One fighter may have several target-game branches. Native game files remain first-class resources; a universal intermediate must not erase native information.

The normal user journey is:

1. Create Fighter and choose a target game/build.
2. Add a local game source, extracted dump, mod build, supported disc image or asset folder.
3. Browse its real characters, outfits, models and animations with original-source previews.
4. Select an ALREADY-RIGGED target model, characterize its existing skeleton and automatically set up non-destructive FK/IK animation controls for those native bones.
5. Choose source motions; retarget, fit transitions and assign the target's required animation slots.
6. Edit model/costumes, gameplay, audio, VFX and UI artwork.
7. Validate the project, build the appropriate native package or SDK input, and playtest through the supported target toolchain.

Simple and Advanced modes must edit the same data. A normal user should not need to understand PAC paths, bone hashes or build scripts to perform a supported routine action.

## 2. What the repository audit establishes

The existing main branches were inspected as reference material:

| Repository | Inspected revision | Relevant finding |
| --- | --- | --- |
| DoctorFlux/fighterport-studio | 49ecd9a7192ab79f00bf81883d5259d352ab256d | README identifies a complete BrawlCrate application foundation using .NET Framework 4.7.2/x86. |
| DoctorFlux/fighterport-studio-next | 96a4e0e81b1ad79837c307d57197ecd4c756179b | Project still targets net472/x86. Its current milestone improves native presentation; broader game adapters and complete fighter packaging remain pending. |

Sources: [S1][S2][S3]. These are source/documentation findings, not newly executed application tests.

Preserve the lessons, not the old application's dependency graph:

- Model and animation resources are often separate files or different branches of a container.
- A clip name or matching bone names do not establish which original model owns a motion.
- Native interpolation, bind transforms, skinning, fractional samples and root trajectories require explicit handling.
- Viewport update frequency does not prove that displayed playback is smooth.
- A saved game path does not implement that game's reader or writer.
- Old test counts are not evidence that the new application passes.

BrawlLib includes older UI/rendering/runtime dependencies. Use an audited data-only extraction where feasible; a narrow legacy worker is acceptable when needed. The modern desktop process must remain x64 and must not embed a WinForms BrawlCrate window.

## 3. Target scope and support accounting

### 3.1 Game priorities

**Primary complete-character destinations:** Brawl, Project M/Project+, Melee, Ultimate and Rivals of Aether II.

**Additional Smash scope:** Wii U and 3DS require distinct adapters and verification. Keep Smash 64 in the roadmap with unresolved capabilities visible. A Wii U parser does not establish 3DS support.

**Dragon Ball donor sources:** Xenoverse 1, Xenoverse 2, FighterZ and Sparking! ZERO. Model/animation import is the first purpose of these adapters. Native character creation for the Dragon Ball games would require separate target builders.

**Rivals 1:** Keep sprite sheets, sounds, GML and move descriptions as a separate 2D/source workflow for the user's Goku material. Sprite frames do not automatically supply a 3D rig, and imported gameplay needs explicit target translation.

### 3.2 Format registry

Every adapter identifies game, platform, build, format signature/version and operation. Track these independently:

- Container browsing and extraction.
- Native reading and dependency resolution.
- Animation evaluation and rendering.
- Editing and retargeting.
- Preservation of unedited or opaque data.
- Native writing and container repacking.
- Target packaging, installation and actual game testing.

Use statuses such as Planned, Experimental, Verified and Unsupported with an evidence record. An operation can be verified for one format version and unsupported for another. Do not show one undifferentiated “game supported” check mark.

### 3.3 Initial format inventory

These are integration and research scopes, not promises of existing FighterPort support.

| Family | Resources to inventory |
| --- | --- |
| Brawl/PM/P+ | PAC/PCS/ARC, BRRES, MDL0, TEX0/PLT0; CHR0, SRT0, PAT0, CLR0, VIS0, SHP0, SCN0; moveset data, REL modules, sound and effects. |
| Melee | HSD/DAT models and skeletons, joint animation and fighter action resources, textures, fighter data, SSM/HPS sound and disc/build structures. |
| Smash Wii U | NUD, NUT, VBN, OMO, animation PACs, MTA, ACMD/MTABLE and associated parameters/resources. |
| Smash 3DS | Separate platform-specific containers and model/motion/material resources; verify actual variants before listing them as compatible with Wii U. |
| Ultimate | NUMDLB, NUMSHB, NUSKTB, NUMATB, NUANMB, NUHLPB, NUMSHEXB/ADJB and NUTEXB; archive resolution, PRC, scripts, audio, effects, UI and target manifests. |
| Rivals II | Installed Modkit templates and schemas; skeletal mesh and baked animation import assets, Lua/data assets, skins/palettes, portraits, audio banks, effects and Workshop build state. |
| Xenoverse 1/2 | EMD, ESK, EAN, EMB, EMM and related character, action, effects and sound dependencies; assess each game/version separately. |
| FighterZ/Sparking! ZERO | Version-specific Unreal archives/packages, mesh/skeleton/animation/texture data and game-specific metadata or animation codecs. |
| Interchange | Verified glTF/GLB/FBX/DAE paths as needed; OBJ for static geometry; images and audio. Interchange is an option, not the mandatory path between every native format. |

BrawlCrate, HSDRaw, Smash Forge and the SSBH projects are concrete references for this inventory. CUE4Parse and Xv2CoreLib are candidates for appropriate donor-source adapters. [S4][S9][S12][S13][S21][S22]

## 4. Architecture and initial technology decision

Use **C#/.NET 10, Windows x64 first, and Avalonia** as the proposed baseline. .NET 10 is an active LTS release as checked on the research date. Avalonia provides desktop UI and OpenGL control infrastructure, while a graphics binding such as Silk.NET can provide access to graphics APIs. These are building blocks; none provides FighterPort's complete 3D editor automatically. [S24][S25][S26]

Complete a measured viewport spike before freezing the rendering backend:

- GPU skinning, textured mesh and two independently displayed characters.
- Orbit/pan/zoom, perspective/orthographic view and bone picking.
- Transform gizmos and overlays with correct depth behavior.
- Animation playback while scrolling/searching a large metadata catalogue.
- Resize, minimize/restore, DPI changes, resource disposal and recoverable rendering failure.
- A distributable Windows x64 build.
- Measured displayed pacing on the recorded hardware; do not claim universal 60 FPS.

Pin the dependency versions actually tested. Choose a stable binding release based on the spike, rather than assuming a development branch is suitable. UI thread, worker thread and GPU context ownership must be explicit.

### 4.1 Keep the initial solution small

Start with these responsibilities. More assemblies should be introduced only for a real dependency or deployment boundary.

| Component | Owns |
| --- | --- |
| FighterPort.Core | Source identities, fighter project, scene/skeleton/clip data, commands, revisions, rational timing, validation and job contracts. |
| FighterPort.Desktop | Avalonia shell, shared document/selection state, panels, timeline and user input. |
| FighterPort.Rendering | GPU resources, materials, skinning, camera, picking and debug overlays behind a viewport interface. |
| FighterPort.Adapters.Brawl | Native resource bridge and serialization. A separate worker executable may host legacy dependencies. |
| FighterPort.Cli | Headless import/evaluate/build/validate entry points using the same services. |
| FighterPort.Tests | Original fixtures, independent numerical expectations, native round trips and necessary UI acceptance. |

Organize rigging, retargeting, transition fitting, slots, gameplay and media as modules within the relevant assembly until a separate package becomes useful. Avoid implementing dozens of empty plugins merely to make the solution tree look complete.

### 4.2 Domain model

At minimum define:

- SourceMount and SourceBuildIdentity.
- NativeResourceRef with container path, indexed entry identity, hash and native version.
- SourceCharacter, SourceModelRevision and SourceClipRevision.
- Scene, Mesh, Material, Skeleton, SkinBinding, Socket, Prop and typed animation channels.
- RigProfile and semantic bone/chain roles.
- FighterProject, OutfitVariant and TargetProfile.
- AnimationRevision, RetargetRecipe and TransitionRecipe.
- SlotAssignment, MoveDefinition, ScriptDocument, AudioCue, VfxBinding and PortraitSet.
- TargetBuild, ValidationReport and BuildReceipt.

Use stable identifiers independent of file names and eventual numeric game slots. Native sources are immutable. Target corrections, costume overrides and per-game adaptations belong to independent revisions.

Keep native curves and unrecognized resource information alongside editable scene data. If an edit would invalidate unknown offsets or relationships, the writer must report the unsupported operation instead of blindly reinserting opaque bytes.

### 4.3 Required service boundaries

Contracts should cover:

- Game-source and archive providers.
- Native model/skeleton/animation readers and evaluators.
- Optional native writers and archive repackers.
- Source-character association.
- Rig characterization and FK/IK evaluation.
- Retargeting and transition fitting.
- Target animation-slot and gameplay-schema providers.
- Target builders, dependency checks and playtest bridges.

A result carries diagnostics and a channel-loss report, not just a success Boolean. Long jobs are cancellable and do not run on the playback path. Original-source preview, edited preview and export share one defined evaluation contract. Workers return prepared assets or batched data; do not make synchronous per-bone, per-frame IPC calls part of normal playback.

## 5. One model viewer with real animation editing

BrawlCrate-style functionality is a product requirement:

- Resource hierarchy, selected model/costume, skeleton tree and properties.
- Textures/materials, bone weights, normals, visibility, morphs and supported native animation channels.
- Obvious animation browser for separate model and motion sources.
- Play/pause/stop, looping, speed, frame stepping and fractional scrubbing.
- Timeline and curve editor with key insert/delete/move/copy/paste.
- Bone translation, rotation and scale using numeric fields and gizmos.
- Local/parent/world edit spaces, auto-key/manual-key, masks and undo/redo.
- Source/target comparison, floor, axes, contact markers and ghost poses.
- Costume switching without losing the current clip, playhead or edit state.

A skeleton overlay moving while the mesh stays in bind pose is a failure. A list of clip names without working playback is a failure. Pose-only, animation-key and bind-pose editing must be distinct operations so rotating a pose does not silently modify the skin bind.

All panels use the same document, selected model, clock and command service. Do not create separate “normal editor” and “model preview” animation implementations.

Use a common zero-based time model. Adapters explicitly convert any native/viewer conventions. In the old Brawl code, native key time and the ApplyCHR bind-pose convention differ; new tests must detect a one-frame offset. See the exact reference paths in section 13.

## 6. Mixamo-like source reuse, rigging and retargeting

### 6.1 Source animation library

An animation library entry must retain its authentic source model/skeleton, bind/rest data, timing, native track semantics, coordinate convention, resource identity and source hashes. Do not transfer an unassociated animation by guessing its ownership from a name.

Browse Game > Build > Character > Outfit/Model > Animations. Preview the original first. Using a clip in a fighter creates a target revision while preserving the donor.

Known-game rig templates should use names plus topology, relative positions, local axes and verified fingerprints. For unfamiliar rigs, show an editable suggested mapping and confidence. Treat humanoid, quadruped, tailed, winged and other anatomies as different profile families.

“Set up rig” includes two distinct cases: adding authoring controls to an already skinned game model, and creating a skeleton/weights for an unrigged mesh. The first is the required automatic workflow. Creating a new deform skeleton, automatic skinning, and assigning weights for unrigged meshes are explicitly outside the current product priorities. They are not prerequisites or default UI actions.

### 6.2 Retarget pipeline

1. Decode the native motion at its original timebase.
2. Convert coordinates and units while keeping bind transforms and root/actor movement distinct.
3. Characterize source and target semantic anatomy and align reference frames.
4. Transfer rotational intent through FK with independent limb proportions and twist distribution.
5. Apply selected IK/contact constraints for feet, hands, props, ledges and body support.
6. Compensate pelvis/body pose within configured limits and stabilize knees/elbows through reach and pole transitions.
7. Keep editable correction layers and rig controls.
8. Fit the start/end transitions using section 7.
9. Bake or encode to the target's actual native or SDK requirements.
10. Reopen and validate resulting poses, timing, contacts and preserved channels.

The neutral characterization profile is not a mandatory replacement skeleton for every game. Preserve each target's native skeleton, helpers and attachments. A Goku Rivals destination rig can be a reusable target, but it must not become the universal source or intermediate skeleton.

Give users useful controls: preserve original motion, preserve target stance, retain hand/weapon contacts, pelvis adjustment, root policy and per-chain weights. Use sensible defaults for verified rigs.

Animation-to-any-model is the long-term ambition. It cannot guarantee impossible contacts or complete conversion across unrelated anatomy, deformer systems and gameplay conventions. Return an editable partial result or a specific blocked constraint. Do not silently stretch anatomy, flatten a jump or label unsupported facial/prop channels as transferred.

## 7. First and last frame transition fitting

This is a first-class requirement, alongside retargeting.

The user must be able to say:

> Use this motion on this fighter. Fit its first few frames from this fighter's idle, and fit its last few frames into this fighter's fall. Keep the move's frame count and important attack timing.

Idle and Fall are quick presets. Any selected clip, pose, phase or eligible animation segment can be a reference.

### 7.1 Simple user interface

In the same animation editor provide:

| Setting | Example |
| --- | --- |
| Fit start from | Target Idle |
| Start reference | Automatic compatible phase, or selected frame/phase |
| Start window | First 4 frames |
| Fit end into | Target Fall |
| End reference | Selected phase or automatic compatible phase |
| End window | Last 6 frames |
| Preserve duration | Enabled |
| Protected ranges | Hit frames, cancel points, landing/grab events or selected intervals |
| Contacts | Automatic suggestion with grounded/airborne/prop overrides |
| Body mask | Whole body or selected chains |
| Preview | Reference > fitted move > reference |
| Apply | New editable revision with one undo transaction |

Always show how many editable frames are available. If a six-frame exit would overlap protected hit frames, show the conflict and offer a shorter window or explicit timing edit. Do not silently change the attack.

### 7.2 Concrete duration example

Use a 24-frame move with UI frames numbered 1–24:

- Frames 1–4 fit from the target Idle.
- Frames 5–18 retain the already-retargeted move.
- Frames 19–24 fit into the target Fall.
- The clip remains 24 frames.
- A protected hit event at frame 12 remains at frame 12.
- The preview continues into the selected Fall phase.

The exact windows are user choices, not defaults for every attack. If the target game always starts Fall at phase zero, fit to that actual entry phase. Phase search may select another phase only when the target supports it or when the reference loop is deliberately rebaked with a new start.

Each target profile must define sample count, last-key time, end-exclusive clip duration and the first reference sample evaluated after the state change. For example, 24 displayed samples can put the final key at 23/60 seconds while the clip occupies 24/60 seconds. Fitting the final key to Fall phase zero and then repeating Fall phase zero on the next runtime update can introduce a hold. Validate the joined animation at consecutive runtime sample times and define the boundary-time reference explicitly; do not assume the last stored key and the state-transition instant are identical.

### 7.3 Interpolation contract

Perform fitting after both the move and reference have been evaluated on the target skeleton. Interpolating poses from unrelated native skeletons before retargeting is invalid.

The fitter stores reference clip revision, skeleton revision, phase, playback rate, channel mask, editable windows, protected intervals, root policy, contact intents and solver version. A referenced animation changing must invalidate the dependent fitted result. Detect recipe cycles; a frozen baked reference can break a cycle intentionally.

A first implementation can use a smooth overlap blend against a sampled reference segment. For normalized window time u, a suitable blend weight is:

~~~text
w(u) = 6u^5 - 15u^4 + 10u^3
~~~

It reaches 0 and 1 with zero first derivatives. Translation and compatible scalar channels use the smooth blend; rotations use normalized quaternion interpolation with consistent signs/branches. Preserve deliberately authored full turns: shortest-arc interpolation must not erase a spinning attack's winding.

For the outgoing window, blend the original target-rig motion into a time-aligned reference segment. Reverse the roles for the incoming window. Blending toward one frozen pose is an explicit pose mode; it should not masquerade as a smooth continuation into a moving idle/fall cycle.

Matching endpoint pose provides position/orientation continuity. Smooth motion also requires appropriate linear and angular velocity at both the join to the unchanged interior and the join to the reference. Use derivative-aware curve fitting or a tested inertial correction where needed. Do not claim velocity continuity just because an easing curve was used: reference timing, native interpolation and the subsequent contact solver can change the derivatives. [S23]

### 7.4 Contacts and movement

- Keep a foot planted only when that contact belongs in the transition.
- Release stance contacts for an aerial/fall exit; do not pull an airborne fighter down to a floor plane.
- Keep root/actor trajectory separate from local body pose. Do not mix engine-controlled translation with authored root motion and double the movement.
- Match facing and appropriate root offsets without teleporting the character.
- Treat ledges, grips and two-actor throws as explicit supports/anchors.
- Use masks to retain a weapon grip or important upper-body silhouette when allowed.
- If the duration, speed limit, contact and protected frames are incompatible, preserve the original and report the infeasibility.

Discrete visibility/material events need channel-specific handling. They are not all interpolated as continuous values. Facial, UV, color, morph and prop channels must retain synchronization or be included in a precise loss report.

### 7.5 Bake and verification

Fit only the selected windows. Re-encoding must not disturb protected/interior samples through changed cubic tangents or compression.

Verify all four joins: incoming reference to the move, start window to unchanged interior, unchanged interior to end window, and move to outgoing reference. Check the fresh native decode at endpoints, neighboring subframes and withheld fractional samples, including the actual transition into the reference clip. Measure pose and linear/angular velocity discontinuities separately. Use target-specific numeric tolerances justified by quantization; do not invent a universal zero-error claim. A discontinuous reference loop cannot receive a velocity-continuous fit claim without explicitly correcting that reference.

When fitting only body channels while preserving root travel, the continuity report must exclude the unchanged root channels from its success claim. A baked clip remains independently playable after its reference sources are unavailable; regenerating the recipe still requires the pinned references.

Scrubbing to a frame must produce the same pose regardless of the order of previous frames. IK history and quaternion branch selection must not make the result depend on playback direction.

For Rivals II, the transition must exist in the exported baked animation. Its official character-animation guidance does not support runtime IK/retargeting/postprocessing on the main character mesh. [S16]

## 8. Required animation slots and automatic assignment

A target profile provides a versioned slot manifest drawn from its actual game/build definitions or installed SDK. The manifest records:

- Canonical slot ID and native binding location.
- Required, optional, alias or legitimate fallback behavior.
- Loop and duration rules.
- Entry/exit states and reference poses.
- Protected gameplay windows.
- Relevant aerial/grab/throw/weapon/costume variants.
- Assigned source and target clip revisions.
- Readiness, loss diagnostics and evidence.

A human-friendly category list can cover movement, crouch, jumps/falls, landing, shielding/dodging, normal attacks, aerials, specials, grabs/throws, damage, ledge actions, knockdown/techs, taunts/results and game-specific actions. The exact entries come from the target profile, not this generic category list.

Auto Fill Missing should:

1. Leave user-protected assignments unchanged.
2. Prefer reviewed exact mappings and known slot packs.
3. Rank source candidates by semantic tags and compatible motion/contact metadata.
4. Retarget each candidate, apply the slot's transition recipe and validate duration/events.
5. Apply high-confidence compatible assignments or queue a preview for review.
6. Report missing/incompatible roles without filling every gap with Idle.
7. Make the batch reversible and cache results by all relevant revisions.

Rivals II has official state documentation and already supports some automatic assignment through an animation directory and naming prefix. The CD_ character data includes state/aerial-grabbed/thrown assignments; attacks have separate data and timing windows. The bridge should use and validate those real structures. Some import-setting sections are explicitly marked outdated after the 5.8 update, so inspect the installed version instead of copying every checkbox from a web guide. [S17][S18]

## 9. Every part of a full fighter

| Workspace | Required scope |
| --- | --- |
| Model | Meshes, materials, textures, UVs, skin weights, skeleton, bind matrices, morphs, helpers, sockets and props. |
| Rig and animation | FK/IK, source library, retargeting, transition fitting, curves, events, native channels, slots and previews. |
| Costumes | Real alternate outfit geometry, accessories and variant mappings; separate recolors/palettes; shared animations where valid. |
| Gameplay | Startup/active/recovery, hitboxes, hurtboxes, damage, knockback, movement, cancels, landing, states, grabs, throws and resources. |
| Code | Per-game source documents, syntax/diagnostics, supported templates/compiler bridges and target-specific escape hatches. |
| Audio | Voice, SFX, announcer cues, sound banks, event timing, previews and supported target conversion. |
| VFX | Effects, particles/trails, materials, attachment points, events, projectiles and target-specific authoring/build bridges. |
| UI artwork | CSP/character-select portrait, BP/battle portrait, stock icon, HUD/results graphics, outfit thumbnails and name data. |
| Target package | Game version, runtime/framework dependencies, destination identities, linked assets, collision/conflict checks, build/test receipts and installation rollback. |

For Goku, different gi, boots, hair, accessories and the Power Pole can be actual outfit/part assets. Do not reduce them to palette changes. Store per-outfit correction data when body shape, footwear or grip locations change.

A camera preset can generate portrait/icon starting renders from the live model, including transparent background and the correct costume. Export dimensions, masks and naming come from the target profile.

Animations and gameplay are linked but separate. Assigning a punch animation does not implement its hitboxes, script, knockback, projectile or sound. A shared move representation may express common concepts; target-specific source must remain editable and preserve unsupported constructs. GML, Lua, PSA, ACMD and compiled game code are not universally interchangeable.

The viewport can preview poses, effects and timeline events. Exact physics, combat behavior and integration require the target's actual game/SDK playtest. Do not present a generic preview simulator as authoritative gameplay.

The target's complete-fighter checklist must also account for conditional dependencies: Kirby copy assets/behavior, articles and their own scripts, transformation or partner forms, Final Smash resources, victory/results assets, and Classic/ending/trophy integration where the selected build supports or requires them. Record supported output, a legitimate declared fallback or not-applicable for each; broken references cannot disappear behind a broad category marked complete. BrawlInstaller's fighter-package structure and Clone Engine's templates are relevant references. [S6][S14]

## 10. BrawlInstaller equivalents and target builders

| Destination | Verified tool/workflow reference | FighterPort integration |
| --- | --- | --- |
| Brawl and configured mod builds | BrawlInstaller | Read/write compatible fighter packages where verified; inspect dependencies, cosmetics, costumes and build settings; stage changes with conflict checks/backups. |
| Melee v1.02 with m-ex | m-ex plus mexTool; HSDRaw for native assets | Separate replacement and expansion profiles; use the exact selected framework revision and compatible code/build tooling. |
| Ultimate replacement | ARCropolis plus the selected script/runtime tooling | Build replacement content with correct dependencies and paths. |
| Ultimate expanded fighter | SSBU Clone Engine plus Clone Pack Workbench | Experimental profile with pinned game, engine and runtime dependency versions; manifests, name-based identity and real game validation. |
| Rivals II | Official Workshop Modkit and its templates/build flow | Produce and validate authoring inputs, import/update correct template assets, run supported tests and use the official publishing flow when requested. |

BrawlInstaller supports its .fighterpackage workflow. m-ex documents content expansion for Melee v1.02, and mexTool describes itself as the installation/management companion. [S5][S10][S11]

The Ultimate Clone Engine README documents 13.0.4/13.0.5 support and dependencies including Skyline, ARCropolis, Smashline, NRO Hook and CSK Collection. Its release 0.6.0-beta.1 was published on 17 October 2026 and explicitly describes features as experimental. Workbench assists with pack manifests/configuration and code scaffolding. Treat these as a qualified integration candidate. [S14][S15]

Do not persist a supposedly universal numeric fighter slot. The Clone Engine allocates identities at runtime; a target profile must resolve its own identity rules. Replacement costumes, one-slot movesets and genuinely additional fighters are separate build modes.

Workbench scaffolding does not complete arbitrary gameplay or rename every reference inside native binary files. FighterPort needs verified native editors/builders for those remaining dependencies.

### 10.1 Rivals II boundary

The current official installation page says the Modkit includes Unreal Engine 5.8. Keep the actual installation/build version in the target profile. This does not mean FighterPort itself is an Unreal application. [S19]

The character-animation guidance requires 60 FPS and baked joint/morph animation; authoring control rigs are not the deliverable. Create a target skeleton that is correct for the chosen model and import paths; do not make an arbitrary universal skeleton mandatory. [S16]

Preserve the Modkit's template references for meshes, skeletons, physics/hurtbox assets, data and render definitions. Early end-to-end validation should use Idle, movement and one attack before the entire moveset is produced.

The official custom-audio workflow currently specifies FMOD Studio 2.03.09 and includes naming/automation conventions. Audio therefore requires a real compatible bank-build workflow in addition to waveform editing. [S20]

Where the installed SDK exposes a supported automation mechanism, bridge to it. Otherwise give a precise manual handoff inside the workflow. Do not fabricate a cooking CLI, write arbitrary cooked UAssets or report a generated folder as a working Workshop character. Keep build/test separate from an explicitly requested public publish.

The official publishing guide places cooking inside the Modkit's Publish action and instructs users not to cook separately. Editor playtesting and input preparation can precede publishing. Respect that supported pipeline rather than inventing an offline packaged-mod step that the current Modkit does not expose. [S27]

## 11. Source games, ROMs and dumps

Register local installations, extracted dumps, supported images, model/motion folders and build overlays as read-only sources. Each registration records game/platform/build, native identity and the source provider.

The source browser must:

- Resolve base game plus mod/patch/DLC overlays with visible precedence.
- Show real character associations and costume variants.
- Resolve nested archive entries by stable identity.
- Extract only selected assets and required dependencies to a controlled cache.
- Preserve source hashes and support exact relinking after a source moves.
- Handle cancellation, malformed input and resource budgets without corrupting the catalogue.
- Keep decoded scene caches separate from originals.

GameCube/Wii images can use a verified disc provider or external tool with known formats. Switch, 3DS, Wii U and Unreal containers need their own support. A file named ISO or PAK is not proof that its contents can be read. Protected or unknown sources require supported readable input/tooling. Do not ship game dumps, keys or commercial assets with FighterPort, and do not execute extracted scripts/executables while indexing.

For Dragon Ball, prioritize the first independently verified Goku source that advances the user's port. Xv2CoreLib/XenoKit and CUE4Parse-related tools can inform the adapters. A general Unreal package parser does not guarantee that every title's animation compression, material or special deformer is supported. [S21][S22]

## 12. Implementation sequence

Every milestone ends with a runnable workflow and recorded evidence.

| Milestone | Deliverable | Completion gate |
| --- | --- | --- |
| M0: independent foundation | New x64 app, shared document/clock, synthetic skinned model, timeline/gizmos and project persistence | Actual visible animation and pose edits survive undo/redo and reopen; viewport spike measured. |
| M1: first native editing workflow | Brawl model plus separate CHR0/motion source, native playback, bone key edit and new native output | Export reopens with correct sampled/skinned results and unchanged source files. |
| M2: reusable transfer and fitting | Source library, verified humanoid mapping, FK/IK, proportion/contact retargeting, idle/fall/custom transition recipes | Original unequal-proportion rigs and private Mario/Snake both directions pass appropriate native tests; duration/contacts/reference joins verified. |
| M3: early Rivals II bridge | Real target schema, a model, movement clips, one attack, an outfit and UI preview | Installed Modkit imports the result and permits a real playtest; errors are surfaced. |
| M4: complete first fighter | Remaining required moves/states, gameplay, audio, VFX, costumes, portraits and packaging | A complete chosen target fighter passes build and actual game checks. |
| M5: expand game coverage | Ultimate/Melee native adapters and their target profiles, then additional Smash and Dragon Ball sources | Each game gains separate read/edit/write/package/game evidence. |
| M6: advanced authoring | More morphology profiles, additional anatomy-control profiles, mesh/UV tools, advanced effects/graphs and broader automatic filling | Each feature is demonstrated on independent assets beyond the original regression characters. |

Run the small Rivals toolchain discovery alongside early architecture work so export requirements influence the design. Bring a suitable Dragon Ball donor reader forward when needed for Goku. Do not delay all Rivals progress until every old-game format is complete; do not mark a partial writer complete to accelerate it.

### 12.1 Immediate Codex task

The next execution task is **M0 followed by the narrow M1 native animation workflow**. Carry M2's data contracts from the start, and implement its first transition fitter immediately after native sampling/editing is sound.

1. Inspect the workspace and applicable instructions. Create a distinct standalone directory/repository only if it does not already exist. Keep it outside the old application repositories.
2. Read this handoff and the current greenfield master. Write the new repository's own AGENTS.md, docs/MASTER_PLAN.md and docs/plans/current.md. AGENTS.md must point to those exact paths; do not create competing master plans.
3. Inspect/pin candidate dependencies; document the Brawl data/worker boundary and complete the rendering spike.
4. Implement a real synthetic skinned-model editor with an animation list, play/scrub, bone transforms, timeline, undo and save/reopen.
5. Connect the first Brawl adapter using locally supplied fixtures where available. A separate model and motion package must work.
6. Export one actual edited native CHR0 and reopen it; verify independent pose/geometry expectations and source hashes.
7. Record precisely which substeps passed, which game fixtures were available and the next unresolved task.
8. Continue implementing instead of stopping after creating planning files.

Local commercial fixtures may be absent in a new workspace. Complete original-fixture coverage and all unaffected implementation, then report the specific missing input for native game acceptance. Do not invent a fixture path or claim a game run occurred.

### 12.2 Required repository documents

- AGENTS.md: read master/current task first; same core for GUI/CLI; native/source integrity; truthful capability evidence.
- README.md: install/build/run and current demonstrated workflow.
- docs/MASTER_PLAN.md: full product scope and governing standalone decision.
- docs/plans/current.md: exactly one concrete milestone.
- docs/ARCHITECTURE.md: contracts and dependency direction.
- docs/FORMAT_MATRIX.md: operation/game/version evidence.
- docs/TRANSITION_FITTING.md: section 7 contract and tests.
- docs/TARGET_PROFILES.md and docs/TARGET_RIVALS2.md: versions, schemas and tooling.
- docs/TESTING.md and docs/DEVELOPMENT_STATUS.md.
- docs/LICENSE_AUDIT.md and THIRD_PARTY_NOTICES.md.

Planned features stay in the roadmap. Do not publish a “complete” release based on placeholder panels or empty output directories.

## 13. Necessary acceptance and reference paths

### 13.1 Independent fixtures

Create or selectively reuse licensed original procedural rigs with different heights, limb ratios, local axes, twists and bone names. Avoid character-name conditionals in the solver.

Necessary cases include:

- Separate model/motion packages and duplicate clip names in different archive branches.
- Actual GPU-skinned geometry matching independent sample expectations.
- Nonzero bind offsets and explicit native frame-index conversion.
- Cubic/stepped channels, equal-time native key pairs where applicable, loop boundaries and fractional times.
- Missing/changed source detection, exact relocation, two projects using one source and independent target edits.
- Unequal proportions, grounded/airborne transitions, unreachable IK goals and near-straight knees/elbows.
- Native save/reopen and preservation of unrelated resources.
- Required-slot validation, protected assignments and whole-batch undo.
- Correct target build receipts and an actual SDK/game run before claiming playability.

Fresh-process reopening tests restoration, but using the same reader/writer alone does not establish independent semantic correctness. Pair it with independently derived pose/geometry/timing expectations or a separate reference evaluator.

### 13.2 Transition-specific cases

- Both start-only and end-only fitting; both together.
- A 24-frame fixture stays 24 frames and its protected middle/hit event is unchanged.
- A moving reference cycle joins with the chosen phase and appropriate velocity.
- The actual runtime entry phase is respected.
- Quaternion signs and an intentional full spin remain stable.
- A planted-foot transition does not slide; a fall transition does not acquire a false floor contact.
- Root motion is neither doubled nor zeroed.
- Overlapping windows, zero/one-frame windows and an entirely protected window give defined results or diagnostics.
- Native export/reopen preserves endpoints, neighboring fractional samples and acceptable velocity continuity.
- Random scrub order and repeated bake are deterministic.
- Editing the referenced Idle/Fall invalidates dependent fitted results.
- Cyclic reference recipes are detected.
- Unsupported discrete or secondary channels are reported instead of silently dropped.

Define tolerances in native units and target quantization. Record measurements before relaxing a test.

### 13.3 Exact old-code locations worth studying

At studio-next revision 96a4e0e81b1ad79837c307d57197ecd4c756179b:

- BrawlCrate/Studio/Retargeting/BrawlRetargeter.cs: native Sample seam.
- BrawlLib/SSBB/ResourceNodes/MDL0/MDL0Node.cs: ApplyCHR and WeightMeshes.
- BrawlLib/Wii/Animations/KeyframeCollection.cs: native curve evaluation.
- BrawlLib/SSBB/ResourceNodes/Animations/CHR0Node.cs: native animation structures.
- BrawlCrate/Studio/BrawlLibrarySource.cs and LibraryRecords.cs: source ownership/provenance.
- tests/LibraryAcceptance/Program.cs: FailureCases, DuplicateBranches, BakeProject, Reopen and ManageSources.
- tests/RetargetAcceptance/NativeCases.cs, CharacterizationCases.cs and TrajectoryCases.cs.
- tests/GuiAcceptance/Run-PresentationAcceptance.ps1: displayed playback evidence.

At studio revision 49ecd9a7192ab79f00bf81883d5259d352ab256d:

- tests/NativeSmoke/OriginalHumanoidFixture.cs: original unequal-proportion test rigs.
- tests/NativeSmoke/HumanoidTemporalNativeChecks.cs: native time/curve edge cases.
- tests/NativeSmoke/HumanoidStandingNativeChecks.cs: weighted sole/contact expectations.
- tests/NativeSmoke/HumanoidReachPoleChecks.cs: reach and pole transitions.
- tests/NativeSmoke/AnimationBrowserChecks.cs: real browser/playback behavior.

Inspect current license headers and dependency use before taking any code. Rebuild these expectations against the new implementation; do not transfer old passing counts.

## 14. Reuse and distribution decisions

Keep an upstream ledger with repository URL, exact commit/tag, adopted paths, component license, notices, modifications and verified integration route.

BrawlCrate and BrawlInstaller use LGPL at the repository level, but BrawlInstaller's credits include AGPL Color Smash and components described as used with permission. Review the actual component to be included. A process boundary is not a blanket exemption from license obligations. [S4][S5]

HSDRaw and the SSBH projects have permissive repository licenses, as do Godot, Avalonia and Silk.NET; inspect transitive dependencies and copied assets separately. m-ex/mexTool reuse requires resolving their actual licensing rather than assuming that a public repository permits arbitrary redistribution. Clone Engine/Workbench code has GPL terms. Keep the SDK/Modkit as the user's installed external toolchain.

Routine implementation can continue locally. Existing authorization to update an old repository is not automatically authorization to create/publish a different public repository. Keep source assets, dumps, local settings and builds out of public source control.


## 16. Detailed beginner workflow: make a complete platform-fighter mod character

This is a **future finished-product user journey**, not a claim that every target builder or gameplay tool exists today. Use Goku as an example; the same project flow applies to any legally supplied skinned model.

### A. Create the fighter project

Choose `New Fighter`, name the character `Goku`, select the initial target `Rivals II` (or Brawl/Melee/Ultimate), and save a versioned project folder. Optionally activate more target branches now or later. The project is the source of truth for outfits, animation slots, source references, timing, effect attachments, portraits and gameplay work. The project remains valid even if initially animation-free.

### B. Import the existing skinned model

Choose `Import Character Model`. Select an actual model and all its required skeleton, skin-binding, materials, textures, morph and auxiliary records from a supported loose file, archive or registered local source. Do not require the user to create a skeleton. Inspect the real deform hierarchy and skin weights. Validate native coordinate, scale, bind pose and bone constraints without silently rewriting them. Choose outfit variants as separate model assets when appropriate; recolors are a different branch.

### C. Set Up FK / IK — automatically on its **existing bones**

Click **`Set Up Animation Controls`**. Attempt exact fingerprint reuse; otherwise apply tested game/build name aliases and structural profiles. A review may ask only to confirm unclear pelvis/head/left-right chain roles. Attach editor-only FK rotations and IK effectors/poles for hands and feet, optional pelvis/spine/chest targets, head/look and attachment controls. Controls should not appear as extra game bones on export. The `Rig inspector` can switch between `Simple` (Auto; Show Handles; Adjust; Bake) and `Advanced` (hierarchy, per-chain offsets, twist, bend plane, root/scale limits). Rig setup must be reusable across clips and saved with exact model/rig fingerprints. Provide `Edit FK`, `Move IK Target`, and `Bake Controls` separately; neither requires importing an external animation.

### D. Register donor sources once

Open `Animation Library` → `Add Source`: select Brawl/Project M native files, GameCube/Wii images or extracted directories; Melee fighter DAT; Ultimate SSBH/model/motion folders or supported archive; Rivals II Modkit assets; Dragon Ball FighterZ/Sparking! ZERO cooked assets; Xenoverse model/ESK/EAN bundles; or future providers. Each source is read-only. Record exact game/build/provider, indexed container path/child ID, hashes, skeleton/rest information and preview dependencies. Association is evidence-based: model+clip from a game source should link to the exact originating *model revision*, not a name-matched file. If ambiguous, provide a two-choice review once. Extract/select only needed assets to an isolated cache; catalog metadata persists after restart, and cache can regenerate.

### E. Use an animation from any source on the target

Select the active target fighter model in the single viewport. Search `Mario / Forward Aerial` or `Goku / Punch`. Click `Preview Original` to see it on its authentic source rig. Click **`Use on This Fighter`**. FighterPort resolves the donor model automatically, compares anatomical profiles, aligns coordinate/rest conventions, transfers motion intent with FK, optionally solves IK/contact goals against *target* proportions, and creates a separate editable target revision. Clearly label the original, FK preview, corrected preview and baked native output. Never pose the donor model by mutating the target, or assume same-named bones prove a mapping. If limb reach is impossible, show target-side adjustment handles and a concise diagnostic. Retargeting is not required for same-rig ordinary edits.

### F. Assign the game's real animation slots

Use the chosen build's **verified target manifest**: movement, stance, crouch, jump/fall, shields/dodges, ledge, items, hurt/knockdown/tech, grabs/throws, jab 1..N, tilt/smash chains, aerials, specials with independent ground/air and start/loop/end/release, taunts and results. `Missing`, `Assigned but Unbound`, `Native Validated`, `Gameplay Missing` and `Game Tested` are different states. Drag reusable clips onto slots. A multi-part move is not automatically executable gameplay merely because animations exist. Preserve custom slot names and extra parts without inventing game-native IDs.

### G. Retiming and target-style adaptation

An animation imported from FighterZ may use *held/stepped artistic poses*. Preserve that timing unless the user selects smoothing. Provide speed presets (0.5x, 1x, 2x), custom rates, preserve-duration versus change-duration policy, game-frame event protection, native timebase and loop semantics. Do not assume FighterZ's battle animations all use a single sample rate. Conversion to a 60-fps authoring target can bake holds and original action keys without making them move continuously.

### H. Smart first/last-frame fitting and Fill Missing Misc

Choose `Fit Start From` → target Idle / any selected target-rig pose or clip phase, and `Fit End Into` → target Fall / any appropriate reference. Edit only the first/last N **existing** frames by default; keep total frame count and protected hit/action frames. Preserve root movement and contacts. Verify joins at real runtime state boundaries, not just at stored first/last keys. For a 24-frame animation, e.g. first 4 from Idle, original center 5–18, last 6 into Fall; hit at frame 12 stays there. A ledge climb belongs to the correct ledge/idle chain, not Idle→everything. Show an explicit conflict when constraints cannot fit.

The `Fill Missing` action chooses compatible donor packs and only empty target slots; it proposes, previews and batch-applies per-slot semantic retarget/fit recipes, retaining custom clips and providing one Undo. Reject unsupported facial/material/prop tracks or report losses rather than discard silently.

### I. Complete the rest of the fighter

Use model/costume, gameplay, sound/VFX and art workspaces. Define actual moveset/state data, startup/active/recovery, hitboxes and hurtboxes, throw/catch references, effects, voice/SFX banks and portrait/CSP/battle portrait/stock art. These tools must have target-specific authoring schemas and native compilers. Some are **later milestones**, even though the final project must reserve their data slots now. A visual attack animation alone does not create playable attack behavior. Conditional systems (Kirby copy, articles, partners, final attacks, special character forms) must be tracked per game/build as required/optional/not-applicable.

### J. Build independently for multiple games

Choose `Build Target` → a configured build of Brawl/PM/P+, Melee, Ultimate or Rivals II. The target adapter maps edited animations to its actual skeleton/asset/slot requirements and compiles or hands off according to a *verified* format/toolchain. The real Rivals II installed Modkit remains the authority for its template, cook and publish process; don't fabricate cooked UAssets or claim one-click game testing without an observed tool. Build reports separate structural validity, native file reopen, runtime bindings, package completeness and actual playtest. A project may yield separate Brawl, Melee, Ultimate and Rivals II outputs while reusing the same donor sources and editorial work.

### K. Iteration and testing

Preview in-editor immediately for pose/timeline/contact errors, then native decode/reopen. Once a target toolchain supports it, stage/load a separate mod into game for playtesting, track failures, change the fighter project and rebuild only affected outputs. Quick in-game testing and full gameplay code generation are lower-priority implementation steps, not omitted from the final scope.

## 17. Non-negotiable auto IK/FK existing-rig requirements

1. **Never create replacement deform skeletons for an already skinned imported fighter.** Keep its original root, native skin binds, hierarchy, game helpers and weights.
2. The auto-characterizer's name aliases are suggestions unless their game/build+hierarchy+rest-pose fingerprints are previously validated. Recognize head/neck, chest/bust, spine/waist, hips, clavicles, arms, elbows, wrists, hands/fingers, legs/knees, ankles/feet/toes, plus optional twist/extra chains. Do not hardcode only Mario/Snake.
3. Control-rig mapping stores source game/build, exact skeleton fingerprint, coordinate interpretation, anatomical chains, axes, rest/calibration poses, lengths, pole directions, virtual foot/hand endpoints and confidence/review status.
4. Editor controls are conceptual overlays/gizmos, not mandatory added exported bones: FK local rotation/translation fields and handles; hand/foot IK effectors and pole vectors; optional pelvis/chest/head and prop controls. Same skeleton can use multiple saved control layouts.
5. Controls function **without** a retargeted clip (manual animation editing); retargeting functions without requiring artist IK controls; combining them preserves the source motion and target proportions. `Disable IK` must not disable FK animation playback.
6. Applying a control at a timeline frame should generate editable target-side keys/layers, with Auto Key and manual Key controls, selected chain masks, Copy/Paste, Undo/Redo and rest-pose reset that does not alter binds.
7. `Save Rig Setup` saves only control/role/calibration metadata tied to exact native skeleton revisions. It never serializes a new deform skeleton by default. `Bake` writes native skeletal keys only through a supported target writer and validates reopened poses and skin.
8. Unknown rigs remain usable with manual 1-click control selection where possible. A partial humanoid rig can still offer arm IK if feet are ambiguous; don't block all tools on an all-bones mapping.
9. Absent bones (e.g. fingers), nonstandard limb structures and native helper constraints are supported as explicit optional/unsupported cases, not silently fabricated bones.
10. Strictly separate: `Skeleton` (deforming bones/weights), `Control Rig` (authoring handles), `Rig Profile` (semantic mapping), `Retarget Recipe` (cross-skeleton motion) and `Native Output` (target animation data).

## 18. Practical UI design and acceptance proof

**Landing UI:** `New Fighter | Open Fighter | Open Model | Animation Library`. **Main toolbar:** `Add Source | Use on This Fighter | Set Up Animation Controls | Fit Start/End | Assign Slot | Export`. **Simple Inspector:** `FK | IK | Retarget | Timing` segmented modes, each editing the same clip; controls remain discoverable but not piled into full-width permanent panels. A searchable library drawer shows `Game > Character > Source Model > Animations` and full associated source preview. Keep the real viewport large, with toggles for skeleton/skin/IK goals/contact and reference ghosts.

Demonstrate with original skinned humanoid fixtures (not only commercial models): importing two different rigs, automatically attaching FK/IK handles without changing either deform skeleton's checksum, moving a hand IK effector at frame 12 and editing shoulder FK keys, undo/redo, native animation export/reopen and independent vertex poses. Retarget the same donor clip to both targets, preserve each target's proportions and donor hashes. Test already-configured profiles across restart and changing bone aliases/rig versions. Validate contact changes through airborne transitions. Verify fractional/loop timing, intentional 360-degree spin, short clips, native quantization and accurate displayed playback. A screenshot of handles without functional keyed deformation is not a passing test.

**First milestone order:** M0 skinned scene/animation editor → M1 Brawl native model + motion import and edited native CHR0 output → M2 automatic editor-side FK/IK controls for the existing skinned rig, then source-bound global animation library and Brawl-to-Brawl transfer → M3 state-aware fitting, slots and batch fill → M4 Rivals II Modkit and other true native adapters → later gameplay compilation and quick testing. This is a recommended ordering of the handoff's existing milestone intent, not a claim that any work is complete.

## 15. Primary references

All current-source observations below were checked on 18 October 2026. Version-specific integration must be rechecked when implementation starts.

- **[S1] Existing FighterPort README:** https://github.com/DoctorFlux/fighterport-studio/blob/49ecd9a7192ab79f00bf81883d5259d352ab256d/README.md
- **[S2] Existing next project:** https://github.com/DoctorFlux/fighterport-studio-next/blob/96a4e0e81b1ad79837c307d57197ecd4c756179b/BrawlCrate/BrawlCrate.csproj
- **[S3] Existing next capability matrix:** https://github.com/DoctorFlux/fighterport-studio-next/blob/96a4e0e81b1ad79837c307d57197ecd4c756179b/docs/FORMAT_SUPPORT.md
- **[S4] BrawlCrate/BrawlLib:** https://github.com/soopercool101/BrawlCrate
- **[S5] BrawlInstaller:** https://github.com/squidgy617/BrawlInstaller
- **[S6] BrawlInstaller fighter-package structure:** https://github.com/squidgy617/BrawlInstaller/blob/master/BrawlInstaller/Classes/FighterPackage.cs
- **[S7] Godot license:** https://github.com/godotengine/godot/blob/master/LICENSE.txt
- **[S8] Godot retargeting design:** https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/retargeting_3d_skeletons.html
- **[S9] HSDRaw/HSDLib:** https://github.com/Ploaj/HSDLib
- **[S10] m-ex:** https://github.com/akaneia/m-ex
- **[S11] mexTool:** https://github.com/akaneia/mexTool
- **[S12] Smash Forge:** https://github.com/jam1garner/Smash-Forge
- **[S13] SSBH library/editor:** https://github.com/ultimate-research/ssbh_lib and https://github.com/ScanMountGoat/ssbh_editor
- **[S14] Clone Engine README:** https://github.com/PropaSmike/ssbu_clone_engine/blob/d2b07786eed425b0e15f8f7db56cabae1b7a087c/README.md
- **[S15] Clone Engine release / Workbench:** https://github.com/PropaSmike/ssbu_clone_engine/releases/tag/0.6.0-beta.1 and https://github.com/PropaSmike/clone_pack_workbench/blob/bed887fd8adfc65efb430ff56686c2227fe42856/README.md
- **[S16] Rivals II character animation:** https://rivals2.com/workshop/knowledge-base/character-creation/animation-overview/
- **[S17] Rivals II animation states:** https://rivals2.com/workshop/knowledge-base/miscellaneous/animation-states/
- **[S18] Rivals II import and assignment:** https://rivals2.com/workshop/knowledge-base/character-creation/import-into-unreal/
- **[S19] Rivals II Modkit installation:** https://rivals2.com/workshop/knowledge-base/getting-started/installing-the-modkit/
- **[S20] Rivals II custom audio:** https://rivals2.com/workshop/knowledge-base/miscellaneous/setting-up-fmod/
- **[S21] Xenoverse candidates:** https://github.com/LazyBone152/XV2-Tools and https://github.com/LazyBone152/XenoKit
- **[S22] Unreal package reader candidate:** https://github.com/FabianFG/CUE4Parse
- **[S23] Velocity-aware transition reference:** https://dev.epicgames.com/documentation/unreal-engine/animation-blueprint-blend-nodes-in-unreal-engine
- **[S24] .NET support:** https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- **[S25] Avalonia:** https://github.com/AvaloniaUI/Avalonia and https://docs.avaloniaui.net/api/avalonia/opengl/controls/openglcontrolbase
- **[S26] Silk.NET:** https://github.com/dotnet/Silk.NET
- **[S27] Rivals II publishing/cooking:** https://rivals2.com/workshop/knowledge-base/getting-started/publish-to-steam-workshop/

