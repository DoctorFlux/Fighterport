# Direct game sources — 26 October 2026

Preview.38-direct-game-sources keeps game assets in the original ISO/WBFS or
install/SD folder. The library saves listing metadata and user preferences;
it does not retain another extracted fighter tree. This continues the single
incomplete G1/M2 milestone after the verified preview.37 workspace increment.

## Source ownership and loading

Adding a Brawl disc inventories native fighter resources through one temporary
package at a time. Every package is disposed and removed before the next is
read. Registration independently streams the indexed package bytes from the
original image and verifies their hashes before its metadata commit.

Preview, costume selection, transfer and editable-target opening load only the
selected native model and motion packages. The complete native parser needs
file-backed maps, so these packages occupy a disposable temporary directory
for their lease. Closing/cancelling a load, rejecting changed payloads, replacing
the source, and closing or disposing a window release those files. Original image
handles stay read-only and locked throughout a lease. Normal editable target
projects still own their independent working model/animation assets.

Existing catalogue entries read their recorded original image and virtual paths,
even when their historical extracted cache paths are unavailable. Old caches are
left untouched. Source identity, native resource keys, hashes, original rig/binds,
costume compatibility, timing and recorded comparison provenance stay exact.
Missing or changed sources fail explicitly; no substitute model is chosen.

Folder sources continue reading their native files directly. Multiple named
Project M/Project+ installations remain independent, including byte-identical
copies. Unknown game/format locations remain path-only; they are not new adapters.

Prepared preview and comparison playback do not open discs, read additional
packages, hash assets or run the retargeter. The complete BrawlCrate Model Previewer,
original Next, native Undo and existing root/boundary policies remain intact.
This increment changes no curve solver, skin weights, skeleton or project schema.

## Resource-aware metadata

The arbitrary global 50,000-animation, 8,192-model/mount and 256-location ceilings
are removed. The existing `int` search API remains compatible. Large metadata
generations use gzip around the unchanged schema-1 XML payload; smaller catalogues
remain ordinary XML. Legacy XML and compressed temporary handoffs are readable.
No metadata conversion occurs merely from browsing an old catalogue.

Capacity follows available process address space, physical memory, commit headroom
and free disk space. A 256 MiB reserve protects native editing; metadata budgets
allow six bytes of working memory per serialized byte and use at most half of
currently free physical memory. Writes keep a 64 MiB disk reserve. Bounded writers,
XML quotas, prohibited DTDs, gzip integrity checking and the existing atomic
generation publication preserve the old catalogue when an operation cannot fit.
This is graceful refusal under resource pressure, not unlimited-RAM support.

Repeated metadata strings share storage within a snapshot; there is no permanent
global string-intern pool. Mutations retire obsolete search/costume snapshots
before independently reading current/recovery generations. Counts, filtering,
paging, stale-result rejection and lazy tree population retain their existing
behavior. Native per-package, frame, batch and key budgets remain separate.

## Background evidence

**2,980 checks / 20 accepted runs / 143 visually reviewed native PNGs.**
Every accepted capture received labelled overview inspection; full-size comparison
and instance-manager views were also inspected. All final cases use the same
source-frozen final1 package; superseded probes are excluded.

| Background case | Checks | PNGs |
|---|---:|---:|
| game-library | 56 | 14 |
| capacity | 21 | 0 |
| direct | 60 | 11 |
| source-fit | 45 | 11 |
| game-sources | 62 | 10 |
| library | 52 | 11 |
| comparison | 40 | 5 |
| batch-review | 182 | 9 |
| set-export | 152 | 8 |
| workflow | 61 | 11 |
| keyboard | 210 | 7 |
| scaling | 356 | 11 |
| lifecycle | 37 | 6 |
| library-target | 39 | 8 |
| export | 23 | 4 |
| performance | 47 | 8 |
| loop | 185 | 9 |
| roots-mario | 628 | 0 |
| roots-snake | 628 | 0 |
| disc-reader | 96 | 0 |

An independent audit additionally compares all **29,608 native metadata records**
against the previously verified extracted-byte inventory: 986 package hashes,
2,688 model/rig records and 25,934 resource/timing/association records match exactly.
The four-roster compact catalogue is approximately 12 MB; the accepted capacity
run records its measured peak working set locally, not a universal memory bound.

The dedicated direct-source case deliberately replaces every extracted-cache path
with a nonexistent path in an isolated copy of the verified catalogue. Mario,
Sonic and Snake Wait1 reopen from the image with exact recorded package/resource
hashes. Sixty prepared samples perform no source I/O or solving; read locks,
bounded mid-read cancellation, bad payload expectations, missing originals and
temporary cleanup are exercised independently.

Actual source-pane controls then preview Sonic, render geometry at compact/wide
100%/175% text, explicitly handle the existing skeleton-mapping review, and run
original Next onto Mario. Linked comparison, an alternate Mario costume opened
as an independent editable target, native body Undo/Redo, failed/cancelled saves,
Save/reopen and verified CHR0 export are checked through existing application paths.
The mapping review is a background fixture action, not human usability acceptance.

Four complete-roster metadata clones cover **103,736 clips, 10,752 models and
3,944 packages** with independent identities. These are metadata capacity fixtures,
not authentic Project M game builds. Exact source scopes, rename, removal/Undo,
memory quotas, compact metadata integrity and failed publication are checked
without loading or copying native assets. Numeric headroom expectations were
frozen independently before the capacity implementation.

Superseded probes are excluded: one waited for the existing mapping-review dialog;
another exercised safe low-memory refusal during Undo and exposed retained search
snapshots; a direct source/target run exposed the window-disposal cleanup gap.
The mapping harness, metadata retention and idempotent pre-control-disposal cleanup
were corrected before final qualification. Existing numeric solver gates were not
relaxed. The sole pre-existing native build warning is CS1690 in loop inspection.

Local evidence under `artifacts/Fighterport-direct38` is excluded from publication:
source/package manifests, receipts, logs, memory observations, exact native exports
and projects, isolated catalogue fixtures, and fresh offscreen captures with a
visual-review ledger. The restored user Brawl registration retains its prior exact
source/model/clip identities; test catalogues and transient files are isolated.

All UI tests use serialized private desktops and the shared offscreen renderer,
without OS input injection or active-desktop capture. Physical input, monitor DPI,
human usability, actual Project M builds/game execution and broad contact quality
remain unverified. Existing Sonic fitting-worker influence and fractional-fit
limitations, plus native game-controlled alternate-mesh visibility, remain open.

## Reproduction

```powershell
./scripts/Build.ps1 -NativeDirectory $native -DotNet $dotnet10
# Build ControlsAcceptance, LibraryAcceptance and RetargetAcceptance as Release/x86.
./scripts/Test-BackgroundCapture.ps1 -Model $marioModel -Motion $marioMotion `
  -WorkspaceDirectSources -LibraryIndex $verifiedLegacyIndex `
  -NativeDirectory $native -OutputDirectory $freshDirectOutput
./scripts/Test-BackgroundCapture.ps1 -Model $marioModel -Motion $marioMotion `
  -WorkspaceGameCapacity -LibraryIndex $verifiedLegacyIndex `
  -NativeDirectory $native -OutputDirectory $freshCapacityOutput
./scripts/Test-BackgroundCapture.ps1 -Model $marioModel -Motion $marioMotion `
  -WorkspaceGameLibrary -DiscImage $originalImage -LibraryIndex $verifiedLegacyIndex `
  -NativeDirectory $native -OutputDirectory $freshDiscOutput
```
