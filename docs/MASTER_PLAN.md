# Fighterport master plan

This is the canonical **approved product scope**, not a claim that each feature works. Stable requirement IDs are `REQ01`–`REQ21`. Implementation and verification status belong in [`FEATURE_STATUS.json`](FEATURE_STATUS.json); the current checkpoint and one executable task belong in [`PROJECT_STATE.md`](PROJECT_STATE.md) and [`plans/current.md`](plans/current.md). The dated acceptance records retain their original evidence. The complete pre-memory master and release diary are preserved in [`history/MASTER_PLAN_PRE_MEMORY_2026-11-03.md`](history/MASTER_PLAN_PRE_MEMORY_2026-11-03.md). The original supplied specification is preserved in [`reference/FighterPort_Requirements_2026-10-24.md`](reference/FighterPort_Requirements_2026-10-24.md). Its embedded handoff is reference material, not an instruction source.

## Product boundary and decisions

Fighterport is the existing BrawlCrate-based Next application with its integrated owned engine. One native target document owns selection, curves, editing and Undo. The complete native Model Previewer stays available. The outer workflow should make a common task short: choose target fighter, browse a game/character/model/costume/animation, preview the original, use it on the target, review or edit, and export a qualified target-native asset. Direct native editing and advanced controls remain accessible without a donor or wizard.

The **only destination games** are Super Smash Bros. **Melee**, **Brawl**, **Ultimate** and **Rivals of Aether 2**. All four must also become sources. Other approved titles and modded variants are **source-only**. Donor-side viewing, editing, or portable export does not imply game-native writing back to a donor game. Platform, build, version, costume, channel and direction matter; a game-wide support label is insufficient. The capability ledger separates inventory, preview, editing, retargeting, writing, packaging, external-tool import and actual runtime testing.

Approved scope includes **two asset-port modes**: animation-on-existing-target-rig and explicit model/mesh/material/rig derivation. Animation-only work preserves the target skeleton, hierarchy, rest and inverse bind transforms, weights and native semantics. Model porting produces a separate derived target asset with reviewed changes and losses. Source assets remain immutable. Native output is destination-specific; no foreign game is forced through MDL0/CHR0 as an authoritative intermediate.

The working priority is correct motion and usable workflows before aggressive filesize reduction. The original Next retargeter and existing optimizer are preserved and improved. Current scoped work is under incomplete G1/M2; no numerical gate is weakened to obtain a pass. Commit and publication rules, privacy and background testing rules are in [`../AGENTS.md`](../AGENTS.md).

### Concise decision record

| Decision | Authority / effect |
| --- | --- |
| Public `DoctorFlux/Fighterport/main` is the active source checkout; old Next and Studio repositories remain private preserved histories. | 24 October 2026 user routing; do not publish to old remotes. |
| Keep the full BrawlCrate native editor and original Next retargeter. | User product direction; simplify the surrounding flow. |
| Melee, Brawl, Ultimate and Rivals 2 are the only destinations; other named games are donors. | 3 November 2026 explicit scope; supersedes broader destination suggestions in older references. |
| Use target-owned native formats, a shared format-neutral rig/motion bridge and modular adapters. | Cross-game authoring direction; each writer still needs its own qualification. |
| Retarget correctness precedes further aggressive CHR0/Melee size work. | 3 November 2026 quality-first instruction; retain optimizer evidence. |
| A semantic head may use a reviewed skin-backed point when a distinct head joint is absent. Map each physical joint once; verify split/merged neck and shoulder chains in both directions before claiming them. | 3 November 2026 anatomy clarification; quills and similar appendages cannot become head joints by proximity or naming alone. |
| Keep background tests separate from human and game-runtime acceptance. | Repeated user choice; no desktop input injection or focus stealing. |
| Asset authoring is the product; gameplay/moveset logic is deferred. | Original specification and later narrowing; preserve native data without claiming authoring. |

## Approved requirements

Each requirement remains approved even if its implementation is partial or absent. `FEATURE_STATUS.json` links each ID to concrete features and receipts; archived material preserves detailed historical evolution. The acceptance examples `A01`–`A21` in the original specification remain test goals, not completed passes.

### REQ01 — existing application and provenance

Use the existing full BrawlCrate-based Next application, native Model Previewer and integrated owned engine. Preserve original Next retarget behavior as a callable route, native format readers/writers that are already qualified, resource lists, material/texture controls, camera and rendering tools, licenses, notices and genuine upstream credit. Do not rewrite the app as a new editor. Native `net472/x86` UI and isolated `.NET 10/x64` engine services retain their established boundary; the native UI owns the editable document and Undo. Maintain source provenance and a source-only public history without game assets or generated binaries.

### REQ02 — both port modes

Support (1) retargeting donor animation onto a selected existing fighter and (2) building a derived fighter model/rig/material asset for a selected destination. Keep these operations explicit. Model porting may need skeleton mapping, mesh/weight/material conversion and review; those changes never sneak into animation-only retargeting. Preserve the original target and allow independent destination revisions.

### REQ03 — four source and destination games

Provide complete, separately qualified source and destination routes for Melee, Brawl, Ultimate and Rivals 2. Identify title, platform, build/version, native asset and channel for each operation. Sources include relevant installed/disc/modpack/Modkit/overlay forms where reviewed. Destination writing includes native animation/model/container outputs only when their own read, save/reopen, target-tool and runtime gates pass. Brawl/Project M/Project+ variations need distinct build and overlay identity, not name-only association. A path, parse, preview or build alone is not full game support.

For Brawl, distinguish PAC, PCS, BRRES, MDL0 and CHR0, and independently track VIS0, SRT0, PAT0, CLR0, SHP0 and preserved SCN0. Keep vanilla, BrawlEX, Project M, Project+, Project M Remix and REX identities separate. For Melee, distinguish HSD DAT joint/skin/material data, AnimJoint versus Figatree, FOBJ fractional time and vanilla versus m-ex/MexManager packaging. Ultimate needs its own SSBH model/skeleton/material/animation/texture and archive routes. Rivals 2 needs the real versioned UE 5.8 Modkit skeleton, baked animation/FBX and content dependencies; an FBX file alone is not a usable Workshop package. Each format, channel and build remains unqualified until its own evidence exists.

### REQ04 — general retargeted motion

Retarget with general structural, geometry, axis and motion algorithms across unfamiliar compatible rigs and proportions. No character, action, filename, frame-index or hash-specific numerical exceptions. Preserve meaningful action timing, arcs, style, holds, spins, discontinuities, local source-authored motion controls and contact intent. Use original Next where appropriate, with frozen inputs, explicit losses, cancellable offline preparation, dependency checks before Apply/export and a complete native output validation chain. Same-rig preservation, unequal-rig quality, contacts and actual game playback are distinct gates. The detailed numeric contract remains in [`AUTOMATIC_RETARGETING.md`](engine/AUTOMATIC_RETARGETING.md).

### REQ05 — native data preservation

Keep original bytes/hashes, exact model/clip/container identities, hierarchy, rest/bind/inverse bind transforms, helpers, all represented skin weights, native timebase/interpolation/flags/loop/terminal behavior and opaque data. Source resources are immutable. Independent result revisions, native snapshots, transaction/Undo, atomic publication, fresh reopen and precise loss reporting protect authored work. Do not materialize absent channels or substitute identity/rest values without proving native semantics. Project save stays exact and non-destructive; export-copy optimization is separate.

### REQ06 — offline work and recoverability

Supported routine browsing, preview, editing, retargeting, fitting, saving and exporting work without a required cloud service. Bound memory/work, lazy-load catalogues, avoid heavy playback work, allow cancellation, and retain the last successful document/build after failure. Use versioned migrations and safe corruption handling. Optional DCC or external verification tools are never an unexplained runtime dependency.

### REQ07 — shared source library and association

Register loose models/motions, packages, extracted folders, discs/ISOs/WBFS, installs and ordered mod overlays through read-only modular providers; later formats need their own readers. Allow saved locations, renaming/removal, exact repair, temporary sources, lazy inventory, bounded asynchronous search, filters/favorites/collections, batch use and explicit source preview. A clip remains linked to its authentic source model, costume/form/build, hashes, original container/member and timing/channels. Duplicate names do not merge identities. Ambiguous associations require review; missing/changed sources are visible and cannot silently transfer. The global catalogue is separate from each fighter project; deleting a donor registration does not rewrite saved target results.

Keep versioned new/open/save/save-as/duplicate/import/from-current-model/recent/model-only/Add Target project flows, legacy inline-read compatibility, exact target/clip/frame resume and independent target branches. Store source identities, reviewed rigs/maps, calibration, owned clips, manual/timing/contact/fitting layers, model/material/outfit variants, native slots and export receipts. Shared donors and semantic intent may be reused, while target body coordinates and edits remain independent; show inherited/overridden layers and protect manual corrections during regeneration. Durable project data is separate from caches, generated builds and the global library.

### REQ08 — complete native editor and controls

Retain BrawlCrate model/resources/animation/bone lists, rendering/shaders/materials/camera, bone picking, gizmos and transform fields, automatic key creation, native interpolation/curves, timeline/playback/frame tools, Transform All, Undo/Redo and native Save/reopen. Keep a single editable target and independent read-only source preview. Existing-rig FK and IK setup must work without a donor. Editing a current imported/retargeted/fitted animation remains native, with one Undo transaction for each Apply. Non-Brawl adapters should use the same viewer interactions while retaining authoritative native documents rather than pretending foreign content is CHR0.

### REQ09 — complete models and channels

Support the model as more than a skeleton: meshes, materials, textures, morph/non-skeletal animation, attachments, costumes/forms, props, object visibility and companion channels where native formats carry them. Preserve unedited data and report unsupported channels instead of silently dropping them. Model-only export and animation-only export are distinct. Portable DAE/glTF/GLB/FBX interchange must handle rigged model, texture/material and selected animation combinations, including separate animation files and batch options; each importer/writer needs round-trip and external-tool evidence. Direct in-app FBX writing is the approved direction, with no mandatory Blender runtime dependency. Game-native model writing is separately qualified.

Carry normals/tangents, UVs/colors/palettes, native visibility, attachment and scale semantics. Report shading losses such as transparency/culling, UV orientation/addressing, tangent basis and unsupported shaders; retain the mirrored/flipped Brawl Mario texture regression. A target profile must enforce its actual bone/influence/topology/material/texture/animation-size/package limits without inventing one global cap. A posed static mesh does not qualify as a rigged source. Outfits/forms are mesh/material/attachment variants; motions are shared only when exact rig compatibility and per-mesh body/contact calibration permit it. Preserve prop trajectories, hand switches and two-hand grips where present.

### REQ10 — meaningful bones and mappings

Cover root/pelvis/waist, variable spine, head/face, clavicle/arm/twist/hand/fingers, legs/heels/toes, helper/corrective/attachment, tail/ear/wing/extra limbs as present. Distinguish a missing joint, an unanimated channel and an authored rest-valued channel. Structural hierarchy, authentic bind/rest, axes, geometry, skinning and symmetry are binding evidence; names suggest but cannot solely authorize a mapping. Reviewed skin-backed semantic points may represent anatomy without a separate joint, while each physical joint receives motion once. Split and merged shoulder or cranial chains require reviewed composition, not duplicated transforms. Reviewed profiles store identities, axes and limits, not hidden numerical overrides. Unmatched content uses explicit retain/rest/follow/manual policies; unknown semantics preserve data and surface losses.

### REQ11 — independent FK posing

Attach editor-only FK handles to existing target bones; do not replace the skeleton or reweight. Pose existing bones with the native gizmos/fields, automatic keys, interpolation, timeline, selection, Undo and Save. FK remains usable independently of IK and a donor and can combine with both. Preserve parent/child and scale-compensation semantics.

### REQ12 — independent IK editing

Attach reviewed editor-only IK targets, pins and poles to compatible existing chains. Allow independent IK placement and native bake into existing bone channels with Undo and save/reopen. Preserve authored support coordinates and disclose unreachable or ambiguous chains. Do not quietly solve through protected ancestors or create a second document owner.

### REQ13 — body proportions and anatomical variation

Use measurements of the actual source and target, joint axes, limb lengths, body volumes, support geometry and skin influences to adapt motion between unequal bodies. Account for hands, feet, body clearance and extra/missing segments, not height or leg length alone. Preserve target binds and weights in animation-only work. Show inability to satisfy contradictory constraints rather than lowering gates or assigning character-specific offsets.

### REQ14 — contacts and environment context

Support reviewed ground, foot patch, hand, ledge, held object, support and release contacts with exact establishment/hold/release intervals, support geometry, anchors and protected dependency paths. Keep fixed contacts in support coordinates where feasible; permit explicit reviewed steps/recovery ranges for incompatible joins. Preserve root motion and all skin influences. Missing floor/ledge/prop/other-actor or moving-support context is unavailable, not invented. Solve coupled simultaneous contacts, report unreachable/conflicting constraints and keep independent held-out and native reopened checks. Detailed acceptance bounds and existing unresolved failures remain in [`AUTOMATIC_RETARGETING.md`](engine/AUTOMATIC_RETARGETING.md) and dated records.

### REQ15 — timing, style and motion continuity

Retain exact source timing by default and support reviewed trims, target duration, per-row fit, style/cadence constraints, loops, transitions, holds and variable-speed choices. Preserve native Euler winding/full turns, source-owned roots, interpolation and terminal samples. Distinguish action identity/style, timing/continuity, clearance and contacts as separate gates. Do not silently inject an idle or change gameplay meaning.

### REQ16 — fixed-duration entry/exit fitting and correction layers

Allow independently toggled start and end fitting to selected target-owned reference poses, including only a target Wait1/Fall first frame when requested. Keep the protected attack middle, authored root/TransN ownership and reviewed contacts; orientation controls follow their own source/reference semantics. Let users choose boundary ranges and inspect/apply to the current native clip or an explicit new revision with one Undo. Preserve recipe, reference identity, dependency hashes and exact joins; report overlap, non-convergence and missing context. Generic rotation/IK/FK recovery must respect all body proportions without a named-character patch.

### REQ17 — native animation sets and slots

Represent target-owned animation sets, variations and native slot identities/rules. Review batches, current clips, timing/losses, comparison, approvals and independent imported revisions. Existing game-specific slot inventories need exact build/profile provenance, required/optional flags and locks. Export reviewed sets atomically, verify each native candidate and give receipts; a matching name alone is not proof of runtime slot semantics. Keep native animation and companion channels distinguished.

Where the destination profile actually defines them, include movement/crouch, jumps/fall/landing, jabs/tilts/smashes/dash attacks, aerials, specials, defense, grabs/throws, hurt/tumble, knockdown/tech/get-up, ledge/wall, item/prop, entry/respawn/taunt/result and form/stance/extra-anatomy families. Keep one-to-many parts, start/hold/loop/release and ground/air/character variants. Separate custom, locked, missing, stale, unbound, fallback, gameplay-dependent, native-validated and game-tested states. The profile, not this taxonomy, determines actual required slots and fallbacks.

### REQ18 — protected Fill Missing

Fill only explicitly approved missing target slots or channels, preserving every locked/manual/accepted target animation and its provenance. Show proposed source/recipe, timing and loss before publication; reject mismatched native slot rules and stale dependencies. Never mass-overwrite an existing target set or substitute a fallback merely to satisfy a count.

Select donor packs/categories against actual missing native requirements, present ambiguous and failed rows, reuse reviewed setup and apply approved rows through one Undo. Preserve extra unbound authoring; never silently replace custom attacks, specials, taunts or results. Version local original/reference packs, tags, profiles and recipes; network sharing is optional.

### REQ19 — size and performance optimization

Keep editable high-quality masters and fitting/contact metadata separate from export copies. Optimize the **finished target animation**, never the unretargeted donor as quality reference. Preserve root/contacts/protected ranges, native flags/timing/turns/weights, and compare decoded native output against the master and existing absolute gates. Provide exact-only and explicit error-bounded policies, independent real-byte accounting, legal codec search, cumulative hierarchy/skin/contact checks, cancellation and safe fallback. Do not claim mathematical global minimum or game compatibility from native reopen. Preserve the existing CHR0 optimizer; later improve Brawl and Melee native filesize through geometry-aware adaptive curves/codecs. Performance includes startup/catalogue scale, playback responsiveness, memory bounds and recovery, with no solver/file work on playback ticks. Detailed CHR0 work and historical receipts remain in [`CHR0_BINARY_FORMAT.md`](CHR0_BINARY_FORMAT.md) and dated optimization records.

### REQ20 — simple workspace and usability

Use the familiar native target previewer and a compact source pane, source/target/preview-and-fix/export progression, focused contextual actions and accessible advanced controls. Avoid bloated repeated prompts. Support source character/model/costume/action selection, original preview, Use on Target, fit, review/edit, native export and direct no-donor editing. Keep project/unsaved-change handling, recent paths, keyboard/focus recovery, compact/wide layouts and supported text sizing. Verify real first-use/repeat/batch/direct-edit tasks, command discovery and wrong-document errors; background captures are not human input acceptance. Do not reintroduce the rejected 200% layout as a product requirement.

### REQ21 — asset-tool scope and future donors

The product authors fighter assets, not gameplay code. Support diverse source-only game families including BT3, FighterZ, Sparking ZERO, Zenkai Battle, Battle Stadium D.O.N, Budokai 3, Xenoverse, Naruto, One Piece and Jump Force through modular authentic readers when individually qualified. BT3 forms, costumes and damage variants remain distinct. Historical Goku reference content includes multiple outfits, forms, props and mixed donors as a generic multi-asset demonstration, never a numerical special case. Preserve related native metadata. Moveset/PSA/REL scripting, hitbox/state machine authoring, scripted meters and new runtime mechanics are deferred rather than silently discarded.

The reference-content set includes Teen/end-of-DB with Power Pole, Saiyan/Namek gi, Yardrat, Cell/Buu gi, Saiyan armor, base jacket, Whis gi, End of Z, adult GT and Xeno outfits, plus Super Saiyan, SSJ3, SSJ4, God, Blue, UI Sign and mastered UI appearance variants. These test generic costume/form/material/attachment and multi-target handling; runtime transformation activation is deferred. Xenoverse 1 and 2, Naruto Ultimate Ninja and Storm, and selected mods require separately identified title/build evidence even if parsers are shared.

## Cross-cutting acceptance contract

For each bounded increment, freeze source and target identities, exact member/hash, rigs/binds/weights, mapping, profile, geometry/context, recipe, numeric bounds, writer and output destination. Test original and final native evaluation at integer, quarter and withheld fractional frames; authored keys, seam/terminal data, random scrub order and relevant contact boundaries. Use independent numeric expectations and full-world/skin/contact witnesses, not a viewer screenshot alone. Compare same-rig results to exact native motion and unequal-rig results to the solved full-quality target plus absolute contact gates. Fresh native serialize/reopen/re-save is necessary but not sufficient. Actual target-tool import, physical input/human usability and game runtime are separate recorded gates. Preserve known failures and never convert a partial pass into universal acceptance.

Protect source bytes, target rig/binds/weights, sibling clips, target document/Undo and approvals across cancellation, failed publication, stale dependencies, Save and reopen. Background tests use private desktops/shared offscreen rendering, serialized and paced, with images actually inspected. Commercial fixtures and captures remain local and ignored. Record exact commands, revision, fixture hashes/availability, passes, failures, not-run checks and next step in dated evidence. The ledger links evidence without rewriting it.

## Proposals, deferred work and exclusions

**Approved but not yet qualified** work remains in `REQ01`–`REQ21` and the ledger; lack of an adapter or runtime result does not remove the requirement. Candidate implementation details from old handoffs or research are **proposals** until reviewed; they may guide experiments but do not supersede this master or fixed acceptance gates. Examples include a particular solver formulation, codec layout, UI placement, importer library, extra donor title or extra destination title. Extra destination games are excluded under the four-target decision unless the user changes it explicitly.

**Deferred** gameplay/moveset authoring includes PSA/REL/hitboxes, scripted meters, state machines and runtime mechanics. Model creation from scratch, automatic skinning, arbitrary unsupported anatomy and moving-platform/other-actor contact inference without authentic context are outside current bounded increments. They must not be represented as supported by an animation result. Later donor adapters and native writers remain separate feature records, with no blanket claim from portable interchange.

### Ledger meaning

`NOT_STARTED` means no implementation is claimed; `RESEARCH` means investigation only; `IN_PROGRESS` means partial implementation or qualification; `IMPLEMENTED_UNVERIFIED` means code exists without the required evidence; `VERIFIED_BOUNDED` means accepted only for the named fixtures, operation and gates; `BLOCKED` names a concrete unresolved prerequisite; `REGRESSED` means an earlier bounded pass now fails; `DEFERRED` means outside the active work queue. `PASS_HISTORICAL` is a committed earlier receipt, not a rerun; `PASS_CURRENT`, `FAIL`, `NOT_RUN`, `UNAVAILABLE` and `NOT_APPLICABLE` are distinct. Numerical/native-reopen, visual, human input, external-tool import and game runtime are separate verification axes. No aggregate is complete while required children remain unfinished.

## Traceability and historical detail

The approved IDs `REQ01`–`REQ21` map to original acceptance groups as follows. These IDs must not disappear without an explicit supersession or approved retirement in the ledger.

| IDs | Original acceptance groups / detailed source |
| --- | --- |
| REQ01, REQ08, REQ20 | A01, A02, A08, A19, A21; native editor/workspace/first-use. |
| REQ02, REQ03, REQ09, REQ21 | A09, A11, A12, A16, A17, A18; source/destination, model and scope. |
| REQ04, REQ10, REQ13, REQ14 | A05, A06, A07, A14; mapping, anatomy, contacts and general motion. |
| REQ05, REQ06, REQ07 | A03, A04, A09, A10, A12, A20; preservation, offline library, reliability. |
| REQ11, REQ12 | A02, A08; independent FK/IK and native controls. |
| REQ15, REQ16, REQ17, REQ18, REQ19 | A13, A14, A15, A16; fitting, sets, protected filling and optimization. |

Detailed contracts: [`ENGINE_INTEGRATION.md`](ENGINE_INTEGRATION.md), [`AUTOMATIC_RETARGETING.md`](engine/AUTOMATIC_RETARGETING.md), [`CHR0_BINARY_FORMAT.md`](CHR0_BINARY_FORMAT.md), [`reference/FighterPort_Requirements_2026-10-24.md`](reference/FighterPort_Requirements_2026-10-24.md), preserved [`history/`](history/), and dated acceptance records. These are supporting detail and evidence, not competing current plans.
