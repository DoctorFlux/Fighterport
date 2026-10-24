# 23 October native integration evidence

Later on 23 October the user explicitly restored the original Next retargeter as
the native default. This document retains the earlier integration evidence;
see [the follow-up acceptance](NEXT_RETARGET_ACCEPTANCE_2026-10-23.md) for current
native retarget/pin tests. References below to 96.2 as the current UI route are
historical, not the current default.

The user explicitly selected Next as the main product. This increment keeps the
native viewer/editors and imports the owned engine at standalone `153726b`
(implementation `4752bb4`), above Next baseline `96a4e0e`. It is source-only work.
The entire future scope remains retained, not claimed implemented by vendoring.

## Actual commands and results

Commands ran from the existing FighterPort workspace. `$MSBuild` was the installed
VS2022 Community `MSBuild/Current/Bin/MSBuild.exe`; `$dotnet` was the local
`.tools/dotnet/dotnet.exe` SDK 10.0.402. NuGet and CLI homes were local caches.

```powershell
& $MSBuild .local/next-main/BrawlCrate/BrawlCrate.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false /nologo /v:minimal
& .local/next-main/scripts/Build-Engine.ps1 -DotNet $dotnet
& $MSBuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /nologo /v:minimal
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model artifacts/viewer-parity/native/original.mdl0 -Motion artifacts/viewer-parity/native/original.chr0 -OutputDirectory artifacts/viewer-parity/next-final-controls-r2
```

Native application, engine, CLI and worker build successfully. The imported
engine regression build also succeeds, with its retained AVA3001 warning for
the old regression renderer's unstable Avalonia APIs. That renderer is not the
product's native UI.

The current original native-fixture scope passes **40 checks**. It starts the
real engine from the native frontend, verifies rig identity and explicit bone
names across the boundary, requests ambiguous coordinate review, uses the current
retarget engine and retains its draft gate. It exercises automatic structural
limb setup, soft reach under large body movement, frozen animation-follow pins,
arbitrary bone pins, exact numeric movement/30-degree rotation, cancellation,
actual native pointer down/move/up handlers, stale-frame rejection, native and
toolbar Undo/Redo, branch replacement, normal bone Undo compatibility, project
Save Fighter/reopen, fresh CHR0 decoding and the actual no-argument startup factory.
Editing one translation axis also preserves an untouched nonzero native rest axis
through a fresh native file decode. No bind, hierarchy or skin replacement occurs.

`--viewer-controls --native-controls <local supplied project>` passes **51**;
`--viewer-editing` passes **76**; `--receipt-compatibility` passes **9**;
`--workspace-only` passes **32**; `--viewer-native` passes **32** using the newly
built worker. Each command ran the imported `engine/tests/FighterPort.Tests`
executable with a distinct `artifacts/viewer-parity/next-engine-*` output folder.
The 200 focused engine checks are actual reruns, not old repository receipts.
The integrated host's `cli --help` dispatch also runs successfully.

A separate serialized `-SuppliedModel` background run passes **3 checks** on
the locally supplied Snake native model/motion: all 70 existing bones cross the
boundary, the pin edit persists to the selected animation, and fresh CHR0 decode
retains the authored channel. Inputs and private paths are not published.

## Displayed evidence actually inspected

`next-final-controls-r2/` contains the complete dark native viewer with the
procedural fixture and independently selected diamond, its native GPU model image,
and empty direct-viewer startup. All three images were inspected. The native
stacked lists, Bones panel, original transform/transport widgets and extra Fighter
toolbar are visible. Search placeholders and native key-highlight semantics
remain functional after theme changes.

`next-supplied-model-r1/` contains two native model images and two complete viewer
images at native display frames 1 and 8. All four were inspected: the actual
textured character, existing native skeleton, persistent pin and changing saved
animation are visible. These are manual native edit/playback samples, not automatic
retarget or game-quality acceptance. Capture receipts record a private desktop,
no active-desktop windows, no injected OS input and no screen copying. Images are
read from the native renderer and native control printing, paced and serialized
by `scripts/Test-BackgroundCapture.ps1`. They remain local for user review.

## Limits and next work

Physical mouse/keyboard use, all monitor/DPI configurations, complete modal-window
lifecycle and every native resource editor have not been exercised here. Original
native controls and camera routes are retained; these checks do not establish
100% visual/input acceptance across all environments. The inherited native
presentation path is unchanged; old displayed-60-FPS receipts are not new runs.

Automatic retarget contacts remain disabled and all retained 96.2 motion,
clearance/contact, reconstruction and generalization failures remain unresolved.
An editable/exportable review draft is not validated Apply-ready motion. Other
game adapters, native gameplay/build pipelines and actual game testing are not
implemented by this increment. Core still has its existing 128-bone import/evaluation
boundary; a native viewer opening a larger model does not prove engine support.
The full standalone Core/CLI functionality and scope are retained, while advanced
character/target/workspace GUI ports remain queued behind this integration slice.
M2 remains incomplete. The sole next queue is `plans/current.md`.
