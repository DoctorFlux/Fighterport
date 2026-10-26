# Apply fitting to the current animation — 27 October 2026

Preview.43-apply-current-fit makes the fitting dialog a direct editing operation:
Preview fit, inspect the prepared result, then Apply fitting. There is no Save as
new toggle, new-name field or extra result-confirmation checkbox. The Apply button
is enabled after successful preparation. Clicking it is the explicit commit;
required binding/floor reviews, numeric limits, busy guards and stale dependencies
are still validated. Save and CHR0 export remain separate ordinary actions.

The old dialog forced multi-animation PACs into copy mode, and selecting that
mode alone performed no edit. The new action updates the exact already-selected
owned CHR0 with native before/after snapshots, promotes its precise native writer,
refreshes the existing editor and records one command on its existing Undo stack.
It does not replace the target/clip owner, append a result, rebuild the scene or
seek/change the selected bone or camera. Names and duplicate labels do not choose
the destination. Source game files and other PAC members remain unchanged.

A multi-clip asset needs distinct saved recipes. An optional `ClipTransitionFits`
collection associates each receipt with the existing asset ID and canonical native
member identity. No name matching or second document/Undo owner is introduced.
SchemaVersion remains 1; old single-clip `TransitionFit` receipts still read and
round-trip. This is additive metadata, not a claim that older application versions
understand the new per-member receipts. New metadata is omitted when empty.
Canonical/unique keys, matching source-owner identity, non-null valid receipts,
4,096 records per asset and the existing aggregate 32 MiB frozen-input budget are
validated before project publication. Existing document/asset bounds remain.

Fit settings/details, review approval and verified single/set export now resolve
the selected member's exact receipt. Fitting a sibling does not invalidate an
unchanged member's approval/frozen export; editing that member's content or recipe
still does. Retargeted standalone clips retain their original Next provenance and
existing single-clip metadata path. Refit uses the current authored animation,
including edits since the previous fit.

The full BrawlCrate Model Previewer, original Next, root-control policy, frozen
reference-frame policy, contact/floor gates and native writing are preserved. No
solver, skeleton, bind, weights, source association or game adapter changes.

## Admitted background evidence

**1,271 checks / 9 runs / 65 inspected native PNGs.**

| Case | Checks | PNGs |
|---|---:|---:|
| members-final1 | 53 | 8 |
| source-fit-final1 | 45 | 11 |
| aerial-final1 | 111 | 5 |
| boundary-mario-final1 | 287 | 8 |
| boundary-snake-final1 | 265 | 4 |
| batch-review-final1 | 182 | 9 |
| set-export-final1 | 152 | 8 |
| native-transitions-final2 | 81 | 11 |
| floor-guard-final1 | 95 | 1 |

The expanded native reference fixture applies directly inside the authentic Mario
motion PAC. It verifies exact selected model/clip/bone/frame and one native Undo
command; every sibling stays unchanged. A second member is temporarily given the
same display name, fitted independently and renamed back. Both native identities,
receipts and complete curves survive cross-clip Undo/Redo, verified export before
Save and fresh project/PAC reopen. Unfitted siblings retain every curve. An approval
and explicitly owned frozen export for the first member survive the sibling fit.

Detached projects reject duplicate/noncanonical identities, foreign owners,
missing receipts, excessive records and the aggregate snapshot budget before any
project appears at the destination. The previously accepted preview.42 aerial
project opens with its legacy receipt and round-trips without migration. These
synthetic metadata failures do not claim playable fabricated native resources.

The original Next Sonic AttackAirLw-to-Mario Fall case still passes the exact
44-frame 1-3 / 35-44 ranges, end-only fitting, first-reference-frame endpoints,
independent fractional middle sampling and exact attack travel. Both authentic
rig directions retain endpoint/join/middle/root oracles. Grounded floor checks
seal an independently verified exact pre-Apply input before modifying the live
clip, preserving the meaningful original baseline after in-place Apply.

The single-clip workflow covers stale references, Apply on the same identity,
Undo/Redo, later body-edit Undo, precise export before Save, fresh reopen and failed
project publication. Batch/review and atomic set-export regressions pass. Native
transition, frozen-reference/details, slot, FK/IK/pole, native interpolation and
precise save/reopen checks pass with the current fitted output. The longer moving-
travel floor recovery still rejects the unchanged numeric limit without publishing
or altering source motion/Undo. No safety or quality threshold is loosened.

Compact/wide fitting views pass at 100/175% text. All 15 labelled overview sheets
were inspected, plus the simplified prepared dialog at full size. A legacy native
fixture had not reframed its main view after its own setup scene rebuild; its old
cropped result capture is excluded. Final2 explicitly frames before fitting,
checks the camera remains exact through preview/Apply, and shows the full posed
model before and after. Other superseded build/development probes are excluded;
one reference assertion initially mixed library `$` keys with native `root` keys.
Production receipt identities use the existing canonical native domain.

Qualified product SHA-256:
`395D053390E0D933B8403D21A5B4E6B71BFA3936387FA7C25F8A76ED57ED4892`.
Native and harness builds pass; only the inherited CS1690 LoopInspection warning
remains in the native build. The accepted engine is unchanged. Evidence stays in
excluded `artifacts/Fighterport-apply43`, including source hashes and visual ledger.

Build/run commands from the active checkout, with local inputs in variables:

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory $native
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=$native/ --no-restore
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -NativeDirectory $native -OutputDirectory $members -WorkspaceFitReferences -LibraryIndex $index -LegacyFitProject $preview42Project
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -SourceCharacter snake -LibraryIndex $index -NativeDirectory $native -OutputDirectory $sourceFit -WorkspaceSourceFit
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -TargetMotion $marioMotion -LibraryIndex $index -NativeDirectory $native -OutputDirectory $aerial -WorkspaceAerialRecovery
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ExitSamples 8 -NativeDirectory $native -OutputDirectory $marioFit -WorkspaceBoundaryFit -WorkspaceFootRecovery
./scripts/Test-BackgroundCapture.ps1 -Model $snake -Motion $marioMotion -RetargetSource $mario -TargetMotion $snakeMotion -Clip AttackAirF -ReferenceClip Fall -ExitSamples 15 -NativeDirectory $native -OutputDirectory $snakeFit -WorkspaceBoundaryFit
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -NativeDirectory $native -OutputDirectory $review -WorkspaceBatchReview
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -NativeDirectory $native -OutputDirectory $setExport -WorkspaceSetExport
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $marioMotion -NativeDirectory $native -OutputDirectory $nativeEditing -TransitionFit
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ExitSamples 15 -NativeDirectory $native -OutputDirectory $floorGuard -WorkspaceBoundaryFit -WorkspaceFootRecovery -ReferenceControls -MovingRootFixture -ExpectFloorRejection
```

Tests use private desktops and the shared offscreen renderer without OS input
injection, active-desktop screen copying or foreground launch. ONE G1/M2 remains
incomplete. Physical input, monitor DPI, human usability, broad contact quality
and actual game acceptance remain unverified. Existing optional fitting/Sonic/SCN0
rendering limits remain. Publish source only as DoctorFlux; assets, settings,
captures and generated binaries remain excluded. The launcher selects the qualified
build without opening a foreground window or closing the user's current session.
