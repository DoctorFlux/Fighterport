# Melee target editing foundation — implementation progress

Starting commit: `36bbb2ce6a626b2936f6a8f3a43c614917387e97`.
Active product: `.local/Fighterport`, public DoctorFlux/Fighterport/main.
Preview.68 qualifies the bounded native target SRT slice inside the ONE
incomplete G1/M2, not full Melee model/tool parity. See the
[bounded acceptance record](MELEE_TARGET_SRT_ACCEPTANCE_2026-11-02.md).
Preview.66 ledges and the existing broader gates remain open.

## Implemented boundary

- `INativeEditableTargetAdapter` prepares an independently owned target and
  attaches its native evaluator/render backend to the complete existing viewer.
  `INativeTargetExportAdapter` supplies native output and a successful-publication
  receipt. The current viewer hooks use MDL0/CHR0 as internal authoring contracts;
  they are not a claim that every future format maps exactly to those types. These contracts are shared entry points for later formats; implementing
  an interface alone never qualifies a reader, solver, writer or game.
  Owned native snapshots are opaque bytes, without a Melee extension whitelist
  in the shared project document. A procedural future-format blob survives
  project Save/reopen without invoking any game decoder.
- Open as target retains the same ModelForm and uses existing document replacement,
  native selection, timeline, SRT editing and Undo/Redo. This is an editable
  animation master, not a second read-only preview pane or second Undo stack.
- Native model, selected motion member, fighter context and executable metadata
  remain immutable project-owned snapshots. Only the selected action member is
  copied, retaining its original archive identity/hash separately; repeated
  validation does not decode the entire action archive. Project Save preserves
  exact editable MDL0/CHR0 masters and the original serialized animation baseline.
  These authoring storage formats are not represented as Melee game exports.
- Editor-only joint IDs survive native model serialization independently of
  display names. No original joint, bind, envelope or weight is replaced. The
  backend evaluates native scale inheritance and skins every native influence.
- Real transform fields use the native common part-to-joint role tables to
  authorize existing body descendants. Every body ancestor and explicit motion
  control remains protected. Missing/conflicting native metadata grants no
  editing permission. Across the supplied roster, 133 costumes resolve and 23
  stay unavailable; this is not a universal anatomy or mod compatibility claim.
  Shared rig-worker loading is blocked before snapshotting an unqualified rig.
- CHR0 group slots are filled with authentic native bind values for unanimated
  axes inside an enabled group. Otherwise serializing a sparse native rotation
  could introduce zero-valued axes. Raw Euler winding remains intact. Native
  fields and their immutable snapshots remain authoritative.
  Activating new channels through the real transform fields or interpolation
  callbacks retains the other native bind axes in the same Undo transaction.
- Normal-detail masks exclude low-only/metal-only geometry from the viewer and
  authoring projection. Shared high/low objects remain eligible as normal parts.
  Missing visibility context fails explicitly; no all-mesh/LOD fallback is used.
  Original native files retain their complete original resources.
- Standard animation export offers a Melee action-member DAT or an explicitly
  labelled editable CHR0 master. Native DAT writing preserves untouched streams,
  symbols, flags and graph data, writes changed curves with float FOBJ storage,
  freshly reopens, compares the current edited master, then commits a new file.
  The action-member association is explicit; unrelated imported animations cannot
  silently replace the initial native action slot. No source resource is edited.
  Frozen dependencies include binding fields and owned snapshot identities;
  in-place changes invalidate export before publication.
- Same-layout graph edits retain original offsets. New structures use an append
  allocator, preserving original data and pointer boundaries and rebuilding only
  verified relocation/symbol metadata. Complete rooted/reference graph hashes must
  match after fresh decoding. A failed changed graph never falls back to an
  unedited source and calls it success.
- Complete source-inventory issues are retained in bounded catalogue metadata.
  Adding a source reports a compact status; Saved games and paths → Source issues
  opens the full read-only diagnostics on demand.

## Frozen verification contract

Native bridge limits: raw local rotation 0.0003 degrees; translation/scale
`max(0.000002, abs(reference component) * 0.000001)`. Joint and surface displacement
must be at most `0.0001 * L`, where L is the finite positive native bind-surface
bounding-box diagonal. These are bridge/encoding checks, not new retarget/contact
limits or proof of contact acceptance.

Coverage includes every editable knot, native eighth frames, endpoints and an
additional 0.381966 interval witness. Native cumulative transforms and all native
surface equations are compared after serialization. Identical triangle positions
may share verification only within the same mesh/matrix slot with identical
ordered influences. No influence is removed or rounded. The report distinguishes
raw vertex count from unique equations. Work caps remain two million joint/time
samples and sixteen million surface-equation samples. This finite coverage is
not a continuous-time proof or a game test.

Non-integer edited FOBJ waits, nonzero native start-offset authoring, unsupported
edited terminal tails and unqualified resource kinds reject explicitly. They are
never truncated, clamped, silently resampled or exported as a substitute format.
Native runtime looping remains action-context owned. Existing exact source
streams and metadata are preserved where an edited mapping is unproven.

## Recorded evidence

Local evidence root: `artifacts/Fighterport-melee68` in the outer workspace; private
assets, settings, captures and binaries are excluded from Git.

- `native-policy26`: 77 procedural checks, including independent Hermite midpoints,
  incoming/outgoing slopes, full turns, explicit helper-row membership, native
  scale inheritance, 5/9/17 influences, strict paths/graphs, cancellation at commit,
  destination races, stale dependencies, native body-role conflicts/missing
  context and exact influence-equation sharing.
- `roster-edited`: all 156 indexed costume models pass an isolated native joint
  scalar edit/fresh graph reopen without mutating the original. This is not 156
  interactive model-editor, rig-solver or game passes.
- `final29-Samus-Wait`, `final29-Fox-Wait1`, `final29-Mario-Wait1` and
  `final29-Mario-Wait2`: 57/57/61/61 assertions. These invoke the actual native
  transform fields for body edits and attempted motion-control edits, native
  export before Save, native Undo/Redo, project Save, actual edited and original
  main-view reopen, deterministic edited re-export and source-browser
  independence. Mario Wait2 retains archive offset 4256 and a separately owned
  zero-offset member snapshot. Earlier target18/20/21 tests edited keys directly;
  they are preliminary evidence, not proof of transform-field authorization.
- `ui19`: 68 assertions across the full source corpus, actual complete-issues and
  Parts dialogs, source views and editable target workflow. All 24 PNGs were
  inspected in a review sheet, with full-size main-view review.
- All 36 final29 PNGs were inspected in three sheets, with full-size native-body
  and compact 175% reviews. Compact/wide captures at 100/175% text preserve the
  exact animation and frame. Physical input and monitor DPI remain unverified.
- `package32-Mario-Wait2`: 63 assertions using the complete preview.68 package,
  including opaque future-format project storage, invalid numeric data, early
  work-budget rejection and in-place native dependency invalidation.
- Final `native2-34` workflows add new translation-group activation and native
  export/Undo: Mario Wait2 66 assertions, Samus Wait 62, Fox Wait1 62. Final
  `native3-35-Mario-Wait2` passes 68, including actual interpolation-editor
  callbacks and exact Undo of the complete sparse-group transaction. Its 11 PNGs
  and the 25 native2 Melee PNGs are byte-identical to the inspected final29 set.
  Final native3 Brawl structure/lifecycle pass 182/37 assertions. Five lifecycle
  images retain exact inspected bytes; the changed path dialog was viewed anew.
  The launcher selects this complete `native3` package, version 2.0.0.43.
- Native Brawl structure/root/export regression: 182 assertions. Brawl lifecycle:
  37 assertions, including native body Undo/Redo, cancelled/failed saves, New/open
  and immutable authentic binds/hierarchy/source bytes.
- The owned .NET 10 engine/CLI and native worker build with the local installed
  SDK. Worker build reports zero warnings/errors. No Core/native-game coupling
  or original Next numerical algorithm replacement is introduced.

Actual decoded edited-member measurements against the same current editable
master (one explicit native rotation edit, not a retarget demonstration):

| Fixture | DAT bytes | Times | Raw vertices / equations | Max local rotation (deg) | Max joint / surface displacement |
|---|---:|---:|---:|---:|---:|
| Mario Wait1 | 6,171 | 801 | 18,984 / 3,334 | 0.00000683019 | 0.00000289129 / 0.00000385224 |
| Fox Wait1 | 7,209 | 1,921 | 19,974 / 3,842 | 0.00001366038 | 0.00000244307 / 0.00000382215 |
| Samus Wait | 5,158 | 1,298 | 30,633 / 5,437 | 0.00001366038 | 0.00000305325 / 0.00000476875 |
| Mario Wait2 | 10,899 | 2,405 | 18,984 / 3,334 | 0.00001366038 | 0.00000302520 / 0.00000388347 |

Scalar, geometry and byte comparisons use fresh native resources. The reader and
writer share HSDRaw; independent frozen numeric fixtures provide separate witnesses,
not an independent full Melee runtime implementation. Failed diagnostic runs stay
local, including the early sparse-axis failure and pre-sharing Fox work rejection.
The quality limits were not relaxed to make these cases pass.

## Commands actually used

```powershell
# VS2022 MSBuild, Release; x86 only for ControlsAcceptance
MSBuild tests/MeleeAcceptance/MeleeAcceptance.csproj /p:Configuration=Release
MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86
MeleeAcceptance.exe --procedural <new-output>
MeleeAcceptance.exe <private-ISO> --inventory <local-json> --models --edited-roundtrip
MeleeAcceptance.exe <private-ISO> --inventory <local-json> --body-policy
./scripts/Test-BackgroundCapture.ps1 -Model <private-ISO> -Motion <private-ISO> -OutputDirectory <new-output> -NativeDirectory <harness-directory> -WorkspaceMelee -MeleeEditableTarget -MeleeTargetOnly -MeleeTargetCharacter <fixture> -MeleeTargetIdle <exact-idle> -MeleeTargetNext <exact-alternate>
./scripts/Test-BackgroundCapture.ps1 -Model <private-ISO> -Motion <private-ISO> -OutputDirectory <new-output> -NativeDirectory <harness-directory> -WorkspaceMelee -MeleeEditableTarget
./scripts/Build-Engine.ps1 -DotNet <absolute-local-dotnet-10> -NativeDirectory <native-directory>
./scripts/Test-BackgroundCapture.ps1 -Model <private-model> -Motion <private-motion> -OutputDirectory <new-output> -NativeDirectory <harness-directory> -NativeExportStructure
./scripts/Test-BackgroundCapture.ps1 -Model <private-model> -Motion <private-motion> -OutputDirectory <new-output> -NativeDirectory <harness-directory> -ProjectLifecycle
```

Dependency revisions remain HSDLib `85567e40797de3c820a55476eca54c704921a848` (MIT,
original vendored files/notices unchanged) and inspected doldecomp/melee
`598987cf347c125c7416517af18bf5c1dc3f15a1` (runtime facts; no decomp source copied).

## Remaining implementation — full model/tool parity stays OPEN

1. Native model geometry, weight, material and texture editing/write mappings;
   topology and native transform edits must be represented by the native backend,
   not silently judged through a display projection. This increment qualifies
   animation SRT editing and native motion-member export only.
2. Shared rig/skin evaluation for original Next, FK/IK controls, fitting, contacts
   and geometry-dependent optimization. HSD compensated scale, authored envelopes
   and dynamic owner bases must enter the existing format-neutral contracts.
   `ValidateSharedRigEvaluation` keeps the old missing-qualification gate explicit;
   an MDL0 display facade is not accepted as proof of compatible native rig math.
3. Multiple native motion-member/slot associations, animation-library addition to
   an existing Melee target, full action-archive/FTData offset repacking, native
   direct-file target import and independent asset conversion. A standalone member
   is not an installed/repacked Melee mod. Native edited AnimJoint writing and
   broader offset/terminal semantics need separate qualification.
4. Broader real modpack/build/costume, material/visibility/shape-animation, tool and
   actual-game acceptance. Existing keyboard registration/scaling timeouts remain
   open; no physical input, monitor DPI or human-usability claim is made here.

Future formats use the same source, target, evaluation and export boundaries with
format-specific implementations and evidence. There is no universal file reader,
no fake MDL0/CHR0 game export and no promise that an unimplemented adapter works.
