# Native interpolation search — preview.58 implementation notes

Starting revision: `b288f1a8dbf8da35ba1a21db6b8d4aab24baef93`.
The bounded increment remains inside the one incomplete G1/M2. Its application,
native workflow, UI and publication acceptance is recorded separately; Core
results here do not pre-approve those gates.

## Existing interpolation and the missed opportunity

CHR0 native scalar channels already use Hermite values and tangents between
stored knots. A donor or solver can produce a key on every frame without making
every one of those knots necessary to reproduce the finished target motion.
Deleting an interior key is safe only when the longer native polynomial passes
the applicable limits. Equal endpoint values alone do not establish constancy:
nonzero tangents can create fractional motion and overshoot.

The previous Core optimizer could delete redundant knots and refit endpoint
slopes, but its span search considered the farthest available endpoint and
power-of-two group counts within each 64-group window. A useful boundary at a
different position could be missed. The update improves proposal search; it
does not change native interpolation, normalize Euler turns, lower quality or
replace the original Next retargeter.

## Bounded error-guided endpoints

Core identifies as `native-hermite-rational-guided-refit-3`. After all existing
endpoint-slope proposals fail a candidate span, it can evaluate a rounded,
constrained chord proposal against every original nonzero Hermite interval at
its endpoints and quarter fractions. Normalized value and derivative residuals
identify a useful error witness. The closest interior retained native knot is
then proposed as another right endpoint, using the incoming side of a pair.

This changes only which span is tried next. The double-precision samples are
**not acceptance evidence**. Every accepted proposal still passes the complete
original-baseline rational Bernstein value and derivative certificate across
all affected original intervals. The original coefficient graph is immutable;
successive approximations never become the quality reference.

Each left endpoint receives at most four additional queued witness-guided
endpoints. The 64-group window remains. Endpoints already tried are not retried,
and a failed span is not assumed to imply failure for every shorter span.
This bounded heuristic does not prove that the best possible span, key set or
smallest native file has been found.

All witness samples and current-knot walks charge the same existing operation
budget and check cancellation. The existing maximum two million work units and
100,000 candidate spans remain. Generation still stops at 80% of the requested
work allowance, preserving the final fifth for caller codec certificates;
legacy deletion retains its existing 60% phase. Budget exhaustion can retain a
fully proved candidate or unchanged curve, never an incompletely checked fit.
The independently retained deletion-only alternative remains available to the
native coordinator for actual-byte and cumulative-quality selection.

Exact cleanup and explicitly exact source-owned motion controls do not enter
guided approximation. Mandatory fitting joins, protected contact/pin/attachment
ranges, unknown paired sides, signed-zero evidence, unsupported interpolation,
non-native times and singular/sign-changing scale retain their established
guards. A witness is not new provenance or permission to edit a protected side.
No source rig, hierarchy, rest/inverse bind or skin coefficient is modified.

## Independent constructive regression

The fixture has 17 integer frames with values `t` through frame 5 and
`5 + 0.25 × (t − 5)` afterward. It has a known continuous generated native pair
at frame 5. Unit-interval source tangents are `0.98f` before the corner and
`0.245f` after it. There are 18 original native scalar rows.

A solution specified independently of the optimizer uses straight native spans
with these four rows:

| Frame | Value | Native tangent |
| ---: | ---: | ---: |
| 0 | 0 | 1 |
| 5, incoming | 5 | 1 |
| 5, outgoing | 5 | 0.25 |
| 16 | 7.75 | 0.25 |

For each source unit interval, deviation from the corresponding straight line
is `delta × u(1 − u)(1 − 2u)`, up to sign, with
`delta = 1 − 0.98f` or `0.25 − 0.245f`. Its maximum absolute value is
`delta × sqrt(3) / 18`; maximum derivative difference is `delta` per native
frame. The larger deviations are about **0.0019245°** and **0.02°/frame**.
They satisfy the fixture's predeclared **0.005° value / 0.1° per-frame derivative**
bounds. These fixture bounds are independent of, and do not expand, product
profiles or saved tolerances.

The starting implementation was run before the change and retained rows at
`0, 2, 4, 5, 5, 16`. The new search finds the constructive four-row result:

| Same immutable fixture and bounds | Starting search | Guided search |
| --- | ---: | ---: |
| Resulting native scalar rows | 6 | 4 |
| Core refit candidate spans | 23 | 16 |
| Accepted Core refit spans | 1 | 2 |
| Total charged Core work | 1,709 | 1,770 |

The extra sample-hint work is reported, not hidden. This is scalar-row evidence,
not a measured CHR0/PAC file-size or runtime-speed claim. Fewer keys can still
require more native storage; actual encoded-byte selection remains mandatory.

## Core execution and coverage

The Release Core optimization suite passes **235 checks: 217 existing plus 18
new checks**. The new coverage establishes the independent analytic extrema and
constructive certificate, the four-row result, immutable input, varied corner
positions 6/9/10/11/12/13, shifted binary-fractional times, Exact and source-root
exclusion, unknown-pair preservation, four small shared-budget configurations
and deterministic coefficients/work receipts. Existing cancellation, authored
range/join, loop/terminal, full-turn, sparse and scale cases remain in that run.

The regression also evaluates 4,097 withheld times using a separately expanded
double polynomial. These sampled observations are reported separately from the
continuous rational scalar proof; neither substitutes for the other.

Actual command templates, from the `engine` directory:

```powershell
& $LocalDotnet build tests/FighterPort.Tests/FighterPort.Tests.csproj -c Release `
  --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false
& $LocalDotnet tests/FighterPort.Tests/bin/Release/net10.0/FighterPort.Tests.dll `
  $FreshCoreOutput --animation-optimization
```

Local evidence is under `artifacts/Fighterport-interpolation58`:
`probe-old.log`, `probe-guided.log`, `core-build1.log` and
`core-guided1/results.json`. The latter SHA-256 is
`da256ddca482ef4e23ae773f84f1dfb0fb581536be1e7f4f30cdb43b0579945b`.
The pinned local SDK is .NET 10.0.402. The build has no compiler errors; existing
AVA3001 and cached offline NU1900 vulnerability-feed warnings are recorded.
No native desktop, active-screen capture or OS input injection was used for
this Core-only execution.

## Native retarget representation and qualification boundary

The optional Interpolated keys retarget representation has a distinct native
receipt version, `interpolated-key-quality-v1`. It deliberately does not treat
every donor key time as a required target knot. The original Next Dense default
and the two explicitly source-scheduled modes retain their separate contracts.
The representation still requires the finished dense target as its reference,
exact protected source-owned controls, native encode/reopen and cumulative
pose/skin checks. Its existing retarget-specific limits are distinct from the
standalone optimizer's saved export policies; they are not silently widened.

The native receipt validator binds Interpolated mode to that distinct version;
legacy source-schedule receipts cannot masquerade as interpolation evidence.
Project provenance also checks mode/receipt agreement, retains dense statistics
as historical evidence, and clears current contact-quality claims for changed
curves. Core's search revision and the native representation's receipt revision
identify different responsibilities, not interchangeable acceptance evidence.

The caller's complete serialization, fresh native evaluation and collective
hierarchy/skin checks remain required after scalar reduction. Finite native
sampling does not prove continuous full-model or contact correctness. No
universal contact quality, new runtime/adapter compatibility, physical input,
monitor DPI, Melee or actual game acceptance follows from these Core checks.
