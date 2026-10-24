# Fighterport

This is the public unified source repository at https://github.com/DoctorFlux/Fighterport.
It imports the BrawlCrate-based Next preview.14 source and integrated owned engine;
the current implementation is **preview.16-workspace-flow**.
The two predecessor repositories are private preserved histories. New commits use
the DoctorFlux identity without Codex author, committer or coauthor attribution;
upstream copyrights, licenses and notices remain intact.

Read the [full product plan](docs/MASTER_PLAN.md),
[one current M2 task](docs/plans/current.md), and
[source-import record](docs/SOURCE_IMPORT_2026-10-24.md).
The supplied workspace image guided the first simpler-workspace increment. The
native app and executable name are retained; broader game adapters and universal
contact quality remain unfinished.


Fighterport now opens with **Source -> Target -> Preview and fix -> Export**
around the complete dark BrawlCrate Model Previewer and existing animation engine.
Choose a source-bound library clip, open the target model, preview the transfer,
edit with the native tools, then export the selected CHR0 or save the project.
Contextual **Continue** actions and visible readiness explain the next step.
**New project**, **Open project** and **More tools** are visible commands;
target/project opening accepts a pasted path or the standard **Browse** dialog.
Invalid paths are explained inline before loading. Source registration resets
pairing approval when its inputs change and selects the committed clip. Source
**Repair** checks the exact indexed identity, supports cancellation and updates
the library atomically after validation. Untouched empty projects start clean.
The source library collapses, **Focus editor** restores editing room, and
**More tools** retains the expert commands. Original resources/animation lists,
the Bones panel, camera/materials, transform tools, autokeys, interpolation,
timeline and Undo remain together in the original native control.

Fresh preview.16 verification passes **257 scoped checks** with **43 actually
inspected PNGs**. The
[workspace-flow acceptance record](docs/WORKSPACE_FLOW_ACCEPTANCE_2026-10-24.md)
records actual commands, outcomes and limits. Historical preview.15 passed
**163 scoped checks** with **25 inspected PNGs**, as recorded in
[its workspace receipt](docs/SIMPLE_WORKSPACE_ACCEPTANCE_2026-10-24.md); those
results do not qualify preview.16. Broader M2/G1 usability, physical input/DPI and
game acceptance remain incomplete. Original Next v7 is unchanged; contact-context
work and the later body-bone/protected-motion-control policy remain queued.

Use **Animation Controls** to set up an existing rig, add pins to selected bones,
soft-pin hands/feet, follow an existing animation, or create an animation from a
model-only pose. The controls window exposes FK hierarchy, reviewed IK chains and pins; original bone picking can select FK controls. Pick pin diamonds directly in the preview. Native Move, Rotate
and Scale tools commit supported edits to animation keys, with native Undo/Redo,
project persistence and CHR0 output. Controls use the original axis/ring/scale gizmos, transform fields and dark interpolation editor. Native key pairs and their tangents survive supported edits.
Creating the first animation from a model-only pose keeps its existing FK/soft
pins and saved pose; no donor animation is required.
Review IK Chains and Shared Bones exposes existing-joint bindings, pole directions,
custom roles and explicit unassignment, with saved setup and native Undo/Redo.
The Pole controls group exposes magenta viewport diamonds: use Move to change
bend direction and save existing joint animation keys. Curves opens the authored
middle-joint track; rotate/scale apply to FK, IK effectors and pins.

Retargeting uses the original Next/BrawlCrate-fork solver and its options,
Skeleton Mapping, progress and details windows. Auto, Grounded, Airborne and
Source contacts bake to native keys. Integrated pin edits can refine the result
and save as CHR0 or a project. Untransferred source tracks appear in the report.
Previously saved imported-engine drafts retain their original validation reports.
Native reopen is separate from motion quality, game packaging and game testing.
**Workspace** opens character/shared/target management through the retained Core storage. Save independent target copies with their native clips and control state; earlier native and compatible standalone projects remain readable. Expert fitting/layer interfaces and other game adapters retain their separate roadmap and acceptance gates.
The source-bound Brawl library and existing native resource editors remain.
Library **Batch / Fit** prepares selected clips with shared/per-row output frame
counts and source trims, shows full channel-loss reports, and adds explicitly
reviewed independent results. Timing fits are non-looping and save their original
source reference plus recipe. Original Next remains the retargeter.

Library **Advanced Fitting...** opens source/target scene and body review with
the same native model renderer, materials, shaders and lighting as the main
previewer. Review floor/ledge, reach, overlap and timing settings, attach an exact
Core companion context, compare original Next, and save review settings with
Undo/Redo. Advanced drafts retain unresolved gates and cannot be applied;
uncalibrated body preference knobs remain disabled. The ordinary Next retarget
and pin/curve saving workflow remains available.

**More tools > Fit / Transitions...** fits selected entry/exit FK curves inside the
existing animation duration, using reviewed same-rig references, protected
middle frames and the same native model renderer. Review and add an independent
result with native Undo/Redo. **Fighter > Transition Fit Details / Frozen
References** shows saved settings, later edits and exact exportable reference
CHR0s. Pin editing and interpolation remain available on fitted results.
**Preserve existing contact trajectories** protects original support skin/bone
paths while fitting unprotected bones; the result lists excluded paths. This
preserves existing support trajectories without inferring new anchors/releases.

**Fighter > Animation Slots / Assign to State...** manages custom required or
optional slots, exact native clip assignments, locks and Undo/Redo. Changed clip
content or models require review. Native projects and Core character workspace
snapshots retain assignments independently per target. These are custom editor
intentions. Load Motion File Profile derives a source-bound Brawl native slot
inventory from an authentic registered motion file. Assign and Export Native
Slot CHR0 writes a new animation using the template slot name/version after exact
fresh verification. Runtime build/action requirements and Fill Missing remain
unverified.

**Fighter > Contact Placement / Release...** prepares reviewed existing-chain
bone contacts offline: approach, hold a moved/rotated world anchor, then return
to the current motion, or release into the saved original motion. Inspect the
native source/result views before adding an independent animation with Undo/Redo.
The native/project result keeps its contact recipe and original reference; pin
and interpolation edits continue to save as native keys. Automatic surface/state
transitions remain separate work. Renderer window/DC lifetime cleanup is verified
through both Mario/Snake retarget and repeated-modal painting sequences.

Build with `scripts/Build.ps1 -DotNet <path-to-dotnet-10>` on Windows x64.
Launch with `scripts/Run.ps1`; use `-NativeDirectory <new-folder>` to select a
separate build, or `-Check` to print the executable selected without launching it.
The retained executable is `FighterPortStudioV2.exe`.
See [integration and verification](docs/ENGINE_INTEGRATION.md),
[workspace-flow evidence](docs/WORKSPACE_FLOW_ACCEPTANCE_2026-10-24.md),
[historical preview.15 workspace evidence](docs/SIMPLE_WORKSPACE_ACCEPTANCE_2026-10-24.md),
[historical contact authoring, native profiles and painting evidence](docs/CONTACT_AUTHORING_ACCEPTANCE_2026-10-24.md),
[earlier contact preservation and custom slots](docs/CONTACT_SLOTS_ACCEPTANCE_2026-10-24.md),
[historical boundary fitting and Mario/Snake save evidence](docs/TRANSITION_UI_ACCEPTANCE_2026-10-24.md),
[historical scene fitting and Mario/Snake persistence evidence](docs/SCENE_FITTING_ACCEPTANCE_2026-10-24.md),
[historical control-review qualification](docs/CONTROL_REVIEW_ACCEPTANCE_2026-10-23.md),
[historical native controls/interpolation/workspace evidence](docs/NATIVE_CONTROLS_WORKSPACE_ACCEPTANCE_2026-10-23.md),
[historical two-direction retarget/pin evidence](docs/NEXT_RETARGET_ACCEPTANCE_2026-10-23.md),
[the sole current plan](docs/plans/current.md),
[the full product plan](docs/MASTER_PLAN.md) and
[per-operation support](docs/FORMAT_SUPPORT.md).
Generated builds, fixtures, private projects/settings and captures stay local.

The standalone history is preserved at
[DoctorFlux/fighterport-studio](https://github.com/DoctorFlux/fighterport-studio).
The original Next README is retained as historical evidence under
[docs/engine](docs/engine/NEXT_README_HISTORY.md); its earlier retarget acceptance
claims do not establish current general motion-quality or game acceptance.

Native source pin: `d56cbac89dc2cd0e5c3f923e1d03041b16902add` from
[BrawlCrate](https://github.com/soopercool101/BrawlCrate).
Preserve [LGPL](LICENSE), [component notices](THIRD_PARTY_NOTICES.md),
[engine provenance](docs/ENGINE_PROVENANCE.md) and
[the upstream README](docs/UPSTREAM_README.md).
