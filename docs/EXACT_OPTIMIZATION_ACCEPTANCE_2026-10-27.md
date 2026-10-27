# preview.45 — standalone Exact animation cleanup

Accepted: **5,961 checks / 15 admitted background runs / 52 inspected native PNGs**.
The launcher selects preview.45-exact-optimization. High-quality remains the next
separately qualified increment within the same incomplete G1/M2 milestone.

## Product behavior and source boundary

**Animation → Optimize animation…** operates only on the current target's selected
project-owned CHR0 v4/v5. Ordinary imported animations need no donor or library.
It pauses through native playback handling without seeking. Original and candidate
previews are independent native BrawlCrate viewports; the editable target is retained.
Analyze is explicit. Apply updates the same native animation in one existing Undo
command, with unchanged target, bone, frame, camera, asset count and PAC siblings.

Only Exact is exposed. Core contains opt-in bounded-reduction math with procedural
checks, but no High-quality user option is exposed before preview.46's independent
native qualification. Nothing optimizes automatically on retarget, fit, Save/export
or reopen. Original Next is unchanged and already emits only actual target bones.

The shared Core reducer removes target-absent tracks and redundant Hermite knots
using exact rational polynomial/Bernstein identity over original source intervals.
Constants can collapse without guessing zero translation or unit scale. Retained
source coefficients, paired sides, signed-zero distinctions, sparse channels,
native loop timing and structural protections are preserved. Native flags, metadata,
entry order and complete float key/tangent bits pass independent I12 reopen.

Absent-target tracks are listed before Apply; no name/role/skin-weight heuristic
determines eligibility. Source originals and historical initial retarget/fit hashes
are not rewritten. Session review approval invalidates on authored Apply/Undo/Redo.

Session baselines are operation-owned and bounded to one current result. Repeating
unchanged optimization retains that baseline; ordinary export receipt changes do
not reset it. Manual changes or project reopen identify a new baseline. Reports
and optimizer settings are session-only; project schema remains 1.

## Frozen checks and honest size/quality claims

See [the frozen scope](ANIMATION_OPTIMIZATION_SCOPE_2026-10-27.md). Exact describes
mathematical scalar equivalence, not bit-identical float evaluation at all possible
times. Independent native samples measure float roundoff, complete forward matrices,
local raw Euler/orientation, all native bones and full skinned geometry, plus scalar
derivatives. Native quarter/withheld/extrema/near-knot witnesses and bounded adaptive
checks include the ordinary non-loop final hold and explicit looping closing sample.
These are sampled geometry results, not a continuous skin/contact certificate.

Native representation stays precise float I12. Actual standalone CHR0 bytes are
measured; key count is not a container-size estimate. Clean standalone stored-file
bytes are compared separately when known. Embedded BRES payloads exclude standalone
string/alignment overhead, so they are not used as an invalid standalone byte cap.
Candidate growth is rejected and no saving is an explicit no-op. No PAC/PCS size or
actual game acceptance follows without separately measured evidence.

Native work uses the existing reconstruction ceilings: 100,000 logical witnesses,
10 million combined bone visits and one billion skin visits. Source/candidate visits
both count; no sample density or fidelity bound is lowered to meet a budget. Counter
exhaustion explains actual required work and publishes no unverified candidate.

## Verification inventory

All application tests use serialized private desktops and the shared offscreen
renderer, isolated settings/library and original local fixtures. No OS input injection,
active-desktop screenshot or focus stealing. Generated output is ignored under
`artifacts/Fighterport-optimize45`, outside the source checkout.

Required final cases:

- Core exact/HQ scalar proofs, independent Hermite oracle, subnormals, paired/signed
  zero, full turns, loops, protected ranges, deterministic identity/permutation,
  unsupported input, work limits and cancellation.
- Donor-free Mario and Snake current-PAC editing, including original procedural
  dense constants and a target-absent tail track; independent adapter fixtures
  verify metadata, six native flags, mirrored/zero scales, loop closing, damaged
  encoding, fixed-time canonicalization, scale-tangent sharing, growth, aggregate
  work counters and cancellation.
- Authentic raw original Next and fitted replays in both rig directions, preserving
  the complete current target, immutable source roots, historical provenance,
  mandatory joins and source-owned root functions. Raw replay restores the accepted
  frozen source into a test-owned working clip and removes only its later fit recipe.
- Exact current-member Apply, one native Undo/Redo, sibling integrity, stale Apply,
  cancellation/late completion, retained review approval during preparation and
  invalidation after Apply, export before Save and exact fresh project reopen.
- Compact/wide 100%/175% UI, original/candidate playback without hashing, details
  disclosure and focus. The removed 200% layout remains removed.
- Existing native keyboard, native fitting/curve editor, verified set export, root,
  boundary and rotation regressions.

Representative commands:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory <native>
./scripts/Build-Engine.ps1 -DotNet <workspace-dotnet10> -NativeDirectory <native>
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=<native> --no-restore
# From engine/, with the workspace .NET 10 SDK:
dotnet build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore
dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll <output> --animation-optimization
# Serialized, from the checkout:
./scripts/Test-BackgroundCapture.ps1 -WorkspaceAnimationOptimization -Model <model> -Motion <motion> -NativeDirectory <native> -OutputDirectory <new-output>
./scripts/Test-BackgroundCapture.ps1 -OptimizationProject <accepted-project> -OptimizationRawNext -Model <model> -Motion <motion> -NativeDirectory <native> -OutputDirectory <new-output>
```

## Development findings

- Corrected a native C# 7.3 conditional conversion during integration; no language
  version or framework migration.
- Full scene reopening is deferred through native OnShown. The initial harness
  checked selection too early; an independent audit proved the saved PAC and
  standalone CHR0 exactly matched. Tests now await the actual exact selected member
  before controls readiness, without substituting another animation.
- Core's end-exclusive preview witness was initially outside the adapter's overly
  narrow non-loop interval. The adapter now verifies the final hold too, rather
  than dropping the witness or changing clip timing.
- Near-knot witnesses are retained for value checks, while sampled cumulative
  derivative chords use resolvable native-frame spacing; local derivative checks
  use the independently decoded Hermite polynomial. No division by tiny double
  probe gaps or false floating derivative certificate.
- Cancelled/closing dialogs retain Busy and ownership until the awaited work ends,
  preventing overlapping native verification or disposal of an in-use preview model.
- Native curve targets and Undo references are explicitly rebound after precise
  entry replacement; native selection is not replaced or the scene rebuilt.
- The early lower adapter work ceiling safely blocked the longer Snake corpus.
  It was aligned with the existing operation policy, without changing quality gates.

## Delivery and retained limitations

Admitted runs under the ignored `Fighterport-optimize45` artifact directory:

| Case | Checks | PNGs |
| --- | ---: | ---: |
| workspace-mario-final2 | 396 | 6 |
| workspace-snake-final2 | 454 | 6 |
| raw-next-mario-final2 | 458 | 2 |
| fitted-mario-final2 | 467 | 2 |
| raw-next-snake-final2 | 768 | 2 |
| fitted-snake-final2 | 777 | 2 |
| keyboard-final4 | 210 | 7 |
| native-transitions-final2 | 81 | 11 |
| set-export-final2 | 152 | 8 |
| workspace-mario-final3 | 396 | 6 |
| core-final3-animation-optimization | 39 | 0 |
| core-final3-boundary-fitting | 380 | 0 |
| core-final3-rotation-path | 127 | 0 |
| root-mario-snake / root-snake-mario | 628 each | 0 |

Fresh original/result poses, compact/wide 100%/175%, details, exact reopen,
keyboard recovery, native interpolation/fitting and set export were inspected in
13 labelled sheets plus a full-size compact optimizer. `visual-review.json` retains
the inventory and image hashes. Disabled native captions retain their inherited
low contrast; physical interaction is not certified by these captures.

Measured precise CHR0 export bytes (before → after): raw Sonic-to-Mario
246,912 → 73,824; fitted Mario 189,732 → 76,980; raw Mario-to-Snake
423,908 → 124,972; fitted Snake 338,468 → 120,668. All four reports measure zero
native local/world/skin and derivative error at their recorded witnesses. These
are resource-level savings, not PAC/PCS or game-memory measurements.

Source baseline: `385d207659c90573ad8b88265b8df98c3045a45c`.
Native version `2.0.0.25`, informational `2.0.0-preview.45-exact-optimization`.
Launcher `native-final2`; executable SHA-256
`378ED30794C187C88E20603121CB8E9625E92C4AC1DE6FD3D3CE3CF908AEAC64`.
Final Core SHA-256
`A0D3A5C8DCFAB6A4F40DEF63A97F51475983B1A55CD8A36BFEB71F85D3D2D348`.
Engine host SHA-256
`85366980997A8717B4E44EF3D6D5E44AE3C9A8C2429512BF7358924ACDE7990E`.

Final Core adds a shifted-constant guard: retain two authentic endpoints when the
first knot is not frame zero instead of proposing an invalid timed fixed channel.
The original successful native cases could not use that previously rejected
branch. All three Core suites reran, and the donor-free native case reran as
final3 on the installed final engine. Earlier Core hash was
`1D46E45EF30FF982692B6B7898A7D4431F5DBB1F0FD060E260ECE5DC579FFE33`;
native executable, engine host and compatibility worker are unchanged.

Excluded development runs include the deliberately rejected encoding/size/work
cases. Keyboard-final2 used mismatched Mario model/Snake motion registration;
final3 could not create its private desktop inside the sandbox. Correctly paired
and escalated final4 passes without a product or test change. No failed run is
counted as acceptance.

Publish source only to public Fighterport/main using
DoctorFlux attribution and contributor verification. Keep one incomplete G1/M2.
Physical input/DPI, human usability, universal contacts, Melee/new adapters, container
size claims and actual game acceptance remain unverified. High-quality remains next.
