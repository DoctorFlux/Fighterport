# CHR0 write-path audit — 28 October 2026

This is the preview.54 candidate source audit, not acceptance or a release claim.
The editable target animation is the reference for export quality and size.
An unrelated source character's compressed file is a comparison, not its baseline.

| Entry point | Reference and write path | Qualification boundary |
| --- | --- | --- |
| Workspace single-animation export | `FreezeOwnedWorkspaceExport` copies the exact owned member; `PrepareOptimizedExportSnapshot` prepares a separate candidate; `ExportWorkspaceSnapshot` encodes, independently decodes and publishes it. | Exact and explicit error-bounded settings use the same backend. Stale dependencies, unsupported protection context, failed quality or increased physical size retain the finished reference or reject publication. |
| Reviewed-set export | Each exact approved member uses the same frozen snapshot and `PrepareOptimizedExportSnapshot`; every candidate is decoded before the directory commit. | Per-member master and candidate content identities, hashes and byte receipts stay separate. No temporary editor selection is needed. |
| Animation optimization dialog | Analyze calls `PrepareOptimizedExportSnapshot` for an independent current-member baseline; `ExportFrozenPreparedOptimization` publishes the already reviewed copy through the single-export guards. | The target master, native selection and Undo are preserved. Report continuous scalar certificates and sampled native geometry independently. |
| Qualified native animation slot | The assigned exact member uses the same frozen optimizer. Name/version conversion independently encodes both the same-slot reference and candidate, compares actual bytes and reseals the qualified output hash. | Unknown or lossy metadata conversion rejects; the editable original name/version and curves stay intact. This is not a whole-container saving. |
| Nonselected or targetless owned CHR0 | An unverified intended model selects an independent `CopySource` precise reference snapshot, with complete reopen/content/storage and prepared-byte/hash checks through the common publisher. | No model is inferred and no live resource is serialized. Error-bounded requests explicitly fall back to the reference route. |
| Retarget motion-PAC insertion | Prepare the same independent export candidate, then `ExportMotionPac` inserts it into a private template copy. Verify full inserted content/storage, untouched CHR0 coefficients/storage, other standalone resource hashes, structure and archive metadata after fresh reopening. | Frozen template and document dependencies are revalidated before same-directory new-file publication. Existing destinations stay protected; only actual container lengths can be reported. |
| Ordinary project Save / arbitrary native resource or container export | Native document serialization preserves the editable master and retained storage policy. `PreciseCHR0EntryNode` defaults derived curves to I12 and accepts only an explicit coefficient-matching packed plan. | Ordinary saving does not silently optimize. Generic resource-tree BRRES/PAC routes and outer compression remain separately qualified; type/frame-count checks alone are not a complete motion/sibling proof. |
| Retargeting and fitting | Native curves remain editable results. Fractional master baking writes native float-time knots when integer cells cannot reproduce solved motion. | No automatic lossy size optimization. Root-control curves, native binds, skeleton and all skin coefficients retain their existing ownership rules. |

Relevant implementation is in `BrawlCrate/UI/ModelForm.VerifiedExport.cs`,
`ModelForm.VerifiedSetExport.cs`, `ModelForm.ExportOptimization.cs`,
`ModelForm.AnimationOptimization.cs`, `BrawlCrate/Studio/NativeAnimationOptimization*.cs`
and `BrawlLib/SSBB/ResourceNodes/Animations/PreciseCHR0EntryNode.cs`.

## Physical byte accounting

`NativeChr0SizeReport` reads freshly serialized bytes independently of the writer's
size calculation. It distinguishes the declared resource block from the complete
standalone file, including external names. Header/dictionary/entry bytes, indexed
or dense curve headers and payloads, user metadata, proved zero alignment and
unclassified bytes are reported separately. Shared/isotropic streams count once.
An unclassified byte is reported rather than silently attributed to curve data.
No PAC, BRRES or compressed-container saving follows from this receipt alone.

Writer-created generated-pair evidence is a separate bounded session registry,
not inferred from names or imported equal-valued keys. The exact content, native
rig, ordered tuple bits, flags and timebase must match. Fitting inherits only
unchanged known tuples; the frozen export copies eligible typed sites into the
same scalar and cumulative validation pipeline. Unknown pairs and exact protected
controls/boundaries retain their preservation contract. A fresh application
process does not recreate this evidence from an old project or exported file.

## Pinned primary references

The upstream revisions below were queried on 28 October 2026. They are technical
references; Fighterport's modified evaluator and writer require their own tests.

- BrawlCrate revision `a4c1ee3385010d5c64a88df9c56267c61f213321`,
  [AnimationConverter.cs](https://github.com/soopercool101/BrawlCrate/blob/a4c1ee3385010d5c64a88df9c56267c61f213321/BrawlLib/Wii/Animations/AnimationConverter.cs).
  Its CHR0 group writer distinguishes fixed inline values, isotropic scale and
  indexed curve payloads. Selecting a smaller codec is not a proof that its
  decoded animation remains equivalent.
- Open-source NW4R decompilation revision
  `e5a4d369011062fe243aa63f6c2e2853854c395d`,
  [g3d_resanmchr.cpp](https://github.com/doldecomp/ogws/blob/e5a4d369011062fe243aa63f6c2e2853854c395d/src/nw4r/g3d/res/g3d_resanmchr.cpp).
  Its indexed 48-bit path decodes frame positions as signed S10.5, while the
  96-bit path retains floating frame positions; Hermite evaluation uses the
  retained endpoint values and slopes. Consequently a generic integer-only
  native key collection cannot faithfully represent all native CHR0 files.

The latter project is a runtime reference from another NW4R title. It does not
establish Brawl game acceptance, Melee support, universal contact quality or
bit-identical floating-point behavior on Wii hardware.

Historical inspection also pinned `libertyernie/brawltools` to
`cad9a29a49d8884c9ec2f15e5c21a7dbb5fe0ac2`; its
[source history](https://github.com/libertyernie/brawltools/tree/cad9a29a49d8884c9ec2f15e5c21a7dbb5fe0ac2)
and BrawlCrate's pinned
[Changelog.txt](https://github.com/soopercool101/BrawlCrate/blob/a4c1ee3385010d5c64a88df9c56267c61f213321/BrawlCrate/Changelog.txt)
are historical evidence, not reproduced runtime results. The
[issue 129 terminal-frame report](https://github.com/soopercool101/BrawlCrate/issues/129)
was read on 28 October: it concerns particular non-looping Mario Kart Wii CHR0s
and reported BrawlBox/BrawlCrate versions. Its private fixture was not available
for an independent reproduction here. No frame is blindly removed or appended,
and the report is not generalized to every Brawl CHR0.
