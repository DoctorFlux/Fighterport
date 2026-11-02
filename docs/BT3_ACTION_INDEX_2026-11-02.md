# BT3 primary motion bank action index — preview.85

Starting commit: `33074f050e7046bc8699e978c5f66eb256727edc`.
Inspected decomp: `z3xox/Tenkaichi3Decomp` at
`1c4d9060d859f5bc53a0843ad5937c04f43aa6e5`.

## Cause and correction

`BtlObj_BindTables` binds game action `i` through
`BObjFile_GetEntry(&slot->file[1], i+1)`. `BObjFile_GetEntry` treats pack
word zero as its count header and returns the resource at `buf[n]`.
Consequently `n=1` addresses the first resource, pack member **0**. The old
Fighterport code used pack member `i+1`, so its displayed action 000 was the
game's action 001 and displayed action 001 was action 002. This directly
matches the user's observed damaged stance and apparent takeoff motion.

All primary-bank selection now uses `PackIndex == action` in the shared
library, source mini-preview, target viewer and compact disc inspector.
Action lists include member 0 and retain the game's `< 0x19E` action bound.
The original disc bytes and costume packs are untouched. Independent BPE
edits retain their exact member identities. Existing edits keyed to an old
mislabelled source resource are not silently reassociated.

## Verification

- Release x86 build passed. Two existing CS1690 warnings appeared on clean
  builds.
- Local USA disc fixture Goku (Mid) primary bank: member 0/action 000 has
  72 native frames, member 1/action 001 has 108; their SHA-256 values differ.
- Private-desktop direct library/source/target case: 40 checks passed. Fresh
  action-000 and action-001 target captures were inspected. Action 000 now
  shows Goku's guarded stance; action 001 shows the slumped stance.
- Whole-disc inventory sweep: action 000 decoded against 161 forms and 698
  costumes; 954 additional sampled actions decoded. Some sampled higher
  members remain unsupported by the bounded BPE reader and were reported,
  not substituted. The 21 inventory checks passed.
- No physical input or running-game pose comparison was performed. The
  decomp proves the member mapping; it does not alone prove game-exact
  materials, lighting or every sampled pose.
- The isolated preview.85 native executable matches the tested build by
  SHA-256 `6fd141f4fe5dc993ddf7db44f84e6bacbb1807109db19427d7808331900a7c3c`.
  The unchanged engine host was reused from preview.84 with SHA-256
  `d12e269418c160ba2593117d346ad2c45b93d38cbdaccfccf09931ea10ec0aa8`.
  `Start-FighterPort.ps1 -Check` resolves the preview.85 executable.

The previous preview.83/.84 idle captures and action-member identity
assertions are superseded. The preview.84 frame-bounds fix remains valid.
