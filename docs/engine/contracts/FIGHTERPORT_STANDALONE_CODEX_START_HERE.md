> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

> Historical bootstrap: this repository already exists. Continue through AGENTS.md, docs/MASTER_PLAN.md, docs/AUTOMATIC_RETARGETING.md and the ONE docs/plans/current.md. Do not recreate the workspace, restart M0/M1 or overwrite the amended master with the older handoff.

# FighterPort Standalone — Copy/Paste Codex Kickoff

**Date:** 18 October 2026  
**Use with:** `FIGHTERPORT_STANDALONE_EXISTING_RIG_MASTER_PLAN.md`  
**Authority:** The uploaded original standalone handoff is included in the revised master file, with the user's new existing-skeleton-control requirement governing conflicting wording.  
**This is a new product, not the BrawlCrate-derived V1 or V2 application.**

## Paste the following into the NEW Codex workspace

```text
Build FIGHTERPORT as a NEW, STANDALONE, game-engine-style desktop studio
for authoring complete platform-fighter characters.

READ THE ATTACHED FILE:
FIGHTERPORT_STANDALONE_EXISTING_RIG_MASTER_PLAN.md

Persist it as docs/MASTER_PLAN.md in the new repository and use its
updated EXISTING-SKELETON IK/FK rule as the governing user decision.
Read any other user-provided standalone handoff, but do not let old
BrawlCrate-based or skeleton-generation language override this decision.

ABSOLUTE REPOSITORY BOUNDARY
- Use a distinct new local directory/repository outside the existing
  DoctorFlux/fighterport-studio, DoctorFlux/fighterport-studio-next and
  soopercool101/BrawlCrate application trees.
- DO NOT fork, clone as the application foundation, copy the whole UI
  shell, or extend any of those editor applications.
- Study their source read-only for validated native data formats,
  native evaluation conventions and regression cases.
- Reuse selected open-source components only after documenting exact
  upstream commit, copyright, license, notices, dependencies and fit.
- Do not use Godot as the application engine or embedded editor.
- Old-repository publication authorization does not transfer to a new
  remote. Do not create/publish/alter remote visibility without the
  user's explicit approval for this new repository.

PRODUCT DEFINITION
The studio owns its own desktop UI, GPU viewport, scene, fighter
projects, asset library, animation clock, key editing, commands,
undo/redo, rig-control overlays, IK/FK solvers, retarget recipes,
target slot schemas, native validation and game build orchestration.

One fighter is ONE master project with multiple independent output
targets: Brawl/PM/P+, Melee, Ultimate, Rivals II. Further Smash targets
are planned. Dragon Ball FighterZ, Sparking! ZERO, Xenoverse 1/2 are
important donor model/animation source families. Each reader/writer,
version, repack and actual game-test capability is independently proven.

GOVERNING IK/FK CORRECTION — MOST IMPORTANT
We are NOT building new deform skeletons for the imported game models.
Those models ALREADY HAVE BONES, hierarchy, bind transforms, skin weights
and game-specific skeleton requirements.

'Set Up IK/FK' means AUTOMATICALLY ATTACH EDITOR-SIDE ANIMATION CONTROLS
TO THE EXISTING BONES. Do not alter/replace the deform skeleton, add
mandatory game bones, reweight the mesh or normalize native bind poses.
- FK: select a bone/chain, rotate/pose/key it directly on the existing rig.
- IK: add editor-only hand/foot goals, elbow/knee pole handles, optional
  pelvis/chest/head/prop controls; solve to existing bones and bake keys.
- Retarget: a SEPARATE action that maps source skeleton motion to the
  selected target's skeleton, with proportion-aware FK transport and
  optional IK/contact corrections. All three operations must work alone
  or together. The same IK gizmos can edit an animation native to its rig.
- The source game model, target game model, rig profiles and control
  overlays must have separate identities and never be mistaken for one.
- Do not require a character to have an imported motion to set up IK/FK.
- Completely unrigged static models may be previewed; skeleton creation
  or auto-weight painting is OUTSIDE the current core scope.

AUTOMATIC KNOWN-RIG WORKFLOW
- For EXACT previously verified game/build + skeleton fingerprints,
  restore the proven map and control layout with one click.
- For common Brawl/Melee/Ultimate/Rivals/Dragon Ball bone names,
  suggest anatomical chains using names PLUS hierarchy, rest axes,
  bone lengths, left/right symmetry and bind/skin evidence.
- Map head/neck, chest/spine/waist/pelvis, clavicles/shoulders,
  arms/elbows/hands/fingers, legs/knees/feet/toes, and optional twist,
  helpers, face and prop attachments.
- Only display unresolved/ambiguous mappings for review. Never call a
  name-only guess verified. Save edits and reuse per exact rig revision.
- A partially characterized rig should still support useful controls.

GLOBAL SOURCE-BOUND ANIMATION LIBRARY
- Register game installs, native fighter model/motion folders, supported
  GameCube/Wii images/dumps, mod overlays, Ultimate data.arc/dumps,
  Rivals II Modkit/source projects, and Xenoverse/Unreal source data
  as individual tested providers, adding adapters incrementally.
- Every library clip retains its AUTHENTIC source model and skeleton,
  native resource/container path+index, hashes, game/build, coordinate
  interpretation, timing, channels and source approval evidence.
- Store source registrations and immutable revisions across all fighters.
  Selectively extract/copy into bounded protected caches; source mounts
  remain read-only and portable/relinkable by verified identity.
- User flow: choose a TARGET model already in the viewport -> search
  library -> preview motion on original source model -> Use on This
  Fighter -> automatic reviewed mapping/FK retarget/optional IK ->
  editable target revision -> target-native output when implemented.
- Unsupported extra animation channels are explicitly reported, not
  silently discarded. No CHR0/MDL0 universal intermediate.

ANIMATION EDITOR AND PLAYBACK
- ONE document, scene, clock, selection, curve evaluator, and undo history
  drives the normal editor, model viewer, rig controls and exporters.
- Large, readable viewport with native bone/mesh deformation, timeline,
  clips, numeric fields, transform gizmos, key insertion/deletion/copy,
  local/world modes, Play/Pause, fractional scrub, fixed camera, FPS and
  source/target comparison.
- Do not fake deformation using moving skeleton lines over a static mesh.
- Solver/baking jobs are background/cancellable and NOT run each live
  preview frame. Measured displayed smoothness matters more than internal FPS.
- Preserve source timing, FighterZ-style stepped/held poses, key curves,
  protected gameplay-event positions, full-turn winding and independent
  playback speed and duration policies.

REFERENCE-POSE FITTING
Implement explicit 'Fit Start From' and 'Fit End Into' with quick Idle
and Fall presets, but any compatible target-rig clip/pose/phase supported.
Work within first/last N EXISTING frames by default, preserving the total
frame count and protected action/event ranges.
A 24-frame move may use frames 1-4 to fit from Idle, preserve 5-18,
and use 19-24 to connect to Fall, with frame-12 hit intact.
Sample moving reference segments, match endpoint orientations AND
velocities where appropriate, preserve root motion, winding, and
relevant contact releases/holds. Define the actual runtime boundary
sample and avoid duplicate last/first frame holds.
Bake to editable keys. Validate all four joins, native fractional
reopen, deterministic random scrubbing, and infeasible-constraint errors.
For Rivals II, never depend on runtime IK/control-rig transitions.

FULL FIGHTER PROJECT
Provide versioned game/build-specific animation-slot manifests with
exact target binding evidence, required/optional/fallback states and
multipart/ground-air/loop variants. Include movement, aerials, normals,
specials, ledge, hurt, get-up/tech, items, grabs/throws, taunts/results
and game-specific states. User-made extra animations stay unbound until
real target identifiers exist.

'Fill Missing' uses donor pack tags, approved rig maps, target references,
IK/contact/transition fitting, no overwrite of custom assignments,
reviewable batch previews and atomic Undo.

Eventually include models/materials/costumes/recolors, native gameplay
code, hitboxes/hurtboxes, sound/VFX, CSP/battle portraits/stock icons,
UI assets, complete target packaging, install and quick game playtest.
Gameplay programming and deployment are later than the core animation
and rig-control workflows but must have reserved project/schema support.
A motion alone is not a playable move.

TECH STACK AND SCOPE CONTROL
Use C# .NET 10, Windows x64, Avalonia as the proposed baseline. Prove a
GPU-textured, skinned interactive viewport with a measured spike before
choosing the final graphics backend. No Godot app; no WinForms hosting.
Keep the initial solution limited to Core, Desktop, Rendering, Brawl
adapter/worker, CLI and Tests until clear boundaries require more.
One clean repository, one AGENTS.md, docs/MASTER_PLAN.md and
ONE docs/plans/current.md execution task. AGENTS.md must link to the
other two and make this existing-skeleton rule permanent.

IMMEDIATE EXECUTION — DO THE CODE, NOT JUST THE DOCUMENTS
M0: create/run original skinned synthetic humanoid with nonzero rest/bind
transforms, actual GPU skinning, animation browser/clock/timeline, visible
playback, FK bone gizmo/numeric pose edits, keying, Undo/Redo and save/reopen.
Demonstrate a valid existing rig and unchanged deform-skeleton identity.

M1: use the first audited Brawl native reader/writer boundary; open a
separate native model source and native CHR0/motion source in the same
viewport, associate the authentic source rig, edit actual skeletal keys,
write NEW native animation output, freshly reopen and independently
verify sampled bone poses, skinned geometry, timing and source hashes.

Next: automatic IK/FK control rig overlays on TWO original different
already-skinned humanoids, including known-name and renamed-bone cases.
Prove FK and IK edits alone and together, stable knee/elbow poles and
native bake. Then global source library, cross-rig reuse and fitting.
Prioritize a simple user-facing 'Set Up Animation Controls' command
before advanced mapping forms. Build real game adapters one at a time.

Do not claim every Smash, Rivals or Dragon Ball model/animation format
is implemented until each specific reader/evaluator/writer works and
has independent fixtures. No universal arbitrary-rig guarantee.

TEST AND REPORT
Run actual build and GUI, source-independent procedural fixtures,
separate-process project reopen, Undo/Redo, target-only preview,
native save/reopen, fractional/interpolation checks and input hashes.
Test both direction retargeting when implemented; avoid relaxing tests
to make a failure pass. Preserve game assets and paths locally, exclude
source dumps/builds/credentials from the new public Git tree.
State exactly what was implemented, what was actually run, current
blocked formats, and the next single executable task. Do not stop
at architecture scaffolding or empty buttons.
```

## Meaning of the rig distinction

- **Imported deform rig:** A game model already owns bones, hierarchy, binds and weights. **Preserve these.**
- **Automatic FK/IK control setup:** Add virtual editing handles to existing bones and save the configuration; make no replacement deform skeleton.
- **Animation retargeting:** Convert source rig motions to a different target rig, optionally using those FK/IK controls for corrections.
- **Native export:** Bake supported transforms and track data onto the target game's existing bones, then fresh-decode for verification.

The first deliverable is intentionally narrow. This document defines the entire product, while the immediate execution stays M0→M1 and the first actual existing-rig IK/FK control milestone.
