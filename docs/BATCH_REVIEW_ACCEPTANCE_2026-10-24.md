# Batch review workspace — 24 October 2026

Preview.25 (`2.0.0-preview.25-batch-review`) follows public main
`4f957f33ad4a5c4eaf26e9af2e8405fa23cd42f5`. This accepts the review increment
within the ONE incomplete G1/M2 milestone; verified set export is the next queued
increment and is not claimed here.

## Behavior and preservation

Batch / Fit uses the current typed library query, including favorites, and shows
both returned/full counts and its 200-row display limit. Its 64 selected clips,
two-million-key review budget, offline timing fit and original Next v7 remain.
Candidate preparation changes no project animation. Failed/cancelled rows are
excluded; explicit loss review permits successful rows to join the existing
single import transaction. Target/source changes reject stale publication/Apply.

Successful import opens Review results after pending native control setup finishes.
The drawer is also available in Preview and fix and Export. Add selected permits
any eligible owned native CHR0 without a donor. Rows identify exact asset/native
resource keys, display source names and duration, and expose full timing, omitted
tracks and review status. Same-named clips remain independent. Previous/Next and
row selection use ordinary native animation selection, never scene reconstruction.
Remove from list changes only session navigation, never project animations.

The native editor continues owning curves, models, selection and Undo. Its existing
Undo commands already identify the affected animation/frame; an additive authored
notification invalidates approval after commits and Undo/Redo. Property/hierarchy
notifications cover native metadata and target changes. The drawer tracks native
selection, including cross-clip Undo. Detached/removed resources lose their watches.
Approval captures exact full native curve/metadata content, target model content
and recorded recipe. Export receipts do not alter the recipe identity. Explicit
validation rejects stale content. Idle/playback performs only cheap selection/busy
checks; it does not hash assets, decode, scan the library or bake. Save rechecks
unchanged approvals after native precise-encoder track replacement and rebinds
watches; reopen has no review list or approval. Project/catalogue schemas remain.

Compare source resolves the selected result's recorded original source, never a
browsed substitute. Comparison and review alternate visibility while retaining
the session rows and prepared source. Native/no-provenance clips explain why
comparison is unavailable while remaining editable. Focus editor closes both;
F6/Tab and focused Escape/focus recovery include review. Layout uses existing
compact overlay/wide docking and 100–200% application text, not process DPI.

## Fresh background qualification

The final cohort passes **1,925 scoped assertions**. All **81 final PNGs** were
inspected: readable review actions/status/details and modal captions, correct
compact overlay/wide native docking, retained real model rendering and native
interpolation/transform/timeline tools. The focused Mark reviewed capture includes
visible keyboard focus. Private-desktop receipts record no OS input injection,
no screen copying and no active-desktop windows. All three builds pass.

| Final scope | Assertions |
| --- | ---: |
| Batch review, both authentic rig directions | 66 + 66 |
| Library organization, both directions | 40 + 40 |
| Linked comparison, both directions | 40 + 40 |
| Verified single export | 23 |
| Keyboard / scaling / lifecycle | 174 + 410 + 37 |
| Native workspace and original Next workflow | 61 |
| Source-local protected controls, both directions | 464 + 464 |
| Total | 1,925 |

Dedicated `-WorkspaceBatchReview` runs both authentic rig directions through actual
batch controls, original Next and independent native reference decoding. Original
procedural Hermite expectations verify fractional timing separately. Real native
body edits/Undo/Redo, duplicate names, exact row synchronization, actual playback,
filtered favorites/counts, stale recipes, import Undo, save/reopen and donor-free
review are covered. Additional real batch dialogs exercise mixed success, cancelled
retargeting and changed source bytes before Apply, using isolated source copies.
The source originals remain byte-identical. Compact/wide 100%/200% captures include
frontmost-control checks and a focused readable Mark reviewed action.

Commands (local fixture/tool paths supplied outside public source):

```powershell
./scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
& $msbuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $sourceModel -OutputDirectory $fresh -NativeDirectory $native -WorkspaceBatchReview
# Repeat reverse direction and current library/comparison/export/keyboard/scaling/
# lifecycle/SimpleWorkspace cases, in fresh serial private-desktop directories.
& "$native/RetargetAcceptance.exe" --motion-controls $sourceModel $target $freshRoot
```

Final `*-final` receipts/captures live under ignored local
`artifacts/Fighterport-workspace25`. The qualification seal records source blobs,
owned binary/build logs, receipt/image hashes and tested/published commit. Generated
builds, local sources, settings, captures and private paths are excluded from Git.
The launcher selects this qualified package; Run.ps1 -Check opens no desktop window.
Source publication uses DoctorFlux author/committer identity and contributor checks.

Development findings: immediate drawer opening could race ongoing native control
setup and is now deferred until idle. Native expected results must cross the same
snapshot/decode boundary as original Next, without relaxing curve comparisons.
Image review caught incorrect wide docking order and omission of the new compact
drawer from offscreen composition. These were corrected with actual Z-order
assertions and full native-control composition; drawer space/actions and the batch
modal's 200% captions were adjusted and reviewed. Development runs are excluded
from accepted totals.

## Limits and next increment

Review approval is a user's session decision, not proof of general contact/game
quality. The complete native editor, original Next and source-local protected roots
remain intact. One import Undo and native edit Undo retain their original owners.
No persistent review approval, new project format, automatic slot filling, solver,
new adapter or skeleton conversion is included. Physical input, monitor DPI, human
usability, broad contacts and game acceptance remain unverified. Preview.26 adds
verified atomic export of the approved set after this source is published.
