# Portable model interchange — bounded preview.92

Starting source commit: `9cde7f819017496af138cb29d7351425d417fb3e`.
This is one bounded export increment inside the sole incomplete G1/M2. It
does not qualify game-native model conversion, animation interchange, or
additional game adapters.

## Delivered

- The existing Model Previewer **Animation → Export model + textures for
  Blender…** command publishes a new, independent folder for a project-owned
  Brawl or Melee target. Brawl uses a fresh load of the saved native model
  member and the existing BrawlLib COLLADA controller exporter. Melee uses its
  verified normal-detail HSD projection. Both write rigged `Model.dae` and
  decoded PNG images. The editable document, animations, source files and
  native Undo state remain unchanged.
- The native BT3 target's edit pane offers **Export pose + textures…**. It
  freezes the already evaluated viewport pose and publishes static `Model.dae`,
  `Model.gltf` + `Model.bin`, `Model.glb` and decoded PNGs. The GLB images are
  embedded in its BIN chunk. No disc read or retarget bake occurs during
  export. BT3 joints, weights and action curves are deliberately absent from
  these *static* outputs; the source pmdl/BPE remains the owner.
- A new folder is prepared as a sibling temporary directory and moved into
place only after XML parsing, GLB chunk/buffer/image checks, nonempty-file
checks, cancellation and current
  dependency checks. Existing destinations are rejected. Every bundle has an
  `Export-info.txt` with scope and SHA-256 hashes. Brawl image references that
  cannot be decoded from the owned resource are listed in
  `Missing-textures.txt`; the exporter does not substitute another image.
- Melee's scratch-time absolute COLLADA texture URIs are rewritten to relative
  bundle filenames before publication. The Brawl image path resolves embedded
  TEX0s headlessly instead of requiring a GL context.

## Background evidence

Visual Studio 2022 MSBuild Release/x86 built the app and private-desktop
`ControlsAcceptance` harness. `--portable-model-export` passed **ten synthetic
checks** (including format structure, RGBA order, cancellation, existing
destinations, damaged GLB rejection and unchanged input); real local cases each added one exact
source check. The test writes ignored artifacts only.

| Local output | Recorded result |
|---|---|
| Brawl Mario `FitMario00.pac` | Rigged DAE: 14 geometry/controller entries. Nine referenced PNGs decoded; `metal00` remained explicitly unresolved. Blender 2.79 imported 15 mesh objects, one armature and nine images, with COLLADA minfilter schema warnings. |
| Melee saved Mario target | Rigged normal-detail DAE: 42 PNG images with relative links. Blender 2.79 imported 44 mesh objects, one armature and 42 images with no missing-image report. |
| BT3 USA Goku Mid costume 1 | Static DAE: all 3,435 decoded triangles and 19 PNG textures; Blender 2.79 imported 105 mesh objects and 19 images. The GLB header/chunks and separated glTF buffer equivalence passed the harness's independent byte parsing. |

The BT3 GLB and separated glTF have **not** been imported by an independent
glTF reader. Maya 2022 is installed locally but no COLLADA/glTF importer was
verified; 3ds Max was not available. No FBX writer, Maya/3ds Max acceptance,
engine importer, model animation interchange, game runtime test or source
model round-trip was performed. The Brawl COLLADA warnings and unresolved
`metal00` remain recorded issues. These outputs are not game-native files.

The final BT3 private-desktop run passed 11 checks in
`artifacts/Fighterport-portable-preview92-final-verified`; the Brawl and
Melee fixture runs each passed 10 checks. The existing BT3 inventory/mesh
regression passed 27 checks. The launcher now resolves the preview.92 native
executable with SHA-256
`b94c4d2cfd355cd6da986c0999b1dcb45907ac44a68f11ddf7651621cddef0d5`.
The owned .NET engine was not changed in this increment. Its existing
preview.91 runtime is bundled because the pinned .NET SDK 10.0.402 was not
installed on this workstation; the native application and acceptance harness
built with Visual Studio 2022 MSBuild.

## Next interchange gates

1. Build one immutable portable scene contract from each qualified reader:
   exact bone hierarchy and IDs, bind/inverse bind matrices, **all** skin
   influences, mesh/UV/material/image links, coordinate transform and timebase.
   Independently compare bind and animated skin positions against each native
   viewer. BT3's current static pose must become a verified rigged projection
   before it is called a reusable model export.
2. Use that same scene to write rigged glTF/GLB for Brawl, Melee and BT3.
   Include explicit animation clips only after interpolation, winding, root
   motion, loop/terminal behavior and material losses are reported. Verify
   imports in Blender and an independent glTF validator/engine.
3. Add an audited FBX reader/writer with explicit version and license
   provenance, then test actual binary FBX files in Blender, Maya and 3ds Max.
   An installed tool may be used for independent checking, not an undeclared
   runtime dependency of Fighterport.
4. Route portable **import** and bounded batch conversion through the same
   scene contract, with per-file failure receipts and no automatic game-native
   publication. Destination-specific Brawl/Melee/BT3/Ultimate/Rivals II and
   other game model writers remain separate compatibility gates.

The format choice will stay compact in the UI: one portable export action and
only formats that the current model/animation adapter has actually qualified.
No file extension alone is treated as proof that all 3D tools can import it.
