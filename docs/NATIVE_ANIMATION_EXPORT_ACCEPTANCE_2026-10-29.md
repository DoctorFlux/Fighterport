# Ordinary native animation export — preview.60

Starting commit: `80353b8c3a605229f9c1890bf6ba222ca4450f08`.
This bounded correction is accepted inside the ONE incomplete G1/M2:
**806 checks in eight admitted background runs and 26 actually inspected native
UI PNGs**. The launcher selects `Fighterport-nativeexport60/native1`.
No broader milestone or physical-input acceptance is implied.

## User-visible behavior

The Model Previewer animation-list **right-click → Export** retains the original
BrawlCrate save picker. It writes the requested current animation without
reading saved optimization preferences, running reduction/geometry analysis or
opening **Preparing verified export copy**. No extra button or export mode is
added. Explicit **Optimize animation / Export optimized copy** remains separate.

The normal CHR0 writer uses an independent exact current-member snapshot, native
serialization/fresh decode and atomic publication. It preserves keys, tangents,
paired sides, full turns, metadata, flags, sparse/absent groups, timing, loop and
retained storage. It does not use the earlier full-quality optimization baseline
in place of an explicitly applied current result. A target model is not required
for ordinary animation export, and unsaved model edits are not rebuilt or cleared.
Sources, library paths, working files and review drafts retain their protections.

The native filter still offers CHR0 and Maya ANIM. ANIM uses the original
converter/model chooser on an independent clip; cancelled conversion publishes
nothing. Text conversion is no longer incorrectly reopened as binary CHR0.
Partial/failed writes retain existing destinations. No new external-editor or
Maya roundtrip acceptance is claimed. See
[implementation notes](NATIVE_ANIMATION_EXPORT_NOTES_2026-10-29.md).

Ordinary retarget PAC export also omits automatic optimization while retaining
its exact target/protection context, template validation, independent CHR0 and
sibling checks. It rejects active-target replacement during template selection.
Explicit workspace verified export, reviewed sets and native-slot workflows keep
their separately qualified policies/gates. Ordinary project Save stays exact.

## Background qualification

Evidence root: ignored `artifacts/Fighterport-nativeexport60`. All test application
runs are serialized on private desktops with the shared offscreen renderer. No
OS input injection, active-desktop capture or desktop switching is used.

The new **`-WorkspaceNativeAnimationExport`** case invokes the actual native
animation-list callback immediately below its unchanged OS save picker. With
saved ErrorBounded/Thorough preferences, it verifies **zero optimization prepares,
zero optimization geometry checks and no preparation form**. Both current and
nonselected owned clips export without temporary selection. Native body Undo/
Redo, target/bone/frame/camera, dirty state, root curves, model/weights and all
405 untouched sibling resources survive. Existing destinations, locked writes,
protected originals and project working files are exercised.

The model-less v5 fixture includes metadata/user data, an unmatched attachment,
sparse/absent groups, 720° motion, fractional paired tangents and loop-closing
data. Export preserves its exact decoded content and storage without inventing
a model association or dropping tracks.

Maya coverage substitutes only the model-picker boundary through an internal
null-by-default seam. The actual native `AnimFormat.Serialize` overload writes
the candidate with an explicitly supplied model, and its bytes match the direct
native converter. No-op cancellation and partial writer failure preserve the
existing destination and remove temporary files. This is callback/converter
coverage, **not physical save/model-picker or overwrite-confirmation coverage**.

The dirty-model regression retains all existing rejection/numeric/state checks
for explicit optimization, review approval, native slots and verified workspace/
set export. Only ordinary native animation export now succeeds on its own
separate path, preserving every dirty model backing field and native history.

| Final-build case | Checks | Captures | Scope |
| --- | ---: | ---: | --- |
| `native-export4` | 46 | 4 | Actual native callback, exact current/sibling/no-model exports, ignored saved policy, native conversion and failures. |
| `dirty-model2` | 55 | 0 | Explicit saved-model gates retained; ordinary resource export is model-independent. |
| `structure1` | 178 | 0 | Fractional native data, exact byte accounting, flags, terminal/timing and time-bit writing regressions. |
| `pac1` | 18 | 0 | Authentic native PAC adapter, complete content/storage, untouched siblings, changed-template/candidate and publication failures. |
| `lifecycle1` | 37 | 6 | Native edits, New, cancelled/failed Save and project reopen. |
| `explicit-exact1` | 193 | 12 | Explicit Exact tool, cancellation, settings invalidation, independent optimized export and layouts. |
| `explicit-apply1` | 265 | 4 | Explicit current Apply, retained baseline/eviction, native Undo/Redo and project Save/reopen. |
| `explicit-bounded1` | 14 | 0 | Explicit Thorough bounded analysis, unchanged cumulative limits, native validation and safe fallback. |

The three explicit segments jointly qualify the original workload; the timed-out
combined case is excluded. Thorough bounded analysis retains its original
finished-master hash and limits. It validates a 10,664-byte approximate candidate,
then chooses the smaller independently verified 10,356-byte Exact result. The
100,000-sample budget rejects another proposal at an estimated 101,778 required
witnesses; that gate is preserved rather than expanded to make the run pass.

All 26 admitted images were actually viewed. Ordinary-export captures show the
textured native model, retained HipN selection/frame, full controls and the
compact exported-file status with the unsaved marker retained. Exact/Apply views
show matching original/decoded poses and visible actions at 1050/1700 widths and
100%/175% workspace text. Lifecycle edited/reopened poses agree visually; save
messages and actions fit. Existing compact top-menu overflow and long-detail
scrolling remain. These static views do not establish continuous motion,
physical picker interaction, human usability or monitor DPI.

The PAC case exercises the actual adapter below its native template picker.
Physical picker interaction is not claimed. No native curve tolerances, root
contracts, weights, solver, shared codec or project format are changed.

## Build and delivery evidence

Final app: file version `2.0.0.36`, informational version
`2.0.0-preview.60-native-animation-export`. VS2022 Release/x86 MSBuild builds the
application and ControlsAcceptance harness with `/restore`,
`/p:BuildProjectReferences=false`, the isolated `/p:OutDir=<native1>/`,
`/nologo /v:minimal /m:1 /nr:false`. The two existing CS1690 warnings remain.

App SHA-256:
`94C38B78B5B60D83AF9937E16CB819BA34D2919812D52B46286ACE87DCE3155F`.
Harness for the five ordinary/native/lifecycle cases, SHA-256:
`3E32667E85B96415D97871F6639995419A5015B297FC9FBD0A677E12187D1BE8`.
Final harness for the three explicit optimizer segments, SHA-256:
`CE78E6AF12E23E8EDA311EA68EBB008F83A678330992AEA17A44526FD534B59C`.
The second harness change only decomposes the existing test workload. The same
app hash above is used by every admitted run; the earlier five case methods
and their production paths did not change with segmentation.
The BrawlLib, Core and engine-host binaries are unchanged, reused with exact
hashes from preview.59; no new Core run or codec modification is claimed.

Actual background commands use `scripts/Test-BackgroundCapture.ps1` with
`-NativeDirectory <native1> -Model <MarioModel> -Motion <MarioMotion>` and each
of `-WorkspaceNativeAnimationExport`, `-WorkspaceExportDirtyModel`,
`-NativeExportStructure`, `-NativeMotionPacVerification`, `-ProjectLifecycle`
and `-WorkspaceAnimationOptimization -WorkspaceOptimizationSegment Exact`,
`Bounded` and `Apply`, with distinct output directories above.
Full literal local commands, hashes, build logs and receipts remain in the
ignored evidence root. Private models/motions, settings, captures and binaries
are excluded from source publication.

Changed production boundaries are `ModelForm.ExportOptimization` and the
ordinary PAC branch of `ModelForm.Retarget`; the original LeftPanel menu/picker,
GenericWrapper resource-tree export and native converters remain in place.
New harness dispatch and the ordinary-export case are additive. DoctorFlux
author/committer identity, attribution hook and contributor verification govern
source-only publication to public Fighterport/main.

## Excluded development attempts and limits

`native-export1` is excluded: its initial test setup waited for controls before
selecting an animation. Setup now follows the existing select-then-wait path;
no timeout or production gate was changed. `dirty-model1` is excluded: a stale
reflection call omitted the existing fourth optional `evaluationMaster`
parameter. The corrected call passes null explicitly and reaches the real
dirty-model rejection; all assertions remain. Preliminary native-export2/3
runs are development evidence, superseded by final native-export4.

`explicit-optimizer1` is excluded: the combined legacy workflow reached its
unchanged ten-minute private-desktop watchdog after bounded analysis passed,
before the current-Apply fixture. Its partial assertions/captures are not counted
as a completed workflow. Test-only Exact, Bounded and Apply segments retain all
36 original Check call sites, the unchanged Apply helper and the shared source/
ownership/sibling checks. Each retains the same ten-minute limit, numerical
ceilings and per-operation work caps. Bounded and Apply explicitly recreate the
original inherited Thorough preference context. Their receipts identify partial
coverage and require all three admitted segments; no single partial case is
labelled as complete. Default Complete execution remains unchanged.

Physical input/pickers, monitor DPI, human usability, general contact quality,
external editors/plugins, Melee and game acceptance remain unverified. The
source-local protected-root policy, original Next retargeter and complete native
Model Previewer are retained. G1/M2 remains incomplete; no next increment is
promoted by this correction.
