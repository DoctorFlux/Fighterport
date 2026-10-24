# Simpler native workspace — 24 October 2026

Preview.15 (`2.0.0-preview.15-simple-workspace`) implements the first bounded
workspace increment in PUBLIC DoctorFlux/Fighterport. It starts from public
main `6bab2084b2bbc07eed48a3ab3a97c8322d988b95`, the imported preview.14 source.
The user's explicit implementation request authorizes this increment; embedded
handoff instructions in the supplied requirements are reference only.

**163 scoped native checks passed and all 25 final PNGs were actually inspected.**
These are fresh runs of this checkout. They do not include older checkpoint
counts, a full engine-suite pass, general retargeting quality or game acceptance.
M2 remains the one incomplete milestone. G1's first mechanical increment is
accepted within the boundaries below; first-time-user and physical input/DPI
qualification remain in the same workspace substep.

## Implemented behavior

The Source, Target, Preview and fix, and Export stage bar and compact task panel
wrap the entire existing native editor. Its original ModelPanel, menus, resource/
animation/bone lists, materials/shaders, camera, gizmos, transform fields, autokeys,
timeline, playback, interpolation and Undo stay together in the same native
control instance. This is an outer WinForms layout, with no replacement renderer
or second authoring document. Additional existing Fighter commands remain in
More tools and the Fighter menu.

The existing source-bound library is a left drawer. Its real search/tree and
read-only original-model audition remain available. The task panel collapses
when the drawer would crowd the editor; Focus editor hides both task areas.
Stage, drawer and focus navigation preserve the exact target, selected native
animation revision and frame. A target can open and use existing-rig controls
without any donor clip. The current operation and destination identify the
implemented Brawl route; other games are not offered as supported destinations.

Preview transfer calls the existing library-to-target original Next
`bind-world-fk-body-contact-ik-v7` route, with its measured progress, cancellation,
independent imported results, exact result identity and stale-dependency guards.
No retargeter, engine, BrawlLib, solver, mapping policy or numeric bound changed.
Native pin and curve tools continue editing existing target channels. Source
timing / batch prepares source-derived results through the existing review tool;
it does not promise an edit of the already selected target curve.

Export result writes the selected owned native CHR0 through the existing
protected-path writer and fresh decode verification. It checks the current target
rig fingerprint and blocks advanced review drafts. Other native resource exports
remain in native menus with their own qualification boundaries. Project reopen
returns an existing animation to Preview and fix and displays its saved source
link; it does not label that link an available catalogue selection until the
existing library resolves and verifies it.

`scripts/Run.ps1` launches a complete native build. `-Check` only resolves the
executable. Optional ignored `.local/development-launch.json` can select a
development NativeDirectory; no private path or machine setting is published.

## Builds and commands

Windows x64, Visual Studio 2022 Community MSBuild and .NET SDK 10.0.402 were used.
The native app and harness target net472/x86; the isolated owned engine/CLI target
.NET 10/win-x64 and the native worker remains net472. Dependencies were restored
from the existing pinned cache. OpenTK.dll was verified against SHA256
`931E38B47B824C316A55B5A54D55650C978CA90DDE1DAEA02299EDDD94E719C0`.

The commands below reproduce the actual invocations with fixture/output/tool
locations represented by variables. Private input paths are only in local receipts.

```powershell
# Run from the public Fighterport checkout; use absolute local locations.
& scripts/Build.ps1 -DotNet $dotnet10 -NativeDirectory $native
& $msbuild tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build `
  /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false `
  "/p:OutDir=$native/" /nologo /v:minimal

& scripts/Test-BackgroundCapture.ps1 -SimpleWorkspace -Clip Wait1 `
  -Model $snakeModel -Motion $marioMotion -RetargetSource $marioModel `
  -NativeDirectory $native -OutputDirectory $marioToSnakeOutput
& scripts/Test-BackgroundCapture.ps1 -SimpleWorkspace -Clip Wait1 `
  -Model $marioModel -Motion $snakeMotion -RetargetSource $snakeModel `
  -NativeDirectory $native -OutputDirectory $snakeToMarioOutput
& scripts/Test-BackgroundCapture.ps1 -Model $proceduralModel `
  -Motion $proceduralMotion -NativeDirectory $native `
  -OutputDirectory $proceduralOutput

& scripts/Run.ps1 -NativeDirectory $native -Check
```

Full native/engine build succeeded. The final app/harness rebuild after all UI
and harness changes also succeeded; no warnings/errors were reported. Retained
local logs are `fighterport-workspace15-build-r3.log` and
`fighterport-workspace15-harness-build-r6.log` under the enclosing workspace's
ignored artifacts directory. The later rebuild did not modify engine sources.
Both explicit and configured launcher paths passed Check; a separate visible
launch was not used as physical-input acceptance.

## Fresh acceptance

| Final run under local artifacts/viewer-parity | Passed checks | Inspected PNGs |
| --- | ---: | ---: |
| Fighterport-workspace15-mario-to-snake-r3 | 61 | 11 |
| Fighterport-workspace15-snake-to-mario-r1 | 61 | 11 |
| Fighterport-workspace15-original-r1 | 41 | 3 |
| Total for this increment | 163 | 25 |

All three final receipt.json files report Passed=true, PrivateDesktop=true,
ActiveDesktopWindows=false, OSInputInjected=false and ScreenCopied=false.
Captures used the existing shared/offscreen viewport and invisible private
desktop, sequentially through the paced low-priority wrapper, with pause/cancel/
resume controls. No active desktop switch, input injection or CopyFromScreen
was used. Harness calls to real native controls/pointer handlers are distinct
from physical mouse/keyboard interaction.

Each direction exercises actual empty startup, model-only target, hierarchical
source selection and original audition, 1320/1600 layouts, all four stages and
Focus editor. The full native editor and its controls retain object identity.
The actual new Preview transfer button produces output equal in every native
key, tangent, flag, name and timebase to a separately prepared original Next
bake from the authentic donor/model pair. This is route-equivalence evidence,
not an independent proof that the original algorithm produces ideal motion.

Repeated same-named transfers create and select separate exact revisions. Native
list refresh and one import Undo restore the intended first revision. Actual
native playback advances the prepared result and Stop preserves the selected
clip/frame; retarget capture/bake diagnostic counts remain unchanged. The local
playback receipts recorded advancement from frame 1 to 9 in 349 ms and 273 ms,
respectively. This is bounded playback-continuity evidence, not a 60 FPS benchmark
or a general allocation/worker audit.

Real native control edits author TopN translation X +0.5 and Y rotation 15 degrees,
with Undo/Redo. The original transform field creates a separate later key.
The actual interpolation field authors tangent +0.125, checked against an
independent subframe Hermite expectation and through Undo/Redo. The shared UI
export route freshly decodes with all edited native content intact; native
project and actual viewer reopen retain the exact animation, control identity
and saved original Next source provenance. The automated case supplies the
destination to the shared export method; the OS Save dialog was not physically
operated.

Cancellation publishes no result. Independently changed settings, source and
target reject stale publication while preserving accepted results and exact
selection. Serialized target-model bytes are equal before these probes and
after each restored probe, as well as at completion. Hierarchy, authentic binds,
helpers, skin weights and all original supplied input-container hashes remain
unchanged. No original is overwritten.

The original procedural native-controls case adds transport/startup, arbitrary
existing-bone pins, independent expected rotations/translations, structural soft
pins, nonzero untouched rest axes, reviewed rig fingerprints, real native pointer
handler dispatch, stale gesture rejection, mixed native/pin Undo history and
project/native persistence. These are the existing regression's 41 actual checks;
they do not qualify every native command or new platform support.

## Fixtures and visual review

Private supplied FitMario00/FitMarioMotionEtc and FitSnake00/FitSnakeMotionEtc
resources were available for both Wait1 directions. Their model/motion pairing
was explicitly registered for the test; game-build membership was not inferred.
Their SHA256 input identities are, respectively:

| Fixture | SHA256 |
| --- | --- |
| Mario model | E73CC6F7C00CEBE5587BDA20BD7484D9DC49FB32FFDDB5663C0284FC16FE62A6 |
| Mario motion | AFA97C15732854F8798A93BC52BDC833CD020CD4711D8ECF68228175F4FC3288 |
| Snake model | 478C9031B027A1F3C8390178A42FDB96E0D9C807F0B0CC0AA6D36E155B12B004 |
| Snake motion | 3F374F594491F0C166E298F7E58A74E75F26934D0F8EFC67DAD107A09E8C03AA |

Original procedural original.mdl0/original.chr0 fixtures were also available
locally. All fixtures, native references, projects, binaries, logs and captures
remain ignored and are not distributed with source.

For each direction, actually opened images are workspace-empty, model-only,
source-audition, source-1320, source-1600, retarget-progress, focus-editor,
transfer-result, native-edit, interpolation and reopened PNGs. The procedural
images are native-model-pins, native-dark-controls and native-startup. Review
confirmed real model rendering, compact actions, retained lists/transforms/
timeline, source/target distinction, responsive task collapse, selected native
gizmo/fields, actual curve/tangent and restored source context. Disabled native
dark-theme button captions have low contrast. Images show particular poses and
layouts, not full-motion, every native menu, physical input/DPI or gameplay.
Local reviewed-image receipts retain each actual file hash and observation.

## Development failures and limits

Earlier development output directories are retained separately and are not
acceptance. The first Mario run stopped because its curve test assumed a second
root translation key existed. The harness now authors that key through the real
native transform field before testing interpolation. An oversized More tools
button found by actual image review was fixed by compact button sizing.

The second Mario run exposed a target-rename probe whose native name setter left
serialization dirty even after restoring the text. The probe now changes the
actual selected target dependency, restores the exact native target/clip/frame,
and checks strict model-byte equality before and after every probe. The original
invariant was retained; no acceptance tolerance was weakened. Final r3 and reverse
r1 pass these stronger intermediate checks.

This increment adds no engine numerical change, format reader/writer, game
adapter, container repacker or runtime/build evidence. Full animation-quality
gates remain independent. Advanced 96.2 Apply and prior contact/release failures
stay unresolved; no full-suite pass is claimed. Physical input/DPI, arbitrary
screen sizes, every native expert command and first-time-user task success are
unverified. See [the sole current queue](plans/current.md) for the remaining
bounded workspace task and [MASTER_PLAN](MASTER_PLAN.md) for queued contacts,
context, layers, slots, destination adapters and game qualification.
