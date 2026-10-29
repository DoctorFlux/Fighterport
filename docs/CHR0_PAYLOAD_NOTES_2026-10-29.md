# CHR0 payload accounting and preparation

This increment starts from
`c92410bbf8a42af7d355b61248aa14e39404655e` (preview.60). The inspected codec,
precise writer, retargeter, fitting and optimizer sources are the owned versions
at that revision, rather than an unpinned current upstream branch. The byte
audit reads the two supplied files without rewriting them. Game fixtures,
private paths and generated evidence stay local and are not distributed.

## Supplied files: measured representation difference

| Local fixture | SHA-256 | Physical bytes |
| --- | --- | ---: |
| Original Sonic AttackAirLw | `1298044FC7202FC67C952424EAB95C4A62A947C476376FB95805D2BEF867BFB4` | 12,016 |
| Sonic AttackAirLw retargeted to Mario | `C18730A68F73CBCDE21655FD6D7E07EEFAC5B0B12A19C9DD02132359798D60A1` | 83,864 |

Both are CHR0 v4, 44 frames and non-looping. The latter is byte-identical to the
prior prepared source-channel UI result: this supplied save/export did not add
the observed size after retarget preparation. This is a representation diagnosis,
not a like-for-like compression benchmark: the finished target motion is the
optimization reference, not the donor's different body/animation result.

| Serialized category | Sonic | Mario target | Change |
| --- | ---: | ---: | ---: |
| Physical standalone file | 12,016 | 83,864 | +71,848 |
| Declared native resource | 11,125 | 83,344 | +72,219 |
| Scalar curve payload | 5,904 | 80,112 | +74,208 |
| Scalar codec headers | 1,656 | 944 | −712 |
| All remaining physical bytes | 4,456 | 2,808 | −1,648 |

The source has 66 bone entries; the target has 40. Names, dictionaries, entry
slots and other non-curve bytes become smaller. Curve payload causes the growth.
The source has 84 L1 streams with 45 stored samples each, 59 I4 streams with
453 stored indexed rows, and five I12 streams with 26 rows. The target has 118
animated I12 streams with 6,676 explicit rows. Its 3,541 distinct animated scalar
time groups include 3,135 extra paired rows, costing 37,620 bytes alone. Duplicate
times can carry distinct incoming/outgoing tangents; deleting one side without
proof changes interpolation.

For example, HipN rotation X has the same **44 distinct integer times** in both
files. The source stores 45 L1 samples plus an eight-byte header: **53 bytes**.
The target stores 74 I12 rows, including 30 paired sites, plus its header:
**896 bytes**. All three HipN rotation streams occupy 159 versus 2,628 bytes.
Visible frame diamonds do not expose these codec and tangent-side costs.

The independent raw parser reconstructs **7,108 projected source scalar keys**
using the existing dense-projection convention. This is a parser projection,
not a fresh native-reader key count measured in this audit. The physical source
has 3,780 L1 samples plus 479 indexed rows. Target isotropic expansion explains
the separate 6,912 prepared-result native key count; stored fixed slots and
expanded native keys must not be added interchangeably. The source byte ledger
also identifies a three-byte zero gap between its resource end and external
string table alignment. It does not classify this as animation payload.
Standalone resource size, physical length, container alignment and outer
compression remain separate quantities.

## Legal-format inventory is not motion qualification

A read-only inventory respects one format per S/R/T group, fixed/isotropic
layout and local native signed rounding. Of 40 animated target groups, 27 have
structurally representable I4 times/tangents and 33 have structurally
representable I6 times/tangents, before value quantization or motion error.
I4 is blocked by fractional times in 13 groups and tangent range in seven;
I6 is blocked by off-1/32 times in six and tangent range in four. Categories
overlap. No index/count maximum is reached by this clip.

The cheapest structural mix gives a hypothetical 53,988-byte layout. This is
not an encoded or accepted candidate, or a promised result: quantization,
protected curves and complete model quality may require larger storage. Actual
final encoded bytes decide among fully qualified candidates.

## Implementation boundary

`NativeAnimationOptimization.ExactStorage.cs` shares the existing group-wide
I4/I6 exact search with prepared fitting and the optimizer. It never changes
native times, values or tangents. Existing baked rotation payloads retain their
complete terminal data. Independently encoded references and candidates must
fresh-reopen with identical coefficients, metadata, flags, track order and
expected storage. Acceptance requires smaller physical bytes and a non-increasing
resource block; otherwise the qualified reference remains. Cancellation disposes
copies and changes no authoring resource.

Fitting previously reopened a source then promoted entries directly to the
precise writer. Promotion retains keys/flags but does not automatically capture
imported compact payloads, so untouched compact groups could expand to I12.
Fitting now uses the independent `CopySource` path before editing: matching
plans survive, while changed groups fall back to I12. After proving the complete
numerical fit, storage-only preparation may attach exact compact plans. The final
physical proof supplies the receipt hash. Native snapshots preserve the plans
through the same clip's Apply/Undo/Redo; no document or Undo owner is added.

The full-quality fitting master, joins, root controls, original references and
recipes remain intact. Ordinary Save remains exact. Ordinary native right-click
Export writes the current animation through the existing picker and verified
copy; it runs no reducer or preparation window. Explicit Optimize CHR0 size owns
further error-bounded reduction and independent export policies.

A second storage-loss path was reproduced during qualification: the legacy
preview-context code called `root.Export(path)` on the live motion PAC. Native
rebuild could discard retained I4/L1 payloads and clear dirty state even while
complete numeric content stayed identical. The resulting serialized-hash change
correctly tripped the fitting stale guard. Changing test selection alone did not
fix it. `NativeFittingPreviewSnapshot` now copies complete ARC/BRRES context via
the raw stream overload, validates exact native visual member/ancestor metadata,
and overlays only the already frozen current MDL0 in independent owners.
Pure-motion references are unnecessary for texture/palette lookup and are
omitted; current CHR0s are passed separately for native posing. Original motion
content, compact payloads, terminal samples, backing bytes and dirty state are
preserved. Unsaved texture/palette/additional-model or structural visual edits
fail closed with an explicit save-context message rather than showing stale
data. Standalone visual leaves require complete independently owned data.
The bounds are 512 MiB per context, 100k traversal nodes and 128 levels; playback
and animation/solver work budgets remain unchanged.

Final optimizer selection already uses actual encoded bytes, with key count
only as a tie-break. Earlier scalar deletion/refit proposals use error or native
row counts, so they can miss a smaller legal group encoding. Bounded search must
compare complete decoded candidates against the retained finished master;
fewer keys alone are not evidence of smaller output. Search effort never grants
more quality allowance or proves global optimality.

## Measured candidates and separate qualification

Final-build continuity was checked again under the local `final2` evidence:
`source-forward1`, `source-reverse1` and the actual `use-model1` export. All seven
result files have identical SHA-256 hashes and independently decoded ordered
rows to the previously audited files below. All six direct Dense masters also
retain identical file hashes and recorded native content identities. The
mode-specific measurements therefore apply to these final-build outputs as
well. Local `final2/accounting-continuity.json` records exact paths, hashes,
counts and the unchanged supplied Sonic source hash. This establishes byte
continuity; suite qualification is recorded separately in the acceptance record,
and human usability remains unverified.

### Actual Use on This Model: supplied Sonic → Mario case

The actual shared-library **Use on This Model** result improves from **83,864 to
81,140 bytes: 2,724 bytes (3.25%)**. It remains substantially larger than the
12,016-byte unretargeted Sonic source. The direct bone-key result below uses a
different preparation context and does not represent this ordinary button's
percentage saving.

The old UI file is byte-identical to the supplied Mario target file. Old/new UI
receipts agree on exact frozen source content, source/target rigs, and the same
237,620-byte Dense target master with content identity
`9fc625cf8156e65802edad0a69058cfd82f0cb90508822dc259afca62d907835`.
This UI case records that master identity in its native receipt; it does not
materialize a separate Dense CHR0 for an independent file-hash comparison.

Independent decoding confirms unchanged 6,912 native keys and the exact knot
schedule, including 6,676 stored variable rows and 3,135 paired sites. Six
accepted I6 streams store 462 rows; 112 I12 streams retain 6,214 rows. Payload
falls from 80,112 to 77,340 bytes while codec headers rise from 944 to 992.
The final coefficients change within the existing strict quality gates, so this
is error-bounded packing. Protected-entry presence and complete ordered rows
remain exact. The completed application receipt reports 3,729 native witnesses,
maximum Euler/orientation errors 0.001239300° / 0.001716740°, world-bone error
0.000684401 native units and skin error 0.000115199. Its continuous scalar
certificate is separate from those sampled native geometry results.

The new UI export SHA-256 is
`582c4b69a438cdd8b5dcf59a411c5a6f437acbe575256e6ff4cd7d95dfd75a45`.
Original source and supplied target files remain unchanged. No requested file
size, tolerance relaxation or smaller direct-test percentage overrides this
measured button result.

### Direct Sonic → Mario preparation

The preview.61 `source-forward3` background receipt passed 894 checks on its
recorded build. The independent byte audit compares its exact exported files
with preview.59 `source-forward2`. Every mode has the same 227,168-byte Dense
Next master: native content
`970ab050c55f9a3e753c7c61a38320c250a7e4805a7996422fc74eac8f08484b`, file SHA-256
`d5531a535d1237d37fe60bd7bdfa387d55b5acd52815dc94237e27d39eb58909`.
Both the independent raw decoder and actual dense file hashes confirm that
baseline equality. No comparison substitutes the unretargeted Sonic donor as
the quality reference.

| Mode | Preview.59 bytes | Preview.61 bytes | Saving | Native keys, unchanged | Accepted storage |
| --- | ---: | ---: | ---: | ---: | --- |
| Source bone keys | 135,468 | 99,172 | 36,296 (26.79%) | 13,558 | 57 I4 + 120 I12 scalar streams |
| Source channel keys | 74,060 | 69,848 | 4,212 (5.69%) | 6,095 | 9 I6 + 109 I12 scalar streams |
| Interpolated keys | 115,016 | 115,016 | 0 | 9,508 | 118 I12 scalar streams; unchanged fallback |

These savings come from payload selection rather than deleting further keys.
Stored variable row counts remain 10,937 / 5,859 / 9,272 respectively; native
key totals additionally include fixed slots and isotropic aliases. The bone-key
file is independently bit-identical to its prior decoded ordered key rows and
has the same complete native result content identity: its saving is lossless
packing. The channel-key file retains its exact knot schedule but contains
accepted quantized coefficients. That saving is error-bounded, not lossless.
Interpolated retains the identical previous file because no smaller fully
qualified representation was found within the bounded search.

The application's completed forward receipt reports maximum raw Euler /
orientation / world-bone / skin errors of 0.002374351° / 0.003108968° /
0.001544386 / 0.000300737 native units for bone keys, at 3,186 native witnesses.
For channel keys it reports 0.001239300° / 0.001716740° / 0.000957171 /
0.000115199 at 3,592 witnesses; local translation differs by at most
0.000004897 native units and scale by zero. Native integer, quarter, withheld,
key/endpoint and bounded adaptive samples are distinct from continuous scalar
Hermite certificates. No scalar/native tolerance or work ceiling was relaxed.
Independent parsing verifies identical protected-entry presence and ordered
rows across old/new outputs; the application receipt separately covers the
complete reviewed source-local control policy and all native skin influences.

The new file hashes are:

- Bone keys: `da2c01e78a43d9c5200a133b0463024c9130b9ba85e45eb9dbcb8a9d9f31752d`.
- Channel keys: `d535a5662aba402403b80b31549a1e74ebadf88a5c4dde8f349e82972979f309`.
- Interpolated: `1b0c55d327309c088cb3511f0a6e06e78406ddf32c6eb3004a8a683a79ef5b39`.

The independent Python parser and the application's pure serialized-byte
accountant agree on physical length, resource size, curve payload and headers
for all twelve old/new result/master files. The supplied original Sonic file
still has its unchanged 12,016-byte hash listed above. Its historical alternate
filename is unavailable and is not silently substituted. Local accounting
JSON/CSV records include all file hashes, format counts, row counts and both
native result identities. Isolated search time per mode is not instrumented;
final increment acceptance is recorded separately. PAC/BRRES and outer compression savings
were not measured.

### Direct Mario → Sonic preparation

The reverse audit compares `source-reverse1` with preview.59 `source-reverse2`.
Every direct mode has the same 188,568-byte Dense master, with native content
`22d23703d0f3e20ddc66df04193e3aa0ca1af26cd0f604c0e64cec4fc36a0ca2` and file
SHA-256 `c90cc90f87d228415a1592c04142243ef5c99d1a26fcf2a0b90847d4e3beb597`.
Both actual file hashes and independent decoded rows confirm baseline equality.

| Mode | Preview.59 bytes | Preview.61 bytes | Saving | Native keys, unchanged | Accepted storage |
| --- | ---: | ---: | ---: | ---: | --- |
| Source bone keys | 188,568 | 188,568 | 0 | 17,909 | 323 I12 streams; unchanged Dense fallback |
| Source channel keys | 111,320 | 108,616 | 2,704 (2.43%) | 9,239 | 22 I4 + 131 I12 streams |
| Interpolated keys | 99,572 | 96,868 | 2,704 (2.72%) | 8,260 | 22 I4 + 131 I12 streams |

All three reverse files retain the exact prior decoded rows and native result
content identities; these added storage savings are lossless relative to their
finished mode result. Channel and Interpolated retain 8,923 and 7,944 variable
rows respectively; 360 rows use I4 in each. Existing source-key motion differences
from Dense remain separately measured by the application. Channel reports 1,913
native witnesses and maximum orientation/skin errors 0.001689626° / 0.000098644
native units; Interpolated reports 2,916 witnesses and 0.001621108° / 0.000233091.
Bone-key attempts would grow the native resource block, so the identical Dense
file remains. Its zero new comparison samples are fallback evidence, not a new
zero-error geometry test.

New reverse hashes are
`2d06349023e62fbfefdb22339f3945efbfba712f2e1bccc0553cd32777aa244e` (channel)
and `3b2556210a72aa39511f5d20d78101b25c1f4b39915f930d519db43476992fb8`
(Interpolated). The independent parser and pure C# accountant agree for the
additional fourteen reverse/UI files, giving 26 matched old/new files overall.
Local JSON/CSV includes per-file hashes, format/row counts, raw time/tuple and
protected-entry comparisons, and the distinct UI master-receipt boundary.

## Evidence status

The original-file diagnosis uses a Python byte decoder independent of BrawlLib
and the application's serialized size accountant. It establishes stored layouts
and byte attribution, not native pose or game compatibility. Preparation now
qualifies with 3,450 checks / 11 admitted background runs and 60 reviewed fresh
UI images; see [acceptance](CHR0_PAYLOAD_ACCEPTANCE_2026-10-29.md).
The applications' recorded native motion evidence is attributed separately above;
this byte audit launches no native viewer or solver. No physical input,
external-editor, Melee or game acceptance is claimed. Fresh reopen through the
application's own reader and independent raw byte parsing are different evidence.
