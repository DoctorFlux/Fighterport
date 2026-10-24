# Project lifecycle and source-owned controls — 24 October 2026

Preview.17 (`2.0.0-preview.17-project-and-motion-controls`) continues PUBLIC
DoctorFlux/Fighterport from `8e7d306d497913343181f52ec8d55c91f5b20ada`.
**1,182 scoped assertions passed; all 23 final PNGs were individually opened and
visually inspected.** M2 remains the one incomplete milestone. These results
qualify the bounded increment below, not all games, native parity, contact
generalization, physical input or first-time-human usability. The asynchronous bound-character-
workspace save branch was inspected, but was not exercised by these lifecycle
cases; they use native .fpstudio projects.

## Behavior and ownership

New, Open and window Close share an owned Save / Discard / Keep editing dialog.
Save completes before replacement continues, including asynchronous character
workspace saves. Cancelling or failing a save leaves the decision and current
native document open. It no longer schedules an unrelated Close after a workspace
save. A clean empty New project needs no prompt. The existing native editor owns
the animation, selection, timeline and Undo throughout.

Untitled Save / Save As uses the existing application-owned path picker with
Browse, extension/folder validation and inline errors. Save As refuses to replace
a different existing project; choose a fresh filename. Normal Save updates its
own manifest using existing atomic, content-addressed project storage. The native
frame survives saving and cancelled replacement. The actual sharing-violation
test retains current dirty state and usable Undo/Redo. Project actions refresh
immediately when asynchronous control setup finishes.

Original Next `bind-world-fk-body-contact-ik-v7` remains the default base. New
results additionally record `MotionControlPolicy=source-local-controls-v1`.
Legacy records with no policy remain readable and are not retroactively
qualified. Unknown policy identities are rejected.

Strict ancestors of the reviewed pelvis/body identify protected motion controls.
TopN, TransN, XRotN and YRotN are examples, not numerical name exceptions. Each
control follows its OWN source-local SRT channels. Inherited parent/world motion
cannot authorize another target control track. Missing control tracks/groups
retain the authentic target baseline; no generated native group is retained.
Source translation uses fixed native rest coordinates without anatomy scaling;
rotation uses source/target parent-rest frames and bind-relative local rotation;
scale uses the explicit local bind ratio. Identical rotation coordinates retain
source Euler turns, key times, duplicate knots and tangents. Translation retains
the existing exact linear curve conversion. Nonlinear converted rotations must
pass a separate integer/quarter/withheld-time local gate before publication.
Unmapped authored controls fail with a binding-review explanation.

Body fitting/contact corrections run below these controls. Native FK handles,
transform fields, bulk paste/clear, curve restoration and the worker edit guard
prevent new manual control compensation. Body FK/IK, pins, native curve editing,
Undo/Redo and playback remain in the full previewer. Protection survives worker
commit replies that omit binding metadata. A model-only animation creates body
pose tracks without freezing global controls into new keys. Reviewed boundary
fitting retains every protected control curve even if the older preserve-root-
translation checkbox is disabled. Ambiguous editor body bindings require review.

Frozen new local gates: translation 0.0002 native units, scale 0.00001,
orientation 0.01 degrees. Existing contact/retarget bounds were not relaxed.

## Fresh acceptance

All paths below are relative to the ignored parent-workspace artifacts folder.
Original supplied game inputs are private fixtures, never repository content.
Their game-build identity/motion membership is supplied metadata, not independent
game verification. Independent native test animations are procedural authored
fixtures; the workspace regression additionally uses the supplied Mario Wait1.

| Final run | Assertions | Inspected PNGs |
| --- | ---: | ---: |
| `viewer-parity/Fighterport-workspace17-motion-mario-snake-r3` | 464 | 0 |
| `viewer-parity/Fighterport-workspace17-motion-snake-mario-r2` | 464 | 0 |
| `viewer-parity/Fighterport-workspace17-lifecycle-snake-r7` | 37 | 6 |
| `viewer-parity/Fighterport-workspace17-lifecycle-mario-r1` | 37 | 6 |
| `viewer-parity/Fighterport-workspace17-workspace-mario-snake-r2` | 61 | 11 |
| `Fighterport-workspace17/engine-semantic-r2` | 26 | 0 |
| `Fighterport-workspace17/engine-boundary-r3` | 34 | 0 |
| `Fighterport-workspace17/engine-viewer-r1` | 59 | 0 |

Native policy cases check body-only clips, each isolated control, inherited
rotation exclusion, source-authored translation/scale/full turns/duplicate knots,
fresh CHR0 decode, native integer/quarter/withheld samples, renamed/reordered
compatible hierarchies and immutable models in both proportion directions.
The current fixtures exercise matching native control frames; the runtime
conversion gate is not broad held-out evidence for arbitrary nonidentity axes.

Each lifecycle run checks actual New, Save, Keep editing, Discard, cancelled
Save-path selection, invalid filename, a locked manifest, body pin editing,
protection before/after commit, native Undo/Redo after failed replacement, project
reopen at frame 3, cancelled Open, cancelled window Close and immutable originals.
The workspace regression checks original Next invocation/provenance, repeated
same-name revisions, all native key/flag/tangent equality against a separate bake,
playback without retarget captures/bakes, world-space body posing, native fields,
curves, Undo/Redo, export/project reopen, cancellation and stale dependency guards.

Visual review covered both actual edited/reopened models; Save/Discard/Keep
editing and failed-save text; invalid path feedback; empty/model-only workspace;
source audition at 1320/1600; focus mode; native transfer/body edit; interpolation;
reopened result and measured progress. Labels/buttons were readable and stayed
inside their captured dialogs. Captures used the serialized, paced private
desktop and shared offscreen viewport. No active-desktop capture, OS input
injection or focus stealing was used. Physical pointer/keyboard, OS Browse
dialogs, multiple DPI settings and human usability remain unverified.

## Commands and build evidence

`$artifacts`, `$native`, `$dotnet`, `$msbuild`, `$marioModel`, `$marioMotion`,
`$snakeModel` and `$snakeMotion` denote local paths, not downloadable assets.
The native output is `$artifacts/Fighterport-workspace17/native`.

```powershell
./scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:minimal
& $msbuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:minimal
& "$native/RetargetAcceptance.exe" --motion-controls $marioModel $snakeModel $forwardOutput
& "$native/RetargetAcceptance.exe" --motion-controls $snakeModel $marioModel $reverseOutput
./scripts/Test-BackgroundCapture.ps1 -Model $snakeModel -Motion $snakeMotion -OutputDirectory $snakeLifecycle -ProjectLifecycle -NativeDirectory $native
./scripts/Test-BackgroundCapture.ps1 -Model $marioModel -Motion $marioMotion -OutputDirectory $marioLifecycle -ProjectLifecycle -NativeDirectory $native
./scripts/Test-BackgroundCapture.ps1 -Model $snakeModel -Motion $marioMotion -RetargetSource $marioModel -Clip Wait1 -OutputDirectory $workspaceOutput -SimpleWorkspace -NativeDirectory $native
# From engine/, using its .NET 10 global.json:
& $dotnet run --project tests/FighterPort.Tests -c Release -- $semanticOutput --semantic-controls
& $dotnet run --project tests/FighterPort.Tests -c Release -- $boundaryOutput --boundary-fitting
& $dotnet run --project tests/FighterPort.Tests -c Release -- $viewerOutput --viewer-controls
```

Final packaging log: `fighterport-workspace17-build-r5.log`. Native/harness builds:
`fighterport-workspace17-harness-r9.log`, `fighterport-workspace17-harness-r10.log`
and `fighterport-workspace17-retarget-harness-r4.log`. Lifecycle runs used harness
r9; workspace r2 used r10 (body-world assertion correction only; native app and
engine were identical). Engine logs end in `engine-semantic-r2.log`,
`engine-boundary-r3.log`, `engine-viewer-r1.log`. Ignored local qualification
metadata records source/build/evidence hashes and image review.

Development attempts exposed missing reply metadata and project-action readiness,
which were fixed. Harness failures also exposed obsolete root-only Euler addition,
binary-encoding comparison instead of semantic Undo comparison, and frame
expectations after earlier test gestures. Final tests use native semantic keys,
explicit frame selection and independent requested world poses with the same
bounds. Earlier failed directories/logs remain preserved and are not counted.

## Remaining scope

The current bounded mechanical increment is accepted. Next within the SAME M2/G1:
readable DPI/scaling and first/repeat human task outcomes, followed by retained
contact/context work. Full original support timelines, coupled/moving surfaces,
arbitrary control-chain/axis conversion, held-out anatomy/body-volume/batch
generalization, other-game adapters and actual game tests remain unqualified.
Advanced 96.2 Apply and its prior failures remain blocked. No binary release,
game asset publication or game installation is authorized by this evidence.
