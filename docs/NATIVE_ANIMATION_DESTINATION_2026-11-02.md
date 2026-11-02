# Native animation destinations — bounded acceptance

Starting commit: `8437ab3f4bd28925da60cabdb7dffb80258b1887`.
Active product: public `DoctorFlux/Fighterport/main`. The original Next
retargeter, complete BrawlCrate Model Previewer, native Undo, source-local
root policy, authentic binds and all skin weights are unchanged.

## Implemented route

The registered native game adapter now reports Melee native motion writing.
An added destination-action export contract accepts an exact member from the
target project's owned Melee action archive. A result without an existing
action association must select that member explicitly; the dialog has no
default selection. This includes independently authored retarget/fitting
results when they satisfy the native writer's existing constraints. A
cancelled review publishes nothing.

The Melee writer replaces the complete S/R/T track set for that destination
action when the selected clip is not its original associated member. Thus
unanimated target axes cannot accidentally retain tracks from the action
being replaced. It serializes a separate, new DAT file, freshly decodes it,
compares local S/R/T, cumulative world positions and the full native skin
surface against the finished target master at integer, eighth and withheld
subframes, then atomically publishes it. The project master, selected clip,
frame and original files remain untouched. Source, action, rig and clip
dependencies are rechecked before publication.

Brawl retains its qualified CHR0 and separate PAC/slot routes. Both games
enter the library through registered disc/image or game-folder sources;
Brawl also has direct resource import. Melee action DAT import requires its
exact model and game-source association through the library. A raw DAT file
with no verified association is not interpreted as an arbitrary target
animation. The extension offered for export follows the current target
adapter; an explicit CHR0 choice remains an editable master, not a Melee
native file. Future formats implement the same registered source, editable
target and destination-export contracts and require their own qualification.

## Evidence

Visual Studio 2022 Release/x86 built the application and ControlsAcceptance;
the pinned local .NET 10 SDK built the owned engine host and native worker.
The real Melee USA 1.02 private-desktop destination case passed 37 checks:
actual confirm/cancel dialog interaction, exact reviewed action selection,
native DAT serialization/reopen, a separate unassociated authoring clip,
no implicit action choice, and unchanged master,
rig and playhead. The associated-action edit/Undo/Redo/Save/reopen path passed
70 checks. The unassociated and associated candidate DATs in the first case
were both 16,631 bytes; this is a route test, not a compression result.
Fresh offscreen source, target and destination-dialog captures were inspected.
All private game data, generated DATs, screenshots and test catalogue files remain outside
the source tree.

Commands actually used: Visual Studio 2022 `MSBuild.exe` built
`tests/ControlsAcceptance/ControlsAcceptance.csproj` in Release/x86, which
also built the native app and adapters; `scripts/Build-Engine.ps1` used the
bundled .NET 10 SDK to package the owned host/worker. The serialized
`scripts/Test-BackgroundCapture.ps1` harness ran
`-WorkspaceMelee -MeleeEditableTarget -MeleeNativeDestinations`, the existing
`-WorkspaceMelee -MeleeEditableTarget` case, the existing
`-WorkspaceNativeAnimationExport` Brawl case, and a saved cross-game
`-WorkspaceMelee -MeleeNativeProject` case. All were private-desktop and used
ignored output directories. `Start-FighterPort.ps1 -Check` resolved the
preview.74 executable; no interactive input or game-runtime test occurred.

The unchanged ordinary Brawl CHR0 native-export path passed 46 private-desktop
checks, including unsaved edits, overwrite protection, native reopen,
untouched PAC siblings and no implicit optimization. A saved Brawl Mario →
Melee Mario Wait1 project was tested against the destination writer: its
finished target master contains 3,258 fractional authored keys over 51
frames. The current Melee FOBJ writer rejected those times and published no
DAT. This is a measured open compatibility gate, not a failed native file
accepted as output.

## Open gates

Melee native model writing, arbitrary standalone DAT association, complete
action-archive insertion, disc installation, runtime loop-rule editing and
actual game acceptance are unqualified. The current Melee FOBJ writer keeps
its integer frame-wait, 16-bit stream and terminal-data guards. A cross-game
result that needs fractional native keys or unverified runtime loop behavior
is rejected explicitly; this increment does not weaken those bounds or claim
that every retargeted animation can already be exported natively. Other
future game formats need their own reader, evaluator, writer and reopen gates.
