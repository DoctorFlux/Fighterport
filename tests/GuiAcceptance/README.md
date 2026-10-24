# Actual executable GUI acceptance

`Run-LedgeAcceptance.ps1 -MotionSet Locomotion` uses Run, JumpSquat, JumpF, Fall
and LandingAirN in both directions with the same native controls, low/angled
cameras, whole playback, native export/reopen and project restart checks.
Pass the executable inside the extracted portable package to exercise its real
runtime dependencies and working directory. It captures owned windows on
failure; a failed attempt never counts as a successful GUI acceptance.

`Run-PortableSmoke.ps1` reopens a saved fighter from a final extracted package,
checks layout, selects/scrubs the requested clip in a low camera and exits
normally without saving inspection changes. Supply `-ExpectedClips` for a known
fixture so selection waits for the complete restored list. Control reads refresh
stale owned providers and retry observed Windows UIA COM failures within bounded
waits; successful screenshots, selection and normal exit are still required.

Run this on an **idle, unlocked English Windows desktop at 100% display scaling**.
The runner moves the pointer, focuses its application windows and invokes native
controls. Do not use the keyboard or mouse during the run. Build the application
first and close other acceptance sessions.

Use **Windows PowerShell 5.1** (`powershell.exe -NoProfile -File ...`), with the
VS2022 Windows SDK/.NET Framework tools installed. The source-only native UIA
bridge generates its interop locally from the installed Windows type library.
It avoids managed UIA returning incomplete Win32 proxy trees on current Windows.
No bridge binaries enter source history or the application build.

```powershell
.\tests\GuiAcceptance\Run-GuiAcceptance.ps1 `
    -ActualExePath '.\BrawlCrate\bin\x86\Release\net472\FighterPortStudioV2.exe' `
    -MarioRoot '<local Mario asset folder>' `
    -Output '.\artifacts\gui-acceptance-run'
```

The source folder must contain the user's `FitMario00.pac` and
`FitMarioMotionEtc.pac`. Output must be new or empty and should remain under the
ignored `artifacts` directory. No game assets are generated or included in this
test's source files.

This launches the actual executable, creates/names a fighter, opens Mario, saves
a model-only project, loads the separate 297-clip motion file, selects/scrubs
Attack11, observes an advancing playhead, saves at frame12, closes both owned and
main windows, and restarts with the project argument. It checks the restored
selection/frame/paused state, captures a different Run pose, exports CHR0, imports
that output as another separate animation and scrubs it. Original source hashes
are checked before and after, including on failure.

UIAutomation identifies controls and native animation rows. Scrubbing uses real
native character/edit messages in the frame-number Edit; setting UIAutomation's range value
alone can move a thumb without raising WinForms' model-update event. Trackbar
positions are read through native messages when RangeValue is unavailable. Saved project
metadata and restart verify that the actual preview frame changed. Geometry
assertions check that the native editor menu/viewport are below the new toolbar
and playback controls are above the status bar. ToolStrip commands use native
accessibility actions on the exact toolbar HWND, or discovered visible controls.
Camera submenu commands resolve the current visible native popup directly,
avoiding stale UIA menu objects after a project restart. The fighter window is
identified by ModelForm and its title, and the name Edit by `Fighter name`,
because the ToolStripTextBox UIA ID is dynamic. File dialogs are matched by exact title and
process ownership. Filename **Edit** elements with working ValuePattern are
preferred by name and IDs1001/1148. The runner types the full path through native
character/edit messages to that exact owned HWND, verifies the readback and invokes Open/Save.
It deliberately avoids setting only ValuePattern: Windows shell dialogs can show
that text while retaining an old internal filename. It never sends Enter to an
uncertain focus target.

`gui-acceptance.json`, stdout/stderr logs, complete screenshots and viewport crops
stay in Output. The initial model-only descriptor is retained as
`model-only.snapshot.xml`; the same live project is subsequently saved with its
animations. The snapshot is checkpoint evidence, not a separately reopenable
project. Screenshots support visual review; numerical/native assertions remain
in the separate NativeAcceptance suite.

On failure, the receipt is marked failed and the application may remain open for
inspection. The runner does not forcibly terminate unrelated processes or dismiss
unexpected dialogs. It must be run to establish GUI acceptance; adding this
script alone does not claim a passing result.

The separate resize check opens an existing clean project in the actual executable:

```powershell
.\tests\GuiAcceptance\Run-LayoutAcceptance.ps1 `
    -ActualExePath '.\BrawlCrate\bin\x86\Release\net472\FighterPortStudioV2.exe' `
    -ProjectPath '<saved fighter.fpstudio>' `
    -Output '.\artifacts\layout-acceptance-run'
```

It requires the same idle desktop and a working area of at least 1336x889. It
resizes the fighter window to 1336x889, 1066x689 and 1600x1000 (the large size is
clamped to the primary working area). At each size it checks the actual native
menu, playback and viewport against the Studio toolbar/status bounds, requires a
viewport larger than 500x300 and captures the application window. It closes the
fighter and main window normally, checks that the project descriptor hash is
unchanged and writes positions, hashes and results to `layout-acceptance.json`.
It changes no timeline or asset data. Run it after other GUI acceptance sessions
have fully exited.

`Run-RetargetAcceptance.ps1` accepts the same executable and Mario/output arguments
plus `-SnakeRoot '<local Snake asset folder>'`. It runs the real Mario-to-Snake
retarget workflow beginning with Wait1 idle, checks automatic skeleton review, mapped provenance, changing rendered poses, playback,
fresh-process project restore, independent CHR0 reopen, cached-source Run retarget
Undo, hand-supported get-up, seated and lying ground-contact views, new Snake PAC
export/reopen, and reverse Snake-to-Mario get-up/seated/lying results with
save/restart/native export/reopen. It asserts the native Floor control's checked
state and captures the posed mesh with the automatic oblique camera. All discovered source-file hashes are checked on completion and
failure. Detailed native geometry/PAC checks remain in `tests/RetargetAcceptance`.

The retarget runner checks Wait1, Wait2 and Wait3 in both directions through
Low side, Opposite side and Three-quarter views, including first/middle/last
frames at the same low-side angle, plus explicit Airborne/Grounded/Source contacts/
Auto options. Toolbar/menu commands use native MSAA default actions where UIA
retains stale popup bounds. Blocking modal accessibility actions run on an STA
helper thread; all targets must belong to this test process. File/frame input
uses owned native Edit HWND messages, never global keyboard shortcuts.
Control lookup tolerates transient native accessibility timeouts. Camera
captures activate the owned window without changing its keyboard focus; the
actor-visibility assertion remains required for each ending low view.

The focused `Run-LedgeAcceptance.ps1` takes the same arguments and checks just
Wait1, CliffClimbQuick and CliffAttackQuick in each direction by default.
`-CliffSpeed Slow` selects CliffClimbSlow and CliffAttackSlow instead. It captures
first/quarter/middle/three-quarter/last poses from both three-quarter and low
feet/knees views, plus the opposite side against the ordinary flat floor. Loop
is disabled and each clip must play to its last frame and stop naturally. It
exports and independently reimports each CHR0, then saves and restarts each
target project. All discovered originals are hashed. The broader runner remains
available without requiring every idle variant for this review.

`-ResumeAfterForward` is a test recovery option for a completed forward checkpoint:
it requires the same executable hash and clip scope, unchanged originals, all three exported
forward file hashes and the recorded project restart. It retains the previous
failure in the local receipt and runs the remaining reverse direction. Ordinary
runs require a new/empty output folder. Closing an owned test project discards
only preview changes made after its explicitly verified save/reopen checkpoint.

`Run-LibraryAcceptance.ps1` tests Add Source, collapsible game/character/model
groups, original-source playback without changing the target, automatic source
resolution in both directions, native export/reimport, two saved projects,
restart, missing-source repair and removal/Undo. Supply `-ActualExePath`,
`-MarioRoot`, `-SnakeRoot`, `-Output` and isolated `-LibraryRoot`; optional
`-GamePath` also exercises the saved build path. It measures actual native
viewport swaps with the drawer open and closed.

`Run-GameSourceAcceptance.ps1` uses `-ImagePath` and `-DolphinTool` plus the
executable, Snake root, output and library-root arguments. It exercises the
real full-roster browser/index, original disc preview, automatic source reuse,
metadata searching during target rendering, native output/reopen, saved-project
restart and exact-image repair. `-ExistingCatalogue` reuses a previously
inventoried disposable test catalogue. Run these scripts with Windows
PowerShell 5.1 on an idle desktop, one GUI runner at a time. Keep CPU-heavy
regressions out of the rendering measurement interval.

`Run-LibraryPerformance.ps1` compares main-viewport playback with the library
closed/open, original source paused and full-catalogue expansion/search. Provide
the executable, saved project, isolated library root, empty output and exact
displayed clip label. `-PlaybackOnly` checks an existing moving retarget. Receipts
include actual swaps, p95/p99 intervals, sampled pose-clock drift and exact camera
transform equality. A native float-time advance above one tick after a delayed
presentation is expected; the animation must keep its authored elapsed duration.
