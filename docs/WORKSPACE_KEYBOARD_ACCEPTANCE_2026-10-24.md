# Workspace keyboard and focus — 24 October 2026

Preview.19 (`2.0.0-preview.19-workspace-keyboard`) continues PUBLIC
DoctorFlux/Fighterport from `906660b359938c6f896f798da2b96719e5db5568`.
**2,172 scoped assertions passed; all 55 final PNGs were opened and inspected.**
These are fresh runs against the final app, not accumulated historical totals.
The bounded keyboard/focus increment is accepted; G1/M2 remains incomplete.

## Behavior and ownership

F6/Shift+F6 moves between project commands, workspace actions, stages, visible
library/tasks and the original native editor. Hidden/disabled regions are skipped.
Each region remembers valid focus, including the native frame spinner's owning
control rather than its unselectable internal edit child. Missing/disposed
invokers fall back to the native viewport.

Outer Tab/Shift+Tab follows declared order. Tasks start at their primary action,
then summaries/details; library order starts Search, Clear and the source tree.
Focused actions scroll into view and workspace buttons draw a focus rectangle.
Standard button/dialog handlers retain Space/Enter activation. Opening the library
focuses search; Focus editor focuses the original viewport; stage/Workflow restores
tasks and focuses their first enabled action. Tooltips/accessibility descriptions
explain region navigation.

Escape closes a pane only while focus is inside it and the workspace is not busy.
Modal dialogs, open menus and native gesture cancellation retain their existing
routes. Cancelling project/unsaved dialogs restores the invoking command/field;
cancelling a nested Save path restores the unsaved decision's Save button after
it is re-enabled. Initial unsaved focus is Keep editing. Nothing implicitly
discards changes. No new global typing/editing shortcuts are introduced.

When a docked library and scaled tasks would together cover the target, tasks
yield their space; selecting a stage or Workflow returns to tasks. Compact
drawers still overlay portions of the native control. Close them or use Focus
editor to expose all native lists, transforms and timeline. F6 navigation itself
does not dismiss panels. Long tree captions still use scrolling/tooltips.

The COMPLETE original Model Previewer remains the authoring control with its
menus, resources, viewport/materials/camera, animation/bone lists, native fields,
gizmos, curves, timeline and Undo. There are no project/format/schema changes.
BrawlLib, engine, native Model Previewer implementation and retargeter sources
are unchanged. Original Next v7 and `source-local-controls-v1` remain intact:
protected controls follow only their own source-authored local animation;
inherited parent motion cannot authorize child keys. Fitting/manual corrections
remain on body bones. Skeletons, binds, helpers, weights and originals are preserved.

## Final evidence

Private supplied Brawl fixtures are used locally, never redistributed. Each
keyboard/scaling case uses its rig's authentic model/motion; transfer is Mario
Wait1 onto Snake. Numeric cases use authentic rigs and independent procedural
source animations. Ignored local evidence is under `artifacts/viewer-parity/`.

| Final suffix (`Fighterport-workspace19-`) | Assertions | Inspected PNGs |
| --- | ---: | ---: |
| keyboard-mario-r6 | 162 | 7 |
| keyboard-snake-r3 | 162 | 7 |
| scaling-mario-r3 | 410 | 11 |
| scaling-snake-r3 | 410 | 11 |
| workspace-mario-snake-r1 | 61 | 11 |
| lifecycle-snake-r1 | 37 | 6 |
| dialogs-r1 | 2 | 2 |
| motion-mario-snake-r1 | 464 | 0 |
| motion-snake-mario-r1 | 464 | 0 |
| Total | 2172 | 55 |

Keyboard cases call the actual application command-key methods with explicit key
data and standard control key/dialog handlers. They verify forward/reverse cycles,
remembered commands/native field, hosted project-name Tab, task/library Tab and
scrolling, disabled/hidden regions, drawer invocation/recovery, modal owner
isolation, Enter/Space/Escape, nested Save and disposed-invoker fallback. The
four layout cases per rig are 1050x650/1600x950 at 100/200% workspace text.
First-use source registration, reuse without another association dialog and
donor-free native work are exercised. Exact document/clip/frame 3/Undo/native keys,
protected controls and original/target model hashes remain unchanged by navigation.

Scaling repeats all five text presets and compact/wide layouts. The retained
workspace regression exercises independent original Next bake parity, native
body pin/field/curve editing and Undo, playback without baking, export/project
reopen, stale dependencies and cancellation. Lifecycle covers real cancelled and
locked-manifest failed saves, New/Open/Close and native body edit/reopen. Scaled
dialog error text is a layout-only fixture; actual failure evidence comes from
the separate lifecycle run. Bidirectional motion tests retain independent local
track, quarter/withheld sample, inherited-motion and rename/permutation checks.

All 14 keyboard, 22 scaling, 11 workspace, six lifecycle and two dialog screenshots
were individually inspected, including actual original source/target models,
read-only audition, focused/scrolling tasks, native curves and real save failure.
Captures remain ignored local game diagnostics. No source/game screenshots or
generated assets are published.

## Builds and reproduction

Full native/engine packaging passed in `fighterport-workspace19-build-r2.log`.
The final native app and controls harness passed MSBuild in
`fighterport-workspace19-harness-r7.log`. The unchanged numeric harness built in
`fighterport-workspace19-retarget-harness-r1.log` and loaded the final app assembly.
Engine SDK was 10.0.402; native app/harness were net472/x86 with VS2022 MSBuild.
No full engine-suite pass is claimed. Use absolute SDK/output paths because the
engine build changes its working directory.

With private fixture paths in variables and a fresh `$Capture` for every call:

```powershell
./scripts/Build.ps1 -DotNet $DotNet10 -NativeDirectory $Native
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
& $MSBuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
./scripts/Test-BackgroundCapture.ps1 -Model $Model -Motion $Motion -OutputDirectory $Capture -WorkspaceKeyboard -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $Model -Motion $Motion -OutputDirectory $Capture -WorkspaceScaling -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $MarioMotion -RetargetSource $MarioModel -Clip Wait1 -OutputDirectory $Capture -SimpleWorkspace -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $SnakeMotion -OutputDirectory $Capture -ProjectLifecycle -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $MarioModel -Motion $MarioMotion -OutputDirectory $Capture -WorkspaceDialogs -NativeDirectory $Native
& "$Native/RetargetAcceptance.exe" --motion-controls $MarioModel $SnakeModel $Capture
& "$Native/RetargetAcceptance.exe" --motion-controls $SnakeModel $MarioModel $Capture
./scripts/Run.ps1 -Check
```

Run Keyboard and Scaling with both rig pairs. Background captures were serialized,
paced and low priority on the private desktop with pause/cancel/resume, through
the shared offscreen native renderer. There was no OS input injection, desktop
switch or active-screen pixel copy. Receipts retain those scope flags; ignored
`artifacts/Fighterport-workspace19/qualification.json` seals final receipts,
reviewed images, binaries, source/build hashes and the publication commit.

## Development findings and remaining gates

Earlier runs are development evidence and excluded from totals. The first
keyboard assertion omitted the existing Clear button between search and tree;
the declared/tested order now includes it. Exact-field testing exposed the
unselectable private NumericUpDown edit child; bookmarks now remember its public
spinner. Image review found simultaneous large library/task coverage of the
model, corrected by yielding task space. Nested Save re-enables/focuses Save
after cancellation. One packaging attempt used a relative SDK path and failed
after the engine changed directories; the absolute-path build succeeded.

The [DPI audit](WORKSPACE_DPI_AUDIT_2026-10-24.md) records source findings and
migration requirements without changing process awareness. The [prepared user
walkthrough](WORKSPACE_USER_WALKTHROUGH_2026-10-24.md) records tasks and acceptance
criteria, not performed human evidence. These handler calls do not qualify real
physical keyboard/mouse input, standard OS Browse, physical/mixed-monitor DPI,
user-config disk roundtrip or measured first/repeat human outcomes. Existing
native shortcuts are retained by source preservation and operation regressions;
their physical key delivery remains part of that walkthrough.

Full native parity, broad/nonidentity root conversion, held-out motion/contact
quality, advanced 96.2 Apply and actual destination/game acceptance remain open.
Next within G1/M2 is the user-driven supported-flow walkthrough and a separately
bounded DPI migration only after its audit prerequisites can be exercised.
