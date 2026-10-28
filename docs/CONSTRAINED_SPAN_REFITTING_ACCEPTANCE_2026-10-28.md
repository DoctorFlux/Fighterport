# Constrained span and tangent refitting — preview.56 acceptance

**Bounded source acceptance: 5,763 recorded checks in 13 final admitted runs,
50 inspected UI PNGs and 316 inspected decoded GIF frames.** Starting commit:
`ba51c74ad0a4a73f9a52ef669913af6c4e328910`.
This completes the bounded increment within the ONE incomplete G1/M2. The package
identifies as `2.0.0-preview.56-constrained-span-refitting`, file version
`2.0.0.32`. The launcher selects `Fighterport-span56/native2`;
`Start-FighterPort.ps1 -Check` resolves its verified application without opening
a visible window. Source publication is the final delivery step; exact
commit/package verification belongs to the delivery receipts.

The objective is a smaller validated native export of the **same finished target
master**, including retargeting, fitting and edits. Original Next, the complete
BrawlCrate Model Previewer, root-motion ownership, authentic binds/hierarchy,
all skin coefficients and native Undo remain. Ordinary Save keeps the exact
editable master. Optimization prepares an independent copy; it does not turn a
previous lossy export into the next compression reference. No project-format
migration or extra user-facing optimization mode is introduced.

See the [current plan](plans/current.md), [existing export boundary](ENGINE_INTEGRATION.md)
and [current benchmark](SPAN_REFITTING_BENCHMARK_2026-10-28.md). The
[preceding benchmark](CHR0_FOUR_STAGE_BENCHMARK_2026-10-28.md)'s
57,684-byte output is a comparison only when the frozen finished master, target,
recipe, protections and quality settings match exactly.

## Implemented proposal and proof

`AnimationOptimizationSettings.EnableSpanRefitting` defaults to **false** for
existing internal callers. The shared native export boundary enables it during
explicit Error-bounded approximate preparation. Exact never refits tangents or
reserves work for this search. The visible Exact/Error-bounded policies and
collapsed Advanced settings stay unchanged.

The generic scalar algorithm has no character, action, filename or bone-role
hardcodes. Exact target identities and reviewed protections enter as data.

1. Run the existing deletion-only reducer and retain its independent
   `PreRefitTracks` candidate if refitting subsequently succeeds.
2. Consider bounded windows of at most 64 distinct current knot-time groups.
   Keep endpoint times and values. Propose secant slopes, normalized
   value/derivative least-squares slopes, blends and existing endpoint slopes.
   Round proposed slopes to native float32 before testing.
3. Require a reduction in native scalar-row cost, accounting for two native
   rows when incoming/outgoing endpoint slopes differ. New split slopes are
   emitted as same-time, single-slope key pairs compatible with the native wire.
4. Certify every changed polynomial against the immutable original curve, using
   rational Bernstein value and derivative bounds over original subintervals.
   Least-squares samples choose proposals; they never establish acceptance.
5. For a known, continuous generated corner, optionally retain its time/value
   and share one balanced slope. Both adjoining original polynomials must pass.
   An unknown pair or actual value jump does not acquire this permission.

Outside endpoint slopes remain unchanged. Only genuinely unused duplicate-time
sides or nonlooping held-exterior sides may share the newly fitted slope.
First incoming/last outgoing loop sides remain intact. No knot-time snapping,
Euler wrapping, shortest-path quaternion substitution, bind normalization or
mesh reweighting occurs.

Both alternatives contribute extrema and near-key witness times. The native
coordinator independently serializes, reopens, certifies and measures complete
candidates against the original master, then chooses actual bytes. Fewer scalar
rows are only a proposal heuristic: they do not guarantee smaller encoding.
The verified deletion-only alternative remains eligible when refitting fails
native quality, requires larger storage or exhausts a later proof budget.

## Limits, protections and cancellation

Existing maximum Error-bounded ceilings remain:

| Measurement | Ceiling |
| --- | ---: |
| Raw Euler / native local orientation | 0.1° |
| Native local translation | 0.002 native units |
| Local scale components | 0.0001 |
| Native world/basis and skin displacement | 0.001 × reviewed bind diagonal `L` |
| Euler derivative | 0.1° per native frame |
| Translation / scale derivative | 0.002 / 0.0001 per native frame |

Saved tighter limits remain tighter. Source-owned motion controls, authored
pin/contact/attachment dependencies, strict fitting joins and protected ranges
override approximation. Signed-zero evidence, unknown paired sides, authored
split sides and discontinuities remain protected. Singular or sign-changing
scale makes the complete bone exact-only. Missing scale/protection/runtime
qualification retains Exact/reference behavior; scalar refitting does not
qualify previously unsupported scale-compensation or absolute-contact semantics.

Generation and decoded-codec scalar certificates share the existing operation
allowance, at most two million work units and 100,000 candidate spans. With
refitting eligible, deletion uses up to 60% of requested work/candidates; refitting
stops at 80% of requested total work, leaving the final 20% for caller codec
certificates. Candidate spans retain the same global cap. Both phase scopes reset
in `finally`. `LegacyPhaseLimited` and `RefitWorkLimited` disclose stopped search.
Exact, disabled and ineligible operations keep their previous full-budget path.

This allocation does not guarantee an identical deletion-only result to an older
full-budget search on every input. Every offered result must still satisfy real
size caps and full validation; a budget stop cannot publish an incomplete proof.
There is no global-optimality or fixed-percentage compression claim.

Working arrays hold immutable key records; accepted coefficient changes allocate
new records. The legacy snapshot cannot be mutated by later fitting. Graph
updates occur only after complete interval proofs. Charged search operations
check cancellation, and cancellation returns no export candidate. No loading,
solving, hashing or refitting occurs on playback ticks.

## Admitted evidence and measured outputs

Local evidence root: `artifacts/Fighterport-span56`. The final
`accepted-run-summary.json` records ten native runs / 5,130 assertions and three
Core runs / 633 assertions, totaling **5,763 checks / 13 runs**. Earlier Core214
and Native1 records below are historical steps and are excluded from that total.

| Evidence | Actual status and scope |
| --- | --- |
| `core-reserve2/results.json` | **PASS 214** after the 80% generation ceiling: 179 existing Core checks plus 35 refitting checks. |
| `core-shared-spans3/results.json` | **PASS 217**, including three further adjacent-span/native-row/loop-wrap checks. This supersedes the 214 count for the final Core corpus. |
| Native1 `native-optimization1/receipt.json` | **PASS 551**, private desktop, no OS input injection or active-screen capture. This predates the final native paired-side guard changes and generation-reserve package; it is not final Native2 acceptance. |
| Native1 `span-refit-integration/native-span-refit-evidence.json` | **PASS**, actual worker wire → Core → native writer/reopen/resave proof of concept, detailed below. |
| Native2 `native-optimization2/receipt.json` | **PASS 568**: existing native corpus, actual worker/refit/encode/reopen proof and 17 new typed paired-side guard assertions. |
| Native2 Sonic AttackAirLw → Mario, `forward2/receipt.json` | **PASS 1,907**: default Next, Fall-first fitting and explicit Thorough refitting preserve full masters/Undo/weights/root protections. Same fitted master 235,344 bytes; legacy alternative 57,684 bytes; selected refit alternative 56,808 bytes. |
| Native2 Mario AttackAirLw → Sonic, `reverse2/receipt.json` | **PASS 1,860**: same 126,600-byte fitted master; 112,652-byte export remains byte-identical to preview.55 through explicit Exact scale-compensation fallback. No new reciprocal bounded-runtime admission. |
| Native2 `structure2` | **PASS 178** native resource/codec/actual-byte accounting checks. |
| Native2 `standalone2`, `export-current2`, `export-nonselected2` | **PASS 196 / 286 / 21**; current and nonselected explicit optimizer/export requests, failed/cancelled/stale work, native slots/PAC siblings and full master Save/resave/reopen. |
| Native2 `dirty2`, `shared2`, `lifecycle2` | **PASS 54 / 23 / 37**; dirty-model publication guards, unsaved shared ownership and normal body edit/New/Save/Close/Undo behavior. |
| `core-fitting2`, `core-contact2` | **PASS 401 / 15** unchanged fitting/contact contracts; these do not establish universal contact quality. |
| Actual native UI review | **PASS 50 fresh PNGs**, individually opened at original resolution with exact hashes, dimensions and review times recorded in `ui-visual-review.json`. All six corresponding scoped native receipts pass. |
| Side GIFs | **PASS 316 decoded frames / 40 inspected sheets**: two forward GIFs of 88 frames each and two reverse GIFs of 70 frames each, with matching-hash inspection receipts and full-size endpoint/reference snapshots. UI screenshot review remains separately recorded. |
| Independent serialized audit | **PASS eight files**: 620,762 scalar comparisons and 24,559 outgoing-side comparisons, zero decoder differences, no comparison exhaustion. This is raw/native decoding agreement, not a claim of zero compression deviation from the master. |
| Launcher/public source/contributors | **Launcher PASS** through `Start-FighterPort.ps1 -Check`, application SHA `1216DF4D3F450A653FD17C48D673CAF83B5CDDC54AF337D5C4991604596FFC05`. Source publication is the final delivery step to public DoctorFlux/Fighterport/main with DoctorFlux attribution and contributor verification. |

The forward refitted file saves **876 bytes / 1.5186%** beyond its same-master
57,684-byte legacy alternative, or **178,536 bytes / 75.8617%** against the
235,344-byte full-quality fitted master. Decoded scalar keys fall from 4,797 to
4,724. The reverse result has no new bounded admission or refitting saving:
its verified **112,652-byte Exact export** remains unchanged. A generic algorithm
does not make every native transform context qualified.

The independent analytic Core fixture contains 17 unit-spaced keys with values
`t` and slopes `0.98f`. Its unit-interval value deviation from the line is
`(1 − 0.98f) × sqrt(3) / 18`; derivative deviation is `1 − 0.98f`. Retaining
those endpoint slopes across the entire span exceeds the frozen 0.005° fixture
value bound, while a refitted line is feasible. Tests independently compare
expanded double polynomials, continuous certificates, withheld samples and
native row costs. Further checks cover fractional times, sparse channels,
unknown/generated pairs, held interpolation, exact fractional protections,
720° winding, loops/terminals, signed zeros, stable/mirrored/singular scale,
rename/storage permutation, budgets, cancellation and immutable sources.

The Native1 proof of concept uses an authentic Mario model with a **procedural
curve on an unweighted leaf** and a separately protected source-motion control.
It is not an authentic attack or deforming-limb compression benchmark:

| Same procedural master | Deletion-only | Refitted |
| --- | ---: | ---: |
| Standalone native bytes | 1,884 | 1,800 |
| Native scalar keys | 170 | 163 |

One Core refit span changes four surviving tangent fields. Fresh native export
and resave retain the verified result. Root curves and the model/all skin-weight
data remain exact. The report records 1,845 native validation samples; maximum
local orientation error is approximately 0.001874°, with zero translation/scale
error. Measured skin displacement is zero because the edited leaf is unweighted;
this cannot establish quality for moving weighted limbs. Scalar/native quality
and actual bytes remain separately reported.

`RefitCandidateSpans`, `AcceptedRefitSpans` and `RefitTangents` describe Core
proposals and scalar-certified graph changes. They do **not** mean that the final
published native output selected those changes. Native rejection, byte cost and
safe fallback must be recorded alongside these metrics.

## Visual inspection and source

The 50 inspected UI captures comprise 12 standalone default/Advanced/details
states, 24 single/set export dialogs, six lifecycle states, one shared-container
preview, five forward native-editor/mapping images and two reverse mapping
dialogs. Primary actions and fields remain readable and inside their windows;
textured Mario previews show matching posed geometry. At 175% text, the expanded
set-export list uses its vertical scrollbar to reveal the clip row below two
metadata lines. The details report also deliberately scrolls. Lifecycle images
show the disabled wrong-extension Save action, contained failed-save message and
a dotted Keep editing focus rectangle.

The shared preview is white because its fixture omits the sibling texture
archives; this qualifies geometry/ownership rather than textured appearance.
The original CHR0-only Sonic editor view retains its auxiliary sphere; any final
GIF mask must remain separately declared. These disk-image observations do not
verify physical input, monitor DPI, human usability, arbitrary poses or contacts.
The image-review receipt SHA-256 is
`0ABF26712717F2ED38F7D72A480C64D3A26517D6FBEA67FF5B402B3BA26ED22C`.

All four GIFs use the shared native offscreen renderer, full orthographic +X side
view and a gold Y=0 inspection guide. They retain the attack's translation and
append only one separately labelled first-Fall reference pose; reference
animation playback is not substituted. Forward has 87 half-frame samples plus
that reference frame; reverse has 69 plus the reference. All 316 decoded frames
were actually inspected through 40 sheets. Matching-hash review receipts separate
the four-pane pipeline context from the two-pane same-master compression view.
At that raster scale no added compression jump or twist was apparent. The guide
is not reviewed contact geometry, and nominal inspection cadence is not a game
timing test. Exact capture masks and model/animation identities stay recorded.

| GIF | SHA-256 |
| --- | --- |
| Forward fitted-master/optimized | `02B0258E5563F6D8BBE012846360140ED78D547D31C6D37F92EF9E90665D38C1` |
| Forward pipeline | `638589D4D8E9B0C30F80B274B9CB0C146C96C323A9B269F9811EB3E0BA121400` |
| Reverse fitted-master/optimized | `C6C81FA22FB5321DAAD0C44DC012A36897BD829008263702CA9272C3CD263887` |
| Reverse pipeline | `741BD740EDDCAC9F125BD235E1F8B7B4E94A687CCD50F85A9DAE813EC0AA6AD5` |

Implementation files are the Core `AnimationOptimization.cs` and new
`AnimationOptimization.Refitting.cs`; EngineHost `OptimizationSession.cs`;
native `NativeAnimationOptimization.cs` / `.Reduction.cs`; and ModelForm's
`.GeneratedOptimization.cs`, `.AnimationOptimization.cs` and
`.ExportOptimization.cs`. The application project changes preview metadata.
Tests are `SpanRefittingChecks.cs`, its existing Core suite hook,
`NativeSpanRefitIntegrationChecks.cs`, `NativeOptimizationAcceptance.cs`,
`WorkspaceSonicFourStages.cs`, `WorkspaceSonicPipeline.cs` and the background
capture script. Their fixture names are regression identities, not algorithm
exceptions. This record and the sole current plan track evidence.

## Actual build and test command templates

Aliases below identify the frozen local toolchain, native2 package, private
fixture files and fresh evidence folders. Their hashes, exact output identities
and logs remain in the local build receipts and benchmark; no private asset path
is part of this source record. Native2 rebuilds the application, harness and owned
EngineHost/Core; its BrawlLib bytes remain the preceding qualified revision.

```powershell
& $MSBuild BrawlCrate/BrawlCrate.csproj /restore /p:Configuration=Release `
  /p:Platform=x86 /p:BuildProjectReferences=false "/p:OutDir=$Native/" /nologo /v:minimal
& $MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore `
  /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false `
  "/p:OutDir=$Native/" /nologo /v:minimal
& scripts/Build-Engine.ps1 -DotNet $Dotnet -NativeDirectory $Native
Push-Location engine
& $Dotnet build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore
& $Dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $Core --animation-optimization
& $Dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $Fit --boundary-fitting
& $Dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $Contacts --contact-authoring
Pop-Location
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native -Model $MarioModel `
  -Motion $SonicClip -RetargetSource $SonicModel -TargetMotion $MarioMotion `
  -WorkspaceSonicFourStages -SpanRefitRequired -OutputDirectory $Forward
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $Native -Model $SonicModel `
  -Motion $MarioClip35 -ProvidedMario $MarioClip35 -TargetMotion $SonicMotion `
  -LibraryIndex $VerifiedLibrary -WorkspaceSonicPipeline -SonicPipelineReciprocal `
  -SonicPipelineMode SourceChannelTimes -SonicPipelineFitOnly -SonicPipelineThorough `
  -OutputDirectory $Reverse
# Other cases ran separately, using MarioModel/MarioMotion and fresh outputs:
# NativeAnimationOptimization, NativeExportStructure, WorkspaceAnimationOptimization,
# WorkspaceExportCurrent, WorkspaceExportNonselected, WorkspaceExportDirtyModel,
# WorkspaceOptimizationSharedContainer, ProjectLifecycle.
```

All native/capture work stays serialized, below normal priority and on private
desktops; no OS input injection or active-desktop capture is claimed. Continuous
scalar proofs, sampled native geometry and independently decoded physical bytes
remain distinct evidence. Exact roots/all original weights, immutable sources,
the full-quality master, native Undo and Save/reopen stay separate from export
size. Unqualified contact/scale contexts retain their documented fallback.

This bounded increment is accepted; the broader G1/M2 remains incomplete. Final
source/launcher delivery receipts record publication rather than extending these
quality claims. Public delivery excludes game assets, settings, captures and binaries.
Physical input, monitor DPI, human usability, universal contacts, Melee,
external-editor/plugin round trips and game acceptance remain unverified.
