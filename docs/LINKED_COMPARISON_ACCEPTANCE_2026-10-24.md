# Linked source comparison — 24 October 2026

Preview.23 (`2.0.0-preview.23-linked-comparison`) follows verified-export commit
`00739db23ed957353aa2a62720db2efe12bcea67` on public Fighterport/main.
This is the second approved increment within the ONE incomplete G1/M2 milestone.

## Behavior and boundaries

Preview and fix includes **Compare source**. A separate read-only native renderer
appears beside the complete editable native target. Its source comes from the
selected result's saved library reference, never from the library browser's
current clip. Opening comparison hides the competing task/library panes. Workflow
or Library can temporarily show their pane while retaining the loaded comparison;
returning to Compare source restores that same recorded source. Close comparison
and Focus editor release it. F6/Tab, focused-pane Escape and focus recovery reuse
the existing workspace rules. Narrow windows retain native horizontal scrolling;
Focus editor restores full room for the original editor.

The existing verified source loader checks exact immutable clip/model identities,
revision, resource/container hashes, approved rig/rest and native timing evidence.
The reference must agree with the result's pinned source reference. Preparation
loads once and holds source file locks. Every referenced source texture is
prepared through the existing native loader before playback, including materials
outside the initial camera frustum. Cancellation cannot publish a late decode;
it is disposed. A target/project/content/rig change during loading invalidates
publication. Missing/changed sources, absent provenance or unrecorded duration
changes explain unavailability while native target editing stays available.

The native evaluator now exposes its actual sampled frame to observers. Comparison
uses only prepared native source curves on that event, including fractional
playback samples. Identity transfers use corresponding frames. Recorded timing
uses source index `Start + (End-Start)*(targetIndex/(Frames-1))`; single-frame
results use Start. The rest frame uses each authentic native bind pose. No
alignment, substitute source, skeleton conversion or camera normalization is
invented. Each pane keeps its own native camera. Loading, hashing, baking, source
analysis and retargeting do not occur on playback/scrubbing callbacks. Target
selection/timing changes invalidate the cached link rather than silently switching
its source. Original target playback and its clock remain in charge.

## Background qualification

Final qualification: **1,701 scoped assertions / 49 final PNGs individually
reviewed in labelled galleries**. Nine final runs passed:

| Case | Assertions |
| --- | ---: |
| Comparison, authentic rigs in both directions | 40 + 40 |
| Verified export | 23 |
| Keyboard / scaling / lifecycle | 162 + 410 + 37 |
| Original Next/native workflow | 61 |
| Source-local roots, both directions | 464 + 464 |

Local receipts/captures are `artifacts/Fighterport-workspace23/*-r5/`; the local
qualification seal retains exact source/blob/binary/receipt/image hashes. Earlier
and failed development runs are excluded. The launcher selects the qualified
preview.23 package.

Dedicated `-WorkspaceComparison` cases run both authentic rig directions, real
original Next revisions and body edit/Undo/Redo. Independent expectations check
identity samples, fractional scrubbing, rest, recorded trim/timing fits,
single-frame results and procedural looping on authentic rigs. Diagnostic counts
prove playback/scrubbing perform no native texture loading, file hashing, source Open, retarget capture
preparation or bake. Cases verify exact target clip/bone/frame/content/Undo,
read-only source selection, panel/focus recovery, source-browser changes, missing
originals, altered actual source bytes, changed saved hashes and missing timing.
The ordinary Save/Open path restores a source-bound result and comparison.

A controlled late-decode fixture uses the actual shared source-work dialog and
Cancel handler: source decoding completes after cancellation, is disposed before
publication, and releases file locks. Fast cached comparison loads can finish
before a timer can click Cancel; that is not claimed as a physical-input test.
Fresh relevant export, native workflow, keyboard, scaling, lifecycle and
bidirectional source-local root-policy regressions run on the final build.
Captures include the actual source and target native viewports, rendered privately.

Commands (local fixtures/output roots supplied outside the public repository):

```powershell
./scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
& $msbuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $sourceModel -OutputDirectory $freshOutput -NativeDirectory $native -WorkspaceComparison
# Repeat the opposite direction and relevant WorkspaceExport, WorkspaceKeyboard,
# WorkspaceScaling, ProjectLifecycle and SimpleWorkspace cases in fresh outputs.
& "$native/RetargetAcceptance.exe" --motion-controls $sourceModel $target $freshRootOutput
```

Development findings: a hidden native lazy tree has no created leaves until its
drawer opens; the browser-change test now opens that drawer. Initial captures
used an unframed test camera; the harness frames the target before opening
comparison, preserving the feature's camera behavior. A source-dialog cancellation
race was guarded and tested with controlled late completion. Failed development
runs remain local and are excluded from totals. A reverse setup timeout was
resolved by settling startup and initializing the test at frame 1 before moving
to frame 3; timeout/numeric bounds were retained. Image review also caught the
workflow summary displaying a newly browsed clip; visible comparison now names
the result's recorded source. The renderer audit exposed lazy initial texture
loads; eager source preparation and a native texture-load counter now qualify
that playback boundary. Numeric tolerances are unchanged.

## Remaining gates

Initial comparison covers the existing library-bound Brawl CHR0 workflow only.
Physical input, monitor DPI, human command discovery/usability, broader contact
quality and game acceptance remain unverified. No solver, original Next algorithm,
root policy or project schema changed. Full BrawlCrate editor and native Undo are
retained. Favorites/filters are the next authorized increment; G1/M2 remains open.
