# CHR0 four-stage benchmark — 28 October 2026

The fresh default-Next sequence exports Sonic's original AttackAirLw at **12,016
bytes**, Mario's full-quality retarget at **306,324 bytes**, its Fall-fitted master
at **235,344 bytes**, and a separately verified optimized copy at **57,684 bytes**.
Optimization saves **177,660 bytes / 75.4895% against that same fitted master**.
It does not reach the donor's size. This is measured bounded search, not a claim
of the smallest mathematically possible CHR0 or acceptance in Brawl.

This record belongs to preview.55 within the sole incomplete G1/M2. It supplements
the [previous source-key benchmark](NATIVE_CHR0_OPTIMIZATION_BENCHMARK_2026-10-28.md)
and [write-path audit](CHR0_WRITE_PATH_AUDIT_2026-10-28.md). Those older files use
different target authoring schedules and are not same-master compression trials.

## Revision, recipe and immutable inputs

Starting product commit: `11d6c1d708e3254d1c68101dfcee70b0c9970214`. The fresh
`Fighterport-stages55/native3` build records application SHA-256
`3c5a2045fa2d0e97053d9efb793f35ecaba58d32c299776eada5194f940e6b1e`.
Its owned BrawlLib SHA-256 is
`199cdbce8bbd288cfcae211f8e6c4b471ee6600e116afbebe0992e2dac8f00e9`.
The build receipt identifies unchanged engine/backend artifacts separately from
the changed workspace, accounting and bounded search composition code.
Primary upstream revision pins remain in the linked write-path audit; no new
external editor/runtime compatibility was established by this experiment.

| Fixture | SHA-256 |
| --- | --- |
| Original Sonic AttackAirLw, 44 frames | `1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4` |
| Sonic model container | `51e9a8b4f51911dc378ab612934f547339b1e557f04d82290c76fcba1350bad1` |
| Mario model container | `e73cc6f7c00cebe5587bda20bd7484d9dc49fb32ffddb5663c0284fc16fe62a6` |
| Mario motion/reference container | `afa97c15732854f8798a93bc52bdc833cd020cd4711d8ecf68228175f4fc3288` |

The original Sonic resource is opened on Sonic and exported through the current
native workflow. Its export is byte-identical to the immutable original,
including stored dense terminal data. The target stage uses **Original Next's
default dense result**, not either source-key alternative. Fit start/end applies
Mario Fall's frozen **first pose only**, with display ranges 1–5 and 35–44. The
original attack middle from display frame 5 through 35 remains unchanged. Native
curve times are 0–43; mandatory endpoint/join times are 0, 4, 34 and 43.

The existing reviewed boundary policy retains the attack's position-control
travel and permits reference rotation-control fitting. Export optimization then
preserves the resulting frozen motion-control channels exactly. The fit fixture
retains its authentic use-model-transform flags; the middle oracle evaluates
them instead of assuming every stored S/R/T channel overrides the model.
Its 378 native witnesses report zero world-position/basis differences across the
protected middle. This is not an absolute foot-contact or clearance claim.

The editable fitted animation is the optimization reference. It retains original
Next corrections, fitting data and all authentic hierarchy/bind/skin data.
Neither the donor nor a previous lossy export becomes its baseline. Ordinary
project Save/reopen preserves each full-quality master. Preparation/publication
preserves the live clip, target/bone selection, frame, camera and native Undo.

## Actual standalone bytes

All values are physical bytes. The resource-size field excludes some standalone
names; it is not the physical file length. Logical keys are native decoded keys,
including projected dense pairs/isotropic axes. Physical keys count stored
scalar rows/samples once. The format column counts **varying S/R/T groups**;
group-wide encoding is distinct from physical scalar stream counts.

| Stage | File bytes | Resource bytes | Native logical keys | Physical stored keys | Bone entries | Varying groups |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Sonic native export | 12,016 | 11,125 | 7,120 | 4,528 | 66 | L1 × 28; I4 × 26; I12 × 3 |
| Mario default Next master | 306,324 | 305,548 | 28,318 | 24,886 | 60 | I12 × 177 |
| Mario Fall-fitted master | 235,344 | 234,544 | 21,540 | 18,942 | 62 | I12 × 186 |
| Mario optimized export | 57,684 | 57,068 | 4,797 | 4,723 | 48 | I4 × 55; I12 × 46; I6 × 1 |

| Output | SHA-256 |
| --- | --- |
| Sonic native export | `1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4` |
| Mario default Next master | `2179cbe08a40d64d19938b0ebdf94b4c715b76dcd5b581443098091f0d6dba2e` |
| Mario fitted master | `3e7ab2dbdb80818d6dae073be29715da8912282a128c991c7b54c3eab5612dfa` |
| Optimized export, both efforts | `fbde75a7779858a635257aa7f28d5b0411480a2b4e45e9a0adb1587f5a0c6659` |

The Sonic source, encoded as the existing precise-I12 reference for the **same
Sonic motion**, measures 87,600 bytes. Its retained compact native data is 12,016
bytes. This isolates representation cost without comparing different rigs' poses.
The optimized Mario candidate's precise-I12 representation is 60,960 bytes;
verified compact storage saves a further 3,276 bytes to reach 57,684. Most savings
against its finished master therefore come from curve/constant/bind cleanup, not
from admitting the donor's dense codec for different target curves.

| Physical category | Sonic export | Mario Next master | Mario fitted master | Mario optimized |
| --- | ---: | ---: | ---: | ---: |
| CHR0 header | 40 | 40 | 40 | 40 |
| Resource dictionary | 1,080 | 984 | 1,016 | 792 |
| Per-bone headers | 528 | 480 | 496 | 384 |
| Animated scalar offset slots | 592 | 1,812 | 1,896 | 1,092 |
| Inline fixed-value slots | 1,076 | 12 | 0 | 412 |
| Curve codec headers | 1,656 | 3,624 | 3,792 | 3,272 |
| Stored scalar rows/samples | 5,904 | 298,596 | 227,304 | 51,076 |
| Names/string prefixes | 784 | 690 | 709 | 549 |
| Proved zero alignment | 356 | 86 | 91 | 67 |
| User metadata / unclassified | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| **Physical total** | **12,016** | **306,324** | **235,344** | **57,684** |

No PAC/BRRES size change or outer archive compression was measured by this
standalone experiment. The small metadata/alignment share cannot explain a
remaining tens-of-kilobytes difference.

## What still occupies the file

Against the exact exported Mario skeleton's 63 members, the retarget, fitted and
optimized outputs have **zero absent-target tracks**. Twenty original Sonic
tracks, including tail/quill members, are absent from Mario and do not leak into
those outputs. The optimizer additionally removes 14 proved eligible exact-bind
target tracks. Unweighted target helpers are not discarded based on skin weight.

The final scalar streams and slots occupy 50,148 bytes for rotation, 3,912 for
translation and 1,792 for scale. Rotations alone account for **86.94% of the
complete file**. There are 126 I12 rotation streams across 42 groups. Their
retained values/slopes, fractional times, paired sides and cumulative hierarchy
effects are the principal remaining cost, rather than donor-only bones.

The candidate retains 1,632 nonidentical-slope continuous corners: 1,436 have
frozen generated-site evidence and 196 remain protected/unproven. Equal values
on both sides do not make unequal incoming/outgoing tangents redundant. There
are no identical-sided pairs in this default-Next result. Complete identical
payload sharing has only a **1,632-byte unrealized upper bound**; this experiment
implements no cross-stream relocation/deduplication writer.

Six retained rotation groups contain off-I6-grid times. Rounding them to I6's
1/32-frame grid makes distinct adjacent times collide in **all six groups**.
Three left-leg groups also require slopes beyond I6's signed tangent range;
observed maxima are approximately 2,548, 5,687 and 3,473 degrees/frame. Merely
snapping times, wrapping Euler values, or clamping slopes is not an acceptable
compression change. The independent native diagnostic around the sharpest
span observes real local/world orientation change, not just a ±360° chart label.

A useful later search experiment is constrained Hermite fitting of eligible
generated body spans/corners, with exact root/pin/contact/join exclusions and
fresh same-master continuous plus cumulative native validation. Timing collisions
require a genuine span fit rather than blind rounding. This record offers no
candidate or promised savings for that unimplemented experiment. The current
search's verified output is the 57,684-byte file above.

## Frozen quality and actual search

Requested/effective policy: **ErrorBounded**. Limits remain 0.1° raw Euler/local
orientation, 0.002 native units translation, 0.0001 scale, and 0.001 × the target's
bind-geometry diagonal for native world/basis and skin position. Here
`L = 23.777803865350322`, giving 0.023777803865350323 native units. Derivative
limits per frame are 0.1°, 0.002 and 0.0001 respectively. Fitting boundaries and
source-owned controls override approximation and remain exact. No tolerances
were raised and no skin influences were removed.

| Same finished master, current export | Value |
| --- | ---: |
| Exact cleanup candidate | 7,997 logical keys / 96,168 bytes |
| Selected bounded scalar profile | 0.125 / 4,797 logical keys / 57,684 bytes |
| Other verified scalar profile | 0.25 / 5,993 logical keys / 72,120 bytes |
| Attempted 0.5 profile | Shared Core proof-work budget exhausted; not offered |
| Thorough preparation time | 63,585 ms |
| Standard preparation time | 58,958 ms |

Both efforts return the **same bytes and hash** for this fixture. Thorough's
new allowance of up to eight selective repairs, versus Standard's two, uses the
same aggregate proof/native ceilings and saved quality limits. Its separate
multi-branch regression qualifies the repair behavior; it does not establish
further real-clip savings here. Search effort never promises a smaller result.

The selected output reports rational Hermite scalar value/derivative certificates
and 9,272 native witnesses covering integers, quarters, withheld fractions,
original/candidate knots, paired sides, loop/terminal boundaries, mandatory joins
and bounded adaptive midpoints. All 63 bones and all skin influences participate.
Maximum native errors are:

| Measurement | Maximum observed |
| --- | ---: |
| Raw Euler / local orientation | 0.0120429993° / 0.0140224411° |
| Local translation / scale | 0.00000489666350 / 0 |
| World position/basis / skin displacement | 0.00730349203 / 0.000698474369 |
| World basis angular difference | 0.0175986969° |
| Euler / orientation derivative per frame | 0.0123767853° / 0.0123617108° |
| Translation derivative per frame | 0.00000391207267 |
| World / skin derivative chord per frame | 0.00760961281 / 0.00122323490 |

A separate 132-time withheld native check covers 63 bones and 3,913 vertices;
its maximum world/skin differences are 0.00719855457 / 0.000695199261, with local
orientation 0.0140183801°. These composed-transform/skin checks are sampled;
they do not prove continuous geometry/contact acceptance. Exact fitting endpoints,
joins, motion controls, metadata, version, 44-frame duration and non-looping
terminal behavior have separate gates.

## Fresh evidence and limitations

Local ignored evidence is under `artifacts/Fighterport-stages55/`:

- `sequence3/`: private-desktop four-stage workflow, 1,906 passing assertions;
  independent candidates, exports and ordinary full-quality Save/reopen checks.
- `native-optimization3/`: 539 assertions, including separate bounded-repair,
  paired-key, codec, terminal-data and preserved-source regressions.
- `structure2/`: 178 assertions, including actual unaligned retained L1 payloads,
  truncated header/terminal rejection and overlapping/nonzero-gap accounting.
- `byte-audit-sequence3/`: independent big-endian offset/codec parser and a
  separate read-only native reader check for the original plus five exports.
  Every physical byte is assigned once; tuples, flags/metadata and sampled
  scalar bits agree, with zero unknown nonzero bytes and no exhausted audit
  sample budgets. Each optimized/master comparison uses 55,884 independent
  stored-channel samples; absent/bind channels use the application's rig gate.
- `model-membership-sequence3.json`, `byte-contributors-sequence3.json` and
  `snap-frontier-sequence3.json`: read-only membership, physical contributions
  and explicitly hypothetical grid diagnostics. The last file is not acceptance.

The independent artifact audit was freshly compiled with native2's reader,
whose BrawlLib bytes match native3 exactly. Decoder SHA-256:
`b074b804f5f42fdc4057b82189d7752acbb124c37b86485b4e8bc4f2f03c3ede`.
Summary SHA-256:
`db30660409406c9a1e312b9f8ab992a090a775ba4b4448824f9330fa8cec4c45`.
The audit uses no animation writer, input injection, screen capture or renderer;
its shared background mutex serializes it with native captures. Source files
remain byte-identical.

Product commands exercised use `scripts/Test-BackgroundCapture.ps1` with the
fresh `-NativeDirectory`, `-WorkspaceSonicFourStages`,
`-NativeAnimationOptimization` and `-NativeExportStructure` cases. The independent
artifact tool runs `Build-Audit.ps1` and `Run-Audit.ps1` with explicit qualified
native reader and frozen local manifest. Membership/contributor tools read only
the produced resources/reports. No private source paths or assets are published.

This four-stage case qualifies Brawl CHR0 v4 on these exact model dependencies.
It does not qualify all costumes, all animations, Melee writing, external
BrawlBox/BrawlCrate versions/plugins, physical input, monitor DPI, human usability,
absolute ground/ledge/contact quality or actual game execution. The fit explicitly
has no reviewed floor/contact solver context. Existing contact gates and Exact
fallbacks remain intact; native reopen is not game acceptance.
