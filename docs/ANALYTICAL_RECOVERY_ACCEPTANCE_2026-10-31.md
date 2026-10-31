# Preview.65.2 — generic analytical correction and fitted recovery

Starting commit: `a68f9bc1557565a4f9688a86ad6d84dcccda10f1`.
Active checkout and publication remain public `DoctorFlux/Fighterport/main`.
This is a bounded hotfix in the ONE incomplete G1/M2, not promotion of queued
preview.66 static ledges. The complete native Model Previewer, original Next
goals/solvers, native Undo, original skeletons/binds and every skin weight remain.

## Implemented changes

Analytical limb/end-contact corrections can switch branches without invoking
the coupled body solver. The offline correction trajectory now covers generated
non-suspended corrections, using policy
`original-next-eighth-correction-trajectory-2`. Every original solved eighth-frame
pose remains an exact reference anchor; source FK is evaluated continuously.
Additional original-solver anchors are proposed only where full-influence support
geometry needs them. Protected motion controls, unmapped locals and all scales
are excluded from correction transport. There are no character/action/name/hash
or bone-index numerical exceptions.

Near a rotation pole, Euler-path guidance checks the physical orientation of
half-span guides. Actual source/correction breakpoints and their midpoints are
included in parent-curve fitting/validation. This prevents a child from inheriting
a narrow parent-error spike missed by the parent's earlier witnesses. Native
Euler interpolation, winding and authored root channels remain; no quaternion
curve replaces CHR0 storage.

Hard curve limits remain 0.025° world orientation, 0.0002 native-unit local
translation, 0.00001 local scale, depth 20 and two-million native curve work/keys.
The additional optional support proposal has a separate two-million-work cap;
it does not raise the curve-bake limit. Its complete node list publishes atomically.
Exhaustion or an unavailable support reserve retains all original correction
anchors and explicitly reports contact quality unqualified. Cancellation,
nonfinite input and mandatory native/root-gate failures still abort. A retained
reference is not a partially corrected or contact-qualified result.

Mario's actual library path also exposed a source-snapshot issue before solving:
nine original L4 streams contain exactly N=33 floats (132 bytes each), not N+1.
The original CHR0 resource block is 11,592 bytes; that is not asserted to be a
standalone file length. A dedicated read-only snapshot proof now checks complete
v4/v5 dictionary/neighbor bounds, metadata, flags and every projected IEEE tuple.
Only unchanged, unambiguously bounded N-sample L4 groups may use exact I12 in the
independent source snapshot. It neither invents a terminal nor reads one from the
next stream. The original resources remain unchanged. Ordinary writing, edited
dense/unknown-terminal/gap protections and generated codec policies are unchanged.
This is not new game/editor compatibility for writing short L4 payloads.

No UI options, project-format changes, alternate document owner, playback solving
or automatic export optimization were added.

## Qualification

Local evidence is ignored under workspace `artifacts/Fighterport-tilts652`.
Failed experiments and the prior qualified packages are retained.

- `precision31`: **1,168 checks**. Eight original procedural combinations cover
  renamed bones, sibling-storage permutations, unequal scale/proportions,
  five-influence support geometry, negative/nonuniform animated scale,
  continuous narrow source-FK motion, root travel/720° turns, cancellation,
  atomic exhaustion, off-midpoint poles and narrow mandatory parent knots.
  Each support combination independently checks 1,030 fixed/withheld times.
  Short-source tests cover v4/v5, 1/3/33 frames, exact snapshots, final holds,
  source immutability, and rejection of edits/timing/loop/gaps/nonfinite data.
- `final-roots36`: **628 checks**; `final-coordinates36`: **94 checks**. Original control
  ownership, native persistence, unusual coordinate frames and existing rig
  relationships remain tested.
- `final-use32-*`: **148 checks**, actual source-library selection, both source-key alternatives,
  explicit result review, current import, own-Wait1 fitting, one native Undo/Redo,
  exact beginning/protected middle/attack-owned translation, immutable original
  sources/models/all weights, native export and project Save/reopen.
- `dense24`: **123 checks** of existing generated L4/protection/quality behavior.
  `write24`: **55 checks** of native no-edit/one-edit/worker/PAC preservation.
  The source-only extension does not disable ordinary write guards.
  These two earlier candidate runs precede the final mandatory-time authoring
  fix; their tested generated/native write paths are unchanged. They are retained
  continuity evidence, not fresh runs of the final package.

Independent native reopen oracles check original eighth-frame anchors, integer
and withheld `.137/.371/.623` samples, native bone transforms and uncapped native
skin support regions. Root-local error is zero. Support orientation is compared
with original Next at supported witnesses, not treated as a continuous proof.
Broad contact/fitting quality is not inferred from reopen success.

| Exact donor member → target | Snake AttackS3S → Mario | Mario AttackS3S → Snake |
|---|---:|---:|
| Duration, native frames | 32 | 33 |
| Independent sample times / original anchors | 342 / 249 | 353 / 257 |
| Maximum original-anchor world orientation error, degrees (limit 0.025) | 0.011175475 | 0.016057710 |
| Maximum original-anchor local translation, native units (limit 0.0002) | 0.000193656 | 0.000124605 |
| Source-owned root-local error | 0 | 0 |
| Full support observations | 379 | 161 |
| Maximum support penetration, native units | 0.000687503 | 0.001215887 |
| Unchanged reviewed contact position bound | 0.003940793 | 0.009954112 |
| Maximum planar lock drift | 0.001120960 | 0.000548996 |
| Supported orientation vs original Next, degrees (limit 0.25) | 0.051070428 | 0.074724768 |
| Direct dense master bake, milliseconds | 1,383 | 1,208 |
| Actual Use, channel-key mode including existing quality checks, milliseconds | 13,513 | 14,442 |

Both source-key alternatives pass each independent native oracle. These sampled
measurements concern retargeted masters; the fitted endpoint/body/middle/root
contracts are checked separately by actual-use tests. They do not qualify every
surface or the whole fitted contact timeline.

| Direction | Source-key option | Actual retargeted CHR0 bytes | Fitted CHR0 bytes |
|---|---|---:|---:|
| Snake → Mario | Bone | 169,348 | 140,120 |
| Snake → Mario | Channel | 164,260 | 136,872 |
| Mario → Snake | Bone | 182,836 | 146,580 |
| Mario → Snake | Channel | 155,808 | 128,884 |

These are physical standalone exports of distinct finished motions, not a new
compression benchmark or a claim of matching donor size. No automatic lossy
optimization was introduced to make the new full-quality masters pass.

Both source motions are the exact inventoried AttackS3S member: Snake's 32-frame
first part and Mario's 33-frame move. No missing AttackS3S1 member is substituted.
End-only fitting uses ten samples and the target's own frozen Wait1 frame 1.
The original beginning is retained and no idle is appended to either CHR0.

Commands actually run include MSBuild 17 Release/x86 with
`BuildProjectReferences=false /m:1 /nr:false`, `--fractional-probe`,
`--native-member-audit`, `--bake-probe`, `--local-precision`, `--motion-controls`,
`--coordinate-frames`, `--verify-trajectory`, and the existing
`Test-BackgroundCapture.ps1` cases `WorkspaceAuthenticLibraryFloat`,
`RetargetTrajectoryCase`, `ReviewedRecoveryCase`, `NativeDenseFloat` and
`NativeChr0WritePath`. Captures use the shared `Build/Run/Review-SideComparison`
tools, serialized private desktops and the native offscreen renderer.

Final captures (`final-media34-*`) pass **684 renderer assertions**. All **130
decoded half-frame GIF frames** were visually inspected on **17 sheets**, plus
both full-resolution final comparison PNGs and all **12 actual-use UI PNGs**.
Both animations remain fully visible in a level +X orthographic side view with
the reviewed gold floor. Original beginnings and action sections match across
the two panels; recovery changes only the fitted ending. The separate frozen
Wait1 card shows frame 1 only with retained attack travel. No unexplained spin,
T-pose, clipping or dropped model was observed in this bounded visual review.
Snake's full-motion framing leaves more margin than Mario's. A floor guide and
visual inspection are not proof of continuous contact or game correctness.
Exact inspected-image hashes are in the ignored local `visual-review36.json`.

Frozen local package: `Fighterport-tilts652/native1`, version
`2.0.0-preview.65.2-analytical-recovery`. SHA-256:

- Application: `B2F1C48BFE5A6FA993BDCB9B002258256F20A54C82DF68E72F59615299F55E5A`.
- BrawlLib: `D930DBF9DE21F1CEF7F2606A181F4BB66EA830D529F940B52B116A0E8038F458`.

The engine/worker binaries retain the qualified .65.1 package unchanged; this
increment rebuilds the application/native library and the acceptance executables.
The app build has two existing CS1690 warnings; BrawlLib and both acceptance
builds have zero warnings. Source only is eligible for public publication.

## Retained failures and held-out cases

The .65.1 baseline reproduces both reported tilt failures before any changes.
All failed trials remain local. An intermediate candidate exposed a Sonic tilt
parent error of 0.025102° against the unchanged 0.025° gate; it was rejected.
Mandatory actual source/correction times resolve that regression. Final Sonic
AttackS3S → Mario bakes/reopens in 1,678 ms; Sonic down-air → Mario, supplied
Mario up-air → Sonic and Mario Wait1 → Snake also pass the held-out bake corpus.

A newly added support-orientation comparison against independent raw subframe
Next solves measures **0.793463626°** for Sonic tilt on Mario. The exact same new
measurement on the already qualified .65.1 baseline is **0.793463626°**. It is an
existing unreviewed contact-orientation limitation, not a passing 0.25° result.
The final Sonic output passes its original native-anchor/curve/root gates and
reduces sampled penetration from 0.00268659 to 0.000432387 native units, but is
not qualified under that new contact-orientation criterion. No threshold was
weakened and no universal contact success is inferred. The requested two
Mario/Snake tilt directions pass the new criterion as well.

Snake DownStandU → Mario in automatic ground mode cannot satisfy the optional
support reserve within its bounded proposal. The final run keeps the complete
original reference, reports contact quality unqualified and passes native
motion/reopen gates in 18,417 ms; it does not invent contact acceptance. Existing
reviewed ground recovery remains a separate explicit correction workflow.

## Boundaries

Generic mechanisms are tested on unfamiliar procedural rigs plus authentic
Mario/Snake/Sonic fixtures; that is not proof for every possible model, animation,
anatomy or game adapter. Missing/ambiguous reviewed mappings and contradictions
remain explicit. Suspended/ledge work stays queued. Raw independent subframe IK
optima are not the new coherent inter-anchor master reference. Compression is
still measured against the same finished target, not against the donor.

Physical input, monitor DPI, human usability, continuous-time contact guarantees,
broad contact/clearance/style quality, external editors, Melee and actual games
remain unverified. The GIF floor is a reviewed target guide, not in-game evidence.
