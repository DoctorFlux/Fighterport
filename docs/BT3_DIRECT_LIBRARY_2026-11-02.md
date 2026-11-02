# BT3 direct library browse — preview.79

Starting commit: `a241bfaf9d2f941fcdc066b1dc56240a29aa7b5e` (preview.78). This is a bounded fix for the saved BT3 ISO showing only “Saved location — not indexed” in the shared library. The sole G1/M2 milestone remains incomplete.

## Change

The existing verified USA SLUS-21678 disc reader now supplies a session-only library tree: disc → exact form slot → available costume pack → native action member. Form and costume are separate choices, so Goku (Mid) base slot 001 and Super Saiyan slot 002 retain separate banks. Action 000 is labeled Idle. Search and game/character filtering use form labels; the tree is populated lazily. The saved path is reused when “Browse saved disc” is chosen, and a newly saved BT3 path opens the same branch. The ISO is read directly; no game files are extracted or catalogue clips fabricated.

**Inspect BT3** opens the existing read-only native pose inspector at the selected form, costume and action. Source preview, Use on This Model and Open as target remain unavailable because native BT3 mesh/texture rendering and editable target ownership have not qualified. Other saved source behavior and the Brawl/Melee native routes are unchanged.

## Verification

Visual Studio 2022 MSBuild of `tests/ControlsAcceptance/ControlsAcceptance.csproj` in Release/x86 passed. The private-desktop commands below passed without OS input injection or active-desktop capture:

- `ControlsAcceptance.exe <private ISO> unused artifacts/Bt3DirectLibrary79qualified --private-desktop --bt3-direct-library`: **6 checks**. Saved-path identity, all 161 form rows, eight Goku Mid costume rows, action selection, disabled unqualified actions and no fake catalogue clips passed. The fresh viewer capture `artifacts/Bt3DirectLibrary79qualified/bt3-direct-library.png` was visually inspected.
- `ControlsAcceptance.exe <private ISO> unused artifacts/Bt3Inventory79qualified --private-desktop --bt3-inventory`: **19 checks**. Exact disc structure, native skeleton/pose sample, form and costume choices, original-byte export and source registration passed. This was the same bounded inspector regression as preview.78 without the longer 698-costume sweep, which remains prior evidence only.

The local `artifacts/Fighterport-bt3-direct-library79/native` package copies the unchanged preview.78 native dependencies and overlays the newly built application. Its executable SHA-256 is `C05C2BCC17F822B27755D88AAA22952CCD2DD50C6ED0D055B27B0C14B15210C9`, identical to the final build. `Start-FighterPort.ps1 -Check` resolves that package. Generated packages, captures and the private ISO are excluded from source publication.

The first combined harness attempt stalled because its second unshown WinForms viewer had no message-loop continuation; it did not establish a product failure. The final direct-library check runs in its own fresh private-desktop viewer process. Physical input, game runtime acceptance, BT3 model rendering and edited native output were not tested.

## Open work

Decode and render authentic pmdl mesh, textures, materials and all skin influences for the selected costume. Then qualify native source preview and cross-game retargeting against finished target motion, followed by separately qualified BT3 target editing/export and other builds/modpacks. Do not infer any of those capabilities from read-only library browsing.
