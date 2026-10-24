> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# Transition fitting contract (M2)

Fit Start From / Fit End Into accept Idle, Fall or any compatible target-rig clip, pose or moving segment. Store source/target skeleton revisions, reference clip revision, phase, playback rate, body mask, root policy, contacts, solver version and editable windows. Edits to referenced revisions invalidate results. Detect recipe cycles; explicitly frozen baked references can break a cycle.

For a 24-sample move at 60 Hz, use zero-based keys 0..23 and end-exclusive duration 24/60 s. UI frames 1..4 may fit from Idle, 5..18 remain unchanged and 19..24 fit into Fall; the hit at frame 12 stays at frame 12. The next state's first runtime sample is specified independently of the last stored key. Never duplicate a boundary sample by accident.

Fit evaluated target-rig poses after retargeting, against moving reference segments. Quintic overlap is a starting blend, not proof of velocity continuity. Use sign-consistent quaternion interpolation and preserve intentional Euler winding/full spins; measure linear/angular velocities. Preserve root movement separately, stable poles, intended feet/hands/prop contacts; release stance for airborne exits. Discrete channels require explicit policy/loss diagnostics.

Overlapping windows, protected fractional intervals, incompatible contacts or speed limits return exact conflicts without changing gameplay. Apply as one undo transaction; bake into editable native keys. Rivals II playback cannot require a FighterPort rig or runtime solver.

Required tests: all four joins, start-only/end-only/both, moving references/real entry phase, zero/one-frame windows, protected subframes/events, full spins, source isolation, contact/release, root movement, random scrub determinism, repeated bakes, invalidation/cycles and fresh native export/reopen. Quantization-specific tolerances come from measured native errors. Until these pass the fitter is planned, not verified.

Automatic-retargeting timing now preserves source-time recipes and an optional end-exclusive boundary key independently of native loop flags. Native non-loop boundary behavior is verified by `NativeTimingChecks`. This is not implementation of Fit Start From/Fit End Into, all four state joins, protected-window fitting, slot manifests or Fill Missing. Those remain a separate subsequent scope after M2 contact acceptance; do not infer transition support from a successful trimmed/retimed export.
