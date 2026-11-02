# BT3 PS2 USA: bounded disc inventory acceptance

Starting Fighterport commit: `d34679ef908f1bd20c494ee99168dd519954dce8` (preview.76). This is an inventory and original-byte export increment within the existing incomplete G1/M2. It does not qualify BT3 model rendering, animation decoding, retargeting or edited native writing.

## Pinned format evidence

- `z3xox/Tenkaichi3Decomp` at `1c4d9060d859f5bc53a0843ad5937c04f43aa6e5`: `port/tools/extract_disc.py` identifies numbered AFS members; `src/sys/file.c`, `include/sys/file.h`, `src/battle/battle_load.c` and `include/battle/battle_work.h` establish one-based partition-1 IDs and ten fighter entries per slot (eight costume/model packs plus two motion banks). Source is AGPL-3.0; no code or assets were copied into Fighterport.
- `ViveTheModder/dbzbt3-research` at `11ab5b0c5d4e3d86bfd0a20abf3a73bbe45aa3a8` documents the main `PZS3US1.AFS` archive. This was used as format evidence, not as proof of runtime compatibility.
- `ascomods/anim-converter` at `e865d3f0d7417cd04bda6d9bf2720ef068ca88fd` documents BT3 ANM import/export through FBX and explicitly reports unresolved rotation issues. Its GPLv3 implementation was not copied. It does not make the ISO's packed motion-bank children directly retargetable.

## Implemented boundary

`Bt3DiscInventory` checks ISO9660 `CD001`, `PLAYSTATION`, volume `SLUS_21678`, executable `SLUS_216.78`, the `DATA` directory and three expected AFS archives. It bounds directory/table reads and offsets, parses 161 exact fighter slots from `PZS3US1.AFS` without extracting the game, verifies each available model pack's native `pmdl` child and records both motion banks' exact child indices. Disc labels are advisory; slot and AFS indices are the identities. A compact inspector in **Add game source** lets the user search characters, inspect costumes and raw motion-bank members, and export an original member to a new file. The saved game path remains an unindexed source location. The complete Brawl/Melee viewer, Next retargeter, source-local roots and all weights were untouched.

Original member export copies from the ISO to a sibling temporary file, computes SHA-256, verifies the written bytes, and publishes only to a nonexistent destination. An existing output remains untouched. This is original opaque-pack export, not an edited model or animation writer. No per-fighter game data, caches or extracted archives enter the library or source tree.

## Observed local fixture and verification

The supplied private ISO has SHA-256 `93f1f911e9a2bbdf92e5794fc3075ddf3f87db3df4b1cb8cfbb0448721c91172` and length 3,020,816,384 bytes. Its AFS member counts are 1 / 3,399 / 65,201. The first fighter is exact slot 000, advisory label `Goku_0`, with eight available model/costume packs and motion banks of 440 and 6 raw members. Its first original pack (`PZS3US1.AFS/01423`) is 742,592 bytes, SHA-256 `744d05b524412a165f081d6df8a42b04bb80e491118428427f1b10f8126f1a33`. These observations belong to this exact USA disc; other builds are rejected until separately mapped.

Visual Studio 2022 MSBuild `tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /m:4 /v:minimal /nologo` passed. The private-desktop `ControlsAcceptance.exe <supplied ISO> <supplied ISO> <isolated output> --private-desktop --bt3-inventory` passed 10 checks: archive identity, every slot, model/motion-bank offsets, exact original export/hash, existing output protection, malformed-image rejection, inspector rows/search, and verified path-only source routing. No OS input injection or active-desktop capture was used. Fresh inspector and Add game source captures were inspected: the inventory lists exact slots/packs/members, and the source dialog clearly states its current limits. Physical input, BT3 rendering, game acceptance and modpack behavior remain unverified.

The local preview.77 package is `artifacts/Fighterport-bt3-inventory77/native`; it reuses the unchanged preview.76 engine/worker bundle and overlays the newly built native application. The local `Start-FighterPort.ps1 -Check` resolves that package. The package and private test artifacts are excluded from source publication.

## Next work in this same milestone

1. Decode native `pmdl` rig, materials, textures and all skin influences; compare native bind/skin evaluation before offering source and target preview. Reuse the game-adapter boundary and full Model Previewer.
2. Identify and decode the motion-bank compression and ANM action tables, preserving exact slot, costume, rig, action, timebase and source hashes. Validate native evaluation against the decomp and independent fixtures; then expose real animation names and cross-game source retargeting.
3. Add native BT3 animation writer, fresh reopen/evaluation, editable target/export routes, and separately qualified pack/AFS installation. Source-format and target-format output remain explicit. Test other builds and modpacks as separate source providers.

No stage above inherits acceptance from the inventory scanner or from third-party converter claims.
