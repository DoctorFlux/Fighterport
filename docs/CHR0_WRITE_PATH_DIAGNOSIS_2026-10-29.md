# CHR0 write-path diagnosis — measured retarget payload and BEFORE controls

**The retargeted size gap is primarily curve payload, even when keyed times
match.** HipN XYZ rotation has exactly the same 44 distinct time sets in Sonic's
source and both tested Mario results, yet its payload grows **135 bytes in L1
to 2,604 bytes in I12**. Across the complete clip, source payload is 5,904 bytes;
the direct target uses 66,024 bytes and the actual **Use this on the model**
target uses 77,340 bytes. Bone/name/metadata overhead shrinks. Matching visible
markers does not retain the source's sampled encoding or make paired
value/tangent rows equally cheap.

The unedited-export experiment is a separate control: plain promotion/worker
export expands Sonic's 12,016-byte clip to 87,600 bytes and Mario's 13,608-byte
clip to 49,228 bytes without retargeting. Source-aware copying confines a
one-key edit's fallback to its changed group. Correcting that write path alone
does not resolve the finished retargeted payload gap.

Original donor comparisons below are **storage context only**. Compression
quality must still be measured against the same finished full-quality target
master, including proportions, root motion, fitting and reviewed edits.

The static source trace and the parent-owned native diagnostic runs start at
`d82c3f709b6d8526d11243bc0d4b6be98b75f9ad` (preview.62). The reported historical
revision `1cc96940` is not an object in this checkout (`git cat-file -t 1cc96940`
fails), so an exact historical diff has not been invented. Its findings are
rechecked against current code below. No historical checkout is selected or
published. Source references below identify this starting revision; the traced
production files were unchanged when inspected. Native diagnostic outputs and
the independent read-only byte comparison are separately pinned below. These
BEFORE results diagnose failures; they do not qualify a correction.

The separate public-GitHub lookup on 29 October 2026 requested full revision
`1cc96940bb58a6bab2f5a5c6ab9fff03e3d4165d` through `fetch_commit` for
`DoctorFlux/Fighterport`. The upstream
[`GET /repos/DoctorFlux/Fighterport/commits/1cc96940bb58a6bab2f5a5c6ab9fff03e3d4165d`](https://api.github.com/repos/DoctorFlux/Fighterport/commits/1cc96940bb58a6bab2f5a5c6ab9fff03e3d4165d)
returned HTTP 422, “No commit found for SHA”. That is a dated lookup outcome,
not inspected historical source or evidence of an exact historical diff.
The production trace remains the active `d82c3f7…` baseline and its current
working-tree correction; no historical repository was selected.

Ignored local artifacts also hold an **unqualified pending** sampled-codec
proposal, quarantined outside the active source. It is not a passing
implementation, is not included in this diagnosis's current baseline, and has
supplied no acceptance evidence. This document does not promote it or recommend
changing native approximation limits.

## Retargeted files: actual same-marker payload causes

The independent parser and storage comparator read four existing preview.62
outputs with explicit `exact-single-round` arithmetic. The evidence is in the
ignored outer workspace `artifacts/Fighterport-write63/accounting-retarget-context/`.
Each case contains its full per-entry/S/R/T comparison, source/target inventory
and exact decoded source-time membership report. Original and existing target
files remained unchanged. No writer, retargeter, optimizer or native process
ran for this added analysis.

| Existing finished target | Original → target physical bytes | Original → target curve payload | Original → target entries | Target logical native rows |
| --- | ---: | ---: | ---: | ---: |
| Sonic down-air → Mario, direct SourceChannel | 12,016 → 69,848 | 5,904 → 66,024 | 66 → 40 | 6,095 |
| Sonic down-air → Mario, actual Use button SourceChannel | 12,016 → 81,140 | 5,904 → 77,340 | 66 → 40 | 6,912 |
| Provided Mario up-air → Sonic, SourceBone | 13,608 → 83,064 | 8,052 → 78,900 | 58 → 41 | 8,613 |
| Provided Mario up-air → Sonic, SourceChannel | 13,608 → 105,816 | 8,052 → 102,036 | 58 → 41 | 8,729 |

For Sonic's direct and Use results, the physical file grows by 57,832 / 69,124
bytes while curve payload grows by 60,120 / 71,436 bytes: all other categories
combined **shrink by 2,288 / 2,312 bytes**. For the two Mario-source results,
payload grows by 70,848 / 93,984 bytes while the rest shrinks by 1,392 / 1,776
bytes. Larger names, dictionaries or extra tail tracks are not the dominant
cause in these files.

The specific same-marker observations are directly reproducible from decoded
time bits and actual serialized streams:

| Mapped rotation group | Source and target distinct XYZ time sets | Source payload | Target payload | Physical representation |
| --- | ---: | ---: | ---: | --- |
| Sonic HipN → Mario HipN, direct and Use | Identical, 44 each | 135 | 2,604 | Three L1 streams / 135 stored samples → I12 / 217 stored value-tangent rows. |
| Provided Mario LFootJ → Sonic LFootJ, Bone and Channel | Identical, 34 each | 105 | 2,376 | Three L1 streams / 105 stored samples → I12 / 198 stored value-tangent rows. |

These are scalar payload bytes; codec headers and entry slots are separately
accounted. Non-looping L1 streams physically contain 45 / 35 samples here,
including their terminal data. The native decoder projects that compact
sampled motion into rows with separate incoming/outgoing slopes where needed.
For example, Sonic's HipN rotation projects 212 source rows from 135 physical
samples, whereas Mario stores 217 I12 rows. Same keyed times therefore do not
mean either the same number of stored rows or the same cost per row. Those
additional tangent sides can be required for motion between markers; this is
not permission to discard them.

The complete format inventories explain most of the allocation:

- Sonic's source has 84 L1 streams with 3,780 terminal-inclusive physical
  samples occupying 3,780 payload bytes, alongside 59 I4 and five I12 streams.
  The direct target instead has 109 I12 streams / 5,145 rows / 61,740 payload
  bytes, plus nine I6 streams / 4,284 payload bytes. The actual Use result has
  112 I12 streams / 6,214 rows / 74,568 payload bytes, plus six I6 streams /
  2,772 payload bytes. Both targets have fewer variable logical channels than
  the complete source: 118 versus 148.
- The Mario up-air source mixes 98 I4, 36 L1, 17 I12 and three I6 streams.
  SourceBone's target has 131 I12 streams / 6,047 rows / 72,564 payload bytes
  plus 24 I4 constant streams / 6,336 payload bytes. SourceChannel's target
  has 131 I12 streams / 8,503 rows / 102,036 payload bytes. It has fewer
  variable logical channels than the source, 131 versus 154, despite the much
  larger allocation.

Actual indexed extra knot-side rows contribute 30,174 bytes in Sonic's direct
target, 36,468 in Use, 36,804 in Mario's SourceBone result and 47,940 in its
SourceChannel result. These totals count physical I4/I6/I12 side-row payload
once, including sharing; they are **not potential savings**. The source L1
decoder's virtual paired rows do not each consume an indexed payload row.
Unknown/required paired sides, exact root controls and native semantics remain
protected.

Near-equal total native row counts can also conceal different allocations.
Mario's SourceChannel result has only 116 more logical rows than SourceBone,
but is 22,752 bytes larger. Its rotation rows increase from 5,184 to 7,426;
scale rows fall from 1,685 to 597 and translation from 1,744 to 706. Rotation
payload rises 26,904 bytes, scale falls 600 and translation falls 3,168:
net payload +23,136, codec headers -384, with fixed/reference slot changes
canceling. This is an actual byte reconciliation, not a key-count estimate.

### Source-channel label and fallback transparency

The files do **not** preserve every donor channel's time set globally. Using
the previously recorded reviewed source/target mapping, and keeping its root
exclusions, the direct Sonic result has identical time sets for 47 of 98
variable-source channel pairs; Use has 26 of 98. The remaining observations
include required target-space repairs and canonical fixed values. Across all
369 mapped non-root channel pairs, the direct and Use targets contain 729 /
1,083 channel-time groups absent from the corresponding source channel.
These numbers describe schedules; they do not prove which keys are removable.
The actual Use case reuses mapping pairs only after verifying its recorded
source/target rig fingerprints match the direct case.

For provided Mario up-air, SourceBone has 488 target rows at times outside its
source-bone union schedule. SourceChannel has 4,991 rows at times absent from
the corresponding donor channel, spanning 2,667 channel-time groups. Its
native receipt reports `SourceSeedKeys=8729`, `AddedQualityKeys=0`,
`DenseFallback=false`. **Zero added-quality keys is not a source-cardinality
guarantee.** After four strict-quality attempts failed, the recorded fifth
attempt retained complete full-quality target paired channels and accepted a conservative
baseline before whole-dense fallback. The final seed count includes that
retained target wire; it does not count only original donor keys.
`RetargetKeyframes.Quality.cs:125–126`, `:173–176`, `:193–201` records those
distinctions in the pinned source. The mode name and `DenseFallback` flag alone
must not conceal this behavior from the user.

The source-to-target reports include raw semantic flags and group existence,
use-model, fixed and isotropic state. An absent source channel becoming a
target fixed value is distinguished from becoming a variable stream. For
example, the Sonic mappings have 57 absent→fixed channels, not 57 dense
animated channels. Cross-rig flags and rest-valued behavior cannot be declared
equivalent from a source-file comparison alone. The finished target master
remains the quality reference; original source size is not an acceptance bound.

The retarget-context summary SHA-256 is
`2a1b5af3a3d5c2640a4ce785a994c613d6ebd8ebe087d504f7e996f53545e392`.
The direct 69,848-byte target is pinned in the finished-target table below;
the actual Use target is
`582c4b69a438cdd8b5dcf59a411c5a6f437acbe575256e6ff4cd7d95dfd75a45`,
and Mario's 105,816-byte SourceChannel result is
`47c9d92fc2195627bce07ac63aaaa7311960d2769152292dfb7db4bb447bc5cb`.
The provided fixtures establish these causes, not that every Brawl animation
uses sampled storage or has the same reducible structure. No new compression
percentage, continuous full-model quality, editor/runtime compatibility or
game acceptance follows from this read-only inventory.

## Starting-revision causal stages

1. **Numerical master construction is dense.**
   `BrawlCrate/Studio/Retargeting/BrawlRetargeter.cs:380–441` first evaluates
   the existing solver at eight samples/native frame, then creates an entry
   for each temporary target bone (`:418–426`). Each of the nine S/R/T channels
   gets a key at every integer frame (`:427–435`), with fitted tangents and
   `PreserveInterpolation=true` (`:423`). This is the full-quality editable
   master construction, not a size-aware export pass.
2. **Fractional master repair can add rows.**
   `BrawlRetargeter.Adaptive.cs:60–82` replaces supported generated curves with
   adaptive samples and stores different incoming/outgoing slopes as paired
   same-time rows. It again preserves interpolation and promotes the output.
   A visible keyed time can therefore cost two I12 rows. Distinct tangents are
   motion data; deleting a side is not an exact cleanup merely because the
   values at the two sides match.
3. **The generic native size pass classifies fixed channels by row count.**
   `BrawlLib/Wii/Animations/AnimationConverter.cs:508–514` uses
   `isExist = count > 0` and `isFixed = count <= 1`. It does not test a complete
   multi-key Hermite function for constancy. Consequently, a channel with 17
   identical values and zero tangents remains a variable channel until an
   earlier cleanup actually replaces its 17 rows with a fixed singleton.
   Equal values with nonzero tangents are not constant and cannot use that
   substitution.
4. **PreserveInterpolation changes the physical format choice.**
   `AnimationConverter.cs:727–735` selects I12 for a nonempty animated group
   if its collection preserves interpolation or has fractional knot times.
   The format belongs to the whole S/R/T group, not independently to X/Y/Z.
   The I12 allocation is `8 + 12*K` per variable scalar (`:743–748`), in
   addition to the bone's value/offset slots and resource-level overhead.
   The writer emits those rows at `:1062–1070`. It does not remove duplicate
   constants as part of that encode.

These stages explain why fixing row counts/constant representation **before**
encoding is distinct from merely selecting another codec. For example, 17 I12
rows need 212 scalar-payload bytes. A proven frame-zero fixed singleton uses
the already-present four-byte value cell and has no animated payload; its
resource-level saving must still be measured with actual group/isotropic layout.
This example is stream-cost arithmetic; the measured fixture inventory appears
below.

## Starting-revision application policy is not universally I12

The application-owned precise writer is
`BrawlLib/SSBB/ResourceNodes/Animations/PreciseCHR0EntryNode.cs`.

- `OnCalculateSize` (`:108–148`) defaults an unmatched group to I12 but retains
  an explicit matching `IndexedCHR0EntryStorage` group (`:122–126`). It keeps
  exact scale-sharing semantics, noncanonical singleton times/tangents and
  one-frame sampled terminal payloads separate. It records an immutable size/
  write plan, rather than repeating a codec heuristic during rebuild.
- `OnRebuild` (`:150–179`) validates that plan, then writes retained matching
  indexed payloads or precise I12 rows. Explicit sampled plans have a separate
  complete-terminal layout. It is incorrect to describe this current class as
  incapable of preserving compact storage.
- `NativeAnimationOptimization.CopyNativeSnapshot` at
  `BrawlCrate/Studio/NativeAnimationOptimization.cs:181–209` captures imported
  storage before replacing entries, attaches matching plans at `:202–205`,
  then promotes the complete copy. `CopySource` uses this source-aware route.
  Missing/unbounded imported sampled payloads fail closed.
- `NativeAnimationOptimization.ExactStorage.cs:24–57` similarly captures all
  plans before entry replacement, retains imported dense terminal payloads,
  and proposes bit-identical I4/I6 storage separately from curve changes.
  This existing optimization is not permission to quantize untouched curves.

However, **plain entry promotion itself does not capture those plans**.
`PreciseCHR0EntryNode.MakePrecise` (`:81–92`) copies semantic flags and the
existing key collection. `Promote` (`:95–105`) calls it for every non-precise
entry in the clip, without first calling `CaptureStorage`. Plain imported
entries can therefore lose their original storage choice when a direct caller
promotes the entire clip. Already precise entries are skipped and retain their
explicit plans. Actual consequences depend on entry ownership and dirty state;
this trace alone does not establish a measured size increase for every caller.

## Starting-revision direct callers and qualification boundaries

| Current caller | Static behavior / required distinction |
| --- | --- |
| Project Save, `BrawlCrate/UI/ModelForm.FighterStudio.cs:513–519` | Every dirty/branch CHR0 is promoted before its root merges/exports. For a dirty clip with plain imported children, this can replace unaffected compact entries without carrying their plans. A clip already owned by precise snapshots follows a different path. Test one edited group plus untouched compact siblings; do not infer preservation from unchanged scalar values alone. |
| Native FK/IK/control Apply, `ModelForm.AnimationControls.cs:340–343` | Supplied tracks are rewritten and the complete clip is promoted. Its snapshot Capture/Restore path at `:319–338` **does** retain storage in snapshots, so Undo replay and first direct promotion must be distinguished. |
| Fitting, `ModelForm.Transitions.cs:139–151`, `:214–240` | Current fitting reopens its own source snapshot, uses `CopySource`, edits boundary channels on that independent owner, then offers exact storage choices. It does not import the worker's all-I12 animation as its final native clip. Untouched already-precise plans can survive. Changed groups intentionally fall back to I12 until separately qualified. Do not blame every current fit on the worker promotion path. |
| Contact editing, `ModelForm.ContactAuthoring.cs:25–28`, `:42–54` | The source and preview roots are exported to snapshots, the candidate reopens the motion snapshot, permitted body rotation intervals are changed, and the entire candidate is promoted. This route does not explicitly run the source-aware CopySource transfer before promotion. Its own dirty/snapshot/untouched-group behavior needs a fresh file comparison. |
| Explicit optimization Apply, `ModelForm.AnimationOptimization.cs:220` | Snapshot restoration precedes promotion. Current snapshots carry matched storage, so this is not automatically the same loss as plain imported entry promotion. |

The actual standalone CHR0 inheritance is `CHR0Node` → `NW4RAnimationNode` →
`BRESEntryNode`. `CHR0Node.Export` delegates its binary route at
`CHR0Node.cs:196–208`; `BRESEntryNode.Export` calls Rebuild and writes the external
string table/postprocessing at
`BrawlLib/SSBB/ResourceNodes/Archives/BRRESNode.cs:1129–1157`.
`ResourceNode.Rebuild(false)` returns without curve reconstruction when the node
is clean (`ResourceNode.cs:1035–1041`). Thus “every Export always rewrites every
curve” is too broad, and “clean standalone export must be byte-identical” also
does not follow: names, pointers and standalone footer are separate writes.
Dirty CHR0 rebuild forces child rebuilding at `CHR0Node.cs:298`; dirty
descendants, copies, merge/postprocessing and explicit promotion need actual
route-specific evidence. Inherited `Export(FileStream)` copies working bytes
(`ResourceNode.cs:958–966`) and is a distinct operation used by the existing raw
ARC/BRRES visual-context staging policy. It is not a replacement for standalone
CHR0 export with its required external names.

## Starting-revision worker policy: a separate all-I12 boundary

The current path is `engine/src/FighterPort.BrawlWorker`, not a substituted old
application. Its own `PreciseChrEntry` type is different from the application
writer above:

- `PreciseChrEntry.cs:15–27` replaces every non-precise entry and copies flags/
  key collections, without an imported immutable storage plan.
- `:30–41` forces **every variable S/R/T group** to I12, allocating
  `8+12*K` per variable scalar. `:44–59` writes those exact rows and rejects a
  non-I12 derived format. It does not collapse constant multi-key functions.
- `Program.cs:68–98` handles `export`, `insert`, and `create`. A replacement
  clip is newly allocated at `:73`; non-replacement clips are all promoted at
  `:76`. Every missing track created for edits is `new PreciseChrEntry` at
  `:91`. These are the two paths by which the worker selects this policy.
- The adapter issues these requests from
  `engine/src/FighterPort.Adapters.Brawl/BrawlAdapter.cs:74`, with ReplaceTracks
  chosen for a new/inserted/retargeted/new-resource result and RewriteChannels
  requested separately. This source-level reachability is not proof that every
  GUI feature publishes the resulting worker resource.
- `Program.cs:104–105` checks edited key values/tangents and entry semantic
  flags after reopen. Such checks do not by themselves prove unchanged codec
  payloads, native physical bytes or untouched source curves.
- `NativeMotionWriter.cs:20–61` handles the separate `save-motion` route. It
  edits the selected native channels and exports the root without calling the
  above all-entry promotion. That route must not be described as the same
  operation as `export`/`create`/`insert`.

The worker project references an explicitly configured BrawlLib binary directory
(`FighterPort.BrawlWorker.csproj:2–5`). A source policy change requires a build/
dependency pin and fresh worker route tests; native application tests alone
cannot establish which worker binary was exercised.

## Starting-revision cleanup and membership guards

The dense construction temporarily creates all target bones, but current output
is filtered at `BrawlRetargeter.cs:441` by
`BrawlRetargeter.SharedBones.cs:19–53`. Only actual source/target mappings can
authorize retained target tracks. Non-mapped temporary rest tracks are removed
only after exact bind-value/zero-tangent/semantic checks. “All target-only bones
are still exported” is not the current source behavior.

For non-Dense retarget modes, `RetargetKeyframes.Quality.cs:283–292` already
allows exact bind-track omission under its frozen model contract.
`QualityCurve` at `:321–335` collapses a completely equal-value, positive-zero
tangent channel to a frame-zero singleton, except for its earlier exact-control
path. The explicit optimizer has a separate `ExactFixedChannel` guard in
`NativeAnimationOptimization.Constants.cs:12–20`, including signed-zero and
complete-function checks. Protected curves, mandatory authored sides and
source-local control contracts remain restrictions on any cleanup.

Therefore the safe first implementation is not “turn off I12” or “increase the
codec error threshold.” It is to preserve valid untouched plans, inspect the
actual selected route, and offer proven constant/redundant cleanup on an
independent result while retaining I12 for remaining curves. A changed key
count is still not a file-size result. Every accepted candidate needs full
metadata/root/flag/terminal and native scalar/geometry verification, actual
serialized byte counts, fresh reopen, and unchanged original assets. The BEFORE
measurements below establish which tested routes need correction; they do not
remove those acceptance requirements.

## BEFORE execution and evidence pins

The native harness produced 12 saved/reopened route outputs for each of two
original clips. Its records declare a private desktop, no injected OS input,
no screen copying, no shared live document and no requested optimization.
The worker records independently identify an executed binary, exit code 0,
zero requested samples and no desktop input/capture. The native process was
x86 (`PointerBytes=4`), CLR `4.0.30319.42000`.

The independent audit read those existing output bytes; it did not invoke a
native writer, evaluator, renderer or application. All reports explicitly use
`--scalar-arithmetic exact-single-round`. This names the decoder's arithmetic
profile; it is not a mathematical losslessness or continuous-pose claim.
Its scenario label is caller context only. Actual operations are established
by the separate native route records, not by labeling two files `one-key`.

All paths in this section are relative to the ignored outer workspace
`artifacts/Fighterport-write63/`; no private game asset is committed.

| Evidence / executed component | SHA-256 |
| --- | --- |
| BEFORE application | `88c4036669235686f2727f7c0078b529ef22706d2f5632ad2f2f5a96ff01d7b1` |
| BEFORE BrawlLib | `199cdbce8bbd288cfcae211f8e6c4b471ee6600e116afbebe0992e2dac8f00e9` |
| Executed BEFORE native worker | `bcc5b06b4e8461a3bc838769c274af78fdaf84d9f346b66f9ab722aae976005c` |
| `before-sonic/write-path-results.json` | `20e30729352426e930b975c1c3ada10a02c013386203431245e8151d39bec989` |
| `before-mario/write-path-results.json` | `398d75266ab017318efc09bc1d6326fa768c67ac2f3aa6cdfd73f601e6ed5372` |
| `accounting-before/summary.json` | `7e26b300411a00b3fda8c8a8e17c130f96a7d53110476e6bf3c9c4572fea06bc` |
| Diagnostic harness source, `tests/ControlsAcceptance/NativeChr0WritePathChecks.cs` | `e3c55fc5a725d7ba4e5fdcf3034b6e35572a5552642b0e79854aaecdbfa31206` |
| Read-only comparator, `tools/compare_chr0_storage.py` | `7d50f86b11640faa3f151ef8eb4d65f42c9beeabd9885f9943876863fbb34693` |
| Independent parser, `tools/chr0_forensics.py` | `fa985fa6ee18e69d2a7178f0725b75b17dfad50949cb43d722b5eeb37ae558a2` |
| Comparator tests, `tests/test_chr0_storage_comparison.py` | `b3992220788b0a64915884fce05f025e6bbc479c0d1324fa155e502627ea4206` |

The diagnostic source is an added test harness, not a file attributed to the
starting commit. Its hash pins the source used for this BEFORE report; later
qualification additions need their own revision/build pins.

| Original fixture | CHR0 bytes / version / frames | Entries | CHR0 SHA-256 | Intended model SHA-256 |
| --- | --- | ---: | --- | --- |
| Sonic AttackAirLw | 12,016 / v4 / 44, non-looping | 66 | `1298044fc7202fc67c952424eab95c4a62a947c476376fb95805d2bef867bfb4` | `51e9a8b4f51911dc378ab612934f547339b1e557f04d82290c76fcba1350bad1` |
| Provided Mario AttackAirHi | 13,608 / v4 / 34, non-looping | 58 | `93559d59573ed7bd6612bdd3a2b7daa06d1df457982884e281f55139f1ebb9fc` | `e73cc6f7c00cebe5587bda20bd7484d9dc49fb32ffddb5663c0284fc16fe62a6` |

All four original clip/model hashes match before, after and the independent
audit's current read (`OriginalInputsUnchanged=true`). These two v4 fixtures
are evidence for the tested routes, not a qualification of every CHR0 version,
costume, game or action.

## BEFORE route results

The single native edit is HipN rotation X at native index 0, adding 0.25 degrees
while retaining its tangent. Sonic changes -27.3806553 to -27.1306553 with
tangent -15.6447659; Mario changes -13.3432016 to -13.0932016 with tangent
-25.25. These are diagnostic fixture choices, not production exceptions.

| Actual route | Sonic physical bytes | Mario physical bytes | Observed route distinction |
| --- | ---: | ---: | --- |
| `native-no-edit` | 12,016 | 13,608 | Both outputs byte-identical to the original standalone files. |
| `native-one-key` | 84,448 | 41,248 | Native rebuild expands untouched groups and changes additional decoded scalar fields. |
| `copy-source-no-edit` | 12,016 | 13,608 | Compact plans retained; Mario has the separate I12 header-word caveat below. |
| `copy-source-one-key` | 14,416 | 13,936 | Only the changed rotation group's encoding falls back to I12. |
| `promote-no-edit` | 87,600 | 49,228 | Plain promotion expands all variable groups to I12. |
| `promote-one-key` | 87,600 | 49,228 | Same representation expansion, including untouched groups. |
| `snapshot-restore-no-edit` | 12,016 | 13,608 | Storage retained, but Sonic's metadata changes. |
| `snapshot-restore-one-key` | 14,416 | 13,936 | Storage effect matches source-aware copy; metadata caveat remains. |
| `native-worker-no-edit` | 87,600 | 49,228 | Executed native worker reproduces the all-I12 expansion. |
| `native-worker-one-key` | 87,600 | 49,228 | Same all-I12 policy after an edit. |
| `control-worker-no-op` | 87,600 | 49,228 | No control edit committed; an exported worker copy still expands. |
| `control-worker-body-edit` | 87,600 | 49,228 | One returned HipN track committed; the exported copy still expands. |

Every row is `SavedAndReopened`. Reopen alone does not make it a passing
preservation test:

- `native-one-key` changes **9 additional scalar channels / 26 ordered tuple
  fields** in Sonic, and **11 additional channels / 82 fields** in Mario,
  relative to the frozen expected edited state. The raw-original byte audit
  counts 10 and 12 changed channels respectively because it also includes the
  one intended edit. These counts must not be interchanged.
- Plain promotion and native worker routes have zero additional ordered
  scalar-tuple differences in these fixtures, but lose original compact
  storage. Sonic additionally changes `ScalingRule` from 2 to 0. Sonic's
  snapshot-restore route also resets that metadata despite retaining the small
  file size. Mario's corresponding header/user metadata remains identical.
- Source-aware copy reports exact native ordered fields and header/user
  metadata against its prepared expected state. For Mario, no-edit output has
  the same 13,608 bytes but SHA-256
  `d360bf4f73ae084f0dc756febb287f0fb199d2a1737d396d313d4fb1c770569a`:
  17 I12 `FrameScale` words change by one ULP; other bytes are equal. The
  native ordered-key fingerprint does not include this raw header word.
  Runtime significance is unverified; this is not byte-identical preservation.
- Control no-op records commit nothing; body-edit records exactly one changed
  authoring channel/field. Their distinct `SerializationDifference` records
  show zero further scalar changes, with Sonic's metadata failure and Mario's
  identical header/user metadata. A missing generic `Difference` field in
  these records is not evidence of exactness.

No tested original-clip route adds/removes a bone entry or materializes a
previously absent S/R/T channel. Thus these measured no-op size increases come
from storage conversion, not from retargeting to additional bones.

## Serialized byte reconciliation

The independent parser accounts for each physical stream once, even when
isotropic axes or several references share it. Per-entry/group reports retain
the aliases and identify the first dictionary-order allocation owner; that is
accounting attribution, not native ownership or a causal claim. Logical channel
counts, stored scalar records and projected native key counts remain separate.

The original standalone byte ledgers reconcile exactly:

| Physical category | Sonic bytes | Mario bytes |
| --- | ---: | ---: |
| CHR0 resource header | 40 | 40 |
| Resource dictionary | 1,080 | 952 |
| Bone entry headers | 528 | 464 |
| Fixed value cells | 1,076 | 656 |
| Animated offset cells | 592 | 616 |
| Scalar codec headers | 1,656 | 2,040 |
| Curve key/sample payload | 5,904 | 8,052 |
| String length words | 268 | 236 |
| String characters / terminators | 516 | 431 |
| Unclassified bytes | 356 | 121 |
| **Physical standalone total** | **12,016** | **13,608** |
| Header's declared resource length, a distinct measurement | 11,125 | 12,859 |

Unclassified regions in these originals are zero-filled. They are reported as
observed gaps, not proven removable alignment/padding. External names and the
declared resource boundary explain why the resource-size header is not the
standalone file length. No PAC/BRRES repack size or outer archive-compression
saving is measured here.

The following are unique physical streams. Payload excludes codec headers,
entry slots and file overhead. L1 sample counts are physically read, including
the stored terminal sample: 45 per Sonic stream and 35 per Mario stream,
although these non-looping clips display 44 and 34 frames.

| Fixture / representation | Format | Streams | Stored rows or samples | Codec headers | Payload |
| --- | --- | ---: | ---: | ---: | ---: |
| Sonic original | I12 | 5 | 26 | 40 | 312 |
| Sonic original | I4 | 59 | 453 | 944 | 1,812 |
| Sonic original | L1 | 84 | 3,780 | 672 | 3,780 |
| Sonic plain promotion / worker | I12 | 148 | 6,851 | 1,184 | 82,212 |
| Sonic source-aware one-key | I12 | 8 | 238 | 64 | 2,856 |
| Sonic source-aware one-key | I4 | 59 | 453 | 944 | 1,812 |
| Sonic source-aware one-key | L1 | 81 | 3,645 | 648 | 3,645 |
| Mario original | I12 | 17 | 131 | 136 | 1,572 |
| Mario original | I4 | 98 | 1,257 | 1,568 | 5,028 |
| Mario original | I6 | 3 | 32 | 48 | 192 |
| Mario original | L1 | 36 | 1,260 | 288 | 1,260 |
| Mario plain promotion / worker | I12 | 154 | 3,710 | 1,232 | 44,520 |
| Mario source-aware one-key | I12 | 20 | 175 | 160 | 2,100 |
| Mario source-aware one-key | I4 | 95 | 1,213 | 1,520 | 4,852 |
| Mario source-aware one-key | I6 | 3 | 32 | 48 | 192 |
| Mario source-aware one-key | L1 | 36 | 1,260 | 288 | 1,260 |

The complete file deltas reconcile without attributing growth to names or bones:

| Route / fixture | Curve payload delta | Codec header delta | Unclassified delta | Physical total delta |
| --- | ---: | ---: | ---: | ---: |
| Plain promotion / worker, Sonic | +76,308 | -472 | -252 | **+75,584** |
| Native one-key, Sonic | +72,684 | 0 | -252 | **+72,432** |
| Source-aware one-key, Sonic | +2,409 | 0 | -9 | **+2,400** |
| Plain promotion / worker, Mario | +36,468 | -808 | -40 | **+35,620** |
| Native one-key, Mario | +27,772 | -96 | -36 | **+27,640** |
| Source-aware one-key, Mario | +352 | -24 | 0 | **+328** |

All other ledger-category deltas are zero in these six comparisons. Sonic's
plain promotion changes 28 L1 rotation groups and 26 I4 rotation groups to
I12. Mario's changes 12 L1 rotation groups, 32 I4 rotation groups, two I4 scale
groups and one I6 scale group. Native one-key changes all 28 Sonic L1 rotation
groups; for Mario it changes all 12 L1 rotation groups plus four I4 rotation
groups. Source-aware one-key changes just **one** group, L1-to-I12 for Sonic
and I4-to-I12 for Mario, as required by the group-wide encoding layout.

The originals have 4,259 / 2,680 physical variable records but 7,120 / 3,874
projected native keys respectively. Promotion preserves those projected counts
while greatly increasing physical I12 rows/payload. A UI showing the same
keyed frames therefore cannot establish equal encoded size.

## Finished-target and fitting evidence, kept separate

The four additional reports under `accounting-before/finished-targets/` read
existing preview.62 outputs. Their first two comparisons use the **same
finished full-quality target result** as reduction reference. They do not
treat the unretargeted original donor's 12/13 KB as a quality-equivalent master.

| Actual pair | Physical bytes | Projected native keys | Scope |
| --- | ---: | ---: | --- |
| Sonic down-air on Mario, finished Dense → source-channel result | 227,168 → 69,848 | 20,319 → 6,095 | Direct native-forward fixture; distinct from the UI fixture's 237,620 / 81,140-byte outputs. |
| Provided Mario up-air on Sonic, finished Dense → source-bone result | 174,168 → 83,064 | 16,601 → 8,613 | Same finished Sonic target reference. |
| Native transition source snapshot → fitted export | 31,312 → 33,780 | +5 | Intentional fitting changes, not a compression comparison of equal motion. |
| Same prepared fitted result, owned export → direct native export | 33,780 → 33,780 | unchanged | Byte-identical; this specific prepared fitting/export route only. |

The first result contains 109 I12 streams (61,740 payload bytes) and nine I6
streams (4,284 payload bytes); its payload delta is -155,592 bytes. The second
contains 131 I12 streams (72,564 payload bytes) and 24 I4 streams (6,336 payload
bytes); its payload delta is -90,048 bytes. Both remove two entries under the
already-reviewed model-specific exact-bind contract, with no added entries.
This audit does not independently reauthorize those omissions or infer a new
universal omission rule.

The transition fixture is the actual `tests/ControlsAcceptance/Transitions.cs`
case: Wait1, an initial ordinary native HipN Y-rotation edit at frame 7,
reference-only track removal, then source-snapshot export, frozen boundary
reference fitting under existing-track scope and ordinary owned export. Nine
I4 and one I6 rotation groups become I12. The +2,468 bytes reconcile as +2,712
payload -240 codec headers -4 unclassified bytes. A later direct export of
that same prepared result is byte-identical. This does not qualify every
Fit dialog/library source or assert that the pre-fit animation stayed unchanged.

| Finished pair identity | SHA-256 |
| --- | --- |
| Sonic→Mario full-quality Dense reference | `d5531a535d1237d37fe60bd7bdfa387d55b5acd52815dc94237e27d39eb58909` |
| Sonic→Mario source-channel result | `d535a5662aba402403b80b31549a1e74ebadf88a5c4dde8f349e82972979f309` |
| Provided Mario→Sonic full-quality Dense reference | `44b2466f2e20bf2b1a5fd2c455c43fd9792ed0603442dedda6c7d2c68fe42926` |
| Provided Mario→Sonic source-bone result | `bd7e9e94f361fd22e1d1520c9078f3e61001cb8fe698f2d6bd5c372342ea8ffc` |
| Transition source snapshot | `8470c1e631b26f2c94bb5f3fb0e576a9654f25581b477e45275d08058c4487c7` |
| Fitted owned/direct native exports, both | `cb8931c78c32d09c88354a09c9bfcbdf6dd4666f8bacccce798521a688ee0d12` |

## Constant inventory does not bypass protections

Neither original donor fixture contains a multi-key constant/zero-slope stream
under the audit's conservative bit/complete-domain test. Their no-op growth
therefore cannot be solved by discovering new constant motion: it is primarily
a storage-retention defect.

The derived Dense references contain substantial constant data: Sonic→Mario
has 251 logical multi-key constant channels in 209 unique physical streams
(110,352 payload bytes); provided Mario→Sonic has 238 logical channels in 164
unique streams (66,912 payload bytes). The reduced Sonic→Mario result has none.
The provided Mario→Sonic result still has 36 logical scale/translation constant
channels across 24 physical I4 streams. Each stream stores 66 rows with equal
values and positive-zero slopes; their payload is 6,336 bytes plus 384 codec
header bytes. Isotropic scale sharing explains the logical/physical difference.
The observed six fixture entries are LFootJ, BustN, LThumbNa, NeckN, UStingNb and
RThumbNa; these labels must not become algorithm exceptions.

That 6,720-byte allocation is an inspection inventory, **not a guaranteed net
size saving or eligibility approval**. Source-owned motion, mandatory source
seeds, paired-side authoring contracts, exact fitting joins, contact/pin and
ancestor/attachment dependencies, signed zeros, flags and unknown protection
context still control any cleanup. Equal key values with nonzero tangents
are separately reported and are not constant. The fitted fixture also contains
one signed-zero channel, which this audit preserves rather than normalizes.
No model is passed to the independent byte comparator; it cannot prove that
a constant equals a native rest transform or substitute missing/group/model-
default semantics. A safe first cleanup can propose a fixed value only after
the existing protection and complete decoded validation gates approve it.

## Commands and evidence limits

Static inspection used `git rev-parse HEAD`, `git cat-file -t 1cc96940`, targeted
`rg -n`, line reads and `git diff --` on the traced production files. The
independent tests and audit used:

```text
python -B -m unittest discover -s tests -p test_chr0_storage_comparison.py -v
python -B tools/compare_chr0_storage.py <original.chr0> <actual-output.chr0> --scenario one-key --scalar-arithmetic exact-single-round --output <ignored-report.json>
python -B <outer-workspace>/artifacts/Fighterport-write63/Audit-Before-Writes.py
python -B <outer-workspace>/artifacts/Fighterport-write63/Audit-Retarget-Byte-Causes.py
git diff --check
```

The comparator suite passed 13 literal/synthetic tests, including flags,
isotropic/shared stream accounting, equal values with nonzero tangents,
duplicate keyed sides, added entries and output/input identity guards. The
hardlink identity branch is mocked because the test sandbox could not create
an NTFS hardlink; it is not evidence of a real hardlink write test. The audit
produced 24 per-route comparisons and four finished-target comparisons with
bounded read-only output. It rechecked every original input hash. The separate
retarget-context runner produced the four donor-layout/finished-target pairs
above, including per-channel and per-S/R/T time membership; it likewise
rechecked every source and existing target after reading.

Native applications/workers were run separately by the parent-owned background
diagnostic harness whose receipts are pinned above; the accounting tool did
not run them. No fresh GIF/capture, monitor-DPI or physical-input acceptance,
independent game runtime, PAC/BRRES size saving, full-model/contact quality
qualification or global optimization proof is supplied by this byte audit.
Pending sampled-codec proposals remain unqualified. Scalar fields/bytes,
format compatibility, motion quality and actual game acceptance are separate
gates.

## AFTER native I/O qualification — final-candidate evidence

The final native I/O runs are `after-final-r4-sonic`, `after-final-r4-mario`
and `after-final-r4-mario-pac` under the same ignored diagnostic directory.
Each passes **55 checks** on the frozen development source based at the
starting commit. These are explicit native I/O qualification receipts, not
acceptance of all UI, retargeting, fitting, contact, container or game workflows.
Every receipt has `PipelineAcceptanceClaimed=false`, uses a private desktop
and records no injected input or screen capture. The qualified final source
commit remains to be recorded at publication; executable/dependency identities
below pin what was actually run.

| Final executed component / evidence | SHA-256 |
| --- | --- |
| Application | `73a313c9cf1d11b51da678a69055a120395bf88e8afa4805c73e3be65f4a107c` |
| BrawlLib | `a0395adbeaf0d9a7da20fddae78d517d5f3a0ac97ecafbd2198701c19fd3ee99` |
| Executed native worker | `cfaa9446757aa1cae67dd37f51a8cb81522384e500ed215b3b82e2e04d15dcf5` |
| Final native diagnostic harness source | `a738f87c045b23dea9dd48dc4b2410a42bbc3a830170d6945ce6bc30203bf171` |
| `after-final-r4-sonic/write-path-results.json` | `c18d0795d86c558fffe0175196e77e9195c8660779b5ea538b5165b1de35d116` |
| `after-final-r4-mario/write-path-results.json` | `c511bf26e0f8a5f1a6776a0e6fc0d29dbf9e71ff881461d7a0fc6da11ababa1e` |
| `after-final-r4-mario-pac/write-path-results.json` | `9658c76cb959423113cde1eaaf1758b128cbe55db3a52f9b88d4bc4a34cc9d02` |
| Each of the three `receipt.json` files, 55 checks | `ca86a7e180b6b49792cd8c6dd35663c8b8c4e1c5dd767cd2c268ffa0417285ad` |
| Independent `accounting-after-final/summary.json` | `ddf1c249581a930b9ddea3a58d91632dbfcd5aeaaa88a9f0c8bf4cdeea3f8c24` |
| Independent `accounting-after-final/mario-pac/summary.json` | `d3aa2a7698c2c773bd836036f01f0095168b31de8cb10aafd15aba0b51fe3839` |

The retained-storage check is non-vacuous: the harness expects and captures
**54 original compact S/R/T groups for Sonic and 47 for each Mario fixture**.
It qualifies ten application/worker routes per fixture. No-edit operations
must retain all 54 / 47 matching groups; the changed rotation-group cases
must retain the other 53 / 46. All required compact payloads remain unchanged;
the expected counts and changed-group lists are recorded in the receipts.
Exact expected decoded scalar/context/metadata checks also pass for those
qualified routes, including the control no-op and single returned HipN edit.
This explicitly excludes the ordinary generic writer's negative control below.

The independent read-only final accounting compares 24 actual standalone
source→AFTER outputs and the matching 24 BEFORE→AFTER outputs. It also compares
12 PAC-derived native standalone outputs with that fixture's actual native
no-edit standalone baseline, and independently compares all 12 against the
corresponding supplied standalone Mario outputs. All input/output identities
are checked against the sealed native records; original model, clip and PAC
hashes remain unchanged. No native process is invoked by this accounting.

| Route family, actual final outputs | Sonic bytes | Provided Mario bytes | Exact PAC-selected Mario bytes |
| --- | ---: | ---: | ---: |
| Native no-edit | 12,016 | 13,608 | 13,608 |
| Source-aware copy, promotion, snapshot restore, native worker, control no-op: no edit | 12,016 | 13,608 | 13,608 |
| Source-aware copy, promotion, snapshot restore, native worker, control body edit: one changed rotation group | 14,416 | 13,936 | 13,936 |
| Ordinary native one-key **negative control**, not qualified | 84,448 | 41,248 | 41,248 |

For example, promotion/worker no-op now avoids 75,584 bytes of unnecessary
Sonic growth and 35,620 bytes of Mario growth. Its edited case avoids 73,184 /
35,292 bytes while preserving all matching untouched compact groups; only
the changed S/R/T group falls back to precise I12. The actual final layout is
the earlier source-aware layout in the serialized table above, not a new
lossy codec or reduced-motion candidate.

Sonic's opaque `ScalingRule=2` metadata is retained through the qualified
routes; this fixes the observed 2→0 metadata reset without claiming newly
supported nonzero-scaling-rule motion. Sonic's qualified no-edit outputs are
byte-identical to the original. Mario's native no-edit remains byte-identical,
but source-aware/copy/promotion/worker no-edit still has the separately observed
17 I12 `FrameScale` word changes, same decoded ordered fields and the same
13,608-byte length. The compact-retention gate does not include those I12
header words. Runtime significance remains unqualified; exact decoded fields
must not be restated as byte-identical output.

The ordinary native-one-key diagnostic still exposes 9 / 11 additional changed
scalar channels and the larger files. The final metadata fix retains Sonic's
header there, but does not turn that generic negative control into an accepted
export path. Passing the 55 targeted checks does not hide that result or
authorize loosening its quality gates.

### Exact PAC member identity, separate from standalone size

The PAC run selects `AttackAirHi` at **`root/1/0/0/76`**, 34 frames, corresponding
to the provided up-air fixture's recorded native content identity
`0:75deb1714db41fd0e74d7741d0aaa442566f1745bc1b7b76a39e7be248280ea3`.
The earlier failed invocation did not forward the requested selector through
the test wrapper; it was not evidence of duplicate matching PAC members.
The final wrapper and diagnostic explicitly record the requested selector and
resolved identity as the same value.

The source PAC is 3,329,344 bytes, SHA-256
`b1de9496061b10cad49c753bd9ea518815e76758872004381cee3aea740380cf`.
The native reader reports a 12,859-byte raw member at offset 852,124 in that
uncompressed root; its raw SHA-256 is
`cd478c12accbf7bcebc3bab75d53c3e359735b92ae45f49a12a7ff94c81f529f`.
Independent byte slicing confirms the reported member range exactly matches
the native dumped root bytes. **No independent PAC parser is claimed.**
The raw member can point to its containing string table and is not a
relocated standalone file.

The actual native standalone no-edit export is 13,608 bytes with SHA-256
`93559d59573ed7bd6612bdd3a2b7daa06d1df457982884e281f55139f1ebb9fc`,
byte-identical to the user's supplied standalone clip. All 12 matching final
route outputs are independently byte-identical between this exact PAC-selected
fixture and the supplied standalone fixture. That comparison uses real
standalone exports, never the 12,859-byte raw member as if it were a standalone
compression baseline. No repacked PAC length or outer archive saving is
measured by this test.

The final read-only command for the standalone cases was:

```text
python -B <outer-workspace>/artifacts/Fighterport-write63/Audit-After-Writes.py --after-prefix after-final-r4 --output accounting-after-final --stage final-candidate
```

Separate bounded Python checks added the PAC native-location facts, verified
its dumped root/member byte slice and unchanged asset hashes, and generated
the 12 + 12 PAC standalone comparisons described above. `final-candidate` is
a report context label; no report infers broader acceptance from that label.

## Workflow acceptance and remaining retarget gap

The later raw-dictionary/partial-Undo correction and its complete current-build
qualification supersede the **tested-binary pins**, not the recorded observations,
of the earlier r4 I/O section above. See the
[preview.63 acceptance record](CHR0_WRITE_PATH_ACCEPTANCE_2026-10-29.md) for the
current `6148f96a…` root/worker library, 6,718 fresh checks / 13 runs, separately
identified source-continuity evidence, 55 Python tests, 1,902 renderer checks,
41 covered UI images and all 308 actually reviewed decoded GIF frames. The
current independent accounting is `accounting-final-r14/summary.json`, SHA-256
`395e26065c448a86177500ec79d8e55cabd723e0f516547fb0e0c731fc9600ed`.
Bounded native I/O, authoring/Undo, optimizer, fitting/contact and project
Save/reopen paths have current affected-boundary evidence. Broader retarget-size,
general contact and runtime/game acceptance remain pending as described below.

The measured I/O correction retains compact imported storage and metadata
through the explicitly qualified routes. It does **not** resolve the principal
finished retarget payload gap demonstrated at the start of this document.
Protection-qualified constant cleanup remains a separate opportunity in
finished target masters; the same-marker sampled-versus-indexed and paired-side
costs still require their own implementation and unchanged quality gates.

The bounded correction is qualified in the
[acceptance record](CHR0_WRITE_PATH_ACCEPTANCE_2026-10-29.md): 6,718 fresh pinned
checks in 13 private-desktop runs, 2,047 separately identified prior checks
admitted by source continuity, 55 Python tests, 1,902 renderer checks, 41 covered
workflow PNGs and all 308 decoded GIF frames actually inspected. Fresh final
runs include ordinary export after native Undo/Redo, fitting references,
contact authoring, project lifecycle and PAC output. Source-only implementation
[`7261bf211e4958a4663fbbf0ca93b8a3be5f2254`](https://github.com/DoctorFlux/Fighterport/commit/7261bf211e4958a4663fbbf0ca93b8a3be5f2254)
was published with verified DoctorFlux attribution to public Fighterport/main.

This accepts the measured write-path/Exact boundary, not the entire retarget
payload gap or an unapplied sampled-codec proposal. Further candidates must use
the same finished target master and unchanged root/all-skin-weight, terminal,
metadata and exact-protection contracts. Physical input, human usability,
monitor DPI, broad contact quality, independently tested game/editor versions
and actual game acceptance remain separate and unverified here.
