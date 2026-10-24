# Contact trajectory protection and custom slots — preview.13

This is the same incomplete M2 integration slice. Local app:
`artifacts/next-preview13/native/FighterPortStudioV2.exe`, version
`2.0.0-preview.13-contact-slots`. Outputs, game fixtures and evidence stay ignored.
The original Next `bind-world-fk-body-contact-ik-v7` remains unchanged and default.
Native dark controls, the standard startup viewer, materials/shaders, lighting,
camera, timeline and the shared fitting `ModelPanel` renderer remain in use.

## Supported contact-aware transition operation

**Fit / Transitions... > Preserve existing contact trajectories** protects
existing support while fitting unprotected bones. The new owned Core receipt is
`contact-trajectory-preserve-1`. It preserves ALL original S/R/T curves on each
affected bone's complete ancestor path for the whole clip, including duplicate
sides, signed zero, native flags, subframes and the loop seam. Endpoint matching
excludes these bones; the review lists every exclusion before independent import.
An entirely protected mask rejects with an exact conflict. Existing duration,
protected middle curves, source snapshots, saved options, stale-input guards and
single native import Undo/Redo continue to use preview.12's implementation.

For authored bone/soft-pin contacts, Core derives those paths from the existing
contact records, retaining their intervals, anchors and frozen follow-animation
references independently in the new animation state. Selectable arbitrary FK
pins remain editor controls; they do not silently become physical contacts.
Stale contact rig revisions, unknown dependencies and surface/companion contacts
without a complete dependency adapter reject rather than losing constraints.

For original Next results, the native adapter checks the exact target rig and
reviewed retarget mapping, then reuses the existing native support regions. It
protects every positive skin influence and its ancestors plus supported effector
orientation paths. Because legacy receipts retain aggregate contact counts, not
the full support timeline, it conservatively protects both relevant sides for
the whole clip. Body/unknown-lock support can protect most or all of a rig and
leave no eligible fit. No character, action, filename or hash exception is added.

This preserves the CURRENT original trajectories. It does not establish that an
earlier retarget or subsequent manual edit meets a stationary anchor, ground
clearance, friction, joint limits or a game's transition requirements. Baseline
errors remain baseline errors. It does not infer new contacts, moving support
frames, foot releases, airborne exits, state boundaries or a coupled IK solution.
Those operations still need the full support context and their own acceptance.
The retained advanced 96.2 Apply gates are unchanged and remain blocked.

## Custom animation slots

**Fighter > Animation Slots / Assign to State...** opens a native dark table.
Add a custom required or optional slot, select an exact registered native clip,
and use Assign / Review. Assignments store the native resource key, model hash
and animation hash. Same-named clips are not substituted. Missing resources,
changed models and later pin/curve edits produce explicit review states.

Lock / Unlock protects the assignment. Assign, Clear and Remove reject a locked
slot until it is explicitly unlocked. Add, assign/review, clear, lock/unlock and
remove each have native Undo/Redo and project persistence. Open Assigned in
Viewer selects its exact animation in the existing model previewer. Slot state
does not bake/rewrite an animation or invoke a playback solver.

Native project snapshots retain slot metadata. Character workspace commits also
synchronize custom slots to Core `SlotAssignment` records using native clip IDs;
independent target copies retain their own slot arrays. Existing target-owned
slots remain intact. Stale/missing assignments keep their review status/resource
identity and cannot masquerade as a replacement game binding.

These are custom editor intentions, not verified Brawl or future game runtime
slot IDs. Verified profiles, aliases/fallbacks, Fill Missing, drag/drop, move/state
phase semantics, batch automatic assignment and game build manifests remain open.
No new reader, writer, repacker, adapter or game acceptance is introduced.

## Qualification

Final qualification passes **441 native + 266 focused engine = 707 checks**.
All **59 final PNGs were opened and actually inspected**. Evidence directories
below are under ignored `artifacts/viewer-parity/`, prefixed `next-preview13-`.
Each native directory contains `receipt.json`; focused engine directories contain
`results.json`. Failed/superseded development runs are excluded from all totals.

| Final native directory | Checks | Inspected PNGs |
| --- | ---: | ---: |
| retarget-mario-to-snake-final-r3 | 29 | 6 |
| retarget-snake-to-mario-final-r3 | 29 | 6 |
| contact-mario-to-snake-r4 | 62 | 7 |
| contact-snake-to-mario-final-r3 | 62 | 7 |
| transition-original-final-r3 | 78 | 10 |
| transition-mario-final-r3 | 78 | 10 |
| transition-snake-final-r3 | 78 | 10 |
| workspace-original-final-r4 | 25 | 3 |

Focused engine directories are `engine-contact-slots-r1` (18),
`engine-boundary-r1` (33), `engine-controls-r2` (64), `engine-editing-r1` (76),
`engine-native-r1` (32), `engine-receipts-r1` (9), and `engine-workspace-r1` (34).
The new independent checks cover fixed contact world matrices at fractional
times, exact original curve objects, free-branch endpoint values, fully protected
conflicts, stale rigs, missing dependencies, no unidentified support promotion,
slot uniqueness, missing-resource identity, target-owned slot retention, foreign
scene rejection and project persistence.

Both ordinary retarget runs compare the actual native UI and library results
against original Next output, then verify moved/rotated pin keys, Undo/Redo,
fresh CHR0/project reopening and the FK-only support guard. The isolated contact
runs use saved original Next projects. They check all protected native key bits,
tangents, duplicate sides and flags, plus all captured foot-region skin points at
17 independent times. A test-authored 12-degree free-bone reference verifies
that the unprotected branch can still fit; it is not a real state-transition
quality fixture. The saved Mario-to-Snake input is byte-identical to the fresh
final original Next library result. No manual tuning or character exception is
used to make those checks pass.

Each contact run then imports/Undo/Redoes the independent result, fresh-decodes
its protected native paths, saves custom slots and reopens the combined native
project. The complete existing pin/FK/IK/pole/native interpolation workflow runs
on that contact-fitted CHR0 itself. The three direct fitting runs likewise test
slots and pin movement, rotation, scale, native tangent fields and independent
Hermite subframes on their fitted outputs. Existing source/bind/weight bytes
remain immutable. The workspace run reads actual committed Core scene JSON and
all three independent native packages to prove custom slot/clip/protection
persistence, then opens target B in the actual native viewer without substituting
target A's edited animation.

Actual image review confirms dark native windows, textured Mario/Snake rendering,
the visible preservation option and exclusion report, locked slot assignments,
native gizmos/interpolation, saved fit settings and independent target opening.
Deliberate large gizmo scale tests can crop the figure; these images do not rate
camera parity or whole-sequence motion/contact quality. The original fixture is
the untextured procedural body. All final native logs are free of unhandled
exceptions and report private desktops with no active-desktop windows, OS input
injection or screen copying.

Development failures remain recorded locally. A new reflection-based native
comparison helper was invalid; it was removed in favor of independent direct
native-bit comparisons. The first engine native-control invocation used a
procedural clip without Wait1; it was rerun with the supplied Snake fixture.
The workspace JSON test needed to read the Framework serializer's ArrayList
instead of casting to object[]. The isolated saved-project fixture now explicitly
selects its exact native retarget result before fitting.

One combined private-desktop stress run, after repeated bake/reopen/modal
operations, logged `Graphics.FromHdcInternal` OutOfMemory during Form painting.
It was cancelled through the capture control file; no user desktop was accessed.
The cause was not established or fixed. Contact acceptance now runs from saved
projects in separate clean processes and passes both directions, but repeated
combined modal stress stability remains unaccepted. These passing scopes are not
a blanket rendering/stress or physical-input claim. No contact/retarget numerical
solver or quality gate was relaxed to obtain acceptance.

Ignored `artifacts/next-preview13/qualification.json` seals committed source,
receipts, actual reviewed images, build logs and nine binary hashes. The native
app/engine build log is `build-final.log`; the latest test harness log is
`harness-build-final.log`, and focused engine test build log is
`engine-tests-build.log`. Only the workspace test reader changed after the other
native scopes passed; that affected scope was rebuilt and rerun separately.
This is source-update qualification, not a binary release or game result. M2 and
the complete standalone integration remain incomplete.

## Commands and verification boundaries

Commands run from the outer workspace with Next at `.local/next-main`. Fixture
variables represent local procedural or supplied Mario/Snake inputs. No private
input path or commercial resource is published.

```powershell
& .local/next-main/scripts/Build.ps1 -DotNet $dotnet -NativeDirectory $out
& $msbuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj `
  /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$out"
& .local/next-main/scripts/Test-BackgroundCapture.ps1 `
  -Model $target -Motion $motion -RetargetSource $sourceModel `
  -NativeDirectory $out -OutputDirectory $newRetargetDirectory
& .local/next-main/scripts/Test-BackgroundCapture.ps1 `
  -Model $target -Motion $motion -ContactTransitionsProject $savedNativeProject `
  -NativeDirectory $out -OutputDirectory $newContactDirectory
```

The same background script runs direct fitted animation checks with TransitionFit
and native/Core workspace checks with CharacterWorkspace. Each uses a fresh
directory and a separate private-desktop process. Engine scopes are
`--contact-slots`, `--boundary-fitting`, `--viewer-controls --native-controls
<native-Snake-project>`, `--viewer-editing`, `--viewer-native`,
`--receipt-compatibility` and `--workspace-only <native-Snake-project>`.

Background captures remain serialized, paced and below normal priority. They use
the actual model renderer and do not activate the desktop, inject OS input or
read screen pixels. Physical input, DPI, full motion/contact quality, automatic
generalization, exact old-reference raster parity and game results are unverified.
The full engine suite is not claimed passing. Native builds have no warnings;
the test-only legacy Avalonia renderer retains its existing AVA3001 warning.
