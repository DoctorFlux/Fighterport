# Forward-tilt and aerial transfer corpus — 1 November 2026

Starting commit: `64eea3dfafc866f101b3bbdd098aa345cbffdebd`.
This is test-only evidence for unchanged **preview.65.5-contact-capabilities**,
inside the ONE incomplete G1/M2. No application/solver/codec changes or new
preview promotion. The launcher retains the qualified .65.5 package.

## Scope and outcome

The requested Peach/Fox and Link/Samus pairs were tested in both directions:
neutral forward tilt `AttackS3S` and all five aerials `AttackAirN`, `AttackAirF`,
`AttackAirB`, `AttackAirHi`, `AttackAirLw`. All 24 exact native members exist.
Each fixture pins its original motion/model SHA256, rig fingerprint, native
member path, version, duration and loop state before preparation. Fixtures and
assets remain private; action names select test data, never numerical policy.

| Direction | Forward tilt | N/F/B/Hi/Lw aerials | Passing cases |
|---|---|---|---:|
| Peach → Fox | Pass | All pass | 6/6 |
| Fox → Peach | Rejected native master | All pass | 5/6 |
| Link → Samus | Pass | All pass | 6/6 |
| Samus → Link | Pass | All pass | 6/6 |

Every case uses original Next correction, `GroundContactMode.Auto` and the same
default `SourceChannelTimes` preparation. All 23 successful candidates qualify
without dense fallback. These are full moves: no fitting, idle/fall append,
invented gameplay jump trajectory or root-motion removal. Stored clips are
non-looping CHR0 v4; repeating GIF playback does not change those flags.

## Rejection retained

Fox → Peach `AttackS3S` fails before producing/importing any native target.
At native time **3.213408**, target `RArmJ` (diagnostic bone index 90), depth 20,
the full-quality native bake measures **0.5949395976°** against unchanged
**0.025°**. Work 819,293; 1,803 samples; one unresolved interval. This is not
work-budget exhaustion. The endpoint residual is 0.0000261178°; the recorded
parent error is 0.0000181868°. The local Euler witness lies near a pole.

This identifies an outstanding generic fractional-rotation continuation/native
baking case, not a proven complete root cause or an impossible motion. No
character/action exception, correction-disabled substitute, tolerance relaxation
or partial target was introduced. Its GIF is explicitly **SOURCE ONLY**.
A separate read-only `--fractional-probe` samples the surrounding raw corrected
FK; that raw probe is diagnostic context, not the coherent correction-trajectory
master used for bake acceptance. The full failure and both diagnostics remain
in local evidence. Next implementation work should reproduce and fix this
generic path while retaining this failure and existing regression gates.

## Numeric and ownership checks

The 23 successful cases have **46 freshly decoded dense/default native motion
and contact oracle runs**, including **12,563 default-result witness times**
and the matching dense schedules. Existing preparation also runs continuous
scalar value/derivative certificates and cumulative native pose/skin checks;
the 23 quality packets record 2,390,302,370 aggregate vertex visits, not unique
vertices. Native trajectory checks include original eighth-frame solved anchors,
integer/eighth timeline samples and held-out .137/.371/.623 fractions.

| Maximum observed default-result field | Value | Meaning |
|---|---:|---|
| RootLocalError | 0.000005385165 | Source/target local floating difference; unchanged 0.0001 bound |
| MaximumAnchorOrientation | 0.01917554° | Original solved anchors; unchanged 0.025° bound |
| MaximumAnchorTranslation | 0.0001997103 | Original solved anchors; unchanged 0.0002 bound |
| MaximumAnchorScale | 0.0000001192093 | Original solved anchors |
| NativeBoneAgreement | 0.00002514271 | Fresh native decode versus coherent reference |
| NativeSurfaceAgreement | 0.000004106202 | Fresh native skinned-surface agreement |
| MaximumPlanarLockDrift | 0.001018315 | Subject to each rig's existing positional bound |
| SupportOrientationAgainstRawNext | 0.03768436° | Existing 0.25° contact orientation gate |

Anchor-only fields do not cover every arbitrary subframe; broader preparation
certificates/native witnesses are separately reported in each quality packet.
Finite geometry sampling is not a continuous-time contact/style/game proof.

All 24 attempts preserve original file hashes and native ownership fingerprints:
bone names/hierarchy, authentic rest SRT, bind/inverse-bind matrices, vertex bind
positions and **every** skin influence/weight. Successful finished-master →
default preparation preserves protected motion-control key times, values,
tangents, signed float bits and semantic flags exactly. The rejected case has
no master/result to compare; its protection flag is not passing evidence.

## Background rendering and visual review

Local evidence root: `artifacts/Fighterport-attacks655-20261101` in the outer
workspace. `index.html` selects one GIF at a time. `corpus.json`, inventory
fixtures/audits, `run1/results.json`, per-case `case.json`, native oracle JSONs,
render receipts, frozen renderer source/build receipts and visual review hashes
retain exact identities and coverage. Private source snapshots retain original
decoded coefficients/flags; their expanded I12 byte size is not an original
compressed-file size comparison.

Captures use private desktops and the shared offscreen native renderer, serialized
and BelowNormal. No active-screen copying or OS input injection. The camera is
fixed, level, orthographic +X (yaw 90°, pitch 0°), framed over each full move at
common native units/pixel. Authored root travel stays visible without per-frame
recentering or model rotation. Half-native-frame samples play at 30 GIF fps for
quarter-speed inspection under a nominal 60-frame clock. Gold guides show each
model's **native bind plane**, not newly reviewed terrain/gameplay context.

All **24 captures** pass **12,066 renderer assertions**, producing **2,376 GIF
frames**: 23 source/decoded-target comparisons and one labelled source-only
failure. All **24 overview PNGs / 200 selected GIF frames** are actually viewed;
one full-size source/target PNG is also inspected. Eight uniform samples plus
available worst-contact witness frames are selected per case. This is sampled
visual inspection, not a claim that every GIF frame was personally inspected.
CHR0-only previews retain native default visibility: unrelated props may appear.
VIS0/PAT0/material/weapon animation is neither transferred nor newly qualified.
Model-proportion, action-style, clothing/weapon clearance and absolute gameplay
quality remain separate from these sampled native/contact gates.

## Reproduction and changed source

`tests/RetargetAcceptance/NativeTransferCase.cs` adds a bounded exact-fixture
runner, unchanged production API preparation, fresh native reopen checks and
ownership/protected-control fingerprints. `Program.cs` exposes `--retarget-case`.
`scripts/Test-NativeTransferCorpus.ps1` runs independent cases and existing
`--verify-trajectory` checks, preserving failures rather than stopping the matrix.
`Complete` means all requested cases ran, not all cases passed. A nonzero per-case
exit/failing oracle remains a failure. Pause/cancel files are checked between
numerical cases; offscreen capture retains its existing per-frame controls.

Commands actually run (paths shortened to local evidence variables):

```powershell
MSBuild.exe tests/RetargetAcceptance/RetargetAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=$e/native1/ /m:1 /nr:false
RetargetAcceptance.exe --native-pair-fixture <source-model> <target-model> <motion> <exact-member-name> <fixture>
RetargetAcceptance.exe --native-member-audit <motion> <exact-member-name> <audit-output>
./scripts/Test-NativeTransferCorpus.ps1 -NativeDirectory $e/native1 -Manifest $e/corpus.json -OutputDirectory $e/run1
RetargetAcceptance.exe --retarget-case <source-model> <target-model> <motion> <fixture> <fresh-output>
RetargetAcceptance.exe --verify-trajectory <source-model> <target-model> <motion> <clip> <export> <fresh-output>
RetargetAcceptance.exe --fractional-probe <source-model> <target-model> <motion> <clip> <probe-output> <times>
& $e/render-tools/Build-SideComparison.ps1 -QualifiedNative $e/native1
& $e/Render-Corpus.ps1 -Root $e
& $e/Review-Corpus.ps1 -Root $e
python $e/generate_report.py --final
```

The test-only final helper moves `MasterReady=true` after the fresh reopen check.
Its passing smoke export is byte-identical to the corpus result (SHA256
`FD87D4C401F88CDA33E0DFBC49E09E12CA285505C69F317133AC79EF49E2B8D8`), and the
failing smoke retains the same rejection. Two acceptance builds succeeded with
zero errors. Existing reflection-populated-field warnings remain.

Application SHA256 unchanged:
`6BFBA755BA836422C550198C26659A513CE2CD50DA094877ECA6F9FAA16F80E8`.
BrawlLib SHA256 unchanged:
`D930DBF9DE21F1CEF7F2606A181F4BB66EA830D529F940B52B116A0E8038F458`.

No new UI, actual Use-button/Undo/Save-project tests, source-bone timing option,
optimized-export benchmarks or costume variants are claimed by this corpus.
Earlier acceptance remains separately recorded. Physical input, monitor DPI,
human usability, external runtime/editor and actual game tests remain unverified.
Preview.66 finite static ledges stays queued; the ONE G1/M2 remains incomplete.
