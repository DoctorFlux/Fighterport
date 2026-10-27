# preview.51 — source-key motion guards and verified storage

Both source-key choices now use source times as seeds, retaining target-derived
axes/tracks and adding required native keys. They share one numerical algorithm
for supported rigs/animations; no character, action, filename, hash or bone-index
exceptions were added. The complete native Model Previewer, original dense Next,
native Undo and source-owned motion policy remain intact. ONE G1/M2 is incomplete.

## Motion and reduction

The sparse v1 layout could drop target-space motion or skip curved intervals
without bounding the error. New v2 preparation retains required target data and
proves complete raw Hermite value/derivative spans using rational normalized
Bernstein bounds. Uncertain spans retain original knots. Existing native
orientation, hierarchy/basis, skin and derivative checks qualify the cumulative
candidate against frozen dense Next. Exact identity is the fallback when bounded
qualification cannot complete. Invalid values, timing and paired sides are
rejected before even a geometry-unavailable identity fallback.

Frozen strict limits remain: raw Euler/local orientation 0.01 degrees, scale
0.00001, translation vector min(0.0002, 0.0001 × L), hierarchy/skin 0.0001 × L,
Euler derivative 0.05 degrees/frame and corresponding translation/scale derivative
limits. L is the original finite positive bind-geometry diagonal. Roots are
exact-only. Full turns, signed zero, paired sides, loop timing and singular scales
retain their protections. V1 receipts still read as historical sampled evidence;
v2 reports record seeds, added knots, certificates and actual native maxima.
The v2 combined world/basis metric is labelled as such; its legacy separate basis
field is not a new measurement. No sampled geometry result proves continuous
contact quality or game acceptance.

High-quality storage now runs independently on a verified Exact reduction when
the user selects it. Rejected approximate reduction no longer blocks that path.
Bounded selective I4/I6 trials retain useful groups when others fail. Selective
curve reduction can restore a failing bone and its actual ancestors from the
same-baseline verified Exact result while retaining other certified proposals.
Every composed candidate is rechecked cumulatively, with shared budgets, at most
eight reduction attempts and 24 packing attempts. Root/pin/contact and exact
fitting-join protections stay intact. Reports distinguish proposed Core counts
from final native counts and classify approximate storage as High quality.

## Actual measurements

The supplied Sonic file remains 12,016 bytes and the supplied Mario file remains
80,436 bytes, with their hashes unchanged from preview.50. The authentic
44-frame Sonic AttackAirLw comparison uses the same dense Next reference,
byte-identical to preview.50. Both fitting references are only Mario Fall frame1;
display ranges are 1–5 and35–44, with exact middle/joins and attack-owned TransN.

| Source-key choice | Old maximum sampled orientation difference | Guarded maximum native orientation difference | Guarded raw | Fitted | Final optimized |
| --- | ---: | ---: | ---: | ---: | ---: |
| Bone times | 12.22065° | 0.001581435° | 63,264 B | 69,744 B | **49,120 B** |
| Channel times | 58.4117° | 0.001478656° | 60,132 B | 67,452 B | **45,052 B** |

Final bone-times optimization retains a verified Exact fallback, 4,043 keys and
47 tracks. Channel-times High quality reaches 3,704 keys/47 tracks; its smaller
composed candidate restores five actual dependency bones and passes all gates.
The supplied Mario file optimized independently measures 64,756 bytes with Exact
and 60,976 bytes with bounded High-quality storage. These are whole standalone
CHR0 sizes, not PAC/PCS or game-memory savings. The 12 KB objective is not reached.

Other authentic tests use Snake AttackS3S → Mario and Mario Wait1 → Snake. The
former measures 44,840/39,872 raw bytes with maximum orientation differences
0.001758°/0.001332°. The latter measures 54,180/50,460 raw bytes and stays within
the same strict gates. This particular Wait1 has 51 frames and its stored loop
flag is **off**; looping is covered separately by procedural cases. A separate
side GIF preserves the existing Airborne preparation and its inherited foot lift;
it is not a new grounded/contact acceptance result.

## Byte-level audit

The local artifact auditor independently parses 16 CHR0 files, including v5 user
metadata: all **798,732 bytes** are assigned once, with **68,618 native key records**
cross-checked. Indexed coefficient tuples and metadata match native decoding;
dense codecs store values and generate native tangents. Independent scalar
evaluation roundoff is reported separately, not described as bit-identical
floating-point evaluation at every time. Six direct offset-navigation checks and
three inspected headless images verify the interactive local hex viewer.

The original Sonic resource uses 84 L1 channels: 3,696 one-byte dense samples.
Its complete key payload is 5,820 bytes plus 1,656 codec-header bytes. The same
decoded source curves encoded precisely as I12 take 55,488 bytes. Final bone and
channel outputs devote 42,808 and38,740 bytes to key payloads, plus3,040 bytes each
to codec headers. Remaining duplicate-payload savings are only a theoretical
1,056-byte upper bound; there is no large padding-only saving established.

The source has 336 unclassified bytes, 73 nonzero, around its L1 blocks. Their
pattern resembles an additional sample plus alignment, but their game meaning
has not been established. They are retained and shown explicitly, never labelled
waste or deleted. Physical referenced ranges and the odd declared resource size
are reported separately. Raw bytes, per-channel CSV/JSON and private paths stay
local under ignored `artifacts/Fighterport-quality51/byte-audit`.

## Qualification

The admitted background record is **8,593 checks across 17 runs**, plus the
separately counted byte audit. Runs use serialized private desktops/shared native
offscreen rendering or no-capture Core/root runners. No OS input injection,
desktop switching or active-desktop capture was used.

| Case | Checks |
| --- | ---: |
| Complete exact-file source-key → fit → optimize workflows | 2,742 |
| Native optimizer/storage/selective-reduction CPU fixtures | 249 |
| Native source-key generalization and Snake → Mario | 500 |
| Native source-key reverse direction, Mario Wait1 → Snake | 491 |
| Source-key workspace, stale/cancel and compact/wide text | 145 |
| Dirty ordinary source, adaptive seeds and independent witnesses | 201 |
| Original dense Next byte equality and native edits | 29 |
| Compact tool, cache, bind cleanup, Undo/export/reopen | 283 |
| Actual old v1 source-key project reopen/optimization | 480 |
| Batch review and complete recipe changes | 245 |
| Atomic set export | 152 |
| Packaged Core optimization | 114 |
| Source-owned root policy, both authentic directions | 628 +628 |
| Raw side comparisons, fitted comparisons and additional-character GIF | 728 +558 +420 |

Development runs before final code are excluded unless exact application/library
hash identity was verified. Native CPU and reverse-direction runs on dev2 use
application/library bytes identical to the final package. The initial dirty-source
test still required literal one-to-one key counts and failed after adaptive keys
were correctly added; that attempt is excluded. The corrected test requires every
source seed, independently measures native motion, preserves dirty/Undo state and
passes201 checks. No numeric or product gate was loosened to pass it.

There are 55 inspected native PNGs. Five distinct GIF sequences contain 229 inspected frames; duplicate artifact pairs
are independently decoded and hash-checked. Side cameras are orthographic +X with
world Y=0 ground line, with no root stripping. Raw comparisons show old sparse,
guarded sparse and dense Next. Fitted comparisons use all44 native frames plus
only a frozen Fall-first-pose hold. Half-speed inspection is labelled explicitly.
Compact/wide source dialogs use100–175% text. The existing narrow175% batch drawer
can leave the preserved native camera off-center or its actor clipped; wide views
are available. This limitation is retained, not counted as broad usability success.

Final package: `artifacts/Fighterport-quality51/native-final1`.

- Application: `4CB1F44DCCAD976DF37DA4D06527BABEFD716224CAC7E4463B2AF4789932D3D6`
- BrawlLib: `04E7AA4C1394257EA69A6448979285C7923AD5570ABC8354F7378B3F57834CC0`
- Core: `71DDB2B586CEDEB6CA7557C55F5F1D4E962ACB50E2150DFBA695FBB7B85D3969`
- EngineHost: `74ADCB24D38E398EB6765C6E705069724E7357E9849551BAF9E73886F88A4BA1`
- Worker: `697431FF9E201F81AB0970F79EEC5B0C8CDDBB246B1DE72E4DC05CB789F1DA33`

Publish source only to public DoctorFlux/Fighterport/main with DoctorFlux
author/committer and contributor verification. Physical input, monitor DPI, human
usability, universal contacts, new adapters/Melee writing and actual games remain
unverified. The separately authorized Small file profile is the next bounded
increment; it does not alter preview.51's strict limits.
