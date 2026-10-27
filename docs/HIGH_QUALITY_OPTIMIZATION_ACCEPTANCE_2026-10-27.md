# preview.46 — bounded High-quality animation reduction

Accepted: **10,241 checks / 22 admitted background runs / 95 inspected native PNGs**.
The launcher selects preview.46-high-quality-optimization. The ONE G1/M2 milestone
stays incomplete; no broader increment is promoted.

## Behavior and numerical boundary

Animation → Optimize animation… still defaults to Exact. High quality is explicit,
manual and applies to the exact current owned CHR0 v4/v5. It is independent of
Next/fitting and never runs during playback, transfer, fitting, Save or export.
The complete native editor, original Next, original rig and native Undo remain.

CHR0 v4/v5 qualification covers metadata the existing native reader/writer can
represent. Nonzero scaling rules, noncanonical loop/path layouts and opaque v5
user-data forms are rejected before snapshot/hash creation. Both original and
working buffers are checked; a legacy working-buffer rebuild cannot hide an
unsupported original field. Canonical Int/Float/single-String metadata, strict
UTF-8 names, ordinal IDs and finite exact float roundtrips are independently
qualified. No global native writer or game-evaluation change is introduced.

The frozen [scope](ANIMATION_OPTIMIZATION_SCOPE_2026-10-27.md) fixes scalar value
and derivative limits before native qualification. Original Hermite knot times,
endpoint values/tangents and raw Euler winding remain; no quaternion rewrite,
tangent regeneration, legacy Clean command or packed-codec quantization.

Exact-first preparation retains a verified fallback. High-quality attempts use
scalar precision scales 1, 1/2, 1/4 and 1/8 against one immutable original session
baseline. Every attempt shares Core work/span and native witness/bone/skin budgets.
The fixed native ceilings never grow. The first fully verified HQ candidate ends
the search; a smaller verified Exact result wins instead. Greedy candidates are
not assumed nested, and no globally minimal file-size claim follows.

Continuous rational Bernstein certificates cover mathematical scalar values and
derivatives over original/candidate interval partitions. Independent precise I12
encode/reopen checks every native coefficient, flag and metadata field. Actual
native evaluation checks local translation vectors, raw Euler/orientation, scale,
complete forward matrices, all bone positions and all skinned vertices. Native
integer, withheld, extrema, paired-side/near-knot, seam and adaptive witnesses are
sampled geometry evidence, not a continuous contact or game certificate.

Source-owned motion controls, potentially singular scale and authored fitting
endpoints/joins stay exact-only. Pin/contact ancestor, skin and attachment closures
retain their original native structure. Missing context is reported. Real bind
geometry supplies L; missing/degenerate geometry leaves Exact available.

Native size and quality rejections are identified separately. Work exhaustion
returns only a prior completely verified smaller candidate or unchanged content.
Cancellation, stale dependencies, metadata/encoding/ownership failures discard
preparation. Apply is one existing native command with no competing document or
Undo stack. Historical retarget/fit hashes remain historical; review approvals
invalidate normally. Actual CHR0 bytes are measured, with no PAC/PCS savings claim.

For a clean embedded resource, the stored CHR0 block size and fresh decoded
candidate block size use the same BRES header-size unit, excluding shared strings.
Growth of that block is rejected separately from standalone file growth. Precise
before/after export bytes, original stored block bytes and standalone stored file
bytes are reported separately when available; dirty inputs do not claim an old
stored block represents their current edited content.

Reports, settings and baselines are session-only. Repeated unchanged results and
mode switching use the retained source; export receipts do not reset it. Authored
changes/reopen establish an identified baseline. Runtime pin preview positions are
derived from the playhead and are excluded from the cache dependency signature;
full authored constraints and exact pin identities remain protected. Per-clip
retention survives optimizing other clips. LRU snapshot disposal under the existing
two-million-key/256 MiB aggregate ceilings keeps a compact identity/hash marker;
missing originals block further approximation instead of silently rebasing.

## Verification and delivery

All application checks use serialized private desktops and the shared offscreen
renderer with isolated settings/library. No active-desktop capture or input
injection. Dedicated cases cover ordinary clips, original Next and fitted results,
both authentic rig directions, typed quality/size/budget failures, ownership,
cancel/stale work, exact protected keys, session baselines, native Undo/Redo,
export before Save, fresh reopen, and untouched native resource bytes.

Independent native procedural cases include real nonzero accepted approximation,
scalar-safe hierarchy amplification, derivative-only contradiction and near-knot
floating witnesses. Coordinator tests use actual independently verified native
candidates to check retry ordering, disposal, shared counters, Exact fallback,
size refusal, budget stop and cancellation/fatal failure.

Admitted cases under the ignored `Fighterport-optimize46` artifact directory:

| Case | Checks | PNGs |
| --- | ---: | ---: |
| workspace-mario-final3 / workspace-snake-final3 | 862 / 890 | 10 each |
| raw-next-mario-hq-final1 / fitted-mario-hq-final1 | 470 / 479 | 2 each |
| raw-next-snake-hq-final1 / fitted-snake-hq-final1 | 780 / 789 | 2 each |
| exact-mario-final1 | 471 | 2 |
| keyboard-final1 | 210 | 7 |
| native-transitions-final1 | 81 | 11 |
| set-export-final1 / batch-review-final1 | 152 / 182 | 8 / 9 |
| workspace-mario-final4 | 883 | 10 |
| raw-next-snake-hq-final2 | 780 | 2 |
| native-mario-final1 / native-snake-final1 | 88 each | 0 |
| layout-final3 | 454 | 16 |
| raw-next-snake-hq-final3 | 780 | 2 |
| core-packaged-optimization / boundary / rotation | 39 / 380 / 127 | 0 |
| root-mario-snake / root-snake-mario | 628 each | 0 |

`admitted-runs.json` records actual application receipts. `visual-review.json`
records all 95 image hashes, inspected in 24 labelled sheets plus a full-size
current compact HQ view. Original/result poses, expanded reports, native editor,
keyboard dialogs, batch review/export and all four supported text sizes were
reviewed. Disabled native captions retain their inherited low contrast. Process
DPI configuration and physical/mixed-monitor acceptance are unchanged and open.

## Measurements and development findings

The authentic original Next Mario-to-Snake result reduces **44,553 → 10,137 keys**
and **423,908 → 118,732 precise CHR0 bytes** using a verified HQ candidate at scalar
precision 1/2. Maximum sampled raw Euler error is 0.0000076294°, local orientation
0.0000091971°, local translation 0.000000059605, world/basis 0.0000137411 and skin
0.0000076294. Independent withheld native FK/full-skin witnesses also pass. Root
control functions, winding and flags remain exact-only.

Raw Sonic-to-Mario and fitted Mario retain the smaller verified Exact fallback:
246,912 → 73,824 bytes and 189,732 → 76,980 bytes, respectively. Fitted Snake
retains its 120,668-byte Exact result because the resource-limited HQ candidate
is larger. No tolerance or probe coverage was weakened to obtain a reduction.
The ordinary edited Mario/Snake fixtures additionally remove 25/45 keys beyond
Exact. These measurements do not predict PAC alignment/compression or game memory.

- A failed baseline assertion followed a deliberate model rename/restore. Actual
  native model bytes changed, although binds and authored controls did not. The
  exact dependency guard correctly established a new identified baseline. The
  corrected test preserves strict reuse for unchanged targets.
- Evaluated pin positions and active marker membership were excluded from the
  baseline cache signature; complete authored constraints remain included. Actual
  pin scrubbing/refresh and authored removal verify the distinction.
- Replacing the one-entry cache with exact per-clip retention fixes A → B → A.
  Forced LRU accounting tests preserve markers, block missing-original HQ work,
  permit Exact and identify genuine authored-edit resets. Cache allocation and
  serialization occur before live Apply.
- A compact resource fixture has a 216-byte original embedded block, 400-byte
  precise baseline and 388-byte candidate despite fewer keys. The same-unit cap
  refuses that growth. A genuine 400-byte input accepts the 388-byte block even
  when its standalone total is 536 bytes; shared strings are not confused with
  resource bytes.
- The original Next itself transfers mapped body motion while excluding an actual
  animated donor-only serial branch on both authentic native model fixtures.
  No optimizer cleanup or Next implementation change is used for that proof.
- The metadata suite qualifies canonical v4/v5 user data and rejects unsupported
  serialized fields, invalid UTF-8, float bit loss and original metadata hidden
  by a legacy working-buffer rebuild. Both original files and native keys survive.
- The layout test found a real 28-pixel combo in a 23-pixel slot. Native item-height
  measurement fixes clipping and downscaling. All bounds checks remain intact.

Workspace development runs final1/final2 and layout-final1/final2 are excluded;
their failures are recorded above. The native metadata/size/quality rejection
fixtures are deliberate passing rejection tests, not failed runs counted as passes.

## Build and publication evidence

Source baseline: `e0b39524130fea1d625d6ea2e3dd788485b3cdb4` (separately published
preview.45). Version `2.0.0.26`, informational
`2.0.0-preview.46-high-quality-optimization`. Launcher: `native-final5`.

Final native executable SHA-256:
`8BF7E60AB490066707A8F9E6B3EAF2F430A8DECBDACC37945CA7FF0D0143248A`.
Core SHA-256:
`54526845862AB7DB35C4971ACD65494D6980FB92A4A22ECB50C5692F72085B47`.
Engine host SHA-256:
`2136C5D03176DC495181F3DFD4084EF84BE071A05C2D3ABCF53EED0981368F75`.
Compatibility worker SHA-256:
`EACFBAB17B3B41A7193C32702F8C06862420D64B3E1505AF51CB2CA07981FE55`.

The first eleven UI cases use native-final2 (SHA
`B310364740D46E85E37B0C261E790D05E5413CAAA335745F3405B1645D5DA272`).
Native-final3 (SHA
`E22A01843AD7963E46CCF327C801192C290509B105140F62D6791783DA98EC7D`)
adds per-clip cache retention/preallocated Apply, independently qualified by the
883-check full workspace case and fresh raw replay. Final4 adds the metadata
guard, qualified by both 88-check native suites. Final5 corrects only dialog
item-height measurement, qualified by all 16 layout captures and another authentic
raw replay. Core/host/worker are identical between these builds; mathematical
reducer/Next code does not change. Final raw replay candidates are byte-identical
across final2/final3/final5. Core suites use an isolated driver with the exact
packaged final Core DLL, rather than relying on a differently built test copy.

Actual command families, from the checkout unless stated:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory <native>
./scripts/Build-Engine.ps1 -DotNet <workspace-dotnet10> -NativeDirectory <native>
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=<native> --no-restore
./scripts/Test-BackgroundCapture.ps1 -WorkspaceAnimationOptimization -Model <model> -Motion <motion> -NativeDirectory <native> -OutputDirectory <new-output>
./scripts/Test-BackgroundCapture.ps1 -OptimizationProject <accepted-project> -OptimizationRawNext -OptimizationHighQuality -Model <model> -Motion <motion> -NativeDirectory <native> -OutputDirectory <new-output>
./scripts/Test-BackgroundCapture.ps1 -NativeAnimationOptimization -Model <model> -Motion <motion> -NativeDirectory <native> -OutputDirectory <new-output>
./scripts/Test-BackgroundCapture.ps1 -WorkspaceOptimizationLayout -Model <model> -Motion <motion> -NativeDirectory <native> -OutputDirectory <new-output>
# Isolated Core driver containing the exact packaged Core DLL:
dotnet <driver>/FighterPort.Tests.dll <output> --animation-optimization
dotnet <driver>/FighterPort.Tests.dll <output> --boundary-fitting
dotnet <driver>/FighterPort.Tests.dll <output> --rotation-path
<native>/RetargetAcceptance.exe --motion-controls <source-model> <target-model> <new-output>
```

Existing keyboard, native transition/curve, set export and batch review switches
complete the matrix. An additional 21-check host DTO/default/limit reflection audit
is retained separately and excluded from the admitted total. `Run.ps1 -Check`
resolves the qualified launcher without opening a window.

Publish source only to public Fighterport/main with DoctorFlux author/committer
and contributor verification. Preserve upstream notices; generated binaries,
captures, settings and game assets remain excluded. Physical input, monitor DPI,
human usability, broad contact quality, Melee/new adapters, container-size savings
and actual game acceptance remain unverified. ONE G1/M2 remains incomplete.
