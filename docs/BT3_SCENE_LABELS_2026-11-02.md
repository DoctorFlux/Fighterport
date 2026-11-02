# BT3 native viewer labels and action switching — preview.86

## Scope

This increment keeps the original Model Previewer and independent native BT3
target document. Changing actions within one costume now retains the existing
scene, camera, object/texture visibility, bone list and selected joint. Only
the native motion and selected animation row change. Switching form or
costume still loads its exact model resource.

The action list uses short labels for the shared IDs whose selection is
supported by [BtlAct_GetIdleMotion and BtlAct_MoveHandler at pinned decomp
revision 1c4d9060](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/src/battle/btl_act_d.c).
Action tooltips explain the selection and mark blended movement side layers.
Unidentified actions retain exact numeric IDs. “Goku (Mid)” is the readable
presentation of the source roster's “Goku (Med)” slot; the slot and AFS
identities are unchanged.

The [native `BobjBone` layout at that revision](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/include/battle/bobj_a.h)
provides numeric joint IDs, hierarchy and rest translations, not anatomical
names. The viewer retains each exact joint ID. It adds display-only body-role
proposals from the existing generic rest-geometry characterizer, marked `?`,
with tooltips saying they are unreviewed. The remaining joints use the native
side-group classification from `BtlObj_GetNodeSide`. No proposed label is
used as a retarget binding or written into a game resource.

## Background evidence

- Starting source commit: `d6b5bb2efd9d365fa7fe199aa0b6207bb2312048`.
- Local BT3 USA `SLUS_21678` source: Goku Mid model member hash
  `bd03e3fb969e55e628dc0a0ed11e5f79dbfdc9b596f9453510e31a00c330b093`;
  action 000 packed member hash
  `f6742842faa40ccffef1c8f4634c0a813614bb5d9be9ce1db20601d5eeddc3c4`.
- MSBuild Release/x86 acceptance harness passed. Private-desktop
  `--bt3-direct-library --bt3-render` passed 42 checks, including selection,
  native edit/Undo/Redo/save/reopen, scene identity and preserved mesh
  visibility and joint selection after action changes.
- Private-desktop `--bt3-inventory --bt3-mesh` passed 23 checks. The tested
  10,305 native skin samples had a maximum two-influence bind-position split
  of `3.37174782316652e-7` native units. This is a necessary source bridge
  check, not proof of motion or contact equivalence.
- A read-only first-costume sweep of all 161 forms found 153 with complete
  **proposed** core body-role lists, 8 incomplete and 0 parse failures.
  Anatomical correctness was not independently verified.
- The fresh compact 175% text capture was inspected: short action names,
  exact joint IDs, `?` role suggestions and object/texture IDs are legible.

## Remaining gates

`Use on This Model` remains disabled for direct BT3 disc choices. Enabling it
requires an independently validated native source rig/skin adapter, exact
native action sampling and timing into the shared solver, reviewed body/root
bindings, source identity/provenance in the owned project, and the existing
retarget contact/quality gates. A temporary CHR0 or MDL0 display projection
must never replace original BT3 data or silently claim that all actions are
qualified. The user's disputed ground-idle appearance still needs a running
game comparison. Project Save, native BT3 target parity, ISO insertion and
game acceptance remain open in the one G1/M2 milestone. All tests here were
background-only; physical input and monitor DPI were not tested.
