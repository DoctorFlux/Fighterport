# Atomic verified animation sets — 24 October 2026

Preview.26 (`2.0.0-preview.26-verified-set-export`) follows published preview.25
`62a18a50fa922688d1d56a35fa9c9c34afec6b4c`. These are sequential bounded increments
inside the ONE incomplete G1/M2 milestone. Preview.25 has its separate
[batch review acceptance record](BATCH_REVIEW_ACCEPTANCE_2026-10-24.md).

## Behavior and preservation

Export reviewed set is available in the Export stage. The application-owned dialog
shows the project, exact target, all included exact result identities, frame counts,
proposed filenames and destination. The exact target resource identity and content
hash are also shown and recorded in the set receipt. Every session row must have current approval.
Preparation freezes explicit owned CHR0 snapshots; it never temporarily selects
animations. Ordinary native selection/scrubbing after freezing is safe. Changed
clip, target, rig, recipe, project or review membership rejects the operation.
Content/target/recipe changes invalidate affected review approval. Both the existing
64-clip review limit and a two-million-key aggregate export limit apply.

Readable filenames sanitize invalid characters and append a stable SHA-256-derived
identity suffix. Duplicate native names remain distinct. Case-insensitive collisions
are rejected before writes. Only a NEW, nonexistent destination directory is valid;
existing empty/nonempty directories and files are preserved. Original imported
sources, library storage, character workspace and project working/saved paths keep
their existing protections. Single-animation export retains its overwrite dialog,
atomic replacement and interface.

Each operation stages precise native candidates in a uniquely named sibling
directory. Each is independently decoded and compared against frozen full metadata,
track order, semantic flags, key indices/values/tangents, frame count and loop state.
The receipt records exact result identities, names, frame counts, filenames,
source clip IDs and verified hashes, without source/working filesystem paths.
Before commit, staged inventory, candidate hashes and receipt hash are rechecked;
the UI owner rechecks live dependencies, destination protections and cancellation.
Directory.Move publishes the entire directory on the same volume. No per-file
publication or destination merging occurs. Application export receipts update only
after this directory commit, using the already verified hashes.

The worker owns only frozen candidates. Cancelling the dialog requests cancellation
and waits for the task to finish before releasing snapshots. Failure/cancellation
does not publish a set. Cleanup only removes explicitly owned temporary files,
never recursively traverses a destination. An externally locked staging file may
leave an unpublished temporary directory until that handle is released; crash
recovery and hostile external filesystem mutation are not claimed.

Live clips, native node identities, selection, bone/frame and native Undo remain
owned by the complete BrawlCrate Model Previewer. Export before project Save works.
Original Next v7, source-local protected controls, timing fit, comparison, separate
library preferences and session-only approval remain. No solver, automatic slot
filling, adapter, persistent approval, second Undo stack or project migration is added.
Playback still does no export verification, file loading/hashing or retargeting.

## Fresh background qualification

The final cohort passes **2,229 scoped assertions** across 15 runs. All **97 fresh
PNGs** were inspected: exact reviewed identities/filenames and readable scaled
modal actions, recoverable review drawers, retained comparison and real native
model rendering, interpolation/transform/timeline tools and visible focus.
Private-desktop receipts record no active-desktop windows, OS input injection or
screen copying. App, controls harness and root harness builds pass. Development
and superseded pre-identity runs are excluded; no tolerance/timeout was relaxed.

| Final scope | Assertions |
| --- | ---: |
| Verified set export, both authentic rig directions | 152 + 152 |
| Batch review, both directions | 66 + 66 |
| Library organization, both directions | 40 + 40 |
| Linked comparison, both directions | 40 + 40 |
| Verified single export | 23 |
| Keyboard / scaling / lifecycle | 174 + 410 + 37 |
| Native workspace and original Next workflow | 61 |
| Source-local protected controls, both directions | 464 + 464 |
| Total | 2,229 |

Dedicated `-WorkspaceSetExport` cases run both authentic rig directions through
filtered/favorite Batch / Fit, original Next, explicit loss review and independent
target revisions. They exercise real native edits before Save, duplicate names,
stable filenames, independently decoded exact native curves and metadata, receipt
hashes/path omission, unchanged live node/frame/Undo state and exact native Undo/Redo.
Frozen operations tolerate selection/scrubbing without changing clips themselves.

Fault cases cover existing set/empty directory/file, cancellation before writing,
during candidate writing and before publication, damaged decoded candidates,
injected write-boundary failure, changed staging/receipt, actual Windows rename
failure with a locked file, stale clip/target/recipe/review membership, filename
collisions and project-working-path protection. Each verifies no set publication,
existing destination preservation and unchanged prior application receipts. The
locked-file test releases its external handle before removing its own remaining
unpublished staging directory. It does not weaken application cleanup restrictions.

The real workspace action and modal are exercised at compact/wide layouts and
100%/200% text, including exact reviewed list, inline existing-folder rejection,
keyboard-visible actions, actual asynchronous success and cancellation while the
writer is deliberately held. A procedural donor-free v5 CHR0 covers sparse and
absent channels, authored TransN keys/tangents/flags, original path, string/int/float
user metadata and invalid filename characters. It joins the Next results in a
single independently verified set. Save/reopen and one batch import Undo follow.

Commands (tool/fixture paths supplied locally, excluded from public source):

```powershell
./scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
& $msbuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/" /nologo /v:quiet
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $sourceModel -OutputDirectory $fresh -NativeDirectory $native -WorkspaceSetExport
# Repeat reverse direction, then current WorkspaceBatchReview/WorkspaceLibrary/
# WorkspaceComparison/WorkspaceExport/WorkspaceKeyboard/WorkspaceScaling/
# ProjectLifecycle/SimpleWorkspace cases in fresh serial private-desktop directories.
& "$native/RetargetAcceptance.exe" --motion-controls $sourceModel $target $freshRoot
# Repeat reverse root-policy direction.
```

Final `*-r2` receipts/captures live under ignored local
`artifacts/Fighterport-workspace26`. The qualification seal records source blobs,
owned binary/build logs, receipt/image hashes and tested/published commit. Assets,
captures, settings and generated binaries stay excluded. Source publication uses
DoctorFlux author/committer attribution, contributor verification and public
Fighterport/main. The launcher selects this qualified package; -Check opens
no active-desktop window.

Development findings: queued progress callbacks retain their cancellation owner
and ignore completed
dialogs, avoiding access to disposed/null cancellation state. Export-time dependency
rechecks explicitly invalidate stale review approval, including recipe changes.
The dialog and receipt now expose the target's exact resource identity and content
hash as well as its name; the final cohort was rebuilt and rerun after this change.
Private-desktop tests reuse still-valid approval between fault cases instead of
repeatedly selecting every animation. No timeout or numeric tolerance was relaxed.

## Remaining gates

This qualifies the Brawl CHR0 review/set-export workflow only. Human approval cannot
establish broad contact quality, generalization or in-game correctness. Physical
input, monitor DPI, human command discovery/task outcomes, broad contacts and game
acceptance remain unverified. One incomplete G1/M2 milestone remains; no further
feature increment is promoted by this delivery.
