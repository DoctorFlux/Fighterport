> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# SHP0 concurrent preview — 2026-10-18–19

Subsequent viewport increment: the probe-related surface stipples described in historical acceptance below were reproduced on authentic Mario and fixed by blocking validation color/depth writes. Exact mixed probed/unprobed comparisons now pass on original/Mario/Snake; see `VIEWPORT_VALIDATION.md` for the new hardware-specific evidence and remaining GPU gates. Further SHP0 authoring is deferred by the user's direction. SCN0 stays skipped.

SHP0 v3/v4 now previews position, normal and both vertex-color morphs in the same viewport/document/clock as CHR0, SRT0, VIS0, PAT0 and CLR0. Exact same-name companions in the same source/hash/container match automatically. Manual family selectors remain available. SCN0 is preserved in the resource catalogue, excluded from selectors/matching and rejected if activation is requested.

Reimport a model package and its separate motion package to prepare morph data for older saved projects. Select the bone animation; a matching SHP0 runs with it. Select SHP0 None to disable only morph deformation. Each family keeps its own native rate, sample count, last sample and loop policy. Bone preview Loop does not rewrite native flags.

## Implementation and fidelity

The isolated audited BrawlLib worker decodes SHP0 entry flags, native Hermite weight curves and target-array identities. It resolves model arrays and native facepoint vertex/normal/color indices before playback. Core receives typed prepared values without Brawl node/UI dependencies. Values retain native source references/hashes; the base scene, skeleton, bind data, topology, UVs and skin weights remain unchanged.

Core blends multiple targets with residual base weight `1 - sum(weights)` without clamping or renormalizing native weights. Disabled update flags remain preserved. Normal sampling retains the native weighted forward/reverse behavior; the reverse is a weighted sum of individual inverse skin transforms, not the inverse of their sum. RGBA interpolation uses native byte truncation. Missing/ambiguous active arrays report exact entry/mesh diagnostics and omit affected bindings. Permanently zero-weight missing targets retain their raw channels and emit a nonfatal diagnostic. Prepared targets are bounded to 32 per entry and 2,000,000 facepoint values per layer.

The renderer streams only changed position/normal/color ranges into a dynamic GPU buffer, leaving skin/UV/index data static. Real mesh skinning and native material programs remain on the GPU. Repeated paused samples reuse prepared output and avoid uploads. Disabling a morph restores original vertex attributes; restoring it does not accumulate deformation. Scene/layer revision changes and Undo/Redo invalidate prepared data. No native lookup, worker call, decoding, baking or contact solver runs on this playback path. Explicit acceptance-only transform feedback/readback verifies actual GPU positions, normals and both colors.

## Fixtures and native results

The supplied Mario/Snake motion packages contain no SHP0. Tests therefore generate original genuine SHP0 v3/v4 fixtures and author additional morph arrays/motion into NEW private Mario/Snake copies. Those copies are stress fixtures, not game-authored facial animation. They retain every existing deform bone, bind, base vertex and weight. Private model packages also retain original texture/palette resources, verified against decoded source pixels. Originals are never overwritten or committed.

The fixtures exercise multiple targets, negative weights, summed weights above one, facepoint seams, normal/color0/color1 arrays, disabled flags, missing base/target arrays, a harmless missing zero-weight target, differing bone/morph sample counts, companion texture/visibility animation and a real same-name SCN0 that remains inactive. Samples include 0, 0.125, 7.75, 15, 23.375, 30, 59, 59.75, 60, 60.125, 75, 119.75 and repeated out-of-order seeks. A fresh native process independently resets shape data, applies CHR0, then SHP0.

| Native fixture | Maximum skinned position error | Maximum normalized normal error | Color0/Color1 error |
|---|---:|---:|---:|
| Original SHP0 v3 | 9.555351e-7 | 1.712013e-7 | 0 / 0 |
| Original SHP0 v4 | 9.555351e-7 | 1.712013e-7 | 0 / 0 |
| Authored Mario copy | 4.075842e-6 | 4.267694e-7 | 0 / 0 |
| Authored Snake copy | 7.629422e-6 | 4.059712e-7 | 0 / 0 |

All 62 focused native checks passed in `artifacts/shp-native-final/results.json`. Core checks passed 172 in `artifacts/shp-core/results.json`; the complete original native regression passed 229 in `artifacts/shp-regression/results.json`. These counts overlap and must not be added as independent coverage. Save/reopen, fractional timing, source hashes, unchanged deform checksums and manual-selection Undo/Redo are covered.

## Displayed acceptance and black-model fix

Visible tests operate real controls, save/reopen in a fresh process, verify one-shot/restart/loop behavior, disable/restore SHP0 at frame 15.25 and compare actual viewport pixels. The bone pose stays unchanged; restored SHP0 pixels are identical. Playback samples exclude timeline/counters. SHP0/SRT0/VIS0 are active concurrently with CHR0; Core companion tests additionally cover PAT0/CLR0 combinations. This does not establish every native companion/material effect.

| GUI artifact directory | Checks | Changed captures / 90 | Paused CPU / 750 ms | Native programs | GPU position error | GPU normal error | GPU color error |
|---|---:|---:|---:|---:|---:|---:|---:|
| shp-original-gui | 24 | 72 | 93.75 ms | 1 | 0 | 0 | 0 |
| shp-mario-gui | 25 | 73 | 0 ms | 7 | 1.3486991e-6 | 1.365714e-7 | 0 |
| shp-snake-gui | 25 | 74 | 93.75 ms | 12 | 3.8146973e-6 | 1.365714e-7 | 0 |
| shp-stock-mario-gui | 22 | 54 | 15.625 ms | 7 | 2.0509542e-6 | 1.8848644e-7 | 0 |

Screenshots were inspected: native Mario/Snake textures are visible, including Mario's eye layers. The authored target shapes deliberately exaggerate geometry/normals/colors; their distorted appearance is not a fidelity claim about an original game animation. The stock Mario run uses the supplied authentic game motion without morph targets. It additionally samples 2,969 lit surface pixels under the bounded screen check, excluding dark grid/pure joint-axis colors, and passes native material diagnostics. Hardware: RTX3060, D3D11 driver 32.0.16.1692, ANGLE OpenGL ES3.0. These screen samples establish displayed change at the observed cadence, not exact compositor presents or a universal 60 FPS guarantee.

The reported pure-black model was reproduced on an authored test copy missing its textures. Brawl's textures can live in a different PAC section from the model BRRES. The fixture now collects actual texture/palette payloads from the source package rather than only the model's BRRES. A separate renderer fix uses a lit fallback with explicit diagnostics when a required image is unavailable, instead of binding texture zero and producing a black silhouette. A saved project with missing texture data needs reimport from the complete original package to recover native materials. No SCN0 lighting is required or enabled. The deliberately textureless-copy run `artifacts/shp-missing-textures-gui/results.json` passed 20 checks, 71/90 changed captures, 5,937 lit surface samples, seven expected missing-image diagnostics and 31.25 ms paused CPU/750 ms. Its SHP0 off/restore pixels also match exactly; the fallback is not claimed as native material fidelity.

Earlier acceptance attempts exposed the missing texture fixture and status-text layout expansion. Constraining status text prevents camera/viewport movement when a long morph diagnostic appears. A bounded startup settling check retains the original paused CPU limit of 150 ms/750 ms; it does not hide sustained idle work. Final runs close their own processes.

## Commands and remaining limits

```powershell
./scripts/Build.ps1 -Native -Publish
$env:FIGHTERPORT_BRAWL_WORKER=Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/shp-core
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/shp-regression --native
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/shp-native-final --morph-only
./scripts/Test-Desktop.ps1 -Project <copy-of-generated-project> -Application artifacts/windows-x64/FighterPort.Desktop.exe -OutputDirectory <ignored-output> -VerifyMorphs -VerifyTransport
```

Repeat `--morph-private <model-package>` for local authored-copy checks. Add `-VerifyNativeMaterials` for textured native GUI fixtures. Exact private commands and generated paths remain in ignored `.local/acceptance/commands.ps1` and artifacts. Release/native builds and self-contained publication passed with zero warnings/errors; `artifacts/windows-x64/FighterPort.Desktop.exe` is the local runnable build, not a public release.

Existing SHP0 weight keys can now be edited and saved into NEW PAC/BRRES copies, as detailed below. New target geometry/entries, cross-topology morph retargeting, additional revisions and actual game testing remain unimplemented/unverified. Private game-authored SHP0 plus its exact model arrays is still the missing real-asset acceptance input. Full shader image parity, authored mip/LOD fidelity and advanced effects remain separate gates. SCN0 stays skipped. M2 remains the one open milestone; its next work is recorded in `plans/current.md`.

## Weight editing and native container output — 2026-10-19

Open the active **Morph keys** section. Choose an existing resolved target; enter a weight, tangent and frame in that morph's own native timebase. **Key** inserts/replaces a Hermite key, **Delete key** removes it, and deleting the last key resets the channel to a zero constant. Weight edits update every position/normal/color binding sharing the exact native entry/target, while preserving the original target arrays, skeleton, bone pose and source curves. Undo/Redo and project persistence use the shared document. Blue timeline markers represent the selected morph channel in bone-clip time. Ambiguous/unresolved targets require their complete native model/motion pair before editing.

**Save motion as…** writes a NEW file of the existing container type: PAC to PAC or BRRES to BRRES. It saves the selected existing CHR0 and all edited SHP0 layers in that exact source/container. It keeps native names, versions, sample counts, flags and loop policies; it does not append repeated endpoints. Reopening updates the native source identity and is one undoable document command. Fighter metadata, control setups, clip identities, original resource provenance and recovery of prior work are retained. Native output never overwrites a source or an existing destination.

The exporter stages the output. A fresh compatibility process reopens it, verifies complete edited SHP0 keys/tangents/flags/timing and hashes of every unrelated native resource unit, including skipped SCN0 and legitimate empty placeholders. The owned evaluator then compares all-bone positions and actual morphed/skinned positions, normals and both colors against independent native sampling at edited key times, withheld fractional neighbors, initial/interior/final samples and loop boundaries. Tolerances are 0.001 native position units, 0.0002 normalized normal distance and 1.01/255 color distance; results must pass before atomic commit. Every edited layer's native key manifest is checked; geometry sampling uses the selected active morph. A deliberate prepared-target mismatch test must discard the stage with no committed file.

Supported native edits use unique integral key times, Hermite interpolation and one tangent per key. Non-looping keys stop at the last sample; looping clips may retain a distinct closing key at sample count. A single fixed SHP0 channel stores only its value, so its sole key must be frame zero with zero tangent. Fractional scrubbing remains supported, but fractional native key storage, split tangents, unsupported interpolation and changed timing require explicit future conversion policies and are rejected. Verification is bounded to 4,096 distinct sample times per save. PCS compression, cross-container conversion, edited SRT0/VIS0/PAT0/CLR0/SCN0 and new/retargeted CHR0 insertion use separate or future workflows. New target geometry is described below.

The procedural fixtures are genuine original SHP0 v3/v4 with multiple position/normal/color targets. Authored Mario/Snake copies retain their existing deform data and texture/palette pixels. Native tests save both BRRES and PAC, with private PAC fixtures also retaining the supplied original motion catalogues. These are authored stress fixtures: the supplied original motions contain no game-authored SHP0. No game installation or in-game result is inferred.

CLI commands use the same Core edits and adapter as the GUI:

```text
morph-key <project> <clip-index> <raw-layer-track-index> <native-frame> <weight> <new-project>
save-motion <project> <clip-index> <new-existing-type-pac-or-brres>
```

Use `--morph-only` for original native round trips and repeat `--morph-private-pac <model-package> <motion-pac>` for private source copies. Add `-VerifyMorphs -VerifyMorphEditing` to desktop acceptance to exercise actual weight/delete controls, Undo/Redo, project Save, native output, automatic reopen and displayed playback. Add `-AnimationSearch 'OriginalMorph ['` for the appended private PAC fixture and `-VerifyNativeMaterials` for textured sources. Exact private paths remain ignored.

### Displayed edited-native acceptance

All three runs below passed on the self-contained x64 build. The original fixture saves a new BRRES; the private runs save new PACs carrying the original motion catalogues. Checks operate the real numeric/key/delete controls, compare changed and restored mesh pixels, invoke actual native Save, inspect reopened key values/source identities and sample displayed playback after reopening. Private checks also save the Undo/Redo states and independently inspect pending edits, new revision identity and retained original provenance. `morph-info` returns compact persisted metadata so acceptance does not materialize the whole motion catalogue in PowerShell repeatedly.

| Run under artifacts | Checks | Changed captures / 90 | Paused CPU / 750 ms | GPU position error | GPU normal error | GPU color error | Unchanged native units |
|---|---:|---:|---:|---:|---:|---:|---:|
| shp-edit-original-gui | 34 | 73 | 46.875 ms | 0 | 0 | 0 | 5 |
| shp-edit-mario-gui | 40 | 73 | 15.625 ms | 1.96605e-6 | 1.3348813e-7 | 0 | 429 |
| shp-edit-snake-gui | 40 | 35 | 0 ms | 3.8221697e-6 | 1.365714e-7 | 0 | 810 |

The same RTX3060/ANGLE hardware was used. Native export/reopen receipts at 15 key/fractional samples report maximum combined bone/skin position errors 7.251601e-7 original, 3.724217e-6 Mario and 6.398552e-6 Snake; normalized normal errors 3.100731e-7, 1.081541e-6 and 9.873355e-7; colors match exactly. Native material programs run without fallback diagnostics (7 Mario, 12 Snake); screenshot checks find 3,951/1,989 lit surface samples. Snake's 35/90 changed captures meets the retained threshold but is not evidence of universal 60 Hz playback. Native and GUI probes measure different paths and are reported separately.

Initial visible runs found an inaccessible generic expander and a reopened-layer GUID mismatch. The editor now uses an accessible fold button and preserves/remaps document identities when reopening; Undo recovers the previous document. An invalid numeric input interrupted one preliminary run. A later timeline screen test encountered an unusable window position/black capture; final acceptance restores/foregrounds its own test window after modal/file operations. Thresholds were not relaxed. Normal export validation continues off the UI thread after the compatibility response; newer edits made during a save are retained rather than overwritten by a stale snapshot. Unbaked pose previews must be baked before native save. No editor/worker is intentionally left running after acceptance.

### Final numerical/native results

Release modern/native builds and self-contained x64 publication passed with zero warnings/errors (`artifacts/shp-edit-build.log`, `shp-edit-build-final.log`). Core passed **181** checks (`shp-edit-core/results.json`). Original v3/v4 plus authored Mario/Snake read/edit/new BRRES/new PAC round trips passed **110** checks (`shp-edit-acceptance/results.json`). The full original native regression passed **250** (`shp-edit-regression/results.json`). Counts overlap and must not be added as independent coverage. Final native tests remove an existing frame-30 key and edit a distinct frame-60 loop-closing key without changing sample counts. Deliberately corrupted prepared target values are refused and leave no staged/committed output.

```powershell
./scripts/Build.ps1 -Native -Publish
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/shp-edit-core
$env:FIGHTERPORT_BRAWL_WORKER=Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/shp-edit-acceptance --morph-only
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/shp-edit-regression --native
./scripts/Test-Desktop.ps1 -Project <copy-of-write-input-project> -Application artifacts/windows-x64/FighterPort.Desktop.exe -OutputDirectory <ignored-output> -VerifyMorphs -VerifyMorphEditing -VerifyTransport
```

The recorded 110-check focused run also repeats `--morph-private-pac` for both supplied model/motion pairs; exact commands are in ignored `.local/acceptance/commands.ps1`. Each native fixture directory contains compact `write-receipt.json`, edited BRRES/PAC outputs and fresh reopened projects. Private inputs, generated native assets, local paths/settings and builds stay ignored. Nothing was published. Next acceptance input is a game-authored SHP0 plus its exact model arrays; the existing viewport/material gates remain open, with SCN0 still skipped.

## New targets and position geometry

**Create target from base** adds an independent target channel to an existing resolved position entry. It begins at zero weight. The new model arrays clone native base coordinates, retaining exact index correspondence and deduplicated bind transforms. Shared normal/color bindings receive coordinated base-shape target arrays; moving positions does not automatically recalculate authored normals. Existing target position arrays are editable too. A target that is also a base mesh array is protected; create an independent target instead. Unique names, finite coordinates, native target limits and topology bounds are enforced before changing the document.

Select a native point index or enable **Pick point in viewport**, then apply native array X/Y/Z coordinates. All facepoints sharing the index update together through their original bind transforms. The viewport marks the selected point in cyan. Geometry commands, target creation, weight keys, Undo/Redo and project save/reopen use the same document and prepared GPU morph stream. There is no new deform skeleton, reweighting, bind normalization or runtime geometry decoding.

Geometry lives in MDL0, while weights live in SHP0. **Save motion as…** therefore stages two new files when geometry changed: the motion output and `<motion-stem>.model.<original-model-extension>`. The dialog displays both paths. Model containers retain their existing PAC, BRRES or standalone MDL0 type; motion output retains PAC or BRRES. Both must be new. The worker reopens both, verifies exact target-array coordinates, original base arrays, bones/hierarchy/rest/bind/inverse bind, weights, UVs, normals, colors, materials and unrelated resource hashes. Core compares the edited preview with fresh native bone/morph skinning at key and fractional times before either file is committed. A failed second move rolls back the newly committed first file; this is not a filesystem transaction across power loss. The editor reopens the pair as one undoable command and preserves target selection across native track reordering. Original provenance stays in the project.

Strict Mario acceptance exposed six changed float coordinates in the pinned x86 native writer. Byte reversal through an intermediate float can quiet a signaling NaN in its *reversed* representation, altering an ordinary finite coordinate. Owned array subclasses and a bounded raw-float cache reader move endian bits as integers. Tests include finite bit pattern `0x3f8cb3ff`, exact fresh array comparison and subsequent native ApplyCHR/ApplySHP skinning. The failing save was rejected; tolerances were not widened. See `LICENSE_AUDIT.md` for the selected APIs and existing local-only distribution limits.

Snake preservation also exposed a count/payload mismatch in earlier authored color fixtures: the general codec compacted duplicate colors while retaining the original count. The final fixtures and new shared color targets use an owned dense RGBA8 writer, retaining every index. Color bounds are checked before native extraction. Earlier authored color measurements above are historical and superseded by the regenerated geometry acceptance fixtures; no supplied original game file was changed.

This extension creates targets within existing resolved entries. First-SHP0 creation, unrelated base-entry creation, topology changes, direct normal/color geometry tools, automatic normal recalculation and cross-topology transfer remain open. SCN0 remains skipped and preserved. The supplied original motions still lack a game-authored SHP0/model pair; authored source copies do not replace that missing acceptance input. No game testing is claimed.

Additional CLI commands:

```text
morph-create <project> <clip-index> <binding-index> <unique-name> <new-project>
morph-point <project> <clip-index> <raw-track-index> <native-point-index> <x> <y> <z> <new-project>
morph-geometry-info <project> <clip-index>
```

### Geometry acceptance evidence

Release modern/native builds and self-contained x64 publication passed with zero warnings/errors (`artifacts/morph-geometry-build-final6.log`). Core passed **188** checks (`morph-geometry-core-final/results.json`), focused original v3/v4 plus authored Mario/Snake native checks passed **144** (`morph-geometry-verified/results.json`), and the complete original native regression passed **265** (`morph-geometry-regression/results.json`). Counts overlap. Native tests append two targets, edit an existing target, preserve native point slots, exercise exact float endian bits, reopen coordinates and save an already-reopened target again. Private tests also save/reopen NEW model PAC + motion PAC pairs with complete source catalogues.

| Displayed run under artifacts | Checks | Changed captures / 90 | Paused CPU / 750 ms | GPU position error | GPU normal error | GPU color error |
|---|---:|---:|---:|---:|---:|---:|
| morph-geometry-original-final | 41 | 72 | 0 ms | 0 | 0 | 0 |
| morph-geometry-mario-gui6 | 45 | 73 | 15.625 ms | 1.9073486e-6 | 1.3328004e-7 | 0 |
| morph-geometry-snake-gui | 45 | 69 | 15.625 ms | 3.8146973e-6 | 1.3411045e-7 | 0 |

The desktop runs use real target/name/point controls, compare actual changed geometry pixels, verify Undo coordinates and exact restored pixels, save projects, write/reopen both new native files, then verify new-source identity and Undo/Redo provenance. Private native receipts preserve 429/810 unrelated motion units, including SCN0; original preserves 5. At 15 key/fractional samples, combined bone/skin errors are 7.251601e-7 original, 3.724217e-6 Mario and 6.398552e-6 Snake; normal errors are 3.100731e-7, 9.858444e-7 and 9.873355e-7; color errors are zero. Mario/Snake execute 7/12 native programs without fallback diagnostics and retain lit-surface checks (3,951/1,989 samples).

The GUI harness selects exposed native indices (Mario 33, Snake 3) instead of assuming index zero is visible. Probe-enabled versus unprobed preliminary captures showed a small raster difference despite bit-identical prepared buffers and skin matrices. Final exact comparisons invoke the same explicit GPU probe path on index selection and Undo. Thresholds remain unchanged; general raster/backend parity remains an open spike gate. The explicit probe/hash work runs only in acceptance mode, outside normal playback. Own viewport clear state is reset each frame. No general 60 Hz or full BrawlCrate image-parity claim follows from these captures.

```powershell
./scripts/Build.ps1 -Native -Publish
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/morph-geometry-core-final
$env:FIGHTERPORT_BRAWL_WORKER=Join-Path $PWD 'src/FighterPort.BrawlWorker/bin/Release/net472/FighterPort.BrawlWorker.exe'
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/morph-geometry-verified --morph-only
./tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.exe artifacts/morph-geometry-regression --native
./scripts/Test-Desktop.ps1 -Project <fresh-copy-of-write-input> -Application artifacts/windows-x64/FighterPort.Desktop.exe -OutputDirectory <ignored-output> -VerifyMorphs -VerifyMorphEditing -VerifyMorphGeometry -VerifyTransport
```

The 144-check native run additionally repeats `--morph-private-pac` for the two supplied model/motion pairs. Private GUI inputs are copies of `pac-write-input.fighterport`, with `-AnimationSearch 'OriginalMorph [' -VerifyNativeMaterials`. Exact commands remain in ignored `.local/acceptance/commands.ps1`; native directories contain `geometry-receipt.json`, paired native files and reopened projects. Next authoring task: create the first SHP0 and a resolved base entry when a motion has no morph animation. A game-authored SHP0/model pair, advanced geometry tools and remaining viewport/material gates are still required acceptance work. Nothing was published or game-tested.
