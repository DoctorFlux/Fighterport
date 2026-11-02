# BT3 playback bounds and idle-label correction — preview.84

**Superseded idle evidence:** preview.85 corrected the action-to-pack-member
off-by-one. The frame-bounds fix remains valid, but the preview.84 idle
captures and action-member identity claims used the next member. See
[correction](BT3_ACTION_INDEX_2026-11-02.md).

Starting commit: `c29d65bea70143c38ee25af9046f0658e9f61e89`.

The reused Brawl playback slider permits display frame 0. BT3's target
transport maps display frame 1 to native time 0, so the previous subtraction
sent -1 to `Bt3NativeAssetReader.Sample`. The target seek and render paths now
clamp to `[0, Motion.Length]`; selected-bone display and key edits use the
same valid native range. No native animation data is changed by the clamp.

The decomp still identifies action 0/1 as the ground motions selected when
`lowHealthIdle` is clear/set. Labels now say `Action 000/001 · ground motion ·
normal-health/low-health selection`; they do not present either preview pose
as a visually verified healthy idle. Costume damage remains independent.
The user's observed mismatch requires an independent game pose comparison;
the motion-ID proof alone does not settle it.

## Verification

- Release x86 build passed; two existing CS1690 warnings.
- Private-desktop BT3 direct-library/render case passed 39 checks on the
  locally supplied USA ISO, including frame-zero input, an oversized seek,
  action changes, key edit/Undo/Redo and standalone BPE save/reopen.
- Fresh compact offscreen capture was inspected. No physical input or running
  game comparison was performed.
- The isolated preview.84 native executable is byte-identical to the tested
  build: SHA-256 `ab41efd71d365ed36a91c56cafe1263bfe46f6a87fdcd442019dc0e9a905d375`.
  The unchanged preview.83 engine payload was reused byte-identically:
  SHA-256 `d12e269418c160ba2593117d346ad2c45b93d38cbdaccfccf09931ea10ec0aa8`.
  `Start-FighterPort.ps1 -Check` resolves preview.84.

Full BT3 animation/model editor parity, project Save, retarget/fitting,
game-pose equivalence and game acceptance remain open in G1/M2.
