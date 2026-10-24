# FIGHTERPORT STUDIO V2: NEXT SOURCE-BOUND ANIMATION LIBRARY MILESTONE

**Repository:** https://github.com/DoctorFlux/fighterport-studio-next  
**Inspected remote main:** `7decc0cf094f6f1dd4c5f0abff325b791704e017` on 2026-10-18. **Re-read actual HEAD before doing anything.**  
**Attached requirements document:** `FighterPort_V2_Source_Bound_Universal_Animation_Library_Master_Plan.md`  
**Current scope:** Build a small, genuinely working shared Brawl source-bound animation library on top of the **existing V2 application**, while retaining every other game's import/export and auto-rig/IK/FK goals as permanent requirements. 

## Mission

Continue V2 from actual current HEAD. Do not restart, merge the retired FighterPort repository, or overwrite V2's tested Brawl native editor/retargeting/viewport. This V2 is an independent pristine-BrawlCrate-derived repo and already has a working native `.fpstudio` fighter workflow and selected Brawl retargeting.

Read all of `AGENTS.md`, `docs/MASTER_PLAN.md`, `docs/plans/current.md`, `docs/DEVELOPMENT_STATUS.md`, `docs/FORMAT_SUPPORT.md`, `docs/RETARGETING.md` and `docs/TESTING.md`. Then copy the attached specification into `docs/V2_SOURCE_BOUND_UNIVERSAL_ANIMATION_LIBRARY.md`, link it from the master plan and use it as an authoritative scope addendum. Do not overwrite the existing master plan or its implemented evidence. `docs/plans/current.md` remains the ONLY next executable task queue.

### Mandatory permanent product requirements

1. A **single global animation library** is shared by ALL `.fpstudio` fighter projects. Every animation knows its **exact native source model/skeleton**, and exact source model + animation game/mount/container/resource identities, hashes, timebase, channel set, revision and association evidence. Never store unbound donor clips as silently auto-retargetable.
2. Register full **game folders, model/motion packages, disc images/ROMs, game installs, overlays and Modkit projects** as read-only source mounts, progressively using actual tested adapters. Discover many clips per character automatically. It should later support Brawl/Project M/P+, Melee, Ultimate, Rivals II, FighterZ, Sparking! ZERO and Xenoverse 1/2. GameCube/Wii ISO/RVZ can use verified `nod` functionality; Ultimate ARC and Unreal require different adapters; no imaginary universal ISO reader or DRM/key bypass.
3. Automatically associate model and animation when native references or tested game-layout manifests prove ownership. If several source models are possible, ask ONCE and persist hash/fingerprint-bound approval. A costume/alternate rig is not assumed equivalent merely because names match.
4. The real BrawlCrate Model Previewer is the editing target. One **Library** list/search UI should let the user preview an animation on its original source model and click **Use on This Model**, using its persisted source model automatically. Preserve active target, source-versus-target comparison and correct native document identity.
5. IK and FK **each work as independent authoring tools**, and both participate in the retargeting process. Provide simple auto-rig on previously VERIFIED known skeletons and a short correction workflow for uncertain mappings. Same source and target rigs need not match names, bone counts, joint axes, rest poses or body proportions. Do not claim untested universal perfection.
6. Future game-neutral model/animation/channels and per-game native writer interfaces must support many model/motion formats without MDL0/CHR0 as obligatory intermediate. Read/write/preview/evaluate/bake/native-reopen have separate capability statuses. Keep x86 BrawlCrate runtime stable; use isolated x64 workers when genuinely needed.
7. Target animation-set slots, 1..N parts, dedicated ground/air/special variants, reference Idle/Fall and other states, per-game verified native bindings, fixed-duration transitions, FighterZ-style stepped timing and automatic Fill Missing Miscellaneous remain permanent next phases. Gameplay scripting and in-game testing remain lower priority.

## Immediate executable checkpoint L0 (implement now)

### A. Source association and global library

- Inspect current V2 `BrawlCrate/Studio/FighterProject.cs`, `BrawlCrate/UI/ModelForm.FighterStudio.cs`, `BrawlCrate/UI/ModelForm.Retarget.cs` and existing resource traversal. `FighterProject` currently owns fighter-local animation assets; `ChooseFighterRetarget` prompts for a donor model. Do not build a duplicate UI or import V1's complicated CharacterProject/Library store.
- Design a minimal durable **global** `SourceMount` + `SourceModel` + `SourceAnimation` catalogue, with immutable hash-pinned source references and a small bounded index. Source identity MUST include exact native nested resource path/index and model/skeleton identity. Storage must survive process restart and support thousands of clips with lazy loading and cancellation. Do not embed the complete library in every `.fpstudio` or create a huge system of empty classes.
- Support user-selected Brawl **model PAC/BRRES and separate motion PAC/BRRES/CHR0**. Associate all appropriate selected package clips with the chosen verified model. Distinguish identical names in different nested archives, different rigs with matching bone names and separately selected body/costume rigs. If pairing is ambiguous, show compact choices, not silent inference.
- Provide `Add Source...` and `Add Whole Character Motion Set to Library`, with atomic index update/Undo or cancellation safety and progress. Source files are strictly read-only; no output into their folder when it risks overwriting originals.
- Show `Available`, `Missing`, `Source changed`, `Needs model association`, `Unsupported channel/version` where appropriate. A source missing after restart retains index metadata; clicking it should offer Repair/Relocate Source with hash/identity validation.

### B. Visible one-click use on current model

- Add a single non-bloated **Animation Library** panel/filter to the REAL native Model Previewer (and optional entry from normal resource browser). Search by fighter, source game, clip, tags. Visible source-model identity must be shown in clip details.
- A source preview uses its own native original rig. Never replace the current fighter's editable model, and never append all source models into the active rendered fighter. Side-by-side preview can follow later if needed.
- **Use on This Model** takes an exact library clip ID and resolves the hash-pinned source model/rig automatically, then invokes the EXISTING V2 `Retarget to This Model` service as appropriate. No repeated Choose Source Model dialog for verified library clips.
- New target-native animation is stored as an independent editable project revision, with provenance including library clip ID/revision, source model and rig fingerprint, retarget mapping/revision, target rig identity, channel/timebase and export receipt. Keep manual one-off source model selection as a fallback for unresolved sources.
- Do not overpromise animation formats: implement tested **Brawl CHR0 skeletal** route in L0; other native channels/other games remain explicitly pending until real readers/writers work.

### C. Run both character directions and saved projects

- Use the user's existing locally configured Brawl Mario/Snake model and motion PACs, if available; inspect/determine their actual paths and resource contents without assuming filename conventions. Native test fixtures must be original and public-safe; real commercial files and private paths must stay ignored.
- Register a source Mario model + its selected motion PAC and a source Snake model + its motion PAC. Check expected available clip metadata against actual native resources; the V2 status previously observed 297 Mario and 355 Snake motions, but inventory rather than hardcoding those totals.
- Open a target Snake fighter and **use a Mario library clip**: preview original Mario, then retarget onto Snake with its body proportions and test native Brawl animation save/reopen. Repeat Snake → Mario independently. Source original hashes and unrelated target resources must remain unchanged.
- Open two separately saved fighters that use the SAME immutable library clip. Edit one target. Save, exit, restart the real V2 application, re-open both and confirm original/library revision/other target remain unchanged. Retargeted clips must play/scrub on their intended target.
- Test malformed, stale, unsupported, missing and ambiguous source links; do not commit incorrect partial library entries or target revisions.
- Run current build/doctor/native/GUI/retarget/regression tests. Real UI tests must click the displayed library clip and play it; calling a setter in a harness is not sufficient. Capture source/source-preview/target/native-output identity. Keep all existing locomotion/contact/native thresholds.

### D. Update master architecture and next task

- Preserve V2 actual current status/format matrix and its already passing tests. Add per-game/per-channel/per-operation capability rows to `docs/FORMAT_SUPPORT.md`: Brawl tested subset versus future Melee/Ultimate/Rivals/Dragon Ball routes. Do not claim an exporter just because the adapter can parse an asset.
- Update `docs/plans/current.md` to L0 while implementing it; after acceptance, advance the ONE authoritative task to GameCube/Wii selective ISO/RVZ or known-rig auto-profile reuse based on the next highest-value supported vertical slice. The full master plan keeps all requested games and formats.
- Review staged and outgoing source-only diffs. Check exact GitHub origin `DoctorFlux/fighterport-studio-next`, owner, expected public visibility and actual commit. Push the coherent passing source update using the V2 repo's recorded authorization, never the retired project's remote. No force push, proprietary inputs, derived assets, private paths, settings, generated binaries, screenshots or unauthorized releases.

## Definition of done (do not mark L0 passing without all)

- Two source model+motion pairs registered read-only; native source association durable across restart.
- Global library independent of fighters; index/search shows real clips/source rigs; no repeated donor-model chooser for library entries.
- A first-time user can open target model → search donor clip → preview on donor → Use on This Model → see correctly retargeted target pose → save a separate Brawl-native output with fresh decode/reopen.
- Both Mario→Snake and Snake→Mario tested, with proportion/IK quality **reported separately** from native serialization accuracy; unchanged source hashes and original native target skeleton/weights.
- A second fighter using the same source remains independent; invalid/missing/ambiguous source cases fail visibly; existing tested Brawl editor/retarget paths continue passing.
- Document exactly what is implemented, what remains unsupported, and the single concrete next task; publish only passing reviewed source.

**The key optimization is for the user, not the architecture: `select a library animation → use it on my current model`; the software automatically remembers which original source model belongs to the animation.**
