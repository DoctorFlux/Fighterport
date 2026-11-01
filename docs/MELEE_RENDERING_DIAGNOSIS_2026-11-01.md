# Melee native rendering diagnosis — 1 November 2026

Starting application revision: `c8a24af492fedfe2e1641c23ccefa8b5aaf06fcb`.
Active product: public DoctorFlux/Fighterport, not the historical Next repository.
This is the read-only source/render stage of the one incomplete G1/M2.
Melee target editing, motion retargeting, edited native writing and asset conversion
remain separate unqualified stages; preview.66 ledges stays queued.

## Pinned evidence

- HSDRaw/material modules: Ploaj/HSDLib
  `85567e40797de3c820a55476eca54c704921a848`, MIT. See retained licenses and provenance.
- Runtime reference, inspected locally: [doldecomp/melee
  `598987cf347c125c7416517af18bf5c1dc3f15a1`](https://github.com/doldecomp/melee/tree/598987cf347c125c7416517af18bf5c1dc3f15a1).
  Inspect `src/sysdolphin/baselib/{jobj,mtx,pobj,displayfunc}.c`,
  `src/melee/lb/lbanim.c`, and `src/melee/ft/{ftparts,ftdata}.c`.
  No decomp source was vendored. Owned code implements the independently described
  matrix and pointer-table contracts; game code is not executed.

The decomp is runtime evidence. The upstream viewer's approximations and successful
parser/UI assertions are distinct evidence and were insufficient for this bug.

## Causes and implementation

The initial bridge used Brawl inverse-bind reconstruction and ordinary inherited
SRT to skin HSD geometry. It also displayed mutually exclusive native model parts
together. Detailed captures showed Samus's morph-ball mesh over the body and
Peach's held parasol during the selected idle.

`MeleePose` now uses the original native envelope matrices, native rigid/weighted
branches, owner/skeleton bases and compensated parent scale. Figatree type and
AnimJoint flags supply their native animation scale overrides without mutating
stored JOBJ flags. Bind display restores authentic rest behavior. Complete blended
matrices provide inverse-transpose normals. Shared display lists use their explicit
native joint reference, not a guessed parent. Every original influence remains.

The native renderer evaluates each display-list matrix slot once per pose; it does
not invert a matrix for every triangle vertex. The Brawl-derived inverse-bind skin
path was removed from this renderer. The independent MDL0 facade is not a qualified
Melee writer or converted editable target. Original native graphs stay authoritative.

Neutral ambient/key/fill lighting keeps native textures/material values intact.
Game scene lighting, bump/shape/material animation and all runtime effects are not
claimed identical to the game from these captures.

`MeleePartVisibility` reads exact native DOBJ variant tables. `MeleeCostumeBinding`
resolves the selected native filename/root pair from bounded DOL pointer records,
without character names, fixed executable addresses or costume index tables in the
algorithm. No executable code is run. Folder sources without main.dol can use only
identical effective costume tables; ambiguous/missing context remains explicit.
High-detail selection restores shared parts after low-detail initialization.

The compact **Parts...** action reviews source-only variants. Its initial first
variant is an inspection policy, not a claim to simulate fighter OnLoad callbacks
or action scripts. Cancel restores the previous preview choices. The Peach evidence
explicitly reviews the parasol/alternative-dress groups as test input; there is no
corresponding character/group exception in production. Selections are session-only.
No parts, source resources, MDL0 binds, hierarchy, weights or target animations are
deleted or changed by source visibility review.

## Completed checks

Background runs use the existing private desktop and shared ModelPanel renderer.
No active-desktop pixels, OS input injection or physical-input claims are involved.

- `MeleeAcceptance --procedural`: 52 independent checks cover unequal inherited
  scale, classical-scale overrides, authored envelope versus reconstructed rest,
  rigid/shared/weighted branches, 5/9/17 complete influences, exact native source
  bytes, Hermite/full-turn/sparse curves, malformed input, mod overlays, visibility
  masks and bounded native executable costume bindings.
- Native corpus: all 156 indexed costume models prepare finite native bind poses.
  Complete graph reopen checks pass: 84 no-edit graph-equivalent native rebuilds,
  72 conservative original-byte fallbacks. This is not 156 visual/game passes.
- `Test-BackgroundCapture.ps1 -WorkspaceMelee`, final `ui18`: 42 checks pass for
  Mario, Fox, Samus, Peach and Donkey source preview, cached motion change, source
  costume association, actual 100/175% Parts-dialog cancellation, original-file
  export, two isolated procedural mod instances and unchanged ISO hash. Twelve
  detailed angle captures also assert non-background model pixels after resize.
- Native CHR0 structure regression: 182 checks pass, including protected root-pair,
  fractional-time, tangent, semantic-flag and source-preservation cases.

Raw local receipts/logs and captures are excluded under
`artifacts/Fighterport-melee67`. Retain the failed earlier runs: incorrect matrix
projection, blank/black views, alternate parts, shared visibility order, empty-group
cancel and the resized first-frame capture were discovered rather than counted as
passes. A wrong scaling fixture lacked Wait1; the next attempt lacked the engine
runtime. Those setup failures are retained separately from completed regressions.

## Commands

```powershell
MSBuild tests/MeleeAcceptance/MeleeAcceptance.csproj /p:Configuration=Release
MeleeAcceptance.exe --procedural <new-local-output>
MeleeAcceptance.exe <local-iso> --inventory <local-json> --models --roundtrip
MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86
scripts/Test-BackgroundCapture.ps1 -Model <local-iso> -Motion <local-iso> -OutputDirectory <new-local-output> -WorkspaceMelee
scripts/Test-BackgroundCapture.ps1 -Model <native-model> -Motion <native-animation> -OutputDirectory <new-local-output> -NativeExportStructure
```

Exact source identity, source instance and prepared native curves are retained.
Only preparation reads/decompresses/hashes/decodes source data. Frame updates consume
prepared scalar players, matrices and mesh data; they do not load files or solve.
The file/source hash assertions occur outside playback, not on a tick.

## Open gates

The broader Brawl scaling suite is not accepted: with authentic Wait1 inputs and
the unchanged qualified engine it reaches 321 successful layout/preservation checks,
then times out in its later modal-file workflow. The unchanged .65.8 baseline reproduces the same timeout after the identical 321
assertions. Preserve both failures; do not claim the missing remainder passed.


The initial part policy does not recover arbitrary game-code defaults or time-varying
visibility. Exact game lighting, special matrix providers, material/shape animation,
physical input, monitor DPI, actual modpack acceptance and actual game behavior stay
unverified. Real modpack folders were not supplied; procedural overlays are separate.

Melee native target ownership/editing, full project persistence, edited native
replacement-file export, bidirectional Next retarget/fitting and Brawl/Melee asset
conversion remain open. The source-copy exporter is explicitly unchanged model and
whole motion files, not export of newly edited Melee animation or a rebuilt ISO.
