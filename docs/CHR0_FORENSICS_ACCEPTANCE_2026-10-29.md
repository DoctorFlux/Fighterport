# CHR0 forensics and selective preparation — preview.62

Starting commit: `01ce42bdea068c0aab83dc7c03a9e0f854a1ad34`.
Publication remains source-only public `DoctorFlux/Fighterport/main`, with
DoctorFlux attribution. This is a bounded increment inside the ONE incomplete
G1/M2, not completion of animation compression, contact quality or game support.

## Implemented boundary

`tools/chr0_forensics.py` independently reads v4/v5 headers, all 32 flags,
dictionaries, strings, fixed/isotropic cells, six scalar codecs, paired sides,
complete sampled terminal data, metadata and every physical byte. It preserves
unknown regions as unknown. Original synthetic fixtures exercise malformed
offsets/counts, aliases, IEEE bits, terminal timing and signed codec limits.

`-NativeChr0Forensics` uses two fresh native owners without a writer, model pose,
desktop input or capture. `tools/compare_chr0_forensics.py` compares every
coefficient bit, ordering, semantic/raw flag and retained compact field against
the independent parser. An explicit arithmetic profile is mandatory for the
recorded native comparison; no numerical tolerance or automatic profile selection
is used. See the [format map](CHR0_BINARY_FORMAT.md) and
[byte findings/write-path audit](CHR0_FORENSICS_NOTES_2026-10-29.md).

Retarget preparation first retains its existing qualified precision/packing
result, then tries at most four complete compositions of previously
Dense-certified bone curves. Typed native failures restore their actual
ancestors/implicated branches. Coefficients must match the immutable selected
wire, and the complete fresh decoded result must pass cumulative native checks
against the original finished target. Only strictly smaller physical output
with non-increasing resource bytes replaces the held baseline. The scalar,
native, cancellation and watchdog limits are unchanged. No solving or diagnostic
work runs on playback ticks.

The original Next solver, complete native viewer, hierarchy/binds/inverse binds,
all weights, protected source-owned controls, native Undo and exact ordinary
Save remain. Ordinary right-click Export still has no optimizer analysis or
preparation window. Fitting/contact recipes and independent export-copy policy
boundaries are retained; no new mode, editor or project-format migration appears.

## Findings that affect validation

The current CLR `4.0.30319.42000`, 32-bit native reader matches exact dyadic
`base + q*step` rounded once to binary32 for the inspected corpus. Separate
binary32 multiply/add steps produce 16,017 exact field mismatches on the supplied
Sonic file. The explicit `exact-single-round` profile reconciles all of them;
no tolerance is substituted. Its native source projection has 7,120 rows and
2,676 paired sites, versus 7,108/2,664 under the historical separate-step model.
Physical sample counts and byte ledgers do not change. This observation does
not prove every JIT, editor or game uses that arithmetic.

The supplied Mario up-air is the primary reverse comparison, replacing the
older down-air/legacy-motion comparisons. It is CHR0 v4, 34 frames, non-looping,
13,608 bytes, SHA-256
`93559d59573ed7bd6612bdd3a2b7daa06d1df457982884e281f55139f1ebb9fc`.
The supplied Mario motion PAC is 3,329,344 bytes, SHA-256
`b1de9496061b10cad49c753bd9ea518815e76758872004381cee3aea740380cf`.
The test loads AttackAirHi from that PAC and verifies it against the standalone
file. The distinct source model container is a model, not a copy of the PAC.

The canonical source snapshot remains 13,608 bytes, SHA-256
`d360bf4f73ae084f0dc756febb287f0fb199d2a1737d396d313d4fb1c770569a`.
All 58 entry flags, nine-channel coefficient bits and complete compact terminal
data agree. Exactly 17 I12 FrameScale words change from `3cf83e0e` to `3cf83e0f`;
every other byte is identical. Their runtime significance remains unqualified.
The GIF source uses the exact supplied standalone bytes, not a claim that the
canonical snapshot is byte-identical. Both original files remain untouched.

## Measured finished-target results

Whole standalone file lengths, not header size estimates:

| Fixture / representation | Full-quality target B | Held preview.61 search B | Final B | Keys |
| --- | ---: | ---: | ---: | ---: |
| Actual Sonic down-air → Mario button, channel keys | 237,620 | 81,140 | 81,140 | 6,912 |
| Direct Sonic down-air → Mario, bone keys | 227,168 | 99,172 | 99,172 | 13,558 |
| Direct Sonic down-air → Mario, channel keys | 227,168 | 69,848 | 69,848 | 6,095 |
| Direct Sonic down-air → Mario, Interpolated | 227,168 | 115,016 | 115,016 | 9,508 |
| Supplied Mario up-air → Sonic, bone keys | 174,168 | 83,064 | 83,064 | 8,613 |
| Supplied Mario up-air → Sonic, channel keys | 174,168 | 105,816 | 105,816 | 8,729 |
| Supplied Mario up-air → Sonic, Interpolated | 174,168 | 105,816 | 105,816 | 8,729 |
| Independent selective-pulse oracle, both source schedules | 572 | 508 | 504 | 43 |

Held values for the newly supplied fixture are the unchanged established search
executed first in this operation, not an invented historical run of those new
assets. The actual button and direct fixtures have different preparation
contexts; their sizes must not be substituted for each other. Donor files are
motion/body context, not the finished-target compression quality reference.

The new search has **no additional saving in either primary authentic case**.
An older secondary up-air fixture saves 24 bytes in bone mode, but is not used
to suggest that the supplied up-air improved. The independent pulse oracle
proves selective repair can retain a required body peak and remove an unrelated
leaf peak, with exact root tuples and independent all-skin witnesses. The first
test draft selected two leaves on one limb and over-specified the first failing
bone; it was corrected to distinct native branches/actual failed ancestry before
qualification. No production tolerance changed.

The current actual button output still spends 77,340 bytes on curve payload;
36,468 bytes are extra paired rows in its I12/I6 representation. The independent
inventory finds no byte-identical stream-alias savings in that file. Other
fixtures have possible sharing, including 39,788 bytes in reverse bone mode;
these are unimplemented opportunities. New L4 proposals need original-target
continuous/native certificates, protected/source timing and terminal ownership,
and separate compatibility evidence. Neither opportunity is advertised as an
achieved saving or a global optimum.

## Commands and admitted checks

Build commands actually run used installed Visual Studio MSBuild:

```text
MSBuild BrawlCrate/BrawlCrate.csproj /restore /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<ignored-native-directory>/ /m:1 /nr:false
MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<same-directory>/ /m:1 /nr:false
python -B -m unittest discover -s tests -p "test_chr0*.py"
python -B tools/compare_chr0_forensics.py <animation> <native-dump> --scalar-arithmetic exact-single-round --output <report>
```

App compilation passes with the two existing CS1690 warnings. A first harness
compile used a DEBUG-only Flags property; the release harness now reads the
pinned private field without changing production code. The initial arithmetic
comparison failure remains recorded and excluded from passing evidence.

| Background runner case | Passing checks |
| --- | ---: |
| NativeSourceKeys, Sonic down-air → Mario | 924 |
| NativeSourceKeys, secondary Mario down-air → Sonic regression | 1,234 |
| NativeSourceKeys, secondary older Mario up-air fixture | 1,204 |
| NativeSourceKeys, supplied Mario up-air → Sonic | 961 |
| WorkspaceUseModelKeys, actual button / compact-wide / 100–175% | 52 |
| NativeAnimationOptimization | 569 |
| WorkspaceAnimationOptimization, Exact / Apply / Bounded | 193 / 265 / 14 |
| TransitionFit / WorkspaceFitReferences | 110 / 66 |
| WorkspaceNativeAnimationExport / ProjectLifecycle | 46 / 37 |
| NativeChr0Forensics, 16 real/synthetic files | 64 |

These total **5,739 passing native checks**, plus **42 Python tests**. Native
reader comparisons report zero bit differences for all 16 files, including
v4/v5, loop/non-loop and all six formats. Independent parser tests also cover
malformed input; those bytes are not fed to an unsafe native loader. The native
pipeline retains exact roots, source seeds, full original weights, supplied
hashes, native flags, stale/cancel handling, editing/Undo/Redo, export/reopen,
fitting and untouched siblings. Continuous scalar bounds remain distinct from
sampled native hierarchy/skin/derivative evidence. Existing contact gates are
not weakened by either result.

Eight workflow cases have separately measured end-to-end durations of 49.94,
62.29, 118.44, 234.68, 96.33, 88.52, 29.21 and 30.48 seconds. These include
initialization and assertions; they are not isolated reducer timings. Search
effort/budget diagnostics are retained per candidate. No isolated search timing
or speedup is claimed.

Structured retarget operation counters include the additional inner attempts.
The retained candidate's textual native report describes its earlier qualification
and can show smaller work counters; it is not the full-operation work total.
Native forensics directly corroborates curve/compact-storage metadata and flags;
it does not dump scaling-rule/user-entry values. Those raw fields are inventoried
by the independent parser and remain subject to the existing separate native
save/reopen metadata tests, not a newly claimed native metadata-decoder proof.

All 60 fresh workflow PNGs are covered by actual review or exact hash continuity
to actually inspected prior images: 57 match their requested counterparts; the
three unmatched images were opened at original detail. Compact high-text
layouts retain native viewport/actions and scrollable long text. Long paths may
scroll horizontally. This is background layout evidence, not human usability.

Local evidence is under `Fighterport-curve62`, with the original ten-file
read-only audit under `Fighterport-payload62`. Assets, settings, binaries,
captures, JSON reports and GIFs remain ignored. Source/tests/docs only are
eligible for publication.

The two new private-desktop media runs pass **1,071 / 831** renderer checks.
Their four GIFs decode to **308 frames** (87 + 87 for Sonic down-air → Mario;
67 + 67 for supplied Mario up-air → Sonic). All 40 decoded contact sheets and
18 selected full-resolution PNGs are actually inspected. The source of the
reverse GIF has exactly the provided `93559d...` hash. Cameras are level +X
orthographic, with a world Y=0 guide; roots and original model/animation bytes
are unchanged. No Fall/Wait reference is appended, and no fitting is represented.
Half-frame native samples use 30 GIF samples/second for inspection; this does
not change either CHR0 timebase.

No obvious candidate-specific missing body part or cropping was observed.
Source-versus-target body-pose differences are visible consistently in Dense
and all reduced curves, particularly the supplied up-air mid-pose; they belong
to the existing transfer, not newly proven compression correctness against the
donor. Feet meet/cross the gold guide in both comparisons. Contact/floor quality
and original transfer style remain unaccepted. Continuous scalar certificates
and native numerical evidence supply the bounded compression checks; the GIFs
do not prove them independently.

The selected package is `Fighterport-curve62/native1`; app SHA-256
`88c4036669235686f2727f7c0078b529ef22706d2f5632ad2f2f5a96ff01d7b1`,
final harness `e30b1467b536729950680f4fca3462b4320c860c6c1a07bca756efce4c15943f`.
The app/engine dependencies remain those of the qualified prior native package;
Core and the native BrawlLib codec are unchanged. Launcher resolution is checked
without starting an interactive window. Source-only publication and contributor
identity are verified separately in the local publication record.

## Remaining boundaries

The large primary file-size gap remains unresolved. This increment implements
measurement and a conservative verified search, not a complete compression
rewrite or every historical CHR0/runtime interpretation. Unusual flag masks,
FrameScale runtime use, external BrawlBox/BrawlCrate versions, MKWii issue #129,
embedded-container savings, monitor DPI, physical input, human usability,
universal contacts, Melee and actual game acceptance remain open. No archived
test report is substituted for a game test.
