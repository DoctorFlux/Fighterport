# FighterPort Studio V2 — FIRST EXECUTABLE TASK

**Only authoritative `docs/plans/current.md` for the NEW V2 repository.**  
**Do not use the retired FighterPort repository's `docs/plans/current.md`.**

## Goal: A clean BrawlCrate-derived editor that is pleasant to use in the FIRST 5 MINUTES

The first milestone should demonstrate this from an empty project:

**New Fighter → Open Model (no animation required) → Add Animation → select clip → Play / scrub model → Save fighter project → restart → restore correct model and animation → native Save As/reopen without modifying the source.**

### Preconditions

- The old FighterPort repo and entire workspace have been backed up and verified; nothing in the old repo has been changed.
- The new working directory has a clean, pinned upstream BrawlCrate/BrawlLib import with full license/credits, and original baseline build and GUI editor have been observed.
- The new Git history is independent, and the Git remote (if one is configured) is a NEW explicitly verified destination, never the previous project's origin.
- The old project architecture/custom UI is not imported.

### Required implementation

1. Create a **small** project format `*.fpstudio` supporting a named fighter, optional model, 0..N animation registrations, source path/reference, rig identity when available, selected clip, and one target game profile. Do not implement a generalized cloud/database/asset-service architecture. A simple durable bounded document plus controlled local staged assets suffices initially.
2. Extend the **real** BrawlCrate MainForm/ModelForm/ModelEditControl. Keep all upstream native viewer/edit features; add one clear new fighter/open/add animation route and a visible animation list next to the 3D model. Do not hide native playback, destroy resource tree, or add nested wizard and panel stacks. Read-only animation audition still works.
3. Handle separate model PAC/BRRES and motion PAC/BRRES/CHR0 where the inherited parser supports them, with explicit model and clip association. Show *actual discovered clip names* and time/loop status; if unsupported, explain precisely why. Avoid fake 'the files are loaded' UI when the list is empty.
4. `Play`, `Pause`, `Stop`, frame scrub, changing clip, and returning to model rest pose must visibly work and agree with native animation sampling. Model selection remains stable when a separate motion file is opened. If the native API cannot directly apply a separate clip, implement a safe transient association in memory; do not overwrite original files or require a legacy companion manifest.
5. Save a small fighter project, exit, open the actual executable again, and reopen it. Model choice, animation names, association and play state/selected frame policy should restore predictably. Report missing/replaced source files instead of silently substituting an asset.
6. Save a **new native animation/resource output** where actual upstream BrawlCrate support permits it; verify by independently decoding, reopening and scrubbing it. Preserve all input file hashes.
7. Keep one simple canonical project/previewer owner; no global 'last window', no copying legacy leases/manifests. Provide safe unsaved-change handling, a basic Undo strategy appropriate to actual edits, and cancellation/error messages.
8. Add focused source-independent original fixtures and automated native + GUI tests. Demonstrate the actual visual interface showing a recognized clip changing the pose, not just programmatically calling a CHR0 setter. Run retained upstream checks and store local build/test results.

### Deliberately NOT in this first task

- Full modern retargeting; old Mario/Snake H3 contact fix; old FK code; full IK handles; imported old UI panels; .fpcharacter/.fpcompanion migration; automatic game/ROM extraction; Melee/Ultimate/Rivals/Dragon Ball adapters; native gameplay scripting or in-game builds; giant tool installers; plugin marketplace. These are permanent MASTER_PLAN requirements with later milestones, not fake first-task deliverables.

### Acceptance

- A first-time user can find and play an animation on a visible selected character in a single window, without understanding internal Brawl file hierarchy.
- A blank model-only fighter project is valid.
- Two distinct motion selections display distinct expected poses, both before and after restart.
- Native source protection and output save/reopen work.
- At least one observed native GUI regression from inherited BrawlCrate remains passing.
- Failure modes are explicit and measured; no source inputs/game assets or binaries committed.
- GitHub publication on the NEW remote happens only after confirming ownership, visibility and explicit authorization for the NEW destination.

### Next task after passing

Implement an opt-in simple `Retarget to This Model` feature against two original different-rig humanoid fixtures using an independently designed, format-neutral rig profile and FK transform transfer, with only ambiguous mappings reviewed, Brawl-native output, fresh decode and source hash preservation. The old project's Mario/Snake test is a *future quality reference*, not a dependency or code import.
