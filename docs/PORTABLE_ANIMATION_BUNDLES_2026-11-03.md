# Preview.94 — separate BT3 portable animation bundles

Starting source commit: `5b0b3c04dadb622b3f32a9d6bb903a42203a2584`
(preview.93). This is one bounded increment of the incomplete G1/M2 milestone.
It does not change the original Next retargeter, native BrawlCrate Model
Previewer, Brawl/Melee codecs or model-only export contract.

## Implemented

- The native BT3 target viewer has a separate **Export animations...** command.
  It lists exact action members for the selected form and costume and initially
  selects the current action. It offers one GLB per action or one GLB containing
  all selected actions, plus a single **Include model and textures** switch.
- The selected current action and other retained native edits export from
  independent serialized snapshots; other actions are read from exact owned
  disc members. Native source bytes are unchanged. The selected target costume,
  action identities and edit revisions are checked again before publication.
- The shared GLB writer can emit a skeleton and animations without meshes or
  skins, or a complete rigged model with textures and animations. Model-only
  exports remain free of clips. A native action whose out-of-range behavior
  cannot be represented by the current glTF linear channels is rejected.
- Candidate files are encoded and reopened by the local GLB verifier in a
  sibling staging folder. A new folder is published only after all files
  pass. Receipts identify clips by a stable identity hash and include output
  SHA-256 values, without private source paths. Cancellation, stale state and
  existing destinations publish no replacement.

## Background evidence

Visual Studio 2022 MSBuild Release/x86 built the native application and
`ControlsAcceptance` harness with only the two pre-existing CS1690 warnings.
All acceptance runs used private desktops; no active-desktop capture or OS
input injection was used.

| Local fixture | Result |
|---|---|
| BT3 GT Goku, costume 1, action 000 plus distinct action 010 | 25 private-desktop checks. Two exact native actions published together without model geometry; per-clip files with full rigged model and textures also published. An edited native action exported independently while the disc member stayed byte-identical. |
| BT3 Mid Goku, costume 1, action 010 plus distinct action 000 | 25 private-desktop checks with the current selected action reversed, including the edited-action case. |
| Cancellation, existing destination, stale dependency | All rejected without a new or replaced output folder. |
| Independent Khronos glTF Validator `2.0.0-dev.3.10` | Both combined action-only GLBs: zero errors and zero warnings. GT Goku per-action rigged GLB: zero errors and zero warnings. The animation-only files have informational empty-helper-node notices. |

The local GT combined GLB was 68,248 bytes and the rigged action-000 GLB was
3,864,620 bytes; these are portable interchange sizes and are not native BPE
or CHR0 size claims. The model-only preview.93 pose/skin comparison remains a
separate earlier result; this increment did not establish broad DCC playback,
game playback or every BT3 action's out-of-range timing.

The local launcher resolves the preview.94 native runtime. Its
`FighterPortStudioV2.exe` reports
`2.0.0-preview.94-bt3-animation-bundles` and SHA-256
`5eb5fbc2f7bfffbcee0207b490ba04893fbc0005750027e964108b266e8906b4`.
Generated runtime files, game assets and GLB samples are excluded from source
publication.

Commands actually run:

```text
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /m:1 /v:minimal /nologo
ControlsAcceptance.exe <local BT3 ISO> none <new output> --portable-model-export --portable-bt3-slot 7 --portable-bt3-action 0 --private-desktop
ControlsAcceptance.exe <local BT3 ISO> none <new output> --portable-model-export --portable-bt3-slot 1 --portable-bt3-action 10 --private-desktop
node check.js <serialized GLB>
```

## Remaining gates

FBX must be written by Fighterport itself, with no installed Blender or other
converter dependency. A direct FBX writer needs model-only, animation-only and
combined skin/texture/animation round trips before it is offered. Portable
COLLADA animation, Brawl/Melee animation interchange, portable import, DCC
playback in Blender/Maya/3ds Max, broad BT3 action qualification and runtime
acceptance remain unverified. The existing model-only export is still the
appropriate path when no animation is wanted.

The glTF 2.0 file contract follows the
[Khronos specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html).
Independent structure validation used
[Khronos glTF-Validator](https://github.com/KhronosGroup/glTF-Validator).
For a possible direct in-app FBX implementation, the MIT-licensed
[SmallFBX](https://github.com/i-saint/SmallFBX) and
[Comfy binary FBX exporter](https://github.com/Comfy-Org/fbx-exporter-three)
are research candidates only; neither has been incorporated or qualified.
