# Preview.73 — native viewer presentation mitigation

Starting commit: `2da22cf791d73d1e9e9fa5696ad8d5c22a4a3072`.
The user reported that after opening Brawl Fox as target, using Melee Fox
Wait1, then using Melee Falco Wait1, the main model image stayed on the prior
animation. Playback, animation switching, camera movement and bone selection
appeared ineffective. No live hung process was available for inspection.

The prior preview.72 private-desktop replay passed, and its main viewport
reported `accelerated=0`: it used the existing OpenGL swap path. The active
desktop can use the optional DXGI interop presenter. This environment
difference makes stale or blocked DXGI presentation a **hypothesis**, not an
observed cause. Preview.72's occlusion fallback cannot detect every possible
stale-present case. Fighterport now defaults `FIGHTERPORT_DISABLE_DXGI` to `1`
at application startup when the setting is absent. An explicit `0` keeps the
DXGI path available for controlled investigation. The native renderer,
camera, animation evaluator, BrawlCrate Model Previewer, original Next solver,
root controls, binds and all skin weights are unchanged.

The focused harness now checks the exact newly selected frame count and that
the viewer remains visible/enabled immediately after each Use. It invokes
native mouse handlers within its private desktop, checks playback and pixel
changes on Falco, then reselects Fox and checks playback and pixels again.
These calls do not inject OS input. The input-path test does not replace a
physical mouse/display acceptance run.

Build and verification performed locally:

- `scripts/Build.ps1 -NativeOnly -NativeDirectory ../../artifacts/Fighterport-viewer73/native`
- `MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x86` with the same output directory.
- Copied the unchanged preview.72 engine deployment into the isolated
  candidate; host and native-worker executable hashes match their preview.72
  copies. The first native-only candidate lacked that worker and its
  control-setup test failed. The deployment was completed and rerun.
- `scripts/Test-BackgroundCapture.ps1 -WorkspaceMelee -MeleeSharedBridge`
  with `FIGHTERPORT_TEST_SEQUENTIAL_USE=1` and
  `FIGHTERPORT_TEST_VIEWER_INPUT_PATH=1`, the saved-library copy, exact local
  Melee USA 1.02 ISO and Brawl WBFS, and a private desktop.

The complete run passed **54 native checks** in ignored local
`artifacts/Fighterport-viewer73/viewer-input-complete/background.log`.
Fox Use returned in 21.2 s; Falco Use in 62.1 s. The selected target changed
to Falco's 241-frame result (Fox was 121 frames); both the native editor and
viewport stayed enabled/visible. Mouse handlers returned in 10 ms and 2 ms.
Falco's playback advanced frames 15→31 and presented changing pixel hashes
with `accelerated=0`, `occluded=False`. Reselecting Fox advanced frames and
presented changing pixels again. Target MDL0 content, native binds and all
weights were unchanged. Fresh Falco and reselected-Fox captures were inspected.
No game inputs, catalogues, captures or generated binaries are published.

This qualifies the **background behavior of the mitigation**. It does not
verify active-monitor rendering, physical input, eliminate the reported hang,
or prove the DXGI hypothesis. Falco retarget preparation remains slow and
separate. If the physical viewer still freezes, the next diagnostic needs its
live process left open to inspect its UI thread and presenter state. The sole
G1/M2 milestone, contact quality and actual game acceptance remain open.
