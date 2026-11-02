# Sequential Use on This Model: follow-up diagnosis

Starting product commit: `5314efe0a0fffc9d2abe4a39bd6bf93768c5024f`
(`preview.72-sequential-use-recovery`). The user's active Model Previewer
report was that the whole window stopped responding after a second import,
and remained frozen after dismissing a later bake-error dialog. They closed
their windows before process ownership could be determined. Immediately before
that, two visible Fighterport processes were present, one preview.71 and one
preview.72. This observation cannot identify which window had the symptom.

The background acceptance harness copied the saved source catalogue to an
ignored test directory. It used the authentic local Melee USA 1.02 ISO and
Brawl WBFS on a private desktop, without active-desktop capture or OS input
injection. The focused run set `FIGHTERPORT_TEST_SEQUENTIAL_USE=1` and
`FIGHTERPORT_TEST_FAILED_BAKE_RECOVERY=1`, then ran
`scripts/Test-BackgroundCapture.ps1 -WorkspaceMelee -MeleeSharedBridge`.
It selected Brawl Fox, imported Melee Fox Wait1 and Melee Falco Wait1 through
the existing retarget/review/import workflow, then attempted Melee Peach Wait1.
The expected failure invokes the same `UseLibraryOnTarget` action directly:
the native `MessageBox` wrapper cannot be dismissed by this input-free harness.
The resulting document and viewer recovery are tested; the physical modal
dismissal remains unverified.

The run passed **54 native checks** in the ignored local
`artifacts/Fighterport-sequence73/three-use-failure-final/background.log`.
Fox review/import returned in 22.1 seconds and Falco in 59.6 seconds. Both
imported independent clips into the same native target. Explicit frames,
playback frames and viewport pixels changed. Peach preparation failed in
14.0 seconds at native frame 179: decoded world orientation on mapped,
editable `R3rdNb` exceeded the unchanged 0.025° limit by 0.00256°.
The local difference was 0.01398°. No Peach result was imported.

After the failure, the harness verified that the retarget busy flag cleared,
the prior target animation and MDL0 remained selected, playback advanced and
fresh pixels rendered. Native camera rotation and bone selection also worked;
the target MDL0 content hash, binds and all skin weights were unchanged. A
fresh offscreen capture was inspected. The original Next retargeter and native
viewer were not replaced. A target-only-bone canonicalization hypothesis was
tested and discarded because the failing joint is mapped and editable.

The whole-window hang was **not reproduced**. The separate Peach quality
failure is real and remains open; the native bound was not relaxed. The user's
unspecified third source is not equivalent to Peach, and this test does not
establish universal recovery or active-desktop behavior. The accepted
preview.72 launcher binary was rebuilt from its clean starting source after
the diagnostic. No new preview or game-acceptance claim is made.
