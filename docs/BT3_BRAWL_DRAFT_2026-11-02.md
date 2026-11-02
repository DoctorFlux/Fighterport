# BT3 action to Brawl review draft — preview.88

## Boundary

Starting commit: `dadacc5013dba6edf45b1838c33a0de8d9d24ebf`.
This increment enables **Use on This Model** for an exact BT3 disc form,
costume and supported primary-bank action when the current target is an owned
Brawl MDL0. It does not replace the original Next solver or the native Model
Previewer. The BT3 pmdl/BPE stays a native source; the target is an independent
Brawl CHR0 **review draft**, not a converted model or a BT3-format output.

The native BT3 sampler feeds the existing format-neutral Next pose solver and
fractional CHR0 baker. A checked ancestor-control map handles unequal BT3 and
Brawl root chains in order. It maps the source's one authored translation
control to the target's actor-motion control and rejects multiple ambiguous
translation controls or a missing target motion role. The null/default path of
`HumanoidRetargeter` retains its original equal-depth behavior. Each candidate
rejects actions with authored keys beyond the qualified sampled endpoint;
those terminal semantics are not guessed. The review names animated source
joints without a target binding and counts gameplay events excluded from the
skeletal CHR0.

Each accepted candidate uses paired native key sides, precise CHR0 writing,
fresh native reopen and
the unchanged `0.025°` world-orientation, `0.0002` local-translation and
`0.00001` local-scale decoded witness bounds. A failed bake is discarded.

Before adding a draft, the UI displays exact source/target identities and the
provisional body mapping. Project storage keeps separate exact native costume
and action snapshots, their hashes, the nested pmdl rig hash, target model
content hash, form/costume/action identity and an explicit draft status.
Saving and reopening a project verifies those owned source snapshots. No disc
data or generated binary is committed to source control.

## Background evidence

Local private USA disc volume `SLUS_21678`; original BT3 sources were read
directly from the ISO and remained unchanged. Local target fixture hashes:
Mario `6a90b79f2be0f57f43bba748e468a84bb9fbd69eeecd159c838dd0056aa8463`,
Snake `c498c51757973949ad3d1b22eecc26e72ecb1a1b0afe7b13c29ef53fe0f09e4`.

| Exact source → target | Serialized CHR0 bytes | Reopened witness coverage | Observed maximum world orientation error |
|---|---:|---:|---:|
| Goku Mid form 001, costume 1 normal, action 000 → Brawl Mario | 180,496 | 1,081 times | 0.00005858° |
| Same source → Brawl Snake | 180,388 | 1,081 times | 0.00006384° |
| Goku Mid form 001, costume 1 normal, action 001 → Brawl Mario | 272,320 | 1,621 times | 0.00009299° |
| Goku Mid Super Saiyan form 002, costume 1 normal, action 000 → Brawl Mario | 272,728 | 1,621 times | 0.00007712° |

The Goku Mid action-000 pose probe sampled 289 quarter frames for each
target. Its maximum source-to-target actor-travel difference before baking
was `0.000018715` native units. The action-000 Brawl CHR0s then passed the
fresh decoded native checks. These are sampled checks, not a proof at every
continuous time. The sizes are full-quality I12 draft outputs; size
optimization remains an explicit, separate export operation.

Private-desktop `--bt3-inventory --bt3-source-rig --bt3-plan-probe
--bt3-bake-probe --bt3-next-regression` passed 40 checks, including seven
original Next native bake checks and fresh project Save/reopen of both BT3
drafts. The separate action/form sample run passed 28 checks. A cancelled
bake produced no result. `--bt3-direct-library --bt3-port-ui` passed 13
checks: exact action enables Use on This Model, the review dialog appears,
the target native editor selects the independent result, the original source
preview resumes, and cancelling a second review preserves the target clip,
frame and source registrations. Fresh private-desktop captures were inspected,
including the actual offscreen source viewport after import; a window bitmap
alone does not display that OpenGL surface.
No active-desktop input or screen capture was used.

## Open gates

- BT3 body-role suggestions are structural proposals. The draft review shows
  the mapping, but broad action/form and contact acceptance remain open.
- Contact quality, floor/ledge placement, action-specific root interpretation,
  native BT3 loop policy, game insertion and running-game playback are not
  qualified by a CHR0 reopen. Source-authored root travel is retained within
  the sampled native bound, not claimed mathematically exact at all times.
- Melee target-native DAT output from a BT3 source needs its own curve/timing,
  root/contact and fresh-reopen acceptance. BT3 pmdl/texture transfer to a
  target game needs separate native model/material/skin writers. Ultimate and
  Rivals II remain future adapters.

The one G1/M2 milestone remains incomplete. No physical-input, monitor DPI,
human-usability, broad contact-quality or actual game acceptance is claimed.
