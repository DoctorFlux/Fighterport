# Native source/library animation order — 26 October 2026

Preview.40-native-library-order makes source/library animation order agree with
ordinary BrawlCrate motion-PAC browsing. The native `LeftPanel.LoadAnims` visits
container children in their recorded order; the catalogue and disc tree had
instead sorted animation names alphabetically.

The cached query index now groups by existing package registration order and
compares recorded resource-key child indices numerically, retaining native
depth-first traversal inside each PAC/BRES. Entry 2 precedes 10; descendants
remain with their parent branch. Labels and duplicate names never determine
order. Readable legacy/non-native keys retain a deterministic identity fallback.
The direct character tree consumes that ordered result without re-sorting it.
Temporary source model groups retain the same query order. Game/character/model
headings and costume choices retain their existing presentation.

Text, channel, availability and favorite filters preserve native relative order.
Ordering happens before counts/paging and lazy tree population. The immutable
metadata cache computes ordering keys once per generation, without new native
loading, file hashing or playback work. Existing resource identities already
contain the required indices: old saved disc/install catalogues need no reindex,
format migration, extra extraction or rewritten registration.

Exact clip IDs, source associations, costumes, automatic preview/default choices,
editable target, native Undo, full Model Previewer, original Next and root/boundary
policies remain intact. No retarget algorithm or authored animation is changed.

## Background verification

**335 checks / 4 accepted runs / 38 visually reviewed native PNGs.**

| Case | Checks | PNGs |
|---|---:|---:|
| order-final2 | 34 | 3 |
| library-final1 | 52 | 11 |
| flow-final1 | 67 | 15 |
| batch-final1 | 182 | 9 |

All captures received labelled overview inspection. The initial ordering case
reached disc/tree parity but its temporary-source test used an ambiguous reflection
helper. Final2 selects the exact existing overload and passes; the failed probe
is excluded. Final2 also removes an unused import from the ordering helper; its
runtime ordering logic is unchanged from final1. Qualified product SHA-256:
`08D729E6609452986341E11D407E2ACA7D7C46791F8FD4CE52E8074202908DCF`.
Builds succeed with the inherited CS1690 LoopInspection warning.

The dedicated `-WorkspaceLibraryOrder` private-desktop case invokes the actual
native animation-list builder on authentic Mario/Sonic disc motion PACs and an
ordinary temporary source motion PAC. Every supported native channel is compared
by exact resource identity, including empty channel lists. The visible lazy disc
tree is independently compared with the complete query order. A separate explicit
numeric-path oracle covers index 2 versus 10, nested paths, duplicate names,
multiple PACs, serialized-row permutations, renamed labels, paging and favorites.
Procedural ordering rows are never decoded as playable resources. Document
witnesses check exact target/clip/bone/frame/content/Undo/dirty state, and metadata
hashes prove browsing and temporary source use do not rewrite the saved library.

Build/run commands from the active checkout, with local paths supplied as variables:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory $native
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=$native/ --no-restore
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -NativeDirectory $native -OutputDirectory $orderOutput -WorkspaceLibraryOrder -LibraryIndex $index
```

Affected regression cases use the existing `-WorkspaceLibrary`,
`-WorkspaceLibraryFlow` and `-WorkspaceBatchReview` switches. Captures use private
desktops and the shared offscreen renderer, without OS input injection or active-
desktop screenshots. Evidence stays in excluded `artifacts/Fighterport-order40`.
The unchanged accepted engine is reused locally; game assets, settings, captures
and binaries remain excluded from source publication.

ONE G1/M2 remains incomplete. Physical input, monitor DPI, human usability,
contact quality and game acceptance remain unverified. Existing native rendering
and optional fitting limitations remain as recorded in preview.39 acceptance.
