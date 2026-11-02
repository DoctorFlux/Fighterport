# BT3 character browsing and native target view — preview.81

Starting source commit: `93af9be06e83e0462764c19132b337c725fff744` (preview.80). This is a bounded increment in the one incomplete G1/M2 milestone.

## Implemented boundary

The direct BT3 library now groups exact form slots under character rows and shows that character's indexed primary-bank actions once. The Transform dropdown chooses the exact native form. The Costume dropdown lists the form's indexed normal costumes and corresponding battle-damaged versions as separate original model resources; the first available normal costume is selected by default. The costume index uses the verified `form * 10 + costume` fighter-pack layout: native slots 0–3 are normal and 4–7 are the matching battle-damaged versions. Character-family labels are browse metadata only; exact slot, model-pack and action-member identities remain authoritative. Missing actions for a selected form are reported rather than substituted.

Preview Original keeps the small read-only native viewport. **Open as target** opens a larger read-only native ModelPanel with Transform, Costume and Animation selectors, Play/Pause and a frame scrubber. It reads the selected BT3 model, embedded textures and motion directly from the saved ISO, and evaluates already-decoded motion during playback. A changed action on the same costume reuses prepared geometry. Loading, cancellation, closing and returning to the project preserve the original editable project; no BRRES/CHR0 representation or fake document ownership is created for BT3. The complete BrawlCrate editor and original Next remain present for their qualified formats.

This target **view** is not native BT3 target authoring. Bone editing, Use on This Model, fitting, retargeting to or from BT3, project-owned BT3 animation Save and native BT3 export still need a format-neutral editable target contract and independently qualified BT3 writers. The primary skeletal motion bank is listed; the second bank has not been qualified as an animation list. Twenty sampled primary-bank actions remain unsupported by the bounded decoder. Unsupported action selection clears the old source preview and explains the failure. Lighting is a neutral inspection presentation, not game-exact toon/material behavior. No new game build or runtime acceptance is claimed.

## Background acceptance

Release/x86 Visual Studio 2022 build of `tests/ControlsAcceptance/ControlsAcceptance.csproj` passed with two existing CS1690 warnings outside this increment. All captures used private desktops and the shared offscreen renderer; there was no OS input injection or active-desktop capture.

- `ControlsAcceptance.exe <private BT3 ISO> unused artifacts/Bt3Target81release --private-desktop --bt3-direct-library --bt3-render`: **18 checks passed** on the final versioned executable. This covered character/action grouping, two Goku (Mid) forms, eight costume choices, normal default, 175% text in a compact 1050×700 workspace, source render and scrubbing, unsupported action clearing, a native seam costume, large target view, playback, same-costume geometry reuse, alternate form, battle-damaged costume and return to the original project.
- `ControlsAcceptance.exe <private Melee ISO> unused artifacts/Bt3Target81melee-regression --private-desktop --workspace-melee --melee-target-only`: **34 checks passed**, including native preview, same-model action, target route and unchanged source bytes.
- `ControlsAcceptance.exe <local Brawl Mario MDL0> <local FitMarioMotionEtc PAC> artifacts/Bt3Target81brawl-regression --private-desktop --workspace-library-target`: **39 checks passed**, including exact target/clip/Undo and Save/export/reopen.
- The optional primary/secondary-bank probe used the same private BT3 ISO. Six sampled secondary-bank members failed the current bounded skeletal decoder; no unsupported member was presented as a verified skeletal action.

Fresh `bt3-large-target.png` and `bt3-character-compact.png` captures were visually inspected. The full model faces the viewer, its normal costume has textures, and both selector labels remain readable at compact 175% text. The test confirms background behavior only; physical-input, mixed-monitor DPI, human usability, all BT3 actions, model editing and game acceptance remain open.

The local launcher selects the ignored `artifacts/Fighterport-bt3-target-preview81/native` package. Its versioned executable (`2.0.0-preview.81-bt3-character-target-view`) has SHA-256 `B94CA5FC01E8D23400B80D4A7DAA1EBFCE8CEF83854DAA7BD84E4454D6A907E9`, identical to the final tested build; packaged BrawlLib, Melee adapter, HSDRaw and OpenTK hashes also match that build. `Start-FighterPort.ps1 -Check` resolved this package. Generated packages, captures, local settings and the private ISO remain excluded from source publication.

## Reference

The fighter-pack normal/damaged index relationship was checked against [`z3xox/Tenkaichi3Decomp` revision `1c4d9060d859f5bc53a0843ad5937c04f43aa6e5`](https://github.com/z3xox/Tenkaichi3Decomp/tree/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5). The implementation reads the user's disc with the owned bounded parser; no decomp code or game assets are published.
