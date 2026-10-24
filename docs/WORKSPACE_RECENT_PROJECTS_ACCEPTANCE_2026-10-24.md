# Recent projects and exact resume — 24 October 2026

Preview.21 (`2.0.0-preview.21-workspace-recent`) continues PUBLIC
DoctorFlux/Fighterport from `f4d7a79d519cc7222cbb8ed4ee03168661b590a5`.
The user authorized the next workspace improvement with background-only testing.
Final qualification is recorded below. This bounded increment leaves the ONE
G1/M2 milestone open and the prepared human walkthrough unperformed.

## Behavior

Open project now includes ten recent project full paths in most-recent-first
order. Nothing is preselected; choosing an entry fills the ordinary path field
and does not open or alter the document. The existing Open button, native project/
asset validation and Save/Discard/Keep editing replacement handling stay in charge.
Missing paths retain their normal unavailable status and disabled Open action.
Browse and pasted paths continue to work without any history.

Successful project Save/Save As and Open record the exact canonical manifest path.
Cancelled or failed project operations do not publish a new history entry. Case-
insensitive duplicates move to the front; same-named projects in different folders
remain distinct. Forget selected and Clear history remove only local navigation
entries, never project manifests, assets or originals. Normal path editing and
Escape remain available; recent controls have standard Tab order, focus painting,
accessible descriptions and full-path tooltips. Very long paths may need the
widened dropdown, tooltip or horizontal path-field navigation to read completely.

History is separate local presentation state at
`%LOCALAPPDATA%/Fighterport/Workspace/recent-projects.json`, shared across app
versions. Reads are bounded at 64 KiB and ten valid rooted .fpstudio paths.
Writes use a unique sibling temporary file and atomic replacement. Corrupt or
unavailable history degrades safely; a history write failure keeps the session
list and does not turn a successful project save into failure. Save reports that
condition in the native status strip; the picker reports history read/write
errors. Background harnesses automatically isolate this file under their capture
output rather than reading/writing the user's real profile history.

## Exact native project resume

The new donor-free native-field replay exposed a pre-existing project-save loss:
default native compression rounded values while rebuilding an edited imported
CHR0. A diagnostic Mario Wait1 comparison found 202 differing values, for example
0.897752166 becoming 0.8977522 and -182.17804 becoming -182.178055. A separate
harness mistake initially checked the new form before its native assets loaded;
that wait was corrected, then the actual compression difference was measured.
Exact expectations and existing numeric bounds were retained.

Project Save now flushes pending native curve Undo and uses the app's existing
precise CHR0 encoder on edited/branched clips before native merge/output. It
preserves all existing key indices, values, tangents and semantic flags, including
protected motion controls. The complete native interpolation and keyframe panels
are rebound to the retained bone's saved entry, including hidden native curve
editors. Existing native Undo/Redo remains usable after Save and real failures.
Precise storage may make edited clip output larger. No new encoder, competing
authoring state, Undo stack or project schema is introduced. This is a project
Save/resume fix; direct resource exports retain their separate qualified routes.

The COMPLETE BrawlCrate Model Previewer stays mounted with its original menus,
resources, render/camera/materials, bone/animation lists, fields/gizmos, curves,
timeline and playback. Engine, BrawlLib and original Next retargeter sources are
unchanged. Original Next v7 plus the separately recorded source-local-controls-v1
policy remain intact: a protected global/motion control follows only its own
source-authored local animation; inherited parent movement does not authorize
child keys. Reviewed structural roles identify equivalents on other compatible
rigs. Body corrections do not change protected controls. Originals and authentic
model hierarchy/binds/helpers/skin weights remain immutable.

## Final evidence

**2,398 fresh scoped assertions passed; all 87 final PNGs were individually opened and inspected.**

| Final suffix (`Fighterport-workspace21-`) | Assertions | Inspected PNGs |
| --- | ---: | ---: |
| dialogs-r1 | 2 | 2 |
| keyboard-mario-r1 | 162 | 7 |
| keyboard-snake-r1 | 162 | 7 |
| lifecycle-snake-r1 | 37 | 6 |
| motion-mario-snake-r1 | 464 | 0 |
| motion-snake-mario-r1 | 464 | 0 |
| recent-mario-r1 | 53 | 7 |
| recent-snake-r1 | 53 | 7 |
| scaling-mario-r1 | 410 | 11 |
| scaling-snake-r1 | 410 | 11 |
| walkthrough-mario-snake-r1 | 60 | 9 |
| walkthrough-snake-mario-r1 | 60 | 9 |
| workspace-mario-snake-r1 | 61 | 11 |
| Total | 2398 | 87 |

Each recent case starts with original procedural storage expectations, then an
actual donor-free native body edit at frame 3. It saves two same-named projects,
reloads history from disk with a new store, checks default/no-auto-open selection,
100/200% path selection, modal Escape and focus recovery, Keep editing on recent
replacement, corrupt project rejection through the same validated native command,
real locked-project Save failure, history-only write failure and recovery, busy
routing guards, moved-project handling, Forget/Clear and exact native Undo/Redo.
A real project Open restores the intended manifest/clip/frame and promotes only
that successful path. Model/original hashes and protected control channels remain
unchanged. The saved native curve and keyframe panels point to live saved entries;
the curve-panel capture displays an unauthored Scale X channel, while exact native
curve preservation is established by independent full-content comparisons.

Storage cases cover empty state without writing, ten-entry eviction/order,
case-insensitive deduplication, defensive returned arrays, filtering invalid/native
asset paths, fresh-instance disk reload, atomic replacement failure with old bytes
retained and temporary cleanup, persistence recovery, Forget/Clear, corrupt and
oversized input and a blocked history-directory path. Project saves remain
successful when only recent history cannot be written.

Both continuous walkthrough directions retain one source registration for two
Next transfers and three independent saved projects; their 43 handler steps and
durations are not human metrics. Fresh keyboard and scaling checks cover both
rigs, compact/wide layouts, 100/200% keyboard navigation, all five text presets,
focus/drawer/modal recovery and exact native ownership. The retained transfer,
lifecycle and motion-control cases preserve their original independent native,
quarter/withheld, local-vs-inherited, rename/permutation and immutable-input gates.
No previous-version receipt is counted toward these totals.

All 14 recent, 18 walkthrough, 14 keyboard, 22 scaling, 11 workspace, six lifecycle
and two dialog PNGs were inspected. The history-only write failure is visibly
reported while the model remains editable. Missing recent paths disable Open;
scaled controls remain separated. Actual source/target models, native curves,
body edits and reopened source links are retained. Existing compact drawers may
overlay the editor, and long labels require scrolling/tooltips. One scaling
capture includes a transient library-loading placeholder for a secondary branch;
its selected supported clip is available. The standalone error-dialog layout
fixture is synthetic; actual failed-save evidence is in lifecycle/walkthrough.


## Builds and reproduction

Native/net10 engine/legacy-worker packaging passed in
`fighterport-workspace21-build-r1.log`; the final native app and controls harness
built in `fighterport-workspace21-harness-r2.log`. Numeric harness build is
`fighterport-workspace21-retarget-harness-r1.log`; it loads the final native
assemblies. Native app/harnesses are net472/x86 with VS2022 MSBuild; engine SDK
is 10.0.402. No full engine-suite pass is claimed. Final outputs are separate
from all preview.20 sealed binaries.

Use absolute SDK/output paths and private fixture variables. Each `$Capture`
must be a fresh output directory. Commands used:

```powershell
./scripts/Build.ps1 -DotNet $DotNet10 -NativeDirectory $Native
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
& $MSBuild tests/RetargetAcceptance/RetargetAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$Native/" /nologo /v:minimal
./scripts/Test-BackgroundCapture.ps1 -Model $Model -Motion $Motion -OutputDirectory $Capture -WorkspaceRecent -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $Model -Motion $Motion -OutputDirectory $Capture -WorkspaceKeyboard -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $Model -Motion $Motion -OutputDirectory $Capture -WorkspaceScaling -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $MarioMotion -RetargetSource $MarioModel -TargetMotion $SnakeMotion -Clip Wait1 -OutputDirectory $Capture -WorkspaceWalkthrough -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $MarioModel -Motion $SnakeMotion -RetargetSource $SnakeModel -TargetMotion $MarioMotion -Clip Wait1 -OutputDirectory $Capture -WorkspaceWalkthrough -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $MarioMotion -RetargetSource $MarioModel -Clip Wait1 -OutputDirectory $Capture -SimpleWorkspace -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $SnakeModel -Motion $SnakeMotion -OutputDirectory $Capture -ProjectLifecycle -NativeDirectory $Native
./scripts/Test-BackgroundCapture.ps1 -Model $MarioModel -Motion $MarioMotion -OutputDirectory $Capture -WorkspaceDialogs -NativeDirectory $Native
& "$Native/RetargetAcceptance.exe" --motion-controls $MarioModel $SnakeModel $Capture
& "$Native/RetargetAcceptance.exe" --motion-controls $SnakeModel $MarioModel $Capture
./scripts/Run.ps1 -Check
```

Recent, Keyboard and Scaling each run with both authentic model/motion pairs.
Private supplied Brawl assets and generated project/capture diagnostics remain
ignored local inputs/outputs. Tests use the serialized, paced, low-priority private
desktop and shared offscreen native renderer with existing pause/cancel/resume
support. Receipts explicitly report private desktop and no active desktop windows,
OS input injection or screen copying. No SwitchDesktop or active-desktop capture
was used. Application-owned controls and actual disk I/O are exercised; native
OS file-dialog input remains unverified. Ignored
`artifacts/Fighterport-workspace21/qualification.json` seals final source, build/
binary, receipts, journals and individually reviewed image hashes plus the
publication commit. The ignored launcher selects the qualified package.

## Development findings and remaining scope

`Fighterport-recent-mario-r1` exposed an uncaught oversized-history exception;
read handling now includes the explicit validation error. R2–r4 exposed the
harness's premature reopen assertion; r5 then isolated actual native compression
value drift. R6 passed after precise project-save encoding; the final revision
adds native curve/keyframe reference coverage. Development logs/captures are
retained locally and excluded from final totals. No test bounds were relaxed.

History disk reload is verified with fresh independent store instances, not a
physical app restart or user-profile migration test. Concurrent-process history
merging is not implemented/qualified; the last successful writer wins for local
history only. Application text preference user.config disk roundtrip, physical
keyboard/mouse, physical/mixed-monitor DPI and human command discovery/task times/
wrong-document outcomes remain unverified. The process DPI configuration remains
unchanged. The prepared human checklist stays open under the user's explicit
background-only instruction.

No new game destination, format support, package build, installation/game testing,
broad contact quality or rig generalization is claimed. Advanced 96.2 Apply stays
blocked; all contact/context gates are retained. G1 remains the ONE current M2
substep. The next human task gate is deferred until the user chooses hands-on
observation; future workspace increments require their own bounded evidence.
