# Preview.76 — native Melee action presentation (bounded)

Starting commit: `20b4b1b9d29570ca434ac9364431dce0b0da1fa3`.
Current milestone: the sole incomplete G1/M2. Public source destination:
`DoctorFlux/Fighterport/main`.

## Implemented

- Read-only Melee library preview now owns its HSD motion and exact native
  name/frame metadata without baking a CHR0 authoring clip. Retarget/fitting
  preparation retains its separate editable bridge.
- The main target animation list asks the native adapter for its format and
  action labels. Melee shows **Melee** and **Melee project animations**. Each
  linked row has a tooltip containing its exact native action key; unlinked
  target revisions say they need a reviewed DAT destination. The selector is
  restricted to the current motion family. Brawl keeps its CHR0 list and
  existing controls.
- Right-click native DAT export, native HSD rendering, body editing, Undo and
  project Save/reopen continue through the existing verified paths. No original
  DAT, MDL0 bind, bone hierarchy, weight or source-owned root was modified.

## Background verification

- Visual Studio 2022 `MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj
  /p:Configuration=Release /p:Platform=x86 /m:4 /v:minimal /nologo` passed.
- `scripts/Test-BackgroundCapture.ps1 -WorkspaceMelee -MeleeEditableTarget
  -MeleeTargetOnly` on the supplied Melee USA 1.02 ISO passed 73 checks,
  including exact action identity, native body editing, DAT export, Undo/Redo,
  Save and fresh reopen. The fresh `melee-editable-main.png` capture was
  inspected and visibly has **Melee** in the selector and **Melee project
  animations** in the list.
- `scripts/Test-BackgroundCapture.ps1 -WorkspaceLibraryTarget` on local
  Brawl PACs passed 39 checks for library target selection, body Undo/Redo,
  Save/reopen and verified CHR0 export.
- The source-preview roster (Fox, Samus, Peach, Donkey) passed 57 checks,
  and Mario source/target checks passed 43, before the target-list change.
  These were private-desktop/offscreen runs with no active-desktop input.
- The local `preview.76` native viewer built at file version `2.0.0.50`, and
  `Start-FighterPort.ps1 -Check` resolves to that build. The .NET 10.0.402 SDK
  was unavailable for rebuilding the unchanged engine; the launcher bundle
  reused the previously verified preview.75 engine files. No engine source
  changed in this increment.

## Remaining native ownership work

This is an action-list and source-preview increment, not a native target
storage migration. The main Melee editor still uses an MDL0 selection facade
and CHR0 curves inside `editable-animations.brres`; the format selector is a
native action *presentation*. Project Save retains that exact editable master.
The native DAT writer validates separate action exports but currently rejects
some fractional retarget curves and terminal semantics. Direct DAT import,
DAT-owned editable curves, native edited model writing and future game adapters
need their own implementation and regression gates. No in-game acceptance or
physical-input usability was exercised.
