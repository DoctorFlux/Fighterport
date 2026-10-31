# Preview.65.4 — bounded native retarget preparation

Starting revision: **39d580b22576f6d5df2946ce81010a62cd32172e**.
Product: current public Fighterport/main; historical Next is not a publication
destination. This qualifies one bounded increment of the ONE incomplete G1/M2.
Preview.66 static ledges remains queued.

The reported screenshot shows two million work units consumed, zero recorded
unresolved intervals and only eleven completed tracks. It establishes exhaustion
before all curves were checked, not proof that the unfinished motion is valid
or impossible. The undisclosed source/target pair was not reproduced. No asset
name, action, filename, hash or character-specific algorithm exception was added.

## Implementation and limits

`WindowedNativeCurveBaker` prepares eight-frame sections on the original absolute
native float timeline. Each section retains the **2,000,000-work ceiling**;
the previous whole-clip two-million ceiling is deliberately replaced, not claimed
unchanged. Total curve-construction work is bounded by
`max(1, ceil(lastNativeFrame / 8)) × 2,000,000`. Progress reports section count,
total work and largest section. Memory-heavy curve caches belong to one section.
Existing source/correction preparation has its separate unchanged work budget.
Decoded whole-master verification is a subsequent phase, not included in the
curve-construction counter; its witness set is bounded to two million items.
Existing rig/frame admission and the two-million-key limit still apply.

Euler continuation is seeded from the previous exact section endpoint. Shared
knots preserve value and both incoming/outgoing tangents through the existing
paired-key writer. Absolute float knot times avoid the rounding introduced by
subtracting and re-adding a section origin near an Euler pole. Full turns remain
authored Euler motion. No quaternion replacement curve or modulo normalization
was introduced. The unchanged native bake bounds are world orientation **0.025°**,
local translation **0.0002 native units** and maximum local scale component
**0.00001**, with depth 20. Contact limits remain **0.25°** and
**0.001 × reviewed limb scale**. No solver goal or tolerance was relaxed.

`NativeRetargetConstants` proves constant local scale/non-pelvis translation
from authentic source curves and the existing Next transform equations. Every
axis must have identical IEEE values and positive-zero tangents, or authentic
bind fallback. Equal key values with nonzero tangents, signed-zero ambiguity,
protected motion controls and solved pelvis translation do not take this path.
Proofs are source-content/hierarchy bound, rechecked and checked against captured
poses; uncertain/over-budget proof falls back to ordinary fitting. No rotations
or validation witnesses are skipped. These groups keep one unchanged first key.
Source-key preparation can materialize that identical fixed function at required
source paired times; it never invents a derivative or discards paired sides.

After final shared-bone restriction, the complete master is actually serialized,
freshly loaded and compared at the union of all section acceptance witnesses.
All target bones, native metadata, entry order/names and model/scale-compensation
flags are checked. Native exports still use the existing precise writer. The
verification uses our native decoder and detached rig evaluator; it is not an
independent third-party codec or an in-game test. Existing independent native
MDL0/skin workflow checks remain additional evidence. A failed section,
cancelled operation, changed proof dependency or decoded mismatch exposes no
partial animation. Budget-only errors now say that preparation exhausted a
section, rather than inventing an unresolved interval at bone -1.

Original Next, full native viewer, source-local roots, every skin influence,
MDL0 hierarchy/binds, authoring document/Undo and export optimization policy
remain intact. Nothing new runs on playback ticks.

## Fresh qualification

Ignored evidence root: `artifacts/Fighterport-budget654` in the parent workspace.
Final production executable and both acceptance harnesses were built in
`native1` (build11). No game assets, reports, settings or binaries are published.

| Final case | Result |
|---|---|
| Original procedural 128-bone, 161-frame hierarchy | 19 checks; 20 sections; 2,155,940 total work; largest 107,797; 3,700 section sample evaluations; 397 ms |
| Existing local precision | 1,216 checks |
| Source-local motion controls | 628 checks |
| Coordinate/unequal-axis regression | 94 checks |
| Actual Sonic → Mario Use/both key modes/own Wait1 | 74 checks |
| Actual Snake → Mario Use/both key modes/own Wait1 | 74 checks |
| Actual Mario → Snake Use/both key modes/own Wait1 | 74 checks |

Total: **2,179 passing assertions**. The procedural case's 740 constant-group
jobs count 37 unique groups repeated over 20 sections, not 740 distinct bones.
Its single-pass control, with proof disabled and the original charge path,
exhausted 2,000,000 work after four tracks/3,681 samples with zero unresolved
intervals (`baseline4`). That control was not a separately built old checkout.
Constant-only preparation also exhausted work (`candidate4`); it was not accepted
as the complete fix.

Procedural tests independently decode native curves at 679 integer/withheld/seam
witnesses; separate near-pole, 720° turn and looped/nonlooped terminal cases check
both sides of section joins. They include nonuniform/negative animated scale,
authentic bind fallback, equal values/nonzero tangents, signed zero, stale proof,
one-frame motion, cancellation between sections and a genuine later-section
protected-parent contradiction. The latter rejects all earlier partial work.
The native control/world checks are sampled; they are not a continuous proof.

All three actual Use workflows use exact pinned AttackS3S members, including
Snake's first neutral part. They exercise explicit review, current import,
target-owned Wait1 frame 1 fitting, Apply, Undo/Redo, export/reopen and project
Save/reopen. They preserve original clip/model bytes, all weights, the original
beginning/protected middle and attack-owned TransN. No idle clip is appended.

Six fresh native oracles additionally verify both source-key exports:

| Direction | Native samples per mode | Maximum support orientation against raw Next | Root-local error |
|---|---:|---:|---:|
| Sonic → Mario | 474 | 0.03949405° | 0 |
| Snake → Mario | 342 | 0.02657581° | 0 |
| Mario → Snake | 353 | 0.03436275° | 0 |

These contact observations use the existing 0.25° bound, not the tighter
representation-only 0.025° bound. Held-out final-build native bakes/reopens:
Sonic AttackAirLw → Mario **1,118 ms**, Mario AttackAirHi → Sonic **1,726 ms**,
and the additional Sonic AttackS3S → Snake pair **2,508 ms**. Successful native
representation does not establish all anatomical/contact/style acceptance.

Private-desktop/offscreen workflows performed no input injection, active-desktop
window operation or screen copying. Eighteen fresh mapping/endpoint PNGs have
exact SHA256 continuity with already inspected preview.65.3/.65.2 captures. Two
own-Wait1 endpoint images were also directly reopened for visual review; the
other sixteen use explicit prior-view continuity (`visual-review11.json`). The
viewer, visible floor and target-owned endpoint remain intact. No physical-input
or new motion-GIF claim is made for this increment.

## Commands and retained failures

### Additional new-character idle request

The follow-up requested other character combinations, using only their idle.
Eight directional cases use the unique authentic **Wait1** member in the original
cached disc motion PAC; no fit, alternate idle or appended endpoint substitutes.
Every original motion/model hash remains unchanged. The six admitted transfers
serialize/reopen without the reported work-budget error. They are dense original
Next masters; this extra corpus does not qualify both source-key UI modes for
these six pairs, which have separate three-direction coverage above.

| Donor → target | Source frames | Bake | Complete native/contact oracle |
|---|---:|---:|---|
| Fox → Peach | 101 | 7.109 s | Pass, 1,101 samples |
| Peach → Fox | 101 | 14.193 s | **Fail**, planar contact drift 0.0127292 > 0.00596133 |
| Captain Falcon → Luigi | 61 | 1.824 s | Pass, 661 samples |
| Luigi → Captain Falcon | 61 | 2.047 s | Pass, 661 samples |
| Link → Marth | 121 | 2.514 s | Pass, 1,321 samples |
| Marth → Link | 71 | 2.150 s | Pass, 771 samples |
| Link → Samus | 121 | Not admitted | Samus right.hand needs reviewed mapping |
| Samus → Link | 137 | Not admitted | Same mapping requirement; no guessed cannon binding |

All six native pose comparisons retain the unchanged 0.025°/0.0002/0.00001
representation limits; five pass the **complete** existing contact oracle.
Peach → Fox is not accepted as a contact-qualified result. Its maximum intended
support orientation is 0.0107714°, penetration 0.00067918, but planar locking
fails. Root-local observed difference is 5.96e-8 there and zero in the other five.
All skin influences remain included in native bone/surface comparisons. This
failure is retained for a separate generic contact investigation; its bound is
not relaxed and no name-specific adjustment is introduced.

The new additive `-NativeAnimationPreview` harness route renders the **exact
newly imported exported clip**, excluding any animations already embedded in the
target costume PAC. It captures first/middle/last poses in the full native viewer
without posing, fitting or retargeting during capture. Six private-desktop runs
pass **30 additional read-only assertions** for identity, document content,
model weights/binds, Undo, dirty state and original/export bytes. Eighteen PNGs
were reviewed: fifteen directly, three with exact SHA256 continuity to directly
viewed initial Fox → Peach captures. No T-pose/missing target mesh was seen in
those poses. Weapon/clothing attachments remain native; these views are not
contact or style acceptance. The preview-capture camera does not establish a
floor/contact measurement. Local report: `idle-results13.html`; numeric identities
and hashes: `idle-pairs11.json`, `idle-summary13.json`; image review:
`idle-visual-review13.json`.

The first preview helper incorrectly assumed a costume PAC contains no CHR0;
Fox's embedded resource rejected that assumption (`idle-preview-Peach-Fox`).
Selection was corrected to the explicit newly imported node; all six fresh
`idle-preview13-*` runs pass. This was a test-helper error, not a production
retarget change. The additive harness alone was rebuilt (build13, zero
warnings/errors); the qualified production executable hash is unchanged.

Commands run, with private local fixture/output paths abbreviated:

```powershell
MSBuild.exe BrawlCrate/BrawlCrate.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<native1>/ /m:1 /nr:false
MSBuild.exe tests/RetargetAcceptance/RetargetAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<native1>/ /m:1 /nr:false
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<native1>/ /m:1 /nr:false
RetargetAcceptance.exe --adaptive-budget-baseline <baseline4>
RetargetAcceptance.exe --adaptive-budget <final-budget11>
RetargetAcceptance.exe --local-precision <precision11>
RetargetAcceptance.exe --motion-controls <original-model> <original-motion> <roots11>
RetargetAcceptance.exe --coordinate-frames <coordinates11>
scripts/Test-BackgroundCapture.ps1 -Model <target> -Motion <donor-motion> -RetargetSource <donor> -TargetMotion <own-motion> -Clip AttackS3S -AuthenticFixtureManifest <pinned> -WorkspaceAuthenticLibraryFloat -RetargetTrajectoryCase -ReviewedRecoveryCase -NativeDirectory <native1> -OutputDirectory <use11-direction>
RetargetAcceptance.exe --verify-trajectory <donor> <target> <donor-motion> AttackS3S <actual-retargeted-export> <oracle11>
RetargetAcceptance.exe --bake-probe <donor> <target> <donor-motion> <exact-clip> <corpus11-case>
RetargetAcceptance.exe --native-member-audit <original-motion> Wait1 <idle-inventory>
scripts/Test-BackgroundCapture.ps1 -Model <target> -Motion <exact-exported-master> -NativeAnimationPreview -NativeDirectory <native1> -OutputDirectory <idle-preview13-case>
git diff --check
```

Build11 has two pre-existing CS1690 application warnings; acceptance builds have
zero warnings/errors. Failed trials remain local: section-relative time rounding
rejected authentic Sonic near-pole witnesses (`candidate5`/`sonic6`); corrected
absolute native time is verified. A proposed discontinuity refusal assertion was
invalid at native float resolution (`final-budget9`) and replaced by an actual
protected-parent contradiction, without changing production limits. The real
Use test caught a missing paired source seed after fixed-channel proof (`use10`);
the explicit fixed-function materialization above passes fresh `use11` tests.
Negative bind-scale procedural input was rejected by existing admission and
replaced by positive binds with negative animated scale; admission was not loosened.

Final app SHA256:
`3ED9FF91F8A001B94742B65F1890FC1CD54A7951E737C63CB75A86117A2A28A2`.
Unchanged BrawlLib SHA256:
`D930DBF9DE21F1CEF7F2606A181F4BB66EA830D529F940B52B116A0E8038F458`.
Local candidate version: `2.0.0-preview.65.4-bounded-native-bake`.

## Remaining boundaries

An exceptionally difficult individual eight-frame section can still refuse at
its work/depth/quality limit. This change removes the demonstrated whole-clip
scaling failure; it does not promise every rig/animation converges. The exact
undisclosed pair needs a retry with this executable and is explicitly unverified.
Sonic's earlier body/head floor intersection, broader clearance/style, the full
18-case contact corpus, finite ledges, moving supports, Melee, external-editor
compatibility, physical input, monitor DPI, human usability and in-game acceptance
remain open. No tolerance, failed case or existing gate is removed to qualify it.

## Publication and launcher

Implementation **feea9c2529626010229b9161c6ab2afac39fe810** is published to
public `DoctorFlux/Fighterport/main`. GitHub metadata confirms public visibility,
main as default branch, DoctorFlux author/committer and DoctorFlux as the only
listed contributor. No upstream historical repository was pushed and no
collaborator was added. Source/tests/documentation only were staged.

The ignored development launcher now selects `Fighterport-budget654/native1`.
`Start-FighterPort.ps1 -Check` resolved the frozen .65.4 executable without
opening a window. Production executable and unchanged BrawlLib hashes above were
rechecked after all extra idle tests and the test-only build13. A running older
application requires restart through the normal launcher to use this build.
