# BT3 native target viewer and motion edit — preview.82

Starting public commit: `4e19c7d8a9c0fb6079ef525c5f33257bd18f2eb7`.
This is a bounded increment in the one incomplete G1/M2 milestone. The supplied
PS2 USA `SLUS_21678` ISO stays local and unmodified.

## Implemented boundary

Opening a BT3 form as target moves the *existing* BrawlCrate ModelPanel into
the central viewer region. The existing side regions contain the native action
list, form and costume selectors, the parent/child bone hierarchy and current
bone key controls. The shared Bones, Polygons and Vertices buttons control the
BT3 renderer; camera controls and the viewport remain the original instances.
On return, the previous render targets, camera, selection, original panels,
project toolbar and document are restored. The original Next retargeter and
Brawl/Melee routes are unchanged.

The selected action uses an independent native BT3 BPE motion editor. Integer
frame keys can be added or replaced for a selected native bone, with session
Undo/Redo. The writer keeps original opaque decoded bytes and events, appends
changed records, reopens the generated BPE and checks timing, track membership,
translation and packed rotation before atomic publication to an independent
`.bpe` file. Original disc bytes and other native members are never rewritten.
Saved BPE can be loaded back into the exact selected action. Native bone IDs
and hierarchy are real; names are not present in the qualified pmdl reader and
are displayed as IDs rather than invented names.

The BPE writer uses a legal literal stream and may grow substantially. It is
not qualified for archive insertion or in-game acceptance. Project Save does
not yet own BT3 motions. Full native Brawl-style transform gizmos, curve and
timeline editing, native project persistence, model/texture writing, retarget,
fitting, cross-game export and other BT3 builds remain open. Future game
formats need format-neutral document/edit contracts, not a CHR0/BRRES facade.

## Idle evidence and open visual question

The decomp's `BtlAct_GetIdleMotion` maps ground action 0 to the default idle,
action 1 to the alternate gauge state, and flight 0x26/0x27 respectively;
model variant is a separate costume choice. This mapping was inspected at
`z3xox/Tenkaichi3Decomp@1c4d9060d859f5bc53a0843ad5937c04f43aa6e5`.
In the supplied ISO, Goku Mid action 0 is a 108-frame/688-key member with
packed SHA-256 `a1340beffd9ddfc85f3062a9f147f0a7586243d9d74b22131856feac73248428`.
The first normal costume is selected by default; the battle-damaged costume
is an explicit separate choice. A captured action-0 pose still looks wrong
to the user, so visual/game-equivalence of the decoded pose remains open;
action labels alone do not resolve that report. The secondary pack could not
be decoded as a skeletal motion with the current bounded reader and was not
silently substituted for the primary bank.
An exact-key probe found that all 54 tracks of Goku Mid actions 0 and 1 start
at native frame 0, ruling out missing first keys as the reason for the
reported initial pose. The current reader rejects action 39 for this form;
its label comes from game action semantics, not a claim of decode support.

## Background evidence

- `MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86` passed. Two pre-existing CS1690 warnings remain.
- `ControlsAcceptance.exe <local BT3 ISO> unused <output> --private-desktop --bt3-inventory --bt3-native-edit` passed 23 native edit/reopen/Undo checks (earlier core run).
- `ControlsAcceptance.exe <local BT3 ISO> unused <output> --private-desktop --bt3-direct-library --bt3-render` passed 30 checks, including shared toolbar toggles, key edit, Undo/Redo, native BPE save/reopen and viewer restoration.
- `ControlsAcceptance.exe <local Melee ISO> unused <output> --private-desktop --workspace-melee --melee-target-only` passed 34 checks.
- `ControlsAcceptance.exe <local Mario MDL0> <local FitMarioMotionEtc.pac> <output> --private-desktop --workspace-library-target` passed 39 checks.
- Fresh offscreen capture `artifacts/Bt3NativeEdit82integrated6/bt3-large-target.png` was inspected: native action list, bone hierarchy and selected-bone marker render in the original target viewer.
- The local launcher points at the isolated `Fighterport-bt3-native-edit-preview82` package. `Start-FighterPort.ps1 -Check` resolved its executable; packaged SHA-256 is `e0869c58e4d8bbcf7dffe1e3e0c0540b4dc91900ba866cd5821de19d62db80df`.

No physical input, game runtime, mod installation, non-USA build, or complete
cross-format authoring acceptance is inferred from these checks.
