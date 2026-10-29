# CHR0 v4/v5: independent physical format audit

This is a bounded description of the Brawl CHR0 v4/v5 files handled by the
current product, with an independent read-only parser in
[`tools/chr0_forensics.py`](../tools/chr0_forensics.py). It is not a new codec,
optimizer, runtime implementation, or game-compatibility claim. No production
reader, writer, scalar tolerance, root contract, model, or skin weight changes
are part of this work.

The parser is original Python standard-library code. Its tests construct
original synthetic bytes from the field equations below; they do not serialize
fixtures through BrawlLib or use game assets as expected results. The previous
local growth decoder was a read-only reference, not an imported dependency.
Referenced source licenses, credits and notices remain with their owners; no
substantial upstream implementation is reproduced here.

## Evidence and scope

The owned source baseline is `01ce42bdea068c0aab83dc7c03a9e0f854a1ad34`.
Relevant owned files are `BrawlLib/SSBB/Types/Animations/CHR0.cs`,
`BrawlLib/Wii/Models/AnimationCode.cs`, `BrawlLib/SSBB/Types/Common.cs`,
`BrawlLib/SSBB/UserData.cs`, `BrawlLib/Wii/Animations/EncodingTypes.cs`,
`BrawlLib/Wii/Animations/AnimationConverter.cs`,
`BrawlLib/Wii/Animations/IndexedCHR0Storage.cs`, and
`BrawlLib/SSBB/ResourceNodes/Animations/PreciseCHR0EntryNode.cs`.

Two pinned primary implementations were also inspected:

- [BrawlCrate EncodingTypes.cs at a4c1ee3385010d5c64a88df9c56267c61f213321](https://github.com/soopercool101/BrawlCrate/blob/a4c1ee3385010d5c64a88df9c56267c61f213321/BrawlLib/Wii/Animations/EncodingTypes.cs).
- RiiStudio at `c5a7cd8d5e06c39d277d80f8c8514e3c44621836`:
  [AnimChrIO.hpp](https://github.com/snailspeed3/RiiStudio/blob/c5a7cd8d5e06c39d277d80f8c8514e3c44621836/source/brres/lib/brres-sys/src/librii/g3d/io/AnimChrIO.hpp)
  and [AnimChrIO.cpp](https://github.com/snailspeed3/RiiStudio/blob/c5a7cd8d5e06c39d277d80f8c8514e3c44621836/source/brres/lib/brres-sys/src/librii/g3d/io/AnimChrIO.cpp).

RiiStudio's inspected CHR0 reader accepts **version 5 only**. It independently
corroborates the scalar formats, signed fractional I6 times, dense `N+1` sample
counts, relative curve pointers and shared-track aliasing. It does not provide
an independent v4 header implementation or qualify the owned native viewer.
The v4 header description comes from the pinned owned Brawl source. Synthetic
tests verify both header layouts, rather than claiming a second implementation
proved v4 runtime behavior.

Two disagreements are material and remain explicit:

1. Bits 5/6 mean rotation-zero/translation-zero in RiiStudio; the owned
   `AnimationCode` calls them rotation-isotropic/translation-isotropic, and its
   reader can consume one shared slot when those bits accompany a present
   group. The normal owned writer only shares **scale**, uses three slots for
   present rotation/translation groups, and sets bits 5/6 on absent groups.
   The forensic parser rejects present groups with those ambiguous bits. It
   reports all raw flags and the independent omission-mask interpretation.
2. The pinned upstream BrawlCrate I6 getter reads unsigned `word >> 5`, dropping
   fractional bits. Owned I6 and RiiStudio read signed 16-bit time divided by
   32. The forensic parser uses the latter independently corroborated wire
   interpretation and preserves the signed raw word. This does not qualify
   round trips through untested external editor versions.

## Byte order, offsets, and resource boundaries

All numeric fields are big-endian. `u16`, `s16`, `u32`, `s32`, and `f32` below
mean unsigned/signed integers and IEEE-754 binary32. Addresses in the report are
absolute offsets within the supplied standalone file, with the CHR0 at zero.
Embedded BRRES/PAC extraction, decompression and relocation are outside this
tool; the common-header BRRES offset is reported without dereferencing it.

| Offset | v4 field | v5 field |
| --- | --- | --- |
| `0x00` | Four bytes `CHR0` | Same |
| `0x04` | `u32` declared resource size | Same |
| `0x08` | `u32` version, 4 | Version, 5 |
| `0x0c` | `s32` relative BRRES offset | Same |
| `0x10` | `s32` dictionary offset, relative to CHR0 | Same |
| `0x14` | `s32` animation-name offset | `s32` user-data offset; zero means absent |
| `0x18` | `s32` original-path offset | `s32` animation-name offset |
| `0x1c` | `u16` frame duration `N` | `s32` original-path offset |
| `0x1e` | `u16` bone-entry count | Part of preceding field |
| `0x20` | `u32` wrap/loop field | `u16 N`, then `u16` bone-entry count |
| `0x24` | `u32` scaling rule | `u32` wrap/loop field |
| `0x28` | End of v4 header | `u32` scaling rule |
| `0x2c` | — | End of v5 header |

Header name/path pointers are relative to CHR0 zero and point to character
bytes, not to a BRES string length word. Zero means absent. The current reader
treats every nonzero wrap field as looping; values other than 0/1 are reported
as noncanonical. Scaling rule is retained as a raw integer. Its complete game
transform meaning, especially scale compensation, is not inferred by parsing.

The resource size and physical file size are distinct. A standalone file may
append alignment and a BRES-style name table after the declared resource. Curve,
dictionary, bone-table and user-data ranges must fit **inside the resource**;
name/path/user-string pointers may point into the physical footer. The parser
does not rescue a truncated curve by reading footer bytes as extra samples.
It identifies NUL-terminated strings and records a preceding `u32` length word
only when that word equals the observed byte length. Every other byte remains
an explicit unclassified region, including zero-filled gaps.

## Resource dictionary and bone tables

A dictionary starts with `u32 declaredSize`, `u32 count`, and a 16-byte sentinel
node, followed by `count` 16-byte nodes. Its required table size is `24+16*count`.
Each node contains `u16 id`, `u16 flag`, `u16 left`, `u16 right`, `s32 nameOffset`,
and `s32 dataOffset`. Both offsets are relative to the **dictionary start**.
The sentinel is included in node-index bounds; its links can refer back to
itself. The parser bounds every link but does not claim to validate Patricia
lookup ordering. Declared dictionary extensions beyond the required table are
left unclassified instead of silently called padding.

A bone table starts with a `s32 nameOffset` relative to that **bone table**, then
the `u32` animation flags, then four-byte cells in scale, rotation, translation
order. Present non-isotropic groups have X/Y/Z cells. Isotropic scale has one
physical cell shared by SX/SY/SZ. A fixed cell is an inline `f32` value; a
variable cell is a `s32` curve offset relative to the **bone table**, not to the
cell or dictionary. The report preserves each reference cell, pointer base,
relative value, target address, logical channel and isotropic aliases.

The owned isotropic-scale reader consults the Z fixed bit while the normal
writer's shared cell is X. The independent source uses the X constant bit.
Normal files keep those bits equal. An X/Z mismatch is rejected by this parser
because the implementations would disagree between float and pointer cells.

## All 32 animation flag bits

No flag bits are discarded or normalized by the tool. The labels below describe
source interpretations, not a proved game evaluator.

| Bit(s) | Interpretation |
| --- | --- |
| 0 | Enabled / owned `AlwaysSet` |
| 1 | Complete S/R/T identity |
| 2 | Rotation and translation zero |
| 3 | Scale one |
| 4 | Scale isotropic: one cell serves SX/SY/SZ |
| 5 | Independent rotation zero; owned rotation-isotropic label (disagreement above) |
| 6 | Independent translation zero; owned translation-isotropic label (disagreement above) |
| 7 | Use model scale |
| 8 | Use model rotation |
| 9 | Use model translation |
| 10 | Scale-compensate apply |
| 11 | Scale-compensate parent |
| 12 | Classic scale off |
| 13, 14, 15 | SX, SY, SZ fixed |
| 16, 17, 18 | RX, RY, RZ fixed |
| 19, 20, 21 | TX, TY, TZ fixed |
| 22 | Scale group exists / requires scale |
| 23 | Rotation group exists / requires rotation |
| 24 | Translation group exists / requires translation |
| 25–26 | Scale format number, low bit first |
| 27–29 | Rotation format number, low bit first |
| 30–31 | Translation format number, low bit first |

The owned reader dispatches physical groups using bits 22–24. RiiStudio uses
omission masks: scale is omitted for identity/scale-one/model-scale; rotation
for identity/combined-zero/rotation-zero/model-rotation; translation for
identity/combined-zero/translation-zero/model-translation. SY/SZ also omit their
own cells under scale isotropy. Reports include this independent group-presence
interpretation and warnings when it disagrees with the owned existence bits.
Such a report inventories the owned physical layout, not cross-reader semantic
agreement. It never manufactures missing model/rest transforms from names.

## Six variable scalar formats

Format numbers are 0 None, 1 I4, 2 I6, 3 I12, 4 L1, 5 L2, 6 L4; rotation's
number 7 is reserved. Scale/translation have two-bit format fields and cannot
represent baked formats. A variable cell with None is rejected. Fixed cells
have no separate timed curve payload regardless of the group's format bits.

| Format | Curve header | Per stored row/sample | Required unaligned size |
| --- | --- | --- | --- |
| I4 | 16 bytes | 4 bytes | `16 + 4*K` |
| I6 | 16 bytes | 6 bytes | `16 + 6*K` |
| I12 | 8 bytes | 12 bytes | `8 + 12*K` |
| L1 | 8 bytes | 1 byte | `8 + N + 1` |
| L2 | 8 bytes | 2 bytes | `8 + 2*(N+1)` |
| L4 | No header | 4 bytes | `4*(N+1)` |

Indexed headers start with `u16 K`, `u16 reserved`, and `f32 frameScale`.
I4/I6 add `f32 valueStep`, `f32 valueBase`. Reserved words and frameScale are
reported verbatim; nonzero reserved words are not assumed corrupt. Current
writers commonly use the binary32 value immediately below 1 divided by the
first-to-last knot span for frameScale (zero for zero span). The parser does
not reinterpret it as a key count, rescale authored times with it, or claim
its complete runtime role is proved.

I4's big-endian 32-bit row packs time in bits 24–31, unsigned value integer in
bits 12–23 and a signed two's-complement tangent integer in bits 0–11. Time is
an integer in `[0,255]`. Tangent is integer/32, ranging from `-64` through
`63.96875`. Value is `valueBase + integer*valueStep`, with integer `[0,4095]`.

I6 stores `s16 timeInteger`, `u16 valueInteger`, `s16 tangentInteger`. Time is
signed S10.5, `timeInteger/32`, ranging from `-1024` through `1023.96875`.
The current owned authoring writer admits only nonnegative times on that grid.
The parser still decodes negative wire times and explicitly marks them outside
that authoring timing contract. `0x8000` means **-1024**, not positive 1024;
frame 256 is legal, while positive 1024 needs I12. Tangent is signed integer/256,
from `-128` through `127.99609375`. Value uses the same base/step equation with
unsigned integer `[0,65535]`.

I12 rows are three `f32` values: time, raw scalar value, tangent per frame.
Fractional times and IEEE negative zeros are preserved in both numeric and raw
bit fields. Euler values are raw degrees: 720 remains 720, never wrapped to 0.
The parser rejects nonfinite typed floats and nonfinite selected-profile decoded
arithmetic.

### Arithmetic is an explicit observation

Quantized values have an explicit arithmetic profile:

- `binary32-steps` is the preserved API/CLI default: round `q*step` to binary32,
  then round `base + roundedProduct` to binary32. It is a stated arithmetic
  convention, not a universal interpretation of the owned C# expression.
- `exact-single-round` forms the exact dyadic value `base + q*step` using integer
  significands and binary exponents, then performs one direct binary32
  nearest/even rounding. Discarded integer bits decide ties, subnormals, signed
  zero and overflow; no intermediate binary64 conversion can double round it.
- `native-x86` is an explicit alias of `exact-single-round` for comparison with
  the observed owned .NET Framework/x86 capture. The name is not a claim that
  every x86 compiler, JIT, register/spill path or game runtime uses that profile.

The selected canonical profile and requested spelling are included in every
report. I4/I6 rows and L1/L2 physical samples also include both profiles' decoded
value/bit alternatives and their raw unsigned quantizer value. Raw base/step
bits remain present. A nonfinite alternative is an explicit error field; it
does not silently replace a selected finite result. Physical bytes, offsets,
stream digests and byte ledger are independent of the chosen profile. Dense
projection values, tangent differences, paired membership and derived storage
content identifiers can depend on it. I12/fixed/L4 floats have no base/step
arithmetic; they remain their exact stored binary32 values.

An actual owned native dump of the supplied 12,016-byte Sonic source exposed
this distinction. Input SHA-256 is
`1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4`;
the frozen native-dump JSON SHA-256 is
`c27eaf9a2669ed7164fed7ebd34ec82a54ef65c97b5aeb7634fb46a4a5ca7fa3`.
An independent exact-Fraction/binary32-neighbor oracle explains all 16,017
reported field differences under single rounding, with zero remaining across
594 channels and complete retained sample metadata. The observed native dump
has 7,120 rows, while the default separate-step projection has 7,108. Forty L1
streams change paired-row counts; 1,456 of 3,780 physical L1 sample values and
132 of 453 I4 row values differ between arithmetic profiles. These are decoded
arithmetic differences, not changed source bytes or tolerance acceptance.

For a concrete raw witness, stream `0xcf4` sample 14 has base bits `c22c1a08`,
step bits `3f4840c5`, and quantizer 44. Its exact dyadic value is
`-36100105/4194304`. Separate-step rounding yields `c109b604`; direct single
rounding yields `c109b602`, which matches the observed native value. Extended
precision evaluation is a plausible explanation for that capture; this byte
comparison does not establish a universal architecture/runtime rule. RiiStudio
also uses double intermediates when converting quantized data, but that source
choice alone cannot prove correctly rounded results for every possible input.
Comparisons must explicitly select and disclose their arithmetic profile and
compare exact bits, without substituting a numeric tolerance.

L1/L2 start with `f32 valueStep`, `f32 valueBase`, followed by unsigned 8/16-bit
sample integers. L4 is an array of raw `f32` values. Physical sample count is
always `N+1` in the independently inspected baked layouts, with indices 0..N.
That count must not be replaced with visible diamonds or projected editor keys.

The owned reader projects N samples for a non-looping clip and N+1 for a looping
clip. Its display representation expresses linear interval slopes as Hermite
rows: incoming slope is the preceding sample difference, outgoing slope is the
following sample difference, and an interior change of slope creates paired
same-time rows. The final non-looping encoded sample remains physical owned
data even though this editor projection does not show it. A one-frame clip
still has two physical dense samples. The tool reports physical samples,
projected rows, paired sites and the terminal sample separately. It does not
substitute this editor projection for independently tested game playback.

[BrawlCrate issue #129](https://github.com/soopercool101/BrawlCrate/issues/129)
was inspected on 29 October 2026. The April 2025 report concerns particular
non-looping **Mario Kart Wii** animations and BrawlBox 0.73b; its reporter's
suggested frame-count change was explicitly untested on other games. This is
historical reported evidence, not a reproduced Brawl defect or a qualified fix.
The audit does not adopt that suggestion, append/drop a sample, or change the
current duration semantics. The original MKWii reproduction and external-editor
versions remain an open compatibility case.

## Interpolation, paired sides and signed zero

For two positive-time indexed endpoints `(ta,va,ma)` and `(tb,vb,mb)`, let
`d=tb-ta`, `u=(t-ta)/d`. The scalar Hermite polynomial is
`(2u^3-3u^2+1)va + (u^3-2u^2+u)d*ma + (-2u^3+3u^2)vb + (u^3-u^2)d*mb`.
Its derivatives use the same native frame timebase. A single native row stores
one tangent, so two rows at the same time can represent independent incoming
and outgoing slopes or an actual value jump. The current owned evaluator uses
the first side at an exact key time and the last side for the following span.
The forensic parser preserves stored order and flags equal-value/different-
slope corners separately from different-value pairs. It does not remove sides.

Binary32 `+0` and `-0` compare numerically equal but have different raw bits.
Fixed values, I12 fields, quantizer headers, decoded values and terminal samples
include hex bits in JSON. Quantized integer time/value/tangent fields are also
reported before decoding. A compact format's inability to preserve a particular
signed-zero coefficient is an admission issue, not permission to normalize it.

## User data, aliases and exact byte accounting

The optional v5 user-data block starts with its total `u32` byte length and a
resource dictionary at block+4. Each pointed entry has six four-byte fields:
total byte length, entry-relative data offset, value count, type, entry-relative
name offset, and ID. Type 0 is signed 32-bit integer, type 1 is binary32 float,
and type 2 is a single external string. Numeric payloads must fit the entry's
declared range. Unknown type payloads remain unclassified, with a warning;
their header, extent and raw containing regions are still reported. This
inventory does not edit metadata or silently drop unknown fields.

Different cells, including cells in different bones, may point to the same
stream. RiiStudio's inspected reader maps shared addresses to track indices;
its writer can reference a shared track, and its merge operation compacts
identical tracks. The parser counts exact same-address/same-format aliases
once physically and records all references and logical axis multiplicities.
Same-address conflicting formats and partial overlaps are rejected. It also
finds separate streams with identical format and complete raw bytes, checking
byte equality after digest grouping. These are **alias opportunities**, not
published files or promised net savings. Repointing cells needs separate native
writer, reopen, ownership, metadata, size, root, full-skin and external-runtime
qualification. No such production mutation is performed by this tool.

The ledger partitions every physical byte exactly once into resource header,
dictionary, bone header, fixed cell, reference cell, codec header, curve payload,
typed metadata, observed string length/characters, or unclassified bytes. It
splits regions at the declared resource end and separately reconciles internal
resource bytes and complete physical bytes. Fixed cells are not counted again
as curve rows. Isotropic aliases and shared pointers are not multiplied into
physical payload costs. Per-stream `aligned_writer_bytes` is a hypothetical
four-byte writer allocation, explicitly separate from required raw bytes.
Unknown gaps include their addresses, sizes, digest and zero-filled status;
zero bytes alone do not prove removable alignment. Container compression and
archive placement are outside this standalone accounting.

## Running and limits

From the active checkout:

```text
python tools/chr0_forensics.py input.chr0 --output report.json
python tools/chr0_forensics.py input.chr0 --scalar-arithmetic exact-single-round --output single-round.json
python tools/chr0_forensics.py first.chr0 second.chr0
python -m unittest discover -s tests -p test_chr0_forensics.py -v
```

JSON is deterministic for the same ordered inputs and command paths: sorted
object keys, fixed schema, finite numbers, no timestamps. Every failed file
produces `ok:false` and the CLI returns 1; a same-file output request is refused
before writing. Successful reports are physical observations, not successful
native motion proofs. Input files are opened only for reading. Python callers
select the additive profile through
`parse_chr0(data, scalar_arithmetic="exact-single-round")`; omitted arguments
retain the historical `binary32-steps` values.

Bounds are 512 MiB per physical file, 65,535 dictionary entries, 200,000 unique
streams, two million total decoded rows/samples including dense projections and
numeric metadata, 1 MiB per string and 16 MiB of unique string bytes. All offsets,
counts, products, strings and read extents are checked before use; Python integer
arithmetic does not overflow on malicious offsets. Indexed times must be ordered
and at most two sides are admitted at one time under the current owned contract.
Zero-duration clips, aliased bone tables, mismatched bone dictionary names,
ambiguous flag layouts, conflicting range aliases and typed nonfinite values
are rejected rather than guessed. These strict admission choices do not assert
that every unadmitted historical byte sequence is impossible in every game.

Remaining unknowns include uncommon flag contradictions and bits 5/6 behavior
in actual game runtimes; Patricia lookup ordering validation; unknown metadata
types; arbitrary embedded archive layouts; GPU/CPU evaluation rounding; dense
terminal behavior in untested external players; and exact runtime transform,
scale-compensation and contact semantics. Full model preview, body-volume/skin
quality, roots, contact success, Melee, external plugins and actual games require
their own evidence. Parsing and smaller byte counts cannot supply that evidence.
