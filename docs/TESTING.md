# Building and checking V2

Use Visual Studio 2022 MSBuild and the .NET Framework 4.7.2 targeting pack. This
application remains native WinForms/net472/x86. No global installers are needed
on the inspected development machine.

Restore the pinned app-local OpenTK dependency with
`scripts/Restore-Dependencies.ps1`, then build `BrawlCrate/BrawlCrate.csproj` with
`Configuration=Release`, `Platform=x86`. Upstream updater/Cargo/custom destructive
hooks are disabled in V2 by default. The product executable is
`BrawlCrate/bin/x86/Release/net472/FighterPortStudioV2.exe`.

Focused native/project tests live in `tests/NativeAcceptance`. Supply real Mario
and Snake roots through the documented test arguments/environment, never by
committing source paths or game files. Tests use unique output folders, fresh
native decoding, fresh-process project reopen and before/after hashes.

`tests/LibraryAcceptance` adds the shared source-bound catalogue, original
identity, exact repair, independent revisions, source removal and game-path
checks. See its README and `docs/ANIMATION_LIBRARY.md`. The matching desktop
runner is `tests/GuiAcceptance/Run-LibraryAcceptance.ps1`; it uses the actual
native renderer for both original and target. Run only one GUI script at a
time on an idle desktop. Future-game path configuration is tested separately
from native adapter support.

Run `tests/GuiAcceptance/Run-GuiAcceptance.ps1` with the built executable, a local
Mario source folder and a new/empty ignored output folder. See its README for
the exact command and idle desktop requirements. The native/project suite has
its own README and accepts local Mario and Snake roots.

GUI acceptance runs the actual executable and selects actual discovered native
clips in the Model Previewer. Observe a changing pose, playhead progression,
scrubbing, project save/restart and native output reopening. Internal CHR0 setters
alone are insufficient evidence. Local screenshots and detailed reports stay
under ignored `artifacts/`; final results are summarized in DEVELOPMENT_STATUS.

Inherited native resource loading, timeline and writer behavior are regression
checks. Pristine upstream contains no standalone test project at this pin; the
retained checks are its actual application builds and native fixture/GUI exercises.

Retarget checks live in `tests/RetargetAcceptance`. Build that net472/x86 harness
with `BuildProjectReferences=false` after the application, then run with the same
local Mario/Snake roots. Its README documents the command and measured tolerances.

Use Windows PowerShell 5.1 to run the actual desktop scripts. They use a small
source-only native UIA COM bridge because the older managed UIA client can expose
only Win32 proxies on current Windows builds. The bridge generates its interop
locally from Windows' installed type library using the VS Windows SDK TlbImp tool.
The product does not depend on this test bridge. Toolbar/menu commands use native accessibility actions (UIA/MSAA) on verified
owned elements, with pointer fallback; native HWND buttons support BM_CLICK. Filename and frame edits receive native character/edit
messages on their exact owned HWND, so input cannot become viewport shortcuts.

`Run-RetargetAcceptance.ps1` takes `ActualExePath`, `MarioRoot`, `SnakeRoot` and a
new/empty `Output` folder. It opens Snake, adds Mario's motion PAC, retargets
Wait1 idle with Mario's source model, scrubs/plays/saves, restarts the actual process,
exports/reopens CHR0, retargets Run using the stored donor choice, tests Undo and
a hand-supported get-up, seated SlipWait and lying DownWaitU, and exports/reopens
a new Snake motion PAC. It then creates the reverse Mario target with Snake's
get-up, seated SlipWait and face-down DownWaitD, saves/restarts and exports/
reopens CHR0. Native Floor state, changing rendered poses, layout and all
original SHA-256 hashes are checked. Wait1, Wait2 and Wait3 in both directions
are played and inspected through low-side, opposite-side and three-quarter views.
The options dialog is exercised with an Airborne retarget, Undo, Grounded,
Source contacts and Auto. Each idle has first/middle/last captures from the same
low-side angle, plus opposite-side and three-quarter views. Playback is stopped
using the actual native button text before frame selection and saving.

The native retarget suite also verifies contact geometry against BrawlLib's own
skinned vertices, foot/palm/body heights, stationary contact drift, unchanged limb
lengths, explicit Airborne mode and name-independent Auto contact detection and deterministic random scrubbing. Both native
target PAC templates preserve all their other sampled motion slots. Renamed
bone/profile tests establish that the solver operates on mapped roles. Run all
checks with `--review-only` for the requested focused scope: Wait1, quick cliff
get-up and quick cliff attack, in both directions, plus the generic/native
regressions. Omit that flag for the broader motion suite. `--reverse-only` and
`--contacts-only` isolate failures and do not establish either complete scope.

`--slow-review` instead selects Wait1 plus slow climb/attack in both directions;
`--cliff-review` selects Wait1 plus all four quick/slow climb/attack fixtures.
These scopes retain the generic/native regressions. Each quarter-frame receipt
also records source/solved/native sole heights and mapped trajectory channels.
Native skin is independently checked throughout supported and unsupported
phases. Trajectory fixtures cover proportion independence, local-axis changes,
sparse authored cubics, duplicate discontinuities and held prefixes.
An independent short traveling stance appears in the middle of unrelated
motion. It must correct bilateral source FK sole drift without stationary foot
locks, while strict source mode preserves that drift. All supplied cliff-ending
native soles must finish within 0.03 of the same plane as idle; higher held lifts
remain in separate proportion regressions.

Automatic characterization is also tested before native retarget acceptance:
real anatomy, Mixamo-style names, Unreal-style names, wholly anonymous joints
and independent limb-proportion changes. A real idle is then retargeted and
fresh-decoded with anonymous source/target bone names and unrelated model/clip
names, without supplying a semantic map. `--mapping-only` isolates these checks.
The same checks include a 17-role humanoid without a separate root, scales from
0.01 to 100, and independently constructed supported hip-bounce poses in both
directions. These numerical fixtures supplement the real native files; they are
not replacement example character assets. Wait1 also measures both soles over
every quarter frame without filtering out low-support phases, so a bouncing idle
cannot pass while both feet float in the middle of the clip.

Performance regression evidence records complete per-clip bake milliseconds,
including source-contact analysis, IK and native curve fitting. Compare complete
clips on the same host; a single cached pose is not a bake benchmark. Numerical
checks compare batched FK with sequential joint edits, Cholesky solutions with
known systems, and accelerated skin minima with all original vertices under
different poses, scales and ground normals. Nearly straight limb tests check
that moving the endpoint retains the existing knee/elbow bend plane.

`Run-LedgeAcceptance.ps1` uses one idle (Wait1), climb and attack in both
directions. `-CliffSpeed Quick` is the default; `-CliffSpeed Slow` selects the
slow pair. It captures first/quarter/middle/three-quarter/last views, including
low feet/knees at every sampled pose and an opposite-side view, against the
normal floor. Loop is disabled and playback must reach the last frame and stop
naturally. Each CHR0 is exported/reopened and each project saved/restarted.
Native tests measure inferred suspended
hand goals separately from ordinary sole/palm floor contacts. No synthetic edge
preview or animation-name condition is part of the product solve.

`RetargetAcceptance --locomotion-review` verifies run, takeoff, jump, fall and
landing in both directions, in Auto, with quarter-frame native skin and exact
source/native actor Z comparisons. Independent continuous ground/air controls
check flight against corrected FK and require restored ground support. Flight
without stored gameplay trajectory must not create floor locks. Full details
and current measurements are in RETARGETING.md. The GUI runner accepts
`-MotionSet Locomotion` and an executable inside an extracted portable package.

`scripts/Package-Release.ps1` produces a Release/x86 testing ZIP, runtime folder,
SHA-256 manifest and exact committed-source archive under ignored artifacts.
It uses an explicit runtime allowlist, excludes test/debug/game files and
standalone Python installer launchers, retains licenses and rejects an existing
destination. Clean source is required for a publishable package. `-AllowDirty`
is only for a development staging package and labels that limitation.

`Run-GameSourceAcceptance.ps1` exercises the actual game-path settings, native
roster browser and full selected roster inventory from a user-supplied WBFS.
Supply `-ActualExePath`, `-SnakeRoot`, `-ImagePath`, `-DolphinTool`, an empty
`-Output` and isolated `-LibraryRoot`. It previews the original disc model,
retargets onto the editable target, exports/reimports CHR0, restarts the saved
fighter, repairs an offline image and verifies source hashes. `-ExistingCatalogue`
reuses an already inventoried test catalogue; it does not repeat extraction.
Never overlap desktop runners or run CPU-heavy bakes during FPS measurements.

Rendering acceptance counts completed native OpenGL swaps through the passive
viewport message `0x84F0`, rather than using timeline advancement as an FPS proxy.
Messages `0x84F1`/`0x84F2` reset/read the maximum interval between completed swaps
in microseconds; frame-stall evidence complements averages.
`0x84F3` returns the bounded trace count; `0x84F4`/`0x84F5` read a trace index's
swap interval and sampled animation frame (microseconds / thousandths of a tick).
`0x84F6` reads the camera translation/rotation/scale float bits for axes 0–8.
These passive diagnostics have no timers or scene traversal of their own.
The library runner compares open/closed drawer playback; the full-game runner
also changes metadata searches during playback. Reports retain actual frame
counts, elapsed time and executable hashes. These are local samples, not a
performance guarantee for other hardware or content.

`Run-LibraryPerformance.ps1` takes `-ActualExePath`, a saved `-ProjectPath`,
`-LibraryRoot`, `-Output` and the exact displayed `-Clip` label. It compares closed,
open, paused-source and browsing playback, saving pose/swap traces, p95/p99 gaps,
clock drift and fixed-camera assertions. Optional `-PlaybackOnly` checks a saved
moving clip without opening the drawer. Float pose advances above one tick are
expected when sampling elapsed time after a late presentation; they do not mean
native keys were removed. GUI measurements exclude Stop's final editing redraw.

NativeAcceptance also tests the actual scheduler without GL: nonblocking start,
UI-thread delivery, a deliberate UI stall without queued-frame bursts, Stop and
restart. Real native motions verify elapsed subframes, delayed ticks, manual
scrub resynchronization, looping, exact end stopping and inherited integer stepping.

Preview.3 additionally compares body-only playback against the inherited complete
channel evaluation across subframes and a loop, checks immediate manual scrubbing,
limits playback text notifications, and checks repeated scheduler start/stop
resource cleanup. The full native/editor suite now contains 88 checks.

`Run-PlaybackSoak.ps1` takes the same saved-project parameters, `-Seconds`, optional
`-OpenLibrary` and `-KeepVisible`. It records ten-second playback/resource windows,
pose/UI queue and draw/present timings, actual loop/playing state and zero rig
capture/retarget bake calls. It closes its own editor on success or failure. An
unexpected Stop records its call stack separately from a presentation stall.
Do not infer FPS from elapsed timeline movement or call a stopped player a leak.
The browsing regression now requires maximum swap gaps below 50 ms, p99 below
35 ms, pose-clock drift range below 35 ms and measured library UI work below 10 ms.

The opted-in scheduler now uses the source preview's WinForms message-timer
style, with a minimum 16 ms interval. No worker posts animation callbacks and no
timer-resolution request or busy wait is used. Motion advances by elapsed native
time even when display refresh is slower than the authored animation rate.
Passive `0x84E6` metrics distinguish rig capture, baking and actual player state;
`0x84E8` reads the last Stop stack. No diagnostic traverses the scene per frame.

The user's original Snake MotionEtc reproduction superseded the earlier internal
60 FPS measurements: the user also saw stutter in the pristine native component.
An optional official PresentMon trace corroborated very few displayed frames
despite near-60 swap-call returns. VSync, front-buffer, context-style and fast
timer experiments did not resolve it and are excluded from the product.
The user confirmed the bounded message timer was much smoother on the original
motion and smooth enough for further testing on the saved Mario-to-Snake idle.
Local full-viewport presentation is approximately 33–40 FPS, **not a verified
60 FPS fix**. A 1 ms timer variant approached 59 FPS internally and visibly
regressed; it was rejected.

For this bounded-clock regression, explicitly use `-MinimumFps 30` with the soak
runner; its default remains 50 and the chosen threshold is recorded. Optional
`-PresentMonPath` points to a separately supplied official console executable
(never bundled). The runner requires at least 80% of steady-state trace presents
to be marked displayed and maximum display gaps below 100 ms. OpenGL `Other`
traces are supporting evidence alongside visual feedback, not a universal
display-rate guarantee. All trace CSVs and machine paths stay ignored.

Preview.4 adds a GPU-only Windows DXGI presentation adapter for the existing
OpenGL image. The native scene renderer, camera and curve evaluator are retained.
`0x84F8` reads whether accelerated presentation is active; `0x84F9` explicitly
samples the native screenshot route; posted `0x84FA` simulates presenter loss for
fallback testing. These probes do not run during ordinary playback.

`Run-PresentationAcceptance.ps1` takes `-ActualExePath`, `-ProjectPath`, `-Output`
and the exact `-Clip` label. It checks distinct native screenshot poses, exact
scrub restoration, live resize, minimize/restore, repeated Play/Stop, paused idle,
normal exit and unchanged project bytes. The default run also simulates loss of
the presentation backend and checks the same native pose after legacy fallback.
`-ExpectLegacy` with `FIGHTERPORT_DISABLE_DXGI=1` validates the initial fallback.

For smooth-60 acceptance use `Run-PlaybackSoak.ps1 -MinimumFps 58
-MinimumDisplayedFps 58 -PresentMonPath <official-console-executable>`, with
both the model's original MotionEtc project and its saved target-native revision.
The display check additionally requires at least 98% displayed presents,
p99 display gaps below 25 ms and maximum display gaps below 50 ms. Receipts record
actual display rate, presentation mode, fixed camera, resources and zero live
retarget/capture calls. Do not replace these checks with internal frame counts.
Native export/reopen and source-protection regressions still use NativeAcceptance.
