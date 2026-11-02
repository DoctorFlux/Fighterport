# Preview.71 — playback after sequential native retargeting

Starting commit: `359b6473915e117fdb8e8407e8f87804af63b175`.
Qualified source version: `preview.71-viewer-playback-recovery`, file `2.0.0.46`.
The one G1/M2 milestone remains incomplete.

## Failure and change

The main viewer used one busy predicate for both native playback and control editing. After a retarget result was imported, automatic read-only control setup could still be loading an immutable snapshot in the owned worker. During that interval, Play was silently discarded. Selecting another animation could start another setup and repeat the symptom. A private-desktop replay of Melee Fox Wait1, then Melee Falco Wait1, on the same Brawl Fox target reproduced the dropped Play command after the second import (`artifacts/Fighterport-sequential71/repro-busy-before`).

Playback now has a separate guard. It may begin during automatic read-only setup, while native control edits and gestures remain blocked. A setup that finishes after playback starts is discarded by its existing epoch/content checks and prepared again after playback stops. Retargeting, source-local root controls, the native Model Previewer, model data, native Undo and the full-quality CHR0 result are unchanged. No character or animation identity selects this rule.

## Background verification

The same exact Brawl WBFS and Melee USA 1.02 ISO were used with a reused verified Brawl catalogue. The private-desktop harness selected the Brawl Fox target, used Melee Fox Wait1 and then Melee Falco Wait1 through the actual library button, accepted the existing source-key review and retained both independent results. The focused pre-fix replay failed only the new pending-setup playback assertion. With the fix, **35 native checks passed** (`artifacts/Fighterport-sequential71/fixed1`): Play advanced before setup finished, ordinary playback advanced after each import, the animation list returned to the first result, and the target's native model content remained unchanged. Fresh offscreen captures of both results and the reselected first result were inspected.

The Fox preparation took about 20 seconds and Falco about 56 seconds in this local fixture. Each review-to-import return took under a second. This change fixes the silent Play block; it does not reduce retarget preparation time or claim that every clip's rendering and game behavior are qualified. The specific Falco clip from the user's session was not identified; Wait1 was the reproducible sequence. No physical input, active-desktop capture or game runtime test was used.

Preview.66 finite ledges and broader G1/M2 acceptance remain open. The generated build, captures and private assets remain local and ignored.
