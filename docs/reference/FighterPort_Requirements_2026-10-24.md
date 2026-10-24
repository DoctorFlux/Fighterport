# FighterPort Studio Master Specification

Complete requirements for character model porting and animation retargeting

**Prepared:** 24 October 2026  
**Development repository:** [DoctorFlux/fighterport-studio-next](https://github.com/DoctorFlux/fighterport-studio-next)  
**Application foundation:** BrawlCrate and BrawlLib  
**Primary destinations:** Super Smash Bros. Melee, Brawl, Ultimate and Rivals of Aether 2  
**Audience:** The project owner and FighterPort developers

FighterPort is a BrawlCrate-based desktop tool for taking a character model and its animations from a supported game, preparing them for another platform fighter, or applying those animations to an existing destination character. A single fighter project should reuse a shared source library while keeping independently editable and validated builds for each destination game.

The active product is **fighterport-studio-next**. Build a new, substantially simpler application workspace around its existing BrawlCrate model editor, project system, library, original Next retargeter and integrated owned engine. Preserve the full native model-preview UI while redesigning the surrounding navigation and transfer workflow. The essential outcome is: choose a source character and animation, choose the destination model and game, preview and correct the transfer, then export valid destination assets.

**Current scope is models and animations. Moveset scripting is deferred.** The new interface must make ordinary transfers easier than the current Next workflow, while keeping advanced rigging, contact and native editing tools available when needed.

This specification separates permanent product requirements, a dated implementation snapshot, proposed engineering contracts, and acceptance criteria. Requirements describe the intended complete tool; they do not imply that every adapter or feature exists today. Section 2 records the inspected baseline. Section 25 gives the implementation handoff. References appear in Section 26.

## 1 Product decisions and scope

### 1.1 Decisions that govern development

**REQ 01 — Continue the BrawlCrate-based implementation and redesign its workspace.** The chosen repository is `DoctorFlux/fighterport-studio-next`. Preserve the real BrawlCrate Model Previewer, native rendering, camera, timeline, skeleton list, native editor menus, resource editors, interpolation and file handling. Redesign the surrounding application UI, layout and navigation so those capabilities form a simple source-to-target workflow. Preserving the previewer does not require retaining today's fragmented FighterPort dialogs or startup flow.

The separate `DoctorFlux/fighterport-studio` repository and its earlier branches remain historical implementation and research references. Existing owned engine code already integrated into Next should be retained and improved where useful. Earlier instructions to avoid Next or categorically reject integration of the owned engine are superseded. The latest request also supersedes preserving the current outer layout unchanged. Preserve the original Next retargeter as the ordinary route while its replacement/advanced paths still have unresolved acceptance gates. [R01–R04]

**REQ 02 — Support both model porting and animation transfer.** Porting a model with its animations and transferring animations onto an existing model are separate operations. Both must be first-class workflows. The tool must also work as a direct native model and animation editor without requiring a porting wizard.

**REQ 03 — Make the four primary games both sources and destinations.** Melee, Brawl, Ultimate and Rivals 2 must eventually exchange supported models and animations through verified adapters. Test each direction independently. Additional game families are source libraries unless an export destination is explicitly added later.

**REQ 04 — Generalize across characters and proportions.** Support all meaningful bone correspondences, differing hierarchy and rest conventions, and varied body proportions. Avoid solver patches selected by character name, clip name, file path, fixed frame number or asset hash. Known-rig profiles may store reviewed structural mappings and calibration; they must not conceal character-specific motion fixes.

**REQ 05 — Preserve native assets and source ownership.** A Brawl asset remains a Brawl asset, retaining its native channels and resource data. Other formats retain their own source representation. Shared evaluated data enables cross-format work; importing an asset must not automatically reduce it to Brawl's format or overwrite the original.

**REQ 06 — Keep routine work local.** Model viewing, editing, rig setup, retargeting and supported file conversion should work offline without a required external service or account. External game toolchains may have their own installation and account requirements.

### 1.2 Priority and eventual completeness

The current product priority is a much simpler UI for reliable models, animations, library reuse, rig controls, retargeting, timing, transitions, animation asset assignments and native output. Implement the simplified end-to-end workflow early, then extend supported assets and motion quality through it.

Moveset scripting, PSA/REL authoring, gameplay compilers, hitbox logic, attack behavior and custom runtime mechanics are outside this implementation scope. Existing working gameplay can remain in a destination project while its assets are replaced. These deferred areas must not block model/animation work or appear as unfinished mandatory steps in the main workflow.

“Any character” and “any action” set the generality goal. They require extensible adapters and constraints, not an assertion that all unknown formats, unrelated anatomies or impossible contact combinations are already solvable. A transfer should deliver a valid result, an editable partial result with specific losses, or a clear reason it cannot yet complete.

### 1.3 Meaning of preservation

Animation-only transfer must preserve the destination model, deform-bone hierarchy, bone identities, rest/bind data and skin weights. It must not resize limbs or rewrite the skeleton simply to make an animation look acceptable.

Model porting may require an explicit derived target rig, weight remap, mesh reduction or material conversion. Those changes belong to a separate target asset and must be shown in a conversion report. This distinction permits practical game exports while protecting the original source and existing target characters.

## 2 Current implementation baseline

### 2.1 Inspected revision

The read-only repository inspection used `main` at commit `70119b4295309014f0af4b4fa660ee2d3d254d62`, dated 24 October 2026, with the commit title “Preserve existing contact trajectories and add custom animation slots.” The current checkpoint is preview.13 inside the incomplete M2 integration slice. Re-read actual HEAD before implementation. This snapshot describes existing code; the user's subsequent request for a simpler new outer UI governs the intended interface. [R01–R03]

The application already combines the native BrawlCrate front end with owned engine components under `engine/`. The retained native application uses .NET Framework 4.7.2/x86; owned modern Core and CLI components use a separate compatibility boundary. The former standalone rendering stack is retained for regression fixtures, not as a second shipped editor. [R04]

### 2.2 What exists and what remains open

| Area | Existing supported or documented subset | Remaining requirement |
| --- | --- | --- |
| Native editor | BrawlCrate viewer, native resources, model-only work, separate motion loading, curves and project saves | Complete cross-game authoring without regressions |
| Retargeting | Original Next solver is the default for direct and library actions; mapping and native result workflows exist | Broad motion quality, anatomy, contacts and every supported game direction |
| FK and IK controls | Existing-joint controls, pins, poles, native gizmos, automatic keys, Undo and native/project reopen | Broader rig coverage, remaining interfaces and live input/DPI qualification |
| Shared library | Brawl package indexing, source-model association, original-model preview and independent target revisions | Other game readers, durable reusable automatic rig profiles and broader catalogue workflows |
| Batch timing | Source trims and shared/per-clip output frame counts with reviewed results | Full timing/style pipeline; current nonidentity timing fits become non-looping |
| Transition fitting | Fixed-duration entry/exit FK fitting with frozen references and protected middle curves | General contact-coupled and state-aware fitting |
| Contact preservation | Existing native support paths can be kept unchanged during boundary fitting | New anchors, repositioning, moving supports and contact-release transitions |
| Animation slots | Custom required/optional assignments, locks, exact identity, changed-content review and persistence | Verified game slots, runtime IDs, fallbacks and Fill Missing automation |
| Workspace | Character/shared/independent-target management and native/Core persistence | Complete layers, companion portability and independently validated multi-game builds |
| Disc and mod sources | Documented read-only Brawl WBFS/ISO route and extracted mod-folder recognition | Independent RVZ acceptance, verified base/overlay composition and raw SD-image support |
| Other adapters | Other game paths and future adapter contracts are represented | Native Melee, Ultimate, Rivals 2 and additional donor adapters |
| Game completion | Native Brawl CHR0 and matching-slot PAC evidence exists for tested cases | Full packaging and actual destination-game acceptance |

These are repository-supported claims, not a fresh Windows application test performed while compiling this specification. The current code audit found only a Brawl adapter project under the engine's adapter directory. Saved paths, UI entries and contracts do not establish native support for other games. [R02–R05]

The current library/retarget route evaluates CHR0 skeletal channels. Inherited editors and inventory can retain other Brawl channels without transferring them through this route. Likewise, inherited writer availability is broader than independently qualified output: standalone BRRES/MDL0 and PCS writing must earn their own acceptance. [R03]

### 2.3 Existing task and the revised interface priority

The inspected `docs/plans/current.md` queues complete native support timelines/context and coupled contact repositioning/release transitions with independent conflict and native-subframe validation. That work remains necessary: whole-path protection preserves existing trajectories and is not the complete solver. [R02, R05]

Reconcile the next executable slice with the latest UI request. Deliver one simple, visible source-to-target transfer through the new workspace using the current accepted engine, while retaining the unresolved contact work and its gates. The interface redesign must not require every advanced solver problem to be finished first, and it must not make unfinished solver behavior appear accepted.

The latest repository record reports 441 native plus 266 focused engine checks, 707 total, and 59 inspected page/image captures. Those counts belong to that checkpoint and its stated scope. They do not prove universal retargeting quality, other-game adapters or in-game compatibility. [R05]

### 2.4 Reconcile historical documentation

The latest user direction takes precedence over older UI instructions. Preserve acceptance records as evidence about their exact operations. Some historical rows still call slots and timing entirely future work, while narrower implementations now exist. Direct model-viewer startup is the current behavior, not a permanent constraint on the new workspace; provide the simpler start/resume experience described in Section 8.

Maintain one executable queue, a durable master scope and a per-operation capability table. Do not add old test totals together, revive a completed milestone, erase unresolved gates, or treat this new specification as permission to reset the repository.

## 3 Games and source coverage

### 3.1 Primary source and destination games

| Game family | Required source support | Required destination support |
| --- | --- | --- |
| Super Smash Bros. Melee | Native models, skeletons, skinning, animations and relevant channels | Verified native model/motion assets; distinguish replacement workflows and supported expansion frameworks |
| Super Smash Bros. Brawl | Native character packages, models, all relevant animation families and dependencies | Native assets and complete required container structure for the chosen Brawl environment |
| Super Smash Bros. Ultimate | Native model, mesh, skeleton, materials, animation and helper relationships | Valid target-native SSBH assets and the required target folder/binding relationships |
| Rivals of Aether 2 | Modkit/exported model and animation sources with original skeleton identity | Official Modkit-compatible skeletal assets, animation imports and validated asset builds |

Brawl compatibility includes vanilla Brawl, BrawlEX, Project M, Project+, Project M Remix and REX where their file, framework or runtime differences apply. A shared Brawl file family does not mean one build profile is interchangeable with every named environment.

Other Smash releases can be added through the same adapter boundaries later. Melee, Brawl and Ultimate are the committed Smash priorities in this specification.

### 3.2 Additional requested donor games

All entries below are permanent source requirements. They are not a claim that extraction or playback already works.

| Source family | Required content | Adapter planning boundary |
| --- | --- | --- |
| Dragon Ball FighterZ | Models, rigs, skinning, motions, expressions, materials and authored timing | Title/build-specific source route; preserve stepped or held animation |
| Dragon Ball Sparking ZERO | Models, rigs, skinning, motions and relevant dependent assets | Its own tested game profile and package versions |
| Dragon Ball Xenoverse 1 | Original model/skeleton/animation sets and materials | Separate validation from Xenoverse 2 |
| Dragon Ball Xenoverse 2 | Original model/skeleton/animation sets and materials | Native EMD/ESK/EAN family candidates where verified |
| Battle Stadium D O N | Models with original rigs, weights and animation | Platform/version-specific research, including the requested PS2 route |
| Dragon Ball Z Budokai 3 | Models, original rigging and animations | Dedicated game-format investigation |
| Dragon Ball Z Budokai Tenkaichi 3 | Models, original rigging and animations | Dedicated game-format investigation |
| Naruto Ultimate Ninja | Relevant models and motions | Identify each chosen title/platform; no franchise-wide parser assumption |
| Naruto Ultimate Ninja Storm | Relevant models and motions | Identify each chosen title/platform and package revision |
| One Piece games | Relevant models and motions | Exact titles remain to be selected when implementing these adapters |
| Jump Force | Models, original skeletons, weights and motions | Verify a title/build-specific source route |
| Existing character mods | User's Brawl/Project M Goku assets, Ultimate Goku assets and other selected mods | Preserve original asset provenance and the underlying game's format rules |

Export back to the Dragon Ball or other donor-only games is not required. Do not delay the Smash/Rivals workflows by adding those destination toolchains.

### 3.3 Interchange and custom assets

Provide appropriate interchange import/export for user-authored models and animations, including an FBX route where needed for the Rivals Modkit and optional DCC work. Other interchange formats should be added according to demonstrated model, skinning, curve and material coverage, not because their extension is recognized.

Blender, Maya and 3ds Max integration should help users who already use those tools. Detect installed tools, allow manual paths and versioned export/import presets, and preserve round-trip identity. Routine supported FighterPort editing must not depend on purchasing a DCC application.

## 4 Everyday workflows

### 4.1 Port a model with its animations

1. Register a source game, extracted character folder, native package or supported disc/archive source.
2. Select the character, exact costume/model and associated motion collection.
3. Preview source motions on the original model with its actual skeleton, bind data, weights and materials.
4. Choose Melee, Brawl, Ultimate or Rivals 2 and the precise destination build/framework.
5. Prepare a derived target model. Review required skeleton, weight, mesh, material and texture changes.
6. Reuse or retarget the source motions to that derived target rig; combine motions from other donors when desired.
7. Assign the destination animation slots, correct contacts and transitions, and resolve unsupported channels.
8. Export target assets, reopen them and complete the relevant target-tool validation.

The source character's appearance is the basis of this workflow. It may require destination-specific costumes or material styles; the source-quality version remains available.

### 4.2 Use animations on an existing fighter model

1. Open the target model or fighter project.
2. Search the shared animation library and preview a candidate on its original source character.
3. Select **Use on This Model** or assign the clip to a target slot.
4. Reuse a matching reviewed rig profile or review uncertain mappings once.
5. Generate an independent target animation, then inspect FK transfer, contact correction and channel losses.
6. Adjust pins, poles, pose curves, timing or transition references as needed.
7. Save native target motion while preserving the target model, skeleton, bind data and weights.

A Dragon Ball animation on an Ultimate or Rivals model is a required example. Selecting a library clip with a verified source association should not repeatedly ask the user to locate its donor model.

### 4.3 Mix models and animations from different formats

A Brawl model must be usable with a Melee source animation when both adapters and the mapping are supported. Likewise, a fighter may combine one game's model, another's movement, a third's attacks and hand-authored corrections.

The editor resolves the animation's original rig, evaluates it correctly, then transfers motion into the current target's pose system. The original model and motion retain their native identities. The final saved animation uses the selected destination representation only when its writer is supported.

### 4.4 Build one fighter for several games

Create a master fighter and add destination branches. Share original sources, semantic action choices and reusable recipes. Keep each target's model, skeleton, contact calibration, animation asset mapping, materials and output limits independently editable.

Changing a Brawl-specific animation correction must not change the Ultimate build. A deliberate master-level change should show which target outputs need regeneration.

### 4.5 Direct editing and model-only authoring

Open native assets, select a bone, manipulate it and create/update the key at the current frame automatically. A model-only project must support posing and creating its first animation without a dummy motion file or unrelated donor. Existing controls and pose state should carry into that first clip.

Users must be able to save a project, close the application, reopen it and continue with the same targets, exact clip assignments, mappings, references and edits.

## 5 Native data and cross-format architecture

### 5.1 Preserve the native document and share evaluated data

Use three connected layers. The native source layer retains original resources and format-specific information. The evaluated model/animation layer exposes geometry, transforms, skinning, curves, channels and semantic relationships for shared tools. The destination layer contains explicit derived conversions and native output.

For Brawl, retaining the native BrawlLib resource tree protects the behavior and format coverage the user already values. For other formats, adapters retain equivalent native records or opaque extension data. The shared layer must not discard information it cannot currently edit.

This is a proposed engineering contract to reconcile native preservation with cross-format editing. It does not require every source to become MDL0/CHR0, nor does it require replacing the existing application or rewriting every parser.

### 5.2 Adapter capabilities are independent

Every game/build/format version should advertise detection, inventory, extraction, model decode, animation decode, native evaluation, preview, editing, retarget participation, native writing, package assembly and destination validation separately.

Use clear states such as planned, implemented with fixtures, native reopened, observed in the editor, imported by the destination toolchain, and tested in game. A parser is not a writer; a writer is not a package builder; a successfully imported animation is not a complete playable character.

Version coverage belongs to the operation. A reader may accept a format revision that a writer cannot produce. Unknown versions should produce a specific diagnostic rather than an apparently successful empty model or motion.

### 5.3 Typed asset content

Preserve mesh topology and object structure; normals, tangents, UV sets and colors; skin weights; bone identities and hierarchy; local/world rest transforms; bind/inverse-bind matrices; native transformation flags; materials, textures and palettes; morph targets; all animation tracks; events and dependencies; native source containers and opaque fields.

Record units, handedness, up/forward axes, local bone axes, pre/post transforms, rotation order, quaternion/Euler semantics, winding, scale compensation, timebase, interpolation, loop behavior and source timing. Do not assume every game uses identical axes, 60 fps, a fixed humanoid skeleton or the same scale rules.

Do not impose an inherited prototype limit such as 128 bones or four influences per vertex on all sources. Store source richness and apply tested destination-specific reductions at export.

### 5.4 Brawl animation families

| Native family | Required treatment |
| --- | --- |
| CHR0 | Skeletal transform animation, native interpolation, flags and bindings |
| VIS0 | Visibility timing and correct object/resource association |
| SRT0 | Texture coordinate transforms and native curve/binding editing |
| PAT0 | Texture/palette pattern changes and resource references |
| CLR0 | Material/color animation and binding fidelity |
| SHP0 | Shape/morph animation and correct mesh/vertex relationships |
| SCN0 | Preserve native data and inherited editor behavior; keep new cross-game retarget/conversion work deferred in the current scope |

The upstream project has separate native implementations for these families. Their presence in BrawlCrate does not establish their transfer through FighterPort's current CHR0 retarget route. Track preservation, playback, editing and cross-game transfer separately. [T01, R03]

### 5.5 Loss reporting

For each channel, identify whether it transferred directly, was mapped, was baked with a stated tolerance, was kept only in the source, needs manual binding, or is unsupported. Preserve synchronization between body, face, visibility, materials, props and effects where supported.

An unknown or omitted channel must not disappear without a visible result. Preview and export reports should identify the exact affected resource or bone, the reason and the available remedy. Editing a known channel must preserve unrelated native resources wherever the format permits.

## 6 Game sources and the shared animation library

### 6.1 Register a source once

Accept supported individual files, model/motion packages, extracted dumps, game installations, disc images/ROMs, mod folders and Rivals Modkit workspaces. Distinguish their actual provider types. GameCube/Wii disc readers, Ultimate archives, Unreal packages and title-specific PS2 containers require different implementations.

Index sources read-only. Let users browse characters, models, costumes and clips without loading or extracting an entire game at once. Use lazy reads, bounded caches, asynchronous search, progress, cancellation and incremental updates. Large scans must not stall the viewer or ordinary playback.

For Brawl mod environments, represent a base game and ordered overlays explicitly. Show which build supplies each resource, preserve alternate configured builds, and report conflicts or missing dependencies. Recognizing a `pf/fighter` folder does not by itself implement overlay resolution.

### 6.2 Every clip knows its source model

**REQ 07 — One shared, persistent library serves all fighter projects.** Every clip stores its exact source game/build, mount, container, nested resource identity, source model, skeleton/rest fingerprint, source hashes, animation revision, timebase, channel set and association evidence.

Pair model and animation automatically when verified native references or a tested game layout establish ownership. Where several bodies or costumes are possible, present a concise association review and persist that approval against the actual identities. Matching a display name is not enough.

A source animation without a resolved original rig may remain catalogued, but it must not silently become eligible for automatic cross-rig retargeting.

### 6.3 Library experience

Provide character/game/clip search, filters, tags, favorites or collections, whole-motion-set import and source-model details. A clip's source preview always uses its original model and motion interpretation. The target model remains separately selected and editable.

Offer **Add Source**, **Add Whole Character Motion Set**, **Preview Source**, **Use on This Model**, **Assign to Slot**, and batch operations through the same library. Avoid duplicate competing catalogues in the native UI and Core workspace.

Show Available, Missing, Source Changed, Needs Model Association, Unsupported Version and Unsupported Channel states. Repair or relocate a source by verifying its identity; do not redirect every same-named asset to a replacement file.

### 6.4 Immutable donors and independent results

A project references a library clip revision, then owns its derived animation and correction settings. Two projects can reuse one donor without sharing mutable target curves. Deleting a library entry must not silently delete independently saved target results; preserve the receipt and report the broken source link where regeneration needs it.

When a source, rig, mapping, solver or target template changes, mark dependent results stale and show a regeneration plan. Do not overwrite the user's manual correction layers automatically.

## 7 Fighter projects and target branches

### 7.1 Project operations

Support new/open/save/save-as/duplicate/import, recent projects, model-only creation, creation from the current model, importing an existing fighter and adding another game target. Preserve and migrate existing `.fpstudio` projects through versioned readers. Do not require an incompatible new project format merely to add a feature.

Use a simple start/resume screen for choosing a workflow or opening a recent project, then keep everyday work in one workspace. Reopen an existing project directly into its saved editing context. A direct native-file editing entry remains available; it should not be the only discoverable way to begin a character transfer.

### 7.2 Project contents

| Project record | Purpose |
| --- | --- |
| Fighter identity | Name, project revision and reusable character definition |
| Source references | Original models, motions, native containers and library revisions |
| Character assets | Meshes, native skeletons, skinning, materials, textures, outfits and forms |
| Rig profiles | Reviewed mappings, calibration, controls, body landmarks and constraints |
| Animation instances | Source recipe, target curves, edits, timing and contact/transition layers |
| Animation sets | Semantic slots, multipart moves, actual native bindings and review status |
| Target branches | Game/build/framework, target model/rig, limits, assets and output configuration |
| Build records | Dependencies, converter versions, losses, generated files and validation receipts |
| Optional native associations | Existing target resource links retained without requiring gameplay authoring |

The exact directory layout should extend the existing project storage. Separate durable authoring data from disposable previews/caches and generated builds. Do not embed the complete global library into every fighter project.

### 7.3 Revisions and transactions

Use stable identities for resources, rigs, source clips and target clip instances. Store enough dependency information to regenerate a target: donor revision, source and target rig fingerprints, mapping, solver/settings, manual edits, timing recipe, reference poses, native writer and target profile.

Apply a batch as a reviewable transaction with Undo. A failed export leaves the last successful build intact. Save/restart must preserve project content even if the session Undo stack is not persisted. Use atomic writes and bounded migration, and keep unrelated project branches unchanged.

### 7.4 Target reuse

Share a correction only when its meaning survives the target change. Semantic intent such as a two-hand grip may be reusable; its exact world-space hand coordinates usually require recalculation for another body. Display which properties are inherited and which are overridden per destination.

Costumes may share animations if their native rigs and binding semantics are compatible. Different meshes can still need different contact landmarks, clearance, material mappings or attachment positions.

## 8 A simpler workspace with the full BrawlCrate previewer

### 8.1 The central UI requirement

**REQ 08 — Preserve the complete BrawlCrate Model Previewer UI.** Retain its actual model editor, resource/animation lists, rendering, camera, right-side skeleton list, transform gizmos and fields, timeline, interpolation, native menus and frame tools. Reusing only its renderer inside a reduced replacement viewport does not satisfy this requirement.

**REQ 20 — Redesign the surrounding FighterPort workflow.** The current collection of project, library, rig, retarget, fitting and export interfaces should become one clear workspace with automatic defaults and progressive advanced options. The native previewer stays familiar while choosing a source, target and export becomes much easier.

The new layout is a product requirement. The specific arrangement below is the proposed implementation design and can be refined through usability checks while preserving these two requirements.

### 8.2 Start and resume

On first use or with no project open, show two primary actions: **Use animations on a model** and **Port a character model and animations**. Keep **Open project**, recent projects and **Open native files** easy to find. Do not require users to understand internal manifests, engine boundaries or native container structure before starting.

Resume a saved project in its prior source/target and editor state. If the user simply opens a supported native model/motion file, allow direct BrawlCrate-style editing without completing a wizard.

### 8.3 One workspace and four stages

Use a compact workflow strip: **Source → Target → Preview and fix → Export**. These stages change the task panel within one workspace; they should not become a chain of modal windows. Keep current source, selected animations, destination model and destination game visible as concise summaries.

| Stage | User chooses | Tool handles automatically |
| --- | --- | --- |
| Source | Game/character/model and one or more animations | Original-model association, native decode, source preview and cached discovery |
| Target | Existing destination model, or derived model-port target, plus game/build | Compatible rig-profile lookup, format readiness and target defaults |
| Preview and fix | Review result and correct visible problems if needed | Retarget preparation, proportion/contact handling, comparison and saved settings |
| Export | Destination asset/profile and output location | Native bake, applicable bindings, reopen validation and a clear result summary |

For previously registered sources and reviewed rigs, the normal path should be source selection, target selection, preview and export. Repeated mapping dialogs and source-model selection are unnecessary when the exact identities are already verified. An uncertain rig still gets a short, focused correction step.

### 8.4 Proposed layout

Use a project/operation bar at the top, followed by the four-stage strip. A collapsible source-library drawer handles search and donor selection. The full native Model Previewer occupies most of the workspace. A compact task panel contains only the choices and primary action relevant to the current stage.

Keep one active editable target. Source audition and comparison use independent preview state, never a second competing authoring document. Once a donor is chosen, collapse or narrow the library when more preview space is useful. Do not add a second permanent bone inspector beside the native Bones panel.

![Proposed FighterPort workspace retaining the full BrawlCrate Model Previewer](FighterPort_Workspace_Layout.png)

The layout image is a structural proposal, not a claim of exact native pixels or an implemented screen. The embedded editor is the actual BrawlCrate component; the outer workspace provides the simpler source-to-target job flow.

### 8.5 Simple controls first

The default transfer panel should show the source and target, an understandable setup status, a **Preview transfer** action, progress/cancel, and a short result summary. Use automatic mapping, proportion adaptation and contact inference where verified. Remember approved settings by rig pair and recipe.

Present correction tools by the visible problem: **Fix a hand or foot**, **Adjust body/root motion**, **Fit start or end**, **Change timing**, and **Review bone mapping**. These actions reveal the relevant native controls or focused task-panel options. Avoid requiring the user to discover which of several expert dialogs owns a basic fix.

Advanced settings remain available for mapping evidence, chain roles, poles, reach, contact priorities, calibration, scene/prop references, channel policies and detailed export tolerances. Hide complexity by default without removing capability or hiding a real failure.

Suggested settings should use meaningful defaults and ranges, not unexplained numeric sliders. Do not add controls that are uncalibrated, ineffective or backed by an unfinished solver path.

### 8.6 Native editing remains intact

Preserve bone picking, skeleton-tree selection, translation/rotation/scale, interpolation editing, frame operations, playback, scrubbing and native menus. Editing a bone at the current frame creates or updates the appropriate animation key automatically; a separate Add Keyframe click must not be compulsory.

Preserve **Transform All** and the native controls associated with the user's earlier “frame frame” request. Inspect the actual reference UI and handlers to identify the intended frame controls precisely, then include them in the native feature-parity checklist. Do not assign an invented interpretation to the phrase.

Native bone editing and IK/FK correction must be available immediately on the result. The simple workflow must not turn the tool into an import/export wizard that loses BrawlCrate's editing depth.

### 8.7 Comparison and understandable failures

Use clear **Original**, **Result** and **Compare** choices with synchronized time and consistent native rendering. Offer FK-only, contacts, paths, ghost poses and per-chain diagnostics through optional views. Keep the original source, target model and edited result visually distinguishable.

Show what the user can do next: choose the missing source model, review two uncertain bones, shorten a reach, move a ledge reference, or select a supported export route. Detailed logs and numeric receipts belong in expandable details. Never call a blocked or stale result successful to make the interface appear simpler.

### 8.8 Batch work without repeated setup

Selecting several animations should reuse the same verified source/target setup. Show a compact batch list with source clip, target assignment, timing and result status. Let the user open only the problematic clips for correction, apply approved changes and export the accepted set.

Preserve per-clip exceptions, independent target revisions and one reviewed batch transaction. Batch work should not require repeating the entire single-clip wizard for every animation.

### 8.9 Playback and document identity

After a model and its motion source are associated, show playable clips immediately. Explain whether a missing preview comes from missing motion, unresolved association, incompatible data or rendering failure. Read-only sources still support audition and export to a new result.

Keep smooth presentation and correct native time sampling. Playback evaluates prepared curves; it does not launch workers, run live retargeting, scan catalogues or hash sources on each tick. Test actual displayed motion, pause/play, loops, scrubbing, resizing and fallback presentation. [R01–R04]

Keep exact project, target, model, native document and clip identities internally without making users manage those identifiers. Opening a donor or changing the selected stage must never redirect Apply/Save to the wrong character.

### 8.10 Acceptance for the UI redesign

A first-time user should complete a supported transfer from the start screen without knowing native filenames, opening expert settings or losing the current target. A returning user with known rigs should not repeat source association or mapping approval. A batch should reuse setup and surface only exceptions.

Verify the complete native previewer against BrawlCrate: right-side skeleton list, camera, lists, auto-key transforms, timeline, interpolation, Transform All, native frame tools and Undo/save behavior. A successful new layout must also preserve source/result comparison, readable scaling and keyboard/mouse interaction.

Observe users or an explicit task-based walkthrough completing model-port, animation-only, batch and direct-edit flows. Measure unnecessary steps, repeated questions, hidden commands and wrong-document mistakes. The redesign is complete when these jobs are simpler and the full native editor still works.

## 9 Models materials costumes and target preparation

### 9.1 Complete model import

**REQ 09 — Import the usable character, not just visible geometry.** Preserve meshes, object structure, original skeleton and hierarchy, bind/inverse-bind information, skin weights, normals/tangents, UVs, vertex colors, textures, palettes, material assignments, morphs and attachments wherever the source adapter supports them.

A mesh with missing rig/weights is not a complete animated-character import. A screenshot or posed mesh capture is not equivalent to obtaining reusable skeletal animation. Report exactly which components are available.

If a target model has no usable skeleton or skinning, show that prerequisite plainly and offer supported rig/skin preparation or an optional DCC bridge. Keep this preparation separate from animation-only retargeting; do not silently alter an existing rig to make the transfer proceed.

### 9.2 Model editing and conversion

Extend the existing model tools to inspect and edit mesh/material assignments, skeleton relationships, skin weights, influence normalization, visibility, shapes and attachments. Model edits must update dependent rig and animation compatibility records instead of leaving silently invalid bindings.

For a model-port operation, create a target-preparation recipe. It may retain a compatible source rig, map weights to a target rig, remove or merge supported helpers, adapt attachments, convert materials, reduce textures or simplify geometry. Each change must be explicit and reversible through retained source/target revisions.

For animation-only retargeting, keep these model-conversion operations outside the solve. The solver should adapt poses and constraints to the target's proportions instead of modifying the target anatomy.

### 9.3 Materials and visual style

Support source materials and their destination approximations, including cel-shaded Dragon Ball assets and the different visual expectations of Melee, Brawl, Ultimate and Rivals 2. Preserve source textures and material parameters even when the destination shader cannot reproduce them exactly.

Provide source/target material previews and reports for transparency, culling, texture addressing, UV orientation, normals, tangent basis, colors, palettes and unsupported shader behavior. Include the previously observed mirrored/flipped-looking Brawl Mario textures as a regression case.

A shared material description can preserve intent, but each game needs a real material writer or toolchain conversion. A visual style preset must not be presented as a faithful shader conversion without comparison.

### 9.4 Costumes forms and props

Manage alternate costumes as actual mesh/material/attachment variants, not only recolors. Store form-specific hair, tails, accessories, visibility switches and animations when necessary. Reuse rig profiles only when the associated structure and semantics remain compatible.

Support prop and weapon attachment data, hand switches, two-hand grips and synchronized object animation. Keep both the object transform and the hand-to-object relationship; retargeting the arm alone may not preserve the interaction.

### 9.5 Destination limits and optimization

Inspect target-specific limits for bones, influences, topology, materials, textures, resource naming, animation size and package structure. Do not invent one limit for all games or apply an old internal prototype cap globally.

Initially accept functionally correct models that are not yet artistically optimized where they still satisfy the destination's hard requirements. Later offer deliberate reduction with visual comparison. A model exceeding a hard native limit cannot be labelled export-ready just because optimization is deferred.

## 10 Automatic rig characterization and all shared bones

### 10.1 Separate the skeleton from controls and calibration

The native skeleton defines deforming bones, hierarchy and bind behavior. The FK rig supplies posing controls. The IK rig supplies effectors, poles, goals and constraints. Retarget calibration aligns the anatomical coordinate frames used to transfer motion. These are related data, but they must not be conflated.

Keep native rest/bind poses, retarget calibration and gameplay reference poses separate. Selecting an Idle reference must not rewrite the rest pose. Setting up IK must not insert export bones into an existing game model merely to make the controls function.

### 10.2 Mapping coverage

**REQ 10 — Support every meaningful shared bone and chain.** Begin with a general humanoid characterization, then allow additional roles and arbitrary chain correspondences. A fixed minimal humanoid list must not become the maximum supported skeleton.

| Bone or chain group | Required mapping behavior |
| --- | --- |
| Root and body center | Distinguish movement root, hips/pelvis, waist and center-of-mass roles |
| Torso | Map lower spine, multiple spine joints, chest/bust and upper-body orientation |
| Head and face | Neck, head, jaw, eyes and supported facial joints or morph relationships |
| Shoulders and arms | Clavicles, shoulders, upper/lower arms, elbows and twist distribution |
| Hands and fingers | Wrist/palm orientation, thumbs and each meaningful finger chain |
| Legs and feet | Thigh, knee, shin, ankle, heel, foot and toe relationships |
| Extra anatomy | Tail, ears, wings, extra limbs and other structurally meaningful correspondences |
| Helpers and attachments | Twist/helper/corrective bones, hair, clothing, weapons and prop anchors with explicit semantics |

Different bone counts require chain-level mapping where appropriate. A source with several spine or twist joints can distribute motion over another chain; that policy must preserve endpoint intent and be editable. Unmatched source or target bones require a declared retain/rest/follow/procedural/manual policy.

Distinguish a joint absent from the source skeleton from a joint present in the skeleton but omitted from an animation's tracks. Evaluate omitted tracks using the format's native default/rest behavior; use the declared mapping policy for genuinely unmatched joints. Preserve the missing-source-bones regression that affected the replacement solver and require it to pass before any alternative becomes the default.

### 10.3 Evidence and manual correction

Rank mapping evidence: an exact approved rig fingerprint; a verified game/rig-family profile; hierarchy and rest geometry; naming aliases and symmetry; skinning/body measurements; then manual review. Names are useful suggestions, not proof.

Show confidence, unresolved roles, duplicate mappings and the rationale for important assignments. Let users assign/unassign bones, choose chains, adjust poles and calibration, and save/load profiles. Reuse a reviewed mapping across many clips; invalidate the relevant approval when the rig identity changes.

Do not silently pair unrelated bones because both have a similar name. “All shared bones” means all meaningful correspondences, not an obligation to fabricate a match for every source joint.

### 10.4 Automatic setup for known skeletons

Known Brawl, Melee, Ultimate, Rivals and Dragon Ball skeleton families should have versioned reviewed profiles as real fixtures become available. A successful profile should make subsequent ports simple, with automatic controls and a brief quality review.

Profiles must identify the actual structure, native flags, rest transforms and relevant attachments. Different costumes or forms can share a profile only after compatibility is established. Store body-fit measurements separately from bone naming because identical skeleton names can accompany very different meshes.

## 11 Independent FK and IK authoring

### 11.1 FK editing

**REQ 11 — FK works on its own.** A user can select a bone or control, rotate/move/scale the supported animated channels, edit a pose or curve, and save native keys without using retargeting or an IK solver. Preserve native scale semantics and distinguish an animated scale track from a destructive bind/skeleton edit.

Support per-bone and whole-animation operations consistent with BrawlCrate, appropriate transform spaces, selection synchronization and exact Undo. Local-axis conventions must remain visible and predictable.

### 11.2 IK editing

**REQ 12 — IK works on its own.** A user can place a hand/foot goal, move or rotate a supported pin, adjust an elbow/knee pole, pin a contact or follow another animation on an already native clip. Solving must bake supported deform-bone keys, leaving the native skeleton intact.

Expose reach behavior, pin strength, contact space, orientation, bend preference and relevant constraints in simple controls. Provide soft constraints for editable compromise and firm contacts where required, with clear diagnostics when a request is unreachable.

An editor pin is not automatically a physical ground contact. Arbitrary bone selection, an effector target and a real support relationship have different meanings and should retain distinct metadata.

### 11.3 Combined controls

Allow FK-only, IK-only and combined retarget/correction workflows. Switching control modes must not unexpectedly pop the pose or throw away authored keys. When a chain mixes FK and IK, preserve the selected pose and record the solve/bake result.

Controls must work for model-only poses and persist into the first created animation. Keep per-clip ownership explicit so a pin placed in one result does not silently edit another clip.

## 12 General retargeting and motion intent

### 12.1 Required transfer pipeline

The proposed common pipeline is: resolve exact source and target; evaluate native source poses; characterize and calibrate rigs; transfer compatible FK motion and root intent; construct proportion-aware body/prop/environment goals; solve constraints over time; apply editable corrections; fit timing/state boundaries; bake and validate destination motion.

Reuse the current original Next route while improving it through measured increments. The pipeline describes responsibilities, not a mandate to replace a working solver with a new library. Any alternative implementation must retain the existing acceptance baseline before becoming the default.

### 12.2 Coordinate and hierarchy correctness

Transfer rotations and translations in correctly calibrated anatomical or chain frames. Handle different parent order, rest axes, bone orientation, left/right conventions, segment lengths and scale compensation. Copying unrelated XYZ channel values is not general retargeting.

Preserve full-turn motion, interpolation, loop behavior and native transform flags. Separate root translation from pelvis motion and permit a reviewed root-motion policy: preserve, scale to target stride, use a selected reference, or create in-place motion when the chosen destination requires it.

Translation handling must be role-aware. Fingers, deform bones, root motion and animated accessories cannot all use one global height multiplier. Report unsupported nonuniform scaling or helper semantics rather than silently flattening them.

### 12.3 Proportion changes

**REQ 13 — Motion must adapt to the target body.** Test short-to-tall, tall-to-short, long-arm-to-short-arm, broad-to-narrow and adult-to-child transfers. Mario and Snake are current examples; Goku and Gotenks are future donor examples. Their names must never select different solver code.

Maintain pose character, expressive arcs, timing, hand gestures and body orientation while making reachable adjustments for target limb lengths, hip/shoulder width, torso volume and foot/hand geometry. Where a contact matters, coordinate pelvis, spine, shoulders and limbs rather than bending only the last joint.

The target's limb lengths and bind data remain unchanged in animation-transfer mode. When an intended pose is impossible, show the competing goals and let the user adjust the contact, priority, target alignment or animation correction.

### 12.4 Motion coverage

Support standing, walking, running, jumping, falling, landing, sitting, crouching, lying prone or supine, rolling, getting up, crawling, handstands or hands-on-ground poses, ledge grabbing/hanging/climbing, two-hand grips, touching the body and interaction with props or another actor.

This list is an acceptance taxonomy, not an action-name dispatch table. Contact and motion behavior must derive from source motion, rig/body evidence, scene relationships, target requirements and reviewed annotations.

Non-humanoid or extra-limb characters should use custom semantic chains and constraints where the source and target have meaningful relationships. Unsupported structural differences remain explicit; the tool must not require all characters to fit one forced humanoid skeleton.

## 13 Contacts support and transitions between constraints

### 13.1 Represent contact intent

**REQ 14 — Preserve meaningful interactions.** A planted foot should stay on its intended support, hands on a ledge should remain on that ledge, a seated pose should fit the seat/body, and a two-hand grip should remain coherent. A free airborne limb must not be locked to the floor merely because it happens to pass near it.

Represent contacts as intervals with establishment, active support and release phases. Record the body point or region, counterpart surface/body/prop, coordinate frame, orientation, tolerance, strength, evidence and uncertainty. Preserve the complete timeline and native dependencies, not only aggregate contact counts.

Infer candidate contacts from motion, velocity, geometry, orientation and context; allow manual correction when intent is ambiguous. Save approved annotations with the clip recipe so the user does not repeat the same work for each export.

### 13.2 Contact spaces

| Contact type | Example | Required target treatment |
| --- | --- | --- |
| World or stage | Planted feet, palms on ground | Preserve the intended support frame and full contact geometry |
| Surface or ledge | Ledge hang and climb | Map the ledge plane/edge and hand locations, then coordinate body motion |
| Body-relative | Hand on face, chest, hip or opposite hand | Recalculate against the destination anatomy |
| Prop-relative | Sword, staff, two-hand weapon | Preserve the grip relationship and prop trajectory |
| Other actor | Grabs, throws, hand contact | Retain paired context and validate both actors' proportions and anchors |
| Moving support | Platform or animated object | Solve in the moving support frame with time-aligned transforms |

### 13.3 Reachability and stable solving

Use segment lengths and joint limits to detect unreachable goals before committing distorted poses. Coordinate root/pelvis/spine/clavicle adjustments, contact priorities and allowed sliding. Preserve stable knee/elbow bend planes near straight or folded configurations.

Use temporal continuity through the full animation. Independently solving each frame without a continuity policy risks pole flips, jumps in corrections and unstable contact switching. Validate both position and orientation where the contact requires them, and use foot/hand regions rather than a single arbitrary bone origin when necessary.

Differentiate a strict support requirement from an artistic preference. If both hands, both feet and a fixed pelvis cannot all be satisfied on a very different body, identify the conflict rather than stretching the target or silently dropping a contact.

### 13.4 Establishing and releasing contacts

Transitions into and out of support need explicit behavior: approaching a surface, acquiring a grip, weight transfer, takeoff, foot lift, ledge release and landing. Corrections should blend according to the approved interval and preserve the target action timing.

The current contact-preservation feature freezes existing support paths and ancestors. That protects what already exists, including any baseline error, but cannot by itself reposition a grip or solve a release. The next contact-solving increment must retain full context and add a coupled solve with independent checks. [R02, R05]

### 13.5 Review and manual correction

Show planted regions, reference frames, reach margins, penetration, sliding, constraint residuals and excluded bones. Allow a creator to adjust contacts and priorities in the native viewport. Keep source/FK/corrected comparisons so the user can see whether a mathematically valid result still looks wrong.

Report motion-quality failures separately from serialization failures. A precisely saved unnatural pose is not an accepted retarget, and a convincing viewport pose with incorrect exported curves is not an accepted output.

## 14 Timing animation style and boundary fitting

### 14.1 Preserve distinct clocks

**REQ 15 — Keep source timing and destination timing explicit.** Store original key times, source sample rate, playback rate, simulation/event timing and target export rate separately. Resampling at a new rate should preserve duration unless the user chooses a different duration policy.

Provide playback speed, clip trim, faster/slower retime, destination frame count and advanced time mapping where useful. The UI must distinguish a preview-speed change from a committed animation retime and from a gameplay motion-rate change.

### 14.2 Preserve authored style

Retain intentionally held or stepped poses, including FighterZ-style motion. Do not automatically smooth every source into continuous motion or assume every clip from a game has one fixed visible cadence.

Offer preserve-source style, deliberate smoothing and selective/hybrid interpolation. Show held sections and protected expressive poses. Resampling or key reduction must preserve chosen impacts, pauses, launches and landings. Destination interpolation may require extra keys or explicit step behavior to keep those holds.

### 14.3 Reference poses and fixed duration

**REQ 16 — Fit entry and exit without automatically adding duration.** Let users choose target Idle, Fall or another animation and select the reference frame/phase directly from the previewer. Fit existing first/last portions, preserving total clip duration by default, protected action frames and the central motion.

References must be state-appropriate: LedgeGrab can enter LedgeHang; a get-up can end at Idle; an aerial recovery can enter Fall. Hurt reactions may need an immediate opening response. Do not insert Idle before and after every animation.

Separate frozen pose references from moving trajectory references. Save exact reference revisions and phase choices. Changing a reference later should mark affected results for review rather than silently modifying them.

### 14.4 Contacts and transitions must agree

Fit pose and, where useful, velocity/trajectory at boundaries while retaining the intended contact schedule. Do not apply FK endpoint correction after IK in a way that breaks a planted hand or foot. Resolve contact changes and boundary fitting together when their constraints overlap.

Very short clips, conflicting entry/exit states or protected impact frames may leave insufficient room for a smooth transition. Report the conflict and expose a bounded manual adjustment. A preview-only crossfade is not a replacement for baked output when the target plays the clip directly.

### 14.5 Loops and events

Preserve or explicitly change loop state, loop seam, event times and root trajectory. Current batch timing fits that become non-looping must remain labelled that way; future general retiming should support tested loop-aware policies rather than silently inheriting a loop flag. [R04]

Preserve existing source events and timing markers as data. A retime must show which markers move and which remain locked. Creating or compiling gameplay behavior from those markers is outside the current scope.

## 15 Animation sets slots and automatic filling

### 15.1 Semantic slots and native bindings

**REQ 17 — Build complete target animation sets.** Provide human-readable semantic categories and a verified game/build-specific mapping to actual native slots, paths, IDs, variants and fallback rules. The semantic name describes intent; the native binding determines where the output is used.

Keep required, optional, custom, missing, locked, stale, unbound, fallback-provided, gameplay-dependent and unsupported states distinct. A custom editor slot is useful authoring metadata; it must not be labelled a verified game action until its native profile is established.

### 15.2 Required category coverage

| Category | Representative required coverage |
| --- | --- |
| Neutral and movement | Idle/fidgets, walk, run, dash, turns, stops, crouch and platform drop |
| Jump and air | Jump squat, short/full jumps, directional jumps, extra jumps, fall, fast fall and landing variants |
| Ground attacks | Variable jab chains, rapid jab/finisher, tilts, dash attack and smash prepare/charge/release |
| Aerial attacks | Neutral, forward, back, up and down aerials with relevant landing or multipart variants |
| Specials | Neutral, side, up and down; ground/air; start, hold, loop, release, follow-up and end |
| Defense | Shield, parry, spot dodge, rolls, air dodge, shield break and related reactions |
| Grabs and throws | Standing/dash/pivot grabs, hold, pummel, release, escapes and directional throws |
| Damage and recovery | Directional reactions, hitstun, tumble, stun, knockdown, techs and get-up variants |
| Ledges and walls | Grab, hang, climb, attack, roll, jump, release, wall interactions and recovery |
| Items and props | Pick up, hold, carry, use, swing, throw, drop and attachment changes |
| Presentation | Entry, respawn, taunts, expressions, wins/results and defeat where applicable |
| Character specifics | Transformations, stances, extra limbs, unique movement, special props and custom states |

This taxonomy is a template seed. Read the actual destination's profile and fighter requirements before declaring the set complete. Not every game implements every category in the same way.

### 15.3 Variable parts and variants

Support 1 to N parts for attacks or state sequences. Do not hard-code three jabs, one special clip, or one fixed list of variants. Store start/loop/end, charge/hold/release, ground/air and character-specific branches as distinct relationships.

Extra authored parts remain in the project even if a destination cannot bind them yet. Report the missing runtime support instead of deleting the parts or pretending that additional files create the gameplay sequence automatically.

### 15.4 Assignment and automation

Allow drag/drop, search-and-assign, reuse of reviewed donor collections, automatic proposals and manual overrides. A slot assignment creates a target-owned instance with source identity, retarget recipe, timing/reference contract and native binding.

Automatically infer mappings only where names, source metadata, known profiles and actual destination rules support them. Show ambiguous candidates. A missing action must not be silently filled by an unrelated same-named clip.

### 15.5 Fill Missing Miscellaneous

**REQ 18 — Fill missing routine motions without replacing finished work.** Let users select donor packs and categories such as ledge, hurt, tech/get-up, grab, shield, item, movement and results. Compare actual required slots with current assignments, propose donor clips, then retarget and fit the approved set.

Protect existing authored movement, attacks, specials, taunts and other assigned clips by default. Show changes, source rigs, target bindings, transition/contact requirements and warnings before applying the batch. Apply accepted results as an undoable transaction.

Different motions need different context: ledge motions need a ledge; a grip needs a prop; a get-up needs the correct starting body pose; immediate damage reactions must retain their timing. Batch automation must reuse those contracts instead of applying one generic standing correction everywhere.

### 15.6 Reusable packs

Support local animation/recipe packs containing original authored assets or references to the user's own sources, semantic tags, rig profiles, contact annotations and transition recipes. Keep packs useful offline and version their dependencies. A marketplace or cloud service is not required for the core product.

## 16 Key reduction and asset optimization

### 16.1 Protect motion while reducing size

**REQ 19 — Optimize for the chosen destination.** Melee and Brawl especially need deliberate animation/file-size control. Begin with a correct native bake, then reduce keys or quantize within explicit error bounds. Keep the unoptimized source and derived bake available.

Measure world-space bone and skinned-vertex error as well as native curve error. Protect contact establishment/release, planted intervals, fingers/grips, expressive extremes, holds, loop seams and gameplay-sensitive frames. A low average error can conceal one unacceptable hand slip or foot penetration.

Re-evaluate fractional times after key reduction because matching stored keys does not prove matching interpolation. Check Euler winding and tangents after the writer's compression or rounding.

### 16.2 Optimization controls

Offer clear quality/size presets with advanced per-channel tolerances, protected keys/ranges and a before/after comparison. Report duration, key count, output size, worst-case error, affected channels and remaining destination constraints.

Model optimization should similarly report mesh, influence, material and texture changes. Do not merge bones or remove weights during an animation-only export. Keep game-specific reductions in the target-preparation recipe.

## 17 Native output for each destination

### 17.1 Brawl and its mod environments

Retain BrawlLib's native document/editor pipeline. Support the appropriate PAC/PCS/BRRES organization, MDL0 models, CHR0 motions and additional animation families according to their qualified versions. Saving a selected resource and rebuilding a complete required fighter container are separate operations.

Preserve untouched resources and associations while replacing a selected animation or model. Use an authentic compatible target template where the export route requires one. Validate matching-slot output and reject incompatible target bindings. Qualify standalone model/BRRES and compressed-package output independently rather than extending current CHR0/PAC evidence by assumption. [R03]

Build profiles must distinguish vanilla and each supported BrawlEX/Project M/Project+/Remix/REX environment. Keep base assets, overlays, fighter slots, costumes and motion packages associated with the chosen build. Asset replacement and validation must use that explicit environment identity without requiring PSA/REL authoring.

### 17.2 Melee

Implement a dedicated HSD adapter. HSDRaw/HSDLib provides native DAT structure parsing and writing infrastructure; HSDRawViewer is a useful viewer/editor reference, with its own stated incomplete areas. Evaluate actual model, joint, material, shape and animation behavior rather than substituting Brawl's representation. [T02]

Preserve native hierarchy, bind semantics, skinning, animation compression, resource references and relocation/container relationships. Validate model/costume data together with its motion data and expected fighter bindings. Read and write capability must be qualified independently.

Qualify both AnimJoint and Figatree animation structures rather than claiming complete Melee motion support after handling only one. The native tool developer identifies both in its Melee support documentation. [T15]

Distinguish editing/replacing an existing fighter from creating an expanded roster entry through a supported framework. The m-ex reference specifically targets Melee v1.02; qualify an integration against that version and the chosen framework build. A model plus motion conversion does not automatically provide action logic, fighter configuration or a complete expanded-roster build. [T17]

### 17.3 Ultimate

Use a dedicated SSBH route for model composition, meshes, skeletons, materials and animations, with helper and related data where applicable. The `ssbh_lib` and higher-level `ssbh_data` projects provide native read/write building blocks; SSBH Editor supplies a useful inspection and validation reference. [T03, T04]

Keep `.numdlb`, `.numshb`, `.nusktb`, `.numatb`, `.nuanmb` and applicable helper/mesh-extension relationships coherent. Declare supported file versions and operations explicitly. Preserve visibility/material animation, helper behavior and animation layering where the selected source and destination support them.

Handle `.nutexb` textures through a separately qualified decoder/writer. SSBH structure support alone does not provide the complete texture conversion route. [T04]

Source acquisition from an extracted dump or supported archive is separate from editing SSBH assets. Mod-folder packaging and runtime asset names are further target operations; new gameplay hooks are deferred. A correctly written NUANMB file alone is not sufficient proof of a working fighter.

Validate output semantics after fresh decode and, where possible, in an independent tool. Some native libraries may re-encode data on save, so preserve original input bytes with hashes while assessing generated output by decoded structure, tolerances and destination behavior. [T04]

### 17.4 Rivals of Aether 2

The official Workshop full launch was announced for 6 October 2026. Current official installation documentation identifies a Modkit based on Unreal Engine 5.8. Treat those as the dated baseline and detect the user's actual installed version before generating a target preset. Some import instructions are explicitly marked outdated after the engine change. [T05–T07]

Generate model and animation interchange assets for the official Modkit, then preserve its real template references and imported asset relationships. The current official animation guide requires 60 fps authoring and baked joint/morph animation; exported control rigs are not the destination runtime rig. Its main character animation path does not provide runtime IK/retarget postprocessing, so required corrections must be represented in the prepared animation. [T08]

The required progression is prepared mesh/animation files, successful Modkit import, correctly assigned target assets/states, local playable validation and, later, optional publication. Keep those states distinct. Select and test an import preset for the installed Modkit instead of hard-coding every checkbox from an older guide.

Use the supported official Modkit build and publication flow. Its documented Publish operation controls cooking and publication; generated interchange files or independently cooked assets must not be labelled a finished Workshop package. [T16]

The official Modkit can also export source models and animations, including a preview mesh with animation. Preserve their original skeleton association and coordinate conventions when bringing them into the shared library. [T09]

Rivals 1 GameMaker/GML scripts and sprites from the earlier Goku work are reference material for a separate adaptation. They are not directly compatible Rivals 2 skeletal assets or gameplay code.

### 17.5 Native output contract

Every export records the target game/build, source/target identities, included resources, transformation and retarget settings, writer versions, channel losses, destination bindings and validation outcome. Build into a staged location and promote the output only after its required validation succeeds.

Always distinguish an interchange file, a native asset, an assembled mod package and a tested playable fighter. Offer the most complete supported operation while clearly showing what remains to finish the chosen destination.

## 18 External tools and adapter research

### 18.1 Reuse existing implementations selectively

| Component or reference | Intended role in FighterPort |
| --- | --- |
| BrawlCrate and BrawlLib | Existing application, native Brawl resources and model/animation editor foundation |
| HSDRaw and HSDRawViewer | Melee DAT/native structure and evaluation/editing references |
| ssbh_lib and SSBH Editor | Ultimate native data APIs and independent inspection/validation |
| Rivals 2 official Modkit | Authoritative destination import, templates, project build and game testing |
| CUE4Parse and relevant Unreal tools | Candidates for title-specific cooked-package source readers |
| Xenoverse format libraries and tools | Candidates for EMD/ESK/EAN model/skeleton/motion source routes |
| DolphinTool and compatible disc libraries | GameCube/Wii source inventory and selective extraction where qualified |
| Blender Maya and 3ds Max | Optional modelling, rigging and interchange bridges |
| Godot source and animation systems | Research/reference for reusable concepts or suitable licensed components |

Use proven parsers and mathematics where they fit. Avoid making users operate a collection of disconnected editors for a routine supported transfer. The main FighterPort workflow remains the native BrawlCrate-based application.

Godot is a reference or component source, not a requirement to build FighterPort in Godot. External research does not establish that a particular game format is already decoded or that its assets will round-trip; tested implementation and evidence are required. [T14]

### 18.2 Dragon Ball and Unreal sources

CUE4Parse exposes Unreal package parsing/conversion facilities and is a candidate for supported source assets. Its presence does not automatically validate every FighterZ, Sparking ZERO or Jump Force build, animation codec, material or dependency. Qualify each game profile using real source-model playback. [T10]

Xenoverse tools provide concrete EMD mesh, ESK skeleton and EAN animation routes to investigate. Separate Xenoverse 1 and 2 validation, preserving materials, textures, native transforms and timing. Do not convert an animation while dropping the skeleton information needed to interpret it. [T11]

### 18.3 Undocumented source formats

For Battle Stadium D O N, Budokai 3, Budokai Tenkaichi 3, selected Naruto/One Piece titles and other unknown formats, use a bounded research process: identify the exact title/platform/build; catalogue containers; locate meshes, bones, weights and motion data; document transforms/compression; build a minimal decoder; compare original playback; then integrate through a typed adapter.

Prefer existing open implementations where available. When reverse engineering is needed, keep the results in reusable schemas, parsers and fixtures. A successful one-off extraction script should become a reproducible source route with version detection and explicit limits.

### 18.4 Toolchain management

Detect relevant installed tools and allow manual paths. Record version, architecture, supported operations, source/provenance and required invocation settings. Use isolated workers for dependencies that do not fit the native x86 process. Avoid transferring huge asset trees repeatedly across the process boundary.

The user should see a practical readiness explanation such as “animation writer unavailable,” “source skeleton missing” or “Modkit version needs an import preset.” Do not expose internal adapter jargon where it does not help them finish the task.

Preserve upstream notices and evaluate redistribution terms for any incorporated component before distribution. Keep development repositories free of private game inputs and generated commercial assets. This is a packaging responsibility, not a reason to block the current design or fixture work.

## 19 Current asset scope and deferred gameplay work

### 19.1 What the tool should complete now

**REQ 21 — Focus this implementation on models and animations.** The current tool should import and organize source characters, preserve native assets, edit models and animations, retarget across proportions, manage costumes/materials, assign animation assets, optimize and export valid destination assets through a simple UI.

Existing target gameplay can continue to drive replacement assets. Where the destination requires a template, resource link or animation-state assignment, manage that asset relationship through the supported toolchain. The user must not have to create a moveset before testing a transferred animation.

### 19.2 Clear output boundaries

An animation file, model package or validated Modkit import is a useful deliverable. Label it accurately. A new playable fighter may still need gameplay work outside FighterPort's current scope; do not imply that asset conversion automatically creates attacks, collision behavior or new roster logic.

Show which target asset was produced, where it belongs and which external target steps remain. Keep this guidance specific to the selected output instead of adding a permanent unfinished scripting stage to every project.

### 19.3 Preserve related data without authoring new code

Retain source timing markers, existing animation references, attachment points and unsupported native resources where possible. Editing an animation must not silently rewrite an existing moveset. Preserve relevant source data for later use without building a gameplay compiler now.

Costumes and transformation appearances remain in scope as models, material variants, visibility/morph data and animation collections. Portraits, stock icons and sounds may be retained as associated resources, but new authoring tools for them are not a dependency of the model/animation pipeline.

### 19.4 Deferred backlog

Moveset scripting and gameplay-code authoring may be considered later. Common moveset compilers, Brawl PSA/REL reverse engineering and authoring, hitbox/state-machine editors, runtime transformation behavior and scripted KI meters are excluded from the current roadmap and main UI.

## 20 Reference project for complete asset workflow coverage

### 20.1 Goku as an acceptance project

Use a multi-game Goku project to demonstrate the asset workflow: a selected Dragon Ball model, animations from the user's Brawl/Project M collection and other supported donors, manually edited motions, completed miscellaneous animation slots and independent Melee/Brawl/Ultimate/Rivals 2 asset targets.

Keep source style and destination presentation separate. A detailed source-quality model may require distinct target models and materials. Do not force all four games to share the same exported skeleton or visual treatment.

### 20.2 Previously requested outfit examples

The content project should be able to represent Teen Goku at the end of Dragon Ball with the Power Pole, Saiyan/Namek gi, Yardrat outfit, Cell/Buu gi, Saiyan armor, the jacket outfit in base form, Whis gi, End of Z, adult GT and Xeno Goku. These are actual outfit variants, not merely palette changes.

Previously requested transformation appearances include Super Saiyan, Super Saiyan 3, Super Saiyan 4, Super Saiyan God, Super Saiyan Blue, Ultra Instinct Sign and mastered Ultra Instinct. Store the associated models, materials, compatible rigs and animation variants as project assets. Implementing how a game activates those forms is deferred with moveset scripting.

### 20.3 What the example must prove

Show the new simple workflow from source selection to target preview and export, source-bound library reuse, model preparation, shared-bone mapping, contact correction, slot filling, fixed-duration transitions, outfits and multiple validated target assets. The example tests the generic pipeline; success on Goku alone cannot establish universal coverage.

## 21 Architecture reliability and responsiveness

### 21.1 Preserve current ownership boundaries

Keep the native editor authoritative for native document selection, committed curve changes and Undo. Shared Core services can prepare transformations, solve motion, manage durable metadata and return reviewable results through the existing process boundary. Do not create a second competing mutable document behind the viewer.

Extend existing services and files where their responsibilities fit. The inspected baseline includes native `ModelForm` workflows, Studio project/library/retargeting code and owned Core services under `engine/`. Refactor as needed to separate UI, asset interpretation, motion solving and output validation without rebuilding the application. [R01, R04, R06]

### 21.2 Proposed service boundaries

| Responsibility | Boundary |
| --- | --- |
| Native UI and authoring | Viewer, menus, selection, transforms, curves, Undo and native resource commits |
| Source providers | File/archive/disc inventory and bounded read-only access |
| Format adapters | Native decode/evaluation/write and declared per-version capabilities |
| Shared library | Source associations, immutable revisions, discovery and search |
| Project workspace | Fighter identity, independent targets, assignments and durable recipes |
| Rig and motion services | Characterization, FK/IK, retargeting, contacts, timing and fitting |
| Build and validation | Destination conversion, packaging, independent reopen and reports |
| Toolchain workers | Isolated dependencies, versioned requests, cancellation and result receipts |

These are responsibility boundaries. Do not create empty abstractions for every future game before delivering a working cross-game route.

### 21.3 Deterministic prepared results

Freeze the exact source, target, rig, reference and settings snapshot used by an asynchronous operation. If the native content changes while preparation runs, reject stale publication or require a new review. Cancellation must leave the project unchanged.

Prepare/bake expensive motion work outside playback. Keep frame evaluation deterministic and stable under random scrubbing, repeated loops and different preview speeds. Cache by relevant input identity and invalidate only the affected results.

### 21.4 Scale to real libraries

Support large game catalogues through lazy models/textures, bounded search results, background indexing, incremental refresh and cache eviction. A user should be able to search many thousands of clips without parsing every mesh into the native process.

Measure time-to-first-preview, search latency, batch throughput, memory and actual presented-frame pacing on declared hardware. Optimize measured bottlenecks while preserving readable code and correctness. Do not promise constant 60 fps on every system from one qualified Windows/NVIDIA result.

### 21.5 Source and project safety

Keep mounted originals read-only. Use staged outputs and clear destination paths. Preserve exact resource identities across duplicate names, nested archives and project branches. Validate file structure and resource bounds, and prevent interrupted operations from leaving partially accepted projects or outputs.

Maintain migrations and original synthetic fixtures for public regression coverage. Local game fixtures and captures can supply additional evidence without becoming source-repository assets.

## 22 Acceptance criteria and evidence

### 22.1 Four separate quality questions

Every completed operation must answer four questions: Did the source decode correctly? Does the transferred motion or model look and behave correctly? Did the destination bytes preserve the intended result? Does the destination toolchain or game accept and use those assets correctly?

Record these separately. Reopening with the same library helps find serialization errors but does not independently establish correct source interpretation or motion quality. Use analytic fixtures, another implementation, native references and destination behavior where practical.

### 22.2 Required acceptance scenarios

| Case | Required demonstration |
| --- | --- |
| A01 Native editor continuity | Load a model and separate motions; use actual lists, Play, scrub, bone selection, transforms and frame tools; edit keys automatically; Undo and reopen |
| A02 Model-only authoring | Pose a model with FK/IK controls, create its first motion, retain control ownership and save/reopen without a donor |
| A03 Shared library independence | Two fighters use one immutable donor; edit/save one result; restart and confirm the donor and second project remain unchanged |
| A04 Source association | Distinguish same-named clips and costumes in different containers; reject ambiguous, stale or missing rig associations visibly |
| A05 Bidirectional proportions | Mario to Snake and Snake to Mario, plus structurally altered/renamed synthetic rigs; preserve target anatomy and avoid identity-based branches |
| A06 Whole-motion contacts | Standing, sit/lie, get-up, hands on ground, ledge hang/climb/release and two-hand grips; inspect complete motions in both relevant directions |
| A07 Broader bone coverage | Fingers, unequal chains, helpers and extra anatomy; distinguish absent joints from omitted tracks and retain the missing-source-bones regression |
| A08 Independent controls | FK-only editing, IK-only correction and combined retargeting each work and save correct native curves |
| A09 Cross-format use | A Brawl model with a Melee source motion once adapters exist; preserve both originals and save a genuine chosen target animation |
| A10 Nonbone channels | Edit/save/reopen supported VIS0/SRT0/PAT0/CLR0/SHP0, preserve unrelated resources and deferred SCN0, and report cross-game channel gaps |
| A11 Model and motion port | A foreign model with rig/weights/materials and motions produces valid assets for each qualified destination |
| A12 Animation-only port | A foreign motion transfers to an existing native target with identical model/skeleton/binds/weights before and after |
| A13 Timing and transitions | Fixed-duration Idle/Fall or other state fitting, protected impacts, source holds, loop handling and explicit duration changes |
| A14 Coupled contacts | New anchors and release phases work through fitting; impossible constraints produce specific conflicts without skeleton edits |
| A15 Slots and filling | Verified target bindings, variable parts, protected custom actions and reviewed Fill Missing results; one batch Undo restores prior state |
| A16 Optimization | Reduced native keys or model resources satisfy declared error/size limits with contacts and important poses intact |
| A17 Multiple targets | One fighter regenerates at least two distinct destination outputs while target-specific changes remain independent |
| A18 Native and game validation | Fresh decode, independent-tool/Modkit checks and eventual actual gameplay receive separate evidence labels |
| A19 Viewer regressions | Visible animation playback, correct texture orientation/material lookup, source/target isolation and smooth presented motion |
| A20 Failure and recovery | Cancellation, stale worker output, changed source, corrupt input, interrupted save and unsupported format leave no falsely accepted result |
| A21 Simple workspace | First-time and repeat users complete both transfer modes and batch work in one coherent flow; advanced settings are optional and the full native previewer remains intact |

### 22.3 Motion and native validation measurements

Measure selected world-space positions/orientations, skinned geometry error, contact sliding and penetration, reach margins, bend continuity, loop seams, root trajectories and event timing. Include integer frames, fractional times and transition boundaries. Test random scrub order as well as forward playback.

Use explicit, versioned tolerances appropriate to the target and test. Retain existing numeric gates unless a reviewed reason changes them. Do not invent one universal angle, distance or vertex-error threshold for every character and destination.

Review complete rendered motions and every required frame in frame-by-frame acceptance sets, especially down poses and ledges. A few attractive screenshots cannot demonstrate the absence of a transient foot slip, pole flip or body intersection.

Check untouched source hashes and target model/skeleton invariants. For generated native outputs, compare decoded semantics as well as the expected retained bytes. Preserve native tangent/flag behavior and sample between stored keys.

### 22.4 Definition of a supported adapter

A source adapter qualifies only for specified game/build/format versions with reliable inventory, original rig/skin reconstruction, source animation evaluation and retained provenance. Unsupported dependencies and channels must be explicit.

A destination adapter qualifies only after real writer output, dependency/binding validation, fresh decode and the relevant target-tool acceptance. A “full fighter” label additionally requires the required gameplay/resources and actual target validation. One passing source animation does not certify the entire game or the reverse direction.

## 23 Implementation roadmap

### 23.1 Deliver the simpler workflow early

The following is a proposed delivery strategy that reflects the latest UI and scope request. Keep one executable acceptance task in `docs/plans/current.md`, with all other permanent requirements recorded in the master scope. Preserve accepted native behavior and unresolved solver gates while redesigning the outer workflow.

| Stage | Deliverable | Completion evidence |
| --- | --- | --- |
| G0 Reconcile the direction | Record the new simple shell, full native previewer and scripting deferral | Actual HEAD and capability matrix agree with the current scope |
| G1 Simplify one complete transfer | Source, target, preview/fix and export in one workspace using the accepted engine | A supported Brawl transfer completes without the old chain of dialogs; full native editing still works |
| G2 Strengthen retarget quality | Complete support context, coupled contact/release work and broader proportion cases | Whole-motion and subframe checks pass without native skeleton changes |
| G3 Complete the Brawl asset route | Qualified model/motion workflows and requested channel preservation/editing | Original preview, editable outputs and correct native resources/containers |
| G4 Batch and animation sets | Reused setup, variable parts, verified target bindings and Fill Missing | Approved batches preserve custom work, save independently and undo coherently |
| G5 First real cross-game route | One model and animation route beyond Brawl through the same workspace | Actual destination assets and target-tool acceptance, with reverse direction reported separately |
| G6 Cover all four destinations | Dedicated Melee, Ultimate and Rivals 2 routes with independent target projects | Separate model, motion, binding and validation evidence per game |
| G7 Expand donor libraries | FighterZ, Sparking ZERO, Xenoverse 1/2 and selected additional titles | Correct source-model playback and transfers to supported targets |
| G8 Production readiness | Multi-target builds, outfits, optimization, broader anatomy and usability polish | Reproducible asset outputs, clear losses and successful first-time/repeat-user workflows |

Given this project's Rivals 2 porting goal, the recommended first new destination is the official Rivals Modkit asset route, provided the installed toolchain and fixtures are available. Ultimate SSBH and Melee HSD integration can be prepared independently. This is an implementation recommendation, not a claim that one is already working or a reason to omit another primary target.

Do not require perfect universal retargeting before delivering the simple UI or a bounded adapter route. Use known accepted cases with clear limits, then widen motion and anatomy coverage through the same workflow. Moveset scripting and gameplay compilers are not stages in this roadmap.

### 23.2 Immediate implementation slice

Reconcile the inspected M2 queue with the new interface request. First establish one vertical slice: open or create a project, choose a registered donor animation and existing target, reuse the verified setup, preview a result with the original Next solver, edit it in the full native previewer and export/reopen a genuine native result.

Implement the new workspace's navigation and task panel around that working path. Preserve existing library identity, native curves, Undo, pin controls, source protections and save behavior. Avoid building a polished shell whose controls only call placeholders.

Keep complete native support timelines and coupled contact reposition/release as the next explicit quality dependency, or complete a necessary bounded part while integrating the UI. Path protection is not the new solver. Failed advanced results remain unavailable for accepted Apply until their existing gates pass.

### 23.3 Decisions that remain implementation-dependent

Confirm exact title/platform versions for the unspecified One Piece and Naruto source entries when those adapters begin. Derive native slot lists, export ceilings, import presets and framework compatibility from the user's actual supported builds. Choose concrete worker/parser dependencies after fixture and integration review.

Refine panel sizing, startup behavior and keyboard shortcuts using task walkthroughs of the new shell. The non-negotiable outcomes are a simpler workflow and complete BrawlCrate preview/editing behavior, not a fixed number of windows or a particular new UI framework.

## 24 Requirement traceability

The requirement identifiers record the user's durable intent. Detailed architecture, diagnostics and acceptance mechanisms are proposed ways to meet that intent. The implementation snapshot is evidence about one revision, not a promise about the whole roadmap.

| Requirement group | Specification coverage | Main acceptance cases |
| --- | --- | --- |
| REQ 01 BrawlCrate and Next | Sections 1, 2, 8 and 21 | A01, A19 |
| REQ 02 Both porting modes | Sections 4 and 9 | A11, A12 |
| REQ 03 Four primary games | Sections 3, 17 and 23 | A09, A11, A17, A18 |
| REQ 04 General motion | Sections 10, 12 and 13 | A05, A06, A07, A14 |
| REQ 05 Native preservation | Sections 5, 9 and 17 | A09, A10, A12 |
| REQ 06 Local runtime | Sections 1 and 18 | Core operations without required cloud services |
| REQ 07 Shared source library | Sections 6 and 7 | A03, A04 |
| REQ 08 Native editor behavior | Sections 8 and 11 | A01, A02, A08, A19 |
| REQ 09 Complete models | Sections 9 and 17 | A11, A12, A16 |
| REQ 10 All shared bones | Section 10 | A05, A07 |
| REQ 11 and 12 Independent FK and IK | Section 11 | A02, A08 |
| REQ 13 Body proportions | Section 12 | A05, A06 |
| REQ 14 Contacts | Section 13 | A06, A14 |
| REQ 15 Timing and style | Section 14 | A13 |
| REQ 16 Fixed-duration fitting | Sections 13 and 14 | A13, A14 |
| REQ 17 and 18 Slots and filling | Section 15 | A15 |
| REQ 19 Optimization | Section 16 | A16 |
| REQ 20 Simpler new workspace | Sections 7, 8 and 23 | A01, A02, A03 and A21 |
| REQ 21 Models and animations scope | Section 19 | Asset workflows complete without scripting prerequisites |

Additional preserved details include the complete donor-game list, native Brawl nonbone channels, SCN0 deferral, native Transform All/frame controls, automatic keys, right-side skeleton list, optional DCC bridges, independent target builds, outfit/form examples and full-motion visual review. They remain required even where a phase initially implements only a subset.

## 25 Implementation handoff

### 25.1 How to place and use this specification

Use the Markdown companion as repository documentation, preferably `docs/FIGHTERPORT_MASTER_SPECIFICATION.md`, with the layout image beside it. Link it from the existing master plan. Reconcile current leading decisions and actual HEAD with the latest simpler-UI and scripting-deferral request. Preserve historical acceptance evidence and one executable queue.

The layout is a concrete starting design for review and implementation, not an implemented screen. Keep the full native previewer while testing and refining the surrounding workspace.

### 25.2 Copy and paste instruction

```text
Continue FighterPort in https://github.com/DoctorFlux/fighterport-studio-next.
Use BrawlCrate/BrawlLib as the implementation base and the attached master
specification as the complete current scope.

The latest direction is a NEW, MUCH SIMPLER outer UI and workflow while
retaining the FULL BrawlCrate Model Previewer UI and editing behavior.
Do not preserve Next's current scattered workflow just because its backend
works. Do not replace the native previewer with only a similar renderer.

Read actual HEAD, AGENTS.md, README.md, docs/MASTER_PLAN.md,
docs/plans/current.md, docs/FORMAT_SUPPORT.md, docs/ENGINE_INTEGRATION.md
and relevant acceptance records. The latest user UI/scope decision overrides
older requirements to keep the existing outer layout or startup unchanged.
Keep the integrated owned engine and original Next retargeter as the default
while advanced alternatives retain unresolved validation gates.

Add the attached spec as docs/FIGHTERPORT_MASTER_SPECIFICATION.md, link it,
and keep one executable task in docs/plans/current.md. Start with one working
transfer through the new workspace: Source -> Target -> Preview and fix ->
Export. Reuse the existing accepted engine, native documents and serializers.
Keep project/library/retarget/correction/export in one coherent workflow.
Use automatic reviewed settings for known rigs and show focused corrections
only when needed. Preserve source/target selections and setup between clips.

Preserve the full native model editor: resource/animation lists, native camera,
right-side skeleton list, bone picking, move/rotate/scale, automatic animation
keys, interpolation, timeline, Transform All, actual native frame tools,
Undo/Redo and native/project save/reopen. Keep direct native editing available.
Use the proposed layout as a starting design, then validate real task usability.

Permanent current scope:
- Port a source model plus animations to Melee, Brawl, Ultimate or Rivals 2.
- Independently retarget source animations onto an existing target model.
- Make all four primary games sources and destinations through tested routes.
- Add FighterZ, Sparking ZERO, Xenoverse 1/2, Battle Stadium D O N,
  Budokai 3, Budokai Tenkaichi 3, Naruto Ultimate Ninja/Storm,
  selected One Piece games, Jump Force and existing mods as donor libraries.
- Preserve native documents and source model/skeleton/skin/channel data.
  Use adapters for mixed formats; do not force every asset into Brawl format.
- Cover CHR0, VIS0, SRT0, PAT0, CLR0 and SHP0 through qualified operations.
  Preserve SCN0 and defer new cross-game SCN0 processing for now.
- Use one source-bound shared library and independent target clip revisions.
- Support all meaningful shared bones, reviewed automatic/manual mapping,
  independent FK and IK, general proportions, contacts and batch transfers.
- Do not hard-code character names, actions, files, hashes or frame exceptions.
  Preserve the missing-source-bones regression and native omitted-track rules.
- Keep the target model/skeleton/binds/weights unchanged in animation-only
  mode. Model/rig conversion is an explicit derived target operation.
- Preserve source style/holds, root intent, timing and interpolation.
  Fit Idle/Fall/other boundaries within the existing duration by default.
- Provide animation asset slots, variable parts, Fill Missing, layers,
  optimization, costumes/materials and multiple independent target builds.
- Produce genuine target assets with native reopen and toolchain validation.
  Rivals 2 uses its official Unreal Modkit and baked animation imports.

Moveset scripting is OUT OF SCOPE FOR NOW. Do not implement PSA/REL research,
common gameplay compilers, hitbox/state-machine authoring, scripted meters
or new runtime mechanics as dependencies of this tool. Keep asset bindings,
source event preservation, existing native resources and destination asset
packaging where needed. Label asset outputs honestly when gameplay work
outside this scope would still be needed for a fully new playable fighter.

Retain the current unresolved contact work: complete native support context
and coupled contact reposition/release. Existing path freezing is not that
solver. Do not remove validation gates to make unfinished results look valid.
Keep expensive work outside playback; compare complete motions and native
subframes; preserve source hashes and target anatomy. Test the actual new
workflow and full native preview controls, not only backend calls.

Deliver a working bounded increment. State what became simpler, what native
features remain available, what was validated, which limitations remain and
the next concrete step. Do not substitute another roadmap-only scaffold.
```

## 26 Sources and reference register

### 26.1 Product requirement history

**U01 — Current decision and prior user requirements.** The latest 24 October 2026 request selects BrawlCrate and `fighterport-studio-next`, requires a much simpler new outer UI while preserving the full native Model Previewer, and defers moveset scripting. Earlier conversations supply the retained model, animation, all-bone retargeting, contact, project/library, source-game and asset requirements. These establish desired behavior, not current implementation claims.

**U02 — Earlier complete design.** FighterPort Studio Complete Master Design dated 17 October 2026 (privately supplied historical reference; account-scoped link omitted). Retained asset requirements were reconciled with the newer repository, simplified-UI and scripting-deferral decisions.

**U03 — Earlier library milestone.** FighterPort V2 Animation Library Next Task (privately supplied historical reference; account-scoped link omitted). Source association and library goals remain relevant; its older no-integration instructions and then-current milestone are superseded by later decisions and code.

### 26.2 Inspected FighterPort baseline

All R references below are pinned to commit `70119b4295309014f0af4b4fa660ee2d3d254d62`, inspected on 24 October 2026.

- **R01** [Next README](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/README.md) — current application, default retargeter and native workflows.
- **R02** [Current executable plan](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/docs/plans/current.md) — preview.13 and remaining contact/integration work.
- **R03** [Format support and evidence](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/docs/FORMAT_SUPPORT.md) — per-operation capabilities and native/game limits.
- **R04** [Native engine integration](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/docs/ENGINE_INTEGRATION.md) — current ownership, controls, timing and transition integration.
- **R05** [Contact and slot acceptance](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/docs/CONTACT_SLOTS_ACCEPTANCE_2026-10-24.md) — conservative contact-path protection, custom slots and reported checkpoint evidence.
- **R06** [Native retarget workflow](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/BrawlCrate/UI/ModelForm.Retarget.cs), [contact protection](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/BrawlCrate/Studio/NativeTransitionContacts.cs) and [slot workflow](https://github.com/DoctorFlux/fighterport-studio-next/blob/70119b4295309014f0af4b4fa660ee2d3d254d62/BrawlCrate/UI/ModelForm.Slots.cs) — code corroborating the inspected scope.

### 26.3 Primary technical references

These references establish the external project's documented role. They do not establish completed integration in FighterPort. Pin actual dependency revisions and test the selected versions during implementation.

- **T01** [BrawlCrate and BrawlLib](https://github.com/soopercool101/BrawlCrate) — native Brawl application and resource implementations.
- **T02** [HSDLib and HSDRawViewer](https://github.com/Ploaj/HSDLib) — Melee HSD DAT read/write and viewing/editing infrastructure.
- **T03** [ssbh_lib and ssbh_data](https://github.com/ultimate-research/ssbh_lib) — Ultimate SSBH formats and operation/version coverage.
- **T04** [SSBH Editor](https://github.com/ScanMountGoat/ssbh_editor) — model/animation editing, inter-file validation and output limitations.
- **T05** [Official Rivals 2 Workshop announcements](https://steamcommunity.com/app/2217000/workshop/) — full-launch announcement dated 6 October 2026 and engine compatibility notices.
- **T06** [Installing the Rivals 2 Modkit](https://rivals2.com/workshop/knowledge-base/getting-started/installing-the-modkit/) — current toolchain and installation requirements.
- **T07** [Importing model and animation into Unreal](https://rivals2.com/workshop/knowledge-base/character-creation/import-into-unreal/) — official target import workflow and outdated-setting notices.
- **T08** [Rivals 2 animation overview](https://rivals2.com/workshop/knowledge-base/character-creation/animation-overview/) — animation authoring/baking and runtime constraints.
- **T09** [Exporting content from the Rivals Workshop](https://rivals2.com/workshop/knowledge-base/miscellaneous/export-content-from-workshop/) — source export and preview-mesh association.
- **T10** [CUE4Parse](https://github.com/FabianFG/CUE4Parse) — Unreal package parsing/conversion candidate for title-specific donor adapters.
- **T11** [LibXenoverse2](https://github.com/Olganix/LibXenoverse2) and [EMD ESK EAN conversion source](https://github.com/Olganix/LibXenoverse2/blob/master/LibXenoverse/emdfbx/main.cpp) — concrete Xenoverse source-format candidates.
- **T14** [Godot source](https://github.com/godotengine/godot) — optional animation/rigging research and component reference.
- **T15** [Native developer explanation of Melee animation structures](https://github.com/Ploaj/StudioSB/issues/19) — AnimJoint and Figatree coverage; historical technical reference, not a current FighterPort implementation claim.
- **T16** [Official Rivals Workshop publication workflow](https://rivals2.com/workshop/knowledge-base/getting-started/publish-to-steam-workshop/) — supported cooking and publication boundary.
- **T17** [m-ex README](https://github.com/akaneia/m-ex/blob/master/README.md) — Melee v1.02 content-expansion framework reference.

