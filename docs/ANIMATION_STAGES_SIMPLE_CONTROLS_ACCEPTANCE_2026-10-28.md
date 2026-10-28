# Animation stages and simpler controls — preview.55 acceptance

**Status: bounded source acceptance; 5,695 checks in 13 admitted runs.** Starting
commit: `11d6c1d708e3254d1c68101dfcee70b0c9970214`. This is the sole current
increment within the ONE incomplete G1/M2 milestone. The qualified launcher
selects `Fighterport-stages55/native4`; `Start-FighterPort.ps1 -Check` resolves
its exact verified executable without opening a visible window. It identifies as
`2.0.0-preview.55-animation-stages-simple-controls`, file version `2.0.0.31`.

This record covers a measured four-stage workflow, fewer visible optimization
controls, a source-byte accounting correction and a bounded search improvement.
It does **not** claim a new retargeter, a general curve-refitting rewrite, a
globally smallest CHR0 or universal animation/contact/game acceptance. See the
[current plan](plans/current.md), [export architecture](ENGINE_INTEGRATION.md),
[four-stage benchmark](CHR0_FOUR_STAGE_BENCHMARK_2026-10-28.md),
[write-path audit](CHR0_WRITE_PATH_AUDIT_2026-10-28.md) and preceding
[preview.54 acceptance](NATIVE_CHR0_EXPORT_OPTIMIZATION_ACCEPTANCE_2026-10-28.md).

## Behavior and source changes

The complete native BrawlCrate Model Previewer and original Next retargeter remain.
Retargeting and fitting retain an editable full-quality target master. Ordinary
project Save preserves that master exactly; optimization prepares a separate
export copy against the same finished target animation. Root ownership, all
original skin coefficients, native Undo and the existing numeric gates remain.

| Changed product file | Bounded change |
| --- | --- |
| `BrawlCrate/Studio/WorkspaceExportOptimizationOptions.cs` | Keep Exact / Error-bounded visible; put search effort and seven saved limits behind Advanced. Disclosure changes no settings and restores focus when collapsed. |
| `BrawlCrate/Studio/AnimationOptimizationDialog.cs` | Show Optimize, Export optimized copy and Close while idle; replace Optimize with Cancel optimization while preparing. Keep existing read-only native previews and expandable measurements. Shorten repeated introductory text. |
| `BrawlCrate/Studio/NativeChr0SizeReport.cs` | Separate required stream extents from optional allocation padding, including tightly packed imported dense streams. Resolve padding after referenced ownership is known. |
| `BrawlCrate/UI/ModelForm.GeneratedOptimization.cs` | Permit up to eight selective native repair attempts for Thorough, versus two for Standard, inside the same aggregate proof/native budgets. |
| `BrawlCrate/BrawlCrate.csproj` | Candidate preview/file version only. |

Exact remains the new-preference default. Error-bounded stays an explicit policy;
saved preferences and tighter limits retain their existing behavior. Search
effort never grants additional motion error. No optimization occurs on playback,
ordinary Save, retarget Apply or fitting Apply. Historical fitting/retarget
receipts are not rewritten to describe an optimized export as their original
result. No project-format migration or second document/Undo owner is added.

## Four-stage measurement

The native3 `sequence3` private-desktop run passed **1,906 checks**. It uses the
exact 44-frame Sonic AttackAirLw, default original Next Dense retargeting to Mario,
full-body start/end fitting to **Mario Fall frame 1 only**, then explicit
Error-bounded/Thorough export. The entry join is display frame 5; recovery begins
at display frame 35. The attack middle and attack-owned translation travel remain
authoritative. Fitting can author the reviewed reference rotation controls when
that existing option is enabled; compression must then preserve the finished
master, including those authored controls.

All four files retain 44 frames and a non-looping stored state. Sizes are physical
standalone bytes; resource size excludes external names. Key counts below are
native decoded logical keys, not a count of physical compressed samples. Format
counts describe varying S/R/T groups; fixed inline groups are excluded.

| Stage | File bytes | Resource bytes | Keys | Tracks | Varying groups |
| --- | ---: | ---: | ---: | ---: | --- |
| Sonic original native re-export | 12,016 | 11,125 | 7,120 | 66 | L1 × 28; I4 × 26; I12 × 3 |
| Mario original Next full-quality master | 306,324 | 305,548 | 28,318 | 60 | I12 × 177 |
| Mario fitted full-quality master | 235,344 | 234,544 | 21,540 | 62 | I12 × 186 |
| Independent optimized Mario export | 57,684 | 57,068 | 4,797 | 48 | I4 × 55; I12 × 46; I6 × 1 |

The correct compression comparison is **235,344 → 57,684 bytes**, saving
177,660 bytes (**75.489496%**) against the same finished fitted master. Fitting's
size difference is an authoring change, not a compression saving. The final file
is still about **4.80 times** the different-rig Sonic source. No donor-sized
output or fixed savings percentage for other animations is promised. A separate
diagnostic precise I12 rewrite of Sonic is 87,600 bytes; the ordinary source
re-export preserves its original compact storage instead.

The membership audit finds no absent-target tracks in these Mario masters.
The fourteen omitted target entries are proved bind-valued tracks with eligible
native semantics; being unweighted or having an unfamiliar name is not a removal
rule. Rotation streams and slots still account for 86.94% of the final file.
The benchmark separates those actual costs from hypothetical further savings.

| File identity | SHA-256 |
| --- | --- |
| Sonic input and byte-identical re-export | `1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4` |
| Mario original Next master | `2179cbe08a40d64d19938b0ebdf94b4c715b76dcd5b581443098091f0d6dba2e` |
| Mario fitted master / compression reference | `3e7ab2dbdb80818d6dae073be29715da8912282a128c991c7b54c3eab5612dfa` |
| Mario optimized export, Standard and Thorough | `fbde75a7779858a635257aa7f28d5b0411480a2b4e45e9a0adb1587f5a0c6659` |

Each authoring stage is saved and independently reopened. The last project save
after export still contains the exact full-quality fitted master. Selection,
frame, target/bone identity, storage, Undo/Redo and original input hashes are
checked around export. Private assets, source paths, projects and generated files
remain local and are excluded from source publication.

## Measured quality and search behavior

The unchanged saved Error-bounded ceilings are 0.1° raw Euler/local orientation,
0.002 native translation units, 0.0001 scale and `0.001 × L` for complete native
bone/skin error, with corresponding derivative ceilings per native frame of
0.1°, 0.002 and 0.0001. Here `L = 23.7778039`, the authentic target bind-geometry
diagonal. Exact motion controls, mandatory fitting boundaries and other exact
protections override approximation. These are the existing explicit export
ceilings, not newly relaxed thresholds.

| Measurement on the decoded optimized candidate | Observed maximum |
| --- | ---: |
| Raw Euler difference | 0.0120429993° |
| Local orientation difference | 0.0140224411° |
| Native world-basis angular difference | 0.0175986969° |
| Local translation | 0.0000048966635 native units |
| Scale component | 0 |
| Native world position/scaled basis | 0.00730349203 native units |
| Complete skin displacement | 0.000698474369 native units |
| Euler derivative | 0.0123767853° / frame |
| Translation derivative | 0.00000391207267 / frame |
| World / skin error derivative chords | 0.00760961281 / 0.0012232349 per frame |

The selected candidate has **9,272 native samples**. Aggregate search work is
22,645/100,000 logical witnesses, 39,659 actual baseline/candidate pose
evaluations, 2,498,517/10,000,000 bone visits and
155,185,667/1,000,000,000 vertex visits. Samples cover integer/quarter/withheld
fractions, native knots and paired sides, endpoints, mandatory boundaries and
bounded adaptive midpoints across all native bones and vertices. The independent
withheld pass adds **132 samples over 63 bones and 3,913 vertices**; its maximum
world/skin errors are 0.00719855457 / 0.000695199261 native units. Continuous
rational scalar value/derivative certificates and sampled cumulative geometry
are separate evidence. Finite geometry samples are not a continuous-time proof.

Plain frozen-pose fit admission reports an initial strict-boundary ratio of
0.002154236 against its existing 0.5 admission maximum. Four endpoint/join/closing
poses retain complete native local/world/skin float outputs exactly. The middle
oracle checks **378** native and model-flag-aware witnesses, reporting zero world
position/basis difference. This is frozen-pose fitting acceptance, not ground,
ledge or contact solving acceptance.

Thorough prepares in 63,585 ms by the outer harness timer; the internal
preparation receipt measures 63,494 ms. A separate Standard attempt from the
**same untouched fitted master** takes 58,958 ms and produces the same byte hash,
size, key count and measured quality. The real fixture therefore establishes
determinism and no regression, **not an extra saving caused by Thorough**.
Both select the 0.125 scalar-precision candidate; a later 0.25 candidate is
larger, and remaining Core proof budget is exhausted at 0.5. The already verified
smaller candidate survives; the search is bounded and not globally optimal.

The new independent four-branch native regression demonstrates the narrower
benefit of extra attempts: three scalar-eligible but collectively failing leaf
branches plus one eligible branch share a moving exact root. Two attempts reject
the remaining failure; eight available attempts recover a verified candidate
after four complete evaluations, retaining the eligible reduction. Same-master
Exact is **97 keys / 1,224 bytes**; the verified composition is **94 keys / 1,188
bytes**. Root tuples, v4 loop metadata, track order and immutable input wires
remain. Failed and successful attempts consume the same aggregate budget.

## Byte-accounting correction and independent inspection

The first sequence attempt exposed a real source-export rejection: accounting
required four-byte-rounded allocation after every imported L1 stream. The
original Sonic resource validly packs dense streams without that padding and
ends at an unaligned resource boundary. Required headers/payloads are now checked
within the declared resource; optional alignment is classified only after all
referenced structures own their bytes. Zero bytes belonging to the next stream's
header remain header bytes. Nonzero unknown bytes remain explicitly unclassified.
Required truncation, incompatible overlap and inconsistent shared-stream extents
still reject. No source bytes, codecs or terminal samples are rewritten by this
accounting change.

Synthetic adjacent 13-byte L1 streams exercise zero and nonzero following headers,
unaligned terminal boundaries, truncated payload/header rejection and overlap
rejection. The authentic Sonic payload is also independently read with all
required **N+1** dense samples. Fresh current-package `structure4` passes **178 checks**;
the earlier `structure2` run remains separately identified historical evidence.

The independent local decoder audit of sequence3 covers **six files**, including
the immutable Sonic input, its re-export, both Mario masters and both search
efforts. All physical bytes are assigned, with zero unclassified nonzero bytes,
**524,721 native scalar comparisons** and **16,561 outgoing-side comparisons**.
This validates parsing/codec attribution separately from native pose acceptance.
The optimized physical allocation is:

| Allocation | Bytes |
| --- | ---: |
| Curve key payload / codec headers | 51,076 / 3,272 |
| Bone headers and value/offset slots | 1,888 |
| Resource dictionary / resource header | 792 / 40 |
| Names / proved alignment / user metadata | 549 / 67 / 0 |
| Unclassified / total standalone length | 0 / 57,684 |

The independent writer/reader agreement does not establish execution in Brawl.
PAC/BRRES alignment, outer compression and game memory savings are not measured
by these standalone receipts. Duplicate-payload and alternate-format bounds in
the exploratory reports are unrealized diagnostics, not additional saved bytes.

## Run provenance, admitted evidence and stopped attempts

Local evidence root: `artifacts/Fighterport-stages55`. The native3 build receipt
reports all **911 shared backend/runtime files byte-identical** to the qualified
preview.54/native19 seed. This is backend identity, not whole-application identity.
Fresh native3 application SHA-256:
`3C5A2045FA2D0E97053D9EFB793F35ECABA58D32C299776EADA5194F940E6B1E`.
The later native4 package rebuilds only the segmented test harness: all **913
application/runtime files** retain their native3 bytes, including the application
hash above. Neither the numerical backend nor product UI changes for this test
split. Only the shared-fixture explanatory comment changes after that harness
build; no behavior, assertion or product code changes. The commit containing
this record identifies the delivered source; the local publication receipt
records its exact hash, owner attribution and remote verification.

| Evidence selection | Recorded status |
| --- | --- |
| Native3 `-NativeAnimationOptimization`, `native-optimization3` | **PASS 539**; includes exact/bounded native checks, constants, fractional time bits, compact codecs, hierarchy/skin, dense terminal retention, cancellation/budgets and four-branch recovery. |
| Native4 `-NativeExportStructure`, `structure4` | **PASS 178**; includes corrected byte ownership and terminal/overlap regressions. |
| Native3 `-WorkspaceSonicFourStages`, `sequence3` | **PASS 1,906**; four requested exports, same-master search control, exact authoring Save/reopen and immutable original inputs. |
| Native1 `layout1` diagnostic | **PASS 165**, diagnostic only; predates final compact text and is excluded from current functional/visual acceptance. |
| Native3 `-WorkspaceAnimationOptimization`, `standalone3` | **PASS 196**; 12 individually inspected compact/wide 100/175% default/details/Advanced captures. Current focus/cancel/settings, explicit export and unchanged-master checks pass. The bounded idle candidate would grow, so the smaller verified Exact result is retained: 10,820 → 10,356 bytes. |
| Native3 combined `-WorkspaceExportOptimization`, `export3` | **EXCLUDED**: the unchanged 10-minute parent watchdog expired during the second, nonselected Fall preparation. Partial assertions are not complete acceptance. `lifecycle3` was not run after that queue stopped. |
| Native4 `-WorkspaceExportCurrent`, `export-current4` | **PASS 286**; explicit Current segment, including all 24 single/set dialog captures at compact/wide 100/125/150/175%, publication guards, contact fallback, slots/PAC and exact master Save/resave/reopen. Requires the Nonselected segment below for complete export-case coverage. |
| Native4 `-WorkspaceExportNonselected`, `export-nonselected4` | **PASS 21**; complementary own-root/explicit bounded Fall request, ordinary/target-free reference, stale/corrupt and selected-state checks. Both export segments pass; neither alone claims complete coverage. |
| Native4 `-ProjectLifecycle`, `lifecycle4` | **PASS 37**; six inspected lifecycle captures, body edit, keep editing, failed Save, reopen and discard. |
| Native4 `-WorkspaceExportDirtyModel`, `dirty4` | **PASS 54**; pending model changes reject before hashing/publication; exact ordinary Save and original inputs remain protected. |
| Native4 `-WorkspaceOptimizationSharedContainer`, `shared4` | **PASS 23**; shared ownership, two unsaved animations, untouched backing/siblings and independent export. Its one inspected preview is texture-free as explained below. |
| Native4 reciprocal `-WorkspaceSonicPipeline`, `reverse-channel4` | **PASS 1,860**; Mario AttackAirLw → Sonic, SourceChannelTimes and Fall-first fitting. Bounded request explicitly retains Exact for unqualified non-unit scale/scale-compensation paths. Two mapping PNGs inspected; no new reciprocal GIF or bounded-runtime claim. |
| Core optimization/fitting/contact regression queues | **PASS 179 / 401 / 15** in fresh `core-optimization2`, `core-fitting` and `core-contacts` receipts. These are separately scoped synthetic/core assertions, not universal contact acceptance. |
| Native3 final side-view GIFs | **PASS**: both 88-frame GIFs actually inspected through all 22 decoded sheets, plus full-size endpoint/Fall-first snapshots. Four-stage SHA `59F11D80E4BFD5FD204E0E850D125EB2B2F1E1A56C975392D959882F6AB91C3D`; fitted-master/optimized SHA `15BC76763F1D45677DDAAE869A760AF4F9B69FF66D9F23A9132BC895C62DD377`. |
| Actual native UI image review | **50 fresh PNGs** inspected: 12 standalone, 24 export, six lifecycle, one shared, two reciprocal mapping and five four-stage native/mapping captures. Hashes and observations are in `ui3-visual-inspection.json` and the sequence's `native-ui-visual-review.json`. |

The script cases above run separately through `scripts/Test-BackgroundCapture.ps1`
with the frozen local fixture aliases and the selected native directory. The
script admits one serialized private-desktop harness; it injects no OS input and
captures through the shared offscreen renderer. Run selections and local fixture
hashes are retained in `accepted-run-summary.json`. The 10 admitted native runs
record 5,100 assertions; the three Core runs record 595. Old live-Apply/obsolete-mode layout
helpers are historical and do not validate the current export-copy dialog.

The expanded 175% set-export list uses its vertical scrollbar to reveal the clip
row below two metadata lines; primary fields/actions remain in bounds. The
shared ownership fixture renders matching white geometry because it contains
one model and two animations, **no texture group**. Its bytes are identical to
the earlier native19 fixture (289,536 bytes, SHA-256
`133C1D5B240E9E941A001F42B79B720BF432ED009DB9F0699DE26452B172792D`).
Independent ARC/BRRES dictionaries prove the authentic Mario PAC's textures live
in sibling archives omitted by this fixture. This case does not qualify textured
shared-container appearance; authentic full-PAC standalone/stage previews remain
separately textured. The original CHR0-only Sonic UI capture retains its auxiliary
sphere; the side inspection mask is explicitly separate from asset semantics.

Actual build/run commands, using local frozen fixture aliases and fresh outputs:

```powershell
& $MSBuild BrawlCrate/BrawlCrate.csproj /restore /p:Configuration=Release `
  /p:Platform=x86 /p:BuildProjectReferences=false "/p:OutDir=$Native3/" /nologo /v:minimal
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore `
  /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false `
  "/p:OutDir=$Native3/" /nologo /v:minimal
# native4 copies native3, rebuilds only the harness, and verifies all 913
# application/runtime files retain their exact hashes.
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore `
  /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false `
  "/p:OutDir=$Native4/" /nologo /v:minimal
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native3 -Model $MarioModel `
  -Motion $SonicClip -RetargetSource $SonicModel -TargetMotion $MarioMotion `
  -WorkspaceSonicFourStages -OutputDirectory $Sequence
# Each case uses a separate invocation/output, serialized below normal priority.
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native4 -Model $MarioModel `
  -Motion $MarioMotion -WorkspaceExportCurrent -OutputDirectory $Current
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native4 -Model $MarioModel `
  -Motion $MarioMotion -WorkspaceExportNonselected -OutputDirectory $Nonselected
# Other actual switches: NativeAnimationOptimization, NativeExportStructure,
# WorkspaceAnimationOptimization, ProjectLifecycle, WorkspaceExportDirtyModel,
# WorkspaceOptimizationSharedContainer. Reciprocal additionally supplies the
# frozen library/target-motion inputs, SourceChannelTimes, FitOnly and Reciprocal.
Push-Location engine
& $Dotnet build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore
& $Dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $CoreOpt --animation-optimization
& $Dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $CoreFit --boundary-fitting
& $Dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $CoreContacts --contact-authoring
Pop-Location
```

Stopped `sequence1` is excluded from passing evidence: it hit the real dense
allocation assumption fixed above. Stopped `sequence2` is also excluded: its
oracle incorrectly required the pre-fit XRotN UseModel flags after the explicitly
enabled reference-control fitting operation authored those groups. The corrected
oracle permits only the reviewed selected-group authoring changes, retains all
compensation flags and attack-owned translation, checks exact middle tuples and
independently evaluates both native and UseModel-effective middle transforms.
No solver, codec tolerance, root ownership, protection or contact gate was relaxed.

The combined export case already approached its deadline in the preceding
accepted revision. The replacement Current/Nonselected selectors explicitly
declare partial coverage and retain every original assertion across their union;
the default still runs the original complete case. Each segment keeps the same
10-minute watchdog and all numeric, operation-budget and protection gates. Their
receipts must both pass; one segment alone is not complete export qualification.

Final GIFs contain 87 half-frame samples from native display frame 1 through 44
and one separately labelled frozen Fall-first context pose, with attack travel
retained. The 30/40 ms sample delays total 2.90 seconds; the final reference hold
adds 1.20 seconds. The inherited quarter-speed caption is nominal for an assumed
60 FPS inspection rate, not CHR0 metadata or a game timing test. Camera and gold
Y=0 guide remain fixed. The source-only auxiliary SphereM capture mask is copied
from the exact previously reviewed model identity; it does not edit an asset or
export. At the inspected raster scale no additional compression jump/twist is
apparent, and final fitted/optimized poses match the appended first-Fall pose.
The guide is not reviewed support geometry or proof of absolute foot contact.

Two restricted Core build attempts stopped without compiler diagnostics. The
same Release build passed with normal toolchain/registry access; its existing
Avalonia AVA3001 private-API warning remains recorded. The resulting test runner
then passed the three scoped Core runs above. No source or test expectation was
changed for those build retries.

## Remaining scope

This increment increases selective repair coverage; it does not implement a
constrained Hermite tangent fitter or a group-cost-optimal reduction algorithm.
The current reducer mostly removes knots while retaining original endpoint
slopes. A next bounded rework should propose refitted **generated, unprotected**
spans and compare actual S/R/T-group encoded costs, while reusing the existing
same-master scalar certificates, immutable encoding plans, real serialization,
fresh decode and complete native quality checks. Fractional timing, full authored
turns and exact boundaries cannot be rounded away to force a compact codec.

The declared corpus includes sparse/absent channels, nonzero-tangent constants,
paired/discontinuous/signed-zero data, loops/terminals, full turns, negative and
nonuniform scale, hierarchy-amplified error, complete influences and safe
cancel/stale/budget fallback. More fixtures do not prove all possible models or
animations. Unknown protections, unqualified absolute contacts, unsupported
runtime scale/compensation semantics or invalid target context retain
Exact/reference output. The current export/Core adapter's existing qualification
limits are unchanged. Retarget authoring artifacts must be assessed separately
from compression against that authoring master.

Physical input, monitor DPI, human usability, new external-editor/plugin round
trips, universal contacts, Melee writing and actual game acceptance remain
unverified. Source-only delivery uses public `DoctorFlux/Fighterport/main`
with DoctorFlux author/committer attribution and contributor verification.
The launcher promotion receipt selects the exact native4 executable; publication
metadata is recorded separately after the commit/push. Assets, settings, binaries,
captures and local reports remain excluded from publication.
