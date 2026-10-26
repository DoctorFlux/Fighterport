# Mario and Sonic disc-library trials — 26 October 2026

These trials use the accepted preview.36 native package and exact costume-00
models and MotionEtc resources from the verified Brawl disc catalogue. They are
additional qualification within the one incomplete G1/M2, not a new solver or
general animation/contact acceptance. The complete native Model Previewer,
original Next, native Undo and source-local protected controls are unchanged.

## Native transfer, export and reopen

| Source → target | Clip | Native frames | Result |
| --- | --- | ---: | --- |
| Sonic → Mario | Wait1 | 306 | Exact original Next; verified export and reopen |
| Sonic → Mario | AttackS3S | 44 | Exact original Next; verified export and reopen |
| Sonic → Mario | AttackAirF | 42 | Exact original Next; verified export and reopen |
| Mario → Sonic | Wait1 | 51 | Exact original Next; verified export and reopen |
| Mario → Sonic | AttackS3S | 33 | Exact original Next; verified export and reopen |
| Mario → Sonic | AttackAirF | 75 | Exact original Next; verified export and reopen |

Each UI transfer was compared with a separately baked original Next result,
including authored keys, tangents, flags and timing. Verified exports were freshly
decoded and compared again; all six saved projects were freshly reopened with
the exact disc clip ID and `fighterport-brawl-disc-v1` provenance. Independent
root proofs compare source-authored local controls against output, including bind
conversion and original boundary-key values/tangents. The separate authentic-rig
root suites passed 628 checks in each direction. Target model bytes, hierarchy,
binds and skin weights were unchanged. Structural fixture mapping was reviewed
numerically against native weighted limb chains and bind geometry; no human
mapping acceptance is claimed.

Eight exact-clip side-view GIFs include both original Wait1 animations and all six
transfers. Samples use every second native frame plus the exact last frame, with
centisecond delays preserving the original native duration. The gold line is
native Y=0, not a game collision surface. These raw transfers have no boundary
fit. The original Wait1 clips run in full in this comparison; this does not change
the separate fitting rule that a chosen Wait/Fall reference uses only its first
pose.

Sonic's native model also contains alternate sphere geometry. The default native
preview draws that mesh over the body, including in the original Wait1; authentic
bone visibility flags alone do not hide it. Four additional Sonic-target/original
GIFs explicitly hide the identified sphere object through the native display
toggle and label that choice in every frame. The original captures are retained.
This is a local diagnostic display choice, not transferred VIS0, PSA/game visibility
emulation, a model edit or a character-specific retargeting exception. Exact native
model bytes, animation bytes, dirty state and Undo remain unchanged. Facial and
other game-controlled visibility are not qualified by these CHR0 trials.

## Separate assisted fitting probes

- Sonic AttackAirF → Mario Fall: the 12-sample exit fit passed its existing
  independent preservation and endpoint gates. The GIF ends with only Fall's
  first pose held; its endpoint and reference render have identical native pixels
  under the retained attack controls. Contact recovery was **off** for this case;
  this does not certify floor contact or foot locking.
- Sonic AttackS3S → Mario Wait1: rejected by the independent pre-join fractional
  world-bone/skinned-vertex preservation check. Authored protected controls and
  pre-join keys passed, but that is insufficient to accept the fitted result.
  Investigate sparse native interpolation/materialization without weakening
  the frozen gates. No successful-fit GIF is presented for this case.
- Mario AttackS3S/AttackAirF → Sonic Wait1/Fall: optional control-worker setup
  rejects authentic Sonic vertices with more than four influences. The native
  editor/renderer and original Next transfers above work. No influence was
  discarded, mesh reweighted or skeleton replaced to bypass the worker limit.

Wait1 above is a native retargeting trial, not an assertion that either idle has
ideal contacts or is a suitable fitted endpoint for every action. Ground clearance,
sliding, action style and transition continuity remain separate gates.

## Evidence and reproduction

Local evidence is under `artifacts/Fighterport-sonic36` in the parent workspace;
it remains excluded from public source. `native-transfer-audit.json` records the
six fresh decode/reopen checks. `roots-*/motion-controls.json` records the two
628-check suites. Per-case logs, independent CHR0 candidates, exported CHR0s,
projects, immutable-input hashes and root proofs are retained. Two superseded
native trial runs failed only in their subsequent exact-clip GIF selection;
their transfer/export assertions were re-audited independently, and their failed
whole-run receipts are not counted as passing. Earlier GIFs that selected a
costume companion container are excluded.

The ignored local `SonicPreview36` harness links the current background helpers
and selects the exact project-owned animation container. Its `--port` path uses
the real library UI, original Next, verified export and project Save/reopen;
its read-only path loads an explicitly identified native animation. Capture uses
the existing serialized private desktop and shared offscreen renderer. Frame
receipts, decoded GIF timings/hashes, labelled six-sample overview sheets and
full-size endpoint PNGs document the visual review. Original source files remain
read-only; no active-desktop windows, screen copying or OS input injection is used.

These are observed original-Next/native-workflow results and bounded fitting
findings. Physical input, human usability, monitor DPI, broad contact quality,
arbitrary-character generalization and actual game acceptance remain unverified.
No new adapter, solver, project format or persistent state is introduced.
