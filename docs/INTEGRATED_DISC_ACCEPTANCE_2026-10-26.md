# Integrated Brawl disc library — 26 October 2026

Preview.36 (`2.0.0-preview.36-integrated-disc`) replaces the external DolphinTool
route and character picker with application-owned read-only ISO/WBFS indexing.
One Add includes the complete native fighter subtree and publishes one catalogue
transaction. The complete BrawlCrate Model Previewer, original Next, native Undo
and current reviewed source-local root/boundary policies remain intact. No project
schema, bind data, solver, native playback or process DPI configuration changes.

## Workflow and association

Choose **Library → Add Brawl...**, select the disc or extracted Brawl folder, and
press **Add to library**. Fighterport inventories the whole roster automatically.
The build label is editable and the native header supplies the observed region,
game ID and revision. No separate executable, download or key-file setup is used.
Original images and native sources remain read-only.

The supplied single-file WBFS and independently converted ISO identify RSBE01,
revision 1. The full fighter subtree is 986 packages / 694,344,766 bytes across
46 character directories plus shared Fighter.pac. It contains 2,688 model
resources and 25,934 animation resources, including 17,702 CHR0 animations.
13,251 skeletal resources have verified automatic original-model associations;
45 character groups contain a linked native Wait1. This does not make every
channel, article or ambiguous source a supported retargeting input.

Native MotionEtc family/container identity selects costume-00 body candidates;
exact model identity disambiguates multi-model containers. Track binding and
native resource/skeleton hashes provide independent evidence. This excludes Spy
and copied-ability variants merely sharing a folder. Same-resource-scope article
or effect animations can link when every authored track binds to one unique
original model. Unresolved resources remain indexed with explicit association
review status; no sibling model is silently chosen. Native costume eligibility
still checks source/build/family ownership and complete hierarchy/bind fingerprints.
No character/action-specific numerical exception or bind normalization is added.

Select an available animation and costume, then **Preview Original**, **Open model
+ animation**, or **Use on This Model**. The first is read-only; the second creates
an independent owned native target using existing unsaved handling; the third
runs original Next on the current target. Comparison retains the recorded exact
costume independently of subsequent browsing. Original companion resources stay
in their native containers. Browse/search/costume enumeration and prepared playback
perform no disc extraction, source decoding, hashing or retarget baking.

The internal reader and its limits, reference audit, provenance and legacy-read
compatibility are documented in [the integrated reader record](INTEGRATED_DISC_READER.md).
Decoded native package graphs have separate disposed lifetimes. Heap compaction
is confined to bounded memory checkpoints during explicit background indexing.
This addresses 32-bit address-space pressure from full-roster decoding.

## Verification

The final cohort passes **4,550 checks / 26 runs**. Its **157 fresh native PNGs**
were actually reviewed in 46 labelled overview sheets; the new source dialog at
100%/175% and wide 175% source/target view were additionally examined at full size.
Original image hashes and the review method are retained in the local ledger.
Overview inspection does not certify every small label or motion/contact quality.
The existing compact large-text panels scroll/truncate; optional drawers can cover
much of the target. Focus editor restores the original viewport.

Twenty-one unchanged-component regression cases used the frozen final4 build.
The only subsequent production-source difference is the native-package cleanup
checkpoint in BrawlGameSource.cs (fixed batches and pending finalizers rather than
managed-heap pressure). All five affected reader/library/backend cases were then
repeated against the final5 package, with large-image I/O serialized for the final
UI run. Both source/package manifests and the single-file difference are recorded;
all native/core retargeting, fitting, rendering, Undo and keyboard sources are
identical between these builds. No old repository's evidence is used.

| Case | Checks |
| --- | ---: |
| reader-final5 | 96 |
| library-target-final5 | 39 |
| game-library-final6 | 56 |
| core-final4 | 380 |
| roots-mario-final4 | 628 |
| roots-snake-final4 | 628 |
| scaling-final4 | 356 |
| next-layout-final4 | 69 |
| next-layout-reverse-final4 | 69 |
| performance-final4 | 47 |
| keyboard-final4 | 210 |
| library-final4 | 52 |
| loop-final4 | 185 |
| batch-review-final4 | 182 |
| set-export-final4 | 152 |
| comparison-final4 | 40 |
| comparison-reverse-final4 | 40 |
| reference-moving-final4 | 231 |
| boundaries-final4 | 277 |
| boundaries-reverse-final4 | 264 |
| transitions-final4 | 80 |
| workflow-final4 | 61 |
| lifecycle-final4 | 37 |
| export-final4 | 23 |
| disc-wbfs-final5 | 174 |
| disc-iso-final5 | 174 |

Both complete disc containers independently yield all 986 matching native package
hashes. The WBFS pass also matches the 41 previously independently extracted
Mario/Snake files. Each full-disc pass includes 174 checks. The independent reader
fixture suite includes 96 checks. The original image hashes remain unchanged.


Procedural cases use an independent synthetic big-endian FST/partition/WBFS writer,
including AES-encrypted hashed payloads, plain hashed and raw layouts. They verify
exact payloads across cluster boundaries and noncontiguous WBFS sectors, aligned
later-slot records, repeated reads, all-file and scoped extraction, shared fighter
files, original immutability and cancellation. Malformed ranges/paths/maps, missing
required allocations, damaged payload/hash/H3 data, unsupported ticket/key types,
multiple discs and split companions reject without external-reader invocation.

The actual WBFS/ISO routes are compared against independently known native package
hashes; complete inventories, exact native models/clips and original bytes are
checked. Previous externally indexed catalogues reopen with the old executable
path unavailable. Full source identities, DATA paths, costume models, hashes and
native timing persist without a schema migration. Repeated registration preserves
known IDs and catalogue bytes; offline repair and cancellation retain associations.

The private-desktop workflow covers one Add with no character picker, full roster
and shared resources, source/costume preview, exact Next output, retained comparison,
repair, independent target opening, body editing/Undo/Redo, save/reopen and verified
export. Supported compact/wide and 100%/175% layouts are captured fresh. Native
workflow, root policy, fitting, review, export, library, comparison, keyboard,
scaling and lifecycle regressions are separate cases in the same cohort.

Superseded probes remain preserved and excluded. One restricted-desktop launch was
denied before opening a window; an early full command-line index hit 32-bit memory
pressure before the per-package lifetime fix. A superseded overlapping large-image UI run was cancelled to serialize drive I/O.
An earlier large-catalogue regression
exceeded its old three-minute watchdog; the dedicated case now has six minutes,
with numeric gates unchanged. The source and package are frozen for the final cohort.

Reproduction uses local paths supplied at runtime:

```powershell
./scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $native
# Build ControlsAcceptance, LibraryAcceptance and RetargetAcceptance Release/x86.
& "$native/LibraryAcceptance.exe" --disc-reader-cases --output $freshOutput
& "$native/LibraryAcceptance.exe" --disc-image $wbfs --all-characters `
  --output $freshOutput --compare-index $independentIndex
& "$native/LibraryAcceptance.exe" --disc-image $iso --all-characters `
  --output $anotherFreshOutput --compare-index $verifiedWbfsIndex
./scripts/Test-BackgroundCapture.ps1 -Model $model -Motion $motion `
  -WorkspaceGameLibrary -DiscImage $wbfs -NativeDirectory $native `
  -OutputDirectory $freshOutput
```

Capture work stays serialized and low priority, on private desktops with the
shared offscreen renderer. No active-desktop capture or OS input injection is used.
Physical input, monitor DPI, human usability, broad contacts and actual game
acceptance remain unverified. RVZ, split WBFS, other game/build/image combinations,
new adapters, repacking and disc writing remain separate. Historical foreground
harnesses are not acceptance for the current workspace. ONE G1/M2 remains incomplete.
Only verified source is published to public Fighterport/main using DoctorFlux
attribution; assets, settings, caches, captures and binaries stay excluded.
