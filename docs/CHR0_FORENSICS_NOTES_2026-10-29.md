# CHR0 forensic findings and bounded preparation work — 29 October 2026

The independent physical audit reproduces the existing byte totals for ten
local files. It explains the source/target storage gap and identifies bounded
search opportunities. It does **not** establish a new size improvement, qualify
payload alias rewriting, or replace the finished target with the donor as the
motion-quality baseline. Final new-build benchmarks and application acceptance
are recorded separately after verification.

**Final preview.62 addendum:** the ten-case reports below pin the original
separate-binary32 projection and parser revision. The subsequent exact native
comparison exposed a real arithmetic distinction; it does not alter their
physical byte accounting. The final parser is
`fa985fa6ee18e69d2a7178f0725b75b17dfad50949cb43d722b5eeb37ae558a2`.
The explicit `exact-single-round` interpretation matches all recorded native
coefficient bits, including 7,120 source rows / 2,676 projected pairs instead
of the historical model's 7,108 / 2,664. Both interpretations and raw quantizer
fields remain available; no tolerance or silent default switch is introduced.
See [arithmetic evidence](CHR0_BINARY_FORMAT.md#arithmetic-is-an-explicit-observation)
and the [final acceptance record](CHR0_FORENSICS_ACCEPTANCE_2026-10-29.md).

The user then supplied the authoritative Mario up-air (13,608 bytes) and its
motion PAC. Those replace the older reverse down-air/legacy-motion comparisons
as the primary demonstration. The PAC's canonical up-air snapshot has identical
native channels/flags and compact terminal samples, but 17 I12 FrameScale words
differ by one ULP. Every other byte is identical. Runtime significance remains
unqualified; the new GIF uses the exact supplied standalone source bytes.
No source file or shared codec is changed by this audit. The primary results
retain their held baselines; the new selective search does not materially close
the real file-size gap. Complete measurements and retained quality limitations
are in the acceptance record, rather than claiming a compression fix here.

The format contract and unresolved interpretations are in
[CHR0_BINARY_FORMAT.md](CHR0_BINARY_FORMAT.md). The independent implementation is
[chr0_forensics.py](../tools/chr0_forensics.py), with original synthetic tests in
[test_chr0_forensics.py](../tests/test_chr0_forensics.py). This audit used only
Python byte reads and existing reports: no native application, evaluator,
writer, capture or input injection was used. All ten input hashes remained
unchanged. Raw reports and game fixtures remain local and ignored; no private
asset locations or game data are included in this document.

## Reproducible evidence

The owned source baseline is `01ce42bdea068c0aab83dc7c03a9e0f854a1ad34`.
Primary external source pins are upstream BrawlCrate
`a4c1ee3385010d5c64a88df9c56267c61f213321` and RiiStudio
`c5a7cd8d5e06c39d277d80f8c8514e3c44621836`; exact source links, attribution
boundaries and implementation disagreements are recorded in the format spec.
The inspected RiiStudio reader qualifies its description only for v5. All ten
real files in this corpus are v4; both header versions have independent
synthetic fixtures.

| Evidence | SHA-256 |
| --- | --- |
| Independent parser source | `2b852d628d4bda680de43d0c25a7533c70daa378ed852795e51da90119920313` |
| Original synthetic test source | `32c1a6d48db9d6914ce3d95a19c8ff9cbcf3b2431e1a7c9fc0337ecaead1d24a` |
| Ten-case structured comparison | `44d65b2abc0cd42f306628556991e3484be699ba2da075decf6b05f26f4a008a` |
| Local comparison runner | `d562722b886414593f295974f88d44cfcc3232a42cafec79834b5a32c53a1402` |
| Prior final-output continuity report | `53e03b6b4a78f7e949c6ccb226d1968c9189917feed73fd792f15da2ac5347d4` |
| Remaining-payload inventory | `01001d0bb0a5f2df6e403b930c6dd07a458266e3f24e5baaa24e02614b1c82ae` |

The structured comparison is the local
`Fighterport-payload62/forensics/supplied-comparison.json` artifact. Each full
per-file report has its own digest in that comparison. These identifiers pin
the evidence used here; a subsequent parser/report revision needs a new hash.

The audit reconciles all physical bytes and declared resource bytes separately,
and matches every compared `NativeChr0SizeReport` total, scalar-format count
and fixed-cell count. The seven prepared-output projected key, stored row/sample
and paired-site counts also match the prior independent continuity report.
There were no parse failures, flag warnings or nonzero unclassified regions.
The source's 356 zero-filled unclassified bytes equal the earlier 353 alignment
bytes plus three unclassified bytes. Forward/target/UI files retain 59 such
bytes and reverse files 69. This tool conservatively leaves these gaps
unclassified; equal remaining byte totals do not prove every zero is removable
padding.

## Ten measured files

These are existing preview.61 outputs and supplied references, not results of
the new selective search. Paired sites for sampled source curves describe the
owned editor projection; they are not extra physical L1 sample rows. Duplicate
raw bytes mean separate byte-identical complete streams that might share an
address in a future qualified writer; they are not achieved file savings.

| Case | Physical bytes | Resource bytes | Stored variable rows/samples | Paired sites | Duplicate raw bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Supplied Sonic source, 44 frames | 12,016 | 11,125 | 4,259 | 2,664 projected | 0 |
| Supplied Mario target | 83,864 | 83,344 | 6,676 | 3,135 | 0 |
| Forward Source bone keys | 99,172 | 98,652 | 10,937 | 5,026 | 4,212 |
| Forward Source channel keys | 69,848 | 69,328 | 5,859 | 2,672 | 0 |
| Forward Interpolated keys | 115,016 | 114,496 | 9,272 | 4,044 | 0 |
| Reverse Source bone keys | 188,568 | 187,984 | 15,247 | 4,211 | 39,788 |
| Reverse Source channel keys | 108,616 | 108,032 | 8,923 | 4,054 | 6,336 |
| Reverse Interpolated keys | 96,868 | 96,284 | 7,944 | 3,582 | 6,276 |
| Current actual Use on This Model UI result | 81,140 | 80,620 | 6,676 | 3,135 | 0 |
| Previous UI result, separate physical copy | 83,864 | 83,344 | 6,676 | 3,135 | 0 |

| Input/result identity | SHA-256 |
| --- | --- |
| Supplied Sonic | `1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4` |
| Supplied Mario and previous UI copy, separately read | `c18730a68f73cbcde21655fd6d7e07eefac5b0b12a19c9dd02132359798d60a1` |
| Forward bone | `da2c01e78a43d9c5200a133b0463024c9130b9ba85e45eb9dbcb8a9d9f31752d` |
| Forward channel | `d535a5662aba402403b80b31549a1e74ebadf88a5c4dde8f349e82972979f309` |
| Forward interpolated | `1b0c55d327309c088cb3511f0a6e06e78406ddf32c6eb3004a8a683a79ef5b39` |
| Reverse bone | `c90cc90f87d228415a1592c04142243ef5c99d1a26fcf2a0b90847d4e3beb597` |
| Reverse channel | `2d06349023e62fbfefdb22339f3945efbfba712f2e1bccc0553cd32777aa244e` |
| Reverse interpolated | `3b2556210a72aa39511f5d20d78101b25c1f4b39915f930d519db43476992fb8` |
| Current UI | `582c4b69a438cdd8b5dcf59a411c5a6f437acbe575256e6ff4cd7d95dfd75a45` |

## Where the bytes go

The source has 66 bone entries, 84 L1 streams, 59 I4 streams and five I12
streams. Its actual curve payload is 5,904 bytes and codec headers 1,656 bytes.
The supplied target has 40 bone entries and 118 I12 streams, with 80,112 bytes
of payload and 944 bytes of codec headers. Other physical bytes fall from
4,456 to 2,808. Curve representation, rather than added bone entries, accounts
for the growth. Both source and supplied target have zero exact duplicate
streams under the complete raw header/payload equality contract.

The current UI result still has 118 streams: 112 I12 and six I6. Payload is
77,340 bytes and codec headers 992 bytes, leaving the same 2,808 other bytes.
The 6,676 stored variable rows and 3,135 paired sites are unchanged from the
supplied target. Its paired extra rows cost **36,468 payload bytes**: 2,943 I12
sides at 12 bytes plus 192 I6 sides at six bytes. In the supplied all-I12 target,
the same 3,135 extra sides cost 37,620 bytes. Distinct incoming/outgoing slopes
cannot be deleted merely because an editor shows one diamond at that frame.
The source's 2,664 projected corners consume no separate paired sample rows
inside L1 storage.

The largest exact raw-alias opportunity is 39,788 bytes in reverse Source bone
keys. No source or UI alias opportunity was found, and no existing shared
stream addresses were present in this ten-file corpus. RiiStudio demonstrates
shared-track references structurally, but these measurements do not qualify a
new owned writer, external editor or game round trip. Alias opportunities do
not resolve this UI case and must not be reported as an implemented saving.

## Why compact storage cannot simply be forced

Among the current UI result's 40 animated S/R/T groups, 13 have times that block
I4 and six have times off the signed I6 1/32-frame grid. Seven groups contain
tangents outside I4's range and four outside I6's; categories overlap. There
are 390 fractional time groups, including 118 off the I6 grid. One reviewed raw
example is time 13.0078125, whose product with 32 is nonintegral. Across this
result the observed tangent range is approximately -5687.1357421875 to
3472.899658203125 degrees per frame, compared with I4's `[-64,63.96875]` and
I6's `[-128,127.99609375]` representable tangent ranges. The short intervals and
one-sided slopes are physical data; this audit does not establish their
biomechanical purpose or permit clamping them.

I4/I6 share a format across the varying axes of an S/R/T group, while individual
axes retain independent knot grids. One incompatible axis can retain I12 for
the group. A common XYZ knot grid is an optional candidate, not a format
requirement. Moving or removing a generated repair knot requires a new
continuous certificate. Source bone/channel timing choices must retain their
selected source seeds, while Interpolated may choose knots independently.
Protected source-local controls, unknown/authored pairs, jumps, signed zeros,
joins/ranges and scale semantics remain binding.

L4 avoids compact value quantization: a 44-frame physical rotation stream uses
45 binary32 samples, or 180 raw bytes, without a per-stream codec header. That
is an inventory fact, not a qualified replacement for a fractional Hermite
curve. Resampling loses authored fractional knot timing and can alter extrema,
derivatives and paired-side behavior between integer samples. The current
generated dense proposal path offers L1/L2; the forensic tool also decodes L4
but adds no new L4 optimizer proposal. Any future L4 proposal needs the original
finished Dense target as its reference, correct group cells/relative pointers,
complete `N+1` terminal ownership, fresh decode, unchanged continuous value and
derivative certificates, both linear and owned Hermite-projection checks,
cumulative native FK/all-skin gates, and separate runtime/editor qualification.
Neither raw float samples nor a successful reopen supplies those proofs.

## Current write-path boundaries

| Route | Current implementation and ownership |
| --- | --- |
| Plain project Save | `ModelForm.FighterStudio.cs` → `SaveFighterProject`: finish the pending native curve edit, promote dirty CHR0s to the precise policy, retain current channels/storage where valid, merge owned roots and export working resources. It does not invoke optimization or apply a saved lossy policy. |
| Ordinary native right-click Export | Native export callback → `ExportFighterNode` → `TryExportOwnedAnimationCopy` in `ModelForm.ExportOptimization.cs` → `FreezeOwnedReferenceExport` and `ExportWorkspaceSnapshot`. This exports the exact current clip through an independent copy with source/destination, stale, fresh-decode and atomic publication guards. It does not prepare optimization, require a target model for ordinary CHR0, or substitute the retained pre-Apply baseline. Native Maya conversion remains the separate virtual converter/picker route. |
| Retarget preparation | `BrawlRetargeter.cs` prepares the original Next target; `BrawlRetargeter.Adaptive.cs` writes the required target curves and paired sides, with shared membership handled separately. `RetargetKeyframes.Prepare` → `QualityCandidate` constructs the selected schedule, continuously certifies it against that Dense target, performs complete native validation and tries `PrepareCompact` against the same master. The donor is never a compression target. |
| Fitting | `ModelForm.Transitions.cs` uses `CopySource` before boundary edits, `ApplyBoundaryTracks`, native fit verification and `PrepareExactStorageCopy`. The editable fit remains a complete numerical master; only bit-exact storage proposals are automatic. `NativeTransitionFit.cs` retains the saved fitting/reference contract. Imported terminal payloads and untouched matching compact plans remain owned data. |
| Contact authoring | `ModelForm.ContactAuthoring.cs` freezes the original/contact receipt, requests the worker result, replaces the permitted body-rotation interval with `NativeControlSnapshot.Write`, promotes the candidate, exports/reopens it and calls `contact-verify` before applying a new revision with native Undo. It does not call the explicit size optimizer or claim compressed-contact acceptance. This path has its own preview/export lifecycle and is not newly qualified by the read-only audit. |
| Explicit optimization | `ModelForm.AnimationOptimization.cs` freezes the current resource and original session baseline; `ModelForm.ExportOptimization.cs` freezes model/protections and fit/contact eligibility. `ModelForm.GeneratedOptimization.cs` invokes Core deletion/refitting and independent complete alternatives, then `PrepareReduction` / `PrepareCompact`. Core certificates, fresh native decode, cumulative geometry, complete bytes and the retained fallback decide. Explicit Apply uses the existing same-clip Undo command; ordinary Save then preserves that deliberately applied document. |
| Motion PAC and reviewed exports | `BrawlRetargeter.ExportMotionPac` uses an independent source-aware current copy plus template/target/sibling validation. Verified workspace/set/slot policy routes remain explicit and use their frozen master/dependency checks. The preview.60 ordinary native-export override is not reversed by those policy routes. |

The precise writer uses I12 for unmatched derived curves and retains an attached
indexed/sampled plan only while its complete coefficients and ownership match.
Global legacy codec heuristics are not changed. Complete model hierarchy,
bind/inverse-bind data, all skin coefficients, full native previewer, source-local
root ownership and original Next remain outside this storage proposal work.

## Bounded selective preparation implementation

The new `RetargetKeyframes.Selective.cs` proposal is an additional **at most four
complete inner compositions** after the established precision/packing route has
already retained a fully verified baseline. It combines complete channels from
the first coarser Dense-certified wire and that retained baseline wire. A typed
native failure identifies the actual bone/ancestor chain; subsequent implicated
branches are found through the real hierarchy, without character/action/name
exceptions. It does not fit numerical coefficients to an accepted approximation
or change the motion reference.

Each complete decoded composition must exactly match its selected immutable
wire coefficients, channel membership, source seeds and relevant flags, thereby
retaining the original Dense scalar proofs. The complete native cumulative gate
still compares it with the original finished Dense target. Optional packing
uses the same original-target certificate and remaining shared budgets. A
candidate replaces the held baseline only with strictly smaller physical bytes
and non-increasing resource bytes. Failure, exhaustion or lack of saving retains
that qualified baseline.

The additional four-attempt bound grants no extra scalar or native allowance:
the existing two-million proof-work / 100,000 span limits and native 100,000
witness / 10,000,000 bone-visit / 1,000,000,000 skin-vertex-visit ceilings remain.
It introduces no global codec change, tolerance relaxation, new mode, automatic
lossy Save, root/weight edits or global-optimum claim. Implementation presence
is not an acceptance result. Root-owned benchmark and background application
evidence must establish actual behavior and any new saving before promotion.
