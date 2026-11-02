# BT3 native source preview — preview.80

Starting source commit: `984c59bfb544aedd18e4ea7e1a130a2b7a0bf56b`. This adds read-only model, costume texture and skeletal-action rendering to the existing small source viewport. The sole G1/M2 milestone remains incomplete.

## Native boundary

The saved SLUS-21678 ISO is read directly. The selected costume's original pack supplies pmdl member 2 and its texture file member 11; the selected form's own motion bank supplies the action. No extracted game asset is cached or placed into a project. The native pmdl hierarchy, rest data, source bytes and two-influence vertex weights are unchanged. The owned reader handles the fighter VIF V4-32 strip batches and optional seam triangles, and decodes the referenced PS2 GS PSMT4/PSMCT32 textures from their 32-bit upload packets. Unsupported or malformed native layouts fail closed rather than drawing a substitute model.

The existing ModelPanel draws the selected source read-only. Animation scrubbing and playback sample already-decoded native tracks; they do not read the ISO, rebuild the mesh or invoke Next on each tick. Consecutive batches sharing one texture are rendered together. Opening a different action on the same costume reuses its prepared geometry. Changing form or costume prepares the selected original model and texture data again. Stale asynchronous loads cannot replace a newer selection, and a failed action clears the old preview.

For display only, BT3's down-positive vertical and native back-facing axes map into the Brawl viewer's up-positive, front-facing presentation as `(-x,-y,-z)`. The native coordinates are not rewritten. The viewer uses a neutral ambient/key/fill light and the original costume colors. It does **not** reproduce BT3's full runtime toon ramp, dynamic face swaps, gameplay visibility or effects.

## Size and orientation

The read-only display keeps a 1:1 unit scale. Measured bind-view bounds in the private corpus: BT3 Goku (Mid) costume 1 has vertical span `16.7904`; Brawl Mario `15.6710`; Brawl Snake `21.2648` native units. That places this BT3 fixture near the existing Brawl scale without a character-specific multiplier. The front-facing Goku and Super Saiyan captures were inspected after the earlier upside-down/back-facing and clamped-negative-UV diagnostic captures. Those earlier captures are not acceptance evidence.

## Background verification

Visual Studio 2022 Release/x86 `tests/ControlsAcceptance/ControlsAcceptance.csproj` built. These existing private-desktop harness commands used the supplied private ISO, no active-desktop capture and no OS input injection:

- `ControlsAcceptance.exe <private ISO> unused artifacts/Bt3Mesh80accepted --private-desktop --bt3-inventory --bt3-mesh --bt3-mesh-sweep`: **21 checks**; all 698 indexed costume packs decoded textured geometry, including seam sections, with zero reader failures. Goku (Mid) decoded 564 strip batches, 3,435 triangles and 19 texture block/format combinations. A preceding sweep correctly rejected six seam attachments that had no ordinary triangle batch; the accepted build reads their native part pivots directly.
- `ControlsAcceptance.exe <private ISO> unused artifacts/Bt3Pose80qualified --private-desktop --bt3-inventory --bt3-sweep`: **20 checks**; 161 native idle actions associated with all 698 costumes. Of 1,127 sampled other action members, 1,107 decoded; 20 remained unsupported by the current bounded motion reader. No replacement motion was chosen.
- `ControlsAcceptance.exe <private ISO> unused artifacts/Bt3Render80accepted --private-desktop --bt3-direct-library --bt3-render`: **11 checks**; the selected Goku costume, distinct Super Saiyan form and a costume with native seam triangles rendered in the small viewport. Action scrubbing changed captured pixels; an unsupported action cleared stale preview content; the editable target remained untouched. The fresh front-facing captures and decoded texture sheet were visually inspected.
- `ControlsAcceptance.exe <private Melee ISO> unused artifacts/Bt3Render80melee-regression --private-desktop --workspace-melee --melee-target-only`: **34 checks** passed, including native Melee preview, same-model action selection, shared offscreen rendering and unchanged source bytes. This checks the shared viewport/library path after the BT3 addition.

The model rendering proves bounded source preview for this disc build, not game-exact lighting or universal action decoding. Physical input, human usability, actual BT3 runtime acceptance, BT3 Open as target, retargeting and native writing remain unverified/unavailable. Brawl and Melee target paths retain their existing boundaries.

The local launcher now selects `artifacts/Fighterport-bt3-native-preview80/native`. Its executable SHA-256 is `7069471A46A0BE7909DABD2BB50E10CAB08D413A4B250ABAA5FC9BF66C48D54B`, identical to the final tested build; the packaged BrawlLib, Melee adapter, HSDRaw and OpenTK dependencies also matched that build. `Start-FighterPort.ps1 -Check` resolved the new package. The package, captures, settings and private ISO remain excluded from source publication.

## Inspected reference

Format interpretation was checked against `z3xox/Tenkaichi3Decomp` revision [`1c4d9060d859f5bc53a0843ad5937c04f43aa6e5`](https://github.com/z3xox/Tenkaichi3Decomp/tree/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5), specifically [`ObjDrawPart`/seam definitions](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/include/sys/gfxm_d_c.h), [`TexFile` definitions](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/include/sys/gfxm_b_b.h) and [VU1 batch notes](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/docs/systems/vu1/prog0_prog1.md). The reference is AGPL-3.0; Fighterport uses an independently written bounded reader, not copied implementation code. Existing upstream notices and licenses remain in place.
