# Requested attack-to-fall and attack-to-idle examples

25 October 2026. These are local tests of the existing **Fit start or end**
workflow on the qualified preview.27 application, source commit
`54c41e1593335f51f2b1ffd4f4eb540b7721f701`. The new loop/seam inspector is read-only;
the existing fit workflow makes these assisted endpoint edits. No application,
Next solver, protected-root policy or project format was changed for these demos.
The separate preview.27 qualification remains tied to that tested source commit.

## Procedure and scope

Original Next first transferred each authentic source animation to the target rig.
The existing native Fit start or end preparation/review/Apply path then fitted
only the exit, using the target's native reference animation at display frame 1
(core sample 0), moving-reference rate 1 and all three transform components.
The assisted recovery spans were the final 12 samples for the aerial and final
8 samples for the tilt. Nearest-equivalent Euler handling and root-translation
preservation remained enabled. Existing reviewed body-edit eligibility, complete
contact dependency paths and source-authored motion-control protection remained
in force. No contact provenance or guard was removed to force a match.

Each independent local project and precise CHR0 was saved. Endpoint measurements
compare authentic bind fallback plus native sampled local transforms against the
actual target reference. Frozen tolerances remain 0.0002 native translation units,
0.00001 scale and 0.01 degrees local orientation. These are numeric agreement
tolerances, not artistic or game-quality thresholds.

## Results

| Requested case | Exact clips and scope | Measured outcome |
| --- | --- | --- |
| Mario forward aerial on Snake, ending in fall | Mario `AttackAirF`, 75 frames; Snake `Fall`, 19 frames; 12-sample exit fit | All 66 eligible local bone/helper transforms match Fall frame 1. Maximum translation/scale difference is zero; maximum orientation difference is approximately 0.00001146 degrees. The four protected motion controls also match this reference endpoint without editing their curves. No other endpoint differs. |
| Snake first forward tilt on Mario, ending in idle | Snake `AttackS3S`, 32 frames; Mario `Wait1`, 51 frames; 8-sample exit fit. Separate 46-frame `AttackS3S2` is excluded. | All 50 eligible local transforms match Wait1 frame 1. Maximum translation/scale difference is zero; maximum orientation difference is approximately 0.00000171 degrees. **This is only a partial fit:** nine protected hip/leg local transforms differ and the transition visibly jumps to idle. |

The ground example retains all original curves along 13 contact dependency paths:
the four reviewed motion controls, HipN and both leg/knee/foot/toe chains. HipN's
remaining endpoint difference is approximately 1.06939 native translation units
and 40.07227 degrees. The other eight leg-chain orientation differences range
from approximately 2.89727 to 33.28505 degrees. A child-local match does not imply
a world-pose match when its protected parent differs. The four motion controls
themselves match the reference endpoint and retain their source-authored curves.

Both cases preserve exact protected root/contact keys, tangents and flags, the
retained middle keys, source bytes, reference clip and original Next provenance.
The ground result demonstrates a remaining need for bounded contact-aware recovery
or release fitting; this test does not implement or qualify that work.

## Background evidence and GIF review

An ignored local net472/x86 harness called the existing application handlers on
private desktops and used the shared offscreen native viewport/capture mutex.
There was no active-desktop window, input injection or screen copy. Rendering was
serialized and paced. The harness references the qualified preview.27 binaries;
it does not replace them. Receipts record 56 aerial and 51 ground checks. These
local checks are separate from preview.27's 2,606 qualified assertions.

Each fixed-camera GIF plays the fitted attack followed by the actual target
reference animation, sampled every two native 60 Hz frames. The GIF uses the
corresponding quantized delays and a final 500 ms hold. Independent decoding
verified every delay, frame count and dimensions; every decoded frame was visually
inspected in contact sheets, alongside the attack-last/reference-first PNGs.

| Local GIF | Dimensions / frames / total time | SHA-256 |
| --- | --- | --- |
| `Mario_Forward_Air_on_Snake_to_Fall.gif` | 960 x 720 / 49 / 2.07 s | `33b8feaabb19fe3c296f99ca19541f54cb76836bd5ed36009bd9626555f594e1` |
| `Snake_First_Forward_Tilt_on_Mario_to_Idle_PARTIAL.gif` | 960 x 720 / 44 / 1.88 s | `3a5afc69330ca1a1422d3374a0ead9bd75e171c1712e1c38e49e73527dd46b5d` |

Local artifacts are retained under the parent workspace's ignored
`artifacts/Fighterport-requested-transitions`: accepted cases
`mario-attackairf-snake-fall-dev2` and `snake-attacks3s-mario-wait1-dev1`, with
`demo-details.json`, `receipt.json`, frame metadata, native captures, independent
projects and CHR0s. `gif-review/manifest.json` records GIF/evidence hashes;
friendly GIF copies are in `results`. Superseded development outputs are excluded.
Game assets, private paths, generated binaries and captures are not published.

This records two specific assisted examples, not arbitrary-character porting,
automatic contact recovery, continuous-velocity agreement or game acceptance.
Physical input, monitor DPI, human usability and broader contact/game gates remain
unverified within the ONE incomplete G1/M2 milestone.
