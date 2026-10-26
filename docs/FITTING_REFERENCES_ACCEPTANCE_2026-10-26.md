# Fitting references and truthful preview — 26 October 2026

Preview.41-fitting-references addresses the reported same-character T-pose in
Fit start/end. Unprepared/invalidated results previously explicitly posed the
result model at native frame 0. Preparation could stop at a generic binding-review
gate while that bind-pose placeholder still looked like the fitted result.

The result pane now displays the current original animation, labelled “Original
animation · no fit prepared”, until a prepared candidate exists. Settings changes,
cancellation and preparation failures retain that truthful preview. Preview fit
still prepares independent native curves; review and Apply/Add remain explicit.

Start/end dropdowns include project clips and native animations already opened in
the Model Previewer. “Choose from library / source...” searches saved/shared and
temporary session catalogues for verified CHR0 references with the exact target
rig. It retains operation-owned read-only native leases and freezes precise CHR0
snapshots in the existing recipe; it never imports references into the project or
changes catalogue schemas. Exact clip/model identities distinguish duplicate names.
The chooser pages 200 rows, reports the complete filtered count and restores focus
on cancellation. Loading pauses fitting playback; opening fitting cancels pending
main-library auto-preview and pauses native/source playback. Ordinary playback
performs no reference discovery, native loading, hashing or fitting.

Automatic binding confirmation requires exact recorded rig/source evidence:
container/resource identity and original bytes, approved association, target rig
fingerprint and unambiguous native track binding. Names alone cannot establish it.
Unproven viewer/native imports retain an explicit confirmation above the choices.
Partial advanced range text no longer triggers full options parsing while updating
that eligibility. Saved options recompute automatic evidence; missing live
references remain disabled as before, with frozen originals retained in details.
Queued initial control preparation can finish before opening fitting for the same
unchanged model/clip; other busy/stale guards remain in force.

The complete BrawlCrate Model Previewer, original Next, native Undo, current-clip
Apply and independent multi-clip-copy route remain. Attack motion-position curves
remain source-authored. Reviewed orientation/reference-control fitting, pin/contact
protections and numeric/floor gates are unchanged. No solver, skeleton, bind, skin,
project format or game adapter is rewritten.

## Admitted background evidence

**947 checks / 7 runs / 45 inspected native PNGs.**

| Case | Checks | PNGs | Product |
|---|---:|---:|---|
| references-final3 | 29 | 8 | native-final2 |
| source-fit-final3 | 45 | 11 | native-final2 |
| boundary-mario-final2 | 277 | 8 | native-final1 |
| boundary-snake-final2 | 264 | 4 | native-final1 |
| keyboard-final3 | 210 | 7 | native-final2 |
| lifecycle-final3 | 37 | 6 | native-final2 |
| floor-guard-final3 | 85 | 1 | native-final2 |

The dedicated `-WorkspaceFitReferences` fixture uses authentic Mario AttackS3S
and Wait1 from the indexed Brawl disc, the project-owned motion package and a
separately opened native viewer package. All three reference routes produce the
same exact endpoint pose. It verifies a genuinely posed model rather than relying
on candidate/resource existence, explicit confirmation for unproven viewer data,
invalidated/blocked previews, cancelled picker focus and unchanged document state.
Apply freezes the exact library identity; export before project Save and a fresh
project reopen retain every fitted key/tangent/flag/timing field. Catalogue bytes
remain unchanged. Compact/wide 980/1600 layouts pass at 100/175% workspace text.

The source-fit workflow verifies current-clip Apply without target/selection/frame
replacement, stale-reference rejection, one native Undo command, exact Undo/Redo,
later body-edit Undo, Next provenance, protected controls, export before Save and
failed project publication. The two authentic rig-direction boundary cases verify
independent endpoint/middle numeric oracles, original keys/tangents, unchanged
travel, reference first-frame policy, floor receipts where enabled, F6 navigation,
busy/cancel guards and Save/export/reopen. Keyboard and lifecycle cases cover
registration/reuse, text editing, focus recovery and Save/Discard/Cancel/failure.

A longer 15-sample recovery with moving attack travel and reference-control fitting
requires lift 0.967152106, above the unchanged 0.788158679 bound. The explicit floor
guard case verifies rejection with unchanged document, roots and Undo. This is not
accepted contact quality. An earlier probe incorrectly expected this combination
to succeed; it is excluded. A keyboard probe paired Mario's model with Snake's
motion and failed its matching-source fixture lookup; the matching Mario pair
passes. Other superseded probes are excluded from totals.

All 11 labelled sheets were inspected, plus the new picker at full size. Geometry
is posed, controls fit/scroll, and failed/unprepared views remain the original.
This is offscreen visual inspection, not physical input or human-usability proof.
Evidence and review hashes stay in excluded `artifacts/Fighterport-fit41`.

Final native product SHA-256:
`9EB435135EF91859D3028674E7BB8412D395114AB92B55FB3313DB26A4CA8A0F`.
The two boundary regressions use earlier source-frozen native-final1:
`8C8677A14DCBA1E5781F1BBE5FECCE38B0F20E40917A1BF3A67DD32902B5B3F7`.
Final2 only separates binding eligibility from advanced-field parsing; the final
reference case explicitly tests incomplete input and restored options. Solver,
root policy, layout and boundary application code are identical across both.
Builds succeed with the inherited CS1690 LoopInspection warning. The unchanged
accepted engine is reused; binaries, game assets, settings and captures are excluded.

Build/run commands from the active checkout, with local inputs in variables:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory $native
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=$native/ --no-restore
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -NativeDirectory $native -OutputDirectory $references -WorkspaceFitReferences -LibraryIndex $index
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -SourceCharacter snake -NativeDirectory $native -OutputDirectory $sourceFit -WorkspaceSourceFit -LibraryIndex $index
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ExitSamples 8 -NativeDirectory $boundaryNative -OutputDirectory $marioFit -WorkspaceBoundaryFit -WorkspaceFootRecovery
./scripts/Test-BackgroundCapture.ps1 -Model $snake -Motion $marioMotion -RetargetSource $mario -TargetMotion $snakeMotion -Clip AttackAirF -ReferenceClip Fall -ExitSamples 15 -NativeDirectory $boundaryNative -OutputDirectory $snakeFit -WorkspaceBoundaryFit
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -NativeDirectory $native -OutputDirectory $keyboard -WorkspaceKeyboard
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -NativeDirectory $native -OutputDirectory $lifecycle -ProjectLifecycle
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ExitSamples 15 -NativeDirectory $native -OutputDirectory $floorGuard -WorkspaceBoundaryFit -WorkspaceFootRecovery -ReferenceControls -MovingRootFixture -ExpectFloorRejection
```

Private desktops and shared offscreen rendering are used without OS input injection,
active-desktop screen copying or foreground application launch. ONE G1/M2 remains
incomplete. Physical input, monitor DPI, human usability, universal contact quality
and game acceptance remain unverified. Existing Sonic/SCN0/native-rendering and
optional fitting limitations from preview.39 remain. No broader increment is promoted.
