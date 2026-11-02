# Preview.72 — sequential native Use and recorded-boundary recovery

Starting commit: `85f8b9b28d402250bb61c9af994693f600806be7`.
Source version: `preview.72-sequential-use-recovery`; file `2.0.0.47`.
One G1/M2 milestone remains incomplete.

## Diagnosis and bounded change

The user reports a whole-window hang after accepting Melee Fox Wait1, then
Melee Falco Wait1, on the same Brawl Fox target using Start-FighterPort.
The application was not running when inspected, so no live hung stack was
available. A private-desktop replay on preview.71 did not reproduce an
indefinite hang. It did expose two independent hazards:

* The accelerated native viewport retained a DXGI swap chain after Present
  reported `DXGI_STATUS_OCCLUDED`, which is a successful HRESULT rather than an
  exception. Internal poses and back-buffer pixels advanced while the presenter
  remained occluded. The viewport now disposes that presenter and uses its
  existing OpenGL swap path when occluded.
* Use on This Model did not cancel an in-flight automatic source-preview load.
  It now cancels the load before retarget/review/import and requeues the selected
  original preview afterward. Existing generation checks prevent a cancelled
  load from publishing a stale viewport.

Neither change depends on character or animation names. The native model,
binds, weights, Next solver, root policy and project ownership are unchanged.

The later screenshot reported a different failure: fractional baking ended at
approximately `6.999999..7` with a large rotation error. An independent
procedural source-key step at frame 7 reproduced the same unresolved interval
on the starting code. The existing 20-subdivision cap left two representable
float times beside the boundary. The baker now permits four more subdivisions
only next to a recorded source/correction breakpoint. It still checks every
candidate against the solved motion, retains the same `0.025°` world-orientation
and other native tolerances, and keeps the existing work budget. Unknown
discontinuities still fail. No character, animation or bone name is special.

## Background replay

The test used the exact locally supplied Melee USA 1.02 ISO and Brawl WBFS.
It copied the latest saved catalogue to an ignored test directory without
writing the user's library; catalogue SHA-256:
`E521E11B8B366B2DCF9AEC7A5F654F60F3401B6CB4970D8D2B88126909F2D3FF`.
No game assets, catalogue, binaries or captures are published.

The focused test built the native application and x86 acceptance harness, then
ran `scripts/Test-BackgroundCapture.ps1 -WorkspaceMelee -MeleeSharedBridge`
with `FIGHTERPORT_TEST_SEQUENTIAL_USE=1`,
`FIGHTERPORT_TEST_QUICK_SECOND=1` and
`FIGHTERPORT_TEST_SAVED_LIBRARY_ROOT` pointing at that copy. The private
desktop never replaced the user's active desktop or injected input into it.
It selected Brawl Fox, accepted Melee Fox Wait1, then accepted Melee Falco Wait1
while Falco's small original preview was still loading.

Result: **46 native checks passed** in
`artifacts/Fighterport-viewer72/savedquick-boundary-final/background.log`. Fox Use
returned in approximately 22 s; Falco Use returned in 60.4 s. The second Use invalidated
the in-flight source load and the original Falco preview resumed. After each
import, explicit target frames had different poses and pixel hashes; playback
advanced frames and rendered changing pixels. The renderer no longer retained
an occluded accelerated presenter. Returning to the first imported clip worked;
the target MDL0 hash, binds and all skin weights remained unchanged. Fresh
Fox and Falco offscreen captures were inspected. The largest logged UI pause
during Falco's automatic controls was 888 ms; source review caused a separate
2.8 s pause. The direct review-to-import return was below one second for both.

The preview.71 baseline also completed this private-desktop sequence, so the
result does **not** prove the user's indefinite active-desktop hang is fixed.
The exact hung process and active display state were unavailable. A follow-up
must verify the launched preview.72 on the user's desktop if the symptom
continues. Physical-input, monitor-DPI and game-runtime acceptance remain open.

`tests/RetargetAcceptance/RetargetAcceptance.exe --adaptive-budget` passed 24
procedural checks after the change. The frame-7 step converged in 3,270 units
of bounded work and 495 sample times, then encoded as native CHR0 v4 and
freshly reopened with zero observed orientation error at all bake witnesses
and adjacent float times. A recorded fractional-frame step also converged;
the same unrecorded integer step still failed. Existing
hierarchy, protected root, winding, one-frame, loop, near-pole and cancellation
checks remained passing. The user's unspecified third character/animation was
not available as an exact replay, so its result remains open.

Format routing remains target-owned: a Brawl target exports CHR0; an associated
Melee target action exports a native Figatree `.dat` member. The common editable
CHR0 representation in a Melee project is an authoring master, not a Melee game
file. An unassociated retarget result still needs an explicitly reviewed Melee
destination action slot before native `.dat` export; alternate game-format
output is not inferred from a filename or provided without a compatible rig.
