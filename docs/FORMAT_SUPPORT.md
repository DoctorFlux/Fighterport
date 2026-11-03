# Fighterport format support (generated)

This view is generated from [FEATURE_STATUS.json](FEATURE_STATUS.json) by
`python scripts/Check-ProjectMemory.py --generate-support`. Edit the ledger, then regenerate.
It describes bounded evidence, not blanket game compatibility.

**Only Melee, Brawl, Ultimate and Rivals of Aether 2 are destination games.**
All four are also intended sources. Other listed games are source-only.

Verification marks: H = historical pass; C = current-session pass;
– = not run; F = failed; U = unavailable; · = not applicable.
Columns N/R/V/H/T/G mean numerical, native reopen, visual, human input,
external tool import and game runtime respectively.

## Four destination games

| Game / build | Direction | Asset / channel | Operation | Feature | Status | N/R/V/H/T/G | Evidence | Next step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | animation / CHR0 start/end transitions | editing | FP-FITTING | IN_PROGRESS | H/–/–/–/–/– | [record](FITTING_CORPUS_2026-11-01.md) | Qualify non-identical-rig fit results and reviewed reference contacts. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | animation / skeletal CHR0 | retargeting | FP-BRAWL-ADAPTER | IN_PROGRESS | H/H/–/–/–/– | [record](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md) | Resolve non-identical-rig quality case. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | animation / skeletal CHR0 v4/v5 | writing | FP-BRAWL-ADAPTER | VERIFIED_BOUNDED | H/H/–/–/–/– | [record](NATIVE_ANIMATION_EXPORT_ACCEPTANCE_2026-10-29.md) | Qualify further native channel/container routes. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | animation / CHR0 v4/v5 | writing | FP-OPTIMIZATION | VERIFIED_BOUNDED | H/H/–/–/–/– | [record](NATIVE_CHR0_EXPORT_OPTIMIZATION_ACCEPTANCE_2026-10-28.md) | Improve geometry-aware compression without changing quality limits. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | animation set / CHR0 clips | editing | FP-SETS | VERIFIED_BOUNDED | H/–/H/–/–/– | [record](VERIFIED_SET_EXPORT_ACCEPTANCE_2026-10-24.md) | Qualify build-specific slot taxonomy. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | animation set / CHR0 files | writing | FP-NATIVE-EXPORT | VERIFIED_BOUNDED | H/H/–/–/–/– | [record](VERIFIED_SET_EXPORT_ACCEPTANCE_2026-10-24.md) | Keep separate from native package installation. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | model / MDL0/material/texture | writing | FP-BRAWL-ADAPTER | IMPLEMENTED_UNVERIFIED | –/–/–/–/–/– | — | Test exact and converted model writes separately. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | model+animation / MDL0/CHR0 | editing | FP-BRAWL-ADAPTER | VERIFIED_BOUNDED | H/H/H/–/–/– | [record](NATIVE_CONTROLS_WORKSPACE_ACCEPTANCE_2026-10-23.md) | Preserve during adapter integration. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | model+animation / PAC/PCS/BRRES | packaging | FP-BRAWL-ADAPTER | IMPLEMENTED_UNVERIFIED | –/–/–/–/–/– | — | Measure native container insertion and runtime acceptance by build. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | destination | model+texture / MDL0/BRRES | writing | FP-MODEL-PORT | NOT_STARTED | –/–/–/–/–/– | — | Implement separate model-port writer and loss report. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | source | model+animation / MDL0/CHR0 and indexed native channels | inventory | FP-BRAWL-ADAPTER | VERIFIED_BOUNDED | H/–/–/–/–/– | [record](DIRECT_GAME_SOURCES_ACCEPTANCE_2026-10-26.md) | Extend unqualified disc/build/container cases. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | source | model+animation / native indexed game source | inventory | FP-LIBRARY | VERIFIED_BOUNDED | H/–/H/–/–/– | [record](LIBRARY_ORGANIZATION_ACCEPTANCE_2026-10-24.md) | Retain exact association during new adapter work. |
| Super Smash Bros. Brawl (Wii; RSBE01 revision 1 fixture) | source | model+animation / MDL0/CHR0 | preview | FP-BRAWL-ADAPTER | VERIFIED_BOUNDED | –/–/H/–/–/– | [record](DIRECT_GAME_SOURCES_ACCEPTANCE_2026-10-26.md) | Qualify all channel/build combinations. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | destination | animation / HSD motion through owned editable bridge | editing | FP-MELEE-ADAPTER | VERIFIED_BOUNDED | H/H/H/–/–/– | [record](MELEE_SHARED_BRIDGE_2026-11-02.md) | Advance native document ownership and unqualified channels. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | destination | animation / skeletal HSD action | retargeting | FP-MELEE-ADAPTER | IN_PROGRESS | H/H/–/–/–/– | [record](MELEE_SHARED_BRIDGE_2026-11-02.md) | Resolve current cross-rig quality and fractional native timing. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | destination | animation / HSD DAT action integer-wait FOBJ | writing | FP-MELEE-ADAPTER | VERIFIED_BOUNDED | H/H/–/–/–/– | [record](NATIVE_ANIMATION_DESTINATION_2026-11-02.md) | Implement and verify fractional-time FOBJ. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | destination | animation / HSD FOBJ | writing | FP-OPTIMIZATION | NOT_STARTED | –/–/–/–/–/– | — | Implement after fractional-time writer and same-master validation. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | destination | model / HSD DAT geometry/texture | writing | FP-MELEE-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Define and implement native HSD model write/reopen pipeline. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | destination | model+animation / HSD fighter/action archive | packaging | FP-MELEE-ADAPTER | IN_PROGRESS | –/–/–/–/–/– | — | Qualify complete target archive insertion and runtime. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | destination | model+texture / HSD DAT | writing | FP-MODEL-PORT | NOT_STARTED | –/–/–/–/–/– | — | Implement native HSD model writer and quality gates. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | source | model+animation / HSD DAT | inventory | FP-LIBRARY | VERIFIED_BOUNDED | H/–/–/–/–/– | [record](MELEE_SOURCE_ACCEPTANCE_2026-11-01.md) | Expand modpack/build association checks. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | source | model+animation / HSD DAT | inventory | FP-MELEE-ADAPTER | VERIFIED_BOUNDED | H/–/–/–/–/– | [record](MELEE_SOURCE_ACCEPTANCE_2026-11-01.md) | Qualify modpacks, builds and ambiguous symbols. |
| Super Smash Bros. Melee (GameCube; USA v1.02 fixture) | source | model+animation / HSD DAT | preview | FP-MELEE-ADAPTER | VERIFIED_BOUNDED | –/–/H/–/–/– | [record](MELEE_SOURCE_ACCEPTANCE_2026-11-01.md) | Broaden costume, action and renderer checks. |
| Rivals of Aether 2 (Windows/UE5.8; unqualified) | destination | animation / UE asset/FBX unqualified | editing | FP-RIVALS2-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Define native target and Modkit authoring contract. |
| Rivals of Aether 2 (Windows/UE5.8; unqualified) | destination | animation / UE asset/FBX unqualified | retargeting | FP-RIVALS2-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Qualify rig bridge and full motion quality. |
| Rivals of Aether 2 (Windows/UE5.8; unqualified) | destination | model+animation / UE5.8 Modkit | packaging | FP-RIVALS2-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Qualify build/cook/runtime separately. |
| Rivals of Aether 2 (Windows/UE5.8; unqualified) | destination | model+animation / UE5.8 Modkit/FBX | writing | FP-RIVALS2-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Implement after pinned Modkit import contract. |
| Rivals of Aether 2 (Windows/UE5.8; unqualified) | source | model+animation / UE native unqualified | inventory | FP-LIBRARY | NOT_STARTED | –/–/–/–/–/– | — | Implement after Modkit/source research. |
| Rivals of Aether 2 (Windows/UE5.8; unqualified) | source | model+animation / UE asset/FBX unqualified | inventory | FP-RIVALS2-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Pin game/Modkit build and inspect source format. |
| Rivals of Aether 2 (Windows/UE5.8; unqualified) | source | model+animation / UE asset/FBX unqualified | preview | FP-RIVALS2-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Implement after source parser and rig bridge. |
| Super Smash Bros. Ultimate (Nintendo Switch; unqualified) | destination | animation / native unqualified | editing | FP-ULTIMATE-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Define native target document and authoring bridge. |
| Super Smash Bros. Ultimate (Nintendo Switch; unqualified) | destination | animation / native unqualified | retargeting | FP-ULTIMATE-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Qualify rig bridge and full motion quality. |
| Super Smash Bros. Ultimate (Nintendo Switch; unqualified) | destination | model+animation / native unqualified | packaging | FP-ULTIMATE-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Qualify build-specific packaging and game runtime. |
| Super Smash Bros. Ultimate (Nintendo Switch; unqualified) | destination | model+animation / native unqualified | writing | FP-ULTIMATE-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Implement writer after native format and target contracts. |
| Super Smash Bros. Ultimate (Nintendo Switch; unqualified) | source | model+animation / native unqualified | inventory | FP-LIBRARY | NOT_STARTED | –/–/–/–/–/– | — | Implement after format research. |
| Super Smash Bros. Ultimate (Nintendo Switch; unqualified) | source | model+animation / native unqualified | inventory | FP-ULTIMATE-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Pin build and document native source layout. |
| Super Smash Bros. Ultimate (Nintendo Switch; unqualified) | source | model+animation / native unqualified | preview | FP-ULTIMATE-ADAPTER | NOT_STARTED | –/–/–/–/–/– | — | Implement after source parser and rig bridge. |

## Source-only games

| Game / build | Direction | Asset / channel | Operation | Feature | Status | N/R/V/H/T/G | Evidence | Next step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Battle Stadium D.O.N (PlayStation 2/GameCube; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| Dragon Ball Z: Budokai Tenkaichi 3 (PlayStation 2; SLUS-21678 USA fixture) | source | animation / native action to shared rig | retargeting | FP-BT3-SOURCE | IN_PROGRESS | H/H/–/–/–/– | [record](BT3_RIG_BRIDGE_2026-11-03.md) | Measure nonstatic BT3-to-target pose/skin/contact quality. |
| Dragon Ball Z: Budokai Tenkaichi 3 (PlayStation 2; SLUS-21678 USA fixture) | source | model+animation / PZS/PB3 native | inventory | FP-BT3-SOURCE | VERIFIED_BOUNDED | H/–/–/–/–/– | [record](BT3_DIRECT_LIBRARY_2026-11-02.md) | Broaden form/action/modification coverage. |
| Dragon Ball Z: Budokai Tenkaichi 3 (PlayStation 2; SLUS-21678 USA fixture) | source | model+animation / PZS/PB3 | inventory | FP-LIBRARY | VERIFIED_BOUNDED | H/–/–/–/–/– | [record](BT3_DIRECT_LIBRARY_2026-11-02.md) | Expand source association and labeling coverage. |
| Dragon Ball Z: Budokai Tenkaichi 3 (PlayStation 2; SLUS-21678 USA fixture) | source | model+animation / PZS/PB3 native | preview | FP-BT3-SOURCE | VERIFIED_BOUNDED | –/–/H/–/–/– | [record](BT3_DIRECT_LIBRARY_2026-11-02.md) | Qualify more actions and transformed/costume variants. |
| Dragon Ball Z: Budokai Tenkaichi 3 (PlayStation 2; SLUS-21678 USA fixture) | source | model+animation / glTF/GLB | writing | FP-BT3-SOURCE | VERIFIED_BOUNDED | H/–/H/–/H/– | [record](PORTABLE_RIGGED_INTERCHANGE_2026-11-03.md) | Broaden actions and DCC playback checks. |
| Dragon Ball Z: Budokai 3 (PlayStation 2; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| Dragon Ball FighterZ (Windows/PlayStation/Xbox; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| Jump Force (multiple; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| Naruto games (multiple; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| One Piece games (multiple; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| Dragon Ball: Sparking! ZERO (Windows/PlayStation/Xbox; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| Dragon Ball Xenoverse 1/2 (multiple; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |
| Dragon Ball Z: Zenkai Battle (Arcade; unqualified) | source | model+animation / native unqualified | inventory | FP-OTHER-DONORS | RESEARCH | –/–/–/–/–/– | — | Pin one build and inspect native model, texture, skeleton and motion formats. |

## Shared and portable capabilities

| Game / build | Direction | Asset / channel | Operation | Feature | Status | N/R/V/H/T/G | Evidence | Next step |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Portable interchange (interchange; format-specific) | source | animation / glTF 2.0 GLB | writing | FP-PORTABLE-EXPORT | VERIFIED_BOUNDED | H/–/–/–/H/– | [record](PORTABLE_ANIMATION_BUNDLES_2026-11-03.md) | Add qualified Brawl/Melee animation interchange and timing cases. |
| Portable interchange (interchange; format-specific) | source | model+animation / FBX | writing | FP-PORTABLE-EXPORT | NOT_STARTED | –/–/–/–/–/– | — | Implement model-only, animation-only and combined FBX with independent DCC checks. |
| Portable interchange (interchange; format-specific) | source | rigged model+animation / glTF 2.0 GLB | inventory | FP-PORTABLE-IMPORT | NOT_STARTED | –/–/–/–/–/– | — | Implement immutable import snapshot and rig/weight/timing validation. |
| Portable interchange (interchange; format-specific) | source | rigged model+texture / glTF 2.0 GLB | writing | FP-PORTABLE-EXPORT | VERIFIED_BOUNDED | H/–/H/–/H/– | [record](PORTABLE_RIGGED_INTERCHANGE_2026-11-03.md) | Broaden rigs, materials and DCC import coverage. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | animation / static floor | editing | FP-CONTACTS | VERIFIED_BOUNDED | H/H/H/–/–/– | [record](REVIEWED_GROUND_CONTACT_ACCEPTANCE_2026-10-30.md) | Expand held-out anatomy and action coverage. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | animation / finite static ledge | editing | FP-CONTACTS | NOT_STARTED | –/–/–/–/–/– | — | Implement after grounded contact and retarget quality. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | animation / moving supports/actors/props | editing | FP-CONTACTS | NOT_STARTED | –/–/–/–/–/– | — | Define explicit support frames and solve/acceptance corpus. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | animation / rig bridge | retargeting | FP-RETARGET-QUALITY | IN_PROGRESS | H/H/–/–/–/– | [record](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md) | Reproduce complete non-identical-rig failure. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | animation set / protected native slots | editing | FP-FILL-MISSING | NOT_STARTED | –/–/–/–/–/– | — | Implement once runtime slot profiles exist. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | animation set / build-specific slots | packaging | FP-SETS | NOT_STARTED | –/–/–/–/–/– | — | Build exact per-game/build slot profiles and protected assignment tests. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | model+animation / multiple native formats | packaging | FP-MULTI-TARGET | NOT_STARTED | –/–/–/–/–/– | — | Implement after per-target writers and quality gates. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | model+animation / native/portable | packaging | FP-VALIDATION | IN_PROGRESS | –/–/–/–/H/– | [record](PORTABLE_RIGGED_INTERCHANGE_2026-11-03.md) | Pin BrawlCrate, Blender, Maya, 3ds Max and game build checks per output. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | model+animation / native game assets | packaging | FP-VALIDATION | NOT_STARTED | –/–/–/–/–/– | — | Run per-game build/packaging/runtime verification when native writers qualify. |
| Shared rig/motion bridge (multiple; per accepted fixture) | destination | model+animation / morph/visibility/material/non-skeletal | writing | FP-ASSET-CHANNELS | IN_PROGRESS | –/–/–/–/–/– | — | Qualify each channel and destination separately. |
| Shared rig/motion bridge (multiple; per accepted fixture) | source | animation / Brawl CHR0 same rig | retargeting | FP-RETARGET-QUALITY | VERIFIED_BOUNDED | H/H/H/–/–/– | [record](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md) | Use as regression while fixing changed-rig case. |

This generated table does not infer target compatibility from a saved path,
successful build, reader result or parseable export.
