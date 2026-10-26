# Fighterport

This is the public unified source repository at https://github.com/DoctorFlux/Fighterport.
It imports the BrawlCrate-based Next preview.14 source and integrated owned engine;
the current implementation is **preview.38-direct-game-sources**.
See the [source/fit and saved-source acceptance](docs/SOURCE_FIT_WORKFLOW_ACCEPTANCE_2026-10-26.md).
The two predecessor repositories are private preserved histories.
Upstream copyrights, licenses and notices remain intact.

Read the [full product plan](docs/MASTER_PLAN.md),
[one current M2 task](docs/plans/current.md), and
[source-import record](docs/SOURCE_IMPORT_2026-10-24.md).
The supplied workspace image guided the first simpler-workspace increment. The
native app and executable name are retained; broader game adapters and universal
contact quality remain unfinished.


Fighterport opens in the familiar **Studio Next** layout: the complete dark
BrawlCrate Model Previewer edits the target, and the source pane sits on the right
with a small read-only original model preview. The original resources, animation
lists, Bones panel, camera/material tools, transforms, interpolation, timeline and
native Undo stay together. The stage/task guide is optional under **View**.

Open the target with **Open Model...**. Use **Add Animations...** for its native
idle/fall references when fitting needs them. **Load source...** chooses an original
model and motion set for this session; loading, previewing and retargeting never
add those files to the shared library. **Add to library** is a separate explicit,
verified operation. **Add game...** accepts saved game sources, disc images, and
game/install/SD folders. Brawl ISO/WBFS and Brawl-family extracted/SD builds can be
indexed now; other games/formats can have their locations saved for future readers.
Fighterport reads ISO/WBFS directly: no separate reader, emulator or key-file setup
is needed. Add the disc once to index its complete fighter roster, animations and costumes
in the shared library; there is no character picker. Game assets stay in the original
image. Indexing reads one temporary package at a time, and preview/retargeting reads
only the selected native model/costume and motion packages. Temporary native leases
are released when closed. Install/SD folders are read directly. The library keeps
compact listing metadata and preferences, with available-memory/disk guards instead
of a fixed total-animation ceiling. It retains no second extracted fighter tree.
For Project M or Project+, choose that game/mod and its SD root or build folder.
Brawl and each mod installation have separate categories, even with identical
character names. **Saved games / paths...** lists every saved source in a compact
editor: rename instances, edit locations, remove an entry or undo the last change.
Multiple Project M installations keep independent source IDs and editable names.
An indexed path change verifies the same original bytes before updating its links.
Removal unregisters library entries and preserves files and saved target projects.
Choose **Open saved library** to reuse an indexed source without extracting it again.
**Animation → Manage library sources...** retains source management. Existing
indexed disc sources and saved provenance stay readable without their historical
reader executable. Other games need their own adapters; paths alone are not support.
See the [direct-source acceptance](docs/DIRECT_GAME_SOURCES_ACCEPTANCE_2026-10-26.md),
[integrated reader record](docs/INTEGRATED_DISC_READER.md) and
[background acceptance](docs/INTEGRATED_DISC_ACCEPTANCE_2026-10-26.md).

Choose a character and animation directly in the Brawl library, then its **Costume**.
**Load as source** sends that exact model and animation to the small original
preview. **Open as target** opens an independent editable
target using the existing unsaved-change decision; the original remains read-only.
**Use on This Model** instead retargets to the current target. Costume options come
from the detected native Brawl source/build and exact compatible hierarchy/binds.
Different binds require review; no model or skeleton is silently substituted.
A recorded comparison keeps its original costume even when library browsing changes.
The pinned source preview, transport, transfer and **Fit start / end...** actions
stay visible while the catalogue scrolls. The target result is an independent
editable native animation. In fitting, enable **Fit start**, **Fit end**, or both;
select exact target poses and join frames. Switching a side off retains its pose
and settings. With both sides off, keep the retargeted animation directly. Preview
and review an enabled fit, then **Apply to current animation** to keep the fit on
the same retargeted clip with native Undo. **Save** keeps those changes in the
project; **Animation → Export verified animation...** writes the fitted CHR0.
**Save as a new animation** is optional for an independent revision. Multi-clip
native containers use that copy route to keep fitting recipes unambiguous.

The **Animation** menu also provides review results, recorded-source comparison,
loop inspection, controls, source timing/batches and verified set export. Native
expert tools remain in the toolbar overflow and Fighter menu. Library **Filters**
shows advanced criteria and their active count; hiding them retains their values.
Search, Favorites only and Clear filters remain available. Existing performance
invalidation guards and the original Next retargeter remain intact.

Temporary source catalogues belong to the current window and are discarded with
it. Opening a source as an editable target carries its bounded source metadata to
the replacement window without registering it in the shared library. Target
animations save normally. In-session comparison retains exact
source identity across library switches; after reopening a project, unavailable
temporary source provenance is explained without blocking native target editing.
Register sources manually when they should become part of the shared library.

**Fighter → Open Fighter...** includes the ten most recent saved/opened project paths. Choose a
path, then Open; **Forget selected** and **Clear history** only remove history.
Missing projects can be located with a pasted path or Browse. History stays local
and separate from project files. Project Save now preserves exact edited native
curve values/tangents and keeps the original curve/keyframe panels connected.

**View → Text size** offers 100/125/150/175% workspace text. The 200% option is
removed; a saved 200% preference reopens at 100%. Native editor fonts and process
DPI remain unchanged. **View → Focus target editor** closes competing panes and
recovers the complete original viewport. **View → Workflow guide** opens the
optional stage/task controls. Project and unsaved-change dialogs also scale.
Physical/per-monitor DPI support is not qualified by application text sizing.

Use **F6 / Shift+F6** between visible regions and **Tab / Shift+Tab** within outer
panels. Opening source browsing focuses search; Escape closes a pane when focus
is inside it. Cancelled dialogs return focus to their invoking control, with
viewport fallback. Native editing shortcuts retain their existing routes. Compact
panes may overlay native side tools; Focus target editor recovers all tools.

**Animation → Review results...** keeps a session list of exact
target clips. Batch / Fit opens it after adding reviewed results. Add an existing
native CHR0 with **Add selected**, inspect it in the full editor, use Previous/Next
or linked comparison, then **Mark reviewed**. Edits and changed dependencies require
review again. Removing a row keeps the project animation. Native cross-clip
Undo/Redo retains its original history. Save keeps unchanged exact approval;
reopen starts a fresh review session. See the
[batch review acceptance record](docs/BATCH_REVIEW_ACCEPTANCE_2026-10-24.md).
**Animation → Export reviewed set...** freezes the approved clips and shows their
exact identities, frames, filenames and target content hash. Choose a **new folder**;
every precise CHR0 is independently decoded and compared before the complete folder
is published in one rename. A receipt records identities and verified hashes.
Duplicate clip names get distinct stable filenames. Cancellation or failed
validation publishes no set; existing folders are preserved. Export works before
project Save and retains native selection, frame and Undo. Single-clip export keeps
its existing overwrite confirmation. See the
[verified set acceptance record](docs/VERIFIED_SET_EXPORT_ACCEPTANCE_2026-10-24.md).
Preview.26 qualification passed **2,229 scoped assertions / 97 inspected PNGs**.

**Animation → Inspect loop / seam** reads the selected project-owned CHR0
against the current native target, including donor-free animations. Compare local
First → Last and stored-loop Closing → First differences, then read exact samples,
raw Euler turns and authored boundary values/tangents. Root travel and full turns
are observations. Reviewed bindings alone identify protected motion controls.
Opening and **Refresh** pause without seeking; **First**, **Last** and **Closing
frame** explicitly jump to native display frames 1, N and N+1. The stored clip loop
and preview-loop checkbox stay separate. Edits/Undo/selection changes clear stale
measurements until Refresh. Native playback and all editing tools remain intact.
Use Close, focused Escape or Focus editor to recover editor space. See the
[loop inspection acceptance record](docs/LOOP_INSPECTION_ACCEPTANCE_2026-10-25.md).
Fresh preview.27 qualification passes **2,606 scoped assertions / 115 inspected PNGs**.

**Fit start and end** can match a frozen first Wait/Fall pose without replacing
the attack's motion-position curves. **Fit reference controls; retain attack travel** fits
reviewed ancestor controls (including XRotN/YRotN equivalents and local height)
inside the chosen boundary windows, while the reviewed RootMotion/TransN keeps
its complete source animation. Exact reviewed roles are required; names alone
never enable this option. New dialogs enable it when those roles are available;
reopened older recipes retain their previous policy until explicitly changed.
Reference preview follows the same selected policy while scrubbing or playing.

Nearest-equivalent rotation fitting considers complete XYZ families, including
coupled 180-degree changes and whole 360-degree turns, at both entry and exit.
It preserves the reference orientation while avoiding manufactured Euler spins.
Disabling the option retains authored reference turn counts. The original action
outside the windows, reference clips, native Undo and original Next remain intact.

The simple fitting panel has **Fit start**, a start pose, **Attack reached at
frame**, **Fit end**, an end pose and **Recovery starts at frame**. **Use for both ends** reuses the exact
start reference. The live ranges show the unchanged attack and shared joins.
For 40 frames, choose attack frame 5 and recovery frame 26: start 1–5, original
attack 5–26, end 26–40 (15 inclusive samples). Duration stays 40 frames. References
use their first frozen pose by default. Preview, review the result, then add an
independent animation with normal native Undo. **Advanced** contains component
and bone masks, reference timing, protections, floor review and the full receipt.

**Adapt hips and legs in transitions** explicitly releases reviewed baked support
paths only inside enabled start/end windows on non-looping clips. The original
middle, source travel and contact provenance remain intact. This option is visible
in the simple panel and initially checked for clips with existing support paths;
preparation and adding still require explicit binding/pin and result/limit review.
Old exit-only settings/recipes keep their original meaning. **Prevent extra foot
dipping** remains an Advanced option requiring a reviewed plane and baseline.
It uses complete authentic foot geometry and the same bounded correction in each
window; it does not promise planted feet or absolute zero penetration. Pins,
protected intervals, unsupported geometry and work/lift contradictions reject.

See the [simple-boundary acceptance record](docs/SIMPLE_BOUNDARIES_ACCEPTANCE_2026-10-25.md)
for current commands, evidence and limits. The [boundary-control record](docs/BOUNDARY_CONTROLS_ACCEPTANCE_2026-10-25.md)
and earlier [foot-recovery record](docs/FOOT_RECOVERY_ACCEPTANCE_2026-10-25.md)
describe their exact historical policies. Physical input/human usability, universal
contact quality, monitor DPI and actual game acceptance remain unverified.

The source library now has **Favorite / Favorited**, **Favorites only**, and
combinable game/build, character/model, availability and channel filters alongside
text search. **Clear filters** resets the view while retaining favorites. Favorites
are local to each library and use exact clip identities, including duplicate names.
Filtering preserves the editable target and recorded comparison source. Preview.24
passes **1,793 scoped assertions / 63 reviewed PNGs**; see the
[library organization acceptance record](docs/LIBRARY_ORGANIZATION_ACCEPTANCE_2026-10-24.md).

**Animation → Compare recorded source** links a verified read-only original source
beside the complete editable target. Scrub/play the native target timeline to see
both, including recorded trims/timing fits and bind poses. Browser selection never
redirects comparison. **Focus editor** closes it. Missing provenance/source/timing
explains unavailability while the target remains editable. Fresh preview.23 passes
**1,701 scoped assertions / 49 reviewed PNGs**; see its
[comparison acceptance record](docs/LINKED_COMPARISON_ACCEPTANCE_2026-10-24.md).

**Animation → Export verified animation...** reviews the exact CHR0 selection and destination, stages
an exact snapshot, independently decodes all curves/metadata, then publishes
atomically. Existing destinations require confirmation; originals and working
files remain protected. Export works before project Save and keeps native Undo.
Fresh preview.22 qualification passes **1,644 scoped assertions / 43 reviewed PNGs**;
see the [verified-export record](docs/VERIFIED_EXPORT_ACCEPTANCE_2026-10-24.md).

Historical preview.21 background verification passes **2,398 scoped assertions**, with
**87 individually inspected PNGs**. Recent-project persistence/recovery, exact
native body Undo/save/reopen, both workflow directions and relevant workspace/root
regressions pass. See the [recent-project acceptance record](docs/WORKSPACE_RECENT_PROJECTS_ACCEPTANCE_2026-10-24.md).
Preview.20 retains its separate [workflow replay receipt](docs/WORKSPACE_WALKTHROUGH_ACCEPTANCE_2026-10-24.md).
The [human walkthrough](docs/WORKSPACE_USER_WALKTHROUGH_2026-10-24.md) remains
prepared, not performed; physical input/DPI and human outcomes remain unverified.
Preview.19 has its separate [keyboard/focus record](docs/WORKSPACE_KEYBOARD_ACCEPTANCE_2026-10-24.md)
and [DPI audit](docs/WORKSPACE_DPI_AUDIT_2026-10-24.md). Historical preview.18
has its separate [scaling receipt](docs/WORKSPACE_SCALING_ACCEPTANCE_2026-10-24.md).
Historical preview.17 has its separate [project and motion-control
record](docs/PROJECT_MOTION_CONTROLS_ACCEPTANCE_2026-10-24.md).
New/Open/Close share Save, Discard and Keep editing; failed/cancelled saves keep
the current document and Undo available. Untitled Save accepts a pasted path.
Original Next v7 remains the base with a separately recorded source-local control
policy: protected controls follow only their own source animation. Manual body
corrections retain that protection; boundary fitting has the explicit reviewed
reference-control exception described above. Names are examples; reviewed hierarchy
identifies equivalent controls. Prior preview.16/15 receipts remain historical.
M2/G1 human usability, physical input/DPI, broad contact quality and game acceptance
remain incomplete.

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
