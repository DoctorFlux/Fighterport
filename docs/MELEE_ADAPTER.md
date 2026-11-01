# Melee adapter — implementation and qualification

Preview.68 qualifies bounded same-viewer owned animation masters,
native Undo/project reopen and verified Figatree action-member output through
shared future-format target/export interfaces. Full native model editing/writing,
shared Next/IK/fitting/contact/optimization rig math and whole archive repacking
are still open. Native snapshots remain opaque in shared project storage so later
formats need no Melee extension assumptions. See
[bounded acceptance](MELEE_TARGET_SRT_ACCEPTANCE_2026-11-02.md) and
[implemented boundary and measured development evidence](MELEE_TARGET_PROGRESS.md).


Starting Fighterport commit: `c8a24af492fedfe2e1641c23ccefa8b5aaf06fcb`.
The active checkout/publication route remains DoctorFlux/Fighterport/main.
Melee is the user-selected next source AND destination. This does not close G1/M2
or the retained contact/game gates. Preview.66 ledges remains queued.

## Shared boundaries

`INativeGameSourceAdapter` registers source providers, image extensions, native
resource providers and operation capabilities. Saved locations are not support.
`INativeSourcePreview` supplies a prepared frame sampler and an existing ModelPanel
render target. Providers must prepare outside playback, freeze exact identities,
retain source data and reject stale publication. Future games implement their own
reader, evaluator, renderer and writer; no CHR0/HSD compatibility is inferred.

The native Brawl editor/original Next remain intact. Owned Melee I/O lives in
`adapters/FighterPort.Adapters.Melee`; native types do not enter Core. The optional
MDL0 construction used by the existing bone/evaluation facade is an independent
display projection, not a replacement for the authoritative native HSD graph.
Melee source drawing uses its HSD material graph in the same native ModelPanel,
not the Collada diffuse-material approximation. The native shader path handles
TOBJ stages, palette/texture decoding, UV sources, TEV inputs and pixel states.
CPU skinning retains all influences and has no four/six-weight or GPU bone cap.
Scene/game-light, material/visibility/shape animation, unsupported matrix semantics
and actual game fidelity require their own evidence. Do not label all rendering
features qualified from a successful parser or one screenshot.

## Direct sources and mod instances

The managed read-only GameCube ISO/GCM reader validates the disc header, byte-based
FST offsets, directory boundaries, virtual paths and native member ranges. It does
not execute DolphinTool, map the entire disc or retain extracted game files.
Source metadata is indexed; explicitly selected native display buffers are temporary.

Folder providers accept extracted roots or a `files` root. Partial replacement
folders explicitly name one complete base source. Overrides match exact relative
native paths. Case-insensitive conflicts, linked paths, self-bases and nested overlays
are rejected. Multiple packs retain distinct location/model/clip IDs and labels.
Unknown pack layouts and patch-only distributions are not automatically playable
or silently merged with vanilla assets. Real m-ex/custom-pack qualification needs
actual supplied fixtures; procedural overlays are separate evidence.

Native exported model/action symbols establish source associations. Complete exact
rig fingerprints qualify alternate costume bindings only within the same source
instance. Ambiguous/missing associations remain unavailable; no character/action
exceptions determine numerical behavior. Native archive offsets retain numeric order.
Figatree end-frame coordinates are stored separately from display frame count.
Runtime loop policy is not invented from animation names.

## Native dependency

HSDRaw and the five material fragment modules originate from Ploaj/HSDLib revision
`85567e40797de3c820a55476eca54c704921a848`, MIT; original sources/licenses are retained
under third_party. The owned native build targets the existing net472 runtime,
without upstream Windows.Compatibility dependencies or an upstream application shell.
The HSD shader strings are adapted for eight texture/UV stages and all-influence CPU
skinning; the upstream six-weight/200-bone GPU path is not used.

## Verification in progress

The supplied local USA 1.02 image identifies as GALE01 revision 2. Its FST contains
1,212 entries and 894 native DAT/USD files. Native inventory finds 156 costume
models, 6,245 motions and 6,101 exact default associations. 144 motions remain
unassociated and six non-fighter packages fail the strict raw envelope checks;
retain those issues rather than substituting assets.

All 156 indexed models pass the native mesh/texture/all-influence preparation
corpus. This is NOT 156 renderer or game passes. Procedural cases cover independent
Hermite expectations, winding, missing tracks, unsafe paths/cycles, cancellation,
mod-overlay isolation and 5/9/17 unchanged skin weights. Native source-snapshot
serialization compares the complete rooted/reference byte/pointer graph after
fresh decoding; an unproven no-edit rebuild retains original bytes. That exact
fallback does not establish support for exporting edited Melee resources.

Background source UI tests run in the existing private-desktop harness with shared
offscreen captures. Keep failed/blank/black captures: fresh inspection exposed a
matrix-layout mismatch, unprepared model bounds and an unset material overlay
uniform; parser passes alone were not accepted as rendering proof.

## Runtime-based renderer correction

The user-directed decomp inspection pins doldecomp/melee
`598987cf347c125c7416517af18bf5c1dc3f15a1`. Native envelopes, compensated scale,
animation scale overrides, shared matrices and exact costume visibility context
replace the initial Brawl skinning approximation. **Parts...** reviews native source
variants without editing source or target data. Game-code initial/animated part
state is not inferred from names. See [diagnosis, commands, fresh evidence and
remaining limitations](MELEE_RENDERING_DIAGNOSIS_2026-11-01.md).

## Remaining delivery stages

1. Qualify source/folder management, native source rendering, costumes and cached
   same-model motion changes; update acceptance/launcher only after fresh checks.
2. Melee-native target ownership and verified native replacement-file export,
   including project persistence and untouched graph/sibling preservation.
3. Bidirectional original Next animation retargeting/fitting and native writing.
   Audit native FOBJ representability: upstream encoding casts frame waits to
   integers. Never truncate fractional master timing or silently weaken quality.
4. Brawl ↔ Melee model/material/texture conversion as independent derived assets,
   with compatibility/loss review and verification of each supported direction.

Initial output is native replacement files/folders, not rebuilt discs, new fighter
slots or gameplay code. Project masters stay exact; compression/export copies are
independent. Physical input/DPI, real modpack acceptance and actual game testing
remain unverified until performed.
