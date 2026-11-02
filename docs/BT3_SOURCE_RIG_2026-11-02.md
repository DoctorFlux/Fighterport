# BT3 native source rig and action lease — preview.87

## Scope

Starting commit: `bc61ce1e0799c54e9f9d21bdd18032b312d46c03`.
This read-only increment prepares an exact BT3 form, costume and primary-bank
action as one source lease. It keeps both the costume-pack hash and nested
native pmdl hash distinct from the packed action hash. Reusing a scene for a
second action first verifies the same costume-pack bytes. A later import can
call `VerifyUnchanged` before publication. Source preview now uses this lease;
scrubbing and playback use already-decoded motion and perform no disc reads.

The format-neutral source rig retains every native joint and each triangle
vertex's owner/parent two-pivot weights. It samples the original action's
translation and quaternion motion at fractional native frames, without
creating a CHR0, MDL0 or editable Brawl substitute. The existing Next
`HumanoidRetargeter` can evaluate these poses against an MDL0 target in a
nonpublishing probe. Proposed anatomical roles remain unreviewed and do not
authorize `Use on This Model`; that button stays disabled for BT3 disc choices.
The source head-size reference is measured from the decoded bind geometry
and proposed head/neck joints, rather than fixed to one character's value.

## Background evidence

- Local USA `SLUS_21678` Goku Mid first costume: nested pmdl SHA-256
  `bd03e3fb969e55e628dc0a0ed11e5f79dbfdc9b596f9453510e31a00c330b093`;
  packed action 000 SHA-256
  `f6742842faa40ccffef1c8f4634c0a813614bb5d9be9ce1db20601d5eeddc3c4`.
  No game resource is committed.
- The read-only source-rig comparison checked 10,305 skinned vertex samples
  at six Goku Mid action times, including fractional and terminal times.
  Maximum independent two-pivot position difference: `0.000003053248`
  native units; maximum bind split: `0.0000003371748` native units.
  Goku Mid low-health action 001 and Super Saiyan action 000 also passed the
  same `0.00001` sampled-position bound.
- A first-costume sweep covered all 161 forms; an expanded sweep covered all
  698 indexed costumes at action-0 frame zero and midpoint. The largest
  sampled native/bridge position difference was `0.00000874057` native units;
  largest bind split was `0.000007644281` native units.
- `--bt3-source-rig --bt3-plan-probe` used locally available Brawl Mario and
  Snake model fixtures as nonpublishing target-rig probes. Each produced 20
  source/target mappings. The probe checks the original format-neutral Next
  pose evaluation across the action at quarter-frame times; it does not
  bake, import, save or validate contact quality.
- Release/x86 MSBuild passed. Private-desktop `--bt3-inventory
  --bt3-source-rig --bt3-plan-probe` and `--bt3-direct-library --bt3-render`
  passed. The latter covered 43 checks, including source action switching,
  native target edit/Undo/Redo/save/reopen and unchanged original resources.
  Fresh source and large target captures were inspected. No OS input was
  injected and the active desktop was not captured.

## Next native transfer gates

| Destination | Current qualified output boundary | Required before a BT3 port is offered |
|---|---|---|
| Brawl | Existing CHR0 animation export for owned MDL0 targets | Adapt the original Next bake to the exact native BT3 sampler; review ambiguous body/root roles; validate source/target contact surfaces, protected root motion, fractional curves, save/reopen and project provenance. |
| Melee | Existing bounded native DAT action-member export for owned Melee targets | The same native source bake plus Melee action timing, fractional-curve writing, target rig/contact and native DAT reopen gates. Melee native model writing remains separate. |
| Ultimate, Rivals II | No qualified native target writer or runtime test here | Each needs an independently verified model/animation reader, target evaluator, writer, container workflow and game-specific acceptance. |

Model and texture transfer additionally requires a target-native geometry,
skin, material and texture writer. Animation retargeting alone cannot produce
a game-compatible character model. BT3 pmdl/BPE originals and the ISO remain
immutable. No BT3-to-target animation or model export, running-game pose
match, broad contact quality, actual game insertion, physical input or
mixed-monitor DPI acceptance is claimed by this increment. One G1/M2
milestone remains incomplete.
