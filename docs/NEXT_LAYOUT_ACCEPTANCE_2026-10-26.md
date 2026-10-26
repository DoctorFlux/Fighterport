# Studio Next layout and temporary sources — 26 October 2026

Preview.34 (`2.0.0-preview.34-next-layout`) completes this bounded UI increment
inside the ONE incomplete G1/M2 milestone. Fresh background qualification passes
**4,011 checks / 21 complete runs / 134 inspected native PNGs**.
The accepted source reference for the familiar layout is the preserved Studio Next
revision `1cc96940bb58a6bab2f5a5c6ab9fff03e3d4165d`, already covered by the
[source-import and license record](SOURCE_IMPORT_2026-10-24.md). Historical
repositories remain private and receive no changes or pushes.

## Everyday workflow

The default uses one outer toolbar, the complete editable native target previewer,
and a right source pane with a small read-only source viewport. The stacked stage
bars and task panel appear only through the optional View workflow guide. Newer
review, comparison, loop, controls, fitting and verified-export tools remain in
Animation/View menus, native toolbar overflow and the Fighter menu.

Load the target with Open Model and its idle/fall references with Add Animations
when required. Load source files chooses a reviewed original model and motion set
for a temporary session. Alternatively, Library selects the shared catalogue;
Sources retains the existing extracted Brawl-family folder and ISO/WBFS/RVZ paths
with their existing tool/provider checks. No new game reader or adapter was added.
Selecting/browsing metadata performs no automatic retarget or source decoding.
Preview Original explicitly opens the source; Use on This Model follows the
existing original Next bake and independent revision transaction.

The source heading, 190-pixel native viewport, transport, transfer button and
Fit start/end button remain pinned while catalogue/filter/details actions scroll.
Text sizes are 100/125/150/175 percent; the user explicitly removed 200 percent.
The removed option is absent from both menus and rejected by the setting API.
Saved unsupported values, including 200, normalize to 100 at startup. Native fonts
and process DPI configuration remain unchanged.

Fit start and Fit end independently enable their existing fitting windows. Off
retains the selected reference and every other setting, removes only that effective
reference, and disables its join/pose-preview actions. Re-enabling restores the
exact choice. Choosing Keep original disables the side; direct valid combo choices
remain compatible with older routes and restored recipes. Enabling without a pose
explains the missing choice before calling preparation. With both sides off, the
UI explains keeping the current animation and does not prepare or change it.
Explicit preview and result review still precede adding an independent fitted clip.
No fitter algorithm, numeric tolerance, timing, root policy or options schema changed.

## Source identity and library isolation

A direct load uses a separate temporary catalogue beneath the existing per-window
worker staging directory. Loading, previewing, transferring, filtering and repairing
that source do not register it in the shared library. Add to library is explicit.
It preserves supplied build evidence, obtains current paths from the exact repaired
temporary mounts, inventories again, compares both container hashes, model/resource/
skeleton hashes and the full motion-resource key/hash set, then uses existing
atomic registration. Repeated registration retains known identities/files.
Native selection, frame, authored content, Undo, dirty state, approval and recorded
result provenance stay unchanged during manual registration and source-mode changes.

Each catalogue retains its search/filter/favorite selection and expansion state.
Switching cancels pending generations and clears stale actionable selection;
asynchronous queries capture the catalogue and reject changed catalogue/generation
before publishing. Recorded comparison resolves exact IDs from retained session
catalogues and the shared library, never the currently browsed replacement name.
Transient catalogues last only for their owning window. Their target results save
normally; after reopen, missing temporary provenance explains unavailable comparison
and leaves native editing available. This is a deliberate session boundary, not a
project-format migration or persistent review-approval mechanism.

## Qualification

| Accepted case | Checks |
|---|---:|
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

The dedicated -WorkspaceNextLayout case exercises actual source loading/cancellation,
source preview and original Next transfer in both authentic Mario/Snake directions.
An independently invoked original bake matches every curve, tangent, native flag and
timing value. Output-owned source copies are moved/repaired before explicit shared
registration; supplied evidence, exact IDs/hashes and native content/Undo are checked.
The shared catalogue's complete file hash snapshot stays identical until Add.

Each direction checks compact/wide windows at 100/175 percent, complete pinned source
viewport/Play/Use/Fit controls, full button captions, target viewport center outside
the source overlay, exact native state and protected controls. The sole default bar
leaves workspaceBody.Top equal to toolbar.Bottom. Keyboard gates repeat this after
closing the optional guide, opening source browsing and recovering focus. These
independent checks caught and fixed a stale empty guide band and compact source
occlusion. Minimum catalogue height includes real TreeView margins; the 140-pixel
control gate was retained. Unchanged status retains the preview.33 layout/measurement,
hidden comparison and duplicate-setup guards; Play/Pause width measures once per
text preset. Playback performs no source loading, hashing or baking.

Existing background cases qualify filters/favorites, a large procedural catalogue,
review/set export, both comparison directions, fitting/no-fit switches, retained
motion controls, native editing/Undo, save/reopen and cancelled/failed writes. The
procedural catalogue test now switches through the real catalogue activation path,
including its generation invalidation, rather than changing the active field alone.
Scaling exercises every remaining preset and repeated resizing. Both root-policy
runs and the independent core boundary suite retain all previous numeric gates.

Actual build/run entry points (variables are local fixture/output/tool paths):

```powershell
./scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/"
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $source -WorkspaceNextLayout -NativeDirectory $native -OutputDirectory $evidence
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $source -TargetMotion $targetMotion -Clip AttackS3S -ReferenceClip Wait1 -ExitSamples 8 -WorkspaceBoundaryFit -WorkspaceFootRecovery -NativeDirectory $native -OutputDirectory $fitEvidence
& "$native/RetargetAcceptance.exe" --motion-controls $modelA $modelB $rootEvidence
# From engine/:
& $dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $numeric --boundary-fitting
```

The exact 21-run final3 cohort, current source/binary hashes, receipts, logs and
134-image inspection ledger are sealed locally. Every accepted PNG was actually
viewed, or verified SHA-256-identical to that reviewer's personally viewed image
with its original path recorded. Probes and superseded final1/final2 cohorts are
diagnostic only. No 200-percent layout is part of accepted final3 evidence.
Assets, captures, settings, temporary catalogues, projects and binaries remain
excluded from source publication.

## Boundaries

At compact sizes, source/inspection/review panes may cover native side tools;
Focus target editor restores them. Both full characters are visible in the tested
compact source/target captures. Compact review may obscure the target body until
closed; comparison retains native horizontal scrolling. Upper library controls and
advanced fitting reports scroll; long source identities deliberately ellipsize.
These are observed layout limits, not physical-input or human-usability acceptance.

The full BrawlCrate Model Previewer, original Next, existing native Undo and source
bytes remain intact. Original Next/body editing keep source-local protected motion
controls; explicit boundary fitting retains attack motion-position S/R/T and fits
other reviewed ancestor controls only inside chosen windows. No new rig, weights,
solver, renderer, adapter or project schema was introduced. Native export/reopen
success does not establish universal contacts or actual game compatibility. Disc
image/tool/provider support is retained; this qualification uses authentic native
files, not a newly exercised installed game or disc image.

ONE G1/M2 remains incomplete. Human/physical input, monitor DPI, broad contact and
continuity quality, unfamiliar anatomy, additional game destinations and actual
game acceptance remain open. Source-only publication is to public Fighterport/main
with DoctorFlux author/committer attribution and contributor verification.
