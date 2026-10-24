# Native control review — preview.9

This follows [preview.8](NATIVE_CONTROLS_WORKSPACE_ACCEPTANCE_2026-10-23.md)
within the same incomplete M2 integration slice. The dark standard viewer,
direct startup, original Next retargeter and native camera remain in place.

Animation Controls now offers **Review IK Chains and Shared Bones**. The dark
native window selects existing upper/middle/end joints, edits a straight-limb
pole direction, reviews body/shared roles, adds custom roles and explicitly
confirms unassignments. Child selectors follow the actual native hierarchy.
Core's existing review functions validate the complete proposal before it is
committed; an invalid chain or body role changes nothing. The reviewed editor
setup is saved with the active control document on its exact rig fingerprint,
with native Undo/Redo. Next's original retarget mapping workflow remains separate.
No native skeleton, bind, weight, animation key or numerical solver is changed.

The bridge now records the coordinate-review dependency in control state.
An unchanged coordinate review preserves subsequent manual chains and poles;
changed explicit coordinates trigger revalidation. Canonical role ordering
prevents a storage permutation from resetting the setup. Older control states
without this optional field remain readable. Setup loading also checks native
content and the current frame before publishing pose/pin data; stale work is
discarded and ordinary UI setup can retry. Review publication retains its own
native-content and ownership guards.

## Commands and evidence

Commands ran from the containing workspace with local SDK 10.0.402 and VS2022
MSBuild. `$mario`/`$snake` denote supplied private asset folders. Assets, private
paths, generated output, snapshots and images are excluded from source publication.

```powershell
& .local/next-main/scripts/Build.ps1 -DotNet $dotnet -NativeDirectory artifacts/next-preview9/native
$native = Join-Path (Get-Location) 'artifacts/next-preview9/native/'
& $MSBuild .local/next-main/tests/ControlsAcceptance/ControlsAcceptance.csproj /t:Build /p:Configuration=Release /p:Platform=x86 /p:NuGetAudit=false "/p:OutDir=$native" /nologo /v:minimal
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model artifacts/viewer-parity/native/original.mdl0 -Motion artifacts/viewer-parity/native/original.chr0 -NativeDirectory $native -ControlReview -OutputDirectory artifacts/viewer-parity/next-preview9-review-original-final-r2
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$mario/FitMario00.pac" -Motion "$mario/FitMarioMotionEtc.pac" -NativeDirectory $native -ControlReview -OutputDirectory artifacts/viewer-parity/next-preview9-review-mario-final-r2
& .local/next-main/scripts/Test-BackgroundCapture.ps1 -Model "$snake/FitSnake00.pac" -Motion "$snake/FitSnakeMotionEtc.pac" -NativeDirectory $native -ControlReview -OutputDirectory artifacts/viewer-parity/next-preview9-review-snake-final-r2
```

All three review runs pass **16 checks** each: valid direct chains, rejected
reversed hierarchy, atomic chain-plus-invalid-body rejection, actual window
Cancel/Save, manual pole persistence with an existing coordinate review, explicit
unassignment, a custom shared role, storage permutation, unchanged native bytes,
exact Undo/Redo, stale review rejection, stale setup-content/frame rejection,
fresh project reopen and immutable source files.

The full native regression batch uses the preview.8 command shapes with
`-NativeDirectory artifacts/next-preview9/native` and these fresh output folders:

| Receipt folder under `artifacts/viewer-parity/` | Checks |
| --- | ---: |
| `next-preview9-review-{original,mario,snake}-final-r2` | 48 |
| `next-preview9-gizmo-{mario,snake}-r1` | 62 |
| `next-preview9-controls-r1` | 41 |
| `next-preview9-{mario-to-snake,snake-to-mario}-r1` | 56 |
| `next-preview9-project-compatibility-r1` | 12 |
| `next-preview9-workspace-r1` | 19 |
| `next-preview9-model-pose-{original,mario,snake}-r1` | 33 |

These total **271 native checks**. Both retarget directions again match the
original Next bake and preserve moved/rotated pin keys through seek, Undo/Redo,
CHR0 decoding and actual saved-project viewer reopen. Current gizmo runs retain
all-key/tangent native IK equality and independent Hermite interpolation.
The 355-clip legacy project and independent target-switch checks also pass again.

The focused engine test project was rebuilt in Release and run below normal
priority against this output's worker. Its native controls, frame editing,
native editing, receipt compatibility and native-backed workspace scopes pass
**64/76/32/9/34**, respectively, in `next-preview9-engine-{controls,editing,native,
compatibility,workspace}-r1`. Use the exact argument order in the preview.8 receipt,
substituting these new output folders. This is **215 engine checks**, giving
**486 current checks** without adding repeated intermediate runs. The retained
regression renderer emits its existing AVA3001 build warning; the native product
build completes with zero warnings/errors. Logs are local under
`artifacts/next-preview9/`.

The 39 PNGs from this matrix were opened for actual visual review: nine review/
project images, six gizmo/curve images, three original controls images, ten
retarget images, two compatibility images, three workspace images and six
model-only images. Capture stays serialized, paced, below normal priority and
on a private desktop that is never activated. No OS input, screen copying or
viewer-settings writes are used. Visual review found a light unused tab strip;
the review window now paints that strip dark while preserving native tab bounds
and hit testing, including native print captures. The final three review runs
were rebuilt and recaptured after that correction.

An earlier original-fixture run exposed that leaving an already-unassigned role
unchanged did not record an explicit review. The Confirm column fixes that case;
the failed run is preserved. Intermediate 11/14-check runs remain historical,
not additional counts. The previous scale-crop, unweighted procedural boxes
and deliberate legacy geometry-rejection capture limits still apply.

This does not complete the standalone roadmap. Viewport pole-handle editing,
remaining advanced batch/fit review and broader expert interfaces remain queued;
transition/slot records are retained contracts, not working standalone tools that
can simply be moved. Physical input/DPI, exact reference raster parity, full
motion/contact/generalization quality, remaining adapters, builds and actual
games are still unverified. Earlier numerical failures remain failures, and
no M2 completion or binary release distribution is claimed.
