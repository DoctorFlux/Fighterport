# Preview.95 — format-aware animation access

Starting source commit: `722336f4b16f4944b7712150b3e77389d7acf286`
(preview.94). This is a bounded increment in the one incomplete G1/M2.
It retains the complete native BrawlCrate Model Previewer, original Next
retargeter, native document ownership and game source bytes.

## Implemented boundary

- Import and Export menus stay visible and usable at the top of the workspace
  while an exact BT3 native target is open. Project-only actions remain
  disabled there. The menus reuse the existing native import/export commands;
  the redundant open/load buttons move to the overflow menu.
- Right-clicking a BT3 native animation row carries its exact action ID to
  the existing selected-action export dialog. The dialog retains preview.94
  combined or separate GLB output and optionally includes a rigged model.
- A direct in-app Maya `.anim` 1.1 writer exports one selected BT3 action per
  file. It samples the frozen native quaternion action into XYZ Euler curves,
  refines the curves until checked local orientation witnesses stay within
  0.01 degrees, writes linear keys at the native 60-frame timebase, freshly
  parses the serialized curves, and compares orientation and translation at
  original keys, output keys and seven interior points per output span. It
  rejects unsupported timebases, invalid values, exceeded work bounds and
  stale action/costume dependencies. A sibling folder is committed only after
  every file passes. BT3 is not converted into a CHR0 authoring document.
- Existing Brawl CHR0 right-click export still includes `.chr0` and `.anim`.
  Melee's CHR0-backed editable target now exposes its existing `.anim` route
  beside verified native DAT and editable CHR0 master export. The Melee
  native action itself remains HSD and is not changed by this filter.

Autodesk's documented [`.anim` 1.1 curve syntax](https://help.autodesk.com/cloudhelp/2022/ENU/Maya-Animation/files/GUID-87541258-2463-497A-A3D7-3DEA4C852644.htm)
and [animImportExport plug-in](https://help.autodesk.com/cloudhelp/2023/ENU/Maya-Animation/files/GUID-91EE8FBF-3267-4A41-BD7B-A2EBA1478F27.htm)
were used for the direct writer and independent import check. The installed
Maya executable was used only to verify output, never to create it.

## Background verification

Release/x86 builds use Visual Studio 2022 MSBuild. Private-desktop harness
runs use the shared offscreen viewport and do not inject input on the active
desktop. Local BT3 and Melee disc assets and generated files stay outside Git.

| Fixture | Observed result |
|---|---|
| BT3 GT Goku action 000, costume 1 | 32 private-desktop checks. Separate `.anim` output, unchanged GLB grouping/model choices, exact row ID, staged publication and native source preservation. Fresh curve verification: 1,584 fitted samples across the tracks, 24,489 checked local witnesses, maximum orientation difference 0.00988471°. |
| BT3 Goku Mid action 010, costume 1 | 32 private-desktop checks on another form, action length and track count. Fresh curve verification: 155 fitted samples across the tracks, 1,670 checked local witnesses, maximum orientation difference 0.0093852°. |
| BT3 native viewer | 47 private-desktop checks. Top Import and Export remain enabled; project Save is disabled; original lists, transport, editing, Undo/Redo and native motion save/reopen still work. |
| Melee USA 1.02 target | 35 private-desktop checks. Existing HSD source preview and exact native association remain; editable target export filter includes `.anim`. |
| Maya 2022 `animImportExport` | Two emitted `.anim` files imported in isolated scenes: 57 joints/174 curves and 54 joints/165 curves. This verifies Maya parsing and curve attachment, not complete skinned-model playback. |

The `.anim` file is a separate animation for a matching named skeleton. Native
gameplay events and loop policy have no portable representation in this file.
The local curve checks cover sampled local motion; they do not prove
continuous-time or world-space contact equivalence. Maya import on synthetic
joint scenes does not prove Blender, 3ds Max, full Maya skinned playback or
game-runtime compatibility.

Actual commands include:

```text
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /m:1 /v:minimal /nologo
ControlsAcceptance.exe <local BT3 ISO> none <new output> --portable-model-export --portable-bt3-slot 7 --portable-bt3-action 0 --private-desktop
ControlsAcceptance.exe <local BT3 ISO> none <new output> --portable-model-export --portable-bt3-slot 1 --portable-bt3-action 10 --private-desktop
ControlsAcceptance.exe <local BT3 ISO> none <new output> --private-desktop --bt3-direct-library --bt3-render
ControlsAcceptance.exe <local Melee ISO> none <new output> --private-desktop --workspace-melee --melee-target-only
mayapy.exe <local verification script> <generated .anim>
```

The checked local Maya 2022 imports reported 57 joints/174 attached curves
for the GT action and 54 joints/165 curves for the Mid action. The 1.1 writer
reopens its files with a separate curve parser before publication. The
import checks used Maya only as an external verifier; Fighterport performs
the writing itself. The first Brawl native export rerun used an unrelated
procedural clip where the legacy test expects `Wait1`, so it stopped during
fixture setup and supplies no Brawl result for this increment. The Brawl
right-click writer was not modified.

The native-viewer capture `bt3-target-toolbar.png` was inspected: Import and
Export are visible and active beside the exact BT3 action list and original
bone/mesh/texture panels. The current local launcher resolves the separate
preview.95 native build. Its `FighterPortStudioV2.exe` reports
`2.0.0-preview.95-portable-animation-access`, SHA-256
`3E0F083D73909AAAE9CADD52F5AD51F1B3C6DD0A002F17DB3593AEB318871BED`.
Generated binaries, game files, captures and private launch settings are not
source artifacts.

## Open work

Direct FBX writing inside Fighterport, Brawl/Melee GLB animation bundles,
portable model/animation import, broad BT3 action and costume qualification,
future game adapters, external DCC skin playback and actual game acceptance
remain open. The per-game native model writers also remain separate. No
unverified format is added to the right-click picker.
