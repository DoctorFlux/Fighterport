# FIGHTERPORT STUDIO V2
## Source-Bound Universal Animation Library, Direct Game-Source Browsing, Auto-Rigging, Cross-Game Import/Export and Complete Fighter Animation Authoring

**Status:** Permanent V2 product specification and phased executable plan — not an assertion that every feature exists.  
**Prepared:** 18 October 2026  
**Repository:** https://github.com/DoctorFlux/fighterport-studio-next  
**Foundation:** independently imported, pinned upstream BrawlCrate/BrawlLib — https://github.com/soopercool101/BrawlCrate  
**Related product spec:** `docs/MASTER_PLAN.md` in the V2 repository and the original V2 master design.  
**Priority:** Animation library + correct source/model association + game-file discovery + interoperable model/animation adapters + simple, independent FK/IK authoring and combined retargeting, leading to complete native animation sets. Gameplay scripting, actual playable-mod packaging and rapid in-game testing follow later.

> **Product promise:** Select any model that FighterPort can decode, discover any eligible animation from a registered supported source game, and click **Use on This Model**. FighterPort knows the animation's authentic source rig, recognizes or helps review the target rig, evaluates the source motion correctly, retargets to target proportions with FK and optional IK, allows editing, and bakes a verified native result for the chosen target game. The same source animation can be reused in multiple fighters without contaminating the source or another fighter.

> **Compatibility boundary:** "Any model + any animation" is the UX ambition, not a license to silently apply mismatched skeletons or claim unsupported readers/writers. Each exact game version, native container, animation channel and target output direction has an independently verified capability. Unreachable contacts, unknown rigs, inaccessible encrypted contents and unimplemented target writers must result in an understandable, actionable status rather than false success.

---

# 1. Live repository inspection and decisions

As inspected on 18 October 2026, V2's public `main` is **`7decc0cf094f6f1dd4c5f0abff325b791704e017`**, titled *Record portable preview acceptance and refresh stale GUI controls*. Check actual local/remote HEAD again before changing anything because development may have continued. Do not confuse this repo with the retired `DoctorFlux/fighterport-studio`.

Repository `docs/DEVELOPMENT_STATUS.md` records the following **repository-reported** results (not independently rerun in the present conversation):

- Actual BrawlCrate-derived WinForms/.NET Framework 4.7.2 x86 application, a simple `.fpstudio` fighter-project document, Brawl model-only setup and visible selected animations.
- Local Brawl Mario model PAC plus motion PAC: **297** motions; Snake data also used. Native clip playback, model viewer and fresh native save/reopen. Original sources remain unchanged in reported tests.
- `Retarget to This Model` with generic humanoid characterization, FK, proportion correction, hand/foot IK and Brawl CHR0 output for a tested subset. The release-preview work reports locomotion/retarget regression results and rendered GUI tests.
- `BrawlCrate/Studio/FighterProject.cs` currently owns a **single fighter's** model and animation file registrations, with a `256`-animation-file cap. It stages imported Brawl resources and saves a `.fpstudio` plus assets; it is **not** a global, cross-project source-model-linked library.
- `BrawlCrate/UI/ModelForm.Retarget.cs` still offers a separate **Choose Animation Source Model** dialog in a common case. The chosen donor path is cached only locally in the editor flow; this is a high-impact UX limitation the persistent source-linked library should eliminate.
- **Not yet implemented** according to the repo: global shared library, reusable fully authored IK/FK rigs, general/non-humanoid rig authoring, per-game animation-slot templates, miscellaneous auto-fill, non-Brawl native adapters, multi-target character outputs, and gameplay compilation. User-provided V2 goal expands these requirements; do not mislabel the existing Brawl subset as universal support.

**Immediate decision:** Introduce a *small, persistent, read-only game/source catalogue and globally shared, source-bound animation library* now. Make source-model association an invariant from day one. Do not rewrite the working native editor, its playback or proved Brawl retarget solver merely to add library infrastructure. Refactor narrow user-facing paths when demonstrably needed, preserving acceptance tests.

# 2. Everyday user experience — no more donor skeleton dialog for known assets

There are three distinct but visually integrated concepts:

1. **Game Sources:** folders, local installed games, user-supplied disc images, extraction directories, mods/overlays, Modkit projects, and individual files. A registered source is **read-only**. It is the place assets actually live.
2. **Universal Animation Library:** a persistent, indexed catalogue of *source character + verified skeleton/model + animations*. Every animation has a durable origin, can be previewed on its own source model, and may be selected from any fighter project.
3. **Fighter Project:** a target model/rig, its target game(s), a versioned list of animation-slot assignments and independent retarget edits. A project links to library entries; it does not duplicate the whole library or own source assets.

### Main first-time action: Add Game Source

- Choose **Installed Game**, **Extracted Folder**, **Project M/Project+ Overlay**, **GameCube/Wii ISO/RVZ/etc**, **Ultimate `data.arc` or dump**, **Rivals II Modkit/Project**, **Unreal cooked installation**, **Xenoverse folder**, or **Single Model/Motion File**.
- Register a label and root path once; detect game/version and supported file structures where supported. Show progress and **capabilities** before doing expensive work.
- Inventory read-only file and resource metadata. Build a library *index*, not a wholesale duplicate of copyrighted input assets. Decode and selectively stage only chosen models/clips for previews or conversion.
- Resolve a source's character, costume variant, skeleton and associated motion collection using per-game rules and native identities. If ambiguous, present the two or more candidates and **remember a user-approved link**. Never bind a clip to a guessed character silently.
- Offer **Open Source Character**, **Preview Source Animation**, **Add to Shared Library**, **Use on This Model**, and **Rescan / Repair Paths** in the same main editor. Avoid a long sequence of companion-manifest dialogs.

### Main authoring action: Use on This Model

1. Open or create target fighter; its model is the active editing target in the existing BrawlCrate Model Previewer.
2. Browse or search library by game, character, clip, tags, channel and target semantic category.
3. Click source clip to play it on its own read-only source model or audition source/target side by side without changing active target.
4. Press **Use on This Model** or drag clip to a slot. Exact compatible rig => directly apply. Verified known source/target profiles => reuse saved mapping. Ambiguous rig => show a *small, single-review* correction view.
5. Default action auto retarget (game-neutral evaluation → FK transfer → proportion fit → contact IK if relevant); produce independent target clip. Preview with source/FK/corrected comparison on demand.
6. Optionally use simple IK/FK handles, timing and transition controls. Apply as a new target revision. Save/Export when verified.

**Routine flow:** `Open fighter → find animation → Use on This Model → preview → save`. Advanced rig mapping, body measurements, IK targets, curve diagnostics and native format details go behind **Advanced**. Show a small status, not a wall of controls.

### Preserve native BrawlCrate usage

Normal File/Open, archive tree, original model editor, texture/material/channel editors, timeline, viewport and animation controls must remain usable. The studio context supplies a **minimal extra source browser and library tab**, not a replacement renderer or separate gigantic dashboard. Do not hide playback or other native panels; persist dock sizing. Do not confuse a preview source model with the editable fighter model. Keep the user on the same primary window.

# 3. Source-model association: absolutely mandatory invariant

**No reusable skeletal animation may be registered as a fully transferable library asset without a resolvable source rig/model (or an explicit, validated skeleton-only representation).** The original animation and its native model must be logically linked, even if stored in different game files.

Record at least:

| Field | Purpose |
|---|---|
| `libraryClipId` and immutable revision | Globally stable identity; a rename never changes the identity. |
| `sourceGameId`, title/version/platform and adapter version | Correct native format evaluator, axes, units, frame conventions, helper rules. |
| `sourceMountId`, relative virtual file and container chain | Return to exact source in an ISO/ARC/PAC/PAK/folder without extracting whole source. |
| Native motion resource ID/tree/index/path | Distinguish different clips with identical names in separate containers or branches. |
| Raw container hash **and** selected-resource hash | Detect replacement while keeping unchanged parts intact. |
| `sourceCharacterId`, costume/body-variant ID | Preserve true owner of the motion; prevent accidental mixing of similarly named assets. |
| `sourceModelId` / exact rig fingerprint / native rest/bind interpretation | Evaluate the clip correctly and retarget only through approved mappings. |
| Link evidence and approval (`native`, `title manifest`, `verified directory convention`, `user-reviewed`) | Clearly distinguish discovered truth from an inferred suggestion. |
| Native animation channel types, timebase/fps, loops, step/linear/cubic, source frame rate | Preserve body, morph, material and other channel timing. |
| Dependency references (textures, skeleton/constraints, props, submotions) | Know when full fidelity needs an additional resource. |
| Missing/stale/read-only/unsupported state | A source can be offline without deleting the library record; retarget must refuse stale unverified data. |

Separate **source-model provenance** from **working editable target instance**. Each target instance pins source clip revision, target model/rig/body, map revision, retarget options and output destination. Retargeting does not mutate or re-export donor animation in place.

### Native association order (avoid unreliable assumptions)

1. Read explicit animation/model references in native game structure if present and properly decoded.
2. Resolve character and motion membership from verified title/build manifests or resource location rules.
3. Resolve known variant/common-skeleton relations (costume models, same body rig, shared universal clips) only when fingerprint/interpretation checks pass.
4. Use directory/file naming and bone-name compatibility as **suggestions**, never sole approval.
5. Compare skeleton fingerprint, parent hierarchy, rest axes, native animation bone binding and independently observed source playback.
6. If more than one viable owner remains, present short options with model thumbnails/rig status. User approves once; cache the approval with source hashes. If no source model can be established, keep the clip in an **unresolved queue** (searchable but unavailable for certified auto-retarget) and let the user attach the correct model.

Tests must cover two equal-name clips under different PAC ARC branches, model/motion separated in different folders, model shared across outfits, replaced file invalidating the association, a wrong model with matching bone names, and offline source restoration.

# 4. Universal game-source catalogue and selective extraction

Use a `SourceMount` / `GameProvider` contract that can **Browse**, **Identify Game and Build**, **List Characters**, **List Models and Rigs**, **List Motions and Channels**, **Resolve Associations**, and **Open a bounded read-only stream of an exact asset**. It is not required to export the entire disc before showing one fighter's clips. Each provider declares `Supported`, `Partial`, `Unsupported` per operation.

### Provider families

**A. Brawl / Project M / Project+ / Brawl-derived mods**
- Loose `.pac`, `.pcs`, `.brres`, `.mdl0`, `.chr0`, other Brawl animation resources; nested ARC and motion resources.
- Configured Brawl folder dump or original user-supplied Wii image; Project M / Project+ SD-card-like overlay with explicit base-game source and deterministic overlay priority.
- Source character can be in a model PAC while motion resides in another PAC. Record both exact container paths and source rig; preserve unrelated resources on native edits.
- First actually runnable provider: **Brawl file/folder + model/motion package association** using the inherited parser. Then plug in Wii image virtual filesystem without changing this model.

**B. Melee**
- Folder dump, GameCube ISO/GCM/RVZ/etc and supported HSD `.dat` structures. Different model/animation/action relation from Brawl. HSDRaw/HSDLib and m-ex/MexManager are useful references.
- Native GameCube disc browsing can reuse a `nod`-based read-only worker or tested Dolphin-derived filesystem operations. Do not use a Brawl animation intermediary. Version and mod-framework bindings must be verified before native outputs.

**C. Ultimate**
- Extracted source folders and supported `data.arc` obtained from the user's lawful assets. Native SSBH model/skeleton/animation resources, with motion folders often distinct from costume/model folders.
- Support `data.arc` through a versioned archive-reader adapter inspired by ArcExplorer; use `ssbh_lib`/`ssbh_data` and SSBH Editor as technical references. Do not assume Switch game images are GameCube `.iso` files or that their protected content can be browsed without legitimate user-side preparation.

**D. Rivals II**
- User-selected installed game directory and **official Modkit project** separately. Inspect actual Modkit template asset paths, skeletal assets and baked animation sequences; link each animation to the correct target skeleton.
- Prioritize supported source/import and target animation asset creation through the installed Modkit. Do not invent cooked UE `.uasset` writers or claim a complete fighter from a folder of animation files.

**E. Dragon Ball FighterZ and Sparking! ZERO**
- User-selected installed game directory / authorized exported files; title-specific Unreal package readers (`.pak`, `.uasset`, `.uexp`, `.ubulk`, and for particular UE5 builds `.utoc`/`.ucas` as applicable). Use CUE4Parse/FModel read support and proven title-specific plugins/workflows where compatible, not arbitrary UE version guessing.
- Bind animations to native skeletal mesh / Skeleton reference, not merely to character name. Preserve complex rest transforms and cartoon stepped/held frame cadence. Game-specific cooks/expression tracks are separate capabilities.

**F. Xenoverse 1 and 2**
- Recognize `ESK` skeletons, `EMD` model meshes, `EAN` animations, `EMM` materials and `EMB` textures as tested; archive/other related formats require title-specific support. LibXenoverse, LibXenoverse2 and XenoKit provide relevant reference implementations.
- Associate EAN animation with the native ESK and correct character mesh, considering alternate skeletons/costumes. Preserve the clip's own timebase/pose type. No assumption that all XV1 and XV2 versions are identical.

**G. Others (later)**
- Smash 64, Wii U, 3DS and further Nintendo/Dragon Ball or other 3D animation sources by plug-in adapter. Do not claim common formats just because a file extension is shared.

### Saved paths and cache behavior

- Global `Sources` tab stores friendly game label, provider ID, exact source root or image, version, mount ID and health. Game paths are **not embedded in a public GitHub source repository**.
- Use stable virtual paths such as `mount://<id>/partition/resource/container-index/item-id`, backed by a local absolute root and safe content hashes. Relative names must never escape their allowed mount.
- Library catalogue persists across restarts. Rescan is incremental and cancellable. Source moved? **Relocate Source** and verify hashes/identities; preserve library IDs, projects and approved associations when identical.
- Missing offline mount => show searchable metadata, preview cache only where permitted; block native conversion requiring unavailable source bytes. Never silently switch to a different game install or update cached source interpretations.
- Deduplicate identical immutable blobs; opt-in bounded cache only. No mandatory complete extraction, large duplication, illicit redistribution or bundled commercial content.

# 5. Shared source-bound library: persistence, search, previews, reuse

### Required library behaviors

- Global, across projects and target games, with reliable crash-safe storage. Minimal initial implementation can use an atomic manifest with a bounded index; SQLite metadata is an option if tests and dependency review justify it. Avoid old V1's bloated project-owned library architecture.
- Paginated search by game, source fighter, native format, clip name, semantic tags, action category, duration, body/morph channels, known rig, version and source-mount health.
- Source-preview on the **source rig**, target-preview on the fighter's rig and optional side-by-side. Preview can selectively extract/decode to temporary files, then discard. Keep source model and target separately visible and labelled.
- Register all selectable clips in a selected motion pack with the same proper source model when native evidence justifies it; don't make users add 297 clips one at a time.
- **Add selected / Add whole motion set** options; detect duplicates by exact origin+native resource identity+hash, not by animation display name.
- Clip classifications from observed native names/state templates may be proposed, marked unverified if inferred. A special, multi-part animation or facial track remains representable without needing gameplay scripting.
- Automatic profile reuse: if an approved source or target skeleton fingerprint matches, auto-apply reviewed semantic rig map. If its topology changes, invalidate review and show only exceptions.
- Persistent target-side instances: independently edit or retarget one clip without modifying global immutable source records or another project's output.

### Portable source references

Support (1) reference-only entries pointing to user-owned originals, (2) selectively cached approved private content, and later (3) portable *metadata-only* profile/slot packs. A copied `.fpstudio` file must not imply that it has legally portable source assets. Report missing mounts with easy relink.

### Recommended minimal schema

```text
SourceMount(id, provider, gameBuild, localRoot, sourceHash, health)
SourceCharacter(id, mountId, nativeCharacterIdentity, gameBuild)
SourceModel(id, characterId, mountId, virtualResourcePath, resourceHash,
            skeletonFingerprint, restInterpretationId, costumeOrBodyId)
SourceAnimation(id, characterId, sourceModelId, mountId, nativeResourcePath,
                resourceHash, channelManifest, timebase, revision, associationEvidence)
RigProfile(id, modelId, skeletonFingerprint, semanticChains, axes, bodyFit,
           confidence, reviewStatus, version)
RigMap(id, sourceRigFingerprint, targetRigFingerprint, reviewedChains,
       transforms, solverPolicies, reviewDigest)
FighterTargetInstance(id, fighterProjectId, targetGameBuild, targetModelId,
                      sourceAnimationId, sourceRevision, rigMapRevision,
                      correctionLayers, nativeSlotBinding, bakeReceipt)
```

Opaque native source identifiers, hashes and provider version are mandatory. No assumption that one skeleton model applies to all costumes; allow explicit rig sharing when verified.

# 6. Model and animation formats: full requested import/export scope

**This is a capability goal matrix, not today's compatibility claim.** Track each format's parser, evaluator, viewer, editor, native writer, fresh decode and tested game build separately.

| Title / family | Key input model & skeleton types | Motion / other channels | Container/provider | Intended native output route |
|---|---|---|---|---|
| Brawl / PM / P+ | `MDL0` in `BRRES`/`PAC`/`PCS` plus textures/materials and skinning | `CHR0` skeletal; `SRT0`, `PAT0`, `CLR0`, `VIS0`, `SHP0`, `SCN0` as their actual channel semantics permit | nested `ARC`, `PAC`, `PCS`, Wii source dump/ISO and overlay | Genuine native resource/animation edits, then supported PAC/PCS reconstruction preserving siblings |
| Melee | HSD `DAT` model/joints, skeleton/weights and native character data | HSD joint/figure/texture animation structures and relevant dependencies (actual names to inspect per build) | GameCube filesystem, `.dat`, m-ex projects where configured | Valid HSD DAT or supported m-ex asset/project writing with exact target binding |
| Ultimate | `nusktb`, `numshb`, `numdlb`, `numatb`, `nutexb`, `nuhlpb` as supported | `nuanmb` skeletal and other typed SSBH channel semantics | `data.arc`, extracted model/body + motion folders, mod overlays | SSBH native animation and mesh/skeleton outputs; exact motion folder/slot binding, reparse |
| Rivals II | Unreal Skeletal Mesh/Skeleton/Physics or suitable interchange (`FBX` as documented) | baked Animation Sequences / morph tracks; target Modkit state assets | installed Modkit project, actual supported UE assets | Modkit-importable baked animations and meshes; inspect actual generated character templates |
| FighterZ | Unreal skeletal mesh and source skeleton (exact cooked package version varies) | animation sequences with stepped/held/art-style source timing, additional face/prop tracks | title-specific `.pak` / `.uasset` / `.uexp` etc | High-priority read/evaluate/retarget into Smash/Rivals; native FighterZ writing later after proven cook pipeline |
| Sparking! ZERO | title-specific UE5 skeletal assets / rig data | UE5 animation sequences and extras in title/build-specific packages | `.pak` or `.utoc/.ucas` and Unreal assets where applicable | Source-model/motion import first; native Sparking writing only with proven tooling |
| Xenoverse 1/2 | `ESK` rig, `EMD` mesh, associated material/texture data | `EAN` plus applicable camera/face/other animation variants | per-title game/data archives/loose files | Source import/evaluate/retarget first; native writer/version coverage as independently verified |
| Additional interchange | `glTF/GLB`, `FBX`, `DAE`, `SMD`, `BVH` only when a tested reader/writer and skeleton interpretation exists | skeletal pose/curves, morphs or other supported channels | loose user-authored files | Intermediate **optional** import/export, never a mandatory lossy gate for native output |

**Do not claim any listed family is already supported just because the engine can read the extension.** Some native files contain multiple interdependent assets/channels. Reading a mesh is not equivalent to correct native facial animation, materials or mod packaging. A reader and writer may support different versions.

### Explicit `IAssetAdapter` capability contract

```text
IdentifySourceVersion
InventoryContainers / ListNativeCharacterCandidates
ResolveModelAndAnimationAssociation (+ evidence, confidence, conflict status)
DecodeModelAndSkeleton / DecodeAnimation / DecodeChannels
EvaluateSourcePoseAtContinuousTime
ReadNativeBindRestAxesScaleConstraints
EncodeNativeAnimation / EncodeNativeModel (if supported)
ReopenNativeAndMeasureError
ReadOrGenerateTargetAnimationSlotManifest (if verified)
DescribeRequiredDependencies / NativeOutputLayout
DescribeLosses / UnsupportedChannels / VersionBoundaries
```

**The common retarget solver must never require `MDL0/CHR0`** for Melee, Ultimate, Rivals II, Xenoverse or Unreal; those are only one adapter's native types. Use a versioned common skeleton/mesh/motion representation with `double` precision intermediate matrices/quaternions, explicit frame/timebase and channel extensibility.

### Native model previewer contract

The viewer should load a compatible **decoded representation** from each supported adapter and render a selected model. It should support skeleton/skin weights, materials/textures where available, bone gizmos, source animation sampling, stepped cadence, morphs and additional native channels as each becomes supported. Do not simply accept a file into a resource tree and show a blank/inaccurate viewport. Keep original Brawl rendering/editor unchanged for its supported resources.

A non-Brawl model should not be forced into an MDL0 just to preview it; implement renderer interoperability or an isolated worker/result bridge. A non-Brawl native writer must be real and validation-backed rather than renaming file extensions.

# 7. Rig profile, automatic FK, automatic IK and actual retargeting (three distinct tools)

**FK authoring:** editable hierarchical rotations/translation curves, pose controls, local/global axes and keyframes on **the current model**, with no donor clip required. FK control handles and target deforming skeleton are separate layers.

**IK authoring:** target hands/feet, wrist/ankle orientation, elbows/knees and pole goals, pelvis/spine/root compensation, grounded interval/prop contact, joint limits and a solver; again **no source animation required**. Reuse the same live viewport/timeline.

**Retargeting:** transfers existing source clip motion from its own source model to selected target skeleton. It uses semantic characterization, rest/calibration frames, FK motion intent and optional IK for proportion/contact fitting. This operation is **not the same** as FK or IK, but can compose both.

### Automatic known-skeleton setup

- Approved per-game/build source profile catalogue for Brawl, Project M/P+, Melee, Ultimate, Rivals II, FighterZ, Sparking! ZERO, Xenoverse 1/2 as each real family is validated.
- Infer chains for root/hip/pelvis/waist/spine/chest/bust/neck/head, shoulder/clavicle/upper/lower arm/hand/fingers/thumb, thigh/knee/shin/foot/heel/toes, and optional facial/morph/twist/helper/hair/weapon/tail/wing attachments.
- Match **known bone-name aliases plus topology, bind/rest orientations, axes, symmetry, chain lengths and optional skin geometry**. Names alone cannot approve a mapping.
- Exact reviewed rig fingerprint → automatic one-click; known skeleton family with matching structural evidence → auto suggestions and minimal review; unknown or different rig → short interactive bone/chain mapping, saved once.
- Display confidence by unapproved chain and preview source/target rest/calibration. Reuse profiles across animations and projects, but include body/costume mesh identity for contact landmarks. A Deltarune-ish stylized/nonhumanoid body should use an explicitly extensible profile, not fake humanoid mappings.
- Support user-defined FK/IK controls and advanced rig profiles that do not require a source animation.

### Core math and quality

1. Evaluate native source animation and convert through its own explicit coordinate/timebase/scale/rest semantics.
2. Preserve source FK motion relative to calibrated anatomical frames and transform target's own parent hierarchy, including differing local bone axes and joint counts.
3. Use source/target proportions, pelvis/shoulder offsets and body landmarks to place reachable goals. Distinguish free motions, planted soles, hand-head, hand-hand and grips. No default floor locks for aerials.
4. Solve IK contacts with coherent poles, bend frames, joint limits, pelvis/spine/clavicle contribution and temporal continuity; no unintended limb stretching.
5. Fit protected transition margins and native target timebase; preserve intentional FighterZ held/stepped timing and root motion. A 0.5x/2x or custom speed control explicitly chooses duration/timing behavior.
6. Bake into the **target's** native deform skeleton/animation channels, preserving timing/action poses, re-open and measure native curve/mesh/contact error at integer **and fractional** times.
7. Render source/FK-only/corrected target comparisons. When physically impossible or structurally unsupported, show specific unfulfilled constraints and safe alternatives.

### Simple editor controls

Normal UI: `Auto Rig`, `Retarget to This Model`, `IK`, `FK`, `Fit`, `Reset Correction`, `Apply`, and `Save` (only show contextually relevant items). Advanced controls: chain mapping, calibration/rest, contact intervals, poles, root policy, joint limits, per-bone curve editor, full quality report. No old V1 large multi-tab forms or hidden registrations for routine work.

# 8. Animation-slot templates and complete fighter animation sets

The studio must know **which animation assets each target game, framework and build expects**. Maintain a universal semantic taxonomy and verified per-game binding packs; never claim a semantic label by itself is an actual native runtime slot.

- Movement/neutral: Idle/Wait variations, walking, run, dash, turn, crouch, jump squat, jumps, fall, land, platform drop, fast fall and more.
- Attack set: jab `1..N`, rapid jab, tilts, dash attacks, smash charge/release/followup, aerials with landings. No fixed part count.
- Specials: neutral/side/up/down with arbitrary ground/air, start/hold/loop/release/follow-up/end variants and custom states.
- Grabs/throws/shield/dodge/parry, ledge hang/climb/attack/roll/jump, knockdown/get-up/tech/tumble, damage/hurt/dizzy, items, entrances/results/taunts/win states and miscellaneous extras.
- Facial, finger, morph, costume, helper, visibility, color/material/UV and accessory animations tracked as typed channels, with per-game writer capabilities.

A slot pack stores target game **version**, underlying native asset name/id/path, semantic group/phase/variant, required/optional/fallback flags, base reference pose and expected export time/channel conventions. Generate/verify packs using **actual target-native source files or official templates**, not ungrounded guessed hard-coded names.

**Rivals II:** inspect actual generated Modkit character assets and official State Animation/Attack Animation assignments; its Modkit needs skeletal mesh, skeleton, animation sequences and multiple linked data assets. Body/morph animation may be baked through supported FBX/Unreal import, but those assets alone do not constitute a playable character. Gameplay scripting remains later.

**Brawl/Melee/Ultimate:** derive authoritative slots per build/character/framework, including multi-part phases, base shared states, supplemental channels and actual motion resource IDs. Keep user's extra slots `Unbound` until a real native association exists.

# 9. Fill Missing Miscellaneous (batch automation)

Target scenario: custom Goku already has bespoke movement, attacks, specials, taunts and wins. Select **Fill Missing Miscellaneous**; choose Brawl/Ultimate/other donor collection; keep all existing authored clips protected.

Batch planner:

1. Compare the selected target game's **verified required animation slots** with currently assigned/covered slots.
2. Select donor animations linked to **correct source model** and evaluated original rig, not clips chosen only by matching name.
3. Reuse source→target rig profiles to batch retarget onto character's own body proportions, FK style and IK contacts. Protect costume-specific contacts if needed.
4. For each slot choose the correct **entry and exit reference** from target Idle/Fall/Crouch/LedgeHang/Knockdown/Shield/etc. Save exact target clip revisions and playhead phases.
5. Fit the original clip's beginning/end **within its existing frame count/duration by default**. Change interpolation keys inside the original time range, preserve central protected action/expressive holds; do not append invisible transition frames or change semantic event timing silently.
6. Maintain ledge→hang, get-up→Idle, aerial→Fall, multipart→next part and immediate hit reaction conventions rather than always blending Idle→Idle.
7. Preview all changes/donor provenance/conflicts in one transaction; accept/skip individual items, Apply as one undoable batch, export only formats with native writers. Unreachable contacts/short overconstrained clips get explicit `Needs Review` status.

This subsystem must be **designed after** target slots, rig reuse and cross-game evaluation exist; do not replace unfinished backends with superficial ‘Auto Fill’ buttons.

# 10. Architecture and data boundaries — lightweight, but correct

```text
FighterPort Studio V2 (existing BrawlCrate WinForms UI and Model Previewer)
    |
    +-- FighterProject (.fpstudio; target character, target games, edits, slot assignments)
    +-- GameSourceBrowser (persistent mounts and bounded virtual filesystem)
    +-- SourceBoundAnimationLibrary (global catalogue, immutable identities, lazy previews)
    +-- GameAssetAdapter Registry (Brawl, Melee, Ultimate, Rivals II, DBFZ, DBSZ, XV1/2)
    |     +-- each adapter supplies reader/evaluator/optional writer/validator
    +-- Neutral Skeleton / Motion / Channel Core (not CHR0-specific)
    +-- Reviewed Rig Profile Store and automatic bone/chain matcher
    +-- FK Posing and IK Rig Editor (independent + composable)
    +-- Retarget/Fit/Bake/Validate service
    +-- Target Game Slot Templates and batch Misc Fill
    +-- Native Asset Export and loss/provenance receipts
    `-- Later Gameplay/Full Fighter Build/Fast Game Test
```

### Keep the first implementation small

- Extend the existing `FighterProject` only where project ownership requires it. **Do not create a single massive project XML containing every game asset in the global library**.
- Implement `SourceMount` + `SourceModel` + `SourceAnimation` and source-model link as a small tested service first. Use simple durable storage and one library lookup pane in the native previewer.
- Introduce game-format-agnostic interfaces only when there is a real first adapter and tests. Avoid abstract classes for 25 hypothetical games without actual reader/writer evidence.
- BrawlCrate is `net472/x86` now; modern Rust/UE/SSBH dependencies may use explicitly isolated `x64` workers with versioned IPC and bounded output, rather than fragile in-process embedding. Preserve one coherent UI.
- Read-only game inputs and immutable library clips; target edits are independent in a staged revision. Treat paths and source files as untrusted. License and provenance audit before copying outside project code or shipping dependency binaries.

# 11. Concrete implementation phases and acceptance gates

## Phase L0 — Source-bound Brawl library: next implementation task

**Goal:** In V2's actual native UI, index Brawl Mario and Snake supplied model/motion PACs as read-only game-file sources. Their 297/355 motion metadata (as declared by existing project tests) should be visible in the GLOBAL library linked to the correct model; use one clip from each source on the *other* active fighter without a repeated donor-model picker. Do not refactor the already passing original model browser or Brawl retarget solver unnecessarily.

Required vertical slice:
- `Add Source Folder / Files` registers read-only model PAC + motion PAC. Discover candidates and group them by selected original character and model. Register selected/entire motion set atomically; preserve exact indexed native paths and SHA-256. No manual animation-by-animation rig setup.
- Source model associations use verified explicit selection and persistent evidence; wrong/ambiguous model is not auto-approved. A source model does not need to be rendered permanently with the target.
- Globally persistent source-linked library accessible from two separately saved `.fpstudio` fighters; searchable native clips and source model preview/play. Ensure source aliases don't collide, and same clip can be assigned multiple times.
- `Use on This Model` selects the source clip with its resolved model automatically, invokes the existing retarget route, retains target model, and saves the generated animation as an independent target asset. Preserve legacy manual one-off retarget for imported unresolved clips.
- Test both Mario↔Snake and source-to-own-model; capture exact source-model association in target provenance. Native CHR0 and target-supported PAC asset round trips where genuinely supported; all original source hashes unchanged. Confirm project restart, source reload and missing/replaced source diagnostics.
- Test GUI on the actual executable: select model, browse library, choose clip, source preview, return to target, retarget, scrub/play, save/restart and new native output. Test independent second fighter remains untouched.
- Avoid bloating main toolbar: one compact **Library** browser and one **Use on This Model** action. Advanced rig corrections remain contextual.

**No requirement to implement ISO, Melee, Ultimate, Unreal or Xenoverse in L0.** Their providers appear as *documented planned capabilities*, not placeholders claiming support. Keep the current Brawl retarget and regression suites passing. Commit reviewed passing source increment to this exact V2 remote only under its existing authorization.

## Phase L1 — GameCube/Wii optical image source + Brawl overlay resolution

Read user-owned Melee/Brawl ISO/GCM/RVZ via tested, bounded `nod` adapter (or documented equivalent). Inventory filesystem without whole-disc extraction, then open character model/motion assets through the same L0 library/linking path. Test exact same association whether inputs are mounted image or extracted folder; preserve image hash and support cancellation/path guards. Project M/P+ overlays use explicit base source and conflict statuses.

## Phase L2 — Reusable automatic rig profiles and independent FK/IK authoring

Persist exact reviewed skeleton-family mappings and body/contact profile variants; Auto Rig on verified structures; minimal corrections for uncertain chains. Add actually functional FK edit and IK goal/pole handles **independent of retarget**, and apply them as optional retarget correction layers. Verify no explicit manual source-model selection when library profile exists. Include different proportions, rotations, finger/toe/spine helpers, reverse directions and numeric native skin/pose/contact tests.

## Phase L3 — First real non-Brawl adapter and game-neutral scene viewer

Choose the adapter with the clearest legal input and verifiable native output, e.g., Melee DAT with HSD tooling or Ultimate SSBH assets, based on available fixtures/writer readiness. Test *import native model + animations → source association → preview on original rig → Brawl target retarget + native CHR0 export* and independent reverse target-native export **only where a verified writer exists**. Do not mislabel a one-way adapter bidirectional. Add 64-bit sidecar only if real dependency requires it.

## Phase L4 — Multiple adapters, target slot packs, batch misc and timebase styles

Broaden native game model/motion/channels and read/write matrix: Melee, Ultimate, Rivals II Modkit and Dragon Ball asset sources, plus additional games. Add verified target-game animation slots, multi-part and variant assignments, sophisticated speed/step/FighterZ hold controls, fixed-duration reference fitting and Fill Missing Miscellaneous. Preserve every target format's timebase, morph/material channels and body/contact requirements with explicit channel loss reports.

## Phase L5 — One complete fighter across multiple native targets

One `.fpstudio` Goku can reuse mixed game animations on a master rig and maintain independent Brawl/Melee/Ultimate/Rivals II target models and slot branches. Export and fresh-open separate game-native animation sets; inspect real Modkit import for Rivals II. Later author gameplay/scripts/buildable mods and quick deployment independently.

# 12. Tests and edge cases

- **Source fidelity:** original model/motion file SHA-256 pre/post, read-only source and restricted stage; no output path overlaps original or game install.
- **Library consistency:** duplicate display names, renamed source, same model names across games, two costumes same/different rigs, model+animations in separate archives, resource names repeated in nested containers, library restarts, offline/relocated mount, replaced bytes and changed native versions.
- **Retarget:** Mario→Snake and Snake→Mario, short→long/long→short limbs, different rest orientations/parents/axes, missing bones, helpers, fingers/toes, blended skin, unsupported scales, joints and singularity, planted/air phases, root motion, full turns and withheld fractional samples.
- **Correct channels:** skeletal only success is labelled skeletal only; mixed morph/material/visibility tracks tested per adapter; missing channel refuses falsely “complete animation” status.
- **Temporal:** source FPS, rational seconds, stepped FighterZ-like holds, loop seams, speed 0.5x/2x and time-warp, protected action keys and fixed-duration margin changes.
- **Native validation:** target native output decoded fresh with independent pose/mesh/contact measurements and untouched unrelated resources where the adapter promises it. A correct preview is not enough.
- **UI:** real native Model Previewer, one target visible, no extra source model rendered as fighter, searchable library, source compare, Use on This Model, progress/cancel, Undo, save/reopen, second project independence, preserved original BrawlCrate menus/timeline.
- **Legal/security:** no assets, decrypted titles, private paths or captures committed to Git; no source downloads, keys, EULA acceptance or global installers.

# 13. Research references — candidates, not integrated capabilities

- Upstream native foundation: https://github.com/soopercool101/BrawlCrate
- V2 working repo: https://github.com/DoctorFlux/fighterport-studio-next
- GameCube/Wii ISO/RVZ virtual filesystem: https://github.com/encounter/nod and https://github.com/dolphin-emu/dolphin
- Melee DAT editor/backend: https://github.com/Ploaj/HSDLib ; Melee expansion/project reference: https://github.com/Ploaj/MexManager and https://github.com/akaneia/m-ex
- Ultimate viewer/native editor: https://github.com/ScanMountGoat/ssbh_editor ; native format core: https://github.com/ultimate-research/ssbh_lib ; `data.arc` explorer: https://github.com/ScanMountGoat/ArcExplorer
- Rivals II official Modkit authoring: https://rivals2.com/workshop/knowledge-base/character-creation/create-your-character-mod/ and https://rivals2.com/workshop/knowledge-base/character-creation/import-into-unreal/ and https://rivals2.com/workshop/knowledge-base/character-creation/animation-overview/
- Unreal title input candidates: https://github.com/FabianFG/CUE4Parse and https://github.com/4sval/FModel ; Sparking-specific modding reference: https://github.com/WistfulHopes/SparkingZERO_ModProject
- Xenoverse: https://github.com/DarioSamo/LibXenoverse ; https://github.com/Olganix/LibXenoverse2 ; https://github.com/LazyBone152/XenoKit ; https://github.com/LazyBone152/XV2-Tools
- Rig/IK conceptual reference: https://github.com/godotengine/godot (4.6 source + documented humanoid/IK behavior)

Before integrating third-party code, check its **actual current API, license, complete dependency tree, build architecture, version compatibility and native output evidence**. e.g., `FModel` GPL versus `CUE4Parse` Apache-2; LibXenoverse license may require additional investigation. Merely placing a tool in the source directory does not prove it is integrated or legally redistributable.

---

# 14. Instructions to integrate this document

1. Read V2 `AGENTS.md`, `docs/MASTER_PLAN.md`, `docs/DEVELOPMENT_STATUS.md`, `docs/FORMAT_SUPPORT.md`, `docs/RETARGETING.md`, `docs/TESTING.md` and `docs/plans/current.md` plus the present document. Re-inspect actual HEAD.
2. Place this exact specification at `docs/V2_SOURCE_BOUND_UNIVERSAL_ANIMATION_LIBRARY.md`; add a short link and scope amendment to the master plan. Avoid copying legacy V1 Authoring/PreviewWorkspace architecture. Do not make a second task queue.
3. **Set exactly one next executable task in `docs/plans/current.md`: L0 real source-bound Brawl animation library and direct source-model reuse in the existing viewer.** Preserve the already passing Brawl/native-retarget tests. Complete and publish that runnable vertical slice before L1/L2/L3.
4. Build and test with current repository scripts. Run real GUI model+separate motion PAC, `Use on This Model`, two independently saved fighter projects, native export/reopen, source hash and restart checks. Distinguish manual reviewer approval, source inference and actual verified render/native/game support.
5. Update `docs/FORMAT_SUPPORT.md` and status to actual measured evidence and unresolved tasks; do not claim all games imported/exported until independently tested. Review source-only diffs and push to exact `DoctorFlux/fighterport-studio-next` main under V2's existing publication authorization. Do not push assets, binaries or local paths.

**The guiding UX acceptance is: one click to choose a library animation, no repeated donor skeleton/model dialogs when a verified source link exists, one click to retarget to the currently displayed model, and a verifiable native result.**

---

*This specification is an addendum to the V2 master product vision, not a rewrite of the proven V2 application's working source. New games, channels and output writers remain declared future capabilities until independently validated.*
