# preview.47 — existing-track fitting acceptance

27 October 2026: **2,638 checks / 11 admitted background runs / 71 inspected
native PNGs**. Seven runs use private desktops; four are background Core/native
numeric probes. Accepted source is published to public `DoctorFlux/Fighterport/main`
with DoctorFlux author/committer attribution. The canonical local launcher selects
`artifacts/Fighterport-keys47/native-final1`. The public contributor list was checked
as DoctorFlux only before publication and is checked again after the push. This
increment stays inside the ONE incomplete G1/M2 milestone.

## Result

Fit start and end has an optional **Fit existing animated bones only** checkbox.
Full fitting remains the default. Eligibility is frozen from the current target
animation's tracks with actual keys and exact target-skeleton membership. Helpers,
fingers and unweighted bones remain eligible when keyed; names and mesh weights
never determine membership. Empty tracks are not eligible.

The option restricts bone membership, not individual component axes. Selected
S/R/T components may create channels on an already-keyed bone. It never adds a
new bone track. Existing unselected tracks remain untouched; this option does
not perform the optimizer's separate absent-target cleanup.

When an optional reviewed reference-control policy requires an unkeyed control,
the dialog disables that policy and names the omitted controls. The complete
source-authored control locals remain unchanged. Missing mandatory recovery
dependencies produce a named blocking diagnostic, without enlarging the mask.
Reference-body previews use the same omitted-bone policy.

Core checks the frozen membership independently. Native publication checks it
again before writing candidate curves. Preparation is independent; Apply uses
the same current native animation and existing single Undo command. The bool
setting is additive and false by default. Historical recipes that omit it retain
full fitting; mismatched options/receipts reject. No schema version changes.

The complete native Model Previewer, original dense Next retargeter, native
Undo/Redo, reviewed root policy and precise I12 writer remain intact. This
increment changes neither solver math nor optimization tolerances.

## Evidence under review

All evidence is local and excluded from Git under
`artifacts/Fighterport-keys47/`. The qualification checkout is detached from
`00d46ec6e6f2971a21f251ec6ec44456987864c7` and contains only preview.47 source,
tests and this scope documentation; in-progress preview.48/49 changes are absent.

| Case | Checks | Result |
| --- | ---: | --- |
| Core boundary, rotation, controls, recovery and floor suite | 401 | Passed |
| Source-owned roots, Mario → Snake | 628 | Passed |
| Source-owned roots, Snake → Mario | 628 | Passed |
| Prior preview.44 native receipt read, mismatch rejection and round-trip probe | 7 | Passed |
| Original Next Snake AttackS3S → Mario Wait1, scoped preparation and ordinary Apply | 188 | Passed |
| Original Next Mario AttackAirF → Snake Wait1/Fall first poses | 292 | Passed |
| Imported Mario current animation, whole-animation scoped size and native edit history | 97 | Passed |
| Imported Snake current animation, whole-animation scoped size and native edit history | 97 | Passed |
| Workspace keyboard, focus recovery, registration and guarded dialogs | 210 | Passed |
| New project, cancelled/failed saves and native lifecycle | 37 | Passed |
| Current PAC members, sibling Undo/Redo, source references and historical asset receipt | 53 | Passed |

The imported fixture compares fresh precise CHR0 exports of the same reviewed
fit using full versus existing-keyed-bone membership. Mario measures **31,388 →
30,088 bytes**, **63 → 57 tracks**. Snake measures **18,392 → 16,212 bytes**,
**70 → 60 tracks**. These are standalone CHR0 measurements, not
PAC/compression savings or a universal game budget. Fitting can still add keys
and channels within eligible tracks. This option does not replace optimization.

The native workflow covers cancellation, protected intervals, changed references,
pre-Save high-precision edits, retained paired keys, native endpoint orientation,
first-reference poses, exact joins/middle, frozen root curves, current-clip Apply,
one native Undo/Redo, verified export and fresh project reopen. Scoped Next
preparation separately verifies track order, membership, omitted controls and
the unchanged live clip, playhead and native Undo.

Final imported cases exercise compact/wide dialogs at all supported 100%, 125%,
150% and 175% workspace text settings. All 71 admitted native PNGs have been
inspected through nine contact sheets and selected complete images. Models,
skeleton overlays, range summaries, scope
explanation and Apply footer remain intact. Compact settings scroll normally;
long drop-down resource names use their existing native elision. Keyboard,
lifecycle and PAC cases retain native models, document identity and guarded
dialogs. Capture hashes and the complete admitted run list are in local
`capture-review.json` and `qualification-summary.json`.

Admitted evidence folders: `core-final1`, `root-mario-snake`, `root-snake-mario`,
`legacy-receipt-final4`, `transition-mario-final1`, `transition-snake-final2`,
`boundary-mario-dev1`, `boundary-snake-final1`, `keyboard-final1`,
`lifecycle-final1` and `fit-references-final2`. The Mario boundary folder's `dev1`
name is retained: its complete 188-check scoped/default replay passed against
the same final application source. Subsequent harness additions affected only
the separate imported-size cases.

## Build and repeatability

Version `2.0.0.27`; informational version
`2.0.0-preview.47-existing-track-fitting`. Native, packaged engine, native worker,
ControlsAcceptance and RetargetAcceptance build successfully. Native compilation
has the pre-existing CS1690 loop-inspection warning; Core tests retain existing
Avalonia AVA3001/dependency warnings. No compilation errors remain.

The native build output is `artifacts/Fighterport-keys47/native-final1`.
FighterPortStudioV2.exe SHA-256:
`4EC88E69BC35B57AC3A419F52930E8CA5C3F7A1FC4B1C6EDAC130632B2FA2406`.
Exact local commands, source hashes, reports and build hashes accompany the
excluded evidence. Reproduction uses the installed VS2022/net472 x86 toolchain
and the workspace .NET 10 SDK from the engine directory:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory <native>
./scripts/Build-Engine.ps1 -DotNet <workspace-dotnet10> -NativeDirectory <native>
MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore /p:Configuration=Release /p:Platform=x86 /p:OutDir=<native>/ /p:UseSharedCompilation=false /m:1 /nr:false
dotnet build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release /p:UseSharedCompilation=false -m:1
dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll <new-output> --boundary-fitting
./scripts/Test-BackgroundCapture.ps1 -TransitionFit -Model <model> -Motion <motion> -NativeDirectory <native> -OutputDirectory <new-output>
./scripts/Test-BackgroundCapture.ps1 -WorkspaceBoundaryFit -Model <target-model> -Motion <source-motion> -RetargetSource <source-model> -TargetMotion <target-motion> -Clip <attack> -ReferenceClip <Wait1-or-Fall> -ExitSamples 8 -ReferenceControls -NativeDirectory <native> -OutputDirectory <new-output>
```

The root policy harness uses `RetargetAcceptance.exe --motion-controls
<source-model> <target-model> <new-output>`. The legacy compatibility probe uses
32-bit PowerShell against an actual prior accepted project, with temporary files
redirected into its isolated evidence folder.

Initial native/worker sandboxed builds required a retry with access to the
installed Windows SDK registry. An initial Core command used the outer SDK
selection and was rerun from `engine`. These were toolchain setup failures, not
passing application cases. Initial legacy probe drafts used the wrong process
bitness/receipt-wrapper path and are excluded. Development imported-fit runs with
the earlier 90-check harness are excluded from final acceptance totals. The
first PAC replay selected a preview.44 per-member receipt fixture for a test
specifically requiring the legacy asset-level field. It passed the feature checks
but failed that fixture assertion. Its complete retry uses the preserved accepted
preview.41 asset-level project and passes all 53 checks; the failed run is excluded.

## Boundaries

Tests use private desktops and the shared offscreen renderer. No OS input
injection, active-desktop capture or desktop switching occurs. The full native
previewer remains the sole target editor. Physical input, monitor DPI, human
usability, universal contacts, broader game acceptance and Melee/native adapters
remain unverified. Source-key retarget modes and compact storage are later,
separately qualified increments.
