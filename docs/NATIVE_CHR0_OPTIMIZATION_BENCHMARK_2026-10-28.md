# Native CHR0 optimization benchmark — 28 October 2026

The final15 background runs retain an editable, full-quality animation master and
export a separately verified smaller copy. For the Sonic-to-Mario fitted attack,
the two source-key schedules produce **53,700-byte** and **59,640-byte** bounded
exports. They do not match the 12,016-byte Sonic donor. Reciprocal Mario-to-Sonic
requests use the explicitly reported Exact fallback: the bounded scale and
scale-compensation combination is not qualified for that target.

This is a bounded measurement record within the one incomplete G1/M2 milestone.
See the [acceptance record](NATIVE_CHR0_EXPORT_OPTIMIZATION_ACCEPTANCE_2026-10-28.md)
for the complete application verification and the
[CHR0 write-path audit](CHR0_WRITE_PATH_AUDIT_2026-10-28.md) for publication routes
and pinned primary references. These results do not establish a compression ratio
for other animations, a globally smallest encoding, universal contacts, Melee
support or execution in Brawl.

## Reference, fixtures and method

Each optimization comparison starts from the **same finished target master**.
Raw retarget and fitted masters have separate snapshots, content identities and
hashes. Exact and error-bounded exports are independently prepared from their
respective master; an already approximated export is never the next baseline.
The original Next pose solver, authentic target hierarchy/binds and every original
skin coefficient are retained. No four-influence truncation or reweighting is used.

The source-key modes seed either each bone's source knot schedule
(`SourceBoneTimes`) or each channel's schedule (`SourceChannelTimes`). They do not
promise a final one-to-one key count: target-space motion and native Euler/Hermite
evaluation can require additional, fractional knots. Tracks not belonging to the
target are excluded by reviewed target membership, not by names such as “tail.”

| Fixture identity | Role | Standalone bytes | SHA-256 |
| --- | --- | ---: | --- |
| Sonic AttackAirLw, 44 frames | Authentic donor/context for the forward cases | 12,016 | `1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4` |
| Mario35 AttackAirLw, 35 frames | Codec-preserved reciprocal fixture | 114,128 | `78a20b168d7035441e51cfa924d67eb07c45dd7fe776fbd05cdeb3313a349205` |
| Mario target model container | Exact forward model dependency | — | `e73cc6f7c00cebe5587bda20bd7484d9dc49fb32ffddb5663c0284fc16fe62a6` |
| Sonic target model container | Exact reciprocal model dependency | — | `51e9a8b4f51911dc378ab612934f547339b1e557f04d82290c76fcba1350bad1` |

Mario35 is a distinct derived test fixture. Its 114,128-byte size is not presented
as the size of a retail Mario file, and it is not silently substituted for a
different original. The Sonic donor is a different rig's animation: its compact
size is useful context, not the compression baseline for Mario's finished motion.
The table identifies model containers; exact rig fingerprints are also frozen by
the application. Private model, motion, catalogue and source-path data are not
distributed with this source record.

Both directions retarget AttackAirLw, fit its start/end to the target's **first
Fall pose only**, then prepare Exact and requested error-bounded exports. The
entry join is display frame 5; the exit joins are display frame 35 of 44 and
frame 26 of 35. The normal native first display frame is 1, corresponding to
curve time 0. The attack's own position-control travel remains authoritative;
Fall does not replace that travel. Protected rotation-control boundary handling
and body fitting retain their existing reviewed policies.

## Actual standalone sizes

All sizes below are exact bytes, not rounded file-manager KB. “Resource” is the
declared CHR0 block; “file” includes the complete standalone encoding and external
names. Key counts are native decoded logical keys. The format column counts
**varying S/R/T groups**, not physical channels, keys or byte allocations. Fixed
inline values are not counted as varying groups. Full per-channel physical
accounting is recorded separately by the independent decoder.

“Bounded” means requested and effective `ErrorBounded`; “requested bounded →
Exact” means the actual verified file is the Exact fallback. Master rows are
retained editable references, not export candidates.

### Sonic → Mario, source channel times

| Stage | File bytes | Resource bytes | Keys | Tracks | Varying groups |
| --- | ---: | ---: | ---: | ---: | --- |
| Raw master | 125,468 | 124,948 | 10,379 | 40 | I12 × 40 |
| Raw Exact | 115,424 | 114,916 | 9,543 | 39 | I12 × 40 |
| Raw bounded | 61,436 | 60,928 | 5,044 | 39 | I12 × 40 |
| Fitted master | 108,308 | 107,788 | 8,738 | 40 | I12 × 117 |
| Fitted Exact | 93,864 | 93,356 | 7,763 | 39 | I12 × 43; I4 × 41 |
| Fitted bounded | 53,700 | 53,180 | 4,422 | 40 | I12 × 42; I4 × 41; I6 × 1 |

### Sonic → Mario, source bone times

| Stage | File bytes | Resource bytes | Keys | Tracks | Varying groups |
| --- | ---: | ---: | ---: | ---: | --- |
| Raw master | 183,372 | 182,852 | 17,550 | 40 | I12 × 69 |
| Raw Exact | 115,844 | 115,336 | 9,578 | 39 | I12 × 40 |
| Raw bounded | 46,828 | 46,320 | 5,084 | 39 | I6 × 30; I12 × 10 |
| Fitted master | 147,536 | 147,016 | 13,701 | 40 | I12 × 117 |
| Fitted Exact | 99,732 | 99,224 | 8,698 | 39 | I12 × 43; I4 × 41 |
| Fitted bounded | 59,640 | 59,120 | 5,363 | 40 | I12 × 42; I4 × 41; I6 × 1 |

### Mario35 → Sonic, source channel times

| Stage | File bytes | Resource bytes | Keys | Tracks | Varying groups |
| --- | ---: | ---: | ---: | ---: | --- |
| Raw master | 148,748 | 148,164 | 12,358 | 45 | I12 × 55 |
| Raw Exact | 141,272 | 140,712 | 11,707 | 43 | I12 × 45 |
| Raw requested bounded → Exact | 141,272 | 140,712 | 11,707 | 43 | I12 × 45 |
| Fitted master | 126,600 | 126,016 | 10,257 | 45 | I12 × 135 |
| Fitted Exact | 112,652 | 112,092 | 9,304 | 43 | I12 × 52; I4 × 40; I6 × 2 |
| Fitted requested bounded → Exact | 112,652 | 112,092 | 9,304 | 43 | I12 × 52; I4 × 40; I6 × 2 |

### Mario35 → Sonic, source bone times

| Stage | File bytes | Resource bytes | Keys | Tracks | Varying groups |
| --- | ---: | ---: | ---: | ---: | --- |
| Raw master | 258,256 | 257,672 | 25,536 | 45 | I12 × 120 |
| Raw Exact | 141,716 | 141,156 | 11,744 | 43 | I12 × 45 |
| Raw requested bounded → Exact | 141,716 | 141,156 | 11,744 | 43 | I12 × 45 |
| Fitted master | 211,848 | 211,264 | 18,537 | 45 | I12 × 135 |
| Fitted Exact | 130,364 | 129,804 | 12,922 | 43 | I12 × 52; I4 × 40; I6 × 2 |
| Fitted requested bounded → Exact | 130,364 | 129,804 | 12,922 | 43 | I12 × 52; I4 × 40; I6 × 2 |

The reciprocal fallback reason is: **non-unit bind/animation scale and
scale-compensation flags share a hierarchy path; bounded runtime world/skin
acceptance is unqualified**. The request is reported, the effective policy remains
Exact, and no flags, skin weights or quality limits are changed to admit it.

For direct identity checks, the fitted master/export hashes are:

| Case | Fitted master SHA-256 | Requested bounded export SHA-256 |
| --- | --- | --- |
| Forward channel | `72b6667b034b8e6d7bc1a21ef90dfb79aa0fd61c372855dbbf95d7c95b8c6393` | `86d7e999d065627158c3c7f7dd330563e7d0a2e9964a84b4a1aacb086adcdd90` |
| Forward bone | `42385e7eb5f15bfaa71d5d2ead46bf4cd74ae3ea546f5abeb7d4f9b92c058452` | `df7db0a98ad3df960fb040b251e8eb966ce43784cc54a043c6ad3b5a5b62386e` |
| Reciprocal channel | `59d84bd9eb7b9dff7bdecba830454516e95758fd4d83afc01b3148fe2eccf43f` | `b9eaf85481e65358b89100f9071fb68d556ad03761c1933ccf558f87463ebf45` |
| Reciprocal bone | `f574e99849c1e43089a2d2b9899d01f5b9523e802c52bf0fae9af67c89d57719` | `8b902ed2fd3cc14920348c7e7242094a9868b1cfd352fe5b52dd147b2e0795f8` |

## Quality measurements and work

The frozen error-bounded export limits are 0.1° for raw Euler channels and local
orientation, 0.002 native units for local translation and 0.0001 for scale. Native
bone-position/basis and skinned-vertex checks use 0.001 × the original target bind
geometry diagonal `L`; the forward target has `L = 23.777803865…`. Derivative
limits per native frame are 0.1° for rotation, 0.002 for translation and 0.0001
for scale. Motion controls, authored pin/contact dependencies and mandatory fitting
joins remain exact even when the export request is bounded.

Continuous scalar Hermite value/derivative certificates use rational bounds at
original and candidate breakpoints. Native hierarchy, orientation and mesh checks
are separately sampled; they are not a continuous mesh-error or contact proof.
Sampling covers native integers, fractional training/withheld witnesses, knot
sides, adjacent floating values, loop boundaries and required fitting joins.
Every native bone and all original skin coefficients participate. Frame-time,
Euler full turns, paired-key sides, signed zeros, metadata and flags are checked
separately from orientation.

These are observed maxima against each **same finished master**. Values are native
units unless marked degrees; the world column is the reported bone-position/basis
measure. Scale error was 0 in all four bounded forward exports.

| Effective bounded export | Raw Euler ° | Local orientation ° | Local translation | World/basis | Skin position | World angular ° |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Forward channel, raw | 0.0117111206 | 0.0140581157 | 0.00000369732012 | 0.00782053418 | 0.000800938657 | 0.0188445768 |
| Forward bone, raw | 0.0127161741 | 0.0135706264 | 0.00000357705470 | 0.00870377399 | 0.000904692644 | 0.0209728613 |
| Forward channel, fitted | 0.0246058702 | 0.0284013020 | 0.00000489666350 | 0.0142882210 | 0.00291091649 | 0.0344292327 |
| Forward bone, fitted | 0.0244064331 | 0.0284013020 | 0.00000489678979 | 0.0142904449 | 0.00291091649 | 0.0344345947 |

| Effective bounded export | Euler derivative °/frame | Orientation derivative °/frame | Translation derivative/frame | World derivative/frame | Skin derivative/frame |
| --- | ---: | ---: | ---: | ---: | ---: |
| Forward channel, raw | 0.0123767853 | 0.0171301918 | 0.00000554701818 | 0.00790319512 | 0.00148748905 |
| Forward bone, raw | 0.0122998357 | 0.0180114531 | 0.00000554888989 | 0.00905714125 | 0.00164308298 |
| Forward channel, fitted | 0.0249184562 | 0.0309116267 | 0.00000397934526 | 0.0231302567 | 0.00213226673 |
| Forward bone, fitted | 0.0249184562 | 0.0303805352 | 0.00000397934526 | 0.0215993119 | 0.00206602625 |

All Exact outputs, including reciprocal fallbacks, report zero sampled pose/mesh
differences at their native witnesses. A reciprocal raw Exact derivative comparison
reports numerical Euler roundoff of approximately `3.09e-13` degrees/frame.
Mathematical scalar equivalence is distinct from a claim of bit-identical native
floating evaluation at every possible real time.

Preparation times below are elapsed milliseconds from a single local run,
including native validation. They are not repeated performance trials or latency
guarantees. The sample count is the report's native validation sample count, not
the number of image frames or all internal work evaluations.

| Case/stage | Exact ms | Exact samples | Requested bounded ms | Requested bounded samples | Effective bounded policy |
| --- | ---: | ---: | ---: | ---: | --- |
| Forward channel, raw | 10,107 | 4,394 | 131,731 | 11,382 | ErrorBounded |
| Forward channel, fitted | 9,021 | 4,105 | 100,040 | 17,853 | ErrorBounded |
| Forward bone, raw | 9,759 | 4,333 | 140,540 | 11,150 | ErrorBounded |
| Forward bone, fitted | 8,864 | 4,075 | 95,488 | 17,141 | ErrorBounded |
| Reciprocal channel, raw | 20,974 | 8,073 | 21,594 | 8,073 | Exact |
| Reciprocal channel, fitted | 19,942 | 7,298 | 19,580 | 7,298 | Exact |
| Reciprocal bone, raw | 22,332 | 8,073 | 22,372 | 8,073 | Exact |
| Reciprocal bone, fitted | 20,085 | 7,298 | 19,279 | 7,298 | Exact |

For example, the forward channel raw bounded preparation records 89,844 logical
evaluations, 158,730 actual pose evaluations, 9,999,990 bone visits and 621,110,490
skin visits. A later proposal exhausted the ten-million-bone budget; the operation
retained its earlier fully verified smaller candidate. The fitted counterpart
records 58,411 logical evaluations, 104,044 pose evaluations, 6,554,772 bone visits
and 407,124,172 skin visits. Exhaustion never publishes a partly validated proposal.

Master-to-Next fidelity is a **different comparison** from all export errors above.
The independent native11 solved-motion observers were not rerun as final15
observers. Their baked source-channel and dense files are, however, independently
verified byte-for-byte identical to the corresponding final15 raw masters. The
four file hashes and original observer-report hashes are recorded in the local
`final15-master-observer-hash-equality` receipt. This permits the following measured
motion results to identify those unchanged masters without claiming a new run:

| Master direction | Integer samples / maximum world orientation ° | Training samples / maximum ° | Withheld samples / maximum ° |
| --- | --- | --- | --- |
| Sonic → Mario | 44 / 0.0000472920474 | 301 / 0.0246059085 | 301 / 0.0242656802 |
| Mario35 → Sonic | 35 / 0.0000260645782 | 238 / 0.0231516379 | 238 / 0.0229260022 |

The dense and source-channel observer outputs share these measured maxima; this
table does not substitute their results for an unmeasured source-bone observer.
The training/withheld times, every-bone observations and exact source/recipe
identities remain in their separate reports. Do not add these maxima to export
maxima and report the sum as a measured end-to-end result.

## Where the bytes go

The 53,700-byte forward channel fitted bounded file has this complete independent
physical allocation. Shared/isotropic payloads are counted once.

| Physical owner | Bytes |
| --- | ---: |
| Curve key/sample payloads | 48,244 |
| Codec headers | 2,648 |
| Bone entry headers/slots | 1,584 |
| Resource dictionary | 664 |
| Names/string storage | 461 |
| User metadata | 0 |
| CHR0 header | 40 |
| Proved zero alignment | 59 |
| Unclassified | 0 |
| **Complete standalone file** | **53,700** |

The declared resource is 53,180 bytes. Physical channels are 125 I12, 100 I4,
3 I6 and 88 fixed cells, whereas the group summary is 42 I12, 41 I4 and one I6.
Neither count can replace the byte allocation. PAC/BRRES archive alignment,
compression and sibling resources can change the container saving; resource
bytes alone do not establish it.

The donor's compact storage is different: its 84 physical L1 channels use 3,780
sample bytes plus 672 header bytes; 59 I4 channels use 1,812 key bytes plus
944 header bytes; five I12 channels use 312 key bytes plus 40 header bytes. It
also has 269 fixed cells. Sparse source curves transformed to another hierarchy,
fitted boundary data and accurate fractional Euler motion need not preserve those
codec choices or that size.

The old forward channel bounded experiment measured 93,660 bytes. The final
53,700-byte file removes 3,330 I12 rows, accounting for the full 39,960-byte
reduction, while its 108,308-byte fitted master remains unchanged. This follows
actual writer-created pair evidence rather than treating imported equal-valued
pairs as disposable. The raw preparation had 4,534 known generated sites; fitting
retained 3,411 unchanged known sites, with 196 mandatory sites excluded from the
3,215 eligible export sites. Unknown and protected pairs retain their contract.

The final forward fitted bounded file still contains 617 fractional key rows,
including 193 off the I6 1/32-frame grid; its minimum time gap is 1/4096 frame.
Exact protected controls/joins and tangent representability also constrain codec
selection. A historically smaller 31,032-byte result used a different, less
faithful finished master and is not an admissible compression baseline here.
There is no measured proof that the current output is globally minimal.

## Native codec fixtures

Let `K` be the number of physically stored indexed rows and `N` the native frame
count. Paired sides are separate indexed rows; dense samples can reconstruct
paired slope sides without storing two sample values. The formulas below describe
curve payload plans, before the rest of the CHR0 file. Generated plans retain
required alignment; imported retained dense extents are audited at their actual
physical boundaries, rather than assuming all original payloads were aligned.

| Storage | Curve bytes | Time/value/tangent boundary |
| --- | --- | --- |
| Fixed | No animated payload; inline table cell is 4 bytes | Exact stored float value |
| I4 | `16 + 4K` | Integer frame index 0–255; packed value and signed tangent fields |
| I6 | `align4(16 + 6K)` for a generated plan | Signed S10.5 time; 16-bit value and signed tangent fields |
| I12 | `8 + 12K` | Binary32 time, value and tangent |
| L1 | `align4(8 + (N+1))` for a generated plan | Dense rotation samples, 8-bit value codes |
| L2 | `align4(8 + 2(N+1))` for a generated plan | Dense rotation samples, 16-bit value codes |
| L4 | `4(N+1)` | Retained dense float samples; no new L4 proposal in this increment |

Actual passed procedural examples:

| Fixture | Curve result | Complete standalone result |
| --- | --- | --- |
| Sixteen exactly representable indexed knots, I12 → I4 | 200 → 80 bytes | 400 → 280 bytes |
| Sixteen integer knots through frame 300, exact I6 | 112 bytes | 312 bytes; no separately written I12 whole-file baseline claimed |
| Four fractional knots at 0, 0.5, 1.125 and 4.5, I12 → I6 | 56 → 40 bytes | 244 → 228 bytes |
| Five-frame v4 non-looping dense rotation, I12 → L1 | 16-byte L1 plan | 312 → 224 bytes |
| Same five-frame dense rotation, I12 → L2 | 20-byte L2 plan | 312 → 228 bytes |

The fractional I6 fixture verifies 1,025 fractional native witnesses and an exact
packed-file resave. Its active times lie on the 1/32-frame grid. Literal raw
decoder fixtures establish `0x7fff / 32 = 1023.96875`, `0x8000 / 32 = -1024`
and `0xffe0 / 32 = -1`. Negative active times are rejected by the current native
authoring contract; 1024 cannot be encoded as positive I6 time. A non-grid time
`0.1f` retains I12 and its exact `0x3dcccccd` time word. Values beyond 1023.96875
must not wrap into signed-negative I6 positions, and 2047/2048 are not valid
positive I6 edge cases. I12 remains available for supported finite times outside
the packed grid. Synthetic fixture preparation timings were not measured.

Dense verification retains all `N+1` physical samples, including the terminal
sample of a non-looping resource. Ordinary non-looping native playback and an
explicit closing-frame inspection are separate evaluation contexts. No terminal
sample is blindly deleted to reduce size.

## Independent decode, application and visual evidence

The final independent audit reads 32 files: eight inputs for each of the four
cases, including repeated donor context and the finished master/copies. It performs
its own big-endian structural decode and physical byte assignment before comparing
every decoded time/value/tangent tuple, metadata and raw flag word against the
qualified native reader. It also compares scalar sampling on fractional and
paired-side witnesses. It does not use the native writer to manufacture expected
bytes or claim that the native reader is an independent geometry oracle.

All 32 files passed structural, tuple, metadata, flag and scalar agreement; every
byte was assigned, no unclassified nonzero bytes remained and no scalar budget
was exhausted. The reports record 2,345,488 scalar checks and 151,694 outgoing-side
checks separately, with 16 same-finished-master export comparisons. Continuous
scalar proofs and the application hierarchy/skin gates remain separate evidence.

The four authentic application workflows pass 1,982, 1,982, 1,860 and 1,860 checks
(7,684 total). The native structure, optimizer and motion-PAC gates are listed in
the acceptance record. Save/reopen, literal codec storage, failed/stale operations,
native Undo, protected controls and untouched sibling resources have distinct
tests; reopening a resource by type/frame count alone is insufficient.

Eight fresh GIF streams show the four master/export comparisons and four optional
source-context views. They contain 632 decoded image frames, directly inspected
through 80 contact sheets with unchanged GIF/sheet hashes. There are two samples
per native frame, an orthographic full side view and a gold native-Y-zero ground
guide; playback is explicitly quarter speed. Each stream appends only one frozen
first-Fall pose. Attack root travel is retained. Shared sole/guide crossings are
observations, not accepted ground contacts. No added export-only spin, snap or dip
was observed in these images; visual inspection does not prove the numeric limits.
Testing used private desktops/shared offscreen rendering, without active-desktop
capture or OS input injection. Physical input, mixed-monitor DPI and human
usability were not exercised.

## State, compatibility and reproduction

Retargeting/fitting and ordinary project Save retain the full-quality master.
Size optimization is an explicit export-copy operation. It preserves current
target/clip/bone selection, frame, camera, live content and native Undo. The report
records the frozen master and export identities, actual bytes, effective policy
and validation before publication. Failed validation, stale dependencies, failed
publication or a size increase cannot replace the master or publish an unverified
copy. Existing retarget/fitting hashes remain historical evidence and are not
rewritten to describe an optimized export.

Uncommitted edits to the native model itself require ordinary Save before
animation export/Analyze. The early and stale-dependency guards reject them
before model serialization so its dirty state cannot be cleared incidentally.
This is separate from unsaved animation edits, which remain supported.
When the model and animations share a BRRES/PAC, preview copies existing raw
backing into independent owners rather than rebuilding the live container.
The reopened model must still match its frozen identity/hash. Pending render
resources or stale backing require Save; dirty CHR0 siblings stay untouched.

Generated-pair evidence is bounded, session-only and tied to exact native content
and rig fingerprints. It originates at the writer, and fitting carries only
unchanged matching tuples. Manual changes invalidate the lookup; exact Undo can
restore a previous lookup, and same-process exact copies/reopen can retain it.
A fresh process does not infer that evidence from imported pairs. Unknown pairs,
signed-zero distinctions and exact protection overrides are preserved. No project
format migration or persistent approximation baseline is added. Unsupported
partially authored in-memory native channel groups are not assumed to roundtrip:
a present S/R/T group stores all three axes, and complete snapshot/reopen checks
must pass before export.

Fractional native key times now use one authoritative floating time in the owned
native collection. Integer inputs retain their ordinary APIs; native graph keys,
Save/export and sampling retain fractional times. The shared viewport clock still
uses normal integer editing frames, so selecting a fractional graph key can show
the nearest ordinary viewport frame. CHR0 legacy curve transforms that cannot
preserve fractional inputs reject before mutation. This does not qualify other
resource formats: SRT0 Resize/Append still traverse integer frames, and the
legacy SHP0 reader still casts key times to integers. Their fractional workflows
remain outside this increment. The changed native key-field
ABI requires rebuilding owned app/worker references; old third-party binary
editor plugins are not qualified by these tests.

The evidence uses the Release .NET Framework 4.7.2/x86 native build, owned engine
SDK 10.0.402 and artifact-only tools built with SDK 9.0.300, MSBuild
17.14.10.27608 and C# compiler 4.1400.25.22906. Receipt identities include:

| Component | SHA-256 |
| --- | --- |
| Native application | `9b2147fb524e0d8f39b2d1868afef996764f4b1ee7e087e979760a883ac2f67a` |
| Native BrawlLib reader/evaluator | `199cdbce8bbd288cfcae211f8e6c4b471ee6600e116afbebe0992e2dac8f00e9` |
| Independent structural decoder source | `36aa6b41a0db41c031d6d86f2b22e5608491bfdacc245a2068c213e591db83d3` |

The final application safety build is native19, file version `2.0.0.30`, product
`2.0.0-preview.54-native-chr0-export-optimization`, SHA-256
`e57d3d706012e08b816b1979673120ed021dff59efc60c620946c167b20379d2`.
The benchmark/media rows remain their recorded native15 executions. A separate
read-only PE/metadata comparison proves all 911 backend/runtime files identical
and 9,177 unchanged application methods. Its 28 changed/added methods are the
explicitly reviewed model-dirty, review/slot-status, raw-preview and exact-owner paths;
their fresh focused/UI tests are recorded separately in the acceptance record.
The comparison resolves tokens and a documented bijective compiler-ordinal
correspondence, preserving strict native layouts, constants, IL/locals/EH and
resources. Only version fields, three compiler-generated AutoLayout capture-field
declaration order changes and the exact new predicate cache field are admitted.
Seven negative canaries pass. This is unchanged-method/backend evidence plus
separate safety qualification, not a claim that every application path is
behaviorally identical or that old runs executed native19.

The pinned runtime reference is NW4R decompilation revision
`e5a4d369011062fe243aa63f6c2e2853854c395d`; the BrawlCrate reference revision is
`a4c1ee3385010d5c64a88df9c56267c61f213321`. Links and the distinction between that
other title's runtime code and actual Brawl execution are in the write-path audit.
No Wii floating-point identity or in-game acceptance is inferred from the pins.

The commands below show the recorded harness route using generic local variables.
They require the private fixture inputs above and their verified catalogue,
reviewed mappings and exact target reference motions; this repository does not
distribute them or assume they exist on a CI machine. Use fresh output directories
and serialize all private-desktop/offscreen jobs through the shared capture mutex.

```powershell
# From the active Fighterport checkout; paths are caller-supplied variables.
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $QualifiedNative `
  -Model $MarioModel -Motion $SonicAttackAirLw -TargetMotion $MarioMotion `
  -ProvidedMario $Mario35Fixture -LibraryIndex $PrivateCatalogue `
  -WorkspaceSonicPipeline -SonicPipelineFitOnly `
  -SonicPipelineMode SourceChannelTimes -OutputDirectory $ForwardChannelEvidence

# Repeat with SourceBoneTimes and a fresh output directory.
# Reciprocal runs select the recorded Mario35 source from the frozen catalogue.
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $QualifiedNative `
  -Model $SonicModel -Motion $Mario35Fixture -TargetMotion $SonicMotion `
  -ProvidedMario $Mario35Fixture -LibraryIndex $PrivateCatalogue `
  -WorkspaceSonicPipeline -SonicPipelineReciprocal -SonicPipelineFitOnly `
  -SonicPipelineMode SourceChannelTimes -OutputDirectory $ReciprocalChannelEvidence

& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $QualifiedNative `
  -Model $TargetModel -Motion $NativeClip -NativeExportStructure `
  -OutputDirectory $StructureEvidence
& scripts/Test-BackgroundCapture.ps1 -NativeDirectory $QualifiedNative `
  -Model $TargetModel -Motion $NativeClip -NativeAnimationOptimization `
  -OutputDirectory $OptimizationEvidence
```

The local evidence bundle additionally snapshots the independent decoder and side
renderer source, compiler/native hashes and frozen file manifests. Those artifact
tools are not represented as public checkout scripts or binary releases. Given
that bundle, the recorded invocation contract is:

```powershell
& "$AuditBundle/Build-Audit.ps1" -QualifiedNative $QualifiedNative
& "$AuditBundle/Run-Audit.ps1" -QualifiedNative $QualifiedNative `
  -Manifest $FrozenAuditManifest -OutputDirectory $FreshAuditEvidence
& "$RendererBundle/Build-SideComparison.ps1" -QualifiedNative $QualifiedNative
& "$RendererBundle/Run-SideComparison.ps1" -QualifiedNative $QualifiedNative `
  -Manifest $FrozenMediaManifest -OutputDirectory $FreshMediaEvidence
```

The scripts reject stale source/native hashes, reused outputs or an occupied
capture mutex. Rendered files alone do not mark visual inspection complete; the
actual decoded-sheet reviews and matching hashes are required. Local evidence
labels are `final15-workflow-file-metadata`, `byte-audit-final15` and
`final15-visual-inspection`. Game assets, paths/settings, captures and generated
binaries remain excluded from public source publication.
