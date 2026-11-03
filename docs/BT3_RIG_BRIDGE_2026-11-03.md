# BT3 native rig bridge and static stance — preview.89

## Scope and source

Starting checkout commit: `70fc16138135ca5dc9d63b0294e529c00c3ab948`.
The inspected BT3 USA disc is local and is not redistributed. Its Goku Mid
form 001, normal costume 1 pmdl SHA-256 is
`bd03e3fb969e55e628dc0a0ed11e5f79dbfdc9b596f9453510e31a00c330b093`;
action 000 packed member SHA-256 is
`f6742842faa40ccffef1c8f4634c0a813614bb5d9be9ce1db20601d5eeddc3c4`.
The pinned [BT3 decomp revision 1c4d9060](https://github.com/z3xox/Tenkaichi3Decomp/tree/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5)
informs native action and side-group identity, not anatomical names or contact
acceptance. The exact local source hierarchy, rest geometry and weighted mesh
are the evidence used for anatomical suggestions and retarget bindings.

The BT3 native ancestor chain J00 → J01 → J02 now binds to the target's
reviewed actor, pitch and facing controls in order. A control is written only
if its own native action has meaningful authored local motion; packed identity
quaternion noise below 0.001° does not move TransN. J02's vertical action
motion therefore remains on YRotN in the checked Goku action rather than
becoming invented actor travel. Synthetic J00 travel and J01 pitch tests
confirm those controls transfer when they actually move. The original Next
constructor and its seven native bake regressions remain unchanged.

The source has two upper-leg joints per leg (J08/J09 and J12/J13 before the
knees J10/J14). Brawl Mario and Snake have one corresponding thigh joint.
The bridge keeps the original source chain and all skin weights, carries the
source knee and ankle world goals into the target's existing leg, and uses the
source knee bend plane for the target IK solve when an extra source thigh
segment exists. It adds no target bone or MDL0 bind change. Procedural motion
of J09 and J13 independently changed the corresponding target knee and foot
without changing TransN. This is chain adaptation, not a claim that both
source thigh rotations can be represented independently on a one-thigh rig.

Skinned hand branches with an unambiguous thumb and four ordered finger
branches map to the target's actual digit hierarchy. The checked Mario and
Snake targets each received 18 mapped finger joints. Ambiguous/incomplete
hands remain unmapped. The native viewer shows provisional finger and extra
thigh labels with original joint IDs, including model helper IDs above 70.
The current native BT3 motion writer still edits only IDs 0–70; helpers above
70 are visible but their Apply key control is disabled.

For a geometrically verified bilateral static stance only, the target body
and feet are corrected against both rigs' existing weighted foot patches.
The source's full two-pivot skin influences, target binds and target weights
are unchanged. Each foot correction is capped at 2% of the target leg length;
the master and freshly reopened candidate must keep floor clearance and
planar planted-foot drift within 0.001 × target leg length. Nonstatic actions
report this correction as unavailable. Dynamic contact, IK for other actions
and game floor state remain open.

## Background evidence

Release/x86 MSBuild succeeded with two preexisting CS1690 warnings. The
private-desktop acceptance harness passed 69 checks for native source/rig,
split-thigh perturbations, both Brawl target bakes, project Save/reopen and
original Next. The direct-library/port UI case passed 13 checks. The full
native viewer case passed 44 checks; fresh compact and wide viewport captures
were inspected. An additional action/form native bake case passed 28 checks:
Goku Mid action 001 and Super Saiyan form 002 action 000 reopened, while
static stance correction correctly reported unavailable. No active-desktop
input or capture was used.

| Same Goku Mid action 000 source → target | CHR0 bytes | Reopened witnesses | Maximum sampled world orientation error | Maximum master / decoded weighted-foot error |
|---|---:|---:|---:|---:|
| Brawl Mario | 291,924 | 1,081 | 0.00006258° | 0.000000596 / 0.000004992 native units |
| Brawl Snake | 291,888 | 1,081 | 0.00006556° | 0.000001349 / 0.000005158 native units |

Both reopened CHR0s have no animated TransN entry for this source action.
The extra thigh joints are recorded as evaluated leg-chain contributors,
not discarded source tracks.
Their exact target clips, native source snapshots and hashes survive project
Save/reopen. The checked Brawl target model fixture hashes are Mario
`6a90b79f2be0f57f43bba748e468a84bb9fbd69eeecd159c838dd0056aa8463`
and Snake
`c498c51757973949ad3d1b22eecc26e72ecb1a1b0afe7b13c29ef53fe0f09e4`.

A separate read-only action-000 binding sweep inspected 161 BT3 forms: 153
admitted the required humanoid mapping and eight (slots 62, 101, 102, 110,
113, 150, 152 and 157) lacked required roles and remained unavailable.
This is a structural admission sweep, not motion/contact qualification of
153 animations. The maximum sampled native/bridge skin position difference
was 0.000008741 native units; all original source weights were retained.

## Remaining gates

The source render, native Brawl draft and weighted static-foot tests establish
bounded local behavior only. Broader BT3 forms/actions, nonstatic floor and
ledge contacts, expressive fingers, BT3 loop semantics, Melee output,
target-native model/texture writing and in-game playback need separate
qualification. The reviewer must still inspect the ported body motion.
Physical input, monitor DPI and human usability were not tested. The one
G1/M2 milestone remains incomplete.

## Commands and local artifacts

From the active checkout, the build commands were Release/x86 MSBuild for
`tests/ControlsAcceptance/ControlsAcceptance.csproj` and
`BrawlCrate/BrawlCrate.csproj`, the latter with `OutDir` set to the ignored
`artifacts/Fighterport-bt3-rig-preview89/native` directory. The acceptance
harness was run on a private desktop with the local BT3 ISO as its first
argument and these option groups:

```
--bt3-inventory --bt3-source-rig --bt3-plan-probe --bt3-bake-probe --bt3-next-regression
--bt3-direct-library --bt3-port-ui
--bt3-direct-library --bt3-render
--bt3-inventory --bt3-binding-sweep
--bt3-inventory --bt3-source-rig --bt3-bake-samples
```

The fresh ignored native CHR0s and captures are in
`artifacts/bt3-rig89-final2`, `artifacts/bt3-rig89-ui` and
`artifacts/bt3-rig89-view`. The large and compact source-view captures were
visually inspected. The ignored local launcher was pointed at the preview.89
build and `Start-FighterPort.ps1 -Check` resolved its executable. Neither
private disc data nor generated artifacts belong in the public source commit.
