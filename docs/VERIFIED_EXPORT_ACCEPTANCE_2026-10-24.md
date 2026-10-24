# Verified animation export — 24 October 2026

Preview.22 (`2.0.0-preview.22-verified-export`) follows public main
`464e0b764122fcbad039155045fd64f939b05e44`. This is the first of the three
user-authorized increments inside the ONE incomplete G1/M2 milestone.

## Behavior and preservation

Workspace Export uses the application-owned path dialog for CHR0, with the current
project, target, exact native resource, clip, frame count and destination visible.
Project Save retains its existing extension, validation and native lifecycle.
An existing export requires confirmation; cancellation keeps it. Native source,
library, project manifest/staging/assets and character-workspace protections apply.
Atomic replacement replaces a directory entry rather than writing through a
possible hard-link alias. Other resource/container export routes remain separate.

A frozen operation captures native entry order, absent channels, all key indices,
float values and tangents, six semantic flags, version, original path, user data,
frame count and loop state. The live animation is never rebuilt or promoted by
this action. A separate snapshot uses the existing precise CHR0 writer. Fresh
native decoding must agree with the frozen content before same-directory atomic
publication. Dependency and destination checks repeat before publication. Changed
target/clip/rig/provenance/content invalidates the operation; choose Export again.
A successful library export receipt is recorded only after publication. This is
intentional project provenance, separate from unchanged live curves and Undo.

The procedural v5 fixture exposed a native post-processing bug: its user-data
header lookup used the final bone-entry pointer. Retaining the animation header
address fixes this crash without changing the format. User-data float decoding
now uses round-trip text to retain native float precision across writing. The
precise channel writer and original Next solver are unchanged. Larger exact
curve files are an accepted storage tradeoff; no project schema is changed.

## Background qualification

Final qualification: **1,644 scoped assertions**, with **43 final PNGs reviewed**
individually in labelled image galleries. All eight final runs passed:

| Case | Assertions |
| --- | ---: |
| Export, each authentic rig | 23 + 23 |
| Original Next/native workflow | 61 |
| Keyboard/focus | 162 |
| Workspace scaling | 410 |
| Project lifecycle | 37 |
| Source-local roots, both directions | 464 + 464 |

Local evidence: `artifacts/Fighterport-workspace22/*-r3/`, the three build logs,
and `artifacts/Fighterport-workspace22/qualification.json`. Failed/development
runs are excluded from totals. The launcher selects the verified preview.22 output.

Dedicated `-WorkspaceExport` runs exercise both authentic rig inputs, real native
body edits before project Save, unchanged entry references/clip/frame/Undo, exact
body Undo/Redo, cancelled and confirmed overwrites, locked publication, stale
frozen dependencies, deliberately damaged candidates, original CHR0 protection,
duplicate clip names, sparse/absent tracks and v5 path/string/int/float metadata.
Export dialogs run at compact/wide sizes and 100%/200% workspace text.

Relevant fresh runs cover the complete original Next transfer/native edit/curve/
export/save/reopen workflow, keyboard, scaling, failed/cancelled save lifecycle,
and independent protected-control numeric expectations in both rig directions.
Original model/motion files remain byte-identical. Background cases isolate new
library state and recent history. Captures are generated on private desktops with
the shared offscreen renderer, without OS input injection or active-screen reads.
Every final capture is reviewed; receipts and image/source/binary hashes remain in
ignored local qualification artifacts.

Commands from the repository (fixture paths/output roots supplied locally):

```powershell
./scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
& $msbuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
./scripts/Test-BackgroundCapture.ps1 -Model $model -Motion $motion -OutputDirectory $freshOutput -NativeDirectory $native -WorkspaceExport
# Repeat with SimpleWorkspace (+ RetargetSource), WorkspaceKeyboard,
# WorkspaceScaling and ProjectLifecycle, using separate fresh outputs.
& "$native/RetargetAcceptance.exe" --motion-controls $sourceModel $targetModel $freshRootOutput
```

Development failures are preserved locally. The first harness ordered frame
selection before deferred native setup completed; it now waits for setup. Modal
completion is checked by DialogResult after handlers, not premature visibility.
Image review found a clipped 100% status row; the export-specific dialog height
was increased and affected runs repeated. Numeric expectations were not relaxed.

## Limits and next increment

This qualifies native Brawl CHR0 workspace export, not other formats/containers or
new game destinations. Physical input, monitor DPI, human usability, broad contact
quality and game acceptance remain unverified. Full native previewer, original
Next and source-local protected controls remain. Linked source comparison is the
next approved increment, followed by favorites and filters. M2/G1 remains open.
