# BT3 native pose inspection — preview.78

Starting commit: `d054f52705c237bcd89e311b5a3b90bbeb7c49ad` (preview.77). This is one bounded step in the incomplete G1/M2 milestone. It does not qualify BT3 mesh/texture rendering, Open as target, retargeting, edited native writing or game acceptance.

## Pinned evidence and implementation

The user-supplied `z3xox/Tenkaichi3Decomp` was inspected at `1c4d9060d859f5bc53a0843ad5937c04f43aa6e5`. Its `src/battle/battle_load.c` associates exact character slot, costume pack and motion table; `include/battle/bobj_a.h`, `src/battle/bobj_a.c`, `src/sys/bpe.c` and `src/sys/math3d.c` describe the bounded packed pose data, interpolation and quaternion layout. The English slot names in `port/src/gs/fight_panels_ui.cpp` were transcribed as factual presentation labels, retaining exact slot and AFS IDs as identities. No game assets or decomp implementation code were copied. The decomp is AGPL-3.0 and remains an external reference.

The read-only `Bt3NativeAssetReader` decodes BPE motion data, pmdl skeleton records, packed local quaternions, translation keys and a native-style local pose sample. It rejects invalid bounds, nonfinite coordinates, unsupported track flags, out-of-range key times and work-budget breaches. It does not replace authentic pmdl meshes, hierarchy, binds, textures or weights with a Brawl facade. Reads remain directly from the verified ISO; no per-fighter extraction or catalogue growth occurs.

The inspector now exposes **form/character slot**, **costume** and **native action** as separate choices. Goku (Mid) base is slot 001 and its Super Saiyan form is slot 002; each selects its own motion bank before choosing a costume within that form. Action 0 is the loader's idle. A background operation checks a selected model skeleton against its selected action and reports native frame, track and key counts. A changed selection clears the prior verification. Saved BT3 paths remain unindexed until real mesh/texture preview and target editing qualify; the application does not offer a misleading Open as target or retarget action.

## Local evidence

The private SLUS-21678 ISO was read without mutation. Goku (Mid) default pmdl SHA-256 `bd03e3fb969e55e628dc0a0ed11e5f79dbfdc9b596f9453510e31a00c330b093` has 69 decoded bones. Its action-0 packed motion SHA-256 `a1340beffd9ddfc85f3062a9f147f0a7586243d9d74b22131856feac73248428` has 108 native frames, 54 animated tracks and 688 keys. Goku (Mid) Super Saiyan with costume 1 was independently inspected from its own exact slot and passed track-to-skeleton association.

Visual Studio 2022 MSBuild of `tests/ControlsAcceptance/ControlsAcceptance.csproj` in Release/x86 passed. `ControlsAcceptance.exe <private ISO> <private ISO> artifacts/Bt3NativeSweep5 --private-desktop --bt3-inventory --bt3-sweep` passed **20 checks** with no active-desktop input or capture. The sweep decoded action 0 against **all 698 available costume models across 161 slots**, and 1,107 sampled additional actions decoded. Twenty sampled action/member pairs were rejected by the current strict motion reader; they were reported and not silently substituted. This is sampled action evidence, not all-animation qualification. Local rest fallback and a fractional authored-key sample were checked. Fresh form/costume/action and inventory captures were inspected.

The local preview.78 package is `artifacts/Fighterport-bt3-native-pose78/native`, copied from the complete preview.77 package with the freshly built executable overlaid. The packaged executable SHA-256 is `410BBC55E6BDD456F8BF2AFA06C980D8DE1B322B607CE33FD2748D2F76518041`; it matches the final build. `Start-FighterPort.ps1 -Check` resolves that package. Local packages, captures and the user's ISO are excluded from publication.

## Open work

The pmdl mesh uses PS2 VIF/VU1/GIF/GS data and native textures need PS2 texture/CLUT decoding. A full renderer must retain all influences, non-LOD geometry, materials and costume differences before native source or target preview is offered. Some action table members are absent or non-pose data, and some sampled keys need further native timing interpretation. The native writer, fresh reopen, editing, export, retargeting and additional builds/modpacks each require separate gates. Physical input and in-game results have not been tested.
