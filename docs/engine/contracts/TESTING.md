> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# 20 October — motion-stage, channel and release diagnostics

## 23 October — dark viewer, result saving and selectable pins

This is evidence for the ONE M2 UI/editing slice in plans/current.md. The supplied UI handoffs were read as reference material. Next source `96a4e0e81b1ad79837c307d57197ecd4c756179b` and hidden read-only native components supplied geometry/behavior; no legacy shell was run or adopted. The local reference binary's exact pinned-source correspondence is unproved. No matched live reference, physical input/DPI, game or complete retarget acceptance is claimed.

Actual final build commands use the local SDK and cached packages:

```powershell
$env:DOTNET_CLI_HOME=Join-Path (Get-Location) '.local/dotnet-home'
$env:NUGET_PACKAGES=Join-Path (Get-Location) '.local/packages'
& ./.tools/dotnet/dotnet.exe build FighterPort.slnx -c Release --no-restore -m:1
& ./.tools/dotnet/dotnet.exe build src/FighterPort.Desktop/FighterPort.Desktop.csproj -c Release --no-restore --no-dependencies -m:1
& ./.tools/dotnet/dotnet.exe publish src/FighterPort.Desktop/FighterPort.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore -m:1 -o artifacts/windows-x64
```

All exit0: `artifacts/viewer-parity/selectable-pins-final-build.log`, `final-pin-panel-build.log`, `final-pin-publish.log`. The solution retains offline NuGet vulnerability-source NU1900 warnings and existing AVA3001; this is not a completed vulnerability audit. The native worker was rebuilt earlier for authoritative key removal/timing and is independently exercised below. Current local worker/dependencies/ledger/license are copied into the standard Desktop/CLI and local self-contained worker directories; no native binary is published remotely.

Final focused scopes, run sequentially from the same source build, all exit0. Receipts are `artifacts/viewer-parity/final-pins-{scope}/results.json`:

| Scope | Actual checks | Evidence |
| --- | ---: | --- |
| controls |51| Original numeric soft reach, fixed-lock negative, renamed/unit cases, exact keys/scrub/Undo/Redo/save/reopen; frozen moving references/rotation/timebase/reanchoring/interval replacement; arbitrary-bone handle identities/removal/storage; supplied native four-pin hip edit, exact reopened joints and immutable source bytes. |
| editing |76| Transactional frame clipboard/time/cleanup, stale/source ownership, scalar/quaternion preservation and project writer new-destination/reopen behavior. |
| native |32| Original procedural native clear/insert/delete/shorten, exact freshly decoded keys/tangents and withheld skin; unsupported partial-group rejection; actual draft CHR0 export/reopen through the common writer. |
| workspace |34| Existing native-backed workspace/storage/source isolation, partial-axis native rejection and supported complete-group output. |
| compatibility |9| Historical JSON/hash/project storage compatibility; absent new policies remain omitted. |

Actual scope arguments, with `FIGHTERPORT_BRAWL_WORKER` pointing at `src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe`, are:

```powershell
$tests='tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll'
& ./.tools/dotnet/dotnet.exe $tests artifacts/viewer-parity/final-pins-controls --viewer-controls --native-controls artifacts/native-v2-snake.fighterport
& ./.tools/dotnet/dotnet.exe $tests artifacts/viewer-parity/final-pins-editing --viewer-editing
& ./.tools/dotnet/dotnet.exe $tests artifacts/viewer-parity/final-pins-native --viewer-native
& ./.tools/dotnet/dotnet.exe $tests artifacts/viewer-parity/final-pins-workspace --workspace-only artifacts/viewer-parity/native/source.fighterport
& ./.tools/dotnet/dotnet.exe $tests artifacts/viewer-parity/final-pins-compatibility --receipt-compatibility
& ./.tools/dotnet/dotnet.exe src/FighterPort.Desktop/bin/Release/net10.0/FighterPort.Desktop.dll --verify-viewer artifacts/viewer-parity/final-pin-panel artifacts/native-v2-snake.fighterport Wait1
```

The final detached viewer run passes **212 checks** (`final-pin-panel/receipt.json`), including21 independent camera expectations, actual layout anchors, automatic setup with/without animation, last-valid IK release, FK keys and independent pin picking/world transforms/cancel/stale/soft-anchor gestures. It never shows/activates a window, injects input or copies the desktop. Actual Avalonia controls embed shared offscreen native pixels; raw viewport images remain separate. Ten final images were individually inspected: empty/model/compact, selected FK/soft pins, Animation Controls and four retarget dialogs. Narrow control text wrapping was corrected after inspection. Review SHA256: `92740B4F425D1AE547C7AC8ECB8E9E32BBA56EC61B7E0821E717057B6318C1A6`. Final standard Desktop/Core/Renderer SHA256 are respectively `E3A0290FC8029B67888A2A0665D4DF184375132DA28377924CC08C30BA85A799`, `DBCF792CD85B54BCC53392D510ADF01D04B089CD831EB8551FCF34A57E43B9CD`, `BAA2A0C21D6F38F11A6639DED5C155B3F5B8F737274260DDF9A215E4694F1FD1`.

The actual self-contained Windows executable also exits0 with **212 checks** (`packaged-pins-ui-r2/receipt.json`), launched with `Start-Process -WindowStyle Hidden -Wait -PassThru`, arguments `--verify-viewer artifacts/viewer-parity/packaged-pins-ui-r2 artifacts/native-v2-snake.fighterport Wait1`, and repository working directory. All ten package image SHA256 values exactly match the individually inspected standard images (`standard-image-comparison.json`); no visual difference is hidden by the separate compilation. Packaged Desktop/Core/Renderer hashes are `FD68B0081AE7CEDE25096D45E15629FAE51EFBAB78C6993BE64B90A7D5BD489B`, `2618C6E171A09BE38D0280508FDA776A91F3A0E592222DD5B4845FAAE0C27102`, `6007F324A1EF072653E4F4238F8DDF86CC2B9F138BCAA903369D5A94E9CEBD1E`. This qualifies the local package's detached route, not live pointer/window lifecycle or game behavior. The normal launcher remains scripts/Run.ps1; its optional AppPath can select artifacts/windows-x64/FighterPort.Desktop.exe.

Actual final background model command:

```powershell
./scripts/Test-BackgroundCapture.ps1 -SourceProject artifacts/viewer-parity/final-pins-controls/source.fighterport -TargetProject artifacts/viewer-parity/final-pins-controls/target.fighterport -SourceClip Wait1 -TargetClip Wait1 -OutputDirectory artifacts/viewer-parity/final-pins-background -Frames @(0,.001) -ExtraFrames @() -Angles @('Side','Front','Low') -MaxFramesPerSecond 2 -App src/FighterPort.Desktop/bin/Release/net10.0/FighterPort.Desktop.exe -Wait
```

It completes six paired images (12 viewports), all individually inspected in Side/Front/Low. The original animation is left; the deliberately large manual hip edit is right. Four diamond handles stay on the hand/foot joints while authored anchors remain separate guides; limbs yield toward their native reach limits without stretching, and keys persist at the nearby fractional sample. This is a manual editor witness, not retarget/body/contact or game acceptance. Fingerprint `DDC6BA0764AB5B59025C0A37EAAE5A9116469F25CBA63D34DFFFC3AB4677BCB4`; source/target project hashes `561CEE39763897492F3867DE367496C4C1A8287590C9FBE39FE286CFFFF3A618` / `8CD8756857A47E90EC2DC34C4C287DA52ED3ADD48B04D8073FCE7C877534BF3A`; actual review SHA256 `41600C74A4A876D8D33FEDAEF478FAC8D11A53B9F5F0EC9FB6E342471F8652CA`. Capture Desktop predates only the control-window width fix (`42A059B9…1F183A`); Core/Renderer match final standard hashes. It is serialized/paced BelowNormal, invisible, nonforeground and resumable; no physical input/screen copy occurred.

Failed intermediate evidence remains: the partial native-axis workspace test exposed Y/Z loss and led to an explicit rejection guard; the original moving-pin expectation omitted the fixture's60-degree distal rest, then assumed24fps instead of its actual60fps (corrected independent expectations, no solver tuning); an ambiguous target-typed vector failed compilation and was corrected; earlier UI evidence exposed clipped Controls text. A stalled owned no-window verifier was stopped and its partial outputs retained. None is counted as a pass. The first direct WinExe package launch produced no receipt; only a waited process with a complete receipt can qualify it. Existing default retarget/contact failure remains historical and unresolved; focused scopes do not constitute a full-suite pass. Contacts OFF, all unresolved retarget drafts Review/Apply-blocked, SCN0 inactive, native originals unchanged. Interactive input, pinned reference/DPI parity, broader native families and game/build gates stay open.

## 23 October —96.2 candidate commands

Actual completed commands: `pwsh -NoProfile -File artifacts/body96-build1/Build-And-Test.ps1` (96.1 curve-only), `pwsh -NoProfile -File artifacts/body96-build1/Run-Native8.ps1`, and `pwsh -NoProfile -File artifacts/body96-build3/Build-And-Test.ps1` (combined96.2). Build3 and local self-contained publish exit0, existing AVA3001 warning remains. Separate focused scopes pass17surface/58endpoint/35temporal/46articulation/9compatibility/62scene/96reach/16reach-workflow/69reconstruction/50workflow/129contracts/23native coefficients. Default Core still exits1 at its retained contact/release assertion after433 actual preceding passes; this is not a full-suite pass. Locked cached restore with NuGetAudit=false is not a vulnerability audit. Build2's test syntax compilation failure remains preserved. An overlapping second build1 launch was rejected by the active build.log lock; it produced no second pass.

The96.1 eight-case native trial reopens all8 outputs exactly but retains all4 prior reconstruction failures. Read-only goal diagnostics run against frozen95.6 binaries via `dotnet run --project artifacts/body96-goals/Probe.csproj` with CandidateDirectory pointing at build14; observations2/receipt.json are the actual usable result, not the first empty-object file.

The96.2 campaign was sealed explicitly against build3 App/Tests/CLI and the unchanged compiled worker, then archived. Actual completed campaign commands are `pwsh -NoProfile -File artifacts/background-body96.2/Run-Campaign.ps1 -Phase Export`, `.../Follow-OnChanges.ps1`, and `.../Auto-Assemble.ps1`. Helpers invoke the existing shared offscreen viewport through `scripts/Test-BackgroundCapture.ps1`,4fps maximum, serialized BelowNormal with2processor hints and pause/cancel/resume. All748 prior fractions are bound by the new regression manifest. No focus/input/CopyFromScreen harness, interactive-window claim or game run is involved. Generation is not actual review. Actual background inspection completes5404 paired images/217 sheets, all1907 integer Side frames and803 fractional Side probes, including all748 inherited witnesses. Eight cases have all three angles;28 have full Side plus selected Front/Low. No captured image is excluded; integrity/coverage has no gaps. This is measured failed fitting, not motion/contact acceptance. See RETARGET_VALIDATION and the ONE current M2 queue.

Additional actual completed commands for this same96.2 checkpoint:

```powershell
pwsh -NoProfile -File artifacts/body96-2-native-geometry/Run.ps1
python artifacts/body96-2-native-geometry/Compare-Witnesses.py
python artifacts/background-body96.2/Aggregate-Reviews.py
python artifacts/background-body96.2/Measure-Campaign.py
python artifacts/background-body96.2/Build-Gallery.py
python artifacts/background-body96.2/Verify-Final.py
python artifacts/background-body96.2/Verify-Checkpoint.py
```

The existing compiled x86 native worker is unchanged; this increment builds all six modern components and locally publishes self-contained Desktop, not a new worker or remote binary release. Frozen Core is5E9D6CC84B6EB42CBEEE0DCC2BCDDBD95C4C7349B1D98A9E600D1774A0428D80; adapterC6D256F7AA08934724CBBFFAA29E07F46DE31B0E8F02D7FBFBE0A437FA18768D. Source-checkpoint-files.json binds the seven tested owned C# files. Final integrity receipt SHA256 is0A644F7019FC281AEDE4C3B25987088338CC34A54CE0856E7F3FC2F7E19410A6. Native output36/36 and reconstruction32/36 are separate from unresolved body/action/support. Read-only native geometry covers7733 paired times, with source/native rig/hash preservation checked again. Individual offline native-case preparation timers total3007.7128185seconds, range15.2658703–298.90969seconds; this is not CPU time or campaign wall time.

Selected exact static baseline diagnostics ran with the existing isolated Probe.csproj and Capture.ps1 in artifacts/body96-baseline-witnesses. Actual inspection is12 selected times/36 paired images/six sheets only. The zero-only Frames request exposed a PowerShell truthiness bug and generated extra floor images; the invalid1359-image review claim is explicitly superseded by actual-review.json. These extra captures are not every-frame baseline acceptance. No fresh interactive window/input, DPI, WBFS extraction, game install or playtest was exercised.

## 23 October —95.6 measured checkpoint

Actual commands for this frozen checkpoint (local generated evidence remains ignored):

```powershell
pwsh -NoProfile -File artifacts/body95-build14/Build-And-Test.ps1
pwsh -NoProfile -File artifacts/background-body95.6/Run-Campaign.ps1 -Phase Export
pwsh -NoProfile -File artifacts/background-body95.6/Follow-OnChanges.ps1
pwsh -NoProfile -File artifacts/background-body95.6/Auto-Assemble.ps1
pwsh -NoProfile -File artifacts/body95-6-native-geometry/Run.ps1
python artifacts/background-body95.6/Aggregate-Reviews.py
python artifacts/background-body95.6/Measure-Campaign.py
python artifacts/background-body95.6/Build-Gallery.py
python artifacts/background-body95.6/Verify-Final.py
```

The sealed campaign uses the actual compiled x86 data worker through FIGHTERPORT_BRAWL_WORKER, native matrix --floor/--ledge and private prepared inputs. Source/target screenshots use the shared offscreen viewport at4fps maximum, serialized BelowNormal with2processor hints and pause/cancel/resume. Geometry is a separate read-only CPU1 probe. No focus/input/CopyFromScreen or game run occurred. The build uses locked cached packages with NuGetAudit=false for offline restore; that is not a security-audit pass. Existing AVA3001 warning remains.

Separate scopes pass:13 surface,58 endpoint,35 temporal,46 articulation,9 compatibility,62 scene,96 reach,16 reach-workflow,67 reconstruction,50 workflow,129 contracts and23 coefficients. Do not sum overlapping scopes. Full Core fails its retained required-contact/release assertion after433 actual passes. The compiled no-window UI check has45 passes, not interactive GUI acceptance. Source-checkpoint-files.json binds the11 changed owned C# files to build14; Core53719BD73C8BEDF7A338A43DA570181F6A42DB234F5C8EDF0B695F93A9A54F79 and unchanged worker319356B83A7E4A270AC64F7E726E3536412D9B4ECE5C3481705B52A57304C484 identify actual tested code.


Frozen build14 / `native-body-surface-95.6` completes all 36 fresh native outputs with 273271 exact supported coefficient comparisons, but only 32/36 reference-reconstruction passes. The same four failures remain; two also exceed the skin bound. Body folds, altered reach/sole roll and rear-foot edge placement remain unacceptable or unrated. All 36 remain Review/Apply-blocked; contacts OFF, native rigs/sources unchanged, SCN0 inactive. Actual inspection covers 5243 paired images / 205 sheets, all 1907 integer Side poses and 748 fractional Side samples; eight cases have all three angles, 28 have full Side plus selected Front/Low. No captured image is excluded. Generation, hash verification and actual visual review are distinct receipts.

Fresh native per-clip ratings, image review receipts and7733 geometry pairs are linked from RETARGET_VALIDATION. Head/incidental volume overlap is reported, not a blanket failure or permission for broad sinking. Discrete vertices/time samples do not prove continuous collision freedom or actual support.

The dated trial sections below are retained history; their earlier running/current wording does not supersede this checkpoint or the ONE executable queue in `plans/current.md`.

## 23 October —95.6 frozen run

Actual command: pwsh -NoProfile -File artifacts/body95-build14/Build-And-Test.ps1. Build and local Windows x64 self-contained publish exit0; separate13/58/35/46/9/62/96/16/67/50/129/23 scopes pass, while full Core retains the contact failure after433 assertions. Restore remains locked/cached with explicit offline NuGetAudit=false, not an online security-scan result.

The frozen campaign uses artifacts/background-body95.6/Run-Campaign.ps1 Phase Seal -StableBinaries -ExpectedPipeline native-body-surface-95.6 with explicit build14 App/Tests/CLI and actual compiled worker, Archive-Seal, Phase Export, Follow-OnChanges and Auto-Assemble.700 inherited fractional witnesses are verified from complete94.7 plus partial95.4/95.5 captures/matrices, including partial/unsupported evidence without manufacturing numerical results. Every original36 row/1907 integer Side pose remains required. Capture stays serialized/shared-offscreen/BelowNormal/processor2/4fps with pause/cancel/resume. Generated sheets are not reviewed images; actual inspection and final aggregation remain required. No focus/input/CopyFromScreen, interactive display or game pass is implied.

## 23 October —95.5 commands and fresh focused results

Actual command: pwsh -NoProfile -File artifacts/body95-build13/Build-And-Test.ps1. It builds all six modern components and local self-contained Windows x64 Desktop under the same locked cached restore/BelowNormal/processor2/-m:1/no-shared-compilation policy. NuGetAudit=false is explicitly offline, not a security scan. Build/publish exit0; separate13/58/33/46/9/62/96/16/67/50/129/23 scopes pass. The default suite retains its contact failure after431 actual passes; do not sum overlapping scopes.

Actual95.5 campaign: artifacts/background-body95.5/Run-Campaign.ps1 Phase Seal -StableBinaries -ExpectedPipeline native-body-surface-95.5 with explicit build13 App/Tests/CLI and actual compiled worker; Archive-Seal; Phase Export; Follow-OnChanges; Auto-Assemble. All36 cases/1907integer Side frames remain, plus693 prior fractional witnesses. The inherited-proof helper verifies actual unchanged source project hashes, captured fractional image hashes and partial-matrix sample counts. Source labels select fixtures only. Keep shared offscreen capture serialized, paced at4fps, BelowNormal with processor hint2 and pause/cancel/resume. Actual review and final integrity aggregation remain separate from capture generation.

The95.4 intermediate campaign was cooperatively cancelled, with only its owned native/import tree terminated; partial-checkpoint.json records16 native rows and only two reviewed cases/375 images. Original native files and sealed dependencies verify unchanged. It is not a full matrix or all-frame review. Failed95.1 native-owner and95.2/95.3 CPU-reference runs remain preserved. No visible/input/CopyFromScreen harness or game run is implied.

## 23 October —95.1 current qualification

Actual isolated command: `pwsh -NoProfile -File artifacts/body95-build9/Build-And-Test.ps1`. It builds all six modern components and local self-contained Windows x64 Desktop with locked cached packages, processor hint2, BelowNormal, `-m:1`, no shared compilation and explicit offline NuGetAudit=false. Build/publish exit0; separate13/52/33/46/9/62/96/16/67/50/129/23 scopes pass; default suite fails its retained contact requirement after425 actual assertions. Do not sum overlapping scopes or claim an online vulnerability scan.

Actual background campaign commands use `artifacts/background-body95.4/Run-Campaign.ps1`: Seal with `-StableBinaries -ExpectedPipeline native-body-surface-95.4` and explicit build9 Desktop/Tests/CLI plus the real compiled worker; Archive-Seal; Phase Export; Follow-OnChanges; Auto-Assemble. The sealed campaign inherits every682 prior94.7 fractional time, keeps all36 cases and1907 Side integer poses, processor hint2/BelowNormal and serialized shared offscreen capture at4fps. Use the same campaign pause/cancel/resume controls; never run visible/input/CopyFromScreen harnesses. Review every actual sheet and critical enlarged images, then aggregate/verify. No new screenshot, interactive or game pass is assumed while generation/review remain incomplete. `artifacts/body95-build9/verification.json` and the campaign seal bind the actual binaries.

## 23 October — frozen94.7 validation in progress

Native capability caveat: independent Core fractional-knot tests do not exercise CHR0 encoding. BrawlAdapter/MotionExport still explicitly reject fractional key indices. Record actual selected fractional-key counts in private native rows; any selected fractional result needs its own capable writer or an explicit unsupported outcome, never implicit rounding. The current frozen worker/codecs are unchanged.

Actual command `pwsh -NoProfile -File artifacts/body94-build16/Build-And-Test.ps1` builds all six modern components and publishes the local self-contained Windows x64 Desktop, with locked cached packages, processor hint2, BelowNormal, `-m:1`, disabled shared compilation and explicit offline `NuGetAudit=false`. Both exit0 with the existing AVA3001 private-API warning and zero errors; this is not an online vulnerability audit or remote binary release. Matching normal Core/App/CLI/Tests Core SHA-256 is `44869CBFEFC95732C06006405CC7795B44A2F4C721F1E0C5A963511476C607C2`. Build verification SHA-256 is `F2AB0C28DD011AA9B67EF06870AA3FEE8518D1BE8FEB1D009DB5E6F5EC5592ED`; frozen old normal binaries and native worker remain unchanged.

Separately invoked scopes pass13 body-surface,47 endpoint-geometry,33 temporal-body,46 articulated-body,9 receipt-compatibility,62 scene-body,96 body-reach,16 body-reach-workflow,60 pose-transfer-rewrite,50 pose-rewrite-workflow,129 retarget-contracts and23 native-coefficients assertions. Do not sum these overlapping scopes. The default Core path still exits1 at the unchanged hand/ledge/foot contact expectation after420 actual PASS lines. `UnexpectedFailure=false` identifies that retained failure; it is not a passing full suite. Independent unit/axis skin discrepancy is0.000007585002 versus the unchanged0.00008 bound. Independent original unequal-proportion geometry errors are0.55689305→0.038600564 and0.16666675→0.026693456; those are soft geometry/action evidence, not contact passes.

The fresh sealed campaign is `artifacts/background-body94.7`. Actual Seal uses `Run-Campaign.ps1 -Phase Seal -StableBinaries -ExpectedPipeline native-body-surface-94.7`, regular build16 Desktop/Tests/CLI paths and the unchanged compiled native worker. `Archive-Seal.ps1` preserves40 sealed own proof files. Export, `Follow-OnChanges.ps1` and `Auto-Assemble.ps1` run as separate root-owned BelowNormal processes, with one serialized4fps invisible renderer and processor hint2. Probe inheritance retains every603 prior93.5 fractional Side time over36 cases. The legacy helper filename baseline92-capture-probes.json now binds that exact93.5 evidence, not invented92 values. New outputs and actual reviewed-image receipts are separate from all prior versions. Native generation and actual review now complete:5041 unique paired images/203 sheets,1907 integer Side frames plus682 fractional Side times, eight full-three-angle and28 selected Front/Low cases, with0 exclusions/integrity gaps.36 outputs reopen exactly,273271 native coefficient comparisons pass and32 reference reconstructions pass; four failures remain recorded. No generated sheet becomes an actual review automatically.

Pre-final private smoke3 uses separately compiled Core `41CEB0FE65E93A6D21DDCE393FC1BBC8BDDACAA2E3FC77AC1EBE654290E4D964` from build15. It has the same final numerical source but a different binary identity and is not relabeled build16/native acceptance. `pwsh -NoProfile -File artifacts/body93-diagnostics/Run.ps1 -CandidateDirectory artifacts/body94-build15/build/bin/FighterPort.Adapters.Brawl/release -ExpectedCoreSha256 <that-hash> -OutputDirectory artifacts/body94-smoke3 -Selection smoke -Build -CheckOrder -Historical92` completes four cases/32 poses with immutable inputs. The old ignored report helper's93 labels are historical captions; the actual receipt Pipeline/Core identify94.7. Native/current displayed output is separately bound to frozen Core4486 above.

The completed94.7 aggregation/review commands actually run after image inspection are:

```powershell
python artifacts/background-body94.7/Measure-Campaign.py
python artifacts/background-body94.7/Aggregate-Reviews.py
python artifacts/background-body94.7/Build-Gallery.py
python artifacts/background-body94.7/Verify-Final.py
pwsh -NoProfile -File artifacts/body94-ui-probe/After-Capture.ps1
pwsh -NoProfile -File artifacts/body94-native-temporal-geometry/After-Capture.ps1
```

The two no-window supervisors wait for completed capture generation, then run `body94-ui-probe/Run.ps1 -DesktopBinaryDirectory artifacts/body94-build16/build/bin/FighterPort.Desktop/release -Run` and `body94-native-temporal-geometry/Run.ps1` followed by `Summarize.py`, sequentially. UI44 passes; native geometry8/2111 completes with immutable proofs. These helpers are ignored local diagnostics, not distributed game assets or new native codecs. Capture/follow/assembler/UI/geometry owners all exit0. For source-reproducible focused coverage the normal test executable accepts `--body-surface`, `--endpoint-geometry` and `--temporal-body`; use a fresh ignored output directory for each scope. Default full Core still fails the retained contact requirement.

Final integrity SHA-256 `312473B691BEBE7CF1D0229B408BA6BD04E31CA7339566B971FA389361FBF0DE`, actual-review manifest `214818831990D32BFCEC680B9777378BE01318BC85C48D079234D3879D8BF312`, all36 per-clip measurements `5A59938FADBB5B909D59F0EFA69D8E79ADD24B967BE4C4C061C9E068B7BABF0D`. Native geometry receipt `CB701DE01CC54FF37D5C1678729F224EA63A7F9680270A105EA4CE8DEA76E52A`, frames `D01CEDB8352A0B80B0C24BF74EAD671E3598B53897E4E23AF9B45AF25253E760`, summary `EB3EB60E7851F2B1C23C8ACA79E099B5416A02A1B4158AF8A2AFC5D8B1596BC2`. The four source/native regional witnesses and remaining failures are reported in RETARGET_VALIDATION; no continuous contact/style or in-game acceptance is inferred.

## 22 October 2026 — pipeline93.5: independent, native and background qualification

The current candidate is `native-body-trajectory-93.5`, scene13 / endpoint source-affinity1. `pwsh -NoProfile -File artifacts/body93-build12/Build-And-Test.ps1` actually builds the six modern components and publishes a local Windows x64 self-contained Desktop into isolated directories. Its SDK invocations use `dotnet build FighterPort.slnx -c Release --artifacts-path <new-root> -m:1` and `dotnet publish src/FighterPort.Desktop/FighterPort.Desktop.csproj -c Release --artifacts-path <same-root> -r win-x64 --self-contained true -o <new-publish> -m:1`, with locked cached packages, disabled shared compilation and explicit offline `NuGetAudit=false`. Build/publish both exit0; build records one existing AVA3001 private-Avalonia-API warning and zero errors. This does not establish an online vulnerability scan, remote binary release or game test.

Actual separately invoked scopes pass40 endpoint-geometry,33 temporal-body,46 articulated-body,9 receipt-compatibility,62 scene-body,96 body-reach,16 body-reach-workflow,60 pose-transfer-rewrite,50 pose-rewrite-workflow,129 retarget-contracts and23 native-coefficients assertions. The default Core path exits1 at the unchanged `Retargeted hand/ledge and foot contacts use the shared solver and explicit release interval` requirement after413 actual PASS lines. Do not add overlapping scopes, treat the wrapper's `UnexpectedFailure=false` as a passing full suite, or infer checks after that failure. The new endpoint scope retains the old distant-source negative and independently tests a very large free endpoint volume in two target sizes; source proximity is not established by a large radius. Temporal coverage includes analytic derivatives, coordinate/unit and supplied rename/storage-permutation equivalence, periodic correction/derivative seams, time ownership, cancellation/resources and actual saved single/batch keys. These do not qualify all contacts, anatomy or actions.

The actual matching regular Core/Tests/CLI/Desktop Core SHA-256 is `DCFF23869410785CB0C431050A055E5FF27DCF61E2297F350CFA542555CAE264`. The separately compiled RID publish is not silently substituted for that identity. The build receipt proves the four frozen normal92 binaries, including the worker, unchanged. Build verification is `artifacts/body93-build12/verification.json`, SHA-256 `572707F25C3B9F4E271EFC23BD6F02F3E528EFA2AAA6D4A60C71B1CB05EC560E`.

`pwsh -NoProfile -File artifacts/body93-ui-probe12/Run.ps1 -DesktopBinaryDirectory artifacts/body93-build12/build/bin/FighterPort.Desktop/release -Run` freshly passes40 actual no-window control/event and compiled presentation/ledger checks against that same final Core, Desktop SHA-256 `7B12AEC045D73AA4526C86A715CDC489714A0EBEAFC3ABAA329107C7A27160D4`. The isolated probe retains current/shared/inherited settings, explicit false values, saved contact preference, reset/use-shared events and truthful historical/unsupported/termination/derived-channel formatting. Its formatter cases use explicitly synthetic receipts, not fake successful motion. Debounce is stopped before any solver, worker or capture job; source/target keys/history and frozen binaries remain unchanged. Receipt `artifacts/body93-ui-probe12/ui-semantic.json` SHA-256 is `77D8F68D941AF9A45C66A69582C9B96D5141FB534FFAA75EEDEC2A9A851BA11E`. These40 are separate from endpoint40 above and do not prove interactive display/focus/input or the mapping dialog.

`pwsh -NoProfile -File artifacts/body93-native-temporal-geometry/Run.ps1` completes the separate corrected read-only run2: eight existing directional cases,2111 paired source/native times, all521 integers and2084 quarter-lattice probes including those integers, plus27 other fractions. It compiles only an isolated sampling reporter against the frozen Core/adapter copies; no production project, solve, worker or renderer runs. BelowNormal/one-processor and original/rig/native/binary hash checks are recorded in run2/invocation.json. The first reporter attempt failed final serialization and remains excluded. Final receipt SHA-256 is `1840F7CCF044939494CD356177AC57E766D7875BEAD9161CF8F93F876E8A02E2`; per-time JSONL is `1A9321AD40FB527E5CD13372B943107D75CECC2D1828B3E90021756122425BD7`; summary is `760CF0162BA215CE49BA2D7F4A93AF8422BC0F03E90260ADB705F7719C92262D`. Do not rerun over existing evidence. Distances use supplied floor/ideal corner and unspecified native units with per-rig characteristic normalization; sampled overlap is not automatically a required-contact failure or a continuous collision certificate. The native source/target comparison and limitations are in RETARGET_VALIDATION.

The actual four-case/32-pose diagnostic command is:

```powershell
pwsh -NoProfile -File artifacts/body93-diagnostics/Run.ps1 `
  -CandidateDirectory artifacts/body93-build12/build/bin/FighterPort.Adapters.Brawl/release `
  -ExpectedCoreSha256 DCFF23869410785CB0C431050A055E5FF27DCF61E2297F350CFA542555CAE264 `
  -OutputDirectory artifacts/body93-smoke12 -Selection smoke -Build -CheckOrder -Historical92
```

It completes with no case failures, exact source/baseline/unfitted-reach equality against the prior93.4 input arrays, unchanged source/rig proofs and exact fresh reverse-query results (maximum skin difference0). Its four cases are both directions of Wait1 and CliffAttackQuick; it is not a new36-case run or actual private workflow batch test. Each prepared fitter is constructed once; subsequent diagnostic queries evaluate its immutable correction field. No worker, native encoding or rendering runs in this command. Preparation case elapsed times are3.853–44.306 seconds, not measured process CPU or campaign wall time. `receipt.json` SHA-256 is `EE2BA817A4B7ED115F94D4960A53172AC34D3A097B459A364093E7309D36EC87`; the separately hash-bound `witness-stage-measurements.{json,md}` and `comparison11.json` retain actual signed distances, degrees and stage arrays. Improvement is mixed; body/action/contact acceptance stays open in RETARGET_VALIDATION.

The stopped93.4 broad campaign is preserved under `artifacts/background-body93.4`; three actual native rows must not become36 assumed passes. Its partial inventory, subsequent123-image QuickAttack actual review and six-image matched frame22 stage proof retain their own23AE binary identity. The separate `artifacts/background-body93.5` campaign is sealed against DCFF and now completes36 native rows, fresh snapshots and actual background review, with exact final coverage/hashes below. It retains all1907 integer Side poses plus prior fractional probes, supplied floor/optional ideal-corner context, eight full-three-angle cases and selected Front/Low for28. Its recorded Seal/Export arguments and `Follow-OnChanges.ps1 -FollowTimeoutMinutes 360` retain revision-gated capture ownership. Offscreen rendering is serialized, no more than4fps, BelowNormal, child processor hint2, isolated data and pause/cancel/resume. Completed image review does not establish accepted motion, interactive input or game behavior. Contacts remain OFF, Apply blocked, native skeletons unchanged and SCN0 inactive. Earlier92 evidence remains historical and is never relabeled93.5.

### Completed93.5 native matrix run

The actual frozen export command is:

```powershell
pwsh -NoProfile -File artifacts/background-body93.5/Run-Campaign.ps1 `
  -Phase Export -MaxFramesPerSecond 4 `
  *> artifacts/background-body93.5/export.controller.log
```

Session92140 exits0. The wrapper sets `FIGHTERPORT_BRAWL_WORKER` to the actual unchanged compiled net472 worker and invokes the following recorded test children sequentially, with the sealed per-context fixture list in `FIGHTERPORT_RETARGET_CASES`:

```powershell
artifacts/body93-build12/build/bin/FighterPort.Tests/release/FighterPort.Tests.exe `
  artifacts/background-body93.5/native-ledge --retarget-matrix `
  artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --ledge
artifacts/body93-build12/build/bin/FighterPort.Tests/release/FighterPort.Tests.exe `
  artifacts/background-body93.5/native-floor --retarget-matrix `
  artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --floor
```

Both fresh export/reopen children exit0; their actual exit receipts are timestamped20:55:40UTC and21:05:58UTC respectively. They retain BelowNormal priority, child processor hint2, isolated data and the sealed source/binary identities. Core is `DCFF23869410785CB0C431050A055E5FF27DCF61E2297F350CFA542555CAE264`; worker SHA-256 remains `319356B83A7E4A270AC64F7E726E3536412D9B4ECE5C3481705B52A57304C484`. No rebuild, original-file write, mod installation or game run is part of these commands.

All26 floor plus ten ledge rows have exact supported native keys/tangents,272606 comparisons total. Reference reconstruction passes34/36 versus historical92's1/36. All new skin/angular/translation/Euler maxima decrease against their respective prepared references; scale maxima remain equal. Snake→Mario CliffClimbQuick and DownBoundD still fail Euler-channel fidelity at0.14307594 and0.061012268degrees against0.05729578; all their other rated representation metrics pass. Keep these failures and the complete36-row table in RETARGET_VALIDATION. Exact native output is not a source-action, support, contact, full collision or displayed-motion pass. The existing default Core contact failure after413 assertions remains unchanged.

`artifacts/body93-native-regression-summary/summary.json` SHA-256 `900137E35285207CA51C869AC0370AFDBB7A834E20717DDA494493F6BC932394` binds all36 per-case metrics, native files, final matrix hashes and the frozen92 comparison; no absent row/value becomes zero. Floor/ledge matrix hashes are `BDAED94233091CC8BB3C1F1184B3F0512D1348D7DDB101401C93D9CD260B1D60` / `9CDD8F43B1AE39DAB35F7E5C9A02ABEFD8B7BF40BBD4FD3F82F73607705ABA5B`. The matching native coefficient count in92 remains a separate historical run, not an additive distinct-check count.

Follow-on imports, all36 native-backed capture cases and actual review are complete. Follow and the sole assembler exit0. After every per-case actual inspection, `python artifacts/background-body93.5/Aggregate-Reviews.py`, `python artifacts/background-body93.5/Build-Gallery.py` and `python artifacts/background-body93.5/Verify-Final.py` each exit0. Aggregation verifies4802 actually reviewed paired images/203 sheets, all1907 integer Side frames,603 Side fractional/closing probes, eight full-three-angle cases and28 full-Side/selected-Front-Low cases, with no exclusions or integrity gaps. All64 capture receipts bind the same sealed binaries, source/native identities and invisible4fps BelowNormal background renderer. These commands verify review claims/files; they never perform visual review themselves. The final receipt SHA-256 is `09CECB13CF4A9D6303BCC5A23F3B207080EBBDC4C936CDA2BF9AFDD05B0B0A8B`, actual-review manifest `D4BF4C13506CA586C86713E62683BE3F2F54C61127C748918025611FB5350927`, and per-clip measurements `A89CD5E22AF7FBD37F14CB0108DE0547ADF14F0F70A73BC82296634F31BEB94D`. The local index.html links all36 actual comparisons. Visible folds, hand gaps, knee penetration and late upright recovery mismatches remain Review;34 representation passes do not accept those poses. Contacts stay OFF, Apply blocked, native skeletons unchanged and SCN0 inactive. Interactive input, continuous collision and game testing remain unverified.

## 22 October 2026 — pipeline 92: final native and actual-frame receipts

The 36-case pipeline-92 qualification is recorded: 36 exact native outputs / 272606 supported key/tangent comparisons, but only Mario→Snake SlipWait meets the reference-reconstruction bounds. All 36 remain Review/Apply-blocked. Actual review covers 4402 paired images / 191 sheets, every 1907 integer Side frame plus 493 Side fractional/closing probes; eight cases have all three angles and 28 have selected Front/Low. No image was excluded and integrity/coverage verification has no gaps. This is neither complete retargeting nor game acceptance.

Actual commands use the local worker through FIGHTERPORT_BRAWL_WORKER and the existing matrix path: `FighterPort.Tests.exe <new-native-directory> --retarget-matrix <prepared-Mario> <prepared-Snake> --floor` and separately `--ledge`, followed by fresh `FighterPort.Cli.exe import-brawl <original-model> <new-chr0> <new-snapshot>`. The private sealed Run-Campaign.ps1 Export and revision-gated Follow preserve full arguments, binary/source/controller hashes and per-case rows. Both owners and the sole sheet assembler exit 0. Actual source/target images use `scripts/Test-BackgroundCapture.ps1`, 640-pixel panes, 4 fps, BelowNormal, child-only processor hint 2; no desktop input, focus changes or CopyFromScreen.

The 272606 native comparisons, actual 36-case review and only 1 reference pass are independent. The 14 read-only native user witnesses pass input/deform preservation but retain measured support failures; twelve supplementary actually reviewed images and 18 published-build parity images are outside main 4402. Each test/build configuration keeps its exact binary identity; source/report-only evidence does not relabel D25 native outputs. The fresh default failure after 340 assertions, offline vulnerability-audit limitation and unexercised interactive/game/deferred gates remain recorded below.


Final receipt compatibility92: `pwsh -NoProfile -File artifacts/body92-compatibility/Build-And-Test.ps1` uses a new isolated build root; `FighterPort.Tests.exe <new-directory> --receipt-compatibility` passes9 historical JSON/hash/actual project persistence checks. Fresh46/62/96/16/60/45/129 scopes pass, and the default path includes the new9 before retaining the original contact failure after340 actual assertions. The reporting-only Core is `B7D5FD5B51A1CB3B4376941BFF56E5E0A11B9F9C9649733DE4D6975672AE3051`; all41 ordinary witness pose/source arrays exactly match D25. Normal native/capture binaries remain sealed and unchanged. This preserves old receipt absence and source ancestry rather than inserting new null/empty evidence; no numerical/contact/native improvement is inferred.

`pwsh -NoProfile -File artifacts/body92-final-ui/Build-And-Probe.ps1` freshly compiles the final nullable-ledger/report consumers in isolated outputs and passes the same36 actual no-window control/formatter checks. Core7FE/desktopEEC hashes, prior-check identity comparison and unchanged normal seals are in its `verification.json`. Build errors are zero; AVA3001 and a CS8604 warning in the ignored current-ledger probe are recorded. No window/focus/input/solver/native job is launched by this test; the mapping dialog and displayed interaction remain separate.

Final source checkpoint0656fba is also built through the actual SDK commands `dotnet build FighterPort.slnx -c Release --artifacts-path <new-root> -m:1` and `dotnet publish src/FighterPort.Desktop/FighterPort.Desktop.csproj -c Release -r win-x64 --self-contained true --artifacts-path <same-root> -o <new-publish> -m:1`, using the repository's local SDK, locked cached packages and explicit offline NuGetAudit=false. The ignored `body92-final-modern/Run.ps1` retains all complete arguments/logs. Both SDK operations pass; actual separate9/46 checks pass and default Core retains its original failure after340 PASS: lines. An initial helper log-pattern error is retained and corrected only by the independent `Verify.ps1` summary; no full-suite pass is manufactured. Core377(normal)/42DF(RID), all four unchanged D25 normal seals, worker byte-copy proof and actual source identity are recorded in that root.

`Capture-Published.ps1 -Source Snake` and `-Source Mario` in the same root use the shared paced invisible capture helper for18 bidirectional slow-climb witness images. Each new framebuffer/PNG hash exactly equals its actually reviewed D25 counterpart. This is published-build equivalence only, excluded from main36 actual-review counts. No newly enabled contacts, source mutation, interactive acceptance or game test is inferred.

The92 native/capture campaign remains partial at33/36 comparisons at18:10UTC. The unchanged sealed helper's idle Follow was stopped separately from Export and resumed through `pwsh -NoProfile -File artifacts/background-body92/Follow-OnChanges.ps1 -FollowTimeoutMinutes 360`. This ignored wrapper gates only the exact idle sleep on matrix-content revisions and retains pause/cancel/timeout behavior. Nine isolated wrapper tests pass, and the real startup verifies3,965 original file/hash/mtime comparisons with no changes (32 cases/57 capture receipts/32 actual reviews/3,876 images). That is coordinator preservation, not3,965 Core or retarget acceptance assertions. Its source/helper identities and retained failed attempts are in the private test/wrapper receipts; the original binary/input seal is unchanged. Native and actual-review completion remain separate gates.

Receipt-only92 follow-up: `pwsh -NoProfile -File artifacts/body92-stop-receipts/Build-And-Test.ps1` builds only to its isolated global output/intermediate directories;46 focused checks pass. `pwsh -NoProfile -File artifacts/body92-stop-receipts/Run-Remaining.ps1` freshly passes62/96/16/60/45/129 scopes; default Core fails the retained contact requirement after331 assertions. Exact40 winding-condition plus41 ordinary pose/source parity against frozen D25 passes. The reporting Core is `0EA9AA6EAEEB36073A332873527D0FA6DA0D03D01A465BE5C87AAD8DF5F48EC5`; native/capture qualification remains bound to actual D25, not relabeled. The isolated stop-UI verification records36 actual no-window checks, with all31 prior cases retained and five explicit compiled formatter cases; no interactive/display/native acceptance is inferred. `body92-stop-receipts/verification.json`, `remaining-results.json`, `actual41/parity.json` and `body92-stop-ui/verification.json` under ignored artifacts contain exact invocations, hashes and limitations.

## 22 October92 — coordinated pose and adaptive curve checks

Use the existing local SDK/build scripts. `FighterPort.Tests.exe <new-directory> --articulated-body` adds analytic native-SRT/world/skin derivative checks and independently authored unequal-arm/leg/body surface fixtures, positive anisotropic scale, source/rig/channel immutability, actor winding, deterministic fitting, renamed/storage-permuted and coordinate/unit-equivalent inputs. The default Core run includes these checks before the retained contact gate. These are bounded fitting/representation checks, not automatic contact or arbitrary-anatomy acceptance.

Retain `--scene-body`, `--body-reach`, `--body-reach-workflow`, `--pose-transfer-rewrite`, `--pose-rewrite-workflow` and `--retarget-contracts`. New curve checks cover shared integral-key tangent refinement, exact key values, independent withheld/closing samples and aggregate offline work rejection. Run `--retarget-matrix` with the actual prepared Mario/Snake inputs and worker into NEW outputs; freshly reopen through the real adapter. Keep all36 directional cases and the Wait1 rear-foot/CliffAttackQuick22 witnesses. Capture every integer Side pose plus fractional/angle probes through `Test-BackgroundCapture.ps1`, paced/serialized/BelowNormal with no foreground/input capture; actually inspect the resulting images. Qualification commands, binary identities, counts and failures belong in RETARGET_VALIDATION. Earlier run totals are not fresh passes.

Fresh recorded commands are `./scripts/Build.ps1 -Publish`, then the Tests executable into `artifacts/body92-checks5/<scope>` for the seven scopes above plus `--articulated-body`. Separate counts40/62/96/16/60/45/129 pass; default `artifacts/body92-checks5/full-core` fails its retained contact assertion after325 preceding passes. This is not a full-suite or game pass. The unchanged actual native worker is selected by the campaign through `FIGHTERPORT_BRAWL_WORKER`. Actual no-window `./artifacts/body92-ui/Run-Probe.ps1 -StableBinaries` passes31 checks against the final matching normal DLLs, preserving the26 earlier real event checks and adding five explicitly synthetic receipt-presentation checks. No interactive window, mapping dialog, input/focus or displayed playback acceptance is claimed by that runner.

## 22 October91.1 — resource-only continuation

Actual local `./scripts/Build.ps1 -Publish` succeeds (`artifacts/scene91p1-publish.log`); normal Core SHA256 is `A87E920402EA2ABB42ACE923CC88B656BE414FF57FF94C96BB3E7C4D869DEBE8`. The self-contained RID Core is separately identified as `7094752E1DEDA7C6F609A63093F787941D36255F51235BC558E1A7DA730F8D75`; do not relabel normal-capture receipts as RID evidence. Known AVA3001 private-Avalonia-API and NU1900 unavailable vulnerability metadata warnings remain; this is not a completed online vulnerability scan. No worker rebuild, external publication, mod installation or game test runs.

Isolated modern builds use the local SDK with `-c Release --no-restore -m:1 -o artifacts/scene91p1-binaries`, preserving the frozen91 capture binaries before later normal publish. Actual test executable flags are `--scene-body`, `--body-reach`, `--body-reach-workflow`, `--pose-transfer-rewrite`, `--pose-rewrite-workflow`, `--retarget-contracts`, plus a default run. Under `artifacts/scene91p1-checks1` their results record62/96/16/47/45/129 separate passes, not additive coverage. The62 scope includes26 scene,20 material-region and16 policy/resource checks. Three new assertions cover a real prepared resource estimate, overflow-safe excessive-count rejection before sampling/mutation and nullable historical budget fields. Default Core exits1 at the retained contact requirement after285 preceding passes; later checks are not reached. The unchanged UI code's actual26 no-window control/event checks remain explicitly91 evidence, not a new interactive desktop run.

The ignored `artifacts/scene91p1-probe` diagnostic was built against the isolated91.1 DLLs and run with `--all-frames artifacts/scene91p1-probe/results.json`. `equivalence.json` compares its entire witness arrays against91 probe5: all36 cases/1907 integers+107 fractions are exactly equal, with unchanged source/target proofs. This establishes resource-only direct-pose equivalence; it does not repeat the26 native floor exports or certify action quality. Matched source/baseline/reach/scene/fresh-native diagnosis at frames23–25 is in `artifacts/body91-fold-trace/TRACE.md` and `results.json`; it does not replace the all-frame image review.

The new ignored `artifacts/background-scene91p1/Run-Campaign.ps1` uses sequential `-Phase Seal` then `-Phase Export`, and `-Phase Follow -AllowPartial -FollowTimeoutMinutes 180` after sealing. Its ten ledge rows use the actual compiled worker, existing test `--retarget-matrix <prepared-Mario> <prepared-Snake> --ledge` path and `scripts/Test-BackgroundCapture.ps1`. The first Follow call before sealing correctly failed without capture and was restarted after Seal; no resulting image or native row is counted from that call. Export and Follow subsequently exit0. Serialized4fps offscreen jobs remain BelowNormal with two-processor child hints and pause/cancel/resume. Only actual inspected cells/images receive per-case review receipts. Older91 floor outputs and its ten preflight-rejected ledge rows remain unchanged, with their capture binaries archived and hashes retained. Combined current coverage, individual native/curve measurements and actual unresolved poses are in RETARGET_VALIDATION; no focus/input/CopyFromScreen test was run.

`Aggregate-Floor-Reviews.py`, the91.1 `Aggregate-Reviews.py` and `artifacts/background-volume91-review/Build-Combined.py` verify completed actual-review claims, all1907 integer Side frames/488 fractions/extra angles, image/sheet/native/snapshot hashes and retained binary/source inputs. They do not themselves visually inspect. Actual coverage is4387 paired images/191 sheets with no gaps/exclusions. The ignored coordinator retained a falsely negative generation flag because stdout-copy Task results entered its temporary input count. Its original file remains; independent `generation-verification.json` checks all ten native rows/eighteen complete no-visible/no-foreground capture comparisons. This metadata issue does not turn unreviewed images into evidence or alter production/native output. All36 exact-native checks pass; only6 curve-representation rows pass and every action/contact result remains Review.

## 22 October91 — volume policy

Actual successful commands: `./scripts/Build.ps1 -Publish`; `./artifacts/scene91-checks5/Run.ps1 -Full` (ignored coordinator helper around the normal test executable); `./artifacts/body91-ui/Run-Probe.ps1 -StableBinaries` (isolated no-window real control events). Focused scopes59/96/16/47/45/129 pass separately; default Core still exits1 at the retained contact assertion after282 preceding passes. The scene scope retains explicit strict floor/wall counterexamples and independent report-overlap support/volume oracles, reverse/permutation, actual saved profiles/batches and missing-policy historical receipt tests. The initial mixed-corner failure is preserved in build3 logs; source6 fixes exact endpoint rejection without relaxing bounds. Old source conflict fixtures were changed to actually touch the supplied scene under the new source-nearness rule; their physical assertions and clipping checks stay unchanged.

Fresh direct diagnosis uses `artifacts/scene90-probe/bin/Release/net10.0/Probe.exe --all-frames artifacts/scene91-probe5.json` and the ignored reader with `-InputPath artifacts/scene91-probe5.json -OutputDirectory artifacts/scene91-analysis5 -RequireAllFrames`. The receipt records the actual91 Core, all36 cases/2014 poses and unchanged source/target data; the executable's historic folder name is not its pipeline identity. Native/image qualification uses new `artifacts/background-scene91/Run-Campaign.ps1` phases Seal/Export/Follow, the existing worker and shared offscreen capture script. Keep binaries frozen after sealing. Only inspected completed sheets and original images may receive actual review receipts. No focus/input/CopyFromScreen harness or game playtest runs.

## 22 October90 — scene/body continuation

Actual commands include `./scripts/Build.ps1 -Publish` (successful build9), the test executable with `--scene-body`, `--body-reach`, `--body-reach-workflow`, `--pose-transfer-rewrite`, `--pose-rewrite-workflow`, `--retarget-contracts`, and a default run. Recorded in `artifacts/scene90-checks9`: separate39/96/16/47/45/129 passes; default Core retains its contact failure after262 preceding assertions. Do not add overlapping counts or call that a passing full suite. Build5 failed on an ambiguous fixture constructor and was corrected; the intermediate workflow assertions assumed the former scene-inactive contract and were updated to isolate contact flags under identical geometry, retaining the raw0.03 sole oracle.

`artifacts/scene90-probe/bin/Release/net10.0/Probe.exe --all-frames <new-results-file>` is a private ignored direct-pose diagnostic. It compares authentic prepared geometry, scene-off reach and scene/body output; it does not encode or review displayed meshes. The supplied Mario/Snake inputs remain available and source proofs unchanged. `artifacts/scene90-analysis/Read-Probe.ps1` is an ignored diagnostic reader with exact recorded usage in DIAGNOSIS; native/rendered evidence requires the separate campaign. All screenshot work uses `scripts/Test-BackgroundCapture.ps1`, serialized paced offscreen rendering, BelowNormal priority and child-only two-processor hints. No foreground/input/CopyFromScreen harness is used; generated images require actual inspection.

## 22 October89 — native-path reach checks

Actual final local commands, with private inputs and generated outputs ignored:

```powershell
./scripts/Build.ps1 -Native -Publish # final9: native worker succeeds with installed SDK registry access
./scripts/Build.ps1 -Publish         # final10: final numerical changes; worker unchanged
$env:DOTNET_ROOT = Join-Path $PWD '.tools/dotnet'
$env:DOTNET_PROCESSOR_COUNT = '2'
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/body89-body-reach10 --body-reach
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/body89-body-reach-workflow10 --body-reach-workflow
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/body89-pose-transfer-rewrite10 --pose-transfer-rewrite
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/body89-pose-rewrite-workflow10 --pose-rewrite-workflow
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/body89-retarget-contracts10 --retarget-contracts
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/body89-full-core10
```

Focused counts are93,16,47,45 and129 independently. Full Core remains failed at the retained retargeted-contact assertion after220 preceding passes; later checks are unverified. New reach checks also run in that default command. Builds succeed; existing AVA3001 is retained and NU1900 reports unavailable NuGet vulnerability metadata, not a completed vulnerability scan. The restricted first native build failed its Windows SDK registry lookup; the authorized local build then succeeded. Local self-contained publish is not remote/binary release authorization.

The new independent fixture scope covers complete arbitrary paths, unequal segmentation, root/rest/coordinate/unit transformations, authored scale, native skin, full turns, source/rest bend provenance, interior-path agreement, source/rig/T/S immutability, hard limits, affine capability rejection, geometric impossibility, deterministic ordering and cancellation. The workflow scope proves real fitted Cartesian output both directions, fitting-off raw transfer, floor/edge independence, single/batch equality, blocked Apply, editable draft save/reopen and authentic ancestry. Core settings checks do not prove an interactive checkbox gesture.

Actual UI semantics were separately exercised through `artifacts/body89-ui11/Run-Probe.ps1` against an isolated Desktop build (`--no-restore -p:BuildProjectReferences=false -o artifacts/body89-ui11/desktop`). The13 passed checks instantiate the real RetargetPanel without a window/lifetime, raise actual checkbox/row/scope/button events, verify overrides/shared/inherited-false/reset and retained contact preferences, stop debounce before solving and assert unchanged sources/target/history. `ui-semantic.json` records the actual isolated binary hash and `sealed-comparison.json` proves the capture binaries were not changed. This is control-event evidence, not interactive GUI acceptance. Probe setup/dependency failures were corrected inside ignored artifacts; no source behavior was faked in the runner.

The ignored private campaign controller is `artifacts/background-body89/Run-Campaign.ps1`: seal final binaries with the explicit normal Desktop App path, then Export26 floor/10 ledge rows and Follow incremental fresh-native import/capture. It uses the same actual public `--retarget-matrix` and `scripts/Test-BackgroundCapture.ps1` entry points shown below, with new output directories. Normal and RID-published Core binaries differ; equality sealing uses normal Desktop/CLI/Tests consistently. Never rewrite seals to relabel later binaries. Offscreen images require separate actual per-case review receipts; no foreground/input/CopyFromScreen test is run. Exact89 measurements, coverage and unresolved failures are in RETARGET_VALIDATION. Historical88 commands/results below remain separate evidence.

The completed campaign actually reviewed 4,320 paired images across all 36 cases: 1,907 integer Side poses, 467 fractional Side probes, eight full-three-angle cases and 28 Side-plus-selected-angle cases. All 190 inspected sheets and every image/manifest/review hash verify, with no exclusions. `index.html` combines supplementary angles into the same 36-clip browser. Actual commands: `Run-Campaign.ps1 -Phase Export`, `-Phase Reopen` and the serialized `-Phase Follow`; then `python artifacts/background-body89/Aggregate-Reviews.py` and `Build-Gallery.py`. Follow and aggregation exit 0. These are new89 receipts, not reclassified88 images or passing body/contact results.

After Follow exited, `./scripts/Build.ps1 -Publish` completed final12 (log `artifacts/body89-publish12.log`), retaining the tested normal Core hash and the unchanged worker. `artifacts/body89-final12/Run-UiSemantic.ps1 -Run` passes the same 13 real control-event checks against the actual final-published components in a new runner. `Run-Witnesses.ps1 -Run` uses `scripts/Test-BackgroundCapture.ps1` for both directions' CliffClimbSlow 13.8107/38.9002/59.9999 in Side/Front/Low: 18/18 actual framebuffer and PNG hashes match the prior complete captures. Both scripts and capture children exit 0. Old evidence is untouched; actual new published hashes, 640-pixel panes, 4 fps pacing, BelowNormal priority and unchanged input proofs are recorded in `comparison.json` and `ui-binary-comparison.json`. These duplicate witnesses add no unique-review count, solve or interactive GUI pass. No numerical test scope is rerun merely to add overlapping counts after this UI-only change.

Post-capture audit found the tiny reflected/nonuniform-scale guard defect. After `./scripts/Build.ps1 -Publish` (log `artifacts/body89-publish13.log`), `artifacts/body89-checks13/Run-Checks.ps1` runs the same focused scopes sequentially into new13 output directories: 96/16/47/45/129 pass. Full Core exits 1 at its known contact assertion after 223 preceding passes. A new runner containing current Tests but archived Core10 fails the new tiny-reflection assertion after 51 passes; no archived binary or previous receipt is overwritten. This is a real failed-before counterexample, not a self-derived success metric.

Current `native-body-reach-89.1` then runs the existing public command:

```powershell
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/body89-native-revalidate13 --pose-native-revalidate artifacts/background-body89/native-inputs.json
```

All 113 evidence assertions pass: all 36 current bakes have exact keys/tangents/timing against the preserved freshly decoded outputs (271,926 supported comparisons), zero bone/full-skin error at quarter/fractional/transition probes, unchanged source/rig/native/manifest hashes and blocked Apply. This runs no second export/worker. All 36 reconstruction receipts equal their previous values, including 27 failed representation ratings. Normal Core13 hash is `323407532AFC830FA6DC55515AA4C90853EAA377076282B8E774ACCBAA22717D`; old capture10/12 records keep their actual distinct hashes. These checks preserve failed body/scene findings and do not complete M2.

After that current-source revalidation, `artifacts/body89-final14/Run-UiSemantic.ps1 -Run` and `Run-Witnesses.ps1 -Run` execute sequentially against actual published13 components. All 13 no-window UI checks and 18 framebuffer/PNG comparisons pass; child captures remain serialized at 4 fps/640-pixel panes and BelowNormal priority. Original10 captures, UI11 and final12 receipts, source/native inputs and published binaries rehash unchanged. Actual new binary identities and integrity/equivalence proofs are recorded under final14; old seals are not rewritten. These are repeated current-build verification scopes, never totals to add to earlier overlapping tests/reviews.

## Historical21 October88 — pose replacement checks

Run the current normal pipeline through the public workflow (private inputs and generated outputs remain ignored):

```powershell
./scripts/Build.ps1 -Native -Publish
$env:DOTNET_ROOT = Join-Path $PWD '.tools/dotnet'
$env:DOTNET_PROCESSOR_COUNT = '2'
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/rewrite88-final5-poses --pose-transfer-rewrite
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/rewrite88-final4-workflow --pose-rewrite-workflow
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/rewrite88-contracts --retarget-contracts
$env:FIGHTERPORT_BRAWL_WORKER = (Resolve-Path src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe)
$env:FIGHTERPORT_RETARGET_CASES = 'Wait1,Attack11,DownWaitU,DownWaitD,DownAttackU,DownAttackD,DownStandU,DownStandD,DownBoundU,DownBoundD,SlipWait,SlipAttack,SlipStand'
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/retarget88-native-floor --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --floor
$env:FIGHTERPORT_RETARGET_CASES = 'CliffWait,CliffClimbSlow,CliffClimbQuick,CliffAttackSlow,CliffAttackQuick'
& ./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/retarget88-native-ledge --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --ledge
```

Fresh final pose scope47, workflow43 and historical-helper contracts129 pass independently; earlier31/38/39/43 runs overlap these and must not be added. The latter tests retained helpers; it is not evidence that suspended contact solving works. New resource checks rate combined source/target bone work, reference/output skin work, native-probe expansion before fitting and pre-cancellation; in-loop cancellation latency was reviewed, not timed. Workflow checks also retain precise source-association/joint/collision review, empty contact-frame provenance and source-context hash ownership. Contact intervals are not fabricated for the new pipeline; native matrix rows report suspended contact reconstruction and uncalibrated action quality, separately from exact native values/tangents, bones, skin and timing. All36 new native outputs pass fresh worker validation, but none is a validated retarget. The old phase-classification assertion is unavailable while that subsystem is suspended, not newly passed. Explicit floor/ledge measurement still evaluates actual skin. Run screenshot jobs only through `Test-BackgroundCapture.ps1`, serialized and paced with pause/cancel/resume; do not start foreground/input harnesses. Capture completion and actual image inspection require separate records. Full legacy trajectory/surface acceptance is not implied by these focused scopes.

To rate the latest reconstruction against the same freshly decoded36 files, the new `--pose-native-revalidate <manifest>` scope checks native SHA256/proofs and prior independent worker receipts before comparing current curves and full skin. Actual command: `artifacts/rewrite88-native-rerate2-runner/FighterPort.Tests.exe artifacts/rewrite88-native-rerate2 --pose-native-revalidate artifacts/background-rewrite88/native-inputs.json`;113 evidence checks pass,29/36 representation ratings pass. This does not run a second worker/export or certify contact/action quality. The default full command was run as `tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/rewrite88-full-core`: exit1 after111 preceding passes at the retargeted hand/ledge/foot contact assertion. Its later native sections were not reached. CI/full Core remains red; do not conceal the unpassed requirement by counting focused checks as a full pass.

Background88 capture and review are complete in `artifacts/background-rewrite88/index.html` and its global/per-clip `visual-review.json`:36 clips,3610 actual paired angle/time images,1907 integer Side poses,213 Side fractions,175 inspected sheets and21 duplicate full-image inspections. Exactly eight cases (Wait1/Attack11/DownWaitU/CliffClimbSlow both ways) have full integer/fraction Side/Front/Low coverage; the remaining28 have complete Side and selected Front/Low probes. Two summary-format DownWaitU review records retain an explicit original per-image-provenance limitation in the global manifest. Private campaign/assembly scripts are under `artifacts/interactive-retarget-preview`; they use `scripts/Test-BackgroundCapture.ps1` with the actual published application, authentic source and freshly imported destination-native snapshots. All work was serialized, paced and BelowNormal; no focus/input/screen-capture harness was run. Do not count these images as contact passes, continuous-time proof or interactive desktop acceptance.

Final build/native/local publish receipt is `artifacts/rewrite88-publish-final2.log` (success, existing AVA3001 warning). The earlier restricted Windows SDK registry failure remains in `rewrite88-publish-final.log`; the authorized native rebuild succeeds separately. After final guards/resources were built, `Test-BackgroundCapture.ps1` captured slow-climb13.8107/38.9002/59.9999 in Side/Front/Low into `artifacts/rewrite88-final-published-witnesses`, using the same authentic source and reopened native target. Its `comparison.json` verifies all nine pixel hashes match the reviewed campaign; neither visible nor foreground windows were observed. These repeat images are excluded from the3610 review total.

## Current82 verification and saved-native comparison

Actual commands for this continuation (private inputs/prepared scenes stay ignored):

```powershell
./scripts/Build.ps1 -Native -Publish
& artifacts/retarget82-check-runner/FighterPort.Tests.exe artifacts/retarget82-contracts-r2 --retarget-contracts
& artifacts/retarget82-runner/FighterPort.Tests.exe artifacts/retarget82-workflow --trajectory-workflow
& artifacts/retarget82-runner/FighterPort.Tests.exe artifacts/retarget82-surface --surface-contacts
$env:FIGHTERPORT_BRAWL_WORKER = (Resolve-Path src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe)
$env:FIGHTERPORT_RETARGET_CASES = 'DownWaitU,DownStandU,DownAttackU,Wait1'
& artifacts/retarget82-runner/FighterPort.Tests.exe artifacts/retarget82-native-floor --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --floor
$env:FIGHTERPORT_RETARGET_CASES = 'CliffClimbSlow,CliffAttackSlow'
& artifacts/retarget82-runner/FighterPort.Tests.exe artifacts/retarget82-native-ledge --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --ledge
```

Contracts124 and workflow54 pass as separate scopes. Surface/native/display outcomes are recorded in RETARGET_VALIDATION; successful native processes certify native precision only. The first build attempt failed without compiler diagnostics; bounded `-m:1` build calls are now used by Build.ps1. The net472 worker's Windows SDK registry lookup required the authorized build outside the restricted sandbox; final local modern/native/publish succeeds. Do not mistake local publish for remote release authorization.

To inspect a fresh output without launching another solve, first import the new native motion with the real destination model through `FighterPort.Cli.exe import-brawl`, then use:

```powershell
& artifacts/windows-x64/FighterPort.Desktop.exe --compare-projects <fresh-single-motion.fighterport> <authentic-source.fighterport> <source-clip-id-or-native-name> --ledge
```

Omit `--ledge` for floor-only context. This specialist path currently accepts ProjectStore scene snapshots; it is not a complete schema2 character-browser workflow. Both panes remain read-only, with independent cameras/clocks and optional linking; fitting/Apply/Save are absent. The capture harness checks displayed times and exact source/target identities on every sample, rejects covered captures, checks no hidden batch and verifies native/project hashes. Run desktop harnesses sequentially. Capture generation remains separate from actual image inspection. Complete77 review is historical comparison; fresh82 files need their own inspection receipts. The corrected sliding test distinguishes sample count24, last key23 and end-exclusive support24; `--surface-contacts` provides a focused whole-body regression entry point and preserves its interval diagnostic JSON.

## Current 70 body/reach and source intake checks

Use the existing local SDK/native worker; originals/prepared private projects and all outputs stay ignored. Do not run GUI harnesses together. These are actual commands/scopes from this continuation (relative paths resolve in the repository):

```powershell
$env:DOTNET_ROOT=Join-Path $PWD '.tools/dotnet'
$env:DOTNET_CLI_HOME=Join-Path $PWD '.local/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PWD '.local/packages'
$env:MSBuildEnableWorkloadResolver='false'
$env:FIGHTERPORT_BRAWL_WORKER=Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
& '.tools/dotnet/dotnet.exe' build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore -m:1 -o artifacts/retarget70-runner
& 'artifacts/retarget70-runner/FighterPort.Tests.exe' artifacts/retarget70-contracts --retarget-contracts
& '.tools/dotnet/dotnet.exe' publish src/FighterPort.Desktop/FighterPort.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/windows-x64
& 'artifacts/retarget70-runner/FighterPort.Tests.exe' artifacts/retarget70-core
$env:FIGHTERPORT_RETARGET_CASES='DownWaitU,DownStandU,DownAttackU,Wait1'
& 'artifacts/retarget70-runner/FighterPort.Tests.exe' artifacts/retarget70-native-floor --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --floor
$env:FIGHTERPORT_RETARGET_CASES='CliffClimbSlow,CliffAttackSlow'
& 'artifacts/retarget70-runner/FighterPort.Tests.exe' artifacts/retarget70-native-ledge --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --ledge
Remove-Item Env:FIGHTERPORT_RETARGET_CASES
& 'artifacts/retarget70-runner/FighterPort.Tests.exe' artifacts/retarget70-plan-snake-climb --retarget-stages artifacts/native-v2-snake.fighterport artifacts/native-v2-mario.fighterport CliffClimbSlow --ledge --plan-only
& 'artifacts/retarget70-runner/FighterPort.Tests.exe' artifacts/retarget70-stages-snake-climb --retarget-stages artifacts/native-v2-snake.fighterport artifacts/native-v2-mario.fighterport CliffClimbSlow --ledge
./scripts/Build.ps1 -Native -Publish
& 'tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe' artifacts/retarget70-final-contracts --retarget-contracts
& 'tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe' artifacts/retarget70-final-coefficients --native-coefficients
& 'tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe' artifacts/retarget70-final-trajectory --prepared-trajectory
```

Final normal70 Release results:117 contracts,23 coefficients and 20 prepared-trajectory checks, each with its own successful results.json. These counts overlap broader scopes and are not a combined suite total. Full70 Core remains failed after 164 preceding assertions at batch readiness (48.7456627-degree action deviation above 45, passing contact separation 0.000046805555, exhausted work). All 12 focused Mario/Snake native outputs reopen exactly but fail contacts. The full build/native/local publish succeeds with zero warnings/errors in `retarget70-final-build-r2.log`; its first restricted-environment attempt ends with no compiler diagnostics and is retained, not counted as passing. Rejected71/72 metric trials and their earlier fractional-contact regression are in RETARGET_VALIDATION; they are not production paths or new passes for70.

The isolated70 test build has zero warnings/errors;117 contracts pass. Prior69 coefficient23 and trajectory20 checks are separate unchanged-component evidence. Native70 floor8 and ledge4 outputs reopen exactly; all 12 fail contacts. Full Core, stage, native and displayed results remain separate; RETARGET_VALIDATION contains exact failure/coverage records. A successful native-matrix process asserts writer fidelity, not all contact/action gates. Local publish is not authorization or evidence of remote publication.

New tracked `scripts/Test-RetargetSource.ps1` takes `-TargetProject`, `-Model`, `-Motion`, `-ExpectedSource`, optional `-SecondModel/-SecondMotion/-SecondExpectedSource`, `-Application` and a new `-OutputDirectory`. Actual private Mario/Snake invocation against the Release69 desktop yielded17 checks in `artifacts/retarget69-source-ui-r3/results.json`, with final target/session checks in `target-postconditions.json`. This verifies real source selection/replacement/cancel/invalid files, immutable inputs and no implicit library registration. Same-named source/target clips require source-popup scoping in UIA; the failed earlier harness attempts are retained. The opening UI is unchanged 70. Caller-supplied native paths belong in local arguments, not portable source.

The private70 preview harnesses run the published70 app sequentially and commit every requested integer/fractional frame field before checking source/target clock and native identity. Captures are guarded against a covered window and assembled into review sheets; a stopped attempt counts no completed acceptance. Every reviewed sheet/angle/clip is recorded in the private gallery's `visual-review.json`, not inferred from image counts. The historical slip/quick/down variants and broader matrix stay in scope even when this focused70 run covers only six clips per direction.

Completed70 visual scope:709 integer poses plus recorded fractions in Side/Front across 12 clips. Low is partial only for Mario→Snake slow climb, with 20 obscured captures excluded. Generated2253 images are not2253 inspected unobscured views; the filtered gallery has 2233. Earlier captures commit with Enter; the final reverse-direction retry uses focus loss and checks actual displayed source/target times. The original center-only occlusion guard missed partial coverage; the later guard samples15 pane positions. Every retained failure and exact reviewed range remains recorded. No Apply, target save, game install or game test follows capture.

## Continuation checks and failure preservation

Current pipeline65 commands (use new ignored directories; worker must be the actual compiled executable):

```powershell
./scripts/Build.ps1 -Native -Publish
$tests = './tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe'
& $tests artifacts/new-trajectory --prepared-trajectory
& $tests artifacts/new-contracts --retarget-contracts
& $tests artifacts/new-coefficients --native-coefficients
& $tests artifacts/new-workflow --trajectory-workflow
& $tests artifacts/new-controls --semantic-controls $preparedMario $preparedSnake
$env:FIGHTERPORT_RETARGET_CASES = 'CliffClimbSlow'
& $tests artifacts/new-ledge --retarget-matrix $preparedMario $preparedSnake --ledge
$env:FIGHTERPORT_RETARGET_CASES = 'DownWaitU,Wait1'
& $tests artifacts/new-floor --retarget-matrix $preparedMario $preparedSnake --floor
Remove-Item Env:FIGHTERPORT_RETARGET_CASES
& $tests artifacts/new-stages --retarget-stages $preparedSnake $preparedMario CliffClimbSlow --ledge
```

Latest completed focused scopes: `retarget65-trajectory-r2/results.json` **20**, `retarget65-contracts/results.json` **102**, `retarget65-coefficients/results.json` **23**. Dense preview tests explicitly cross the stored-contact budget, construct/seek an EditorDocument, save/reopen and compare every serialized vertex/influence. Full solver skin stays complete; bounded calibration is not proof of complete collision freedom. Optional stage tracing copies arrays only for the offline diagnostic, never normal playback.

Action-pass before/after and final post-constraint action costs are measured separately. A final physical restoration may worsen soft action; the final 45-degree/action and strict contact gates still decide readiness. Released source-driven FK recovery counts as action restoration only when independent geometric cost decreases and independent withheld foot constraints remain satisfied. The current static five-bone workflow still fails the 59.0648193-degree action gate; preserve that test. The full pipeline65 invocation (`FighterPort.Tests.exe artifacts/retarget65-core`) passed 164 assertions and stopped at this real batch-readiness failure. Historical pipeline62 full Core proceeded through all 54 workflow checks then failed sliding-source coverage; neither is a fresh full-suite pass for current source. Later current tests remain unrun.

Visible review must commit frame fields (Enter or focus loss), parse numeric strings with Float and explicit current/invariant culture rather than allowing thousands separators, and compare both displayed clocks. The failed fitted-preview attempts exposed an invalid stored collision array and non-atomic load, not a successful synchronized display. Current fitted captures use bounded calibration and atomic document replacement. The actual sequential `open-suite65.ps1`/`capture-suite65.ps1`/`assemble-review65.ps1` runs in the private interactive-retarget-preview artifacts completed both directions of slow climb, DownWaitU and Wait1. All 409 integer frames and recorded fractional probes were visually inspected in Side/Front/Low sheets (1,299 captures); `mario-snake-review65/visual-review.json` records failures, not passes. The final viewer processes were closed by their owning harness. Following cameras do not establish absolute travel. The other requested slip/down/attack/get-up variants retain historical coverage and need current reruns.

Fresh native pipeline65 runs used `FIGHTERPORT_RETARGET_CASES=CliffClimbSlow`/`DownWaitU,Wait1` with the commands above and the actual compiled worker. `retarget65-native-{ledge,floor}/retarget-matrix.json` records six exact native outputs and six failed contact rows. The very small down articulation residual excludes the separately recorded large body adjustment; inspect both it and world deviation, not just `MaximumDegrees`. The six original/prepared input SHA256 values match their pre-run records (`retarget65-inputs-after.json`). `./scripts/Build.ps1 -Native -Publish` completed in `retarget65-publish-r2.log` with zero warnings/errors and a local self-contained output. An initial restricted-sandbox invocation stopped with zero diagnostic errors and is retained in `retarget65-publish.log`; the normal Windows retry used the same source.

Use the same solution and original private Mario/Snake prepared inputs. `--prepared-trajectory` verifies actual coefficient ownership, both proportion directions, independent withheld foot pins, action correction, cancellation and unchanged native anatomy. `--native-coefficients` retains the independent moving-reference curve tests and fixed-root contradiction. `--retarget-contracts` covers channel ledgers, name-free branch ambiguity, surface-area/body-volume action weights, full-vector/full-skin constraints, support reference rank and feasibility-priority algebra. `--trajectory-workflow` runs the actual existing virtual-chain/batch/persistence integration without rerunning unrelated editor checks; it preserves the same required assertions. A failed integration is not counted as a pass because focused tests pass.

The ordinary CHR0 torso-control checks are `--semantic-controls <prepared-Mario> <prepared-Snake>` with the actual worker environment. The 31-check receipt `artifacts/retarget53-controls-native-r2/results.json` includes existing-native torso keys, fresh worker decode/evaluated skin, exact supported values/tangents/timing/loop and unchanged sources/deform data. Earlier exact-write failure is retained in `retarget53-controls-native/failure.txt`. Actual GUI controls checks are separate 21-check receipts per model in `semantic-controls53`; UI_REFERENCE records their inspected coverage.

Pipeline56 focused checks pass 17 trajectory, 23 coefficients and 99 contract checks (overlapping scopes; do not sum). Its full Core run passes the formerly failing released-arm assertion, then fails the delayed-contact five-bone trajectory. The narrow restoration/checkpoint experiments and all native failures remain in fresh `artifacts/retarget5*-*` directories. See the newest RETARGET_VALIDATION record for the current compiled pipeline and exact measurements. One attempted test rebuild hit the still-running owned test executable (MSB3021/MSB3027); after both native/test processes exited, `retarget56-tests-build-r2.log` passes with zero warnings/errors. No source compiler failure was hidden by that retry.

## Latest focused commands and scope — pipeline 51/52

The same SDK/cache setup below is used. The pipeline-52 r5 focused Tests build returned zero warnings/errors. Actual new commands were `FighterPort.Tests.exe artifacts/retarget52r3-contracts --retarget-contracts` (85 checks) and `FighterPort.Tests.exe artifacts/retarget52r5-coefficients --native-coefficients` (23 checks). Failed r3/r4 coefficient receipts remain; do not add overlapping rerun counts. The coefficient test uses independent moving-frame expectations, both proportions, withheld samples, deterministic repetition, cancellation and fixed-root negative constraints. It does not invoke the native worker.

Pipeline-51 native runs used the actual compiled `FIGHTERPORT_BRAWL_WORKER`, prepared private Mario/Snake projects, and `FIGHTERPORT_RETARGET_CASES=CliffClimbSlow` with `--retarget-matrix ... --ledge`, followed by `DownWaitU` with `--floor`. All four files reopen exactly but fail contacts; exact measurements are in RETARGET_VALIDATION. The full Core run fails after 111 checks at the real authored-release witness. These failures remain acceptance failures.

Actual model-viewer captures for the same four directional cases are in `artifacts/preview51-{snake,mario}`. All 272 displayed integer frames plus the recorded fractional probes were visually inspected in three views; 870 captures are evidence, not passes. `artifacts/mario-snake-review51/visual-review.json` distinguishes the three distorted fitted drafts from the Snake → Mario down preview that stayed unfitted. The gallery includes findings and exact coverage. The requested complete 28-variant review47 remains historical coverage, not a new complete run.


These commands/results support the existing M2 queue in [plans/current.md](plans/current.md). They are historical pipeline-47–50 evidence; the current milestone controls later source versions and acceptance. Use a **new output directory** when rerunning a command, and preserve old failure receipts. Run native matrices against an isolated matching executable while rebuilding shared `bin` outputs, and run GUI harnesses sequentially.

Modern solution build actually recorded for pipeline 50 (`artifacts/retarget50-build.log`): **zero warnings/errors**, **53.10 seconds**. The existing local SDK/cache configuration is:

```powershell
$env:DOTNET_ROOT=Join-Path $PWD '.tools/dotnet'
$env:DOTNET_CLI_HOME=Join-Path $PWD '.local/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PWD '.local/packages'
$env:MSBuildEnableWorkloadResolver='false'
./.tools/dotnet/dotnet.exe build FighterPort.slnx -c Release --no-restore -m:1
```

Actual focused command forms/output locations:

```powershell
$tests='./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe'
& $tests artifacts/retarget50-contracts --retarget-contracts
& $tests artifacts/retarget50-release --native-release-stages
```

The first receipt contains **37 checks** at its historical checkpoint: channel ownership/provenance/serialization, prepared action invariants and independent full constraint measurements. Later structural branch/ancestor-position checks are in the same entry point but are not included in that old count. The second contains **seven diagnostic invariant checks**, not a passed contact solve. Its constant and moving-release Core curves violate the **0.0000948** contact bound at **6.75** despite valid prepared quarter poses; preserved integer keys and correct release at **7.0001** are recorded separately. See [RETARGET_VALIDATION.md](RETARGET_VALIDATION.md) for measurements and the rejected numerical trials. `--native-release-stages` does **not** invoke the Brawl worker or establish fresh native encoding/reopen.

The stage probe accepts exact source/target project arguments, clip selection and separate execution limits:

```powershell
# Authentic source/baseline/plan only: no solve or curve output.
& $tests <new-directory> --retarget-stages <prepared-source> <prepared-target> CliffClimbSlow --ledge --plan-only
# Prepare and solve only: no curve fitting, native encoding/reopen or GUI.
& $tests <new-directory> --retarget-stages <prepared-source> <prepared-target> CliffClimbSlow --ledge --temporal-only
# Prepare, solve and compare initial/final Core native-compatible curves.
& $tests <new-directory> --retarget-stages <prepared-source> <prepared-target> CliffClimbSlow --ledge
```

The authentic Snake→Mario baseline command used `artifacts/retarget48-baseline-runner/FighterPort.Tests.exe` with source `artifacts/native-v2-snake.fighterport`, target `artifacts/native-v2-mario.fighterport`, `CliffClimbSlow --ledge`, and output `artifacts/retarget48-baseline-snake-mario-climb`. That isolated runner contains pipeline 47. `artifacts/retarget48-snake-mario-climb` records the pipeline-48 `--temporal-only` comparison. `geometry.json`, `anchors.json` and `stages.json` distinguish source/baseline/plan/solve/curve results; none is displayed-frame review or fresh worker output. The baseline already has large solved angular distortion, so curve tuning alone cannot establish action quality.

Actual pipeline-50 native subset commands used the isolated runner and the real existing worker:

```powershell
$env:FIGHTERPORT_BRAWL_WORKER=Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
$nativeTests='./artifacts/retarget50-runner/FighterPort.Tests.exe'
$env:FIGHTERPORT_RETARGET_CASES='CliffClimbSlow'
& $nativeTests artifacts/retarget50-native-ledge --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --ledge
$env:FIGHTERPORT_RETARGET_CASES='DownWaitU'
& $nativeTests artifacts/retarget50-native-floor --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --floor
Remove-Item Env:FIGHTERPORT_RETARGET_CASES
```

The fresh pipeline-47 DownWaitU baseline used the same matrix arguments, floor flag and case filter with `artifacts/retarget48-baseline-runner/FighterPort.Tests.exe`, writing `artifacts/retarget47-fresh-native-floor`. The 48/49 subsets remain in `retarget48-native-{ledge,floor}` and `retarget49-native-{ledge,floor}`. Each ledge subset has one exact native draft/one unresolved bake; each floor subset has two exact native drafts. No emitted output failed the supported native fidelity check, but contact/action/context failures persist and there is no complete Apply-ready row. Pipeline 48/49 aggressive prior/preconditioner choices were rejected for mixed regressions; pipeline 50 is a conservative isolated comparison, not a completed retarget fix. These are **two selected motions in both directions**, not all 32 matrix cases, requested slip variants, a new every-frame visual review or an in-game test.

The full Core runs are explicitly **failing**:

```powershell
# Actual pipeline-49 test-project build and full Core invocation after fixture fixes.
./.tools/dotnet/dotnet.exe build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/retarget49-core-fixed-reviewed
```

`retarget49-core-fixtures-build.log` records the successful test-project rebuild. The initial sandboxed build (`retarget49-core-fixed-build.log`) failed before compiler diagnostics; its retry and later build succeeded and remain separate logs. Test-only setup corrections explicitly create an animation for authored contact persistence and provide the existing independently authored rig axes. They do not change model-only semantics or loosen any assertion. After **111 passing checks**, `retarget49-core-fixed-reviewed.log` fails authored-contact separation **0.115689136 / 0.0000948000015** at **6.75**. The corrected-fixture pipeline-47 comparison (`retarget47-core-reviewed-fixtures.log`) also fails, at **0.00135696027**. A pre-existing failure does not excuse 49's regression, and neither run is a full-suite pass.

Automatic detailed-branch matching now has separate name-free, both-direction, permutation, ambiguity and geometry/skin negative assertions in `ChannelTransferChecks`; supplied-binding numerical transfer remains separate. Candidates require review beneath already reviewed parents and do not repair historical whole-rig **0/4 correct chains each way**. The CLI `retarget-map <source-project> <target-project>` and existing model-viewer `BoneMappingText` report expose scoped identities/binding evidence; the prepared channel ledger additionally exposes stale state, indirect ancestor effects, and explicit **NotMeasured/NotVerified** fields. Those reports are inspection tools, not native channel-error measurements. Prior map receipts `retarget50-mario-snake-map.json` and `retarget50-snake-mario-map.json` each contain 48 pairs and provide a baseline for confirming later mapping changes leave the reviewed Mario/Snake correspondence intact.

# Earlier 20 October library, game-source and native-curve continuation

The current displayed-review set uses the real Mario/Snake source catalogue: Wait1; CliffClimbSlow/Quick; CliffAttackSlow/Quick; DownWaitU/D; DownStandU/D; DownAttackU/D; SlipWait; SlipAttack; SlipStand, both directions. Capture every integer frame plus explicit fractional probes in side/front/low views. Review actual poses against the source and record inspected coverage, not only image counts. Fit-status headings and disabled Apply remain visible; time-budget cancellation and unresolved/unfitted previews are not successful fits. Complete-pose tracking cameras are suitable for pose inspection but not absolute travel validation. Private review captures/receipts stay ignored.

Fresh `./scripts/Build.ps1 -Native -Publish` completed successfully in `artifacts/relationship47-publish-retry.log`, including the net472 worker, local self-contained x64 application and 31-package notice audit. The initial sandboxed attempt failed before reporting compiler diagnostics (`relationship47-publish.log`); it is retained and not counted as a pass. This is local packaging, not a binary release or game test.

Completed private displayed-review commands, run sequentially against that packaged application:

```powershell
./artifacts/interactive-retarget-preview/open-suite.ps1 -Source Snake
./artifacts/interactive-retarget-preview/capture-suite.ps1 -Source Snake -SolveBudgetSeconds 180
./artifacts/interactive-retarget-preview/assemble-review.ps1 -Source Snake
# Gracefully close the owned review instance before opening the reverse direction.
./artifacts/interactive-retarget-preview/open-suite.ps1 -Source Mario
./artifacts/interactive-retarget-preview/capture-suite.ps1 -Source Mario -SolveBudgetSeconds 180
./artifacts/interactive-retarget-preview/assemble-review.ps1 -Source Mario
./artifacts/interactive-retarget-preview/build-gallery.ps1
```

These are local private-fixture review helpers, not portable CI inputs. `artifacts/mario-snake-review47/coverage.json` verifies 28 completed previews, 1,519 integer frames, 4,827 captures, all files present, all integer times in three angles, matching displayed clocks, no preparation timeouts and no Apply. Every integer frame was manually inspected from Side; `visual-review.json` records additional Front/Low/fractional inspection precisely. Status totals are 13 distorted, five unfitted, ten candidates still in Review; no complete Apply-ready result. `review.md` includes per-clip rounded UI measurements and findings. `index.html` is an offline interactive gallery of actual captures, not a solver or native playback benchmark. Preparation and capture elapsed time are combined and explicitly labelled; no isolated solver timing was measured.

Actual Mario→Snake SlipStand Play/Pause moved both visible meshes from frame 0 to 18.547 with linked clocks (`playback.json`, before/after PNGs); the session was restored paused at 0. Original/prepared input SHA256 checks pass for six files, and both disposable target copies remain byte-identical to their prepared input. No Save/Apply or game test occurred. The complete every-angle capture set does not mean every angle was manually inspected at every frame; following cameras also prevent an absolute-travel/temporal-camera acceptance claim. Fresh worker native reopen evidence remains the separate pipeline-47 subsets documented in RETARGET_VALIDATION.

## Latest source/contact regression checks

The following historical checkpoint is pipeline 47. Actual modern build `artifacts/relationship47-build.log`: zero warnings/errors, 2:13.51. Its source/geometry commands use `artifacts/relationship47-source-witnesses` (8 checks) and `artifacts/relationship47-support` (28 checks). The same documented matrix/generalization/library entry points write fresh `relationship47-*` receipts. All intermediate numbered runs below are historical; counts overlap. Pipeline-45 underside optimization was rejected after worse native action/contact measurements in both climbs; the partial 38 ledge and 43/45 floor diagnostics were explicitly cancelled and are not complete passes. Source/native/game acceptance remain separate.

`--retarget-stages <source> <target> DownWaitU --plan-only` also records both authentic source and target-baseline geometry/contact plans without solving, baking or claiming native/contact success. The two current down-plan receipts are diagnostic evidence only; full down-motion native acceptance remains open.

Build actually run: `./.tools/dotnet/dotnet.exe build FighterPort.slnx -c Release --no-restore -m:1` with the existing local DOTNET_ROOT/DOTNET_CLI_HOME/NUGET_PACKAGES and workload resolver disabled. Pipeline-42 receipt `artifacts/relationship42-build.log`: zero warnings/errors. The initial pipeline-38 test addition failed to compile because of tuple-field inference; corrected before the successful builds. Preserve failed logs rather than counting them.

```powershell
FighterPort.Tests.exe artifacts/relationship42-source-witnesses --source-support-witnesses <prepared-Snake>
FighterPort.Tests.exe artifacts/relationship42-support --support-geometry-only
$env:FIGHTERPORT_BRAWL_WORKER = '<actual compiled worker>'
$env:FIGHTERPORT_RETARGET_CASES = 'CliffClimbSlow'
FighterPort.Tests.exe artifacts/relationship42-native-ledge --retarget-matrix <prepared-Mario> <prepared-Snake> --ledge
FighterPort.Tests.exe <new-directory> --generalization <prepared-Mario> <prepared-Snake>
```

The source witness command labels the user's actual initial hang and fractional screenshot times, including released hands. Eight focused checks pass; source labels are not a target-contact oracle or full interval/mode calibration. Eighteen independent surface/math checks pass, including a stationary midair region, corner-to-top transition and fixed-anchor/region non-cancellation. Native/full displayed motion results are recorded separately in RETARGET_VALIDATION. An obsolete pipeline-38 matrix was explicitly cancelled before any completed row after source coverage failed. Intermediate 39/40 initial-hand and 41 fractional-hand failures are retained, not waived. The generalization command tests known-rig derived three-frame clips; it reports automatic name-free mapping failures separately and does not establish held-out generalization.

Modern Release build passed **zero warnings/errors**, 25.19 seconds: `artifacts/library-source-final-build.log`. The earlier retained-native build (`library-native-retained-build.log`) also passed zero warnings/errors in 23.63 seconds. Local self-contained publish is recorded in `library-source-final-publish.log`; the unchanged configured native worker, notices/licenses and modified component source stay beside the local desktop output. This is not remote binary publication. Two intermediate full builds failed because running test/CLI processes held their Windows output DLLs; no compiler failure was inferred from those locks. Native work uses an isolated runner for subsequent builds.

Actual Mario/Snake multi-model checks passed **16 checks**: `artifacts/library-source-retained/results.json`, with `model-library-receipt.json`. They reproduce a historical `{}`-identity catalogue, register Snake after Mario, retain both native rigs and same-name clip ownership, reject cross-source clip lookup, deduplicate the matching original without relabeling it, reopen a zero-clip model after restart, switch/edit/save/reopen independent managed models, preserve original/deform hashes, keep separate Project+ Main/Practice profiles, resolve extracted and exact trailing-separator fighter roots, verify selected file hashes/receipts, and reject traversal/cancellation. Earlier `library-game-models` failed the actual identity regression; `library-game-models-fixed` then exposed a test seeking on a one-sample grip clip. The retained test explicitly selects authentic Wait1 before fractional seeking. Those failures remain recorded, not counted as passes.

```powershell
$env:DOTNET_CLI_HOME=Join-Path $PWD '.local/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PWD '.local/packages'
$env:MSBuildEnableWorkloadResolver='false'
./.tools/dotnet/dotnet.exe build FighterPort.slnx -c Release --no-restore -m:1
$env:DOTNET_ROOT=Join-Path $PWD '.tools/dotnet'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe <new-directory> --library-models artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport
```

`scripts/Test-GameSources.ps1` passed **12 checks** on the supplied 7,440,695,296-byte WBFS using the installed external WIT 2.31a r6005: `artifacts/wbfs-provider-final/{results.json,inventory.json,prepared.json}`. Inventory contains **986 native files / 47 character folders**. Only the four selected Mario/Snake model/separate-motion packages were prepared; receipt hashes/sizes match, both decode through the real `native.brawl.v2.integer-floats` provider, and sequential registration retains two distinct models. Original image size/modification stamp stayed unchanged; no full-image hash/integrity or game check is claimed. WIT reports invalid extra-partition offsets; exact diagnostics remain in the receipt even though DATA selection succeeded. ISO, split images, unrelated resources/providers and full builds are not proven by this WBFS run. The earlier `wbfs-provider-verified` harness incorrectly looked for Wait1 in a numeric native Entry path; the retained check uses the clip name plus genuine format/entry identity.

```powershell
./scripts/Test-GameSources.ps1 -Disc <supplied-WBFS> -Wit <installed-wit.exe> -OutputDirectory <new-directory>
```

Fresh packaged desktop harnesses ran **sequentially**, with **20 checks each direction** under `artifacts/library-native-final-ui-{snake-mario,mario-snake}/results.json`. They use authentic source/target models and the real Add Source dialog while a comparison is open, verify distinct catalogue entries, replace the comparison source, add a managed model, switch back to the unchanged original frame/pose, and retain existing stale-result/rebuild/Undo/independent-clock/GPU/read-only/SCN0 guards. `-NoScreenCapture` means no unobscured raster/mouse or visual contact acceptance is claimed. The earlier `library-game-ui-snake-mario` reached the feature checks but failed harness shutdown after model switching because Process cached the old main-window handle; Refresh before CloseMainWindow fixed that harness error. Receipts overlap earlier UI coverage and must not be summed into new capabilities.

```powershell
./scripts/Test-EmbeddedRetarget.ps1 -SourceProject artifacts/native-v2-snake.fighterport -TargetProject artifacts/native-v2-mario.fighterport -OutputDirectory <new-directory> -Application artifacts/windows-x64/FighterPort.Desktop.exe -NoScreenCapture -MultiModel
# Repeat sequentially with Mario and Snake swapped.
```

Native retained-source commands/results are recorded in RETARGET_VALIDATION. No palm demonstration, complete default-suite run, game mod installation or playtest was performed. Existing Mario/Snake originals, prepared projects, native worker and supplied WBFS were available. Source updates remain separate from game/binary publication. GPU recovery/DPI and further SHP0 authoring remain deferred; SCN0 remains inactive.

## Earlier 20 October embedded increment — historical

Fresh modern solution build: `artifacts/embedded-verified-build.log`, **zero warnings/errors**, 2:12.52. Command (using existing local SDK/package cache):

```powershell
$env:DOTNET_CLI_HOME=Join-Path $PWD '.local/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PWD '.local/packages'
$env:MSBuildEnableWorkloadResolver='false'
./.tools/dotnet/dotnet.exe build FighterPort.slnx -c Release --no-restore -m:1
$env:FIGHTERPORT_BRAWL_WORKER=Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
$env:FIGHTERPORT_RETARGET_CASES='Wait1,DownWaitU,DownWaitD,DownAttackU,DownAttackD,DownStandU,DownStandD'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/embedded-native-floor --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --floor
$env:FIGHTERPORT_RETARGET_CASES='CliffWait,CliffClimbSlow,CliffAttackSlow'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/embedded-native-ledge --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --ledge
```

Both matrix commands completed: floor **11 native-correct outputs/three unresolved**, ledge **five/one**. None of the 20 rows is complete Apply-ready. All emitted supported native floats/tangents/keys, skeleton poses, evaluated skin, rates, counts and loop flags pass fresh-worker checks; both directions preserve source file hashes, deform rigs and all twenty reviewed finger joints. Unresolved/contact/action/context rows remain failures/review in RETARGET_VALIDATION. This is not a full default-suite or game pass; all 32 baseline cases remain retained. Nine independent floor/edge geometry/serialization checks also passed (`artifacts/embedded-support-geometry/results.json`); no palm retarget was run.

Stage diagnostic (offline analysis, not acceptance):

```powershell
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe <new-directory> --retarget-stages <source-prepared> <target-prepared> DownWaitU
```

It records solved actual skin, the initial Hermite curves and final projection separately, plus integer-pose preservation and explicit work receipts. The one-evaluation initial-curve diagnostic is never a successful bake. `RETARGET_VALIDATION` records numerical outcomes and rejected finer-linearization experiments; their files are not shipped results.

New UI harness: `scripts/Test-EmbeddedRetarget.ps1`, using authentic prepared Mario/Snake sources registered through the explicit reviewed **FighterPort project** provider (not relabelled as a native reader). `artifacts/embedded-search-snake-mario/results.json` passed **12 checks**; `artifacts/embedded-search-mario-snake/results.json` passed **15**, including real numeric target edit/Undo, stale Apply blocking and explicit target rebuild. Counts overlap. Both verify one main native window, real independent readonly source/candidate GPU models, native clip selection/search, fractional linked/independent seeking, playback clock advancement, preserved main frame/pose/skeleton, cancellation and unchanged target files with inactive SCN0. GPU completion is not unobscured raster or contact acceptance.

Final packaged-source reruns both passed **15 checks each**: `artifacts/embedded-current-{snake-mario,mario-snake}-uia/results.json`. Both used the updated default `artifacts/windows-x64/FighterPort.Desktop.exe`, sequentially with `-NoScreenCapture`, including the final source search/identity/choice code and stale/rebuild behavior. `artifacts/embedded-current-publish.log` records successful self-contained publication. The final capture attempt (`embedded-current-snake-mario/failure.json`) still stopped on a covered viewer after five checks; no raster/mouse acceptance is claimed. Four final-source native repeats (DownWaitU and CliffAttackSlow both ways) passed exact fresh native checks under `artifacts/embedded-verified-{down,ledge}/retarget-matrix.json`, with default-visible worst-vertex witnesses and unchanged source/rig hashes. They reproduce the 20-row measurements; do not count repeats as additional modes. Build and native worker availability are verified; the unchanged worker was not rebuilt this turn.

```powershell
./scripts/Test-EmbeddedRetarget.ps1 -SourceProject artifacts/native-v2-snake.fighterport -TargetProject artifacts/native-v2-mario.fighterport -OutputDirectory <new-directory> -Application artifacts/windows-x64/FighterPort.Desktop.exe -NoScreenCapture
# Repeat sequentially with Mario/Snake swapped; never run desktop harnesses together.
```

Without `-NoScreenCapture`, the harness refuses covered-window captures. The attempted reverse capture (`artifacts/embedded-search-mario-snake-visible/failure.json`) stopped after five checks because the tested viewer was covered; it is not a display pass. Earlier partial attempts found hidden transform fields, list realization/virtualization, invariant PowerShell casts of localized decimal text, and stale source identity metadata. The harness now focuses the correct tool/field, parses the UI culture, waits for the actual prepared candidate, filters authentic source choices and closes owned dropdowns. `AnimationPreviewPane` updates native identity when selection changes. Earlier failed receipts are retained and not added to successful totals. Existing floating-window caption assumptions in Test-Library and old manual ApplyTransform/schema-1 save assumptions in older harnesses are not current acceptance; migrate them rather than claiming they passed.

Local current self-contained publish uses the existing packages/worker (worker source unchanged):

```powershell
./.tools/dotnet/dotnet.exe publish src/FighterPort.Desktop/FighterPort.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore -m:1 -o artifacts/windows-x64
```

Keep the configured worker, notices/licenses and modified-component source alongside the local output as in Build.ps1. This is local packaging, not remote binary publication. GPU recovery, per-monitor DPI, further SHP0, complete native builders and actual game tests remain deferred/unverified. Build/run logs and fresh final-source repeats remain under ignored artifacts; no private fixtures or generated assets belong in Git.

Earlier records below are historical; only the explicitly rerun checks above describe this increment.

# Verification

## Character lifecycle and automatic keys — fresh 19 October evidence

Modern solution build passed with zero warnings/errors in 113.32 seconds (`artifacts/character-workspace-boundary-build.log`):

```powershell
$env:DOTNET_CLI_HOME=Join-Path $PWD '.local/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $PWD '.local/packages'
$env:MSBuildEnableWorkloadResolver='false'
./.tools/dotnet/dotnet.exe build FighterPort.slnx -c Release --no-restore -m:1
$env:FIGHTERPORT_BRAWL_WORKER=Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/character-source-mario --workspace-only artifacts/native-v2-mario.fighterport
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/character-source-snake --workspace-only artifacts/native-v2-snake.fighterport
```

Both actual Mario and Snake runs passed **33 checks each**, with overlapping scenarios rather than 66 distinct capabilities. They check genuine empty/model-only creation/reopen, Project+ Main/Practice isolation plus an unresolved Rivals target, affected-channel frame-zero keys, repeated fractional updates, exact Undo/Redo/cancel/no-change, stale gesture rejection, pooled full authentic source contexts, unchanged deform data, failed-save recovery, older-save completion, managed destination protection, complete-folder relocation, schema-1 explicit upgrade/original protection, newer-schema rejection, read-only source refusal and genuine NEW CHR0 export/fresh native pose verification. Receipts: `artifacts/character-source-{mario,snake}/{results.json,workspace-receipt.json}`. Original native worker/fixtures were available; no source game files were changed. The worker was not modified/rebuilt for this slice.

Latest local self-contained publication command (not remote binary publication):

```powershell
./.tools/dotnet/dotnet.exe publish src/FighterPort.Desktop/FighterPort.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore -m:1 -o artifacts/windows-x64
./scripts/Test-CharacterWorkspace.ps1 -OutputDirectory artifacts/character-source-save-diagnostic -NativeCharacter artifacts/character-source-mario/MovedCharacter/Character.fighterport -Application artifacts/windows-x64/FighterPort.Desktop.exe -NoScreenCapture
./scripts/Test-CharacterWorkspace.ps1 -OutputDirectory artifacts/character-source-save-snake -NativeCharacter artifacts/character-source-snake/MovedCharacter/Character.fighterport -Application artifacts/windows-x64/FighterPort.Desktop.exe -NoScreenCapture
```

Both sequential final desktop runs passed **10 checks each** in `artifacts/character-source-save-diagnostic/results.json` (Mario) and `artifacts/character-source-save-snake/results.json`. The harness copies each actual character folder before editing, enumerates only its own PID windows, uses UI Automation without global pointer injection and waits for a changed committed revision rather than sleeping an assumed save duration. It tests real New/target creation/restart, native skeleton TreeView, numeric auto-key/Undo/Redo, coherent GUI Save/close/restart and actual x64 GPU frame completion without render error or active SCN0. `-NoScreenCapture` explicitly leaves visible raster, mouse gizmo gestures, camera parity and unobscured playback acceptance open. Earlier attempts stopped on covered capture, closing during an in-flight save, or reading the descriptor before async save committed; the close path/harness now await actual completion. Earlier partial receipts are not cumulative passes.

The first final-source packaged Mario run (`artifacts/character-source-desktop-mario/failure.json`) exceeded its 30-second save check, leaving three staged 42 MB scene records and the previous descriptor intact. Its application status was not captured, so that run is not a pass or a proven save-error diagnosis. The revised harness permits a bounded 120 seconds, observes Save's enabled/completion state, captures MainStatus on failure and still requires the new coherent revision plus fresh-process content equality. The subsequent Mario diagnostic and Snake run passed without an application save error; Snake's measured save-plus-close wall time was approximately 11.2 seconds. Whole-scene revision rewriting and variable IO/memory pressure remain a performance concern. Incremental immutable scene reuse is not implemented, and no latency acceptance is claimed.

CLI entry points: `character-new <name> <folder>`, `character-info <descriptor>`, `character-add-target <descriptor> <name> <game> <platform> <build> [mode]`, `character-add-scene <descriptor> <prepared-scene> [target-id]`, `character-copy-to-target <descriptor> <shared-scene-id> <target-id>`, `character-upgrade <schema-1-input> <new-copy>`. These use the same Core ownership/storage service. Legacy scene commands still take schema-1 scene documents; character-wide equivalents remain a gap rather than implicit format flattening.

The complete default suite/old desktop harnesses were not rerun or declared passing. Their fixture-start, manual Apply/keying and schema-1 save assumptions need migration while retaining meaningful native/contact assertions. Retarget numerical acceptance, native export, GPU completion and visible/game acceptance remain distinct. This slice does not complete `UI_AND_PROJECTS.md` or M2.

## Latest support-context commands and evidence

The complete default procedural suite/hosted CI was not rerun as part of the user-directed Mario/Snake investigation. Historical workflow assertions still assume the retired pose-preference implementation and older Apply readiness; they need migration to the new explicit capabilities and independently rated source coverage. Focused native fidelity and GUI receipts do not establish a current full-suite pass. Preserve meaningful contact/action expectations rather than disabling them to obtain a green run.

Run mouse/display harnesses sequentially on an unobscured desktop. `Test-Library.ps1` enumerates only the tested process's native handles before entering UI Automation, avoiding unrelated protected-process token failures. Caption dragging checks that the tested title bar is actually under the intended point before sending mouse input; a covered desktop is an environment failure, never a movement pass. The final packaged reruns did not complete; see `RETARGET_VALIDATION.md` rather than reusing earlier GUI counts as final-build acceptance.

See `RETARGET_VALIDATION.md` for the completed four down-rest and six slow-ledge native probes, unchanged-source checks and per-clip failures. These commands exited zero for native fidelity; all ten remain unresolved or contact review. Nine independent supplied floor/edge geometry and serialization checks passed under `--support-geometry-only`. Current user-directed retarget investigations use Mario/Snake only; no palm retarget was run.

CLI commands `retarget`, `library-use` and `library-use-selected` accept `--floor` or `--ledge`, with `--source-floor`, `--target-floor`, `--source-edge`, `--target-edge` and `--platform-backwards`. Numeric values are invariant decimals in each model's native units, defaults zero; edge inward follows the reviewed rig forward or its opposite. These are explicit geometry inputs, not verified game-stage context. There is no backend selector. No generated/native/private output belongs in Git.

## Latest support-context and window amendments — 19 October 2026

The user requires geometry-driven fitting, with no character-, native-bone-name- or animation-name-specific corrective rules. Reviewed mappings identify existing anatomy and native channels; the fitting code uses rest geometry, skin influences, motion and supplied support geometry. Native skeletons, binds, helpers and weights remain unchanged. Perfect transfer across arbitrary anatomy and contradictory contacts is not an established capability.

Comparison offers **Flat floor** and **Floor + 90° ledge**, alongside explicitly uncertain inferred context. Advanced fields place source/target floor heights and edge offsets independently and select the inward direction. The supplied ledge is an ideal platform half-space with a perpendicular vertical face: outside/below-edge hanging space is empty, while geometry inside the platform is tested against its top and face. This is editor support context, not native stage/article export or SCN0 playback. Finite platforms, other objects, actor travel and missing interaction evidence retain their capability gaps.

The retarget comparison is now an owned, nonmodal window. Both it and the main window can move independently; activating the main window keeps its comparison above it without global Topmost. A second retarget request activates the existing comparison. Closing the owner cancels work and closes the comparison. Main-document edits invalidate its preview and require reopening before another fit/Apply; stale results cannot overwrite edits.

Source/target body proportions drive separate file-unit, root-travel, pelvis-height, torso and ledge-span transfers. Collision correction selects existing native channels through actual skin influences. Deterministic geometry seeds and winding-aware Euler reconstruction are offline. Body-supported phases come from source geometry, not motion names. Incidental inferred observations use a versioned soft objective (initial weight 25); required/authored pins and grips, whole-mesh collision clearance and required torso support retain the unchanged hard bounds. Changed inferred support patterns require review; making a contact soft is not a complete-contact acceptance pass. Full automatic source-mode calibration and reliable body/native curve fitting remain unfinished.


## Latest user decision — one retargeter, Mario/Snake testing (19 October 2026)

The replacement is now the sole normal retargeter. The previous retarget engine and backend selector have been removed from Core, Desktop, CLI and batch execution. No old-engine fallback exists. The comparison uses the label **Retargeting**, without V2/experimental choices. Persisted `retarget.temporal-v2` and historical `retarget.legacy` identifiers describe saved provenance; they do not expose an alternate engine or upgrade historical validation. Independent existing-bone FK/IK tools and the data-only native worker remain.

This explicit user decision supersedes earlier instructions in this document to keep the previous engine as default or wait for V5 before changing defaults. It does not waive contact, action, native or display acceptance. Current down/ledge fitting remains unfinished; failed drafts cannot be applied as validated results. Uncalibrated artistic controls remain disabled rather than presented as working controls.

Use the actual supplied Mario and Snake models/motions in both directions for ongoing retarget testing and displayed comparisons. Stop running the palm demonstration. Previously recorded procedural receipts are historical evidence only; retain independent CI fixtures and frozen difficult cases. Keep existing skeletons unchanged and SCN0 inactive. The sole current executable substep is in `docs/plans/current.md`.


## Current Mario/Snake commands

Build the local desktop and data-only worker using `./scripts/Build.ps1 -Native -Publish`. Set `FIGHTERPORT_BRAWL_WORKER` to the actual compiled worker. Use trusted prepared Mario/Snake projects and retain private inputs and outputs in ignored local storage.

```powershell
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/new-mario-snake --retarget-matrix <prepared-Mario> <prepared-Snake>
./scripts/Test-Library.ps1 -ReviewOnly -OutputDirectory artifacts/new-mario-target -TargetProject <prepared-Mario> -NativeModel <Snake-model> -NativeMotion <Snake-motion> -SourceSearch 'Wait1 [' -InspectAngles
```

There is no `--backend` or `-Backend` selection. Run the converse GUI inspection sequentially with Snake as target and Mario as source. Review-only mode inspects actual source/target meshes without committing failed contact drafts. `FIGHTERPORT_RETARGET_CASES` is an explicit diagnostic subset; the unfiltered native matrix retains all sixteen motions in both directions. Native output correctness never substitutes for contact/action acceptance. Do not run the old palm demonstration for this investigation.

Core tests independently cover same-rig exact winding, rest behavior both directions, rotated coordinates, physical units, reflected frames, changed parent rests, extra native twist topology, five-node constraint solving, deterministic coupled fitting, source-coverage negatives, backend migration and actual selected Apply/Undo/persistence. Original native fixtures independently label palm/plane geometry and reopen through a fresh worker. Static and moving fixtures must be distinguished. Desktop harnesses run sequentially and explicitly frame both cameras before inspecting images. A GPU counter or a blank viewport is not visible acceptance. Do not rebuild an executable while an existing test process holds its apphost; keep output ownership separate or wait for completion. Current receipts and known failures: RETARGET_VALIDATION.md.

The body-fitting increment includes an original enlarged-head down-rest clip. Its complete bake must retain torso support, every checked skin vertex above the fixed floor, limb articulation and unchanged deform data at withheld fractional/closing samples. Separate checks allow deliberate supported body turns but reject unrelated arm distortion, distinguish upright/hovering/handstand negatives, persist source body phases and test exact half-open release boundaries. Native contact and action results remain independently measured on fresh output.

The default `--retarget-matrix` now tests 16 actual native motions in both directions: Wait1, DownWaitU/D, DownAttackU/D, DownStandD/U, DownBoundU/D, CliffWait, CliffClimbSlow/Quick, CliffAttackSlow/Quick, Attack11 and SlipWait. `FIGHTERPORT_RETARGET_CASES` is an explicit narrower investigative override. Keep full-clip results separate from shortened probes. GUI comparisons additionally capture worst body-support/adjustment samples and both sides of below-edge/above-plane phase transitions from Side/Front/Rear/Low. Missing ledge geometry remains review, not blanket-floor acceptance.

The pose-fidelity increment adds an original unequal-head/body lying fixture, independent checks for a rigid body lift without torso/limb rotation, and authored-pin retention. A separately constructed 90-degree torso error verifies the action gate even when contact validation passes. The native matrix records both the editable correction report and a fresh-native action measurement against the FK-only target, alongside exact native values/poses and contact measurements. A shortened diagnostic is only an investigation; it does not replace full authentic-clip or GUI acceptance. Fresh current receipts belong in RETARGET_VALIDATION.md.

Historical detailed-bone increment: build/native/local publication passed with zero warnings/errors; original native regression passed 337 checks in `artifacts/detailed-verified-native/results.json`. Its matrix tested nine motions per direction. The current default covers sixteen per direction as listed above. `Test-Library.ps1` exercises real registration, both source pickers, independent clocks/GPU views, compact sizing, invalidation/rebake, target edit/undo/save/restart and displayed pixels. Current per-run results, commands, fixture provenance and limits: `ANIMATION_LIBRARY.md` and `RETARGET_VALIDATION.md`. Historical counts overlap and are never added.

Build: `./scripts/Build.ps1`; native worker: `./scripts/Build.ps1 -Native`. Procedural checks: `./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/tests`.

Native checks require `FIGHTERPORT_BRAWL_WORKER` set to the compiled worker executable. Add `--native`; optionally repeat `--private <model-package> <separate-motion-package>` for private local fixtures. They generate original MDL0/CHR0 assets, compare independent trigonometric/Hermite expectations, compare the owned evaluator/skin geometry with BrawlLib at withheld fractional samples, edit/export to new files, reopen in fresh processes and verify source hashes. Old test counts are never reused.

Private tests explicitly select a clip with multiple changing channels on actual model bones and assert visible-scale mesh deformation. An archive's first CHR0 can belong to a weapon, so its presence alone is inadequate character-motion evidence. The selected native resource identities and measured geometry errors are recorded in the results.

The native worker is optional for CI. `FighterPort.slnx` contains six modern assemblies; the seventh executable exists only for the actual legacy native dependency boundary. CI runs original fixtures on Windows. A workflow being present is not a claim that hosted CI has run.

`./scripts/Test-Desktop.ps1` launches a bounded visible desktop acceptance run and always closes its own process. It operates the actual controls, samples displayed viewport pixels outside counters/timeline, measures settled idle CPU and records GPU/hardware information under ignored artifacts. With `FIGHTERPORT_GPU_PROBE=1`, explicit acceptance jobs use transform feedback/readback to compare actual GPU-skinned vertices with the independent CPU evaluator. These expensive probes do not run during normal playback. Numeric/source data tests alone do not establish display smoothness.

Add `-VerifyViewportParity` for exact displayed-pixel comparisons before/probed/after redraws at frames 0, 1.25 and 15.25. Validation uses its own program, blocks color/depth writes and waits for paused frames. Original/Mario/Snake pass on the recorded ANGLE backend. This supersedes the earlier probe-related raster limitation below, without establishing all GPU/material image parity. See `VIEWPORT_VALIDATION.md`.

For large-catalogue playback, generate an ignored original project with `FighterPort.Cli.exe fixture-catalogue artifacts/catalogue-10000.fighterport 10000`; run a copy with `-AnimationSearch 'Catalogue 00000' -VerifyCataloguePlayback -DisableGpuProbe`. This captures actual displayed playback through six queries, empty results and wheel scrolling; it checks active clip/frame isolation and filter persistence through key edit/Undo. `-DisableGpuProbe` proves normal rendering has zero validation readbacks. Final catalogue acceptance passed 22 checks. The entries reuse four prepared original clips; this is not an import/global-library benchmark. Run GUI harnesses sequentially because the app permits one desktop instance.

The desktop harness exercises the actual GUI Save command, fresh-process project reopening and pointer-driven translate/rotate/scale handles. A command-line project path gives this check a deterministic local destination. The native Save picker was used successfully during investigation, but the current Windows UI Automation proxies expose its fields as panes and the dialog automation was inconsistent; no complete file-picker automation claim is made. Inspect the recorded result before claiming a gate passed.

For a private native project use `-Project <generated-project> -OutputDirectory <ignored-output> -AnimationSearch 'Wait2 ['`. The harness searches/selects the real animation browser. Choose a motion long enough for its sampling window; short non-looping animations legitimately stop. Do not alter native timing or looping to satisfy a display threshold. The original-fixture run also checks functional existing-bone IK, saved keys and reversible mesh deformation. Headless controls tests cover renamed unequal-proportion rigs, different local axes, pole placement, near-straight limbs, unreachable goals, FK/IK composition and native IK baking/reopen.

Add `-VerifyNativeControls -VerifyNativeMaterials` to check actual saved foot/hand keys, poles/setup identity, unchanged deform checksums, GUI Undo/Redo and compiled native material programs without renderer diagnostics. Use Mario `Wait2 [` and Snake `Wait3 [` for the bounded display window. Snake's shorter non-looping `Wait2` correctly ended too early in a preliminary run; the threshold was retained. GUI saving waits for atomic file replacement before hashing once, avoiding repeated hashing of large projects while serialization writes them.

The combined numerical/native run passed **342 checks** (artifacts/standalone-contact-retarget-native/results.json). Latest Core-only checks passed **156**, adding rotated model/mesh coordinates, loop-closing keys and repeated-reuse ancestry. The native retarget rerun passed **11 checks**, including final-fractional/closing samples. Coverage includes new/duplicate/rename clips, quaternion/spherical/held channels, distinct rates, renamed/proportioned/rotated-bind rigs, support constraints, release and isolation. Mario-to-Snake and quaternion/30 fps to native 60 fps CHR0 reopen against native matrices. Model-only CHR0 creation, original BRRES insertion and both supplied PAC insertions verify resources/hashes. Bounded positive nonuniform-scale correction is separate from analytic rejection; arbitrary squash/stretch remains unsupported.

The initial combined run encountered BrawlLib's modal empty-data export for Snake redirect placeholders. After the fix, focused --container-only reruns passed six checks per private motion without that modal. Preservation receipts include byte lengths and actual empty-byte hashes. Never discard zero-length redirects. Native outputs remain new files and generated assets stay ignored.

Add -VerifyTransport -VerifyBodyContacts -VerifyAuthoring to desktop acceptance to exercise visible loop/restart, fractional seeking, planted pelvis edits, saved anchors/Undo/Redo and animation creation. The owned frame field commits on Enter/focus loss and accepts current-locale or invariant decimals. An initial transport failure was traced to read-only anonymous CLI scalars being omitted by persistence JSON settings; a typed inspection record now supplies actual clip length/checksum, and acceptance asserts nonempty metadata. Viewport pixel checks exclude counters/timeline; pure joint axes are excluded from gizmo detection. Bounded transport input traces run only with acceptance metrics enabled. Bone pixel parity, shader image references and private PAT0 remain missing evidence.

SHP0 checks and exact current measurements are in `SHP0_VALIDATION.md`. Core editing checks passed 181; focused original/private native editing passed 110; complete original native regression passed 250 (overlapping counts). `--morph-only` now generates and edits original native v3/v4 fixtures, saves new BRRES/PAC files and checks independent native reopen. Repeat `--morph-private-pac <model-package> <motion-pac>` to author new morph copies while retaining the supplied original motion catalogue. Existing key deletion, distinct loop-closing keys, unchanged flags/timing, simultaneous bone edits, native resource hashes and staged mismatch rejection are required. Supplied motions have no game-authored SHP0; that input remains missing despite passing authored-copy coverage.

Add `-VerifyMorphs` to desktop acceptance for simultaneous CHR0/SHP0/SRT0/VIS0 playback, fixed-frame morph disable/restore, unchanged bone pose, exact restored pixels and GPU normal/color stream checks. Add `-VerifyNativeMaterials` for native programs without diagnostics and a lit-surface screenshot check against black silhouettes. `-VerifyTextureFallback` instead expects missing-image diagnostics plus lit fallback surfaces on an explicitly textureless test copy. The fallback check is not native shader parity. Native source textures/palette pixels are independently checked in the authored-copy fixtures. All runs close their own processes.

Add `-VerifyMorphGeometry` with `-VerifyMorphEditing -VerifyMorphs` to create a target through real controls, edit an exposed native point, check actual changed/restored pixels, compare prepared geometry and bone-matrix hashes, and save/reopen a new model/motion pair. Native point zero can be occluded; the harness tries spaced indices and undoes invisible attempts. GPU-probed comparisons use the same explicit probe path before/after; preliminary mixed probed/unprobed captures differed despite identical CPU buffers/poses. This is a bounded acceptance condition, not full raster/backend parity. Regenerated dense RGBA8 fixtures supersede earlier authored color fixtures with compacted duplicate payloads. Final original/private native geometry acceptance passed 144 checks in `artifacts/morph-geometry-verified/results.json`.

Add `-VerifyMorphEditing` with `-VerifyMorphs` for actual numeric weight/tangent editing, key deletion, pixel-verified Undo, project persistence, the real native Save dialog and automatic output reopening. Private PAC fixtures use `-AnimationSearch 'OriginalMorph ['`. Native reopening Undo/Redo and source provenance are checked using compact CLI `morph-info` results from actual project saves. The harness restores its own window before screen scrubbing/playback after file dialogs. Latest editing GUI runs passed 34/40/40 original/Mario/Snake checks; all retain the original visible-change and GPU thresholds.

Screen sampling reports observed changed-pixel cadence, not exact compositor-present times or a universal 60 FPS guarantee. Minimize/restore, resize, two-character comparison and orthographic captures are included. Per-monitor DPI change and recoverable GPU/context failure still require independent acceptance. No game launch, installation, mod publishing or game physics accuracy is inferred from viewport tests.

## Automatic fitting/batch acceptance commands

`AUTOMATIC_RETARGETING.md` governs the current M2 slices. Use fresh ignored artifact directories. `RetargetWorkflowChecks` covers neutral equivalence, actual preference changes, hard pins, preview/export timing separation, combined trim/retime and final fractional segment, held/protected conflicts, shared and overridden persistence, name identities, mixed batches, one Undo/Redo, cancellation, source re-registration and edit-followed-by-Undo races. It also checks pooled source context integrity and playback fingerprint preparation. `ContactReferenceChecks` uses original moving platforms, two simultaneous skin grips plus planted feet, unequal-arm bakes and barycentric blended-skin self-contact. These procedural interaction APIs are not game/native-article acceptance.

```powershell
./scripts/Build.ps1 -Native -Publish
$env:FIGHTERPORT_BRAWL_WORKER = Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
$tests = './tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe'
& $tests artifacts/new-rt-checks --native
& $tests artifacts/new-private-checks --native --private $privateMarioModel $privateMarioMotion --private $privateSnakeModel $privateSnakeMotion
$env:FIGHTERPORT_RETARGET_CASES = 'Attack11,Wait1,SlipWait,CliffClimbQuick,CliffClimbSlow,CliffAttackQuick,CliffAttackSlow,DownBoundU,DownWaitU,DownStandU,DownBoundD,DownWaitD,DownStandD'
& $tests artifacts/new-26-case-matrix --retarget-matrix $preparedMario $preparedSnake
Remove-Item Env:FIGHTERPORT_RETARGET_CASES
# Without the filter, 16 motions run in both directions (32 cases), including down attacks and slow ledge motions.
./scripts/Test-Library.ps1 -OutputDirectory artifacts/new-fitting-ui -InspectAngles -VerifyFittingBatch
# Actual native-model comparisons; explicitly review the reported skeletal losses:
./scripts/Test-Library.ps1 -OutputDirectory artifacts/new-mario-source-ui -TargetProject $preparedSnake -NativeModel $privateMarioModel -NativeMotion $privateMarioMotion -SourceSearch 'Wait1 [' -TargetSearch 'Wait1 [' -InspectAngles -ReviewChannelLosses
```

Run desktop harnesses sequentially. `-VerifyFittingBatch` drives actual sliders, verifies changed target pixels at a common fractional pose, isolates preview speed, checks trim/export-speed source linking, applies the explicit passed subset, then saves/restarts and checks one Undo/Redo and GPU evidence. `-ReviewChannelLosses` explicitly operates the real loss-policy checkbox; it never relaxes contact/context bounds. The native matrix records both baked and fresh-decoded strict contact validation at quarter samples, withheld fractions and phase boundaries, as well as independent native bone/skin checks. No-contact measurements are not clean support passes.

`NativeContainerChecks` now verifies two inserted motions, original catalogue identities, exact independent edited poses, preserved resources and source hashes. It exercises cancellation between stages and an actual file lock preventing the committed receipt write; recovery reconciles the committed hash without deleting it. `NativeTimingChecks` independently verifies a 24-sample non-loop CHR0 with a boundary key at 24, including fresh native samples 23.125/23.5/23.875. This boundary is not an extra displayed sample.

Historical counts above overlap current suites. Current commands, actual counts, failed investigations, per-clip limitations and artifact receipts are recorded in `RETARGET_VALIDATION.md` and `DEVELOPMENT_STATUS.md`; do not add overlapping totals. No emulator/game playtest, WBFS extraction or Modkit cook/publish is implied.

`LibraryChecks` additionally verifies streamed commit proofs, changed stable clip manifests with unchanged identity/package labels, corrupt prepared bytes, corrupt raw bytes and cancellation. `Test-Library.ps1` waits for actual Apply closure and save completion before checking the reopened file. `Test-Desktop.ps1 -VerifyTransport` seeks inside the final fractional segment and checks the duration-time stop, visible restart and loop behavior. Review/context rows in optional matrix comparisons must stay blocked even if their native data passed.

Static-clearance checks include a stationary hand in midair, a brief released hop between ground observations, and a lying rig with no qualifying stationary interval. The last case checks every actual vertex at independent fractional samples, requires zero invented contacts and explicitly rejects missing-contact acceptance. These extend the existing seated/lying/handstand/kneeling/ledge fixtures with renamed bones, unequal proportions and rotated bind axes.

For displayed difficult cases add `-CompareClips DownWaitU,DownWaitD,CliffAttackSlow,CliffClimbQuick -ExpectedMatrix <fresh-matrix-json> -FromCharacter Mario -ToCharacter Snake` to the native library GUI command; reverse the model/project and direction labels for the other direction. The harness verifies candidate-versus-unfitted-selected-motion identity separately from Apply eligibility and captures the independently measured worst contact/floor samples as well as start/middle/end. Use a fresh receipt directory, and do not substitute an older matrix after changing the solver. Optional CLI `contact-info <prepared-project> <clip-index> [comma-separated-frames]` reports authentic phases/patches and actual prepared-morph whole-mesh minima at those fractional samples. It is diagnostic evidence, not a contact-label oracle.

Optional comparison runs can also include DownStandD and DownBoundU in both directions; only completed receipt sets establish displayed acceptance. It waits for the selected source ID's terminal row state (bounded at 900 seconds), rather than interpreting an enabled Analyze button during debounce as completion. Existing per-pane Frame actions have direct automation IDs and frame the actual sampled pose before capture, including below-edge hangs. Failed synchronizations remain failed investigations, not displayed acceptance. `-VerifyTransport` additionally uses real mouse input to verify right-click does not scrub, left drag works, and capture loss/release outside followed by hover cannot change the paused target. Timeline requires a live primary-button capture and resets its drag state on loss.


`-VerifyUnfittedPreview` on the private Snake-source/Mario-target library harness checks an early, linked DownAttackU FK preview, disabled Apply, changing target-only mesh pixels at distinct fractional samples, source switching during fitting, cancellation and unchanged main target clips/rig. It does not certify the unfitted motion's contacts. Large native animation ComboBoxes are virtualized; the source-switch check uses actual keyboard input rather than requiring a distant item to exist in the realized accessibility tree. See the current validation receipt for completed checks and failed investigations.


Native projection 3 has a deterministic rig-size work budget, stores its actual work receipt and blocks Apply on exhaustion. Final static/moving native fixtures pass within budget; all emitted private ten-case candidates remain reviewed drafts after exhaustion. Current Core: 432 checks. Actual framed comparison Apply/Undo/save/restart: 23 checks (`artifacts/retarget-v2-bounded-gui-poll/results.json`), with bounded nonblocking process-exit polling and captured startup streams. Full per-clip values, failed investigations and current/historical native totals: RETARGET_VALIDATION.md. No default-backend promotion or GPU recovery/DPI claim.

Current support-context commands:

```powershell
FighterPort.Tests.exe <new-output> --support-geometry-only
FighterPort.Tests.exe <new-output> --retarget-matrix <prepared-Mario> <prepared-Snake> --floor
FighterPort.Tests.exe <new-output> --retarget-matrix <prepared-Mario> <prepared-Snake> --ledge
FighterPort.Tests.exe <new-output> --native-exact <captured-expected-clip.json>
FighterPort.Tests.exe <new-output> --container-only <prepared-Mario>
```

`--floor`/`--ledge` in the matrix declare zero-height support in each reviewed rig's frame; ledge offset is zero and inward is rig forward. These are explicit test inputs, not inferred game-stage geometry. Select appropriate private test cases with `FIGHTERPORT_RETARGET_CASES`; retain the complete earlier baseline. Native failures save a compact expected-clip JSON and up to 32 exact key differences for diagnosis. A matrix with no emitted candidate exits unsuccessfully and keeps its unresolved receipts. Retarget CLI commands also accept `--floor`/`--ledge`, optional `--source-floor`, `--target-floor`, `--source-edge`, `--target-edge` model-unit values and `--platform-backwards`.

Run desktop harnesses sequentially. `-ReviewOnly` verifies real caption dragging, owner z-order, source/target pixels, support-scene selection and blocked Apply without committing failed fits. The earlier interrupted comparison run is not a completed GUI pass. Full original/procedural suites were not rerun in this user-directed Mario/Snake increment; historical totals are not fresh results.
# 21 October source83 focused verification

Fresh commands, in the repository root, with the existing local .NET SDK/environment and `FIGHTERPORT_BRAWL_WORKER` set to the actual compiled worker:

```powershell
./.tools/dotnet/dotnet.exe build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore -m:1 -o artifacts/retarget83-runner
./artifacts/retarget83-runner/FighterPort.Tests.exe artifacts/retarget83-contracts --retarget-contracts
./artifacts/retarget83-runner/FighterPort.Tests.exe artifacts/retarget83-workflow --trajectory-workflow
./scripts/Build.ps1 -Publish
./artifacts/retarget83-runner/FighterPort.Tests.exe artifacts/retarget83-stages-climb --retarget-stages artifacts/native-v2-snake.fighterport artifacts/native-v2-mario.fighterport CliffClimbSlow --ledge
```

The build succeeds,126 contracts and54 workflow checks pass, and local self-contained publish succeeds. The native worker is unchanged from the successful82 `-Native -Publish` build. `--trajectory-workflow` is the focused workflow flag; unknown flags do not select it. Native83 uses `--retarget-matrix` with `FIGHTERPORT_RETARGET_CASES=DownWaitU` / `--floor`, then `CliffClimbSlow` / `--ledge`, both directions in distinct artifact directories. Each native output is written new and reopened by a fresh worker. Private source paths are not portable project identity. See RETARGET_VALIDATION for native/contact/action/display outcomes rather than treating exit0 as complete retarget acceptance.

Source82's complete twelve-clip displayed review inspects709 integers plus42 fractions from Side/Front/Low. Source83 separately inspects every272 integer poses and18 fractions from all three angles in four freshly reopened down-rest/climb files. All four native outputs are exact but fail contact acceptance. Both reviews use saved-motion comparison without fitting/Apply/Save or hidden jobs. Following cameras do not establish world travel; seeking/capturing does not prove visible timed playback. The actual83 `--surface-contacts` run exits1 after41 preceding passing assertions at baked large-head support; later assertions were not reached. Its receipts are `retarget83-surface.log`, `retarget83-surface/failure.txt` and `retarget83-surface-exit.json`. The earlier82 run fails the same assertion. No full Core/surface83 or32-row fresh pass is inferred from focused scopes.

The fresh native commands were:

```powershell
$env:FIGHTERPORT_RETARGET_CASES='DownWaitU'
./artifacts/retarget83-runner/FighterPort.Tests.exe artifacts/retarget83-native-floor --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --floor
$env:FIGHTERPORT_RETARGET_CASES='CliffClimbSlow'
./artifacts/retarget83-runner/FighterPort.Tests.exe artifacts/retarget83-native-ledge --retarget-matrix artifacts/native-v2-mario.fighterport artifacts/native-v2-snake.fighterport --ledge
Remove-Item Env:FIGHTERPORT_RETARGET_CASES
./artifacts/retarget83-runner/FighterPort.Tests.exe artifacts/retarget83-surface --surface-contacts
```

The two matrix commands exit0 for native encoding checks while reporting contact failures in every row. The surface command fails as recorded above. Actual GUI capture/review commands are retained under the ignored `artifacts/interactive-retarget-preview/`: `capture-native83.ps1` selects only DownWaitU/CliffClimbSlow, `assemble-review83.ps1` assembles the integer/fraction sheets, and `build-gallery83.ps1` publishes the four-clip local gallery after actual inspection. Image generation alone does not satisfy review. These diagnostic scripts contain local private fixture paths and are not public source inputs.
# Background model screenshots (21 October)

Use the shared offscreen GPU path while the PC is in use. It does not open a window, use UI Automation/focus/input, or copy desktop pixels. Do not run Test-Desktop/Test-Library or the historical foreground capture harness concurrently with the user's work. Their interaction/layout/visible-playback checks remain separate; model-image capture is not their replacement acceptance.

Build the current desktop, then launch a request (paths below are local ignored prepared inputs):

```powershell
./.tools/dotnet/dotnet.exe build FighterPort.slnx -c Release --no-restore -m:1
./scripts/Test-BackgroundCapture.ps1 -SourceProject artifacts/native-v2-snake.fighterport -TargetProject <fresh-native-target.fighterport> -SourceClip CliffClimbSlow -OutputDirectory artifacts/<new-capture> -Ledge -ExtraFrames .125,.375,13.8107,38.9002,59.9999
./scripts/Control-BackgroundCapture.ps1 -OutputDirectory artifacts/<new-capture> -Action Status
./scripts/Control-BackgroundCapture.ps1 -OutputDirectory artifacts/<new-capture> -Action Pause
./scripts/Control-BackgroundCapture.ps1 -OutputDirectory artifacts/<new-capture> -Action Continue
./scripts/Control-BackgroundCapture.ps1 -OutputDirectory artifacts/<new-capture> -Action Cancel
# Once a cancelled job exits, remove its exact .cancel marker, then:
./scripts/Test-BackgroundCapture.ps1 -OutputDirectory artifacts/<new-capture> -Resume -Wait
```

Without `-Frames`, capture every integer target sample plus fractional/closing probes. `-Angles` defaults to Side/Front/Low. `-Frames` accepts an explicit diagnostic subset. Supply the real `-SourceUp/-TargetUp` and `-SourceInward/-TargetInward` when the scene axes differ from world Y-up/Z-inward; `-Ledge` is supplied context, not action detection or finite-platform acceptance. Source/target clocks map through their actual rates and optional request timing map. Source is left, target right, with exact frame/identity records and PNG/pixel hashes in receipt.json. Each body is framed independently; this does not verify absolute actor travel.

Defaults: one serialized capture process, BelowNormal priority, child-only DOTNET_PROCESSOR_COUNT=2 (a .NET scheduling hint, not a hard CPU cap), four pairs/second,640px panes. `-MaxFramesPerSecond` can lower load. Native decoding/import, verification and contact solving remain separate jobs; no solve/Apply/Save occurs while capturing. Pause/cancel is cooperative at pair boundaries and queued lease acquisition; source preparation may finish before observing it. Restart/resume requires identical request, project hashes and Core/Rendering/Desktop binaries; changed dependencies require a new directory. Valid images are retained, damaged/missing images regenerated. A partial/cancelled receipt is not complete; a failed GPU/provider request never falls back to foreground capture.

Rendering uses pinned Avalonia11.3.9 private graphics-service lookup, emitting AVA3001. Record that warning rather than claiming zero warnings. The normal backend remains provisional and GPU recovery/DPI remain deferred. The discarded custom WGL trial failed intermittent startup and is not a second backend. Current provider/GPU/progress/control receipts are under `artifacts/background-platform87*`; image generation is distinct from actual all-frame review. Native contact/articulation failures remain recorded in RETARGET_VALIDATION.

Fresh completed evidence: `background-platform87-acceptance.json` verifies870 PNG hashes, exact paired frame times, unchanged source/target files, native program/probe results and absence of visible/foreground capture windows across four down-rest/slow-climb native outputs. `background-platform87/visual-review.json` records actual inspection of all272 integer/18 fractional poses from Side/Front/Low (36 contact sheets); all four retarget cases remain failed. Control/repair/stale receipts separately prove serialization, pause/cancel/resume, identical damaged-image repair and refusal of changed settings. `scripts/Build.ps1 -Publish` succeeds with AVA3001 (`background-platform87-publish.log`, exit0). The published self-contained app captures the three user climb witnesses from three angles with the normal editor mutex occupied, producing the same pixels as the normal build (`background-published87-witnesses-check.json`). The unchanged native worker was built82. Final focused contracts rerun129 assertions with exit0 (`background-platform87-final-contracts-exit.json`); workflow54 and four exact native reopens are separate87 receipts, not overlapping assertion totals. No foreground GUI interaction or game acceptance was run for this background-capture amendment.
