# Workspace performance and cleanup — 25 October 2026

Preview.33 (`2.0.0-preview.33-workspace-performance`) completes this bounded
increment inside the ONE incomplete G1/M2 milestone. Fresh qualification passes
**3,829 checks / 19 complete runs / 124 inspected native PNGs**.
A separate matched preview.32 baseline passes 25 checks with 8 inspected captures;
it is excluded from those candidate totals. No active-desktop capture or OS input
injection was used. Assets, captures, projects, preferences and generated binaries
remain local and excluded from publication.

## Changed behavior

Unchanged status updates no longer rebuild the task actions, resize the native
editor or remeasure toolbars/library controls. Geometry, pane visibility, text size,
favorite captions and dynamic drawer content retain their invalidation paths.
Text changes reconcile cached geometry after all font/width mutations. This also
fixes a 175% compact drawer extending beyond the workspace after text resizing.

A hidden linked comparison retains its prepared source but performs no source
pose evaluation or skinning. Reopening samples the current native target frame.
Duplicate native selection notifications do not restart controls preparation;
explicit invalidation still reloads, and queued setup waits until playback stops.
Existing full hash/epoch/frame and publication/Apply dependency checks remain.

Fitting snapshots each exact resource root once per operation, then reopens two
independent read-only preview owners. Reference-body preview skins only the final
composed pose. Independent legacy-two-pass comparisons cover local/world matrices,
all weighted vertices and transformed normals, sparse channels, fractional frames,
and both reviewed retained-control policies. Live authored data stays unchanged.

Review approval retains large immutable receipt/snapshot strings and compares their
exact ordinal values. It captures every mutable recipe field afresh. The same two
export-receipt fields remain excluded as before. 106 independent mutation comparisons
against the complete old serializer pass, including nulls, nested edits, reordering,
equal replacement strings and a changed final snapshot character. Save/export still
recheck exact clip and target content. No timestamp cache or playback hashing was added.

Preview and fix keeps **Review results**, **Compare source** and **Fit start and end**
up front. **More actions** discloses inspection, controls, source timing and expert
tools. Search, Favorites only and Clear filters stay visible; advanced library
filters collapse and display their active count while retaining exact queries.
Ordinary F6/Tab/Space/Enter, hidden-control skipping and focus recovery pass.
The fitting range summary now grows immediately when either side changes; native
preferred-size checks cover enabling/clearing sides at 100/200% without resizing.

## Measurements

The same authentic Snake Wait1 → Mario result, body edit, target/clip content,
frame and query were used on the same host/private-desktop workload. Each timing
has 8 warmups and 31 owner-STA observations; setup, captures and paint completion
are outside the stopwatch. Client widths 1050/1700 use 950 px height, text 100/200%.
The baseline has the previous expanded controls; the candidate has the intentional
new disclosures. These are method timings, not FPS, physical-input latency,
hardware-independent guarantees or human-usability results. Ordinary target sampling
and stage navigation are reported too, including variability and regressions.

| Width / text | Workload | Old median ms | New median ms | Old p95 ms | New p95 ms |
|---|---|---:|---:|---:|---:|
| 1050 / 100% | stable-workspace-status | 7.472 | 0.117 | 9.209 | 0.141 |
| 1050 / 100% | stable-document-status | 7.805 | 0.385 | 8.619 | 0.449 |
| 1050 / 100% | workspace-stage-navigation | 25.286 | 19.912 | 37.741 | 24.005 |
| 1050 / 100% | native-target-sample | 1.015 | 1.001 | 1.184 | 1.520 |
| 1050 / 100% | stable-library-status | 24.136 | 0.103 | 26.016 | 0.120 |
| 1700 / 100% | stable-workspace-status | 1.050 | 0.110 | 1.605 | 0.122 |
| 1700 / 100% | stable-document-status | 1.436 | 0.389 | 2.177 | 0.475 |
| 1700 / 100% | workspace-stage-navigation | 19.577 | 17.547 | 28.307 | 25.268 |
| 1700 / 100% | native-target-sample | 1.031 | 0.989 | 1.670 | 1.230 |
| 1700 / 100% | stable-library-status | 1.136 | 0.099 | 2.473 | 0.128 |
| 1050 / 200% | stable-workspace-status | 7.092 | 0.098 | 7.954 | 0.101 |
| 1050 / 200% | stable-document-status | 7.326 | 0.399 | 8.607 | 0.548 |
| 1050 / 200% | workspace-stage-navigation | 22.921 | 15.682 | 31.797 | 19.235 |
| 1050 / 200% | native-target-sample | 0.989 | 0.993 | 1.157 | 1.284 |
| 1050 / 200% | stable-library-status | 24.191 | 0.104 | 26.928 | 0.113 |
| 1700 / 200% | stable-workspace-status | 1.088 | 0.100 | 1.448 | 0.111 |
| 1700 / 200% | stable-document-status | 1.299 | 0.385 | 1.934 | 0.616 |
| 1700 / 200% | workspace-stage-navigation | 16.551 | 18.467 | 24.548 | 24.995 |
| 1700 / 200% | native-target-sample | 1.085 | 1.047 | 1.560 | 1.116 |
| 1700 / 200% | stable-library-status | 12.293 | 0.099 | 14.267 | 0.107 |
| 1700 / 200% | hidden-comparison-target-sample | 1.542 | 1.002 | 2.334 | 1.444 |

All stable-state cases perform zero new outer geometry/bar/library measurement
passes. 39 unchanged selection callbacks perform zero invalidations/engine loads;
explicit invalidation and deferred-pause setup still work. Hidden comparison
performs zero source samples and resumes without a new source load. Navigation
and sampling preserve exact native target/clip/bone/frame, Undo, dirty state and
session approval without source loading, file hashing, baking or texture loading.

For four >6 MiB procedural recipe checks, legacy full-JSON captures take
163.62 ms versus 25.06 ms
for actual candidate dirty synchronization. Returned/serialized characters fall
from 26,239,680 to 25,440;
immutable strings retain exact values without escaping/copying them. The old
measurement is the serializer subset, the new one includes dirty-sync/list work;
this is a descriptive short observer benchmark, not a frame-rate claim. Malformed
procedural metadata is never baked, saved or exported. Full data, sample arrays,
counters and hashes are retained locally in performance.json,
performance-comparison.json and review-recipe-performance.json.

## Qualification

| Final case | Checks |
|---|---:|
| core-final5 | 380 |
| roots-mario-final5 | 628 |
| roots-snake-final5 | 628 |
| performance-final5 | 47 |
| keyboard-final5 | 200 |
| scaling-final5 | 410 |
| library-final5 | 52 |
| loop-final5 | 185 |
| batch-review-final5 | 182 |
| set-export-final5 | 152 |
| comparison-final5 | 40 |
| comparison-reverse-final5 | 40 |
| reference-moving-final5 | 231 |
| boundaries-final5 | 233 |
| boundaries-reverse-final5 | 220 |
| transitions-final5 | 80 |
| workflow-final5 | 61 |
| lifecycle-final5 | 37 |
| export-final5 | 23 |

Actual build commands use scripts/Build.ps1 with the local .NET 10 toolchain and
NativeDirectory, MSBuild Release/x86 for ControlsAcceptance and RetargetAcceptance,
and dotnet build from engine for FighterPort.Tests. Build/run logs and exact binary,
receipt, source-blob and image hashes are sealed in local qualification.json.
Representative executed commands (fixture/tool/output variables contain local paths):

```powershell
./scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native/"
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -WorkspacePerformance -NativeDirectory $native -OutputDirectory $newOutput
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -WorkspaceKeyboard -NativeDirectory $native -OutputDirectory $newOutput
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -WorkspaceScaling -NativeDirectory $native -OutputDirectory $newOutput
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ReferenceClip Wait1 -ExitSamples 8 -WorkspaceBoundaryFit -WorkspaceFootRecovery -NativeDirectory $native -OutputDirectory $newOutput
```

The final-cohort manifest names exactly the 19 accepted runs. Failed/probe and
superseded captures remain diagnostic evidence and are excluded. All accepted
native PNGs were actually viewed, or verified SHA256-identical to an already
viewed capture with its original path recorded. Captures cover compact/wide and
100/200% text, all five scaling presets, comparison in both authentic directions,
review/set export, fitting/reference preview, native editing and save/reopen.
The new range-height check exposed a one-pixel native/TextRenderer disagreement;
production now honors the native label's full preferred height, without relaxing
that check. Full root-policy regressions and every earlier fitting numeric bound
remain intact; no algorithm/solver tolerance, rig/bind/weight or project schema changed.

At large text sizes, compact drawers intentionally overlay the native editor and
scroll; Focus editor recovers the original viewport. Some 200% review actions are
below the scroll fold. Disabled Favorite text has low contrast when no source is
selected. These are recorded limitations, not claims of completed human usability.

The complete BrawlCrate Model Previewer, original Next, shared native Undo and
current reviewed root policies remain. Original Next/body editing keep source-local
protected controls; explicit fitting retains attack motion-position S/R/T and fits
other reviewed ancestor controls only inside chosen windows. Human/physical input,
monitor DPI, broad contact/continuity quality and game acceptance remain unverified.
Process DPI configuration is unchanged. No next increment is promoted; G1/M2 remains
ONE incomplete milestone. Publish verified source only to public Fighterport/main
using DoctorFlux author/committer attribution and contributor verification.
