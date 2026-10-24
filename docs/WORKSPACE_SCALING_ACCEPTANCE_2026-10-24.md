# Workspace scaling — 24 October 2026

Preview.18 (`2.0.0-preview.18-workspace-scaling`) continues PUBLIC
DoctorFlux/Fighterport from `736c8e1fbee8cde5b9eab3c1a83cc78cf65469a1`.
**1,848 scoped assertions passed; all 41 final PNGs were opened and inspected.**
These are fresh runs of the final application and harness. Historical preview.17
counts are separate. M2 remains the one incomplete milestone, with G1 current.

## Implemented behavior

- The outer workspace has a Text menu with 100/125/150/175/200% presets.
  Workspace headings, task details, library rows/actions and project/unsaved
  dialogs scale together. Native Model Previewer fonts remain original.
- A separate action bar keeps Workflow, Source library, Focus editor, More tools,
  Open project, New project and Text accessible. Compact captions retain full
  accessible names/tooltips. More tools opens the original native overflow.
- Workflow and library dock when room permits and become dismissible drawers
  in compact windows. Workflow recovers the task after Focus editor; stage
  navigation preserves the document, selection, frame and native Undo stack.
- Task details scroll while readiness and the primary action remain visible.
  Readiness is limited to four lines with full tooltip/accessibility text.
  Library content scrolls and retains usable tree space; long tree captions
  still need horizontal scrolling/tooltips.
- Disabled outer-workspace buttons use readable captions. Project/unsaved
  dialogs wrap/scale within the work area, retaining all actions at 200%.
- Text size uses a user-scoped setting, outside fighter documents. Save failure
  reports a session-only preference. Capture runs isolate user settings; they
  verify preference survival across New project, not a real disk/config roundtrip.

This is application text scaling. Process DPI awareness, monitor scaling and
native-editor fonts are unchanged. It does not qualify physical/per-monitor DPI.

## Preserved ownership and root policy

The COMPLETE original BrawlCrate Model Previewer stays mounted: native menus,
resources, animation/bone lists, viewport/materials/camera, transform fields,
gizmos, interpolation, timeline and Undo. Compact overlays do not replace or
reparent its editor. Minimum native horizontal room is derived from the actual
582px CHR0 editor and 290px playback controls plus spacing (888px).

Original Next `bind-world-fk-body-contact-ik-v7` and the separate
`source-local-controls-v1` policy remain intact. Engine, BrawlLib and retargeting
sources are unchanged from the base commit. Each reviewed global/motion control
follows only its own source-authored local animation. Inherited world motion
does not authorize child keys, and generated/manual corrections use body bones.
TopN/TransN/YRotN/XRotN are examples; reviewed hierarchy identifies equivalents.
The [preview.17 policy receipt](PROJECT_MOTION_CONTROLS_ACCEPTANCE_2026-10-24.md)
retains its bounds and outstanding conversion/generalization limits.

## Final runs

Local evidence is ignored under `artifacts/viewer-parity/`. Authentic supplied
Brawl model/motion fixtures remain private and are not redistributed. Scaling
uses Mario and Snake independently; transfer uses Mario Wait1 onto Snake. Motion
control cases use both authentic rigs with independent procedural animations.

| Final directory suffix (`Fighterport-workspace18-`) | Assertions | Inspected PNGs |
| --- | ---: | ---: |
| scaling-mario-r8 | 410 | 11 |
| scaling-snake-r2 | 410 | 11 |
| workspace-mario-snake-r2 | 61 | 11 |
| lifecycle-snake-r2 | 37 | 6 |
| dialogs-r3 | 2 | 2 |
| motion-mario-snake-r2 | 464 | 0 |
| motion-snake-mario-r2 | 464 | 0 |
| Total | 1848 | 41 |

Each scaling run exercises seven preset selections (including repeated 100/200),
21 client-size cases (1050x650, 1320x850, 1600x950), 84 stage actions, 42 focus
toggles and six first/repeat library/workflow cycles. Assertions check native
control/font identity, exact target/clip/frame 3/Undo, primary-action reachability,
panel bounds, source-only protected controls and unchanged native keys/original
bytes. Registration/search and read-only source audition at 200% use actual
library/model resources. Dialog cancellation preserves the active document.

The retained workspace regression checks original Next transfer/native key,
flag and tangent parity against a separately prepared bake; same-name independent
revisions; playback without baking/capture work; body pin/field/curve edits and
Undo; native export/project reopen; stale dependency/cancel guards; authentic
deform data and immutable originals. This is route parity, not universal motion
quality. Lifecycle checks exercise real save/discard/keep-editing, cancelled and
locked-manifest failed saves, New/Open/Close and native body edit/reopen. The
two scaled-dialog checks include synthetic error text for layout only; the
separate lifecycle run proves actual save-failure handling.

Bidirectional motion-control regressions independently check source-local tracks,
native integer/quarter/withheld samples, missing tracks, inherited motion and
rename/storage permutation. No new numerical bound or solver tuning is introduced.

All 22 scaling, 11 workspace, six lifecycle and two dialog images were individually
inspected. Captures include original target/source models, compact/wide layouts,
100–200% text, native curves, retarget progress, real save failure and reopened
documents. Images remain local game diagnostics, not public product assets.

## Reproduction and build evidence

Use PowerShell variables for private fixture paths and output directories.
`$Native` denotes the ignored preview.18 native output; `$Capture` a fresh output
directory for each invocation. Build used Visual Studio 2022 MSBuild,
.NET Framework 4.7.2/x86 and SDK 10.0.402 for the isolated engine:

```powershell
./scripts/Build.ps1 -DotNet $DotNet10 -NativeDirectory $Native
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
& $MSBuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
./scripts/Test-BackgroundCapture.ps1 -Model $MarioModel -Motion $MarioMotion -OutputDirectory $Capture -WorkspaceScaling -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $SnakeMotion -OutputDirectory $Capture -WorkspaceScaling -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $MarioMotion -RetargetSource $MarioModel -Clip Wait1 -OutputDirectory $Capture -SimpleWorkspace -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $SnakeMotion -OutputDirectory $Capture -ProjectLifecycle -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $MarioModel -Motion $MarioMotion -OutputDirectory $Capture -WorkspaceDialogs -NativeDirectory $Native
& "$Native/RetargetAcceptance.exe" --motion-controls $MarioModel $SnakeModel $Capture
& "$Native/RetargetAcceptance.exe" --motion-controls $SnakeModel $MarioModel $Capture
./scripts/Run.ps1 -Check
```

Full packaging passed in `fighterport-workspace18-build-r3.log`. Final native
app/controls harness rebuild passed in `fighterport-workspace18-harness-r11.log`;
retarget harness build passed in `fighterport-workspace18-retarget-harness-r1.log`.
All seven final runs used this final app. Each background directory retains
`receipt.json` and `background.log`; numeric runs retain `motion-controls.json`.
The ignored `artifacts/Fighterport-workspace18/qualification.json` seals receipts,
reviewed images, binaries, logs, source hashes and publication identity.

Background captures were serialized, paced and low priority on a private desktop
through the shared offscreen viewport, with pause/cancel/resume. No OS input was
injected, no active-desktop pixels copied, and no interactive window was shown
over the user's desktop. No full engine suite was rerun or claimed.

## Development failures and limits

Earlier failed runs/timeouts remain development evidence and are excluded from
the final totals. Repeated font changes exposed equal-font disposal in WinForms;
the five presets now share a bounded process-lifetime font cache. Actual native
timeline measurements replaced an inadequate initial 760px room estimate.
Compact Show workflow recovery was corrected. Nested dialog picker row sizing
clipped Browse at 200%; an explicit percentage row now constrains it. Clear's
measured button width was increased to show its full caption.

The harness now rethrows modal assertions outside the application's error handler
instead of leaving a hidden native error dialog. It waits for native startup
before importing. Capture composition preserves actual drawer z-order and the
source drawer's actual offscreen GL image; bitmap-only composition had covered
that model with a blank region. These are capture corrections, not a replacement
renderer. Final reviewed screenshots use the corrected harness.

Physical pointer/keyboard, standard OS Browse interaction, user-config disk
roundtrip, physical/per-monitor DPI and first-time/repeat human task outcomes
remain unverified. Full native parity, nonidentity/unmatched control conversion,
general motion/contact quality, advanced 96.2 Apply and actual destination-game
acceptance remain open. No new format, game adapter or universal-rig claim follows.

Next: qualify physical DPI/input and measured first/repeat workspace tasks within
the same G1/M2 slice, preserving queued contact/context work and every frozen gate.
