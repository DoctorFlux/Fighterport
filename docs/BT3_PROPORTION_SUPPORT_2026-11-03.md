# BT3 proportion and weighted support bridge — preview.90

Starting checkout commit: `bfa9eb6c3e634663268fd35526d63e4f0a308812`.
This increment stays in the one incomplete G1/M2 milestone. It changes neither
the original Next retargeter nor the BrawlCrate Model Previewer. Source pmdl,
motion bytes, MDL0 binds, hierarchy and every skin weight remain unchanged.

## Implementation and bounds

The BT3 source adapter now derives hand and body support patches from the
existing two-pivot weighted vertices and the reviewed anatomical rig roles.
The contact marker excludes zero-weight native influence slots; retaining such
slots in the marker average previously produced `NaN` and suppressed real
stationary support. No joint, mesh, action or character name selects a numeric
retargeting correction.

BT3 → Brawl performs a quarter-frame geometry preflight for palms near the
source bind-foot plane. Only source motion with detected planted palm samples
enters the existing Next coupled body/hand/foot contact solver. A body or shin
surface crossing that plane alone cannot enable this branch. The candidate
must pass the unchanged fractional native bake, then fresh CHR0 reopen and
decoded support-height, planar-lock and 0.25-degree end-orientation checks.
The support position bound is `0.001 ×` the reviewed target limb length.
Complete bounded original-support evidence is frozen, attached to BT3
project provenance and required before publication;
an over-budget or unavailable context rejects the draft. Contact detection,
solving, source hashing and verification run during preparation, never during
playback.

For actions without a qualified palm plant, an independent body-pelvis shift
preserves the **lowest** source weighted-sole height relative to its own bind
floor, scaled by reviewed source/target leg lengths. This changes no actor
control, bind or weight and does not claim a planted contact. The existing
strict bilateral static stance gate still takes precedence when available.

## Background evidence

The local, private USA disc fixture is BT3 form slot 007, costume member
`PZS3US1.AFS/01493`, action 000. Its pmdl SHA-256 is
`ac85e361773657efe9d04478672b61dec74b6d3038a761f3ff6a4d03c9725b7c`;
the packed action SHA-256 is
`81795fe10ab4e32a8ca97c9486b1e5051f09fbc495a15fef495bbaa908228bea`.
The native action has 57 tracks, 936 keys and 204 display frames. The Brawl
model fixture hashes remain Mario
`6a90b79f2be0f57f43bba748e468a84bb9fbd69eeecd159c838dd0056aa8463`
and Snake
`c498c51757973949ad3d1b22eecc26e72ecb1a1b0afe7b13c29ef53fe0f09e4`.

| Source → target | Reviewed leg scale | Fresh CHR0 bytes | Decoded witnesses | Maximum lowest-sole difference |
|---|---:|---:|---:|---:|
| GT Goku → Brawl Snake | 2.137393 | 788,736 | 3,046 | 0.000003741 native units |
| GT Goku → Brawl Mario | 0.846184 | 788,760 | 3,046 | 0.000001954 native units |

The GT → Snake standalone CHR0 SHA-256 is
`8b1cf581d8a1b2bb18c16943ca428f75083243b401972d7d468bbbe564168f4b`.
Maximum sampled world orientation differences were 0.000236° for Snake and
0.000184° for Mario. No Brawl actor travel was manufactured for the checked
Goku Mid action in the separate native/Next regression.

A five-frame synthetic hand plant made from the exact GT source rig retained
its full native weighted mesh. Both source palms were detected as support;
on the larger Snake rig their target clearances were approximately
`0.000000084` and `-0.000001202` native units. The reverse synthetic
Snake-pose → smaller GT-rig hand plant also reached both target weighted
palm patches within 0.01 native units. The synthetic large-target support
timeline retained exact source/target rig identities, hand ranges and
ancestor dependencies in the existing bounded project evidence shape.
Both pose tests kept all mapped
actor/root controls identical to the uncorrected pose and did not alter any
joint count or weight. This reverse test is **pose-level only**: BT3 BPE
retarget writing and save/reopen remain unqualified.

Release/x86 build succeeded with two preexisting CS1690 warnings. Private-
desktop harness runs passed: 36 focused proportion checks, 69 native/Next
checks, 28 additional native-action checks, 13 BT3 workspace checks and five
read-only native-preview checks. Fresh original Model Previewer captures of
the reopened GT → Snake draft at frames 1 and 102 were inspected. They show
the untextured test model and do not establish contact or game appearance.
No active-desktop capture or input injection was used.

## Open quality and compatibility gates

The nonstatic GT action does not meet the stricter static planted-foot gate.
The lowest-sole transfer is verified, but its other foot differed from the
source-scaled height by as much as **0.0127303 target native units** at four
checked frames. A per-foot IK experiment introduced a 0.11566° fractional
native rotation error and was rejected by the unchanged bake gate; it is not
in the delivered code. The actual GT action 000 did not contain a detected
hand plant, so no real hand-supported BT3 action has yet passed the complete
native-contact publication gate. Body-only floor, ledge, moving support,
unequal-rig reach conflicts, broad action quality, reverse native BT3 BPE
output and actual game playback remain open. Procedural geometry plus these
fixtures do not prove success for every body proportion; incompatible or
ambiguous source/target anatomy must fail rather than invent motion.

## Commands and local artifacts

The acceptance harness was built from `tests/ControlsAcceptance/ControlsAcceptance.csproj`
with MSBuild Release/x86 and run through `ControlsAcceptance.exe` on a
private desktop with the local BT3 ISO as first argument. The option groups
were:

```
--bt3-inventory --bt3-proportion-probe
--bt3-inventory --bt3-source-rig --bt3-plan-probe --bt3-bake-probe --bt3-next-regression
--bt3-inventory --bt3-source-rig --bt3-bake-samples
--bt3-direct-library --bt3-port-ui
--native-animation-preview
```

Generated CHR0s, receipts and captures are ignored local files under
`artifacts/bt3-proportion90-evidence`, `artifacts/bt3-proportion90-project-final`,
`artifacts/bt3-proportion90-actions`, `artifacts/bt3-proportion90-ui-final` and
`artifacts/bt3-proportion90-capture`. No disc bytes or private assets are
included in the source commit.
