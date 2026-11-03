# BT3 fighter-action labels — preview.91

Starting source commit: `089045f7be30af2e56f188dc2487ff3e1124b4a6`.
This is a bounded presentation increment inside the single incomplete G1/M2.
It does not change the native Model Previewer, original Next solver, root
control policy, source bytes, project format, skin weights or game output.

## Evidence and identity boundary

The inspected [Tenkaichi3Decomp revision
1c4d9060d859f5bc53a0843ad5937c04f43aa6e5](https://github.com/z3xox/Tenkaichi3Decomp/tree/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5)
describes common runtime motion choices in
[`btl_act_d.c`](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/src/battle/btl_act_d.c)
and other battle action handlers. Its
[`bobj_a.c` fighter binding](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/src/battle/bobj_a.c#L2308)
loads `0x19E` (414) direct fighter motion IDs from the primary pack. The
[`btl_char_fx_c.c` path](https://github.com/z3xox/Tenkaichi3Decomp/blob/1c4d9060d859f5bc53a0843ad5937c04f43aa6e5/src/battle/btl_char_fx_c.c#L737)
uses IDs from `0x19E` upward for partner/model animation. This distinguishes
the selected fighter's action list from later raw members. No decomp code or
game data was copied into this source tree; display labels are derived facts
with pinned evidence.

The checked USA disc's Goku Mid primary pack contains 440 raw members and
its secondary bank contains six. The library and native target list show
only primary fighter actions 000–413. The raw inventory still shows the
other 26 primary members and all six secondary members with bank/member IDs.
It does not associate those raw members with the fighter costume model or
offer them for direct retargeting.

## Implementation

- `Bt3ActionLabels` supplies short, source-pinned common roles for directly
  evidenced motion IDs: idle, movement, dash, jump/flight, rush, charge,
  blast, grab and related entries. Every other fighter action visibly says
  `Action NNN · Unlabeled`; an unverified role is not guessed from a pose or
  character name.
- A BT3 action's library context menu offers **Label BT3 action…**. The
  user can save or clear a reviewed display name for the exact saved source
  registration, form slot and native action member. Costume selection remains
  independent because costumes of one form use that form's motion bank.
- Labels are stored atomically in bounded `bt3-action-labels.json` preferences
  beside the library, never in the catalogue, project or original ISO. A
  corrupt or unavailable preference file falls back to exact numeric IDs.
  Aliases appear in library search, tree rows, details, native target action
  list, tooltips and caption. The native member ID stays visible.
- Search filters BT3 action labels in the background without decoding a
  motion or changing the target. Existing model/animation selection and
  retarget workflows continue to use exact native identities.

## Background verification

Release/x86 MSBuild of `tests/ControlsAcceptance/ControlsAcceptance.csproj`
passed, with two preexisting CS1690 warnings. On a private desktop with the
user's local USA ISO:

| Case | Result |
|---|---|
| `--bt3-inventory --bt3-mesh` | 27 checks passed. Verified exact 414-action versus 26 auxiliary plus six secondary-member display, label persistence, form/source isolation, clearing and corrupt-preference fallback. |
| `--bt3-direct-library` | 10 checks passed. Verified context-menu labeling and search by a saved form-specific label. |
| `--bt3-direct-library --bt3-render` | 46 checks passed. Repeated native source/target viewer, animation-switch, bone, playback, edit/Undo/Redo, native BPE save/reopen and label/search checks. |

The full viewer case needed a ten-minute watchdog rather than its former
three-minute default; the first run reached its former timeout after native
target checks and is not counted as a pass. The later complete run passed.
After the final preview.91 version stamp, Release/x86 and the 27-check
inventory case passed again. The source behavior in the 46-check viewer
case is otherwise unchanged by that metadata-only version stamp.
Fresh private-desktop captures were inspected: the action label is legible
in the library and the verified idle/movement labels appear in the original
Model Previewer's native animation list. No active-desktop capture or OS
input injection was used.

The local Goku Mid fixture's native model SHA-256 was
`bd03e3fb969e55e628dc0a0ed11e5f79dbfdc9b596f9453510e31a00c330b093`;
action 000 packed-member SHA-256 was
`f6742842faa40ccffef1c8f4634c0a813614bb5d9be9ce1db20601d5eeddc3c4`.
Generated captures, preferences, binaries and private disc bytes remain
ignored local evidence.

The local launcher now selects
`artifacts/Fighterport-bt3-action-labels-preview91/native`.
`Start-FighterPort.ps1 -Check` resolved the packaged executable, whose
SHA-256 is
`B4F4B2CBA104195458EAD3217E56C348FC47B9B337412905244FFD369DBA109A`
and whose embedded product version is
`2.0.0-preview.91-bt3-action-labels`. The source-only public commit
contains no package or private disc data.

## Open limits

The decomp does not provide a complete, verified per-form move-name table.
Unlabeled actions require review and a user-supplied alias; no claim is made
that all 414 action roles are now known or that every member decodes.
Partner/auxiliary member semantics and model associations need separate
verification. Human usability, physical input, monitor DPI, in-game
acceptance and broad BT3 retarget/contact quality remain unverified.
