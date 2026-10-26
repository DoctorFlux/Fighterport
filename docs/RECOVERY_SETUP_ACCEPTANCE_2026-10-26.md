# Recovery setup and aerial fitting — 26 October 2026

Preview.42-recovery-setup fixes the reported Sonic AttackAirLw retargeted onto
Mario and fitted to Mario Fall. The source has 44 non-looping native samples.
The reported start 1-3 and end 35-44 ranges are valid and do not overlap.

A preserved baseline reproduces the exact error. Its options show all S/R/T
components, reference frame zero (native display frame 1), frozen anchors and
valid ranges. The failing field is hidden `PreserveContacts=false`, alongside
`BoundaryContactRecovery=true`. This original Next aerial has no baked support
paths, so requesting contact recovery cannot be satisfied even by checking the
hidden preservation option. The combined diagnostic incorrectly suggested a
range/component/reference problem.

Ordinary existing-body FK fitting already includes hips and legs. The UI now
makes contact recovery unavailable when the selected result has no recorded
baked support contacts, explains that body fitting includes hips/legs, and leaves
ordinary start/end fitting available. It never drops recorded contacts to make
an operation succeed, guesses contacts from animation names, or invents anchors.
Editor pins remain protected by the existing adapter. Missing/unsupported bindings,
loop recovery, conflicting pins and unsupported support paths retain their gates.

For a clip with recorded support paths, enabling recovery configures its required
contact preservation, all S/R/T components and frozen reference frame 1 together.
Those dependent advanced fields are disabled while recovery is active; choosing
that explicit mode exposes the policy in its help text. Turning recovery off
restores the prior advanced FK values. Explicitly restoring saved options clears
temporary toggle defaults before applying that preset. Floor-plane review is never
automatically affirmed. The native validation is unchanged in meaning, with separate
messages for missing contacts, components, reference policy and overlapping ranges.

There is no numerical algorithm, source/rig, skeleton, weights, native writer,
project format or game-adapter change. The full BrawlCrate Model Previewer, original
Next and existing Undo stay intact. Reviewed boundary reference controls retain
the existing policy; attack motion-position curves remain source-authored.

## Admitted background evidence

**829 checks / 6 runs / 37 inspected native PNGs.** All use the final source-frozen
`native-final2` product and the unchanged accepted engine.

| Case | Checks | PNGs |
|---|---:|---:|
| aerial-final3 | 111 | 5 |
| boundary-mario-final4 | 286 | 8 |
| boundary-snake-final4 | 264 | 4 |
| references-final4 | 29 | 8 |
| source-fit-final4 | 45 | 11 |
| floor-guard-final4 | 94 | 1 |

The new `-WorkspaceAerialRecovery` case opens authentic verified Mario/Fall and
Sonic/AttackAirLw from an isolated copy of the Brawl disc catalogue, creates the
actual original Next result, then operates the real fitting dialog. It verifies:

- The exact reported 44 frames, ranges 1-3 / 35-44 and automatic verified binding.
- Successful two-ended and end-only preparation without hidden advanced setup.
- Both endpoints independently match only Fall frame 1 under attack travel.
- Every motion-position key, tangent and semantic flag remains exact.
- Original join/middle keys and independent fractional middle poses remain exact.
- Truly overlapping visible ranges reject, and restored valid ranges prepare again.
- Preparation preserves live target, clip, bone, frame, content, Undo and dirty state.
- Apply, native Undo/Redo, verified export before project Save and fresh project
  reopen retain the exact result and valid frozen recipe; originals/index remain
  unchanged.

Affected grounded/reverse-rig cases verify independent native curve/pose oracles,
contact masks, support/plane receipts, unchanged source motion, F6, busy/cancel,
Undo and Save/export/reopen. New mode checks begin with deliberately incompatible
advanced FK values and verify all required recovery settings/disabled fields,
precise independent validation failures, restoration on toggle-off and explicit
saved-preset precedence. Bone masks are compared by exact membership where native
checkbox restoration changes enumeration order; no binding or permission is relaxed.

The reference regression covers project, separately opened native viewer and
read-only library choices, including cancellation/focus and export/reopen. The
current-clip workflow covers stale reference rejection, one native Undo command,
body-edit Undo, applied-fit export before Save and failed project publication.
The existing longer moving-travel floor case still rejects lift 0.967152106 above
the unchanged 0.788158679 limit, preserving document, roots and Undo.

Compact/wide 980/1600 fitting layouts pass at 100/175% workspace text. All nine
labelled sheets were inspected, plus the exact aerial endpoint at full size:
actual posed geometry is visible in both panes and controls fit/scroll. The
intentional baseline and superseded development/test probes are excluded from
counts. One probe expected an unrelated local native file to inherit disc binding
proof; the final exact-disc fixture preserves verification requirements. Another
preset check assumed serialized checkbox mask order; final checks preserve exact
mask/reference membership and the requested preset values.

Native product SHA-256:
`8B204817E796D075EBFBE5C8CB203030B182F4F8385F32C51FDA77912E67C7B2`.
Native and harness builds succeed with only the inherited CS1690 LoopInspection
warning in the native build. Evidence, exported test clips/projects, capture hashes
and review ledger remain excluded under `artifacts/Fighterport-recovery42`.

Build/run commands from the active checkout, with local input paths in variables:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory $native
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=$native/ --no-restore
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -TargetMotion $marioMotion -LibraryIndex $index -NativeDirectory $native -OutputDirectory $aerial -WorkspaceAerialRecovery
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ExitSamples 8 -NativeDirectory $native -OutputDirectory $marioFit -WorkspaceBoundaryFit -WorkspaceFootRecovery
./scripts/Test-BackgroundCapture.ps1 -Model $snake -Motion $marioMotion -RetargetSource $mario -TargetMotion $snakeMotion -Clip AttackAirF -ReferenceClip Fall -ExitSamples 15 -NativeDirectory $native -OutputDirectory $snakeFit -WorkspaceBoundaryFit
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -LibraryIndex $index -NativeDirectory $native -OutputDirectory $references -WorkspaceFitReferences
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -SourceCharacter snake -LibraryIndex $index -NativeDirectory $native -OutputDirectory $sourceFit -WorkspaceSourceFit
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ExitSamples 15 -NativeDirectory $native -OutputDirectory $floorGuard -WorkspaceBoundaryFit -WorkspaceFootRecovery -ReferenceControls -MovingRootFixture -ExpectFloorRejection
```

Testing uses private desktops and the shared offscreen renderer, with no OS input
injection, screen copying or foreground launch. ONE G1/M2 remains incomplete.
Physical input, monitor DPI, human usability, broad contact quality and actual game
acceptance remain unverified. Earlier optional fitting/Sonic/SCN0 rendering limits
remain as recorded in preview.39. Publish verified source only as DoctorFlux;
assets, settings, generated binaries and captures stay excluded.
