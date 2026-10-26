# Automatic library preview and same-window targets — 26 October 2026

Preview.39-library-flow continues the single incomplete G1/M2 milestone. The
complete BrawlCrate Model Previewer, original Next, native Undo and existing
source-local root/boundary policies remain intact. No project/catalogue schema,
solver, rig, bind transform or skin-weight migration is introduced.

## User workflow

Selecting a character chooses its most broadly associated verified native model
and prefers its Wait1/Idle clip. Selecting an animation or compatible costume
updates the small original source preview automatically. A clip change within
the prepared model/motion package reuses the exact native lease and model;
there is no second decode or disc-package read. Load as source remains an
explicit refresh/reframe action. Browsing never adds temporary files to the
shared library and never retargets or replaces the editable main target.

Slow loads are debounced, cancellable and serialized. Completion must still
match the exact catalogue, clip, costume and request epoch. Older candidates
are disposed. Pending loads defer while the target/source clock is playing;
ordinary prepared playback does not open discs, load packages, hash files or
bake motion. Search/status updates retain the existing asynchronous outline,
lazy leaves, exact source/instance identities and stale-result guards.

CHR0, SRT0, PAT0, VIS0, CLR0, SHP0 and SCN0 preview through their retained native
evaluators. Unique same-name companion channels in the same native BRES owner
are prepared together; ambiguous companions are skipped with an explanation.
Switching resets authentic bind/morph presentation before applying the next
bundle. Stored-loop closing intervals retain native fractional sampling; game-loop
behavior is not inferred from an idle name. Missing native bone/material/vertex
bindings are reported, and SCN0
uses native indexed scene/light bindings. Preview model choice for a non-skeletal
channel is a read-only viewing context, not skeletal transfer approval. Native
version/provider and 4,096-frame operation bounds remain. Skeletal retargeting
still requires the existing independently verified CHR0 association.

Open as target prepares and independently decodes full owned model/motion copies
before the existing Save/Discard/Cancel decision. It swaps the current project
inside the same ModelForm, editor, viewport and worker. Window size, library,
filters, costumes and session metadata remain. A successful cold open also
commits the exact prepared original source preview; cancellation disposes that
candidate and keeps the previous preview/document. A failed Save keeps the
current dirty document and Undo available. The new target retains all original
native companion resources and uses normal body editing, Undo/Redo and Save.
Review/inspection/comparison state belonging to the replaced document is cleared.

## Background evidence

**3,039 checks / 20 accepted runs / 149 visually inspected native PNGs.**
All 149 captures received labelled overview inspection; the committed cold-source
workspace and native SCN0 view also received full-size inspection. The SCN0
fixture is an unlit silhouette with its recorded indexed scene lighting; no
replacement scene lights were invented.

Broad regressions ran on final1. The cold-source handoff, deferred loading,
provider/frame bounds and fractional-loop source sampling then received focused
final2 verification. Final3 changes only harness readiness/diagnostics and the
loop case's watchdog (three to ten minutes). Its product executable is byte-
identical to final2:
`A19BF9A1E00B432D00B71028572C37E59D90BE1D9CE56030C018A260707EC2FA`.
The table intentionally records the package generation of every admitted run;
it is not a claim that the whole matrix used one binary. Superseded probes,
the earlier cold-source wait, and the expired loop run are excluded. A cold-load
test now awaits existing control preparation and invokes the same command method
directly so command exceptions reach the harness instead of a modal error box.
The ordinary source/target buttons and menu routing remain exercised separately.

| Background case | Checks | PNGs |
|---|---:|---:|
| source-fit-final1 | 45 | 11 |
| game-sources-final1 | 62 | 10 |
| library-final1 | 52 | 11 |
| comparison-final1 | 40 | 5 |
| batch-review-final1 | 182 | 9 |
| set-export-final1 | 152 | 8 |
| workflow-final1 | 61 | 11 |
| scaling-final1 | 356 | 11 |
| lifecycle-final1 | 37 | 6 |
| export-final1 | 23 | 4 |
| next-layout-final1 | 69 | 5 |
| roots-mario-final1 | 628 | 0 |
| roots-snake-final1 | 628 | 0 |
| disc-reader-final1 | 96 | 0 |
| direct-final2 | 60 | 11 |
| keyboard-final2 | 210 | 7 |
| library-target-final2 | 39 | 8 |
| performance-final2 | 47 | 8 |
| library-flow-final3 | 67 | 15 |
| loop-final3 | 185 | 9 |

The dedicated `-WorkspaceLibraryFlow` case verifies character defaults, Sonic
AttackHi3 → AttackHi4 reuse without new decoding/package reads/hashing/baking,
rapid cancellation/stale completion, deferred loading during native playback,
actual source geometry, authentic stored-loop fractional sampling, all seven
native channels and their independent target candidates. It checks exact target,
clip, bone, frame, content, Undo and dirty-state preservation. It also proves same
window/editor/viewport/worker ownership, successful and cancelled cold opens,
Save-before-replacement, body Undo/Redo and fresh project reopen.

The direct-source regression additionally exercises actual disc reads with no
extracted cache, compatible costumes, original Next transfer, recorded comparison,
failed Save, independent target edits and verified export, then checks complete
temporary-package cleanup. Both authentic root-control directions pass 628 checks
each. Original input files remain unchanged. Compact/wide cases use the retained
100/175% text endpoints; the removed 200% layout is not reintroduced.

Commands executed from the active checkout, with local input/output variables:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory $native
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=$native/ --no-restore
dotnet build tests/RetargetAcceptance/RetargetAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=$native/ --no-restore
dotnet build tests/LibraryAcceptance/LibraryAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=$native/ --no-restore
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -NativeDirectory $native -OutputDirectory $output -WorkspaceLibraryFlow -LibraryIndex $index
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -NativeDirectory $native -OutputDirectory $loopOutput -WorkspaceLoopInspection
& "$native/RetargetAcceptance.exe" --motion-controls $mario $snake $rootOutput
& "$native/RetargetAcceptance.exe" --motion-controls $snake $mario $reverseRootOutput
& "$native/LibraryAcceptance.exe" --disc-reader-cases --output $readerOutput
```

The remaining rows use the corresponding existing background switches. Local
logs, receipts, capture hashes and reviewed sheets are under the excluded
`artifacts/Fighterport-flow39` evidence directory. The unchanged accepted engine
was copied into the qualified local package; no game files or binaries are
published. Builds succeed; the inherited CS1690 LoopInspection warning remains.


## Boundaries

These checks use private desktops and the shared offscreen renderer. No active-
desktop capture or OS input injection is used. Physical mouse/keyboard acceptance,
monitor DPI, human usability, broad contact quality and actual game acceptance
remain unverified. Other game locations are still path-only until their adapters
exist. Unmatched article/scene channels do not acquire invented bindings, and
non-skeletal channels are not converted into CHR0 retargeting.

Existing Sonic optional-fit skin-influence and fractional-join limitations remain
separately reported. Sonic's default Wait1 still shows the alternate sphere over
the lower body in the retained native renderer; runtime-controlled visibility
is not qualified. Native visibility observations are not in-game render proof.
ONE G1/M2 remains incomplete; no broader milestone is promoted.
