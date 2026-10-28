# Native CHR0 export optimization — current preview.54 scope

The user's 28 October implementation brief applies to this current Fighterport
checkout, not the preserved private Next repository. Starting commit:
`37ec23c8aaa7de502d0a1efe661d673015d996e0`, branch `main`, canonical remote
`https://github.com/DoctorFlux/Fighterport.git`. Existing uncommitted complete-skin,
rotation and optimization work is retained. ONE G1/M2 remains incomplete.

## Authoring and export boundary

Keep the original Next pose/IK solver and complete native Model Previewer.
Retargeting, timing fits, start/end fitting, reviewed contacts, pins and FK/IK
produce the full-quality editable TARGET master. Ordinary project/native saving
must preserve that master and its recipes/references. Remove automatic lossy
optimization from retarget/fitting authoring transactions. The earlier automatic
authoring default is superseded by this explicit instruction.

Optimize an independent export copy of the current finished target. Compare
compression against that master, not an unretargeted donor. Source-relative bake
fidelity remains a separate gate. Preserve the existing precise I12 reference
route and never optimize an already lossy export as a new implicit baseline.
Optional applying optimization is only to an explicitly independent editable
copy, with the existing Undo system and the original master retained.

Reject uncommitted native target-model edits before live model hashing or
serialization, at initial preparation and all later publication checks. Ordinary
Save can commit those model changes first; unsaved animation edits remain
supported. A failed animation operation must not rebuild the dirty model and
silently clear its pending-edit state.
Preview must also preserve dirty animation siblings when the model and clips
share a native container. Copy existing backing into independent owners without
rebuilding the live root, and verify the exact model after reopen. Stale backing
or pending render-resource changes requests Save; dirty CHR0 curves alone do not.

Expose explicit **Exact** and **Error-bounded** policies. Exact permits only
proved unchanged decoded functions and conservative structural changes. Bounded
reduction/quantization uses explicit saved limits; search effort is independent
of those limits. Existing approved ceilings remain the initial bounded profile:
0.1 degrees raw Euler/local orientation, 0.002 native units translation and
0.0001 scale, corresponding existing derivative gates, and 0.001 times the
verified positive finite bind-geometry diagonal for cumulative native geometry.
These are ceilings, not a fresh allowance per pass. Exact roots, authored pins,
contacts, mandatory boundaries and protected segments override approximation.
The export must also retain existing absolute fit/contact acceptance: when that
context cannot be independently verified, retain Exact/reference output rather
than claim bounded-contact qualification. Do not relax unresolved gates.

## Shared implementation and measured bytes

Audit direct, library/batch, timing-fit, fit/contact, standalone, reviewed-set,
native-slot and supported container routes. Reuse one export optimizer; routes
without verified intended-model/protection context retain the reference path and
are separately qualified. No global shared-codec tolerance changes or unrelated
resource rewrites.

Current owned CHR0, standalone tool, reviewed-set and qualified native-slot
exports share the frozen-master optimizer. Slot name/version conversion gets a
new, like-for-like slot reference size and independently resealed output hash.
Owned nonselected animations without a verified intended target use an
independent precise reference copy rather than the live legacy writer. The
existing retarget motion-PAC insertion route prepares the same independent
candidate, then verifies its full decoded content/storage and every untouched
native sibling and archive entry before a new-file commit. Arbitrary BRRES/PAC
resource-tree exports remain distinct reference routes; standalone savings do
not establish container or outer-compression savings.

Solver-created paired-key evidence is bounded and session-only. The adaptive
writer records ordered coefficient bits, flags and native timing context at
creation; export resolves it only for the exact finished content and native rig.
Fitting carries only unchanged known tuples. Manual edits invalidate lookup;
exact Undo or same-process reopen may restore retained evidence. A fresh process
or an evicted record keeps unknown pairs protected and reports that limitation.
Exact controls, pins, contacts, protected ranges and mandatory joins still take
precedence. Recognizing equal values alone never establishes generated origin.

The same immutable native encoding plan governs sizing and writing. Search legal
S/R/T-group formats, fixed constants and valid isotropic sharing; preserve absent
channels, flags, compensation, winding, discontinuities and terminal payloads.
Use existing interpolation-aware rational certificates and complete cumulative
native comparison. Encode then independently reopen each candidate before it is
eligible. Choose the smallest completely validated physical output searched,
with deterministic tie-breaking, cancellation and existing work budgets. No claim
of a mathematically global optimum or guaranteed compression percentage.

Report actual stream payload, entry headers/value-offset slots, dictionary,
strings/metadata, alignment/padding, resource-block size and physical standalone
length separately. Report PAC/BRRES and outer compression savings only when
actually measured. Verify I4 255/256 boundaries and both the legacy I6
2047/2048 assumptions and the reference runtime's signed S10.5 boundary
1023.96875/1024; unrepresentable data falls back without clamping/wrapping.

## Fractional native representation

The direct Next-pose observer found genuine between-frame bake errors. A
five-point exact-rational certificate proves that one authentic near-extension
knee span cannot meet the requested all-bone angular accuracy with ANY single
cubic between its whole-frame boundaries. Native fractional knots are therefore
implemented as actual CHR0 times, retaining integer APIs where compatible;
no frame-count/timebase change or substitute runtime format is allowed.
CHR0 curve editing, snapshot/Undo, precise writer, source hashes and export
verification must preserve those times. Fractional SRT0/SHP0 editing is not
qualified by this CHR0 increment; legacy resource-specific actions retain their
separately documented limits. Native I6 uses signed S10.5 times and
I12 stores float times. Negative active knots remain explicitly unsupported by
the current zero-based animation / frame-zero-bind-pose authoring contract.

Adaptive target curves must preserve original roots, hierarchy, binds and ALL
skin coefficients. Use generic error-based subdivision and remove redundant
knots; no character/action/profile numeric exceptions. The earlier 31,032-byte
forward and 39,204-byte reciprocal files retain known fractional-motion errors
and are historical size comparisons, not equivalent finished masters. Under the
latest explicit brief, no-growth acceptance compares each export with the SAME
correct finished target master. Report increases over those older files openly;
do not silently lower motion quality or substitute a donor size target.

## Verification and cleanup

Test constants with nonzero slopes, sparse/rest/identity semantics, flags,
isotropic functions, signed zero, negative/nonuniform/singular scale, full turns,
fractional/paired keys, loops and terminal data, format overflows, invalid data,
stale dependencies, cancellation, repeated export/reopen/resave and unchanged
master/Undo/selection. Include authored/generated curves, reviewed contacts and
protected fitting middle/joins. Compare complete cumulative native transforms
and every skin influence; scalar continuous bounds and sampled geometry remain
separate claims. Test available Mario/Snake fixtures and both AttackAirLw
directions, background-only, with fresh orthographic side GIFs/ground line and
only the target's first Fall frame. Original/private assets stay local.

The user clarified cleanup as old generated saved outputs, builds, build reports
and GIF reports, not removal of tools/UI. Delete superseded generated artifacts
only after checked in-workspace dependency/reparse/process preflight. Retain
original inputs, final reopen dependencies/evidence and the accepted launcher
package until its replacement qualifies. Preserve Git history/licenses/credits.

Record commands, actual outcomes, coverage, hashes, tool/runtime revisions,
search time and fallback reasons. BrawlCrate issue129 is its specific nonlooping
Mario Kart Wii terminal regression, not a claim about every Brawl resource.
Physical input, monitor DPI, human usability, broad contacts, Melee/new adapters,
third-party binary-plugin ABI compatibility and actual game acceptance remain
unverified until exercised. Publish verified source only as DoctorFlux to public
Fighterport/main; never add Codex attribution/collaboration.
