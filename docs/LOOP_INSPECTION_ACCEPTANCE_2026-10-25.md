# Loop and seam inspection — 25 October 2026

Preview.27 (`2.0.0-preview.27-loop-inspection`) extends published preview.26
`18d30965d4e0d93f2377652e588d331fc9873013`. This is a bounded increment inside
the ONE incomplete G1/M2 milestone.

## Behavior and preservation

**Inspect loop / seam** in Preview and fix opens a read-only drawer for the
selected project-owned CHR0 and current native target. Imported donor-free clips
work too. The header identifies the project, exact target resource and animation,
frame count, stored loop flag and separate native preview-loop setting.

Opening and Refresh use the normal native pause path without seeking. Only
**First**, **Last** and **Closing frame** explicitly move the existing playhead.
For N frames these are native display frames 1, N and N+1; closing is available
only for stored-loop clips with safe samples. Frame 0 stays bind pose. Explicit
closing evaluation does not change ordinary native playback's existing final
fractional interval or wrap behavior.

The table reports First → Last and, for stored loops, Closing → First local
translation distance, shortest orientation difference in degrees and maximum
absolute scale-component difference. Details retain signed XYZ translation/scale
changes, raw Euler changes including full turns, all sampled local transforms and
authored boundary incoming/outgoing values and tangents. Interpolated boundaries
identify their native neighboring keys. Values use native units/degrees/frames;
the preview FPS does not supply a clip timebase. These are observations, not
automatic quality scores, root-travel errors or smoothing recommendations.

Sampling starts from each authentic native bind transform and overrides only
channels containing keys, using native sparse interpolation, loop limits and
incoming-side paired-key evaluation. Unmatched tracks, ambiguous native bindings,
malformed keys and non-finite data are explicit. Ambiguous/unsafe samples disable
frame jumps rather than silently selecting a substitute. A two-million-authored-key
limit is checked before copying samples. Protected motion controls are classified
only from the current valid reviewed binding; otherwise classification is
unavailable. Remaining valid bindings say Other bones without inferring anatomy.

Resource/selection changes, authored edits, Undo/Redo and control-binding changes
clear measurements immediately. Explicit Refresh is required. Scrubbing/playback
never recompute diagnostics, hash/load files or run Next. Reports contain only
internal numeric/string observations of the existing native data. No persistent
inspection state, project-format migration, scene rebuild, renderer, document
owner or Undo stack is added.

The drawer hides competing panes while retaining the review list, library state
and prepared comparison. Close and focused Escape recover the invoking control
or the native viewport. Focus editor closes inspection/comparison. F6/Shift+F6,
outer Tab/Shift+Tab, visible focus, scroll recovery and existing compact/wide
100–200% application text sizing apply. Busy/control-setup/unfinished native edit
guards prevent inspection from implicitly committing pending work. Disabled native
capture modes cannot enter their save path through inspection.

The full BrawlCrate Model Previewer, original Next retargeter, native Undo and
source-local protected-root policy are retained. No retargeting numerical policy
or native animation writer is changed in this increment.

## Fresh background qualification

The final cohort passes **2,606 scoped assertions across 17 runs**. All **115
fresh PNGs** were inspected, including native models, selected body-local details,
authored key/tangent text, compact/wide drawers, scaled actions and focus recovery.
Private-desktop receipts record no active-desktop windows, input injection or
screen copying. Application, controls and root-policy builds pass.

| Final scope | Assertions |
| --- | ---: |
| Loop inspection, both authentic rig directions | 185 + 192 |
| Verified set export, both directions | 152 + 152 |
| Batch review, both directions | 66 + 66 |
| Library organization, both directions | 40 + 40 |
| Linked comparison, both directions | 40 + 40 |
| Verified single export | 23 |
| Keyboard / scaling / lifecycle | 174 + 410 + 37 |
| Native workspace and original Next workflow | 61 |
| Source-local protected controls, both directions | 464 + 464 |
| Total | 2,606 |

The dedicated `-WorkspaceLoopInspection` case first runs original procedural
numeric fixtures. Expectations use independent Hermite polynomials, double
rotation matrices and frozen authored constants. Comparison tolerances were
recorded before implementation: 0.0002 native translation units, 0.00001 scale and
0.01 orientation degrees. These are numerical equivalence tolerances, not quality
scores; no tolerance or timeout was relaxed.

Fixtures cover nonneutral bind fallback, absent/sparse channels, native N+1 loop
interpolation, paired incoming/outgoing keys and tangents, one/two-frame clips,
stored loops/nonloops, explicit/interpolated closing samples, full 360/720-degree
turns, nonuniform/zero/negative scale, case-insensitive native bindings, duplicate
bindings, unmatched tracks, invalid indices/non-finite data and early key-budget
rejection. A real procedural parent/child hierarchy separates protected parent
travel from body-local measurement.

Both authentic rig directions use favorite-filtered Batch / Fit through original
Next, then real native body edits, cross-clip Undo/Redo and exact session approval.
Independent normal native viewport evaluation checks endpoint translation/scale
per bone. Opening/Refresh preserve exact target/clip/bone ownership, frame, local
pose, child identities, content, Undo, dirty state and approvals. Explicit jumps
retain the ordinary native preview/playhead behavior. Stored-loop metadata and
preview-loop differences, control/resource changes, pending numeric text and busy
guards are exercised. After invalidation, table/details/jumps stay cleared until
explicit Refresh; scrub/playback counters prove no analysis, hashing, loading or
baking. Read-only row/pair navigation selects no native bone and never seeks.

Donor-free native v5 fixtures use actual target Hips identities with 0/360/720
degree keys, authored boundary tangents, protected travel from the reviewed binding
and an unmatched track. They preserve exact keys/metadata/Undo/approval through
inspection, precise export before Save, project Save and fresh reopen. A clean
saved/reopened project stays clean on Refresh/Open. Original Next results also
reopen with exact authored content and fresh inspection, without persistent reports
or inherited session review. One import Undo removes only the procedural fixture.

The four compact/wide 100%/200% combinations exercise the actual frontmost drawer,
F6/Shift+F6, Tab/Shift+Tab, focused Escape, Close recovery and Focus editor. Existing
review lists, library browsing and prepared comparison survive pane changes.

Capture review caught and corrected the scaled selector's cached row height and
DataGridView selection/current-cell event ordering: Details now uses the newly
committed exact bone. Superseded development cohorts are excluded. The final
application and engine binaries are identical across the accepted runs;
the controls harness was rebuilt only to stage the final readable caret/camera
captures. Final loop runs are `loop-*-r5`; the other fifteen runs are `*-r4`, under
ignored local `artifacts/Fighterport-workspace27`. Independent image-review
manifests contain exact paths/hashes for all 115 images. The qualification seal
records source blobs, owned binaries, build logs, receipts, images and the
tested/published commit. Assets, captures, settings and generated binaries remain
excluded from public source. The launcher selects this qualified package;
`-Check` opens no application window.

Commands (tool/fixture paths supplied locally and excluded from public source):

```powershell
./scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
& $msbuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $sourceModel -OutputDirectory $fresh -NativeDirectory $native -WorkspaceLoopInspection
# Repeat reverse direction; then affected WorkspaceSetExport/WorkspaceBatchReview/
# WorkspaceLibrary/WorkspaceComparison/WorkspaceExport/WorkspaceKeyboard/
# WorkspaceScaling/ProjectLifecycle/SimpleWorkspace cases, serially in new directories.
& "$native/RetargetAcceptance.exe" --motion-controls $sourceModel $target $freshRoot
# Repeat reverse root-policy direction.
```

## Remaining gates

Two-clip transitions, velocity scoring, automatic smoothing, retiming, loop-flag
changes and playback corrections remain separate work. This inspects endpoint
poses and authored boundary data only. Physical input, monitor/per-monitor DPI,
human usability, broad contact quality and actual game acceptance remain unverified.
Application text sizing does not qualify physical DPI. G1/M2 stays incomplete.
