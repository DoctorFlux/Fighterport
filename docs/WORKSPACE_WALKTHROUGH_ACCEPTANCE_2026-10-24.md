# Workspace workflow replay — 24 October 2026

Preview.20 (`2.0.0-preview.20-workspace-walkthrough`) continues PUBLIC
DoctorFlux/Fighterport from `3079490ec28d37c27dd6c4ac8593ab041338a6fd`.
The user explicitly selected **background-only** execution of the prepared tasks.
**2,292 fresh scoped assertions passed; all 73 final PNGs were opened and inspected.**
This accepts the bounded background increment. The human walkthrough is unperformed;
G1 within the ONE current M2 milestone remains incomplete.

## Behavior and native ownership

A new `-WorkspaceWalkthrough` private-desktop harness follows actual application
handlers continuously through source registration, target selection, original
Next transfer, native body correction, Undo/Redo, export, project Save and reopen.
It registers a source once, reuses that exact association on a second independent
project and checks that every first-project asset/manifest/export stays byte-identical.
Reopened projects select the exact authored clip at frame 3 with their saved
source identity. A third project opens native model/motion and saves/exports
without opening the source library or running a retarget.

The replay exposed a real native BoneState Undo defect: sampled pose restoration
regenerated neighboring rotation tangents. In the diagnostic case selection
changed no content, but Undo changed three tangents: -1.020824 to -1.024,
-1.99026215 to -1.961, and -2.78588057 to -2.756. Expectations were kept exact.
The native control now accepts an optional document-owner snapshot callback.
BoneState retains its existing position in the native Undo stack and records an
in-memory restore action for affected CHR0 bone channels. The existing
NativeControlSnapshot restores exact keys/tangents/flags and absent groups, then
refreshes the native model/curve/keyframe UI and existing control setup invalidation.
No competing Undo stack, persisted state or project schema was added. Model-only,
bind-edit and callback-free use retains the prior fallback.

Original procedural clips independently check absent tracks and a missing key
between sparse neighbors with authored high-precision tangents and native flags.
The harness verifies the actual native BoneState route, inserted frame-3 key,
exact Undo and exact Redo. Real edited transfers also verify Undo/Redo after
native resource merge and a failed project save.

The COMPLETE BrawlCrate Model Previewer remains the native authoring control:
menus, resources, camera/materials, animation/bone lists, viewport, fields, gizmos,
curves, timeline and playback. Only its narrow bone Undo integration changed.
Engine and original Next retargeter sources are unchanged. Original Next v7 and
`source-local-controls-v1` remain separate recorded provenance: each protected
control follows only its own source-authored local animation. Inherited movement
does not authorize child keys; reviewed hierarchy identifies equivalent roles.
Manual/fitting corrections stay on body bones. Original bytes, authentic target
model/binds/helpers/weights and source library identities are preserved.

## Final evidence

All runs below use the final preview.20 package. Supplied Mario/Snake Brawl
fixtures remain private local inputs; no game assets, captures or private paths
are published. Each walkthrough uses source Wait1, the authentic target model
and its own motion package for the separate donor-free path.

| Final suffix (`Fighterport-workspace20-`) | Assertions | Inspected PNGs |
| --- | ---: | ---: |
| walkthrough-mario-snake-r1 | 60 | 9 |
| walkthrough-snake-mario-r1 | 60 | 9 |
| keyboard-mario-r1 | 162 | 7 |
| keyboard-snake-r1 | 162 | 7 |
| scaling-mario-r1 | 410 | 11 |
| scaling-snake-r1 | 410 | 11 |
| workspace-mario-snake-r1 | 61 | 11 |
| lifecycle-snake-r1 | 37 | 6 |
| dialogs-r1 | 2 | 2 |
| motion-mario-snake-r1 | 464 | 0 |
| motion-snake-mario-r1 | 464 | 0 |
| Total | 2292 | 73 |

Each walkthrough records **43 handler operations, one source registration,
two actual Next operations and three independent saved projects**. Local
`walkthrough-steps.json` journals operation outcomes and handler elapsed times;
`walkthrough-summary.json` explicitly marks human/physical-input outcomes false.
These counts/durations are not human step or task-time measurements. Body
corrections are fixed at +2 and +4 degrees at frame 3, using the existing frozen
0.0002 field tolerance. Full native content and protected control keys/tangents/
flags use exact equality, with fresh independent native output decode.

Cancelled target selection preserves the source and empty target. Keep editing
preserves clip/frame/Undo; a real locked-manifest sharing violation leaves the
dirty project available, and Save recovers after lock release. The older lifecycle
case additionally exercises cancelled nested saves, New/Open/Close and discard.
Application-owned source/path/unsaved dialogs are exercised through real controls.
Native import/export handlers receive paths directly: OS Browse/Open/Save dialogs
are bypassed, explicitly labeled in the journal and unverified for physical use.

Fresh keyboard cases repeat compact/wide 1050x650 and 1600x950 layouts at 100/200%
workspace text, routing keys through actual application/control handlers. Scaling
repeats all five presets and layout/focus/stage/library cycles. Transfer regressions
retain independent original Next parity, body pins/fields/curve edits, Undo,
playback-without-bake, stale dependencies, cancellation and output reopen.
Bidirectional root cases retain independent local-track, inherited-motion,
quarter/withheld-sample and rename/permutation expectations.

All 18 walkthrough, 14 keyboard, 22 scaling, 11 workspace, six lifecycle and two
dialog PNGs were individually inspected. Actual models, distinct source audition,
body edits/frame 3, saved links, curve UI, focus rectangles, large text scrolling
and real save failures are visible. Compact drawers retain their existing overlay
behavior; close them or Focus editor to expose every native tool. Long captions
still require scrolling/tooltips. The dialog error-layout fixture is synthetic;
actual failure proof is in walkthrough/lifecycle captures.

## Builds and reproduction

Full native/net10 engine/legacy-worker packaging passed in
`fighterport-workspace20-build-r1.log`. Final native app/controls harness passed
MSBuild in `fighterport-workspace20-harness-r2.log`; numeric harness build passed
in `fighterport-workspace20-retarget-harness-r1.log`. Native app/harness are
net472/x86 with VS2022 MSBuild; engine SDK is 10.0.402. No full engine-suite run
is claimed. These outputs are separate from the sealed preview.19 package.

Use absolute SDK/output paths and private fixture variables. Every `$Capture`
value must be a fresh directory. Commands actually used:

```powershell
./scripts/Build.ps1 -DotNet $DotNet10 -NativeDirectory $Native
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
& $MSBuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $MarioMotion -RetargetSource $MarioModel -TargetMotion $SnakeMotion -Clip Wait1 -OutputDirectory $Capture -WorkspaceWalkthrough -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $MarioModel -Motion $SnakeMotion -RetargetSource $SnakeModel -TargetMotion $MarioMotion -Clip Wait1 -OutputDirectory $Capture -WorkspaceWalkthrough -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $Model -Motion $Motion -OutputDirectory $Capture -WorkspaceKeyboard -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $Model -Motion $Motion -OutputDirectory $Capture -WorkspaceScaling -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $MarioMotion -RetargetSource $MarioModel -Clip Wait1 -OutputDirectory $Capture -SimpleWorkspace -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $SnakeMotion -OutputDirectory $Capture -ProjectLifecycle -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $MarioModel -Motion $MarioMotion -OutputDirectory $Capture -WorkspaceDialogs -NativeDirectory $Native
& "$Native/RetargetAcceptance.exe" --motion-controls $MarioModel $SnakeModel $Capture
& "$Native/RetargetAcceptance.exe" --motion-controls $SnakeModel $MarioModel $Capture
./scripts/Run.ps1 -Check
```

Keyboard/Scaling each ran with both authentic rig pairs. Background captures were
serialized, paced and low priority using the private desktop/shared offscreen
native renderer with existing pause/cancel/resume support. Receipts report
PrivateDesktop=true, ActiveDesktopWindows=false, OSInputInjected=false and
ScreenCopied=false. No active desktop input/capture or SwitchDesktop was used.
Ignored `artifacts/Fighterport-workspace20/qualification.json` seals final source,
build/binary, receipt, journal and individually reviewed image hashes plus the
publication commit. The local ignored launcher selects the preview.20 package;
Run -Check validates its destination without launching an interactive window.

## Development findings and remaining gates

Earlier `Fighterport-walkthrough-mario-snake-r1/r2` diagnostics exposed the Undo
tangent defect. After repair, r3 reached a harness-only wait error (waiting for a
library selection before selecting it); r4 exposed the procedural fixture's
default native encoder densifying sparse keys. The wait now waits for tree/search
settlement before explicit selection; procedural fixture export uses the existing
precise native writer. R5 passed before two post-failure Undo checks were added.
All development outputs/logs are preserved locally and excluded from final totals.
No tolerances, engine/root numeric rules or production writer were relaxed.

The [human walkthrough](WORKSPACE_USER_WALKTHROUGH_2026-10-24.md) remains prepared,
not performed. Command discovery, repeated setup burden, wrong-document mistakes,
physical keyboard/mouse use, physical/mixed-monitor DPI and user-config disk
roundtrip remain unverified. Handler timing is not a usability score. Current
process DPI configuration is unchanged; the [DPI audit](WORKSPACE_DPI_AUDIT_2026-10-24.md)
still governs later migration prerequisites.

No new formats/destinations, runtime/game installation/testing, model-port/batch
acceptance, broad contact quality or rig-generalization claim follows. Advanced
96.2 Apply remains blocked; all contact/context gates are retained. Next within
G1/M2 is human task observation when the user chooses a hands-on session. Respect
the current background-only instruction; keep exactly ONE current milestone.
