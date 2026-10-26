# Source, fitting and named game installations — 26 October 2026

Preview.37 (`2.0.0-preview.37-source-fit-workflow`) keeps the full native
BrawlCrate Model Previewer, original Next, native Undo and current source-local
root/boundary policies. This is one bounded continuation of incomplete G1/M2.
No solver, skeleton, skin-weight, native playback, DPI configuration, project
format or catalogue schema is replaced.

## Everyday workflow

**Add game...** accepts a saved source, disc/file, or game/install/SD folder.
Brawl ISO/WBFS and Brawl-family folders use the integrated reader. Project M
and Project+ use their own selected game/mod categories and can select the SD
root, projectm/pf, private/wii/app/rsbe/pf, or exact fighter folder as appropriate.
All supported native fighters are inventoried without a character picker.
Other games/formats can have locations saved now, explicitly as unindexed
path-only entries. A saved path is not decoding, writing, repacking or game support.

**Open saved library** reuses an indexed source without extraction. The source
picker remembers its game/build, kind and exact path. **Saved games / paths...**
opens one compact list with instance name, game/build, file/folder kind, location
and state. Select a row to rename it, edit its path, remove it or undo the last
library change. The same manager is available through the picker's **Manage...**.
Indexed game/build identities remain fixed; the instance's display name is editable.

Brawl and each Project M installation have separate categories. Multiple mod
installations keep different source IDs even with the same build label, character
names, native resource keys, or identical files. Names are labels, not identity.
Instance-scoped queries filter by exact source ID before counts/paging/tree
population. Costume choice stays within the same original source and reviewed
rig/bind family. Renaming does not redirect recorded comparisons or retarget recipes.

Characters show their animations directly; the costume selector supplies the
model variant. **Load as source** loads the small read-only original preview.
**Open as target** opens the large editable native target through existing
unsaved-change handling. **Use on This Model** uses original Next on the current
target. Source loading explicitly initializes its own context, prepares native
textures while paused, and frames its own perspective camera after layout.
Selecting another clip never leaves an unrelated loaded source pretending to be
the selected input. Playback/scrubbing does not load sources, read discs or bake.

In **Fit start / end...**, preview and review the chosen boundaries, then
**Apply to current animation**. An owned single-clip animation retains its exact
ID, native object, name, frame, target/bone selection and original Next provenance.
The fitted curves and portable recipe replace that clip's current working state
as one command on native Undo. **Save** preserves them in the project; verified
CHR0 export also works before project Save. **Save as a new animation** remains
available; multi-clip native containers use that route to avoid ambiguous
asset-level recipe ownership. Original source files are never overwritten.

The inherited global catalogue bounds remain in this preview. Full multi-roster
capacity and direct on-demand disc loading are the next authorized continuation;
the small SD-layout fixtures do not qualify arbitrary complete mod libraries.

## Source edits and protections

Instance names use the existing source label. Path-only entries can change their
game/build and file/folder location. Repointing an indexed disc verifies its exact
whole-image hash/size through the existing repair path. Repointing an indexed
Brawl-family folder resolves the actual fighter subtree and independently verifies
every registered package before one metadata commit. Verified files remain locked
through publication. Verification is bounded to 10,000 packages / 4 GiB, uses a
reused streaming buffer, checks cancellation, and rejects stale catalogue generations.
Wrong content, cancellation or publication failure retains the prior linked paths.

Removing an indexed source unregisters only its owned library entries. Other
sources stay registered, and any retained clip whose explicitly linked model was
removed becomes unassociated rather than silently receiving a substitute. Source
files, extracted caches and saved native target projects remain intact. The existing
library Undo restores exact identities. This does not add a second document Undo.
All remembered original file/folder locations retain output-write protections.

## Background evidence

**3,410 checks / 21 accepted runs / 157 visually reviewed native PNGs.**
Labelled overviews cover every accepted capture; full-size views additionally
check the fitting review and the new source manager/chooser.

| Background case | Checks | PNGs |
|---|---:|---:|
| next-layout-reverse-final4 | 69 | 5 |
| library-final4 | 52 | 11 |
| comparison-final4 | 40 | 5 |
| library-target-final4 | 39 | 8 |
| scaling-final4 | 356 | 11 |
| keyboard-final4 | 210 | 7 |
| workflow-final4 | 61 | 11 |
| source-fit-final6 | 45 | 11 |
| next-layout-final6 | 69 | 5 |
| performance-final5 | 47 | 8 |
| game-sources-final5 | 62 | 10 |
| game-library-final5 | 56 | 15 |
| boundaries-final1 | 277 | 8 |
| boundaries-reverse-final1 | 264 | 4 |
| batch-review-final1 | 182 | 9 |
| loop-final1 | 185 | 9 |
| roots-mario-final1 | 628 | 0 |
| roots-snake-final1 | 628 | 0 |
| transitions-final2 | 80 | 10 |
| export-final2 | 23 | 4 |
| lifecycle-final2 | 37 | 6 |

The focused source/fit case exercises the actual displayed-source transfer,
current-clip application, stale-reference rejection, exact protected controls,
native Undo/Redo, subsequent body-edit Undo, verified export before Save, fresh
project reopening and failed project publication. Exact curves, tangents, flags,
timing, recipe and original Next provenance are independently checked. XML
collection defaults are normalized through the actual serializer for provenance
comparison; native numeric expectations are not relaxed.

The saved-source case covers multiple discs and an install folder for a future
game, idempotent registration, missing paths, remembered unsupported Brawl RVZ,
no-decoding/index-reuse counters, compact management, rename/edit/remove/Undo,
name conflicts and unchanged target document state. Two SD-layout fixtures use
authentic native Mario resources; one independently changes Wait1's loop content.
They retain the same character/build labels while proving distinct Project M
instances, exact queries, costume isolation, verified relocation, cancellation,
preview resolution and removal/Undo. A third byte-identical folder remains a
separate source. These are Brawl-format SD-layout fixtures, not actual Project M
game acceptance or evidence for every mod build.

The new original preview has an independent geometry-pixel check against the
same viewport with polygons hidden, so a non-null lease or updated label alone
cannot pass. A pre-existing incompatible camera is reset on loading. Compact/wide
100%/175% source views and source dialogs are captured fresh. Relevant library,
source/target, scaling, keyboard, comparison, lifecycle, review, loop, fitting,
export and root-policy cases are included in the recorded cohort.

Local evidence lives under `artifacts/Fighterport-workflow37` in the parent
workspace and is excluded from public source: per-run receipts/logs, immutable
input hashes, precise native exports/projects, source/package manifests, accepted
cohort list, actual captures and visual-review ledger.
The cohort is explicitly mixed-build: unchanged boundary, review, loop and root
paths use final1; original transition/export/lifecycle cases use final2; current
query, reverse layout, keyboard, scaling, comparison and workflow cases use
final4. Final6 repeats the focused source/fit and Next-layout cases after two
remaining hints were changed to the visible Load as source caption. Final5 repeats the affected source dialogs, instance management and real
WBFS workflow after button-sizing fixes, plus performance. Source manifests
record these boundaries. Between final4 and final5, production changes are only
font-measured button minimum sizes in the two new source dialogs; additional
test assertions and the performance watchdog do not change product behavior. Superseded probes are retained
and excluded. The first final performance run exhausted its three-minute harness
watchdog after passing the initial preservation checks. Its repeat retains all
31 observations, eight warmups and original correctness gates, with a bounded
ten-minute process watchdog; this is not a relaxed performance or latency gate.
Early routing supplied donor motion to six same-character regression
cases; those were repeated with authentic target motion. Focused probes also
exposed grid-selection synchronization and large-text button clipping, both fixed.

Reproduction uses the existing background harness and local inputs:

```powershell
./scripts/Build.ps1 -NativeDirectory $native -DotNet $dotnet10
# Build tests/ControlsAcceptance with Release/x86 into the same native directory.
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $donorMotion `
  -RetargetSource $donor -Clip AttackS3S -TargetMotion $targetMotion `
  -WorkspaceSourceFit -SourceCharacter snake -LibraryIndex $verifiedIndex `
  -NativeDirectory $native -OutputDirectory $freshOutput
./scripts/Test-BackgroundCapture.ps1 -Model $marioModel -Motion $marioMotion `
  -WorkspaceGameSources -LibraryIndex $verifiedIndex `
  -NativeDirectory $native -OutputDirectory $anotherFreshOutput
```

All UI evidence uses serialized private desktops and the shared offscreen
renderer, without active-desktop capture or OS input injection. Physical input,
monitor DPI, human usability, universal contact quality and actual game acceptance
remain unverified. Sonic's optional-worker influence limit and tilt fractional-fit
issue from the separate trials remain open. Native game-controlled alternate-mesh
visibility is not newly qualified here. Publish source only under DoctorFlux
attribution; preserve upstream credits and keep one incomplete G1/M2 milestone.
