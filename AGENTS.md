# Fighterport project bootstrap

## Read order and recovery

Before editing or claiming support, read this file, then
`docs/PROJECT_STATE.md`, `docs/plans/current.md`, and the relevant records in
`docs/FEATURE_STATUS.json`. Read the linked sections of
`docs/MASTER_PLAN.md`, subsystem contracts and dated acceptance records for
the task. Inspect actual Git HEAD, branch, status and diff; reconcile later
commits and uncommitted user work. The chat history and archived plans are
supporting evidence, not the current checkpoint.

At a context recovery or resume, reread the short state and current task
before changing files. State the accepted baseline, current task and its
acceptance boundary briefly, then continue the next unfinished action.

## Permanent product rules

- Work in this existing public `DoctorFlux/Fighterport` checkout. The two
  historical `fighterport-studio-next` and `fighterport-studio` repositories
  are private preserved histories; never push to them or replace this app.
- Only **Super Smash Bros. Melee, Brawl, Ultimate and Rivals of Aether 2** are
  destination games; all four must also become sources. BT3, FighterZ,
  Sparking ZERO, Zenkai Battle, Battle Stadium D.O.N, Budokai 3, Xenoverse,
  Naruto, One Piece, Jump Force and other approved donors are source-only.
  Donor-side viewing/editing/portable export does not authorize native
  game-writing to those titles.
- Preserve the complete BrawlCrate-based Model Previewer, native bone and
  animation editors, gizmos, interpolation, timeline, automatic keying,
  Transform All, camera/material tools and native Undo/Redo. Keep the original
  Next retargeter and one owning native document. Simplify the surrounding
  workflow without a competing editor or second Undo stack.
- Animation-only work never silently changes the target hierarchy, rest/bind
  transforms, inverse binds or skin weights. Preserve immutable source bytes,
  hashes, native identities, timebases, flags, interpolation and source-authored
  root/motion controls. Existing-rig FK/IK handles do not replace bones or
  reweight meshes. Model-port output is a separate derived asset.
- Use reviewed structural, axis, geometry and motion evidence. Names may
  suggest bindings but never authorize character/action-specific numerical
  exceptions. Keep frozen numeric and contact gates from the linked contracts.
  Correct motion and usable workflow precede aggressive native filesize work.
- No playback tick may load/hash/decode assets, bake, call the worker
  synchronously or run a full contact solver. Freeze dependencies for
  preparation; reject stale Apply/publication and leave documents unchanged
  after cancellation or failure. Preserve offline operation.
- Read/evaluate/preview/edit/retarget/write/repack/tool-import/game-runtime are
  separate capabilities for each title, platform, build, asset and channel.
  A saved path, parseable file or native reopen alone is not game acceptance.
  Mark physical input, human usability, monitor DPI and unrun game tests open.
- The asset-tool roadmap includes both port modes, libraries, contacts,
  transitions, animation sets, protected Fill Missing, models/materials/
  textures/morphs, portable interchange, native output and optimization.
  Gameplay/moveset scripting, hitbox/state-machine authoring and runtime
  mechanics remain deferred; preserve related native data.

## Work, evidence and publication

- Keep one bounded active task in `docs/plans/current.md`. At meaningful
  checkpoints and before ending, update affected feature records,
  `docs/PROJECT_STATE.md` and the current task with actual passes, failures,
  skipped checks, limitations and one exact next action. Preserve dated
  acceptance records; never rewrite an old pass as evidence for changed code.
  Use a tested revision or reproducible HEAD-plus-diff identity, never a file's
  own future commit hash.
- Use original procedural fixtures and independent numeric expectations.
  Background tests use private desktops and the shared offscreen renderer,
  serialized, paced and low priority with pause/cancel/resume. Follow
  `scripts/Test-BackgroundCapture.ps1`; inspect actual images. Never steal
  active focus, inject OS input or copy the user's screen. Record real commands,
  exact fixture availability, losses and failed approaches.
- Keep commercial game assets, derived assets, private paths/settings, secrets,
  caches, captures and generated binaries out of public Git. Retain upstream
  copyrights, component licenses, revisions and notices before selective reuse.
- Source-only publication to public `DoctorFlux/Fighterport/main` was
  previously authorized. Use verified DoctorFlux author **and** committer
  identity, no Codex coauthor/collaborator, and preserve genuine upstream
  authorship. Use `core.hooksPath=.githooks`; inspect outgoing commits,
  attribution/trailers, assets and contributor state before publishing.
  Publication authorization does not establish game acceptance or binary
  release permission.

Detailed approved requirements and their IDs live only in
`docs/MASTER_PLAN.md`; feature status lives only in
`docs/FEATURE_STATUS.json`. `docs/FORMAT_SUPPORT.md` is generated from that
ledger. Original long-form instructions and release narratives are preserved
under `docs/history/` for historical reference.
