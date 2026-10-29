# Source-timed Use on This Model: implementation notes

Starting revision: `c02f426f6eb3df162cd1b0e24fabbf4d659a7b70`
(preview.58). The active product remains `DoctorFlux/Fighterport`, within the
single incomplete G1/M2 milestone. These notes describe the implementation audit,
not acceptance evidence or measured savings.

## Why the common action could still produce dense keys

The library's **Use on This Model** button calls `UseLibraryOnTarget`, then
`BakeWithProgress`, then the original Next bake and the selected key-layout
preparation. Temporary loaded sources, saved library sources and game-library
sources converge on this route. The button did not explicitly force Dense, but
the form's uninitialized `_retargetKeyframeMode` field selected enum value zero:
Dense. A fresh session therefore took the dense route unless the user first
changed the existing options dialog.

There was also a separate reduction restriction. The fractional master writer
can create two keys at one native time with the same value and different
incoming/outgoing tangents. These are continuous, writer-generated pairs, rather
than necessarily authored value jumps. The strict source-seeded reducer preserved
an entire channel when it encountered any pair. Consequently, choosing a sparse
source schedule could still retain every generated pair on that channel.

Neither observation means that every target curve can safely use only the
donor's knots. Native target motion can need extra keys after axis conversion,
proportion corrections and the original Next solve. The full-quality finished
target remains the quality reference.

## Session default and explicit choices

A fresh `ModelForm` now initializes `_retargetKeyframeMode` to
`SourceChannelTimes`. This changes the visible application's initial output
choice. It does not renumber the enum, change the public bake API's Dense default,
change legacy provenance defaults, or replace the internal original Next
reference. Historical receipts without a key-layout field retain their Dense
interpretation.

All explicit session choices remain available and honored:

- **Source channel keys**, the initial UI choice, seeds each corresponding target
  channel from its mapped source channel's key times.
- **Source bone keys** seeds from the source bone's combined channel schedule.
- **Interpolated keys** can reduce even a source channel keyed every frame;
  donor knots are not mandatory seeds for that mode.
- **Dense Next** retains the original complete native result. Explicit forced
  Dense reference operations also remain unchanged.

The existing dialog is now reachable through **Animation > Retarget options…**.
That command needs a target model and an idle workspace, but does not require a
selected target animation: source use can add an animation to a model-only
project. The actual library button keeps its existing name and caption. Its
tooltip and accessible description explain the chosen layout, the source details
show that layout, and successful import status identifies the result's actual
mode. No additional editor or permanent layout row is introduced.

## Scoped permission to reduce generated pairs

`FreezeQualityGeneratedPairs` takes evidence directly from the just-created
retarget result before its surviving pairs are entered in the session registry.
Permission requires the exact native rig fingerprint; a unique bone/channel/time
claim; matching entry transform flags, frame limit and loop state; and matching
ordered value/tangent bits for both sides. Equal-looking values or a bone name
alone do not establish generated provenance. Signed-zero sites, value jumps,
duplicate claims, unknown/authored pairs and protected source-owned controls
remain excluded.

Reduction groups keys by native time. A retained group preserves its complete
ordered sides. A candidate span runs from the left group's last, outgoing side
to the right group's first, incoming side. Only an eligible continuous generated
pair may disappear as a whole. Every positive-duration original interval is
compared with the proposed Hermite span; the zero-duration pair itself is skipped
only after checking its known site and bit-equal values. The adjacent interval
checks cover both one-sided derivatives.

The comparison uses exact rational input coefficients and normalized Bernstein
bounds for raw Hermite values and derivatives. An uncertain bound or exhausted
proof budget keeps keys. The native preparation boundary receives a separate
`AllowGeneratedPairReduction` permission and the exact sites. This permission does
not enable tangent refitting, select the Small file profile or relax strict
tolerances. Exact-only, protected/exact bones, mandatory frames and fitting joins
retain their existing guards. The permission is copied through internal reduction
options rather than inferred later from names or curve appearance.

## Preserved quality, ownership and work limits

Source schedules are seeds, not a promise of identical source and target key
counts. Endpoints, exact protections and necessary target repair keys remain.
Binding follows the reviewed mapping and exact native target skeleton, rather
than assuming that matching names prove equivalence or that different names prove
a source-only bone. A source-only track without a supported target binding is
not copied as an unrelated target track. Target axes or derived motion required
to preserve the checked finished result are not silently discarded.

The strict ceilings remain raw Euler and local orientation 0.01 degrees, Euler
derivative 0.05 degrees per native frame, local scale components 0.00001, and local
translation `min(0.0002, 0.0001 × L)` native units. Native bone/basis/skin geometry
uses the existing `0.0001 × L` gate; scale/translation derivatives use the
corresponding component bounds per frame. `L` is the finite positive target
bind-geometry bounding-box diagonal. Missing usable scale retains the exact
reference. Singular or sign-changing scale remains subject to the existing
exact-only fallback.

Limits remain two million animation keys, 4,096 target bones, two million scalar
proof-work units, 100,000 span attempts and 10,000 additional proof probes. Shared
native verification retains its 100,000-sample, ten-million-bone-visit and
one-billion-vertex-visit limits. Cancellation and the cumulative native budget
remain shared across preparation attempts.

The four grouped proposals use the existing precision sequence `1`, `0.5`,
`0.25`, `0.125`, without changing the strict quality ceilings. If they fail and
usable geometry, eligible generated-pair evidence and remaining work permit,
the existing fifth candidate slot tries a conservative alternative: retain every
original paired channel in full, disable generated-pair removal, and still allow
exact constant cleanup and independently certified unpaired spans. It uses the
original strict bounds, not expanded tolerances or new budgets. If this candidate
also fails, or verification exhausts the shared budget, retain the exact complete
dense reference. No failed or partially verified approximation is published.

Every proposed result still passes precise native encoding, a fresh native load,
metadata/flag/key/tangent identity checks and cumulative native pose/skin
comparison against the frozen full-quality target. Continuous scalar certificates
and finite sampled native geometry checks remain separate claims. Existing
stale-dependency checks, explicit review, independent import revision and native
Undo ownership remain in place.

This change does not alter model hierarchy, binds, inverse binds or skin weights;
retarget/fitting provenance semantics; source-owned root-control curves; native
playback; project format; or ordinary Save/export policies. No solver or
optimization work is added to playback ticks. It does not establish universal
contacts, physical-input usability, monitor-DPI behavior, Melee support or actual
game acceptance.

Qualification and observed limitations are recorded in the
[preview.59 acceptance record](SOURCE_TIMED_USE_ON_MODEL_ACCEPTANCE_2026-10-29.md):
3,209 checks / seven admitted background runs and 36 actually inspected UI PNGs.
