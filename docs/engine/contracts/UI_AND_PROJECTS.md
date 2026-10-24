> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# FighterPort UI and Character Projects

## Latest direction — 23 October 2026

Use normal Model Preview from Next at `96a4e0e81b1ad79837c307d57197ecd4c756179b`, retaining the dark theme and opening directly in that viewer. Stacked left panels, Bones/Keyframes/Files and bottom Transform Frame/All/playback supersede the older dashboard/custom-header/tab and landing-screen proposals below. Preserve independent application/project/target/library services and all M2 gates. `UI_VIEWER_PARITY.md` supports the existing MASTER_PLAN/current plan; earlier acceptance language is a requirement, not fresh verification.

Latest continuation additionally requires the Fighter toolbar visible, reference camera controls and Next's window-based retarget flow. The old embedded retarget task is retained as an optional advanced preview; normal retargeting uses source selection, mapping where ambiguous, compact progress and details windows. Save Result As supports a new native CHR0 and a project snapshot through shared writer contracts, with future formats gated on actual writer implementations. Draft output is not motion/contact acceptance. These requirements stay within the ONE current M2 slice.

Existing-rig controls attach automatically for compatible models, with ambiguous mappings exposed for review. Pin controls have separate IDs/preview picking and can be added to any existing bone: reviewed limb endpoints use IK and other bones use FK. Move/rotate/scale must save ordinary keys with exact gesture Undo. Soft pins hold within native reach and yield only when necessary; Follow existing animation stores a frozen motion reference and explicit pin offsets. Generic FK handles follow the bone and add no inferred support constraint. Skeletons, binds, weights and fixed support/socket policies stay intact. Controls retains pin-only selection, show/hide, removal and model-only setup without a donor.

19 October 2026 revision 4 for the current fighterport studio repository

Implementation checkpoint — 20 October 2026: this supplied specification is integrated into the existing M2 queue. Genuine empty characters, independent target scenes, schema-1 upgrade copies, immutable inputs/revisions, scoped model-viewer editing and automatic keys are implemented. The existing RetargetPanel now embeds source/candidate previews, source animation selection, floor/optional ledge context and fitting/batch review in the model viewer. Edit preserves the editable target; closing cancels uncommitted work; target edits stale candidates and require Rebuild. See `CHARACTER_STORAGE.md` and `TESTING.md` for actual evidence/limits. Full Build composition, complete project browser, native workspace leases, slot synchronization, other-game builders and reliable down/ledge contact fitting remain unfinished. These requirements are not completion claims.

Current implementation reference: `DoctorFlux/fighterport-studio`, commit `5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d`. This revision updates the earlier design against the actual standalone code, its implemented fitting/library workflows and its current execution plan.

Revision 4 adds the user's BrawlCrate-style editing requirement: moving, rotating or scaling a bone writes its key automatically at the current animation frame, and a simple skeleton hierarchy replaces the Bone Inspector on the right. Imported assets retain their complete native documents, different game formats can be combined through supported adapters, and the selected mod target determines output formats automatically.

## 1 Product direction

FighterPort should open a character project into a familiar model and animation editor. Keep a large viewport in the center, a useful animation list on the left, a simple skeleton hierarchy on the right, and playback below. Select a bone and move, rotate or scale it directly; with an editable animation selected, FighterPort creates or updates its key at the current frame automatically. Put automatic source reuse, fitting and game setup into this workspace. A user should be able to create an empty Goku project, save immediately, add a model later, use motions from several source games, and prepare separate versions for Project+, Melee, Ultimate and Rivals II.

One character is one project. A project can contain multiple target configurations, including two configurations for the same game. Shared creative content and source knowledge remain reusable; each target owns its native skeleton choice, animation bindings, gameplay, conversion settings and output. Selecting another game changes the active target inside the same editor.

A Brawl model remains a native Brawl asset inside that project, including its model, material, texture, skinning and animation resource relationships. A Melee motion can be associated with that model through a source-aware animation binding. Import does not replace either original with a reduced common file format. Native asset identity, the model used for editing/preview, and the destination game are independent choices. The ordinary workflow is Add, Use Animation, Save Project and Build for the selected game; the user does not choose an export file format.

The current `DoctorFlux/fighterport-studio` repository now contains the independent C#/.NET 10/Avalonia application. Its existing editor, Core services, renderer, native worker and `.fighterport` JSON persistence are the implementation base. BrawlCrate and `fighterport-studio-next` remain interaction and native-format references. The older `.fpstudio` XML format is separate from the standalone schema; do not treat the extensions as interchangeable. [C1][C2][C3][R1]

The current M2 plan already records implemented source-library, fitting, batch, contact-analysis, timing and single-container export slices. Unresolved contact cases and their acceptance remain active. GPU/context recovery and real per-monitor DPI acceptance are explicitly deferred by the latest user amendment. Integrate the requested UI/project work into the same master/current plan, retain the open acceptance work, and do not make deferred GPU work an invented prerequisite. [C8]

### What the current code changes about this design

The existing layout is already recognizable as a model editor. The required rework is its navigation, project lifecycle and target ownership. These findings come from source inspection; they do not claim a newly executed desktop session.

| Current behavior | Required change |
| --- | --- |
| Main toolbar contains nine unrelated actions | Move actions to Project, the local asset browser and the relevant editing tools |
| New loads the procedural sample character | Create a valid named project with no model required |
| Model import replaces the document and clears its project path | Add or deliberately replace an asset inside the existing project and selected target |
| The project already has a Targets array but one required scene and global authoring collections | Give targets independent authored content and add a project shell that can exist before a scene |
| Source library, comparison, fitting and batch Apply already work | Reorganize their controls while preserving their services, source proofs, stale-result guards and Undo |
| Native output actions are spread across bone and morph inspectors | Resolve their formats from the selected resource or mod target and present contextual save/build actions |
| Save protects newer edits, but replacement and close paths lack a dirty-work decision | Preserve save ownership and add one Save, Discard or Cancel lifecycle |

`FighterProject.Targets` is not a single legacy TargetGame string. Its existing `TargetProfile` contains ID, game/platform/build identity and mode, but does not yet own separate scenes, clips, native workspaces or target settings. Global source registrations also support several named game builds; those read-only locations are distinct from authored project targets. [C2][C3][C4][C9]

### Product rules

- One editable target document supplies skeleton selection, transform values, animation evaluation, timeline, native output and undo. Read-only source previews use the same Core services and can have independent clocks with optional time linking.
- Bone transforms key automatically in the active editable animation. Do not require Add Keyframe, a recording button or an auto-key switch before ordinary animation editing. The right side in Edit is the skeleton hierarchy, not a Bone Inspector.
- Native assets retain their native format, complete source bytes, resource hierarchy and dependencies. Common editing/evaluation views must preserve links to that native data and must not become a reduced replacement for it.
- Native animation editing remains available. Full support includes skeletal, visibility, texture-transform, texture/palette-pattern, material-color, morph and scene animation families, plus model/material data. Preserve all native content even when a particular operation is not implemented yet.
- Simple and advanced controls edit the same data. An advanced control changes visibility, never the project format or interpretation.
- Set Up IK/FK attaches editor controls to the imported skeleton. The normal workflow preserves the existing deform bones, hierarchy, rest transforms and skin weights.
- Every reused animation retains its authentic source model, skeleton, revision, channels and timing.
- Save Project preserves native-backed editable work and cross-format bindings. Build derives the selected game's native files automatically; preparing assets, building a mod, installing a test build and publishing retain distinct outcomes.

These rules continue the current standalone requirements and ownership boundaries. [C2][C5][C8]

## 2 The main workspace

### Permanent layout

Keep the familiar left browser, central viewport, right skeleton list and bottom transport, with a compact project header above them. Avoid introducing a new window for each feature. The inspected BrawlCrate viewer has a docked Bones tree, direct Move/Rotate/Scale tools, and Bones/Keyframes/Files controls. Batch selection and fractional timeline handling are FighterPort additions. [R11]

| Region | Normal contents | Contextual expansion |
| --- | --- | --- |
| Top | Project name, save state, active target, Add, Save and Build | Project menu, target setup and supported contextual native save actions |
| Left | Project, Library and Targets tabs; current animation list | Objects, textures and native resources for the selected asset |
| Center | Target model, floor, camera controls and Move/Rotate/Scale tools | Compact transform values beside the viewport tools, source comparison, skeleton/control overlays and contact markers |
| Right | Simple expandable skeleton hierarchy for the editable model | Retarget fitting and mapping controls when explicitly in Retarget; no Bone Inspector in Edit |
| Bottom | Play/pause, step, current frame, scrubber, loop and preview speed | Curves, Jobs and Problems drawers |

Project is the normal left tab. Inside it, Animations is the default view while working on motion. Preserve the existing Animations, Objects, Textures and Resources functions as local asset views. Bones is the right-hand hierarchy. Put detailed Keyframes with the timeline, native Files with the resource browser, and rig setup in a contextual Controls view. Preserve those functions without adding a separate Bone Inspector. [C2][C5]

Place New/Open/recents in Project or File. Put New Animation beside the clip list, source registration inside Library or Settings, and Set Up Animation Controls in Controls or the skeleton's context menu. Keep Undo/Redo available in compact controls and shortcuts. Add offers the supported model, animation, source and target actions. At narrow desktop widths, secondary actions move into an overflow menu instead of crowding one horizontal row.

The ordinary output action is Build for the selected game/configuration. The target profile chooses native formats, container entries, filenames, dependencies and destinations; there is no required Export as CHR0, DAT, FBX or other format-selection step. Retain the current native writing, container insertion, batch and model/motion save services behind that workflow. A contextual Save Native Asset action can save a project-owned resource in its existing native format, without an output-format picker, when the writer supports the edited content. Existing detailed native-file operations may remain under Native Files for specialist work. Build Mod is available only when its actual builder exists; a working native motion writer is identified as that supported preparation step. [C2][C7][C13]

The active target must be written in full, for example `Project+ - Practice build`. An adjacent scope label says `Shared character` or `This target - Project+ Practice`. Avoid relying on an icon or color to identify the destination of an edit. A selected source motion never silently becomes the active target model.

At approximately 1440 by 900, use a starting left pane of 240 logical pixels, a right skeleton pane around 220, and a timeline around 150 high; the viewport receives the remaining space. A contextual fitting panel can be wider when needed. These are proposed starting dimensions, not fixed requirements. At smaller desktop sizes, reduce optional chrome and collapse a side pane before making the viewport unusable. Splitters, panel visibility and Reset Layout are available. Preserve useful hit targets and text at 100, 125, 150 and 200 percent display scaling. Actual monitor-transition validation belongs to the explicitly deferred GPU/DPI acceptance work; it is not a prerequisite for this UI/project slice.

Use native-feeling compact controls, system/light/dark appearance, restrained selection color and readable 13 to 14 pixel text. Avoid dashboard cards, a permanent welcome panel, or rows of inactive planned features in the editor. Icons supplement short labels for important actions.

### Edit and Retarget in one workspace

Open an ordinary project in Edit. The target model occupies the main viewport, the current animation list remains visible, and the right skeleton tree stays available for bone selection. Selecting a bone does not open or replace it with an inspector. Retarget is a contextual workspace within the same character project. Entering it reveals source selection, comparison and fitting controls; returning to Edit restores the skeleton tree and its selection without changing the editable target or discarding a prepared draft. Keep the existing comparison window usable during gradual integration rather than replacing it with a second solver or document implementation.

Project and Library identify the left browser's contents. The active target picker identifies where authoring changes go. Edit and Retarget identify the current task. These are separate questions; changing one must not silently change the others. Target management can open from the target picker or its existing Targets view. Build opens a destination/readiness panel, not another permanent top-level workspace. Show native origin in asset details, such as Model Brawl MDL0 and Motion Melee, independently of Mod Target Rivals II. Do not make either native label a required output-format selection. Supported partial preparation and native saves retain precise outcome labels until the complete builder exists.

The first view has one strong local action at a time: Add Model for an empty project, direct bone manipulation while editing an animation, Preview Fit before transfer review, and Apply Selected after valid results exist. Bone edits need no extra key confirmation. Save remains a consistent project action. Source registration, detailed native resources, mapping evidence and diagnostics remain reachable without occupying every panel at startup.

### Viewport behavior

Opening a valid model fits the camera once. Selecting a clip, changing a slider or switching a costume should preserve the camera. Provide Frame Selection and Reset View. Select, Move, Rotate and Scale operate on the active editable selection; camera movement remains independent.

Show the target model name and current clip in a quiet viewport corner. When comparing, label the original source and the target explicitly. Source preview is read-only. Retain the implemented independent cameras/transports and optional seconds-based linking, including the existing recipe-linked time conversion for trimmed or retimed results. Normalized phase may be added as an explicit comparison option. Source selection must preserve the main target document, fractional playhead and undo history. [C5][C6]

The current main viewport Compare button shows another clip on the same model; the library comparison shows the authentic source model. Give them distinct labels: Compare Animation with an explicit clip choice, Show Source for the source pane, and Fit Animation for the transfer review. Do not silently choose the next clip when the user expects a source comparison. [C2][C11]

Keep transform, skeleton and contact overlays optional. A selected editable animation supplies the key destination, with its clip name, frame and target scope visible. A quiet timeline status can say Keys on edit; this describes the behavior and is not an arm/record toggle. Editing the rest or bind pose remains a separate deliberate operation.

### Skeleton list and contextual properties

The right side in Edit shows the actual selected model's bone names and parent/child hierarchy as a compact expandable tree. Selecting a tree row highlights the same bone in the viewport; selecting a viewport bone reveals and selects its tree row. Keep hierarchy expansion, selection and scrolling stable while scrubbing, editing and undoing. Empty projects show no invented skeleton. Source comparison does not substitute the source rig for the editable model's tree. The BrawlCrate Bones panel likewise builds its hierarchy from the target model's root bones. [R14]

Move, Rotate and Scale stay beside the viewport. Optional precise X/Y/Z fields are a compact part of those tools and follow the selected bone; they do not form a second Bone Inspector. Applicable FK/IK setup lives in Controls or a context action. Animation details, model/material/native properties and target setup open through their own contextual views. Multiple selected clips expose the existing batch fitting controls with mixed values represented as Mixed.

Only expose the most useful settings immediately. Use named disclosure sections such as Adjust fit, Start and end, Timing and Native properties. Opening an advanced section does not navigate away from the clip or restart playback. Each setting has a clear reset that either returns to the inherited value or to the automatic fit; these are different operations and need different labels.

### Bone edits create keys automatically

With an editable animation selected, moving, rotating or scaling a bone through the viewport immediately previews the change and creates or updates the affected transform-channel keys at the current animation time. Numeric edits to the same transform follow the same command path. There is no required Add Keyframe button, recording action, Apply Pose step or auto-key switch. Repeating an edit at the same bone/channel/time updates that key rather than adding duplicates. Untouched channels, bones, interpolation data and native companion layers remain intact.

One completed drag or numeric edit is one undoable operation. During a drag, update a transient preview; commit the final affected keys at gesture completion. Preserve the native curve policy and capture any changed neighboring tangents or required entry initialization in that transaction. Escape or a cancelled gesture restores the starting pose without leaving keys. Undo restores the previous values, key existence and affected curve state; Redo restores the committed result. A no-change gesture creates no key. When supported IK/contact controls move several bones, capture the actual affected transform channels in the same transaction rather than keying the entire skeleton.

Key markers update immediately for the selected bone and animation. Selecting a bone or clip, changing transform mode, scrubbing, camera movement, playback and programmatic display refresh never author keys. Starting a transform gesture pauses playback at a stable time. Capture the exact project, target, clip, bone and time for the gesture; a session or target change must cancel or finish it in its original owner, never write into the newly selected target. Keys are project edits, not an automatic disk save or build.

With no editable animation selected, preserve the existing model-only authoring-pose workflow and show that pose scope. Do not create a clip or mutate the rest/bind pose implicitly. New Animation is the explicit way to begin a new motion. Library/source previews remain read-only; editing a reused motion acts on its editable project/target revision while preserving the native original and its source identity.

FighterPort's zero-based animation frame 0 is a valid key time. Keep fractional times and native adapter indexing conversions; do not copy BrawlCrate's viewer convention that reserves its frame 0 for a base pose. In the inspected BrawlCrate implementation, transform edits write CHR0 keys at viewer CurrentFrame minus one when an animation is active. Preserve that direct editing interaction while keeping FighterPort's time model explicit. [R13]

## 3 Project Manager and creation

### Startup

The Project Manager contains New Project, Open Project and recent projects. Open Model is a secondary quick-inspection path; Save as Project promotes that session when the user wants to retain work. A recent-project row shows its name, location, last saved time, thumbnail and configured targets. Read this metadata without importing every model or starting external tools.

Reopening a project should load the shell first, restore the last active target and selection, and prepare only the assets needed for that view. Other targets and the global catalogue load on demand. An unavailable game installation should leave the project editable and identify the affected target.

### New Project

Use one short form:

| Field | Initial behavior |
| --- | --- |
| Project name | Required; for example Goku |
| Folder | Suggested user project location with a change action |
| Target games | Optional multi-select; Add later remains valid |
| Starting model | Optional drop area or Browse |

Create writes a valid empty project and opens its workspace. No animation, rig mapping, ROM path, Modkit or fighter slot is required just to create and save. Unsupported target integrations can be recorded as planned targets, but creation must not claim that a native project or package was generated.

The empty workspace shows the character name, saved state, optional targets and Add Model. It does not load the procedural fixture or invent a skeleton. In the current code a Scene requires bones and an EditorDocument immediately works with a scene; retain those validation rules. Add a lightweight project workspace above the scene editor, with no active scene document until a real asset is selected. Model-only editing already has an authoring-pose path and must remain supported. [C3][C4]

Useful creation choices are Blank character, From model and From an existing supported project. A template can supply target structure and settings when the corresponding adapter is implemented. Keep less common import and duplication commands in the Project menu.

Use `Goku/Goku.fighterport` as the visible example. If the chosen folder already holds another project or unrelated files, offer a new child folder or open the detected project. Do not reorganize unrelated files.

Import normally adds an asset to the open project and selected scope while preserving its project ID, path, targets and existing edits. Replacing the active model is a separately labelled, undoable asset operation with compatibility review. New, Open, project replacement and Close share Save, Discard and Cancel handling when work is dirty. Await a successful save before continuing; cancellation or save failure leaves the current workspace intact. A normal target switch retains its working session and should not demand a save every time. [C2]

### Adding another game

Add Target asks for the game, build or framework, and the supported mod type. Examples of mod types are character replacement, costume replacement and additional character. Display only operations provided by the adapter. A game with several registered installations asks which named installation to use; a detected default can be preselected.

FighterPort then proposes the character model, existing compatible source links and applicable shared animation assignments. The user can keep shared assets or select an existing game-specific model. Record the target immediately even if its native template or toolchain needs attention. Show the next actionable requirement in the Targets panel.

## 4 Everyday workflows

### Open and animate

1. Open a project, or create it and add a model.
2. Select an existing animation or add a separate motion file.
3. Play or scrub to a frame, select a bone from the right skeleton tree or viewport, then move, rotate or scale it; its animation keys update automatically.
4. Save Project.

Model-only authoring is valid. New Animation creates an editable clip on the selected existing skeleton without requiring a donor motion. Loading a motion package should discover its actual clips and preserve their native identities; it should not demand a companion manifest invented for the UI.

### Choose what to reuse

The source library must support both of the user's intended workflows. Present the choice when it changes the operation, and retain it for the current source/target session.

| Action | What remains the destination | What FighterPort creates |
| --- | --- | --- |
| Use animations on this model | The already selected target model and deform rig | Derived editable clips fitted to that exact target rig |
| Add source model and animations | A new model entry in the chosen shared or target scope | Managed copies or pinned revisions of the source mesh, rig, skinning, materials and selected motions |

Adding the source model must not replace another model silently. It retains its native format and full asset relationships when assigned to any target configuration. The target adapter derives any game-required model, rig, material or animation representation without changing the source asset's identity. Show a focused compatibility decision only when an automatic policy cannot resolve a meaningful difference. Importing a source character intact does not establish that a destination game's runtime can read its original files. Keep source originals immutable and make each required target adaptation a separate derived revision.

For the required mixed-format example, select a native Brawl model and a native Melee motion with its authentic source skeleton/model context. FighterPort decodes each through its own adapter, fits the skeletal motion to the Brawl rig, and saves both native sources, their binding, the fitting recipe and any target-local edits. The Brawl model keeps its materials, skinning and existing native companion resources. Choosing another skeletal motion does not delete or rewrite those resources; companion layers have explicit bindings and blend/replace policy. A Brawl build writes the derived motion through the Brawl writer; a Rivals II build derives the native assets required by its Modkit profile. No manual intermediate-format round trip is part of this workflow. This is a required product scenario; Melee parsing and the complete destination integrations remain planned in the audited implementation. [C4][C13]

For the animation-only action, show the exact destination beside Apply, such as Goku - Project+ Main. Selecting a new source animation never substitutes its model for that destination. For a batch, keep the currently previewed row separate from the checked membership; previewing another row does not select it for Apply.

### Use a source animation

1. Keep the intended target model visible and open Library.
2. Select a motion and optionally preview it on the original source model.
3. Choose Use on This Character.
4. FighterPort resolves the source association, restores verified rig setup or requests only the ambiguous roles, retargets, and prepares an uncommitted target preview.
5. Adjust and review the result, then choose Apply to commit the verified result as one undoable operation. Assign it to a supported game slot when needed and save.

The original source remains in the library. The new target clip appears in the same animation list as ordinary authored clips. Clicking it plays the prepared result; a heavy solver does not rerun every display frame.

### Work across games

For example, Goku has a shared Run motion, a Project+ target using one native rig and a Rivals II target using another. Select Project+, refine the arm clearance and save. Select Rivals II and keep its existing arm clearance. The shared source remains the same, while each target stores its own fit and native assignment.

When switching targets, preserve the semantic animation selection where a corresponding assignment exists. Preserve the playhead in seconds and clamp only if the target clip is shorter. If the corresponding clip does not exist, show the unassigned slot and an action to create it; do not substitute a different motion silently. Comparison can explicitly align normalized phase when useful.

### Assign a move

Drag a clip onto an animation slot or use Assign to State. Slots come from the selected game's verified profile and have required, optional, alias, fallback and unbound states. A move can have multiple phases, grounded and airborne variants, loops and follow-ups. Shared names such as Forward Aerial express intent; the target owns actual runtime identifiers, windows and gameplay data.

## 5 Source library and automatic fitting

### Source organization

Retain the implemented source-bound library and native Brawl or authored-project registration. Browse provider or game, build/collection, character, outfit/model, then animations, omitting irrelevant levels for sources such as Mixamo. The same fitting UI applies when a new source provider is actually implemented. A registered folder, archive/image or game installation does not establish extraction or native format support. [C1][C6]

The global library is shared across projects. A project references immutable source revisions and keeps the snapshots needed to reproduce its used results. Index metadata and extract selected assets as needed. Do not duplicate the full game library for every character project. Removing a catalogue registration must not delete source files or already saved target revisions.

The current library uses the word cache for preserved, content-addressed original blobs as well as prepared data. Those original blobs may be needed for offline reuse and must not be treated as disposable thumbnails. Keep preserved source storage distinct from rebuildable preview caches. A project copy/relocation must collect the verified inputs it uses or resolve them by content identity. [C10]

If a source animation has no proven original model, show Choose Source Model once for that source group. The saved association is tied to the model, skeleton and resource revisions. A matching filename or familiar bone names alone are insufficient.

### Automation with visible results

| Task | Automatic behavior | When to show a focused decision |
| --- | --- | --- |
| Format and resource discovery | Inspect signatures and indexed native resources | Unsupported or ambiguous format/version |
| Model and motion association | Reuse exact approved source identity | Multiple plausible source models |
| Rig controls | Restore matching reviewed setup; suggest missing semantic roles | Ambiguous chain, orientation or anatomy |
| Retargeting | Apply proportions, explicit root policy and supported constraints | Missing roles, incompatible channels or unreachable contact |
| Bone keying | Create or update affected keys when a bone transform is edited in an editable animation | No editable animation: retain authoring-pose scope and offer New Animation |
| Organization | Place imported or created items in their intended project category | Name collision or user-requested custom location |
| Target setup | Resolve selected versioned template and dependencies | Missing toolchain, unknown framework or unresolved slot |
| Animation assignment | Suggest verified compatible bindings and legal fallbacks | Several candidates or no valid binding |
| Build | Prepare a dependency plan, validate, then run supported steps | Unsupported native operation or external edit conflict |

Automatic actions should expose a concise result such as `Mapped body chains - review left hand` or `Prepared 12 clips - 2 need contact review`. Avoid invented confidence percentages and blanket Perfect or Game ready labels. Full diagnostics are available on demand.

### Simple fit controls

Retain automatic fitting and contacts with a compact Adjust Fit section. Use the existing five parameters and their existing units/bounds; moving controls must not reinterpret their stored values. The first four fit ordinary body adjustments; Free Hand Reach can live under Advanced if it is less often needed. [C6][C7]

| Control | Current range and default | Meaning and constraint |
| --- | --- | --- |
| Arm spacing | -15 to 30 degrees; 0 | Adjust arm clearance while respecting supported contacts |
| Stance width | 50 to 150 percent; 100 | Adjust stance scale within the reviewed fitting policy |
| Pelvis height | -10 to 10 percent of leg length; 0 | Offset the pose without changing native skeleton/bind transforms |
| Body lean | -20 to 20 degrees; 0 | Adjust supported body lean while retaining the required contact checks |
| Free hand reach | 80 to 120 percent; 100 | Adjust free-hand goals; fixed prop grips require their own constraints |

Motion fidelity currently accepts only 100 because its adjustable optimization is not calibrated. Do not expose an operative Motion Strength or Motion Fidelity slider. Hide the future preference from normal controls, or show a read-only advanced value when it explains a result. Existing sliders and native-correct output do not establish universal contact or anatomy acceptance. [C7][C8]

Advanced sections expose only implemented controls. Additional left/right or per-chain adjustments, hand/foot orientation, pole direction, contact-interval authoring, mirror and interaction authoring remain gated by their actual capabilities; the current retarget window explicitly leaves mirror and interaction/native actor packaging unavailable. Retain supported root and timing policies with the solver's exact policy names in tooltips or advanced descriptions; the normal UI explains the observable effect.

Set Up Animation Controls, Auto Fit and Retarget remain separate commands. A user can pose or correct a motion on its original rig using IK, without retargeting. Controls attach to existing bones and bake to target keys as required. Fully unrigged mesh skeleton generation is outside the current core scope. [C8]

### Timing and transitions

Preview Speed belongs beside playback and changes viewing only. Retain the implemented explicit trim/export-speed operations and their conflict checks; give per-clip timing a clear contextual section. Preserve duration and protected gameplay events by default. Frame rate, sample count, end-exclusive duration, last key and loop-closing sample remain separate concepts in Core. The editor uses zero-based fractional frames; native adapters own any required indexing conversion. [C3][C7][C8]

Start and End fitting offers Fit Start From and Fit End Into, with Idle and Fall shortcuts and another compatible target clip/pose/phase as a valid choice. Fit within existing edge frames by default. Show the selected windows and protected action interval on the timeline. A duration change requires an explicit retiming choice, and a physical conflict needs a visible unresolved result.

## 6 Batch animation work

Selecting several clips exposes a contextual batch fitting panel headed by the exact selection count; it does not replace the right skeleton tree during ordinary bone editing. Reuse the existing RetargetBatch and fitting workflow. Keep checked batch membership separate from the one currently previewed clip. Show the source model group, exact destination target and setting scope before an operation. For motions from the same original model and skeleton, reuse one reviewed mapping and the same source-to-target default setup. [C6][C7]

Offer three scopes in the fit panel: This clip, Selected clips, and Save as default for this source model and target rig. Saving a default is an explicit action; changing one animation must not silently rewrite every later transfer.

Resolve settings through a defined order: exact rig-pair defaults, shared clip recipe, target preset, then target clip overrides. Calibration itself is identified by skeleton fingerprints, never inferred from an unrelated target preset. Show each effective value's origin and Reset to inherited. Promoting a setting to shared content shows which targets would be affected.

Later explicitly set fields override earlier layers; unset fields inherit. Zero, false and a disabled constraint are explicit values. Reset to inherited removes the override instead of copying today's inherited value. Each applied target revision pins the resolved recipe and source/preset revisions, so a later preset change marks the result stale rather than changing its saved behavior silently.

Mixed values stay Mixed until changed. Applying arm spacing to a selection leaves each clip's other settings intact. Shared batches partition by actual source-model/skeleton revision and target-rig revision. Clips with different rigs must not inherit the first selected clip's calibration.

The current nullable FittingOverrides already distinguishes inheritance from explicit zero/false; preserve that contract. Its Apply to Selected action can intentionally copy all resolved settings. Keep that bulk replacement distinct from a new change-only-this-field batch gesture, with labels and Undo matching the actual operation. [C7]

Prepare batch results in the background. Show representative previews plus a per-clip review list, then Apply creates the target revisions and assignments as one undoable transaction. Users can explicitly apply only successful items; report their count and excluded items. Cancel or failure before Apply leaves current assignments intact. On reopen, an interrupted operation offers retry from a known snapshot rather than applying partial results automatically.

Retain the implemented source-proof, source-revision, document-version, cancellation and duplicate-Apply guards. Add project/target/session identity to the publication boundary when target switching is introduced. A bake launched for Project+ Main must never publish or Apply into Project+ Practice after the user switches views. Preserve stored Redo results and the existing ancestry, contacts, presets and batch receipt transaction. [C6][C7]

Fill Missing is a specialized batch. It protects manual assignments, uses approved donor packs and verified slot semantics, previews the proposal, and identifies unresolved slots. A valid target-defined fallback is allowed; filling unknown requirements with an arbitrary Idle is not. [P5]

## 7 The project data model

### Shared character and independent targets

The current FighterProject record is a scene-authoring aggregate with additional metadata. Introduce a character workspace above or around it, keeping the existing scene document/evaluator/command path for loaded assets. A project can have zero active scene documents; the visible viewport has at most one active editable target session, plus explicitly read-only source/comparison sessions. Inactive targets retain their own authoring revisions and working state. [C3][C4]

The project holds shared identity, outfit definitions, reference artwork, source references, rig setup knowledge and animation intent. Shared animation keys are always associated with an actual rig. A target with a different skeleton creates a derived animation using a saved recipe; it cannot interpret shared bone indices as its own.

Each target owns the chosen model/skeleton revisions, target animation instances, slot bindings, timing overrides, native materials, gameplay, sounds/effects bindings, artwork mapping and external project connection. It can use a shared asset by reference, derive it, or own a local override.

Display these relationships as Shared, Inherited, Edited for Project+ Practice, or Generated from a named revision. Reset to Shared removes a compatible override. Use for Other Targets copies an applicable setting or recipe through an explicit review; it does not reinterpret target-native keys or scripts as universal data.

Outfits and recolor palettes have separate identities. A different gi or Yardrat outfit can own different geometry and materials; a palette changes the appearance of an outfit. Each target maps outfit/palette combinations to its own supported skin or costume slots.

A shared edit marks affected outputs as Needs Update or Needs Review. Preserve target-specific manual edits. If applying a new shared revision conflicts with them, show a comparison and keep the last resolved version until the user chooses how to reconcile it.

### Native assets and mixed format composition

The project preserves native documents and combines their supported behavior through one editor. The common Core scene is an evaluation view, not a complete replacement file format. The current Scene has bounded geometry, texture and skinning assumptions; native data that does not fit those assumptions must remain preserved and identified instead of being truncated or discarded. Expand typed capabilities through reviewed adapters while keeping original content available. [C4][C13]

Retain three related forms of data. First, native assets retain exact original bytes, format/version, container entries, resource IDs, dependencies and provenance. Project-owned native working revisions preserve model hierarchy, rig/rest transforms, skinning, meshes, materials/shaders, textures/palettes, animation curves, flags and unsupported resources. Second, prepared editing/evaluation views decode supported behavior and retain native bindings; they can be rebuilt from the retained native basis and saved edits. Third, target derivatives record the exact source and target revisions, adaptation settings and native destination contract. Importing or switching targets must not replace the first form with either of the other two.

A cross-format animation binding identifies the selected model/rig, original motion and authentic source rig, typed channel bindings, native coordinate/rest-space conventions, timebases, mapping evidence, fitting recipe and target-local overrides. A file extension or coincidentally matching bone name is not sufficient association evidence. A motion loaded without its source rig context remains preserved and requests that context when required. Share prepared immutable data, keep one active edit/undo path, and preserve source/target ownership during asynchronous work.

The universal animation model must account for all native families without flattening them into skeletal keys. BrawlCrate exposes the following distinct animation types; the standalone's current operation coverage is narrower and must remain accurately recorded. [R11][R12][C13]

| Brawl family | Behavior and required binding |
| --- | --- |
| CHR0 | Skeletal transforms; source rig/rest-space, target rig, scale/root and timing policy |
| VIS0 | Object/draw visibility; original object/bone/draw identity and a valid destination binding |
| SRT0 | Texture-coordinate transforms; material, texture slot, UV set and native matrix semantics |
| PAT0 | Texture/palette animation; original resource identities and destination texture/palette bindings |
| CLR0 | Material colors; the appropriate material/register/color-mask semantics |
| SHP0 | Morph position/normal/color behavior; exact base mesh, target arrays and index correspondence |
| SCN0 | Scene animation including cameras and lights; explicit scene bindings and native interpretation |

Skeletal transfer must preserve the preview model's material, visibility and morph layers unless the composition explicitly replaces or blends them. Morph transfer to different topology requires valid correspondence; a rig map alone cannot supply it. Texture, shader, palette, visibility and scene behavior require their own semantic adapters. Engine gameplay/script/event data remains native and separately bound; moving a motion does not automatically translate a moveset. Keep exact timing, interpolation and native identifiers even when the preview uses a common timebase.

Every relevant channel in a derived result records whether it is Transferred, Preserved with original, Needs binding, or Unsupported for this target. Include an approximation/bake result when an explicit target policy is used. Preserved with original means the data survives in the project, not that it is active in the destination. Retain existing lost-channel and review metadata. A required unrepresentable channel blocks a complete-build claim until a supported adaptation or explicit omission policy is selected; the user should resolve the meaningful behavior, not choose a file extension. [C4][C13]

Saving a project preserves the native basis and reconstructible edits, including unsupported data. A contextual native save writes a new project-owned revision in that resource's existing format using a capable writer. No-edit source preservation is byte-verifiable; edited round trips require decoded semantic checks and preserved unrelated resource identities. Do not reconstruct an archive solely from a partial Core view or blindly relocate opaque sections with unknown references. If a writer cannot preserve the necessary content, retain the complete source and report the exact native-save limitation instead of writing a stripped file.

Building for a game resolves the required model, animation, material and container formats internally from that target's profile. The game receives target-native derivatives; original Brawl/Melee/Ultimate/Rivals assets remain in the project. Any SDK-required interchange file is an internal, reproducible build input, not the project's canonical asset or a required manual conversion step. Cross-format interoperability is a complete product goal across the supported game families; a capability table and actual mixed-source fixtures establish progress toward it.

### Target identity

A target record must include stable target ID, display name, game, platform, game revision/region, framework and version, mod type, template identity/revision, target model/rig revision, required slot profile, adapter/toolchain versions, native workspace reference, and output policy. Some values may initially be unresolved, with an explicit setup state.

Keep user-editable display names separate from stable identifiers. `Project+ Main` and `Project+ Practice` can be separate targets. Fighter slots, costume slots, Mod IDs and expansion identities belong to the chosen framework; there is no mandatory universal numeric fighter slot. [P5]

### Suggested descriptor shape

The inspected standalone format is JSON schema 1. Extend it through an explicit versioned workspace representation and compatibility reader. The following fields are a design contract, not a JSON example that loads in today's app. Existing TargetProfile IDs and metadata must be retained when expanding ownership. [C3][C4]

| Field group | Required information |
| --- | --- |
| Header | Product/format identity, schema version, minimum reader version, project ID and name |
| Current revision | Committed revision ID and references to its validated manifest/assets |
| Character | Shared character ID and root definition reference |
| Targets | Stable target IDs and target-definition references |
| Sources | Pinned native/source-model/clip identities and project source lock |
| Native assets | Original and working native revisions, resource graph, dependencies, unknown-data retention and exact identities |
| Compositions | Model and motion references, source rig context, typed layer bindings, mapping/fit recipes and per-channel outcomes |
| Dependencies | Rig maps, templates, slot packs, solver and adapter versions |
| Project settings | Portable authoring and conversion policies |

Absolute game/SDK installation paths, window geometry and machine preferences belong in local configuration. Do not require those paths to match after the project moves.

## 8 Folder organization

The following is FighterPort's internal project structure. Native files keep their formats and resource identities within these categories; the folders do not mandate conversion into a universal interchange file. Actual game folders and target-native derivatives are generated automatically by the selected target adapter/template. A generic FighterPort folder is not itself a valid native mod package.

| Relative path inside Goku | Contents and ownership |
| --- | --- |
| `Goku.fighterport` | Versioned standalone project entry point |
| `ProjectSettings/` | Portable project policies and profile pins |
| `Sources/` | Source registrations, pinned library references, complete selected native originals/dependencies and provenance |
| `Character/Models/` | Shared native model working revisions and their non-destructive edit records |
| `Character/Costumes/` | Outfit identities and their model/material relationships |
| `Character/Palettes/` | Recolor definitions linked to outfits without duplicating their identity |
| `Character/Materials/` | Shared texture/material authoring content |
| `Character/Rig/` | Reviewed maps and editor controls bound to exact skeleton revisions |
| `Character/Animations/` | Native motion references, authentic source rigs, mixed-format bindings, authoring data and recipes |
| `Character/ReferencePoses/` | Reference clips/poses/phases for fitting |
| `Character/Moves/` | Shared move intent and notes with explicit links to target implementations |
| `Character/Audio/` | Editable audio sources and cue definitions |
| `Character/Effects/` | Effect definitions, props and reusable source assets |
| `Character/Artwork/` | Portraits, stock icons, HUD art and editable originals |
| `Targets/projectplus-main/` | Project+ configuration, target derivatives and overrides |
| `Targets/melee-mex/` | Selected Melee/framework configuration and its target content |
| `Targets/ultimate-main/` | Selected Ultimate mod mode and its target content |
| `Targets/rivals2-main/` | Rivals II target and its Modkit connection |
| `Build/<target-id>/<build-id>/` | Committed generated outputs for an exact project snapshot |
| `Exports/<target-id>/<export-id>/` | Legacy or optional specialist handoffs; not required for new-project scaffolding or normal game builds |
| `Reports/` | Project validation, compatibility and build/test receipts |
| `Cache/` | Reconstructible thumbnails, decoded previews and temporary bakes |
| `.fighterport/` | App-managed revision data, save transactions and recovery snapshots |
| `.local/` | Machine-specific source/toolchain paths, session state and job state |

A target folder has `target.json`, then the relevant Models, Rig, Animations, AnimationSets, Gameplay, Materials, Costumes, Palettes, Audio, Effects and Artwork categories. `Native/` holds an explicitly managed native authoring workspace or a record linking an external one. Never treat manually edited native content as disposable build output.

Create the minimum necessary folders at project creation and materialize others when first used. The UI shows friendly categories even when their backing directories do not exist yet. It should not expose every cache, receipt or internal revision folder in normal Project view.

Use friendly, normalized names with stable IDs stored independently. Prevent case-insensitive collisions on Windows and target-specific forbidden names. Rename or move inside the app updates references transactionally. When an external move is detected, relink by verified asset identity; a same-named file with different content is a new revision, not a silent replacement.

### Sources and portability

Imported project assets should normally be managed copies of the selected resources and necessary dependencies. A source catalogue can remain external and large. The project records original identity and stores the snapshots required by its current edits. Do not mistake a disposable cache for the only copy of a used source revision.

Current native references and source proofs can contain absolute paths. Moving the descriptor alone does not establish portability. A managed resolver should store project-relative asset references or content identities, with machine-local bindings for external locations. Relocation verifies bytes and identities before relinking; it must preserve native resource entry identities and original provenance. [C3][C4][C10]

Create Portable Copy collects the referenced assets, profiles, recipes and authoring dependencies needed by the selected targets. It does not include an entire game installation or promise to bundle unavailable native SDKs. Report remaining external requirements. Duplicate Project creates a new project identity while preserving or explicitly copying target identities according to the chosen framework; it must not duplicate a published Mod ID silently.

## 9 Saving loading and recovery

Save must work for empty projects, model-only projects, partially configured targets and incomplete movesets. Validation of project structure is distinct from completeness for a game build.

Save a consistent project revision. With multiple authored files, writing the descriptor atomically alone is insufficient. Stage changed records and asset blobs in a new revision, verify their references and hashes, flush them, then atomically switch the descriptor or equivalent commit pointer. Keep the last committed revision until the new one succeeds. Use copy-on-write or immutable revision files so a failed save cannot produce a mixture of old and new content. The existing single-file temporary-write, flush and replace path remains valid until externalized records require this wider transaction. Create required parent directories during project creation. [C3]

Preserve Desktop's current save-ownership guard: capture the intended authoring document and its content version; mark only that saved version clean. Later edits remain dirty, and opening a source preview must never change which project is saved. Add project-wide dirty/session coordination without weakening that behavior. [C2]

When externalizing content, the committed revision manifest is authoritative on load. Every record and blob it references is immutable. Friendly folders can expose working files, but Save captures changes into new revision objects; readers must not combine mutable working files with committed data. Keep atomic replacement staging on the same filesystem. Recovery and cleanup preserve all objects reachable from retained project revisions.

Save Project commits FighterPort-owned content and the last reconciled native asset references and fingerprints. An external Modkit or other native editor owns its unsaved in-memory documents unless a verified integration can save and acknowledge them. Show native synchronization state separately. A build requiring native content must snapshot or verify the selected on-disk native revision; a FighterPort-only save cannot claim that all external work is saved.

Show clear states: Unsaved project, Saved, Saving, Unsaved changes and Save failed. A failure retains the live edits and points to retry or Save Copy. A Recovery snapshot available state is separate from the last explicit save. A crash recovery restores a coherent revision and never starts a publish/deployment operation.

On reopen, restore project content, chosen target, model/costume, selected clip, playhead, fit settings, mappings, constraints, assignments and local layout where available. Open paused. Preserve all saved target revisions even when a source drive is missing. Actions needing an unavailable asset show Relink Source or the exact dependency; other content remains accessible.

Preserve the current undo guarantees and make project operations transactional. Persisting every historical undo entry is not a prerequisite for restoring saved content, but if undo history is persisted, its asset dependencies must be retained and validated. Opening a source preview or changing a camera never modifies authored data.

Recognize unsupported/newer schema versions before writing. Offer read-only inspection or a migration into a separate copy when supported. Detect concurrent edits by another process or external native tool and show a conflict instead of silently using the last writer. Save Copy and recovery cleanup must preserve referenced revisions, not only current visible assets.

### Existing project compatibility

Read existing schema 1 projects with both legacy inline source snapshots and the implemented LibraryContexts pooling table. Restore the full authentic source scene, models, layers and resources before validation/evaluation; preserve the pooling benefit instead of copying heavy geometry into every target clip. Retain IDs, exact fractional keys, native identities, rig setups, source proofs, recipes, contacts, fitting profiles and batch/export receipts. [C3][C12]

An old Targets array does not record which global scene/clip belongs to each game. Migrate the saved scene into a clearly named Existing Character workspace and retain the target records for explicit assignment. Do not silently duplicate that mutable scene into every target or infer a game from filenames. Keep the original saved file and write an upgraded copy until the upgrade is explicitly selected. Missing referenced context records, incomplete referenced context data or unsupported schemas produce focused errors rather than partial authoring data. An absent ContextKey remains valid for legacy inline snapshots, including inline entries alongside pooled contexts.

## 10 Game specific setup and outputs

Each target profile combines game, version, framework, mod type, native template, naming/binding rules, converters and build/test bridge. Users mostly choose the game and mod type. FighterPort detects or asks for the remaining concrete dependencies as needed.

| Target family | What its profile must own |
| --- | --- |
| Brawl | Native resource bindings, chosen fighter/costume identity, motion/model container templates, gameplay/media dependencies and selected installer/build layout |
| Project M and Project+ | Separate named build configurations, overlay ordering, framework/version and replacement versus expansion rules |
| Melee | Selected game/framework revision, HSD/fighter resources, native animation and gameplay bindings, supported package/project schema |
| Ultimate | Chosen ARCropolis replacement or separately supported expansion mode, fighter/costume identity, native asset paths, scripts/plugins and packaging rules |
| Rivals II | Installed Modkit/template revision, generated Mod ID, native assets/references, state and attack bindings, skins/palettes, media and official test/publish workflow |

These are required profile responsibilities. The current standalone has Brawl native editing/writing, repacking and single-container batch output, while complete character builders and further native game adapters remain later work. Track read, preview, edit, retarget, write, package, install and test independently. [C1][C8][P5]

Capability records identify the operation, native format/version and supported resource/channel kinds. Compute a target's readiness from its actual asset dependency graph. A CHR0 writer must not enable unsupported material, gameplay or complete-character packaging operations.

Complete Brawl support includes SCN0 and every other native animation/model family in scope. SCN0 is currently catalogued/preserved but inactive, and the other families have different read/edit/write/transfer coverage. This user requirement sets the full support goal; it does not establish that the missing adapters or operations are implemented. Keep the current status accurate until each operation has native and functional evidence. [C6][C13]

### Brawl family

Derive destinations from the selected build/template. A Project+ source-build example places its `pf` tree beneath `SDCard/Project+`, but that does not define every Brawl or Project M installation. Project+ and expansion builds need separate supported rules. The official Project+ FAQ also limits the idea of producing a universal ISO build. Do not use Build ISO as the default action for all Brawl-family targets. [R5][R6]

### Melee and Ultimate

The m-ex reference identifies Melee v1.02. Current MexManager source serializes a character package as a ZIP with `fighter.json` plus assets; historical tools and loose DAT exports are not necessarily that package format. Pin the selected framework and importer schema. [R7][R8]

ARCropolis uses a mod root under `sd:/ultimate/mods`. The profile must still supply actual game-relative paths, resource dependencies and the chosen fighter/costume identity. Adding a costume, replacing a moveset and adding a roster entry are different operations. Additional gameplay or expansion tooling needs its own supported setup; folder placement alone does not implement it. [R9]

### Rivals II

Use the real Modkit character template. Its generated assets contain linked model, skeleton, physics/hurtbox, animation, character/attack, skin and rendering data. Preserve that graph when importing the user's assets. Official documentation identifies `Content/ModContent/[ModID]/UnrealAssets` for the character's native content. The Mod ID must come from the actual template, not a fabricated folder name. [R2][R3]

The supplied discovery already found an installed Modkit and existing SonGoku native assets, but does not verify an import automation API. Reuse a selected existing native project when appropriate rather than generating a duplicate character. The exact import-settings sections of the official guide are marked outdated after the 5.8 update; validate settings against the installed kit before implementing them. [P6][R3]

Adding Rivals II to a FighterPort project records an unresolved or linked target first. Link Existing Modkit Character and Create Modkit Character are explicit native setup actions. Preserve the selected real native identity and avoid creating another character automatically. A local target ID never substitutes for the real Mod ID.

Preparing animation/model inputs is a useful early operation. A future integration can update native references through verified supported tooling. Label the outcome Inputs prepared or Native project updated according to what actually happened. For the main mesh, authoring controls and retarget corrections must be baked into supported animation data; a running FighterPort solver is not part of the game asset. [R10]

The documented Modkit Publish action performs cooking and publishing. Do not invent a separate standalone cooking command or treat publishing as an automatic end of Save/Build All. Keep Test and Publish distinct, using the supported native workflow. [R4]

## 11 Build panel and readiness

Build opens a compact panel for the active target or a selection of targets. It shows the next useful action, exact unmet requirements and the last successful result. Target rows report outcomes independently. A failure in Ultimate must not change the Project+ result to failed, or make an incomplete Rivals target look successful.

Use separate dimensions for configured setup, prepared assets, native project update, native package, testing and publishing. They are not a mandatory universal sequence: some integrations expose only prepared inputs, and Rivals has a combined supported cook/publish workflow. Readiness is attached to a specific target and content revision.

Ordinary authoring states can be Saved, Needs setup, Needs update, Needs review, Ready to build, Inputs prepared, Native asset saved, Mod built, Test recorded or Unsupported operation. Use the outcome actually achieved and identify any preserved-only, unmapped or approximated channels. A successful build state must not imply that gameplay has been tested. Include the target/game/toolchain revision when inspecting a result. Normal actions choose the game/configuration; native formats are resolved automatically.

Before a build, freeze the relevant project snapshot and show its planned outputs. Stage work separately, validate, decode/reopen supported native output, then commit into a new build directory. Preserve the last successful build if any later step fails. Edits made while a job runs remain in the live document; finishing that job never replaces newer work with its old snapshot.

Native workspaces may be edited in their own tools. Record previous exported hashes and native identities, detect external changes, and reconcile before overwriting managed native assets. New SDK output is not permission to delete unrelated files inside a Modkit or mod build.

Native updates use a single-writer lock keyed by canonical workspace identity. Detect overlapping native destinations and deployment slots across selected targets before writing. Two jobs may share read-only sources but cannot concurrently update the same native asset graph. Conflicting deployments require selecting one target or configuring distinct destinations; use the bridge's supported concurrency even for different Mod IDs in one editor project.

Build receipts record project/target revisions, input and setting hashes, actual tool and adapter versions, output file manifest, diagnostics and validation results. Test receipts add the exact build, environment and observed result. Deploy or Install transfers one chosen build to a configured test location with a file manifest and rollback record. Publishing remains a distinct deliberate command.

## 12 Implementation sequence

### Continue the existing foundation

Read `DoctorFlux/fighterport-studio` AGENTS.md, master plan, current queue and latest UI/architecture/status documents. Reconcile differences against the user's latest decisions. The current main tree is the standalone implementation, not the old BrawlCrate-based application described by historical revisions. Preserve the independent desktop, existing-bone controls, native resource editing and one Core evaluation path. [C1][C2][C8]

Record this UI/project request as a bounded slice in the same master/current plan. Retain the active unresolved-contact acceptance work and do not mark M2 complete because the new shell is finished. GPU/context recovery and per-monitor DPI remain documented deferred requirements. Existing source-library and RT0-RT5 implementations are the foundation for this slice. [C8]

### First project slice

Implement Project Manager, empty/model-only creation, the compatibility loader, Save/Save Copy, dirty-work decisions, active target ownership and folder resolution. Introduce Project+ Main and Project+ Practice as two independent target configurations, plus a differently configured game target. Prove that adding/importing assets preserves project identity, that edits survive close/reopen, and that they do not leak between targets. Only supported native operations are enabled.

### Bounded delivery order

Deliver the first lifecycle change before coupling it to the whole interface rework: create a named empty workspace, save and reopen it, then add a real model while preserving project identity and dirty-work decisions. Follow with managed folder resolution and independent target sessions, then compose the simplified shell and existing retarget controls. Each step should leave the current native editor usable. The source/library/solver work and unresolved M2 acceptance remain tracked in the same execution plan.

The first demonstrated multi-target case should use two named Brawl-family configurations to exercise actual editable/native behavior and one unresolved non-Brawl configuration to prove that setup metadata can be saved without claiming adapter support. Cross-rig and additional-game native acceptance follow through their own adapters and fixtures.

Native retention and mixed-format references belong in the initial project storage contract, even before additional adapters can evaluate them. After the required native adapters exist, demonstrate Brawl model plus Melee motion with authentic source context, intact Brawl companion resources, project save/reopen and automatic Brawl-target serialization. Then verify destination-specific derivatives for further games. Do not use a mockup, filename label or same-format procedural fixture as proof of native cross-game interoperability.

### Workspace integration

Consolidate browser, target selector, right skeleton tree, viewport transform tools and timeline around existing document services. Route direct bone transforms and compact numeric edits through the same automatic key command, preserving grouped Undo/Redo and native channel identity. Preserve animation selection/playhead during search, edits, Undo and target switching where compatible. Make the one-clip import/play/edit/save workflow easy before adding a large batch surface.

### Library and fit

Move the existing LibraryDrawer, preview panes and RetargetWindow workflow into the new navigation progressively. Retain authentic source reuse, mappings, fitting overrides, automatic contacts, timing, batch review, source verification and one Apply/Undo transaction. Show the five implemented fit parameters, keeping uncalibrated motion fidelity unavailable. Continue the open contact/anatomy acceptance work through the existing solver; the UI rework must not create another bake/evaluation path. [C6][C7][C8]

### Target integration

Implement versioned slot profiles, protected assignments, native template ownership and per-target build receipts. Connect one verified target output at a time. Then add target comparison, additional frameworks, transition fitting, Fill Missing and broader complete-character features in the existing dependency order. Keep future UI commands out of the normal toolbar until useful behavior exists.

### Concrete code responsibilities

| Current code | Responsibility after refactoring |
| --- | --- |
| `MainWindow.cs` | Workspace header, project/target navigation and lifecycle coordination |
| `EditorDocument.cs` and `ProjectStorage.cs` | Preserve scene document behavior and compatibility; add workspace ownership/persistence around them |
| `AnimationBrowser.cs` and resource tabs | Fast project animation browser with retained object/texture/resource access |
| `LibraryDrawer.cs` | Source catalogue and source selection using existing registration/proof services |
| `RetargetPanel.cs` and `RetargetWorkflow.cs` | Reusable fitting review around existing batch, settings, source analysis and Apply behavior |
| `AnimationPreviewPane.cs` | Independent source/comparison viewport and transport |
| Bone/control sections and `MorphEditor.cs` | Right skeleton tree, linked viewport selection, direct transform/IK commands with automatic keys, and separate contextual morph/native editing |
| `Timeline.cs` and transport section | Fractional target playback, selected-bone key markers, preview speed and loop controls without key creation during navigation |
| `SourceWindow.cs` and `SourceRegistry.cs` | Global read-only game/source/build registrations, separate from project targets |

These are component responsibilities, not a request to invent duplicate evaluators or force every control into a new class. Extract the smallest useful units and preserve existing automation IDs where they remain meaningful. [C2][C3][C6][C7][C9]

## 13 Acceptance criteria

| Scenario | Required result |
| --- | --- |
| Empty project | Create, save, close and reopen without a model, animation or SDK |
| Add model | Import keeps project ID/path, target configurations and existing edits; no procedural substitute is created |
| Model only | Imported mesh/rig opens; New Animation and IK/FK setup need no donor |
| Familiar editor | Visible clips, real mesh deformation, viewport Move/Rotate/Scale, right skeleton tree and a working timeline remain available; no Bone Inspector is required |
| Automatic bone keys | Editing a bone in an editable clip creates or updates its affected keys at the current frame without Add Keyframe, Record, Apply Pose or an auto-key arm toggle |
| Key updates and Undo | Re-editing the same channel/time updates one key; one drag or numeric edit is one Undo/Redo transaction; no-change and cancelled gestures leave no new keys |
| Non-authoring navigation | Bone/clip selection, scrubbing, playback, camera changes and field refresh do not create keys or dirty the project |
| Skeleton selection | Right tree and viewport select the same actual bone; parent expansion and selection persist; source preview never replaces the editable skeleton |
| Key time and ownership | Frame 0 and supported fractional frames key correctly; source originals stay read-only; switching targets cannot redirect an in-progress edit |
| No active animation | Model-only posing remains valid, with no implicit clip creation or bind-pose mutation; New Animation starts an editable motion |
| Project relocation | Moving the project folder preserves managed assets; external sources relink by verified identity |
| Multiple targets | A Project+ fit change leaves Rivals II overrides and native content unchanged |
| Same game targets | Project+ Main and Practice retain separate documents, settings, native destinations and working history |
| Target switch | Preserve intended slot/clip and seconds where possible; clearly show absent assignments |
| Shared update | Affected results become stale; manual target work is preserved for review |
| Source reuse | Each clip retains the real source rig/model and immutable provenance through save/reopen |
| Native retention | Native Brawl imports retain complete original files, container entries, model/material/skinning data and every animation family; unknown resources survive project save/reopen |
| Mixed native formats | With implemented adapters, a Brawl model evaluates Melee motion using its authentic source rig while both native sources and their independent identities remain intact |
| Typed channel composition | Replacing skeletal motion leaves existing model companion layers intact; each transferred, retained-only, unmapped or unsupported channel is accounted for |
| Automatic native output | Selecting a game/configuration determines all output formats and paths; no manual format picker or intermediate-file round trip is required |
| Native save fidelity | Supported native edits reopen correctly and preserve unrelated resources; unsupported content cannot be silently omitted by reconstruction from a partial scene |
| Batch transfer | Settings apply only to selected fields/clips; reviewed rig pairs are grouped correctly; Apply and Undo are atomic |
| Interrupted save | Reopen the last committed coherent revision or offered recovery; never a partial file graph |
| Save ownership | Source preview still saves the intended target; edits made during a save remain dirty |
| Legacy migration | Inline and pooled schema 1 sources preserve full content/identity; target ownership is not guessed |
| Late retarget result | Switching target/project prevents publication or Apply into a different authoring session |
| Failed build | Keep the previous successful output and live edits intact |
| External native edit | Detect conflicts; do not overwrite user edits or unrelated native files |
| Performance | Large-list interaction and background work do not run solvers/decoders in playback or restore excessive idle rendering |
| Native correctness | New supported output reopens with correct timing, poses, channels and preserved sibling resources |
| Capability honesty | Each target reports actual operation support; preserving native bytes is distinct from preview/edit/transfer support, and a motion output cannot mark a full character tested |
| Reuse route | Adding source model plus motions creates a new entry; animation-only transfer retains the chosen target rig |
| Context switching | Edit/Retarget, Project/Library and active target remain independent selections |
| Preview membership | Inspecting a batch row leaves its checked Apply membership unchanged |
| Complete character | Required target gameplay, media, outfits, references and conditional assets are accounted for before claiming completeness |

Use meaningful integration and native round-trip checks for these risks. Retain the existing regression suite; a new mock UI or a metadata-only assertion cannot substitute for displayed deformation or native output evidence.

## 14 Codex integration note

Place this design in the current `DoctorFlux/fighterport-studio` repository as `docs/UI_AND_PROJECTS.md`, linked from its master plan. Merge the first project slice into the existing executable queue and retain its unresolved acceptance requirements. Keep proposed schema/paths adaptable to the actual serializer. Update `UI_REFERENCE.md`, `ARCHITECTURE.md` and target-profile documentation with implemented decisions and measured results.

The companion Codex brief gives repository-specific implementation instructions. The project hub, independent authored targets and reorganized shell are proposed work. Existing library, five fitting sliders and batch operations are source-verified features to preserve; their presence does not complete native game builders or unresolved M2 acceptance.

## Sources

The current standalone repository was inspected at commit `5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d`. Current source and explicit user amendments supersede historical bootstrap/snapshot assumptions. Source inspection establishes code behavior and recorded status; it is not fresh Windows execution or game acceptance.

Current repository source root: https://github.com/DoctorFlux/fighterport-studio/tree/5605bf7e69b0d09f7d78bc799f4a3b9cc38e218d

- [C1] `README.md` and `docs/DEVELOPMENT_STATUS.md`: current standalone, implemented workflows and open acceptance.
- [C2] `src/FighterPort.Desktop/MainWindow.cs`: layout, document replacement/import, save ownership and lifecycle.
- [C3] `src/FighterPort.Core/EditorDocument.cs`: schema 1 project aggregate, scene editing and atomic persistence.
- [C4] `src/FighterPort.Core/Scene.cs`: scene validation, native references, source identities and TargetProfile.
- [C5] `docs/UI_REFERENCE.md`: implemented editor/source/retarget interaction reference.
- [C6] `src/FighterPort.Desktop/LibraryDrawer.cs`, `AnimationPreviewPane.cs` and `RetargetPanel.cs`: source selection, comparison and fitting UI.
- [C7] `src/FighterPort.Core/RetargetWorkflow.cs` and `src/FighterPort.Desktop/RetargetPanel.cs`: fitting bounds, nullable overrides, batch lifecycle and motion-fidelity restriction.
- [C8] `AGENTS.md`, `docs/MASTER_PLAN.md` and `docs/plans/current.md`: latest authority, M2 acceptance and deferred GPU/DPI work.
- [C9] `src/FighterPort.Core/SourceRegistry.cs` and `src/FighterPort.Desktop/SourceWindow.cs`: independent global source/build registrations.
- [C10] `src/FighterPort.Core/AnimationLibrary.cs` and `tests/FighterPort.Tests/LibraryChecks.cs`: preserved originals, prepared source packages and offline reuse.
- [C11] `src/FighterPort.Rendering/SkinnedViewport.cs`: existing same-model next-animation comparison.
- [C12] `src/FighterPort.Core/ProjectStorage.cs`: pooled authentic source contexts and legacy inline reading.
- [C13] `docs/FORMAT_MATRIX.md`: current operation-specific native and cross-game coverage, preserved/inactive SCN0, partial companion behavior and complete-build limits.

- [P5] TARGET_PROFILES.md, supplied snapshot 19 October 2026. Per-framework target identities, completeness and slot rules.
- [P6] TARGET_RIVALS2.md, supplied snapshot 19 October 2026. Installed Modkit discovery and unverified bridge boundaries.
- [R1] FighterPort older project persistence, inspected revision 96a4e0e81b1ad79837c307d57197ecd4c756179b: https://github.com/DoctorFlux/fighterport-studio-next/blob/96a4e0e81b1ad79837c307d57197ecd4c756179b/BrawlCrate/Studio/FighterProject.cs
- [R2] Rivals II official character template guide: https://rivals2.com/workshop/knowledge-base/character-creation/create-your-character-mod/
- [R3] Rivals II official import guide and its 5.8 version warnings: https://rivals2.com/workshop/knowledge-base/character-creation/import-into-unreal/
- [R4] Rivals II official publishing pipeline: https://rivals2.com/workshop/knowledge-base/getting-started/publish-to-steam-workshop/
- [R5] ProjectMCodes source build example: https://github.com/Fracture17/ProjectMCodes/blob/master/README.md
- [R6] Project+ official FAQ: https://projectplusgame.com/faq
- [R7] Melee m-ex reference: https://github.com/akaneia/m-ex/blob/master/README.md
- [R8] Current MEX package code: https://github.com/Ploaj/MexManager/blob/master/mexLib/Types/MexFighter.cs
- [R9] ARCropolis mod root code: https://github.com/Raytwo/ARCropolis/blob/master/src/utils.rs
- [R10] Rivals II official animation guide: https://rivals2.com/workshop/knowledge-base/character-creation/animation-overview/
- [R11] BrawlCrate viewer sources, inspected upstream revision d56cbac89dc2cd0e5c3f923e1d03041b16902add: https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlCrate/UI/Model%20Previewer/ModelEditControl/Designer.cs and https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlLib/Internal/Windows/Controls/ModelViewer/Panels/RightPanel.Designer.cs
- [R12] BrawlCrate typed animation selection and editing: https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlLib/Internal/Windows/Controls/ModelViewer/MainWindowBase/Animation.cs
- [R13] BrawlCrate CHR0 transform edits and native key indexing: https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlLib/Internal/Windows/Controls/ModelViewer/Editors/CHR0Editor.cs
- [R14] BrawlCrate skeleton hierarchy and selection: https://github.com/soopercool101/BrawlCrate/blob/d56cbac89dc2cd0e5c3f923e1d03041b16902add/BrawlLib/Internal/Windows/Controls/ModelViewer/Panels/BonesPanel.cs

Public references checked 19 October 2026. Framework-native paths and APIs must be verified against the versions selected in each real project.
