# Ordinary native animation export — implementation notes

Starting revision: `80353b8c3a605229f9c1890bf6ba222ca4450f08`
(preview.59). This is a bounded correction within the same incomplete G1/M2,
in the active `DoctorFlux/Fighterport` application. It restores the ordinary
Model Previewer animation-list export contract without replacing the native
viewer, original Next retargeter or explicit optimization tools.

## Why ordinary Export opened optimization

The Model Previewer animation-list context action already opens BrawlCrate's
native `SaveFileDialog` and chooses the native filter for the animation type.
For CHR0, `FileFilters.CHR0Export` advertises native `.chr0` and Maya `.anim`.
After the picker accepts a destination, `FighterNativeExportRequested` invokes
`ExportFighterNode` through the application's ordinary error handling.

The owned-CHR0 branch then entered `TryExportOwnedAnimationCopy`, read saved
`NativeExportOptimizationPreferences`, froze a model/protection context and ran
`PrepareWorkspaceExportWithProgress`. Consequently, an ordinary right-click
Export could unexpectedly open **Preparing verified export copy** and perform
Error-bounded analysis merely because that policy had been used previously.
It could also reject a plain animation export because the active model had
unsaved changes, even though an exact resource export requires no model solve.

The resource-tree `CHR0Wrapper`/`GenericWrapper` route is distinct. It already
uses the original native writer after application output-path protection and
does not enter this preparation window. Its context menu is retained.

## Current-document CHR0 output

The owned `.chr0` branch now uses `FreezeOwnedReferenceExport` directly and
publishes through the existing `ExportWorkspaceSnapshot` boundary. It does not
read or write optimizer preferences, call the optimizer, compute geometry error,
start a worker or open the preparation progress window. No additional reference
preparation pass is inserted before the final verified write.

The frozen copy is made from the exact requested current native clip, including
an explicitly identified nonselected owned member. It is not taken from the
session's retained pre-optimization master. An earlier explicit **Apply to
current animation** therefore remains applied when the user later performs an
ordinary native export. The retained full-quality baseline remains available
for subsequent explicit optimization under its existing session rules.

The source-aware native snapshot preserves current keys, equal-time sides,
tangents, transform flags, order, metadata, timing, loop and retained storage.
Existing unsupported or unrepresentable native metadata/storage cases still
fail conservatively; this change does not weaken their writing gates. Plain
CHR0 export does not require a selected MDL0 or a saved model, infer a target
rig, remove foreign/attachment tracks, rebuild the live model or alter weights.

Existing ownership, busy-state, review-draft and stale clip/project/recipe checks
remain. Source, library/game-installation and project-working-file protections
still apply. The writer stages a sibling temporary CHR0, freshly decodes it and
compares complete expected content/storage before an atomic move or replacement.
Destination hashes are rechecked before publication. A failed write, validation
or publication preserves an existing destination and removes its candidate.
Successful export records its receipt and a compact native-export status only
after publication. No live clip is rebuilt or temporarily selected to export it.

Normal playback stopping and pending native curve-edit completion remain at the
existing operation boundary. There is no new document owner, Undo stack, project
format or playback-tick processing.

## Native Maya conversion

`CHR0Node.Export` dispatches `.anim` to the original native Maya converter. That
converter owns its original MDL0 chooser and can return normally without writing
a file when the chooser is cancelled. The previous generic post-write binary
`NodeFactory` reopen was inappropriate for this text conversion.

The owned `.anim` branch now calls that native virtual writer on the independent
current clip, using a sibling temporary filename whose suffix remains `.anim`.
This preserves native format dispatch. A missing candidate after the native
chooser returns is cancellation: no destination is published and no successful
receipt is recorded. An empty candidate is rejected. A completed nonempty native
conversion receives the same stale-state, protected-path and destination-hash
checks before atomic publication; partial candidates are removed on failure.

Maya output is not decoded as a binary CHR0 or described as a verified native
CHR0-equivalent resource. The converter retains its own format behavior and
limitations. `.txt`, `.json` and `.smd` remain import formats, not newly advertised
export choices; the existing unadvertised Collada writer is not expanded here.

`NativeAnimationConversionWriter` is an internal verification seam, defaulting
to null in normal application use. Background tests may provide the actual
native `AnimFormat.Serialize` overload with a specifically chosen model, avoiding
OS chooser/input automation while still exercising the real converter. It is
not a user setting or a replacement production converter. Such tests can compare
converter bytes, cancellation and failure behavior below the chooser; they do
not qualify physical picker interaction or the native Maya chooser itself.

## Ordinary retarget PAC output

The ordinary retarget-to-PAC route no longer reads optimizer preferences or
invokes automatic preparation. It retains `FreezeOwnedWorkspaceExport` for its
existing target/protection context; that snapshot's candidate is the actual
current clip even when a separate retained optimization baseline exists.

The original saved-target, exact rig, unique template-slot, protected-path and
new-destination rules remain. Validation also explicitly rejects replacement of
the form's active target while the motion template is being chosen. The frozen
context and template hash are revalidated before publication.

`BrawlRetargeter.ExportMotionPac` receives the independent current candidate and
its existing validation callback. The optional centrally prepared standalone
hash is absent because no optimized candidate is being prepared. The adapter
still independently writes/reopens the CHR0, checks content/storage/raw payload
identity, verifies the final container and untouched siblings, rechecks the
target/template/candidate dependencies and publishes only a complete new PAC.
Those checks are not replaced by type/frame-count checks or an optimistic status.

## Explicit workflows remain separate

The standalone optimizer, **Export optimized copy**, workspace verified export,
reviewed-set export and source-bound animation-slot workflows retain their
explicit preparation, saved policy and compatibility gates. Their qualification
is not inferred from the ordinary native export route. Other resource types keep
their existing native paths. Ordinary project Save remains exact, and protected
roots, native hierarchy/binds/all skin coefficients and the original retargeter
remain unchanged.

Qualification is recorded in the [preview.60 acceptance record](NATIVE_ANIMATION_EXPORT_ACCEPTANCE_2026-10-29.md):
806 checks / eight admitted background runs and 26 actually inspected UI PNGs.
Tests remain background-only;
physical input/pickers, monitor DPI, human usability, general contacts, external
editor compatibility, Melee and actual game acceptance are not established by
this implementation audit.
