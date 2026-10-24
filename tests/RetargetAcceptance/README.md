# Native retarget acceptance

Build the real application first with `scripts/Build.ps1 -Configuration Release`.
Use VS2022 MSBuild to build `tests/RetargetAcceptance/RetargetAcceptance.csproj`
with `/restore /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false`.
Run the resulting `bin/x86/Release/net472/RetargetAcceptance.exe`. For the current
focused review, check one idle plus cliff get-up and attack in both directions:

```powershell
& .\tests\RetargetAcceptance\bin\x86\Release\net472\RetargetAcceptance.exe `
    --review-only `
    --mario-root '<local Mario folder>' --snake-root '<local Snake folder>' `
    --output '.\artifacts\retarget-native-run'
```

The roots must contain the costume `FitMario00.pac` / `FitSnake00.pac` and matching
`FitMarioMotionEtc.pac` / `FitSnakeMotionEtc.pac`. The environment variables
`FIGHTERPORT_TEST_MARIO_ROOT` / `FIGHTERPORT_TEST_SNAKE_ROOT` can replace arguments.
Output must be new/empty. Original files are hashed before and after. All generated
assets, source references, project copies and JSON reports stay ignored.

The native checks compare the format-neutral FK adapter with BrawlLib's independent
native evaluator, then evaluate fresh native output at every integer key and at
withheld `frame + 0.37` samples. The native skin export/reopen tolerance is 0.06
target model units, integer solver/native joint tolerance 0.06 and fractional
solver/native approximation tolerance 1.0. Actual maxima are documented in
`docs/RETARGETING.md`; passing a tolerance is not a claim of exact interpolation.

Wrong donors, missing/ambiguous maps, ancestry incompatibility, rebased local
axes, deterministic scrubbing, target segment lengths, full turns, provenance
persistence without a donor reference, edit/re-export and all 355 Snake PAC
slots are also checked. All 354 untouched slots retain interpolation samples.
In-memory numerical winding/rebasing cases supplement the real supplied examples;
they do not create replacement character assets. Use the separate real-executable
GUI runner to verify visible playback and project restart.

`--review-only` retains the generic characterization, FK/IK, proportion, native
export, project persistence and source-protection checks. Its actual motion set
is Wait1, CliffClimbQuick and CliffAttackQuick in each direction. It does not
repeat Wait2/Wait3. Omit this flag to request the broader regression suite; a
focused passing report does not claim that the broader set was rerun.

`--slow-review` selects Wait1, CliffClimbSlow and CliffAttackSlow in both
directions. `--cliff-review` selects Wait1 and all four quick/slow climb/attack
fixtures in both directions. Both retain the generic/native regressions and
check every quarter frame. Per-motion `*-feet-trajectory.json` files record
source/solved/native sole heights, support weights, ankle positions and each
mapped trajectory channel. Independent BrawlLib skin and unscaled trajectory
checks run across the entire clip, including unsupported phases. Additional
fixtures cover independent local axes, held prefixes, discontinuous sparse
native travel curves, actor motion independent of body size, and upward plane
crossing followed by settled support. None of these fixture names controls the
product solver.
Supplied cliff endings also assert both native soles within 0.03 of the idle
floor. A separate short traveling upright segment in the middle of unrelated
motion verifies automatic bilateral support without stationary locks. Strict
source mode and higher held lifts remain independent controls.

Both scopes retarget Snake's real clips onto Mario, export a new
Mario PAC with 297 slots and check all 296 untouched slots. Eighteen contact clips
in each direction cover Wait1/Wait2/Wait3 idle variations, a jab, run, landing,
down-smash, get-up, jump, airborne attack, face-up/down lying, squat and seated
SlipWait. Low foot lifts must retain source-scaled clearance; supported knee
bend planes must retain a dot product above 0.95 with the mapped donor pole.
Native contact heights and stationary planar locks must remain below
0.2 target units at quarter frames, native integer end-contact/lock errors below
0.03, end-contact solver errors below 0.02, and body integer/solver errors below
0.1. Contact skin positions are compared independently
with BrawlLib's weighted vertices. These are measured tolerances, not exactness.
Renamed bone/profile tests exercise the format-neutral solver without character
names. Diagnostic `--contacts-only` / `--reverse-only` runs isolate failures;
these flags do not substitute for either documented acceptance scope.

Quick/slow ledge climb and attack clips supplement these fixtures. They test
automatic suspended support, grip placement through native interpolation,
initial body position below the reference floor, and subsequent ground contacts.
The solver contains no ledge clip-name rules. Authored anisotropic scale is
checked separately from actual changes to local bone offsets or scale tracks.
`Run-LedgeAcceptance.ps1` exercises Wait1 and the selected quick/slow pair visibly in both directions,
exports/reopens each native motion and restarts the saved fighter projects.

Characterization checks do not supply the expected role maps to the detector.
They rename both real rest skeletons to Mixamo/Unreal conventions and anonymous
joints, repeat with independent leg/arm proportion changes, and verify inferred
anatomy against independently recorded native roles. A separate real native idle
export uses anonymous source/target bones and unrelated model/animation names.
`--mapping-only` isolates characterization and that native idle round trip.
Airborne attack fixtures explicitly select Airborne; production Auto uses source
geometry/contact duration and never a motion-name list.
The Wait1 acceptance fixture additionally requires both soles to remain within
0.04 target units of the floor across the entire quarter-sampled native clip,
including phases the contact detector would otherwise classify as unsupported.
Independent numerical hip-bounce cases verify grounded soles, proportional
supported hip height and nonzero preserved bounce in both directions. Rootless
17-role rigs and uniform scales of 0.01 and 100 are characterized without
arbitrary limits on limb proportions. Source contacts mode is checked separately
from Auto's persistent stance stabilization.

`--locomotion-review` selects Run, JumpSquat, JumpF, Fall and LandingAirN in
both directions, always in Auto mode. It retains the independent numerical and
native/persistence checks. Whole-clip quarter-frame records include support,
locks, both skinned soles and original/expected/native trajectory. Supplied
JumpF/Fall fixtures must have no floor support/locks and agree with corrected
FK throughout; their gameplay trajectory is not stored in those native clips.
Source actor Z is additionally compared directly, with zero error required.
Run/takeoff/landing must establish source ground contacts. A continuous
numerical takeoff/flight/landing control on each real rig independently labels
clear flight and ground periods, verifies release/reacquisition and checks actor
travel. Ground-height fitting is asserted only where its weight is complete;
flight is verified against FK rather than an assumed supporting floor.
