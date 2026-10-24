> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# Viewport and catalogue acceptance — 2026-10-19

The user directed development away from further SHP0 authoring. This increment fixes a viewport discrepancy and verifies shared playback during large-catalogue interaction. SCN0 stays inactive. M2 remains the one current milestone; the graphics backend is provisional.

## Implementation and observed regression

The acceptance transform-feedback point draw could change displayed color/depth on the measured ANGLE backend despite rasterizer discard. Mario at frames 0, 1.25 and 15.25 reproduced dark surface stipples and unequal pixels with/without the probe. Separating the shader program alone reproduced the failure. Blocking color/depth writes during validation, restoring owned state in `finally` and retaining a separate validation program removed the discrepancy. This establishes the fix on the tested backend, not the driver's internal cause.

Validation requests now wait for a paused frame. Normal playback performs no validation readbacks. Acceptance-only F9/F10 redraws choose the probed/unprobed path with `FIGHTERPORT_GPU_PROBE=1`. Before/probed/after screen hashes compare exactly without a pixel tolerance.

Animation search debounces typing for 100 ms, caches prepared names and retains the virtualized list. Equivalent results avoid item rebuilding. Filtering preserves the document clip and fractional playhead even when the selected row disappears. Matching/total counts and accessible selected identity remain available. Clip-array changes, key edits and Undo refresh against the current query instead of clearing it. Filtering performs no source decoding, worker calls, contact solving or baking.

`fixture-catalogue` generates 1–50,000 uniquely named entries from four original procedural clips. It is a UI stress fixture, not a global library or further game support. No component/dependency was added or copied from another application.

## Final measurements

Hardware: NVIDIA RTX 3060; ANGLE 2.1.25606 (`cb8b4e1307a9`), Direct3D11 driver 32.0.16.1692, OpenGL ES 3.0. Windows used the same 1440×830 harness size; displayed sampling uses a 520×330 viewport crop excluding counters, lists and timeline. Screenshots were inspected.

| Desktop run | Checks | Changed displayed captures / 90 | Paused CPU ms / 750 ms | Maximum GPU position error |
|---|---:|---:|---:|---:|
| Original procedural fixture | 33 | 74 | 31.25 | 0 |
| Authentic Mario model + separate motion | 23 | 52 | 15.625 | 2.0509542e-6 |
| Authentic Snake model + separate motion | 36 | 41 | 15.625 | 3.9393212e-6 |
| 10,000 original entries, probes disabled | 22 | 78 | 0 | No readback |

Original/Mario/Snake before/probed/after pixels match at all three integer/fractional samples. Mario/Snake execute 7/10 native material programs without fallback diagnostics and retain 63/70 displayed joints. Native normal errors are below 1.89e-7; color errors are zero. Snake additionally passes saved reviewed controls, foot/hand IK, one-step Undo/Redo and pelvis editing with planted feet (1.118034e-6 residual). Both native runs pass visible loop/restart. Original numeric/pointer FK, IK, new animation creation and save/reopen pass.

During final sustained catalogue interaction, 235/240 screen captures changed: observed p95 interval 66.57 ms, maximum 101.48 ms. Six live queries include 10,000, 19 and zero matches. Four wheel operations change visible realized rows. Every filter preserves the active clip and exact document frame. Live filtering takes 0.53–2.73 ms, excluding debounce and later compositor/layout work. Key edit/Undo preserve the query/count. Validation readbacks remain zero. A preceding traced run passed 20 checks: 236/240 changed captures, p95 54.12 ms, maximum 113.74 ms.

An earlier untraced catalogue run stopped playback; its cause was not established and no fix is attributed to it. Both later traced runs completed. One overlapping desktop launch was rejected by the single-instance guard; the clean sequential original run passed. These observations do not establish arbitrary hardware performance or context recovery.

Modern/native Release and self-contained x64 build: zero warnings/errors (`artifacts/viewport-final-build.log`). Meaningful Core regression: 188 checks. Full original native regression: 265, including fresh edited CHR0 reopen, timing, skin geometry and original preservation. Counts overlap. No native reader/writer was changed in this increment.

## Reproduce

```powershell
./scripts/Build.ps1 -Native -Publish
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/viewport-core
$env:FIGHTERPORT_BRAWL_WORKER = Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/viewport-native --native
./scripts/Test-Desktop.ps1 -Application artifacts/windows-x64/FighterPort.Desktop.exe -OutputDirectory artifacts/viewport-original-final -VerifyViewportParity -VerifyTransport -VerifyAuthoring
./src/FighterPort.Cli/bin/Release/net10.0/FighterPort.Cli.exe fixture-catalogue artifacts/catalogue-10000.fighterport 10000
Copy-Item -LiteralPath artifacts/catalogue-10000.fighterport -Destination artifacts/catalogue-10000-final.fighterport
./scripts/Test-Desktop.ps1 -Project artifacts/catalogue-10000-final.fighterport -Application artifacts/windows-x64/FighterPort.Desktop.exe -OutputDirectory artifacts/viewport-catalogue-final -AnimationSearch 'Catalogue 00000' -VerifyCataloguePlayback -DisableGpuProbe
```

Private runs use ignored project copies: Mario adds `-AnimationSearch 'Wait2 [' -VerifyNativeMaterials -VerifyViewportParity -VerifyTransport`; Snake uses `Wait3 [` and also `-VerifyNativeControls -VerifyBodyContacts`. Exact private commands remain in `.local/acceptance/commands.ps1`. Supplied model/motion fixtures were available; no new private acceptance input was required. Originals were not edited.

Evidence: `artifacts/viewport-core/results.json`, `viewport-native/results.json`, `viewport-original-final/results.json`, `viewport-mario-final/results.json`, `viewport-snake/results.json`, `viewport-catalogue-final/results.json`; parity runs also contain `viewport-parity.json`, catalogue runs `catalogue-playback.json` and `catalogue-scrolls.json`. Run GUI acceptance sequentially; `finally` closes its own app. Assets, settings and artifacts stay ignored.

## Remaining gates

Next concrete task: recover from a recoverable GPU/context/resource failure without replacing the document, then verify sizing/hit testing across a real per-monitor DPI change. Independent BrawlCrate image comparisons, bone scale/label modes and advanced material fidelity remain open. The backend is not finalized. Screen sampling measures observed cadence, not exact compositor presents or universal 60 Hz. Ten thousand entries reuse four prepared clips; this is not a disk-import/library benchmark. Full source-bound library UX and broader retarget/contact coverage remain product work before transition fitting/slots and further adapters. No game test or publishing was performed.
