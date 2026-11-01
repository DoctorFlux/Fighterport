# Preview.65.7 — reviewed floor correction for ordinary fitting

Starting commit: `3b359b9d0f39ff3201b2c8adf69e3d09525accc7`.
The bounded **preview.65.7-reviewed-fk-floor** slice qualifies within the ONE
incomplete G1/M2. Preview.66 finite static ledges stays queued. Original Next,
the complete BrawlCrate Model Previewer and native Undo remain intact.

## Cause and implementation

Correct endpoints did not make the intermediate FK blend ground-safe. The four
previous tilt fits had no baked support counts, so the existing floor guard was
unavailable despite authentic geometry and reviewable hip/leg bindings.

The existing offline `BoundaryFloorRecovery` envelope now also serves ordinary
FK fitting, separately identified as `fk-boundary-floor-1`. It requires exact
reviewed body/leg bindings, complete weighted foot regions, an explicitly reviewed
finite plane and frozen first-frame references on a non-looping animation. It
does not invent contacts or authorize releasing preserved contact dependencies.
Pins, protected intervals, missing geometry and unreachable/budget-exhausted
corrections still reject. Body translation alone is corrected inside the selected
windows, with zero correction/slope at joins and endpoints. Source-owned travel,
native binds, hierarchy, inverse binds and every skin weight remain unchanged.

The shared native dialog exposes one optional **Prevent extra foot dipping**
checkbox in its ordinary panel. Floor height and explicit plane/baseline review
remain in Advanced. Existing guarded preparation and Apply dependency checks,
independent preview, current-member Apply and one native Undo transaction remain.
No floor is silently selected and nothing runs automatically on Save/export.
The existing recipe envelope gains an algorithm identity, not a project-format
migration. Native validation checks its exact windows, plane, limits, geometry
and frozen inputs; legacy contact-floor recipes still read normally.

No character, animation, filename, hash or bone-index numerical exceptions were
added. Fixture names/indices are explicit private binding data only. Profiles,
native interpolation and the retargeter/codec were not changed. The four freshly
retargeted inputs are byte-identical to the preceding corpus's inputs.

## Frozen bounds and before/after evidence

The pre-implementation `frozen-bounds.json` retains the existing **0.0002 native
unit added-penetration bound**, **20% of the shorter reviewed leg chain** lift
ceiling, 8192 integral body-key and 200000 pose-evaluation budgets. The envelope
fits 1/32-frame witnesses and validates 1/64/.37/.73 witnesses. No tolerance,
contact protection, native encoding or work limit was weakened.

Every case uses the donor's neutral forward tilt and the target's own **Wait1
frame 1**, with the same ten-sample end-only fit. The original beginning/middle
remains; no idle is appended. Fixture-assisted review confirms the existing
ancestor roles and both complete native foot chains through the normal control
review dialog. It does not claim a human session or automatic arbitrary-rig
inference. Each unchanged bind-plane guide is explicitly frozen as test context,
not accepted as a gameplay floor.

An independent native oracle measures both old and corrected serialized files
against identical complete uncapped foot regions, all positive weights and the
same authentic target reference under attack-owned motion-position controls:

| Transfer | Old maximum added dip | Corrected maximum added dip | Old fit bytes | Corrected bytes |
|---|---:|---:|---:|---:|
| Peach → Fox | 1.0171411783 | 0 | 170436 | 170724 |
| Fox → Peach | 0.5746523347 | 0 | 163900 | 164188 |
| Link → Samus | 1.8773414120 | 0 | 166480 | 166768 |
| Samus → Link | 0.0032160245 | 0.000000268221 | 127512 | 127800 |

All four old files reproduce an expected native floor rejection; all four new
files pass. **613 native times per version/case** cover 1/64 frames, .37/.73 and
two irrational held-out fractions. Both feet's every positively influenced native
vertex are included without a sensor cap or four-weight truncation. Native FK and
skinning evaluate fresh resource instances, independently of the Core envelope.

These added-dip numbers differ from the preceding report's raw bind-guide minima:
the complete full-weight foot/reference baseline can already be below that plane.
Authored source/reference penetration remains. This is clearance recovery, not
planted feet, zero absolute penetration, sliding/contact-orientation acceptance,
a minimum-lift solution or a continuous-time proof. Native orientation comparison
still uses the existing 0.025° bound; the largest endpoint residual is
0.000021934° with zero endpoint translation/scale error.

The 288-byte increase per file is 24 additional precise body-translation knots.
It is not a compression benchmark. Corrected editable masters remain independent
of export optimization; floor/contact acceptance continues to restrict compressed
copies to Exact until separately qualified. Ordinary export/Save remains exact.

## Tests and inspection

- **436 Core boundary/floor checks:** analytic dip, ordinary FK without baked
  contacts, entry/exit/both windows, unequal proportions, mesh volumes, moving
  parents, nonuniform scale, tiny influences and uncapped regions; renamed bones
  and reversed/permuted storage; exact roots/middle/endpoints, missing review,
  protected pins, overlaps, singular parents, nonfinite data and lift/work gates.
- **19 complete-skin checks:** five, nine and seventeen influences and immutable
  storage/dependency behavior remain intact.
- **228 native checks / four actual Library Use → Fit → Apply cases:** exact
  attack motion-position S/R/T, source files and complete model serialization;
  original beginning/middle keys, paired sides and tangents; fresh native
  export/reopen, same member/selection/frame/Undo owner, Undo/Redo, project
  Save/reopen and exact frozen reference persistence. **1190 pre-recovery native
  witnesses** supplement exact key checks. All fit results retain zero invented
  planted/locked contact counts.
- **295 native legacy checks:** Mario/Snake start/end and existing-track fitting,
  reference-control policy, prior contact-floor recipes, native/project reopen,
  Undo and 980/1600-wide layouts at 100/175% text.
- **2310 renderer assertions:** four private-desktop orthographic side GIFs,
  **318 encoded frames**, original donor / retained old fit / corrected fit,
  common native units/pixel, level +X view, visible plane guides and root travel.
  Each ends with one separate frozen target Wait1 frame-1 inspection card.
- Actually inspected: **40 selected GIF frames** in four target-pane sheets,
  four full-size peak-dip PNGs, all ten full native regression UI PNGs and all
  four applied endpoint viewport crops. Not every GIF frame is claimed viewed.

Local evidence: outer workspace `artifacts/Fighterport-floor657/index.html`,
`summary.json`, `run1/results.json`, per-case `independent-native-floor.json`,
`before-floor3/results.json`, `core-boundary3`, `complete-skin1`,
`legacy-boundary1`, `media1`, and visual-review/hash manifests. Sixteen final
original-file checks cover eight distinct immutable model/motion files. Report
links, GIF frame facts/delays, reviewed image hashes and original hashes verify.
Assets, private bindings, captures, generated tests/binaries and settings remain
ignored and are not published with source.

## Actual commands and bounded delivery

```powershell
MSBuild.exe BrawlCrate/BrawlCrate.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=$e/native1/ /m:1 /nr:false /v:minimal
./scripts/Build-Engine.ps1 -DotNet $workspace/.tools/dotnet/dotnet.exe -NativeDirectory $e/native1
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=$e/native1/ /m:1 /nr:false /v:minimal
dotnet run --project tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore -- $e/core-boundary3 --boundary-fitting
dotnet run --project tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-build -- $e/complete-skin1 --complete-skin
./scripts/Test-FittingCorpus.ps1 -NativeDirectory $e/native1 -Manifest $e/reviewed-corpus.json -OutputDirectory $e/run1
./scripts/Test-BackgroundCapture.ps1 -Model $mario -Motion $snakeMotion -RetargetSource $snake -TargetMotion $marioMotion -Clip AttackS3S -ExitSamples 8 -NativeDirectory $e/native1 -OutputDirectory $e/legacy-boundary1 -WorkspaceBoundaryFit -WorkspaceFootRecovery
& $e/render-tools/Build-SideComparison.ps1 -QualifiedNative $e/native1
& $e/Render-Corpus.ps1
& $e/Review-Corpus.ps1
python $e/generate_report.py
```

Core commands run from `engine` with the pinned workspace SDK 10.0.402. Initial
system-SDK/path attempts failed without changing project requirements. One new
test initially assumed the first stored track was the root; the reordered-track
fixture exposed that assertion bug. It now resolves the exact scene-root identity
and passes with unchanged numerical limits. Read-only old-output probes initially
needed complete native model population and the actual assertion-message shape;
those failed probe logs are retained before the final four expected rejections.

Qualified local package/launcher: `Fighterport-floor657/native1`.
Application SHA256:
`03467F286546C1F51821BF830082C2934E11205CDD581378F5A043EA8398217E`.
Core SHA256:
`F09847C378508E86B454BBCAEF74A760952AF09422C57EC5AA5AE43E87E624F9`.
Native BrawlLib is unchanged:
`29EDD2D2701C7FFF6356B7A1BB6E4BC82B7F367119A4954A8C0229C15B9C4EFB`.

Qualification covers this reviewed static-plane slice, not all anatomy, costumes,
contacts, ledges or games. Missing/unreviewed bindings, singular placement,
contradictory anchors and unreachable clearance still fail closed. Continuous
contact quality, minimal/floating recovery, physical input/DPI/human usability,
other game adapters and actual game acceptance remain open. Keep ONE G1/M2
incomplete and preview.66 finite ledges queued. Publish source only with verified
DoctorFlux attribution; preserve upstream licenses and historical failures.
