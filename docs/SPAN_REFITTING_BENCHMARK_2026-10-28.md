# Constrained span/tangent refitting — preview.56 benchmark

The forward fitted Mario export is **56,808 bytes**, compared with the independently
verified deletion-only alternative's **57,684 bytes** for the **same 235,344-byte
finished master**. The refitted copy saves another **876 bytes / 1.5186%**, or
**178,536 bytes / 75.8617%** against that master. Its decoded key count falls from
4,797 to 4,724. This is a measured bounded-search result, not a global minimum or
a promise that other animations achieve the same ratio.

The reciprocal Mario → Sonic fitted export remains **112,652 bytes**, identical
to its preceding verified Exact result. Its existing non-unit scale/scale-compensation
boundary prevents bounded runtime admission. Scalar refitting does not qualify
those transform semantics or silently disable their protection.

This benchmark belongs to the sole incomplete G1/M2 increment. The implementation
starts at `ba51c74ad0a4a73f9a52ef669913af6c4e328910`. See the
[current plan](plans/current.md), [export architecture](ENGINE_INTEGRATION.md),
[write-path audit](CHR0_WRITE_PATH_AUDIT_2026-10-28.md) and
[preceding same-master benchmark](CHR0_FOUR_STAGE_BENCHMARK_2026-10-28.md).
Fresh UI and GIF review is complete: 50 native PNGs and all 316 decoded frames
from four GIFs, reviewed across 40 sheets with hash-bound receipts. Physical-input
and broader runtime acceptance remain separate from this bounded qualification.

## Frozen implementation and fixtures

The native2 package records these SHA-256 values:

| Component | SHA-256 |
| --- | --- |
| Application | `1216df4d3f450a653fd17c48d673caf83b5cddc54af337d5c4991604596ffc05` |
| Engine host | `e7c5bd2944dcb88098805a19d4501e006b4ee578e1b1bcd43debce80cac1718f` |
| Core | `bb4afb4b46a80c90e5cb7a570348950897e18eb3eea65de6858d52c8d96f723c` |
| Owned BrawlLib, unchanged | `199cdbce8bbd288cfcae211f8e6c4b471ee6600e116afbebe0992e2dac8f00e9` |

The original Next retargeter, native renderer and complete influence coefficients
remain in use. Refitting changes independent export curves, not the MDL0 hierarchy,
rest/inverse binds, weights or authoring master. Ordinary project Save retains the
full-quality result and historical fitting/retargeting evidence.

| Immutable fixture | SHA-256 |
| --- | --- |
| Sonic AttackAirLw, 44 frames | `1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4` |
| Sonic model container | `51e9a8b4f51911dc378ab612934f547339b1e557f04d82290c76fcba1350bad1` |
| Forward Mario model container | `e73cc6f7c00cebe5587bda20bd7484d9dc49fb32ffddb5663c0284fc16fe62a6` |
| Forward Mario reference-motion container | `afa97c15732854f8798a93bc52bdc833cd020cd4711d8ecf68228175f4fc3288` |
| Reciprocal Mario AttackAirLw, 35 frames | `78a20b168d7035441e51cfa924d67eb07c45dd7fe776fbd05cdeb3313a349205` |
| Reciprocal Mario source container | `d4937cac8986f60440afdc14ed90c212a68d20a7bfe092d97a87b8dc20a82963` |
| Reciprocal frozen Sonic Fall resource | `54eb0c20d704b4717ab6e0b63d82e9c4694231e40bbdb225da76158b0bab75f9` |

The reciprocal Mario source container is separately identified; it is not silently
substituted for the forward model container. The exact standalone native model
snapshots hash to `6a90b79f2be0f57f43bba748e468a84bb9fbd69eeeccd159c838dd0056aa8463`
for Mario and `8e3b45214051868e6df9bea10c9864ddc6f01b2bf4bbe737f3b9f0a1ec4e6123`
for Sonic. Original file hashes are checked again by the background workflows.
Private input paths, assets and generated binaries are not published.

Forward authoring uses default Original Next Dense and 44 native frames. Fitting
uses Mario Fall's frozen first pose at both ends, joins at native display frames
5 and 35, and mandatory native times 0, 4, 34 and 43. The before/after fit oracle
records 378 native/quarter/withheld middle witnesses with zero world-position and
basis-component differences. Reference orientation controls can be authored by
that existing fitting recipe; attack-owned translation remains retained.

Reciprocal authoring uses the authentic 35-frame Mario clip and SourceChannelTimes,
then Sonic Fall's first pose with display joins 5 and 26. Its mandatory times are
0, 4, 25 and 34. No additional Fall playback is folded into either finished clip.
Export optimization uses each finished target as its baseline, never the donor
or a previous lossy export. Source controls and required fitting boundaries remain
exact under the recorded target/protection dependencies.

## Forward physical sizes and selected candidate

All byte lengths are physical standalone CHR0 files. A resource-size field excludes
some external names and is shown separately. Decoded key counts include native
paired/isotropic projection; they are not physical compressed row counts.

| Forward stage | File bytes | Resource bytes | Decoded scalar keys | Tracks |
| --- | ---: | ---: | ---: | ---: |
| Sonic native re-export, context only | 12,016 | 11,125 | 7,120 | 66 |
| Mario default Next master | 306,324 | 305,548 | 28,318 | 60 |
| Mario Fall-fitted full-quality master | 235,344 | 234,544 | 21,540 | 62 |
| Verified Exact cleanup alternative | 96,168 | — | 7,997 | — |
| Same-master deletion-only alternative | 57,684 | 57,068 | 4,797 | 48 |
| **Selected span/tangent refit export** | **56,808** | **56,192** | **4,724** | **48** |

The Sonic source re-exports byte-identically; the raw and fitted Mario masters are
also byte-identical to preview.55. That makes the latter deletion-only/refit
comparison a like-for-like compression trial. The donor's compact 12 KB storage
is useful context, but its different skeleton/motion is not the quality baseline.
The source's same-motion precise-I12 representation remains 87,600 bytes.

| Forward output | SHA-256 |
| --- | --- |
| Default Next master | `2179cbe08a40d64d19938b0ebdf94b4c715b76dcd5b581443098091f0d6dba2e` |
| Fitted master | `3e7ab2dbdb80818d6dae073be29715da8912282a128c991c7b54c3eab5612dfa` |
| Deletion-only / preview.55 export | `fbde75a7779858a635257aa7f28d5b0411480a2b4e45e9a0adb1587f5a0c6659` |
| Selected refit, both search efforts | `10e79a28a96f1bc9494428076f84bcefda83b412441896e9f747e717c9d64a1d` |

The report explicitly identifies **“span/tangent refit proposal”** as the selected
native curve alternative after both alternatives complete fresh native validation
and actual-byte comparison. The winning 0.125 construction reports 16,661 refit
proposals, 58 scalar-certified refit spans and 146 changed incoming/outgoing tangent
fields. These are Core construction counters, not independently additive savings
or a count of every published physical slope field. The final native result is
separately established by its decoded bytes and same-master quality checks.

| Accounted physical category | Selected bytes |
| --- | ---: |
| Curve scalar payload | 50,200 |
| Curve codec headers | 3,272 |
| Bone headers and value/offset slots | 1,888 |
| Dictionary / resource header | 792 / 40 |
| Names / proved alignment | 549 / 67 |
| User metadata / unclassified | 0 / 0 |
| **Total** | **56,808** |

The new precise-I12 representation is 60,084 bytes; qualified compact storage
saves another 3,276 bytes. Varying S/R/T group formats remain I4 × 55, I12 × 46
and I6 × 1. Physical stream counts are I4 × 133, I12 × 137, I6 × 3 and 103 inline
fixed slots. Scalar-stream formats must not be confused with group-wide choices.

A separate bounded, read-only big-endian raw comparison uses no native loader.
It observes 33 changed rotation I12 streams, 73 removed stored I12 sides and no
added sides. At shared time/side addresses, zero value bits and 73 tangent fields
change. Semantic flags and frozen exact-control raw representations remain the
same. Varying physical rows fall from 4,620 to 4,547; adding 103 fixed slots gives
4,723 to 4,650 physical stored items, distinct from decoded 4,797 to 4,724 keys.
I12 stream headers/payloads total 49,936 → 49,060 bytes; I4/I6 totals stay
4,256 / 156. The entire 876-byte change is curve payload; other accounted
categories remain unchanged. This diagnostic is explicitly **not a full independent
file/runtime audit** and does not itself validate pose or skin quality.

## Same limits, bounded search and native quality

Saved limits remain raw Euler/local orientation **0.1°**, translation **0.002
native units**, scale **0.0001**, and world/basis/skin displacement **0.001 × L**.
`L` is the frozen positive bind-geometry diagonal: 23.777803865350322 for Mario,
so the geometry limit is 0.023777803865350323 native units. Per-frame scalar
derivative limits remain 0.1°, 0.002 and 0.0001. Construction precision can tighten
proposals but never expand those saved total allowances. Exact roots, pins,
contact/attachment dependencies, mandatory joins/ranges and signed-zero evidence
override approximate permissions; native flags, winding and timing remain.

The enabled search reserves the first 60% of each Core request's remaining work
allowance for deletion proposals, permits refitting only up to 80%, and leaves
the final 20% for decoded codec certification. The coordinator retains the same
**two-million aggregate scalar work** and **100,000 candidate-span** ceilings
across requests/retries. Phase exhaustion retains fully proved candidates; it is
not permission to publish an incomplete result or run during playback.

| Forward candidate | Actual outcome |
| --- | --- |
| Exact | Verified, 96,168 bytes |
| Precision 0.125 | Selected refit 56,808 bytes; deletion alternative independently verified at 57,684 |
| Precision 0.25 | Verified 138,220 bytes / 16,902 decoded keys; zero accepted refits; legacy/refit phase limits reported |
| Precision 0.5 | Verified 139,552 bytes / 17,382 keys; zero accepted refits; phase limit reported |
| Precision 1 | Shared scalar proof budget exhausted; not offered |
| Thorough preparation | 200,366 ms outer measured wall time; internal report 200,155 ms |
| Standard preparation | 139,692 ms outer measured wall time; internal report 139,638 ms |

Both efforts produce the identical selected file. More effort does not promise a
smaller file. The new search takes longer than the preceding deletion-only trial;
these are local background measurements, not interactive latency guarantees.

The winning candidate has **9,961 native witnesses** over integer/quarter frames,
original/candidate knots, mandatory and loop/terminal boundaries, withheld fractions
and bounded adaptive midpoints. The operation totals 42,829 logical witnesses /
74,398 actual baseline/candidate pose evaluations, 4,687,074 bone visits and
291,119,374 skin-vertex visits, below the unchanged 100,000 / 10,000,000 /
1,000,000,000 native caps. All 63 bones and all 3,913 skinned vertices participate.

| Forward maximum measured error | Value |
| --- | ---: |
| Raw Euler / local orientation | 0.0120429993° / 0.0140224411° |
| Local translation / scale | 0.00000489666350 / 0 |
| World position/basis / skin displacement | 0.00735118981 / 0.00121382480 |
| World basis angular difference | 0.0177137558° |
| Euler / orientation derivative per frame | 0.0124999993° / 0.0154858627° |
| Translation / scale derivative per frame | 0.00000391207267 / 0 |
| World / skin derivative chord per frame | 0.0100948052 / 0.00141808844 |

A separate 132-time withheld check covers the same 63 bones/3,913 vertices. Its
maxima are Euler 0.0120429993°, local orientation 0.0140183801°, world 0.00734141176
and skin 0.00114387798. Skin/world error is larger than the preceding candidate's
measurement but remains within the **unchanged same-master total limits**; this
is Error-bounded output, not a lossless-motion claim. Mandatory native poses and
the recorded motion-control functions have separate exact gates.

The scalar Hermite value/derivative certificate is continuous over its rational
polynomial intervals. Native floating-point, composed hierarchy, derivative
chords and full-skin measurements are separately sampled. Neither finite native
sampling nor a side-view image proves continuous contact correctness or a game.

## Reciprocal result and explicit fallback

| Mario → Sonic, SourceChannelTimes | Bytes | Decoded keys | Tracks |
| --- | ---: | ---: | ---: |
| Mario original source, context only | 114,128 | 11,724 | 58 |
| Sonic raw full-quality target | 148,748 | 12,358 | 45 |
| Raw target Exact / bounded-request fallback | 141,272 | 11,707 | 43 |
| Sonic Fall-fitted master | 126,600 | 10,257 | 45 |
| **Fitted Exact / bounded-request fallback** | **112,652** | **9,304** | **43** |

The fitted fallback saves 13,948 bytes / 11.0174% against its 126,600-byte master,
and is byte-identical to the earlier verified Exact export. Both requested Exact
and Error-bounded/Thorough use the same frozen master. The latter explicitly
reports effective **Exact**, zero refit proposals and the reason: non-unit
bind/animation scale and scale-compensation flags share a hierarchy path whose
bounded runtime world/skin semantics remain unqualified. The fitter does not
pretend that its scalar certificate closes this separate native gate.

| Reciprocal output | SHA-256 |
| --- | --- |
| Raw target master | `340edd4c92e92e07b7b2580805283b35313c9b099fe14c492a5ebcde54aa1ff9` |
| Raw Exact/fallback export | `6895c45e5fbefee9ac3dbcba7da0881827f8f1a5eb61e9d77d45ffa27a943f5e` |
| Fitted master | `59d84bd9eb7b9dff7bdecba830454516e95758fd4d83afc01b3148fe2eccf43f` |
| Fitted Exact/fallback export | `b9eaf85481e65358b89100f9071fb68d556ad03761c1933ccf558f87463ebf45` |

Fitted Exact preparation takes 25,222 ms and the explicit bounded request 25,259 ms
by the outer timer (internal latter report: 25,217 ms). It records 7,298 native
witnesses / 14,373 actual pose evaluations, 1,192,959 bone visits and 98,368,812
vertex visits. All 83 bones and all 6,844 skinned vertices participate. The main
report measures zero local/world/skin/derivative differences. A separate 105-time
withheld check also measures zero Euler/world/skin differences; its quaternion
self-comparison angular floor is about 0.00000170755°. That is reported separately
from decoded exact coefficient identity and is not admitted refitting error.

## Independent proof fixtures and accepted runs

The final Core corpus passes **217 checks**, including 38 new refit checks.
Seventeen integer knots with values `i` and slopes `.98f` give an independently
specified line: maximum value difference `(1 - .98f) × sqrt(3) / 18`, about
0.0019245°, and maximum speed difference about 0.02°/frame. A coarse span retaining
the old slopes instead has about 0.03° error at frame 4, exceeding the fixture's
saved 0.005° bound. The real refitter therefore demonstrates a tangent-fitting
opportunity that deletion alone cannot express. Adjacent accepted spans,
native paired-row emission, loop-wrap outside sides, fractional times, authored
protections, winding, signs/scales, identity/permutation and shared budgets have
independent checks; the complete original remains the comparison reference.

The native proof fixture uses an authentic structural leaf and moving protected
root. Actual worker output and precise encoding give **1,884 → 1,800 bytes** and
**170 → 163 stored keys** versus deletion-only. It changes no model/skin
coefficients. Local withheld Euler/orientation differences are about
0.00187206° / 0.00187397°, derivative 0.01999998°/frame. Its selected leaf is
unweighted, so its withheld bone-position/skin differences are zero; this fixture
does not establish nonzero skin-deformation accuracy by itself. The validator
still visits every bone/skin vertex and rejects the scalar-certified proposal
under a tighter saved whole-model limit. Its 1,845 native witnesses and shared
3,778 witnesses / 476,028 bone visits / 29,566,628 vertex visits remain recorded.
Actual export/reopen/save/reopen and a separate fresh-decoded scalar certificate
also pass. Paired tests require typed generated evidence and explicit permission;
unknown pairs, roots, pins, joins, jumps and signed-zero evidence stay protected.

| Current numerical/workflow evidence | Result |
| --- | --- |
| `core-shared-spans3` | 217 Core assertions pass |
| `native-optimization2` | 568 native assertions pass, including paired-provenance and actual refit/codec/reopen checks |
| `forward2` | 1,907 workflow assertions pass; original source, default Next, Fall-first fitting and real selected refit |
| `reverse2` | 1,860 assertions pass; authentic reciprocal transfer/fitting, exact fallback and unchanged live master/selection/Undo |

Evidence lives under the ignored `artifacts/Fighterport-span56/` root. Numerical
runs use separate `scripts/Test-BackgroundCapture.ps1` cases on private desktops,
the shared serialized capture mutex and below-normal priority. Core runs use the
local .NET 10.0.402 Release runner with `--animation-optimization`. The build has
the existing Avalonia AVA3001 warning, no compiler error. Raw byte diagnostics
are read-only; no OS input injection or active-desktop screen capture occurs.

## Independent audit and visual review

The final independent eight-file audit accounts for every physical byte in both
original sources, both finished masters and the previous/new selected exports.
It reports **620,762 scalar comparisons and 24,559 outgoing-side comparisons**
against native decoding with zero differences, no unexplained nonzero bytes and
no sampling-budget exhaustion. This is separate evidence from the narrower raw
before/after diagnostic above; neither is an independent game runtime.

All four orthographic side GIFs were decoded and actually inspected: 88 forward
frames per GIF and 70 reciprocal frames per GIF, 316 frames in 40 review sheets.
Two-pane master/export and four-pane source/master/previous/new views retain the
full actors, feet and gold Y=0 guide. One frozen first Fall body pose is appended;
the reference animation is not played. The source Sonic auxiliary sphere is hidden
only in the declared inspection renderer, leaving exports unchanged. No added
pose jump or twist is apparent at the review resolution. Native aerial positions
relative to Y=0 are observations, not reviewed floor/contact acceptance. GIF
cadence is labelled inspection timing, not game-speed playback. All 50 native UI
PNGs were separately opened and hash-bound, including compact/wide text layouts.
Local reports and review receipts are under `artifacts/Fighterport-span56/results/`
and its sibling `media-forward2`, `media-reverse2`, `byte-audit-final2` folders.

## Qualification still separate

The bounded increment's numerical and visual gates pass; the launcher selects
the qualified native2 package. Actual BrawlBox/stock BrawlCrate version interaction, plug-in compatibility,
physical input, monitor DPI, human usability, Melee writing, arbitrary costumes,
universal contacts and game execution remain unverified. No extra solver,
adapter, skeleton conversion, lossy project Save or project-format change is
qualified here. Unreviewed absolute contact and unsupported native-semantic
contexts retain Exact/reference output. A reopened file is not game acceptance.
