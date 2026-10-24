# Favorites and library filters — 24 October 2026

Preview.24 (`2.0.0-preview.24-library-organization`) follows public main
`412b1dfc595c848a2ec9161bd2c1489a54466158`. It completes the third approved
feature increment inside the ONE incomplete G1/M2 milestone.

## Behavior and boundaries

Select a source and toggle **Favorite / Favorited**. **Favorites only** combines
with text search, game/build words, character/model words, availability and an
exact indexed channel. **Clear filters** resets those criteria and search, keeping
stored favorites. All filters apply before result counts, paging and lazy tree
construction. Batch/Fit uses the same current filtered query. Filters expose
normal text editing, ordered Tab navigation, accessible descriptions and tooltips.
Workspace text sizing and scrolling also cover the new controls.

Favorites use exact immutable clip IDs, including when names repeat. They belong
to that library's separate `preferences.json`, not the catalogue or fighter
project. Source repair retaining an exact identity retains its favorite; a new
identity never inherits a favorite by name. Source associations, approval,
revision records, project/catalogue schemas and their Undo are unchanged.

Preferences have schema 1, at most 50,000 exact IDs, and a two MiB bounded read.
Reads hold an ordinary read stream and check length before parsing. Writes use a
unique sibling file, flush it, then atomically replace/publish. Corrupt,
unsupported, oversized or unavailable preferences preserve evidence and leave
ordinary browsing available. Failed writes preserve the prior file and keep
session preferences with an explanation in library details. New background cases
isolate preferences under their own library roots. Concurrent app instances use
last successful replacement; cross-process preference merging is not qualified.

Typed `LibraryQuery` overloads are additive. Original string signatures remain,
including unambiguous legacy `Search(null, ...)` and `Browse(null)` calls. Exact
game/build/character/model-ID/channel predicates are available to callers; UI word
fields narrow game/build and character/model metadata. Metadata search remains
asynchronous, stale generations are rejected, and the native outline still adds
leaves in paced batches. Availability is the existing cached source-health state;
using a source still performs its mandatory full identity/hash verification.

A filtered-out selection loses its actionable selection and transfer/preview/
favorite commands. Filtering does not change the target, its native frame,
selection/curves/Undo, or the retained source comparison. Library and comparison
panes take turns being visible; returning to Compare source restores the result's
recorded original source. No new collection or tag editor is introduced.

## Background qualification

The final preview.24 cohort passes **1,793 scoped assertions**. All **63 final
PNGs** were inspected, including native target/source rendering and the new filter
controls at compact/wide layouts and 100%/200% workspace text. Review confirms
readable filter captions and Clear filters, visible focused Favorited state,
retained read-only source/fully editable target boundaries and real rendered models.
Compact drawers still cover native space while open; scrolling and Focus editor
recover the original controls, as in the existing compact-layout policy.

| Final case | Assertions |
| --- | ---: |
| Library organization, both authentic rig directions | 40 + 40 |
| Linked comparison, both authentic rig directions | 40 + 40 |
| Verified export | 23 |
| Keyboard/focus | 174 |
| Text/layout scaling | 410 |
| Project lifecycle | 37 |
| Native workspace/Next workflow | 61 |
| Source-local protected controls, both directions | 464 + 464 |
| Total | 1,793 |

Final receipts/captures use fresh `*-r2` directories under the ignored local
`artifacts/Fighterport-workspace24` qualification root. `qualification.json`
records source blobs, owned binary/build-log hashes, receipt hashes and reviewed
image hashes, then the tested/published commit. Earlier development and pre-final
runs are excluded. Builds use the local .NET 10 SDK and VS 2022 MSBuild x86; all
three app/controls/root harness builds pass. Private-desktop receipts record
`PrivateDesktop=true`, `OSInputInjected=false`, `ScreenCopied=false`. The local
launcher selects the qualified preview.24 package and passes `Run.ps1 -Check`.
Source-only publication uses DoctorFlux author/committer attribution on public
Fighterport/main; generated outputs and private fixture paths stay excluded.

Dedicated `-WorkspaceLibrary` cases cover actual disk persistence/reload, exact
identities with duplicate names, locked/corrupt/oversized/versioned preferences,
repair, combined filters/counts/paging, clear/reset, hidden selection, newest async
results, real target body edit/Undo/Redo, and retained comparison. An original
10,000-row procedural catalogue checks independent counts and first/last pages,
no native opening/baking during metadata filtering, and actual lazy UI population
of only the first 1,000 leaves. Procedural metadata is not a game-support claim.
Compact/wide cases repeat at 100%/200% text; a scrolled 200% capture reviews the
explicit favorite state and keyboard focus. Relevant current export, comparison,
keyboard, scaling, lifecycle, native workflow and protected-root regressions run
on the final build. Captures include actual native model rendering, not mockups.

Commands from the repository (local paths supplied outside public source):

```powershell
./scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
& $msbuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $sourceModel -OutputDirectory $freshOutput -NativeDirectory $native -WorkspaceLibrary
# Repeat the opposite rig direction and current WorkspaceComparison,
# WorkspaceExport, WorkspaceKeyboard, WorkspaceScaling, ProjectLifecycle and
# SimpleWorkspace cases, always in separate fresh private-desktop outputs.
& "$native/RetargetAcceptance.exe" --motion-controls $sourceModel $target $freshRootOutput
```

Development findings: the oversized-preference fixture exposed an omitted
InvalidDataException recovery branch, which now degrades safely. An initial UI
assertion ran before the existing lazy leaf timer finished despite the count
already being correct; the harness now waits for incremental population. Image
review caught the 200% Clear filters caption truncating; sizing now measures its
full text. Favorite state is explicitly labelled Favorited and keyboard-focusable.
Failed/development outputs remain local and are excluded from final totals.

## Remaining gates

This completes the three requested bounded feature increments. The full native
BrawlCrate editor, original Next retargeter and source-local root-control policy
remain. Human task discovery/completion, physical input, monitor DPI, broad contact
quality, additional game destinations and game acceptance remain unverified and
separate. G1/M2 remains the ONE incomplete milestone; custom collections/tag
editing and new adapters/solvers/conversions remain deferred.
