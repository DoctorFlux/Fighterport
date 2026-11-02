# Preview.75 — native Melee model-source export (bounded)

Starting commit: `bce3171502d5778c07c423775c1f79e0a7b2bda5`.
Current milestone: the sole incomplete G1/M2. Public source destination:
`DoctorFlux/Fighterport/main`. All local game inputs and captures are ignored.

## Delivered

- The main viewer's **Animation → Export original native model + textures…**
  publishes a new folder with the exact project-owned Melee costume DAT. HSD
  model materials and image data remain embedded in that file. Fighter context
  and common DATs are copied alongside it; newly opened targets retain their
  exact source virtual filenames. Older saved targets remain readable and use
  explicit generic export names. A receipt records SHA-256 per original file.
- The workspace's **Export verified animation…** command now uses the native
  target adapter when a Melee target is active. The animation-list context
  exporter already offers Melee action `.dat` first, followed by an explicitly
  named CHR0 authoring copy. Brawl targets retain their existing CHR0 path.
- Model-source export checks the owned source hashes, exact model member and
  rig, current project/model dependency, existing destination and output path.
  It freshly decodes every published DAT and rechecks dependencies immediately
  before an atomic new-folder rename. No editable clip, playhead or native Undo
  state is changed.

The source DAT bundle is not a model edit, action-archive installation or game
test. Edited native model writing remains unqualified and is not offered.

## Background verification

Visual Studio 2022 Release/x86 built `tests/ControlsAcceptance/ControlsAcceptance.csproj`;
the bundled .NET 10 SDK built and packaged the engine/worker. The serialized
`scripts/Test-BackgroundCapture.ps1` private-desktop harness ran against the
local Melee USA 1.02 ISO and Brawl PAC fixtures. Fresh offscreen captures of
the Melee main/source viewer and native destination dialog were inspected.

| Case | Result |
|---|---|
| Mario Wait1 native action plus model bundle | 41 checks passed; `PlMrNr.dat` 473,522 bytes, `PlMr.dat` 194,992 bytes, `PlCo.dat` 149,101 bytes; exact model hash, rig and decoded embedded textures; unchanged clip/frame; existing folder and stale dependency rejected |
| Fox Wait1 focused model bundle | 32 checks passed; `PlFxNr.dat` 362,978 bytes, `PlFx.dat` 259,850 bytes, `PlCo.dat` 149,101 bytes; exact virtual filename, model hash/rig and decoded textures; native menu and DAT-first animation filter present |
| Existing Melee edit, native action export, Undo/Redo, Save/reopen | 70 checks passed |
| Existing Brawl native animation-list export | 46 checks passed |

The broader Fox native *animation destination* case was also attempted but
stopped at its pre-existing terminal-data gate: its editable action has data
beyond that destination's stored ending. No DAT was published. The focused
Fox model-bundle case above passed; the terminal writer was not weakened.
No active-desktop input, physical monitor DPI, game runtime, modpack install or
universal format compatibility was tested.

## Open native ownership work

The main Melee editor still stores an MDL0 selection facade and editable
CHR0s in BRRES. The renderer uses native HSD geometry, materials, skinning and
textures, but that is not native DAT document ownership. A future increment
must replace the animation-list/evaluation/edit/Undo contracts with native
adapter-owned model and motion resources, qualify fractional native motion
writing and terminal semantics, and support direct DAT action import and
edited native model/texture writing. The existing 3,258-fractional-key
Brawl-to-Melee Wait1 export remains rejected. Every later game format needs
its own reader, writer and validation evidence through these interfaces.
