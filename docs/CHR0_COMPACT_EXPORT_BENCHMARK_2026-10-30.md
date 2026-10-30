# preview.64 — same-finished-target CHR0 benchmark

**Status:** bounded preview.64 qualification complete; publication is separate.
Starting commit `1c96251bb7d8d4cf3ed54443e1920857942d9ad6`.
All inputs are private local fixtures. No game assets/binaries/settings/media
are published with this source record. [Acceptance](CHR0_COMPACT_EXPORT_ACCEPTANCE_2026-10-30.md)
and [implementation notes](CHR0_COMPACT_EXPORT_NOTES_2026-10-30.md) define scope.

## Fair baselines and actual files

Each row compares its own finished target master with its independent verified
copy. Do not use the untouched donor as the compression-quality reference.
Actual Library Use retains correction=true and native +Y/anatomical heading,
without the legacy explicit identity-coordinate override.

| Finished target / operation | Master bytes | Exact copy | Bounded copy | Master→bounded | Native projected keys | New L4 groups/streams |
|---|---:|---:|---:|---:|---:|---:|
| Sonic down-air→Mario, source-bone timing |109984 |87212 |58256 |51728B /47.03% |14459→5186 |2/6 |
| Same action, source-channel timing |81140 |79568 |54308 |26832B /33.07% |6912→5220 |3/9 |
| Source-channel action fitted to Mario Fall frame1 at both ends |75152 |Not separately benchmarked |53004 |22148B /29.47% |6343→4357 |0/0 |
| Procedural nonflat v4, authentic Mario and Sonic models |876 |Reference876 |420 |456B |66→66 |1/1 |
| Same procedural v5 |880 |Reference880 |424 |456B |66→66 |1/1 |

Raw source-bone bounded search first retains61280B; L4 contributes only its
additional3024B reduction. Source-channel retains58712B first; L4 contributes
4404B. Other savings come from existing exact/reduction/indexed work. Procedural
456B splits into320B L4 body storage and136B exact indexed root packing.
Key count can increase versus a sparse incumbent while actual bytes fall.

The fitted44-frame master uses frozen Fall frame1 references,5 entry samples
and10 exit samples, existing tracks only and source-owned root translation.
Four complete native entry/exit endpoint and join local/world/skin float outputs stay
exact; the middle/root/reference/Save/reopen gates pass. No smaller eligible L4
group exists under those protections: zero new-L4 attempts is correct fallback,
not evidence that fitting lost its requirements. Contact/floor fitting was not
enabled or newly accepted.

Source Sonic CHR0 is12016B, SHA
`1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4`.
The supplied Mario up-air is13608B, SHA
`93559d59573ed7bd6612bdd3a2b7daa06d1df457982884e281f55139f1ebb9fc`.
Those are storage context only; there is no12KB promise.

## Exact identities and storage

| Target file | SHA-256 |
|---|---|
| Raw bone master109984B |`07edb1050d371943ca71fbe3c3dbdc579c91e63ad25ae17d7dca734bc2125c5b` |
| Raw bone bounded58256B |`0b0e51bd28f6322bd813d41e6c4f0507f1ab9dacd1700a00cc434ef22e974a79` |
| Raw channel master81140B; exact preview.63 actual-Use byte identity |`582c4b69a438cdd8b5dcf59a411c5a6f437acbe575256e6ff4cd7d95dfd75a45` |
| Raw channel bounded54308B |`9cf68fa6f3d4de7e1510829a195368231b89d9a0b1f534e19808e55ddec4ab5d` |
| Fitted master75152B |`036c46c44925a3dbeaf009f76d97550613d33fe05cf449de81928e5d3dd8ccfc` |
| Fitted bounded53004B |`b797a89fb8e4461476ce1594f066f5f785d5b182f0535cb916e4686638a38183` |

Raw bone's final whole-SRT formats:38 I12/2 L4; scalar streams112 I12/6 L4.
Raw channel:35 I12/2 I6/3 L4 groups;103 I12/6 I6/9 L4 streams.
The fitted candidate has128 I12/100 I4 scalar streams and no L4.
Group and stream counts are different quantities.

| Final physical file | Declared resource | Curve payload /headers | Bone headers+slots | Dictionary | Strings | Native-proved alignment |
|---:|---:|---:|---:|---:|---:|---:|
|58256 |57748 |54600 /896 |1564 |648 |451 |57 |
|54308 |53800 |50628 /920 |1564 |648 |451 |57 |
|53004 |52484 |47572 /2624 |1584 |664 |461 |59 |

Resource header is40B in these v4 files; user metadata0B. The independent
Python parser reconciles physical totals but conservatively labels57/59 bytes
unclassified. The app's separate ledger proves those as alignment and reports
no unknown bytes. Do not silently turn the parser's weaker classification into
an independent padding proof. Header resource size is not physical file length.

## Quality, protections and work

Saved limits: raw Euler/local orientation0.1°, translation0.002 native units,
scale0.0001; native bone/skin0.001×bind diagonal L. Derivative/frame: rotation0.1°,
translation0.002, scale0.0001. L=23.777803865350322 for these Mario fixtures.
Exact motion controls/pins/contact dependencies/joins override approximation.

| Measured maximum / native witness coverage | Raw bone | Raw channel | Fitted |
|---|---:|---:|---:|
| Raw Euler, degrees |0.012490809 |0.0124543905 |0.0248977244 |
| Local orientation, degrees |0.014934069 |0.0134969957 |0.0284142222 |
| Local translation, native units |3.69732012e-6 |3.21886639e-6 |1.19416329e-6 |
| Local scale |0 |0 |0 |
| Reported world/basis metric |0.00864391492 |0.0074844394 |0.0141230242 |
| Skinned-vertex displacement |0.00121333395 |0.00122291572 |0.00272043616 |
| Euler derivative, degrees/frame |0.0124998093 |0.0124998093 |0.0249245049 |
| Orientation derivative, degrees/frame |0.0181708162 |0.0187103647 |0.0318412023 |
| World error derivative metric/frame |0.00862032527 |0.00892909975 |0.0200116773 |
| Skin error derivative/frame |0.00148775562 |0.00140012072 |0.00255500778 |
| Reported native witnesses |12794 |11157 |17876 |
| Preparation milliseconds |187317 |170087 |135239 |

Raw cases separately include132 withheld native samples across63 bones/3913
vertices. Native coverage includes integer/quarter/withheld fractions, original
and candidate keys, protected/endpoints and bounded adaptive midpoints. All
original skin coefficients participate; no four-influence truncation was added.
Core rational scalar proofs, floating physical-linear hulls and finite geometry
are distinct. Finite samples do not prove continuous geometry/contact quality.

| Scalar charge ledger | Raw bone | Raw channel | Fitted |
|---|---:|---:|---:|
| Core generation |1601704 |1690768 |1714195 |
| Core certificates |274884 |164904 |126282 |
| Dense/indexed admission work |123410 |121716 |159500 |
| Total /2000000 |1999998 |1977388 |1999977 |
| Complete compact attempts /24 |7 |4 |3 |
| Eligible/attempted/accepted early L4 groups |2/1plan/2 |3/1plan/3 |0/0/0 |

Nested phase totals are not extra charges. All alternatives share the original
master and aggregate native/work counters. Later attempts stop at existing
limits; no partially verified candidate is offered. Greater effort does not
allow worse quality and no global optimum is claimed.

## Apply, container and failure evidence

The actual dialog's new L4 current-Apply test reproduces81140→54308B, three L4
groups and exactly one native command. Undo/Redo restore exact original/result
storage; repeat admission retains the original master. The actual animation-list
Export callback performs zero optimization and reopens byte-identically to the
accepted54308B output. Source/model/weights/history/recipe/selection/frame/camera
and siblings retain their contracts.

Supported native PAC insertion compares **the same finished-master insertion**
3395136B with optimized insertion3368384B: actual26752B saving. Reopen/resave
remains3368384B and the extracted selected slot is byte-identical54308B.199
native checks cover target/slot/flags and20964 untouched native entries. The
independent audit measures file lengths/hashes; it is not an independent PAC
parser. No outer compression or game benefit is inferred.

Both actual Library Use reverse up-air layouts reject in the original corrected
fractional bake before target import/optimization (3640 unresolved intervals,
work1999698). No reverse output/GIF is fabricated. Legacy explicit-identity
coordinate fixtures are separate:147088/122044B forward masters and their
reverse refusals cannot replace the actual Library baselines above.

The combined fitting run timed out in duplicate final HQ analysis and is wholly
excluded. Focused harness7 repeats normal retarget, full fit, project Save/reopen
and one post-fit preparation under the unchanged10-minute limit;204 checks pass.

Final read-only accounting:23 comparisons, input hashes unchanged, no writer or
native process; local `accounting-final2/summary.json` SHA
`82b7fe798c23b797e60a4ccd90636acaedbb0a457fa360357c5e43346284e4be`.
Source/target fixture hashes, size/format/key ledgers, actual native proof/work
receipts and fallback reasons remain locally pinned. General contacts, external
editors/runtime, Melee, physical input/DPI/human usability and game acceptance
remain open. Three same-target before/after GIFs are reviewed in full:261
decoded frames on33 sheets plus11 selected full PNGs, with1352 separate renderer
assertions. Fixed+X orthographic side, gold Y=0 and byte labels are visible.
Before/after motion visibly matches; foot/line crossings in both copies do not
establish contact quality. Fitted endpoints agree without appended reference
playback. GIF SHA-256 values are respectively
`2b55deaa8b393a4e587ed5dec969954c9b94a7f56fd017b957d8f14bb07191a2`,
`afd98d7753c70b8a22adcd0439ce5f43d482f7c24a4dc8dd7d7c931dabafa2b2`,
`c6dc3e2fdc05689710e32917bdd3304368b49578322e891ca1dd9c5455ee64ee`
for raw bone, raw channel and fitted channel. Review receipt SHA
`65612b5b1c63aa72ba3fd62d603bde9c95913ddc09fb82dccc199ad0267e8b4e`.
