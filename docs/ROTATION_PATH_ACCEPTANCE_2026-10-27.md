# preview.44 — native rotation paths

Accepted bounded increment: **3,222 checks / 14 admitted runs / 68 inspected
native PNGs**. ONE G1/M2 remains incomplete.

## Behavior

Start/end fitting uses the same orientation-aware FK method for every selected
eligible native XYZ bone. No character, bone, action, filename or index exceptions
were added. The existing Advanced equivalent-angle option is labelled **Smooth
rotation transitions**, enabled by default. Normal use remains **Preview fit →
Apply fitting → Save/export**, on the exact currently selected native animation.
Disabling the option preserves the existing authored reference-turn policy.

Complete equivalent endpoint Euler families and neighboring whole turns are
compared by the actual native Hermite path's local angular travel. A single
integral FK midpoint on the shortest spherical endpoint path is considered too;
it is accepted only when actual native travel decreases without increased
measured peak angular speed. Original joins, their native keys/tangents, the
complete attack middle, duration, timebase and protected source controls remain.
No second document, Undo stack, skeleton, bind/weight change or playback work.

The user identified HipN, but the cropped Fall values are equivalent orientations:
(-0.078132, -0.081514, -0.5721804) and
(179.9219, -179.9185, -180.5722). Matching small raw angle labels is not the goal.
The native output may retain equivalent whole-turn labels to avoid a channel jump.

The sealed original Next Sonic AttackAirLw → Mario Fall case uses 44 frames and
visible joins **5 / 35**, with only Fall frame 1 at each endpoint. Independent
matrix samples at 1/128 frame measure HipN exit travel **124.7654 → 102.5254°**
and peak speed **17.3241 → 17.1198°/frame**. Entry stays **169.2014°**: the
candidate did not improve it under the speed guard. Nineteen bones reduce travel;
all 63 bones retain or reduce measured travel and peak speed. These are local
rotation observations, not contact, style, whole-body or game acceptance.

## Bounds, receipts and compatibility

The [frozen scope and rejected experiments](ROTATION_FIT_SCOPE_2026-10-27.md)
record the numerical contract before each candidate was implemented. Candidate
travel uses analytic native XYZ angular speed, Simpson integration at at least
64 intervals and at least 16 per native frame. Candidate search is bounded to
two million evaluations; native midpoint construction adds at most three axis
keys per window. Endpoint orientation tolerance remains 0.01°. Existing pin,
floor and contact gates are unchanged. This is bounded candidate selection, not
a global optimum or a continuous-time certificate. Extreme/unrepresentable
input and excessive work refuse before Apply; cancellation and stale guards stay.

`OptimizeRotationPaths` is additive and defaults false in the Core API for legacy
callers; the native dialog explicitly enables it with the existing default option.
The receipt records `native-orientation-path-1`, selected and legacy measured
travel. Saved options must agree with the receipt, and measurements must be finite,
nonnegative and no worse than the legacy candidate. Existing algorithm identifiers
remain for the surrounding fitting/contact adapter; the new rotation subpolicy is
explicit. Old receipts stay readable. Project schema version stays 1.

The original Next engine and native editor remain intact. During ordinary transfer,
reviewed motion controls follow only their own source-authored locals. Boundary
fitting retains the attack's complete motion-position control curves; existing
explicit reviewed reference-orientation control policy remains unchanged.

## Verification

Background-only private desktops, the shared offscreen renderer and isolated
library/settings are used for all UI cases. No OS input injection, active-desktop
capture or focus stealing. Source assets and catalogues remain immutable.

Final assertions and captures are recorded in ignored
`artifacts/Fighterport-rotation44` outside the public source checkout.

| Case | Checks | Native PNGs |
| --- | ---: | ---: |
| Generic rotation paths, moving references, paired keys and failure budgets | 127 | 0 |
| Authentic per-bone independent matrix/path/peak checks | 190 | 0 |
| Existing Core boundary/contact/floor regressions | 380 | 0 |
| Independent motion controls, Mario → Snake | 628 | 0 |
| Independent motion controls, Snake → Mario | 628 | 0 |
| PAC-member ownership, same-name clips, legacy reopen | 53 | 8 |
| Source preview, fitting Apply and failed Save | 45 | 11 |
| Sonic AttackAirLw → Mario Fall, exact 5 / 35 joins | 109 | 8 |
| Snake tilt → Mario Wait1, both boundaries/floor | 287 | 8 |
| Mario aerial → Snake Fall, both boundaries | 265 | 4 |
| Batch review, native Undo and approvals | 182 | 9 |
| Verified set export | 152 | 8 |
| Native fitting/editor/Save/export/reopen | 81 | 11 |
| Unchanged contradictory floor gate | 95 | 1 |

The procedural cases independently exercise pure-axis shortest paths, coupled
180° families, full turns, both gimbal signs, sparse bind fallback, mirrored/zero/
nonuniform scales, moving-reference timebases, paired source joins, renamed bones,
storage permutations, invalid data and excessive work. Authored-turn mode remains.
Native cases cover original Next in both authentic directions, current-PAC and
standalone ownership, original middle/control preservation, Apply/Undo/Redo,
per-clip receipts/review approval, export before Save, failed/cancelled publication,
reopen and compact/wide 100%/175% text. The removed 200% layout stays removed.

Representative commands (fixture aliases identify local authorized test inputs):

```powershell
./scripts/Build.ps1 -NativeOnly -NativeDirectory <native>
./scripts/Build-Engine.ps1 -DotNet <workspace-dotnet10> -NativeDirectory <native>
dotnet build tests/ControlsAcceptance/ControlsAcceptance.csproj -c Release -p:Platform=x86 -p:OutDir=<native> --no-restore
# From engine/, using the workspace .NET 10 SDK:
dotnet build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release --no-restore
dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll <output> --rotation-path
dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll <output> --rotation-native <source-worker-json> <reference-worker-json>
dotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll <output> --boundary-fitting
# From the checkout, serialized private desktop:
./scripts/Test-BackgroundCapture.ps1 -WorkspaceAerialRecovery -Model <Mario-model> -Motion <Mario-motion> -TargetMotion <Mario-motion> -LibraryIndex <verified-index> -NativeDirectory <native> -OutputDirectory <new-output>
```

Other dedicated switches are `-WorkspaceFitReferences`, `-WorkspaceSourceFit`,
`-WorkspaceBoundaryFit`, `-WorkspaceFootRecovery`, `-WorkspaceBatchReview`,
`-WorkspaceSetExport` and `-TransitionFit`; existing moving-root/floor-rejection
options exercise the hard failure. The independent native harness uses
`RetargetAcceptance.exe --motion-controls <source-model> <target-model> <output>`.

## Development failures and limitations

The initial SO(3) cubic failed the frozen native representation bound on authentic
HipN (23.017° at 1–3; 18.593° at 1–5). It was rejected and is absent from production.
The implemented native midpoint construction is different; it makes no claim of
approximating that cubic. A branch-only prototype left HipN unchanged, motivating
the separately frozen bounded midpoint candidate. A test initially compared all
retained output times against source times, erroneously counting an inserted
sparse join as a mutation; the corrected oracle checks every original retained
key/paired side and tangent exactly. No product gate was weakened.

Physical input/DPI, human usability, universal contacts, arbitrary anatomy,
additional game adapters and actual game acceptance remain unverified. This is
an improvement to existing local XYZ FK boundary fitting, not a new IK/contact
solver or a promise that every animation/model combination is solved.

## Delivery

Source baseline: `8ec99b7c0a73c186392488f6280e29c8ac7d1626`. Native version
`2.0.0.24`, informational `2.0.0-preview.44-rotation-paths`.
Final launcher: ignored `native-final3`; executable SHA-256
`2DAC8C589CC470694E646D59B8656F6F07272FAFF37AAF038575999CE16380F5`.
Final Core SHA-256
`44CDCB7F2D19600B5B5A34933F0D16CF15407010B79F6F0A9AFD9FF6278084C3`.

Eight UI cases use `native-final2`; final aerial qualification uses `native-final3`
after an Advanced-report sentence was corrected to distinguish the nearest branch
from orientation-aware selection. The native executable is identical. Prior Core
SHA-256 is `7EA046DE6762F1C84131166A17F924F5EB77B985F878FB3BB7E23781C87CF56C`;
the only Core source change between these builds is that diagnostic sentence.
All three Core suites reran on final source. Both aerial native exports are
byte-identical SHA-256
`4D37AFCE22418215221BA2A88220600D5B3480353CDC965F98E75157DBD80C5A`.

Admitted UI folders: `members-final1`, `source-fit-final1`, `aerial-final2`,
`boundary-mario-final1`, `boundary-snake-final1`, `batch-review-final1`,
`set-export-final1`, `native-transitions-final1`, `floor-guard-final1`.
Admitted Core folders: `core-final3`, `native-math-final3`,
`core-boundary-final3`. Independent root folders: `root-mario-snake`,
`root-snake-mario`. Development probes and superseded repeat runs are excluded.

`visual-review.json` records hashes of the 68 admitted images, actually inspected
through 16 labelled sheets plus a full-size recovery view. Original/fitted models,
source preview, frame-36/39/42 recovery samples, direct Apply, compact/wide scrolling,
batch review/export and the blocked floor result were reviewed. The 175% compact
panels retain scrolling; disabled native captions remain inherited dark controls.
These observations do not certify physical interaction or game rendering.

`scripts/Run.ps1 -Check` resolves the final verified build without opening a window.
Publication uses DoctorFlux author/committer, preserved upstream notices, public
`DoctorFlux/Fighterport/main`, source only. Contributor verification remains part
of publication. Generated binaries, captures, settings and game assets stay excluded.
