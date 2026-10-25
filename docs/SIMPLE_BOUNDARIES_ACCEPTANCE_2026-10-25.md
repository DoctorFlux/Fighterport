# Simple start/end fitting — 25 October 2026

Preview.32 (`2.0.0-preview.32-simple-boundaries`) extends the accepted preview.31
inside the ONE incomplete G1/M2 milestone. Fresh bounded background qualification passes **3,691 checks in 23 complete runs,
with all 117 native PNGs visually inspected**. These are mechanical/numeric checks,
not human-usability or game-quality scores.

## User flow

**Preview and fix → Fit start and end** opens a short default panel: Start pose,
Attack reached at frame, End pose, Recovery starts at frame, and Use for both ends.
Keep original start/end disables that side. The range summary uses inclusive
native display frames and explains shared joins. For 40 frames, entry 1–5 and exit
26–40 leave the original attack at and between display frames 5 and 26, including
the 20 interior frames 6–25. It does not add frames, rescale time or play the idle.
Each target reference defaults to its frozen first frame.

Preview prepares a separate result. Adding requires binding/pin and result/limit
review and creates an independent animation through the existing native import
and Undo. The default result summary is short. Advanced exposes the existing
native component/bone masks, reference timing, protected ranges, exact pins,
control policy, reviewed floor settings and full technical result/limits. Hiding
Advanced changes no settings. F6/Shift+F6 traverse setup, both native viewports,
report and footer; normal Tab/Enter/Space and modal Escape remain. Focused fields
scroll into view. Text uses the existing workspace setting, with explicit native
editor slots to avoid WinForms fixed-height layout overlap. Process DPI is unchanged.

## Fitting and ownership

`BoundaryContactRecovery` is an additive, separate option from the preserved
legacy `ExitContactRecovery`. New simple dialogs visibly enable boundary adaptation
for existing support paths; standalone Core and older saved options default false.
Non-looping clips can adapt the exact reviewed hips/legs and reviewed ancestor
controls inside either or both windows. Whole-clip support preservation remains
available without adaptation. Shared joins and the original middle retain exact
native keys, paired sides, tangents and flags. Complete motion-position/TransN
S/R/T stays source-authored at every time. Other reviewed ancestor controls may
match the reference in the windows under preview.31's explicit policy. No roles
are inferred from names. Original Next, its baked support provenance, authentic
rigs/rests/weights, native document ownership and Undo are retained.

Timed contacts contained in the unchanged middle may remain. A contact crossing
either window, fixed authored/editor pin, protected action/event interval,
unsupported hand/surface dependency or ambiguous rig rejects. Contact adaptation
requires complete S/R/T, the complete reviewed dependency mask and frozen first
reference poses. No support metadata is removed to make an operation pass.

New algorithms are `contact-boundary-recovery-1` and `contact-boundary-floor-1`.
Receipts record exact native windows and optional per-window floor proofs. Native
validation binds options, frozen source/reference identities, integral ranges,
policy and combined budgets; detached changed/fractional ranges and damaged floor
proofs reject. Existing recipe algorithms and project formats remain readable.
There is no second document, Undo stack, playback solver or project migration.

The reviewed floor envelope now accepts either explicit native-time window without
reversing animation/key pairs. Its correction is zero with zero slope at both
ends. Complete positive-influence foot geometry and original weights remain.
Each window retains the .20 shorter-leg lift bound and .0002 added-penetration
gate; the operation shares 8192 hip keys and 200000 pose evaluations across windows.
The baseline is min(0, original, authentic first reference under retained attack
travel). Native 1/32 fitting and independent 1/64/.37/.73 witnesses remain, with
additional native irrational witnesses. This is a bounded clearance observation,
not a continuous-time certificate, planted-foot solve or minimum-lift guarantee.
Changing either reference, phase, frozen mode, range or control policy clears
floor review and invalidates the candidate.

## Independent expectations and verification

Before tuning, the 40-frame procedural case freezes native entry times 0..4 and
exit times 25..39. For original Rx=t and frozen reference Rx=60, independent cubic
midpoints are H(2)=31.5 and H(32)=44.25. Tests retain both paired join sides and
check the original middle at fractional times. The two-ended weighted two-link
case independently requires .2928932188 units of lift in each separate window;
its authentic endpoints, original middle and actor travel remain unchanged.
Existing position .0002, scale .00001 and orientation .01-degree gates are unchanged.

Authentic checks use Snake AttackS3S on Mario with Wait1 at both ends, and Mario
AttackAirF on Snake with Wait1 entry and Fall exit. The display-frame-5 join and
15-sample exit exercise the simple range mapping. Separate shorter floor runs
verify both full native skin regions, including a deliberately authored nonzero
moving TransN witness. Those are fixture edits, never production exceptions.
Original donor/reference containers stay immutable. Checks cover cancellation,
busy guards, candidate invalidation, exact middle/root curves, native Undo/Redo,
precise export before Save and portable project reopen. All UI capture work uses
private desktops and the shared offscreen renderer with no OS input injection or
active-desktop pixels. Every accepted capture is visually inspected.

A longer 15-sample Snake-to-Mario floor recovery needs about .96715 native units
against its unchanged .78816 lift limit and correctly rejects. The expected-failure
case verifies no publication, changed attack travel or Undo changes. Earlier
layout probes exposing overlap and a regression replay missing explicit floor
re-review are excluded; the corrected final cohort is identified below.

## Commands and evidence

Run commands from the active checkout, with local authentic paths supplied as
fixture variables and a new ignored evidence folder for every invocation:

```powershell
./scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 "/p:OutDir=$native/"
# From engine/, with the .NET 10 toolchain:
& $dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $numeric --boundary-fitting
& $dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll $contacts --contact-slots
# From checkout root:
& "$native/RetargetAcceptance.exe" --motion-controls $modelA $modelB $rootEvidence
./scripts/Test-BackgroundCapture.ps1 -Model $target -Motion $sourceMotion -RetargetSource $source -TargetMotion $targetMotion -Clip AttackS3S -ReferenceClip Wait1 -ExitSamples 15 -WorkspaceBoundaryFit -NativeDirectory $native -OutputDirectory $output
# Add -WorkspaceFootRecovery for both-window reviewed-floor verification.
# Add -MovingRootFixture for the separate authored nonzero-travel witness.
# Add -ExpectFloorRejection for the frozen contradictory longer floor window.
```

| Final run | Checks | PNGs |
| --- | ---: | ---: |
| core-verified-final | 380 | 0 |
| contact-core-verified-final | 19 | 0 |
| roots-mario-verified-final | 628 | 0 |
| roots-snake-verified-final | 628 | 0 |
| foot-mario-verified-final2 | 83 | 6 |
| foot-moving-verified-final2 | 114 | 7 |
| recovery-snake-verified-final2 | 18 | 1 |
| transitions-verified-final2 | 80 | 10 |
| keyboard-verified-final2 | 174 | 7 |
| workflow-verified-final2 | 61 | 11 |
| scaling-verified-final2 | 410 | 11 |
| contact-native-verified-final2 | 62 | 7 |
| reference-mario-verified-final | 92 | 7 |
| reference-moving-verified-final | 123 | 8 |
| reference-snake-verified-final | 26 | 2 |
| both-mario-verified-final | 78 | 4 |
| both-floor-verified-final | 204 | 8 |
| both-moving-verified-final | 204 | 8 |
| both-snake-verified-final | 191 | 4 |
| both-rejection-verified-final | 16 | 1 |
| lifecycle-verified-final | 37 | 6 |
| export-verified-final | 23 | 4 |
| comparison-verified-final | 40 | 5 |

**Total: 3,691 checks / 23 complete runs / 117 inspected native PNGs.**

The ignored local qualification manifest binds source blobs, build/package hashes,
receipts, numeric results and inspected PNG hashes to the tested/publication commit.
The local development launcher selects the qualified preview.32 package after
acceptance; no visible application is launched during this background session.
Only source/docs/original fixtures are published to public DoctorFlux/Fighterport/main
under DoctorFlux author and committer identity, with contributor verification.
Game assets, native exports, projects, captures, settings and binaries stay local.

## Open gates

ONE G1/M2 remains incomplete. Physical input, human command discovery/usability,
monitor/mixed-monitor DPI, universal contact quality, foot locking/sliding, style
through arbitrary transitions and actual games remain unverified. The longer
floor rejection is not bypassed. New solvers/adapters, contact timelines, moving
supports, automatic attack slots and game-state/actor integration remain separate.
