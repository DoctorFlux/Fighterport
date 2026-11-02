# BT3 in the shared Model Previewer — preview.83

Starting commit: `b7f8eee0b0a0268a1ee218cd3d068707740a09e2`.
Inspected BT3 decomp: `z3xox/Tenkaichi3Decomp` at
`1c4d9060d859f5bc53a0843ad5937c04f43aa6e5`.

## Qualified change

The native BT3 target now occupies the original BrawlCrate Model Previewer
viewport, Animations, Objects, Textures, Bones and playback panels. Native
object/texture visibility and bone markers use those panels. The markers
reuse the BrawlCrate node orb and axis presentation and support viewport
picking. The compact layout keeps the Return to project action visible.
The native BPE key editor remains inside the same window with its independent
Undo/Redo and standalone motion save/reopen. Opening and closing the BT3
target preserves the existing editable Brawl project. No BT3-to-CHR0 master
or altered disc resource is created.

## Idle identity check

`battle_load.c` requests the primary action bank at character file ID +8;
`bobj_a.c` binds action `i` to pack entry `i+1`. The viewer reads that same
primary bank. `BtlAct_GetIdleMotion` selects ground action 0 when the
`lowHealthIdle` flag is clear and action 1 when set. `BtlChar_PostScene`
sets that flag under the low-health threshold when allowed by combat state.
The model's normal/battle-damaged costume file is selected separately at
character file ID +0/+4. Hence action 0 and 1 should not be swapped based
on appearance alone. The game-exact rendered pose and lighting have not
been compared with a running game and remain open.

## Background verification

- Release x86 ControlsAcceptance build: passed; two existing CS1690 warnings.
- Private-desktop `--bt3-direct-library --bt3-render`: 37 checks passed on
  the locally supplied USA ISO. Covered native panel routing, visibility,
  bones, picking, 175% compact layout, action change, independent key edit,
  Undo/Redo, BPE save/reopen and normal/damaged costume versus action 0/1.
- Private-desktop `--workspace-library-target`: 39 Brawl checks passed.
- Private-desktop `--workspace-melee --melee-target-only`: 34 Melee checks
  passed.
- Fresh wide and compact offscreen captures were inspected. The compact
  Return to project button is visible; textured model, bone markers and
  native lists share the original editor frame.
- The isolated preview.83 native viewer build succeeded. The standard
  packaging script could not rebuild the unchanged engine because the
  checkout requests .NET SDK `10.0.402` and the machine currently exposes
  SDK 7 and 9. The package uses the byte-identical preview.82 engine host
  (`SHA-256 d12e269418c160ba2593117d346ad2c45b93d38cbdaccfccf09931ea10ec0aa8`)
  with the newly built native viewer (`SHA-256 ac059f34443979addef4f61d2dc787fcf8d90737e97fe0da1fdb807ce29089052`).
  No engine source was changed in this increment. `Start-FighterPort.ps1 -Check`
  resolves the preview.83 executable.

Tests were background-only. No physical keyboard/mouse, game runtime,
cross-game retargeting, full BT3 gizmo/curve authoring, project Save, ISO
insertion or universal-format parity was accepted. Private game assets and
captures remain local.
