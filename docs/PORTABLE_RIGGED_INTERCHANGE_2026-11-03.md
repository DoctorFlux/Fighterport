# Rigged portable interchange — bounded preview.93

Starting source commit: `57fb4597b887f81eea66cf9dae26af6ab2b37c9e`
(preview.92). This is one increment of the incomplete G1/M2 milestone. The
source checkout remains the full native BrawlCrate/Next application.

## Implemented boundary

- Existing saved Brawl and Melee target model exports keep `Model.dae` and PNGs
  and now add rigged `Model.gltf` + `Model.bin` and `Model.glb`. A common
  converter reads the staged COLLADA skeleton, inverse binds, geometry,
  materials and all declared vertex influences. Additional influence sets use
  glTF `JOINTS_n`/`WEIGHTS_n`; no four-weight truncation is built into this
  converter. The original native model and animation resources are unchanged.
- The native BT3 viewer's **Export model + textures...** attempts a rigged
  model-only export for the selected costume. It includes every native joint
  (helpers and fingers), each two-pivot skin influence and decoded textures.
  No animation clip is embedded in the model bundle.
- A separate internal BT3 action encoder was tested against native renderer
  samples to prepare a later animation-export workflow. It preserves
  full-range local translation/rotation tracks in glTF/GLB at a declared
  60-frame timebase. It is not exposed by the model command. Gameplay events
  and native loop policy are outside that portable clip.
- A bind preflight requires the native two-pivot bind points for blended
  vertices to agree within `0.0001` viewport units. Native tracks whose
  out-of-range fallback cannot be represented by ordinary glTF linear channels
  are not mislabeled as equivalent animation. Model export is unaffected by
  that action-timing limit. A model whose native pivots cannot be represented
  retains the prior static posed export and writes `Interchange-limitations.txt`.
- Exports retain the preview.92 sibling staging, cancellation, dependency
  checks, destination collision guard and SHA-256 receipt. The new glTF/GLB
  writer is used only for the portable bundle, not by native Brawl, Melee or
  BT3 writers.

## Background evidence

Visual Studio 2022 MSBuild Release/x86 rebuilt the application and
`ControlsAcceptance` harness. All acceptance runs used a private desktop,
with no OS input injection or active-desktop capture. Local game assets and
generated bundles stayed outside the source tree.

| Local case | Result |
|---|---|
| Brawl Mario saved model | 13 private-desktop checks; 14 rigged controllers, 7,048 triangles, nine decoded PNGs. One referenced `metal00` image remained explicitly unresolved. Khronos glTF Validator `2.0.0-dev.3.10`: zero errors and warnings. |
| Melee Mario saved model | 13 private-desktop checks; 43 rigged controllers, 5,908 triangles, 42 PNGs. Validator: zero errors/warnings; informational notes for an unused UV and four non-power-of-two images. |
| BT3 Goku Mid, costume 1, action 000 | 13 checks in the initial internal action run; 3,435 triangles, 19 PNGs, 55 full-range native tracks over 72 frames. Validator: zero errors/warnings. Independently evaluated GLB skin positions against native renderer output at frames 0, 0.25, 36.5 and 72: maximum vertex difference `0.0000019641` viewport units. |
| BT3 Goku GT, costume 1, action 000 | 16 checks including unsupported clipped-track rejection and model-only export; 3,375 triangles, 23 PNGs, 57 full-range tracks over 203 frames. Validator: zero errors/warnings. Internal action GLB/native vertex comparison at 0, 0.25, 102 and 203: maximum `0.0000019262` viewport units. Model-only GLB has no `animations` array. |
| BT3 Goku Mid, costume 1, action 010 | 13 checks; 54 full-range tracks over 21 frames. Validator: zero errors/warnings. Independent GLB/native comparison at 0, 0.25, 11 and 21: maximum `0.0000020374` viewport units. |

The independent skin comparison decodes the serialized GLB, evaluates its
TRS animation and skinning separately in Python, and compares it to native
renderer vertex probes. Validator results check format structure; sampled
vertex agreement is a separate numeric observation. Bind matrix checks on the
recorded Brawl and Melee fixtures had maximum `world × inverseBind` residuals
of approximately `0.0000053` and `0.0000079`. These samples do not prove every
BT3 action, costume, fractional instant or target DCC renderer.

Representative commands actually run (with local input/output paths omitted):

```text
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /m:1 /v:minimal /nologo
ControlsAcceptance.exe <BT3 ISO> none <new output> --portable-model-export --portable-bt3-slot 7 --portable-bt3-action 0 --private-desktop
ControlsAcceptance.exe <BT3 ISO> none <new output> --portable-model-export --portable-bt3-slot 1 --portable-bt3-action 10 --private-desktop
node check.js <serialized Model.glb>
python compare_bt3_skin.py <BT3 output folder>
```

The latest GT Goku run passed 16 checks. Brawl and Melee model tests and the
same staged COLLADA-to-GLB converter passed earlier in this increment; the
final version build reports `2.0.0-preview.93-rigged-portable-export` with
SHA-256 `52b92bf02a0df6b04b8a7f04b3271191b020bab29ee409ae69e75f9d9d07c5ac`.
`Start-FighterPort.ps1 -Check` resolves the local preview.93 runtime.
The owned .NET engine was unchanged here; its prior verified runtime remains
bundled because this workstation lacks the pinned .NET SDK 10.0.402.

## Evidence and limits

The portable file contract follows the Khronos glTF 2.0 specification at
`https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html`; independent
validation used Khronos `glTF-Validator` version `2.0.0-dev.3.10` from
`https://github.com/KhronosGroup/glTF-Validator`. The preceding Brawl/Melee
COLLADA and static BT3 paths are in preview.92. No BrawlLib native codec or
Next solver was changed here.

Blender 2.79 headlessly imported the new BT3 rigged COLLADA with 545 mesh
objects, one armature, 23 images and 38,150 mesh vertex groups. It also printed
`DAG zero... not allowed to happen!` while quitting; this import proves scene
loading, not full animation or visual fidelity.

Open: a separate animation export command with exact clip selection, one file
per animation or one combined file, optional model inclusion and qualified
FBX/glTF/GLB/COLLADA writers; portable **import**; Brawl/Melee animation
interchange; BT3 actions with out-of-range timing; broad costume/action
qualification; Blender glTF import; Maya/3ds Max verification; native game-model
writing; Ultimate/Rivals II adapters; and actual game acceptance. Model bundles
are intentionally free of animation clips.
