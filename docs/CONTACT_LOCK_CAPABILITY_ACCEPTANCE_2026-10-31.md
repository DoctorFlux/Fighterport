# Preview.65.5 — established contact locks and terminal arms

Starting commit: **a3e74f44f05b0c84e5f9023fc5364f99d1b4ec04**.
Current Fighterport checkout; public DoctorFlux/Fighterport/main. This is a
bounded increment inside the ONE incomplete G1/M2. Preview.66 finite static
ledges remains queued. No character, action, filename, hash or bone-index
exceptions participate in the implementation.

## Contact diagnosis and correction

The preceding Peach → Fox Wait1 failure was **0.01272920** native units of
stationary-marker drift against the unchanged **0.00596133** bound. An additional
read-only oracle establishes **0.01272866** in the raw solver itself, with the
worst decoded witness at frame **75.25**, role **pelvis**. The largest failing
marker was body-classified, not a foot marker. This is not primarily a codec or
native interpolation error, and no CHR0 tolerance was relaxed to mask it.

Established body markers previously had weight 1 while end contacts had weight
40; body planar stop checks allowed 1% of limb length, exceeding the acceptance
contract. The shared coupled solver now raises body locking priority smoothly
from its existing soft weight to the end-contact priority as a lock establishes
(0.8–0.99 confidence). Established planar stop checks reserve half the existing
0.001-limb positional bound. End-only escalation also considers planar drift,
rather than floor height alone. Soft establishment/release remains weighted.

A new unequal procedural fixture caught a second issue: final two-bone end
projection could reduce foot error while increasing an already established body
marker's error. That optional projection is now transactional and retained only
when it does not increase the worst established positional residual. Otherwise
the jointly solved pose is restored. This applies to all active marker roles;
no contacts or dependency paths are removed. Intended end orientations remain
constrained. The same 180 coupled iterations, damping, pose prior and bounded
search remain; the fix does not increase an operation budget.

Policy `coupled-established-locks-1` is recorded in the existing complete hashed
support JSON, alongside unavailable end effectors. Existing envelope/version and
legacy reads remain; no project-format migration or invented legacy timeline is
introduced. No new work runs on playback ticks.

## Mapping diagnosis and capability handling

The authentic affected arm has no distinct wrist joint. Its only descendant of
the forearm has exactly zero local translation and the same elbow pivot. Mapping
that helper as a hand would create a zero-length IK segment. The model's native
hierarchy, helper, transforms and skin remain intact.

The shared characterization path now distinguishes an unassigned distinct wrist
from a genuinely terminal forearm. The latter requires existing upper/lower-arm
bindings, a valid nonzero ancestral segment, no hand binding, and **exact zero
local translation for every descendant** of the forearm. There is no proximity
threshold for discarding distal joints: even a tiny nonzero offset still needs
mapping review. Names can still propose ordinary roles, but no new model/bone
aliases were added. Exact known absence is capability information, not an
ambiguous-mapping warning that forces an impossible assignment.

For a proven terminal arm, retargeting keeps mapped upper-arm/forearm FK and
uses the common measured elbow span for that side's scale ratio. Only the
unavailable source/target hand IK goal is omitted. There is no fabricated wrist,
hand curve, skin patch or hand contact. Existing feet, other hands and body
surfaces retain normal contact processing; missing a hand no longer disables
all floor analysis. Suspended support can only use genuinely available common
hands. Direct IK requests for an unavailable target end fail explicitly.

The existing Skeleton Mapping evidence row and source-key review explain the
limitation. Distinct unbound descendants still require review; explicitly mapping
a coincident helper still fails the zero-length check. Profiles remain reviewed
bindings/geometry, not model-specific numerical overrides. The new capability
supports partial-arm retargeting; it does not claim missing-hand contacts work.

## Final qualification

Ignored local evidence: parent-workspace `artifacts/Fighterport-contact655`.
Final production package: `native1`, build9,
`2.0.0-preview.65.5-contact-capabilities`.

| Regression | Passing assertions |
|---|---:|
| Original terminal-arm and simultaneous five-influence contact fixtures | 100 |
| Existing local/fractional precision | 1,216 |
| Source-local motion controls, two actual model inputs | 628 |
| Coordinate and unequal-axis regression | 94 |
| Bounded native bake, cancellation and contradictions | 19 |
| Actual library Use, both source-key modes, Link ↔ Samus | 104 |
| Actual library Use, both source-key modes, Peach → Fox | 52 |
| Three actual tilt/own-Wait1/fitting/Undo/save workflows, both modes | 222 |

Total **2,435 passing assertions**: **2,383 fresh final-build** assertions plus
52 Peach → Fox UI assertions from build8 with explicit unchanged-path continuity.
Between build8 and build9 only terminal-arm absence proof/diagnostics changed:
full rigs with both hand roles return before that proof. Both actual Peach
source-key files were independently revalidated against the final build9 solver.
Both partial-arm UI directions were rerun fresh after the stricter proof change.

The procedural corpus covers both terminal-arm sides/directions, opaque names,
parent-valid storage permutations, unequal scales/local axes, exact coincident
helpers, tiny distinct descendants requiring review, and zero-length mapping
rejection. Simultaneous body/foot tests retain every one of five influences,
protected root locals, scales and end orientations. The previously failing large
fixture now measures 0.00021294 drift and 0.00025405 penetration against a
0.0144 bound. Small/permuted variants pass the same relative bounds.

All eight exact original **Wait1** directions now bake, serialize, freshly reopen
and pass the complete native/contact oracle. No alternate idle, fit or appended
pose is used in this idle corpus.

| Direction | Frames | Native witnesses | Maximum planar lock drift | Bound |
|---|---:|---:|---:|---:|
| Peach → Fox | 101 | 1,101 | 0.00296952 | 0.00596133 |
| Fox → Peach | 101 | 1,101 | 0.00124867 | 0.00787171 |
| Link → Samus | 121 | 1,321 | 0.00069788 | 0.01066523 |
| Samus → Link | 137 | 1,497 | 0.00070389 | 0.00818457 |
| Captain Falcon → Luigi | 61 | 661 | 0.00039939 | 0.00463848 |
| Luigi → Captain Falcon | 61 | 661 | 0.00078162 | 0.01012114 |
| Link → Marth | 121 | 1,321 | 0.00060946 | 0.00947049 |
| Marth → Link | 71 | 771 | 0.00082506 | 0.00818457 |

Six additional final native oracles cover the **actual source-key exports** for
Peach → Fox and Link ↔ Samus, both timing options. They pass unchanged full-skin,
root and contact limits; observed drift matches the corresponding dense rows.
Their independently measured support orientation errors remain below 0.011°
against the unchanged 0.25° contact bound. Exact source-owned control coefficients
and flags are additionally checked during actual review/import. Root-local
floating observations are zero except 5.96e-8 for Peach → Fox; this tiny observed
roundoff is not described as bit-identical quaternion evaluation.

The eight master oracles cover every eighth frame plus withheld .137/.371/.623
times, original solved anchors, terminal metadata, actual native MDL0 bone and
skin evaluation with all influences. The actual source-key reviews add continuous
scalar certificates and cumulative native bone/mesh measurements, including
adaptive witnesses. Finite geometry sampling is not a continuous-time proof.
All original native source/model bytes stay unchanged. Root/bind/weight gates,
0.025° master orientation, 0.0002 translation, 0.00001 scale and reviewed contact
0.001-limb/0.25° limits remain. Neither saving nor optimization policy changes.

Actual UI checks cover exact library source/costume ownership, explicit source-key
review, model-only target import, protected roots, all weights/binds, selected
native member, export/reopen, project Save/reopen and original source immutability.
The three original tilt directions additionally pass current fitting to their
own Wait1 first frame, Undo/Redo and protected attack sections. No second Undo
stack, editor replacement or new UI panel is introduced.

Eight fresh PNGs were directly inspected: both terminal-arm mapping/pose paths
and Peach → Fox's first/frame-15 poses (`visual-review9.json`). The mapping row
leaves the unavailable hand empty with explicit evidence; native meshes/floor
and the selected results remain visible. Other generated captures are not counted
as directly inspected. Whole-duration contact quality is measured separately.
All UI runs use private desktops/shared offscreen capture, without input injection
or screen copying. Local result page: `idle-results9.html`.

## Commands, source and failures retained

Actual commands, with local private paths abbreviated:

```powershell
MSBuild.exe BrawlCrate/BrawlCrate.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<native1>/ /m:1 /nr:false
MSBuild.exe tests/RetargetAcceptance/RetargetAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<native1>/ /m:1 /nr:false
MSBuild.exe tests/ControlsAcceptance/ControlsAcceptance.csproj /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<native1>/ /m:1 /nr:false
RetargetAcceptance.exe --mapping-audit <original-model> <output>
RetargetAcceptance.exe --terminal-contacts <procedural9>
RetargetAcceptance.exe --local-precision <precision9>
RetargetAcceptance.exe --coordinate-frames <coordinates9>
RetargetAcceptance.exe --adaptive-budget <budget9>
RetargetAcceptance.exe --motion-controls <source-model> <target-model> <roots9>
RetargetAcceptance.exe --native-pair-fixture <source-model> <target-model> <original-motion> Wait1 <manifest>
RetargetAcceptance.exe --bake-probe <source-model> <target-model> <original-motion> Wait1 <output>
RetargetAcceptance.exe --verify-trajectory <source-model> <target-model> <original-motion> Wait1 <actual-export> <oracle>
scripts/Test-BackgroundCapture.ps1 -Model <target> -Motion <original-motion> -RetargetSource <source> -Clip Wait1 -AuthenticFixtureManifest <manifest> -WorkspaceAuthenticLibraryFloat -RetargetTrajectoryCase -NativeDirectory <native1> -OutputDirectory <output>
# Tilt regressions also supply -ReviewedRecoveryCase and the exact -TargetMotion.
git diff --check
```

Production build retains two pre-existing CS1690 warnings; test builds have
zero warnings/errors. Production executable SHA256:
`6BFBA755BA836422C550198C26659A513CE2CD50DA094877ECA6F9FAA16F80E8`.
Unchanged BrawlLib:
`D930DBF9DE21F1CEF7F2606A181F4BB66EA830D529F940B52B116A0E8038F458`.

Changed code: shared contact solver/preparation, characterization/partial-arm
capabilities and their native surface/curve/suspended consumers; existing support
JSON diagnostics; version; original procedural and read-only audit/oracle tests.
No MDL0 writer, bind/skin data, shared codec or original source asset changes.

Retained failures include the raw baseline, build6 unequal-contact projection
failure (0.0225582 > 0.0144), and the initial diagnostic variable-name compile
error. Build7 projection guarding passes; final build9 retains that fix. One root
test invocation mistakenly supplied a motion PAC as its target model and was
rejected; the correct two-model run passes all 628 checks. One Sonic tilt call
supplied the motion container against a manifest pinning the exact standalone
original member; identity checking correctly refused it. The exact previously
pinned member hash was restored, and all three fresh `tilt10` runs pass. No fixture
hash, threshold or failure assertion was relaxed to obtain acceptance.

## Boundaries

This qualifies the affected Brawl idle transfers and retained regressions, not
every anatomy/model/animation/costume. A nonexistent hand still has no two-bone
hand IK/contact capability; unfamiliar distinct/ambiguous joints require review.
Only currently supported humanoid anatomy/native adapters are covered. Broad
clearance/style, Sonic's earlier low-body intersection, the complete 18-case
contact corpus, finite/moving ledges, props, other actors, Melee, physical input,
monitor DPI, human usability, external-editor/runtime and actual game acceptance
remain open. Shared code has no identity-specific branches or hidden overrides.
