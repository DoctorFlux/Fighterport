# Brawl disc library and costumes — 26 October 2026

Preview.35 (`2.0.0-preview.35-brawl-disc-library`) completes this bounded source
workflow increment inside the ONE incomplete G1/M2 milestone. Fresh qualification
passes **4,207 checks / 25 runs / 156 inspected native PNGs**.
The complete BrawlCrate Model Previewer, original Next retargeter, native Undo and
current reviewed root/boundary policies remain intact. No engine solver, bind,
project schema or process DPI configuration changed.

## Use the library

1. Open **Library → Add Brawl...**, or **Animation → Add Brawl disc / folder...**.
   Choose an ISO/WBFS or an extracted Brawl game/build folder. Disc files require
   a compatible DolphinTool; the application checks header/extraction capabilities
   and native Brawl identity before full-image hashing or extraction. Folders need
   no external tool.
2. Choose characters and confirm their native source/build association. Preparation
   caches only selected disc character folders and publishes the complete inventory
   atomically. A cancelled character chooser publishes no partial catalogue.
3. Select an available skeletal animation and **Costume**. **Preview Original**
   explicitly loads the exact original model, textures and animation read-only.
   Play and scrub in the small source viewport. Browsing and costume enumeration
   do not load, hash, bake or alter the target.
4. **Open model + animation** opens an independently editable target using complete
   original containers and the existing Save/Discard/Cancel handling. Alternatively,
   **Use on This Model** runs original Next into the current native target. Optional
   start/end fitting remains the existing separate operation.

The detected Brawl native provider and exact source/build/character ownership
bound the costume list. PAC/PCS files retain their separate exact identities;
localized numeric costume names are inventoried too. Automatic alternatives require
identical native model version and complete hierarchy/bind fingerprints. Different
binds appear as needs-review and cannot be silently substituted or normalized.
Direct files require an explicitly approved model/motion association; a matching
character name alone never supplies one. This is no new skeleton conversion.

A selected costume travels into preview, transfer provenance and target preparation.
Recorded comparison resolves the result's exact ModelId, independent of later
library/costume browsing. The current animation association is never rewritten.
Repair includes the selected costume's own mount and accepts only the original
indexed bytes. Refresh invalidates cached costume metadata for a retained clip.

Target opening verifies full container hashes, exact native model and clip keys,
resource hashes, authentic rig and timing before replacing anything. Ambiguous
model paths reject rather than choosing a sibling. Selection/project dependencies
are checked after preparation and immediately before opening. Full companion
animations and textures stay available in the native editor. Temporary-source
metadata is carried to the replacement window without shared registration; it is
not a persistent project field. Reopening a project still explains unavailable
session-only provenance without blocking editing.

## Independent evidence

Actual single-file Brawl WBFS and a genuinely converted ISO both identify RSBE01
revision 1. Mario and Snake yield 1,274 native animation resources, with identical
native extracted-file hashes between disc containers. This resource count includes
channels that remain separately qualified; the new source/target workflow here is
CHR0 skeletal animation. Actual extracted folders are independently indexed with
no Dolphin path, then native inventories and first/last local/world poses are
compared with the disc route.

Costume witnesses require a different palette filename stem, so a PAC/PCS pair for
costume 00 cannot satisfy the alternate-costume proof. Exact selected models, native
container/resource hashes, source/build/partition provenance, first/last native
poses and original bytes/dirty state are checked. Metadata-only fixtures also
exercise locale suffixes, foreign source/character/family rejection, changed-bind
review, corrupt resources and changed containers. Split WBFS is rejected before
reader invocation because complete multi-part provenance is not represented.
Repeated complete inventory publication preserves exact known IDs/catalogue bytes;
offline disc repair and cancellation retain immutable associations.

The new private-desktop cases cover fresh-menu initialization, actual old-reader
rejection, source/character cancellation, different costume preview, read-only source
and editable target ownership, relocated costume Repair, original Next output,
recorded comparison, duplicate animation names, failed/cancelled Save, native body
Undo/Redo, project save/reopen and precise verified export. The source trackbar now
uses an explicit fixed transport row rather than stretching Play beyond its parent.
Compact/wide 100/175 text cases retain strict overlap, caption, viewport and document
preservation gates; all remaining supported text presets also run in scaling tests.

| Accepted case | Checks |
|---|---:|
| library-target-final3 | 39 |
| game-library-final3 | 52 |
| core-final3 | 380 |
| roots-mario-final3 | 628 |
| roots-snake-final3 | 628 |
| scaling-final3 | 356 |
| next-layout-final3 | 69 |
| next-layout-reverse-final3 | 69 |
| performance-final3 | 47 |
| keyboard-final3 | 210 |
| library-final3 | 52 |
| loop-final3 | 185 |
| batch-review-final3 | 182 |
| set-export-final3 | 152 |
| comparison-final3 | 40 |
| comparison-reverse-final3 | 40 |
| reference-moving-final3 | 231 |
| boundaries-final3 | 277 |
| boundaries-reverse-final3 | 264 |
| transitions-final3 | 80 |
| workflow-final3 | 61 |
| lifecycle-final3 | 37 |
| export-final3 | 23 |
| disc-wbfs-final3 | 52 |
| disc-iso-final3 | 53 |

All accepted images were actually inspected and their hashes retained in the local
ledger. Superseded probes and failures remain preserved locally and are excluded
from the final cohort. The acceptance record follows the final built source and
package hashes. Assets, disc images, tool distributions, extracted caches, captures,
settings, binaries and private paths are excluded from public publication.

Reproducible entry points, with paths supplied locally:

```powershell
./scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $native
# Build ControlsAcceptance, LibraryAcceptance and RetargetAcceptance Release/x86.
./scripts/Test-BackgroundCapture.ps1 -Model $model -Motion $motion `
  -WorkspaceLibraryTarget -NativeDirectory $native -OutputDirectory $freshOutput
./scripts/Test-BackgroundCapture.ps1 -Model $model -Motion $motion `
  -WorkspaceGameLibrary -DiscImage $wbfs -DolphinTool $tool `
  -LegacyDolphinTool $olderTool -NativeDirectory $native -OutputDirectory $freshOutput
& "$native/LibraryAcceptance.exe" --disc-image $wbfs --dolphin-tool $tool `
  --output $freshOutput --characters mario,snake
& "$native/LibraryAcceptance.exe" --disc-image $iso --dolphin-tool $tool `
  --output $anotherFreshOutput --characters mario,snake --compare-index $previousIndex
```

Capture work uses private desktops and the shared offscreen renderer, serialized
at low priority, without OS input injection or active-desktop screen copying.
External read-only CLI checks use separate isolated output directories. Preserve
pause/cancel/resume controls. This is handler and native-render evidence, not a human
walkthrough. Compact 175% deliberately scrolls and truncates long source identities;
full metadata/accessibility text remains available. A retained native camera can
crop the model edge in compact large-text views; automatic full-model framing
is not claimed. Existing loop/review drawers can obscure the character at compact
175%; Focus target editor closes them. Physical input, human usability,
monitor DPI, broad contact quality and actual game acceptance remain unverified.

The [Dolphin component record](DOLPHIN_SOURCE_REUSE_2026-10-26.md) pins upstream
release/source, license, dependencies, actual reader hashes and protocol boundaries.
Only source is published to public Fighterport/main under DoctorFlux attribution.
RVZ, other game/build/image combinations, split images and other games' install-path
adapters need their own evidence. No disc repacking, new solver or general game
acceptance is claimed. ONE G1/M2 remains incomplete; no next increment is promoted.
