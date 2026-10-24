# Workspace task flow — 24 October 2026

Preview.16 (`2.0.0-preview.16-workspace-flow`) continues the simpler workspace in
PUBLIC DoctorFlux/Fighterport from `eb40e187e5f34a0f681779bb1b0c2620f2408614`.
**257 scoped native checks passed and all 43 final PNGs were actually inspected.**
These are fresh runs of the current application, not imported preview.15 results.
M2 remains the one incomplete milestone and G1 its current workspace substep.
Human task success, physical input, common OS dialogs, DPI and game acceptance
remain separate. The user explicitly deferred the root/motion-bone tweak; it is
not implemented or qualified here.

## Changes and ownership

Source and Target offer contextual Continue actions. Readiness explains missing
inputs, unapproved associations, unsupported channels and failed verification.
Continue verifies authentic source bytes/native identity through the existing
offline worker before leaving Source. A forced failure remains blocked for the
selected clip even when file size/timestamp still match cheap browse metadata;
the task offers exact-source repair. Nothing is decoded or hashed on playback
ticks. Target-only native editing still needs no donor animation.

More tools, Open project and New project are visible at the checked 1320 width.
More tools displays the actual original toolbar overflow and its native commands.
Target/project opening uses a real shared path-entry dialog with Browse, inline
existence validation and Cancel. Native loaders remain responsible for content
validation and native ownership. Open project uses the existing validated project
lifecycle and current editor as dialog owner. An untouched empty startup project
starts clean; real imports/name edits and prepopulated unsaved projects remain
dirty. Model-only project reopen returns to Target.

The existing Add Source dialog clears stale inspected models and resets pairing
approval when either chosen package or exact model changes. Missing/invalid
selection has inline guidance. Unchecked association remains explicitly Index
for review; reviewed input is labelled Add reviewed motion set. Registration
selects a committed exact clip, including after deduplication. Adding unapproved
motions with another original model cannot silently select an older approved pair.
Game/build text remains supplied metadata, not independent build verification.

Repair accepts a pasted replacement path or Browse, reports wrong-byte errors
inline and updates only the existing exact hashed source identity. Verification
runs offline. Its cancellation token reaches the existing atomic catalogue
publication check. Missing/changed sources get Repair; unsupported channels or
unapproved associations get the appropriate explanation instead of a relocation
action that cannot fix them.

The COMPLETE native BrawlCrate Model Previewer remains mounted: resources,
materials/shaders, camera/rendering, bone/animation lists, gizmos/fields, autokeys,
frame tools, timeline/playback, interpolation and Undo. Native committed documents
and selection remain authoritative. No renderer, second editor, engine numerical
algorithm, BrawlLib implementation, original Next v7 solver, mapping or frozen
acceptance bound changed. No new dependency, format adapter, writer or game route
was introduced.

## Actual builds and runs

Windows x64, Visual Studio 2022 Community MSBuild and .NET SDK 10.0.402 were used.
Native app/harness remain net472/x86, owned host/CLI .NET10/win-x64, with the
existing isolated native worker. Pinned OpenTK SHA256 remains
`931E38B47B824C316A55B5A54D55650C978CA90DDE1DAEA02299EDDD94E719C0`.

The full native/engine build succeeded, followed by successful app/harness
rebuilds after changes. No warnings/errors were reported. Local logs include
`fighterport-workspace16-build-r1.log` and the final harness logs
`fighterport-workspace16-harness-build-r6.log` / `-r7.log`. R7 changes only the new
flow test's wait for an exact lazy-tree clip ID. The native EXE/BrawlLib hashes
were verified identical between r6 and r7. The forward flow/edit runs use r6;
reverse flow/edit and procedural runs use r7. Their scope is recorded below.

Actual invocation forms, with private locations represented by variables:

```powershell
& scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build `
  /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false `
  "/p:OutDir=$native/" /nologo /v:minimal

& scripts/Test-BackgroundCapture.ps1 -WorkspaceFlow -Clip Wait1 `
  -Model $snakeModel -Motion $marioMotion -RetargetSource $marioModel `
  -NativeDirectory $native -OutputDirectory $newFlowForward
& scripts/Test-BackgroundCapture.ps1 -WorkspaceFlow -Clip Wait1 `
  -Model $marioModel -Motion $snakeMotion -RetargetSource $snakeModel `
  -NativeDirectory $native -OutputDirectory $newFlowReverse
# Repeat those two fixture directions with -SimpleWorkspace and fresh outputs.
& scripts/Test-BackgroundCapture.ps1 -Model $proceduralModel `
  -Motion $proceduralMotion -NativeDirectory $native `
  -OutputDirectory $newProceduralOutput
& scripts/Run.ps1 -NativeDirectory $native -Check
```

| Final local run under artifacts/viewer-parity | Checks | Inspected PNGs |
| --- | ---: | ---: |
| Fighterport-workspace16-flow-mario-to-snake-r5 | 47 | 9 |
| Fighterport-workspace16-flow-snake-to-mario-r2 | 47 | 9 |
| Fighterport-workspace16-edit-mario-to-snake-r1 | 61 | 11 |
| Fighterport-workspace16-edit-snake-to-mario-r1 | 61 | 11 |
| Fighterport-workspace16-original-r1 | 41 | 3 |
| This increment | 257 | 43 |

All five receipt.json files report Passed=true, PrivateDesktop=true,
ActiveDesktopWindows=false, OSInputInjected=false and ScreenCopied=false.
Runs were serialized through the paced low-priority background wrapper and
shared offscreen viewports, with pause/cancel/resume controls. No desktop switch,
focus stealing, OS input injection or active-screen copying was used.

## Task and numeric evidence

The new flow drives actual application-owned registration, target selection,
project opening and repair Forms with their real controls and handlers. The
observed first-use sequence is Add source -> Continue to target -> Open model ->
Continue to preview -> Preview transfer -> native correction -> Export. Model-only
Save/Open project, original-source audition and repair are exercised within the
same task. These are programmatic handler steps, not measured human clicks,
human completion time or first-time-user success.

Both directions check approval reset, cleared missing-model inspection, real
registration/autoselection, duplicate registration with unchanged catalogue
generation/count/clip ID, and an unapproved alternate original model using the
same motion file. The unapproved selection remains unassociated and blocks
Continue while preserving the distinct approved identity. They check visible
tools/project commands at 1320, real expert-menu display, invalid target path,
Cancel without document change and actual model-only project replacement/resume.
Serialized model bytes match before Save and after actual viewer reopen.

Tests move/change only isolated copies of original sources. Cached selection of
a now-missing file fails explicit Continue without catalogue/target mutation.
Actual repair rejects different bytes inline with unchanged catalogue hash, then
accepts the exact original at its new path. Pre-cancelled repair leaves the index
unchanged. A one-byte change with preserved size and timestamp is independently
rejected, keeps Continue blocked and Repair visible, and can be repaired using
another exact copy. Mid-operation UI cancellation is not separately qualified.

The new primary route reaches original `bind-world-fk-body-contact-ik-v7` with
exact library provenance. Original native pelvis/body fields author X rotation
+2 degrees, Undo/Redo restore independently expected values, and CHR0 export
freshly decodes with every edited key, tangent and transform flag intact. Project
reopen retains exact source identity; all supplied input hashes and target-model
bytes remain unchanged.

The 61-check editing regression in each direction freshly repeats native route
equivalence against a separately prepared original Next bake, same-name independent
results, exact selection/import Undo, bounded playback without new retarget
capture/bake, stage/focus ownership, native pin/curve/Hermite edits and Undo/Redo,
export/project/viewer reopen, cancellation and stale source/target/settings
rejection. It retains strict intermediate/final model-byte and original-container
invariants. Route equality is not an independent proof of ideal motion. Its TopN
pin edits prove retained authoring continuity, not the deferred protection policy.

The 41-check original procedural regression covers independent pin/rest-axis/
rotation expectations, reviewed rig fingerprints, native worker/engine transport,
actual native pointer-handler dispatch, stale gesture rejection, mixed Undo/Redo,
startup and native/project persistence. No full engine suite was rerun or claimed.

## Fixtures, review and development failures

Private supplied Mario/Snake model and motion packages were available for Wait1
in both directions, along with original procedural original.mdl0/original.chr0.
Input identities match the fixture table in
[preview.15's report](SIMPLE_WORKSPACE_ACCEPTANCE_2026-10-24.md#fixtures-and-visual-review);
fresh per-run hashes are retained locally. Pairing was explicitly approved for
the tests; exact game-build membership remains unverified. Assets, copies,
projects, captures, binaries, logs and machine settings stay ignored.

Actually opened flow images in each direction are flow-toolbar-1320, more-tools,
register-source, target-invalid, model-only-resume, source-audition,
missing-original, repair-wrong-bytes and native-result. Both editing runs' eleven
images and the three procedural images were also actually opened. Review confirms
compact readable actions/dialogs, full original native controls, distinct donor/
target views, readiness/repair failures, selected native fields/curves and saved
source context. Long paths/tree labels scroll horizontally; disabled native dark
captions retain low contrast. Local reviewed-image receipts bind all 43 hashes to
actual observations. Stills do not establish full-motion or physical usability.

Development flow attempts remain separate and are not accepted counts. The first
menu assertions inspected ToolStripOverflow.Items, which does not enumerate these
displayed original items; the final check verifies the actual original item's
displayed parent and the visible menu. Menu opening is deferred to the next UI
turn after a toolbar click. Early reopen checks ran before WinForms OnShown
restored the scene; the final test waits for the actual target and Target stage,
retaining the exact byte comparison. A diagnostic null-target attempt is retained.
The first reverse run raced a duplicate-named lazy-tree entry; the wait now
requires the exact approved clip ID. No numeric bound or preservation assertion
was weakened for these harness corrections.

The one current [G1 queue](plans/current.md) retains physical/DPI/human task
qualification and remaining workspace usability. Actual New project replacement/
unsaved-dialog input, common OS Browse/Save input, corrupt-existing-package dialog
decoding and broader native parity are unqualified here. Contact/context work,
advanced 96.2 failures/blocked Apply, other adapters and games remain open.
