# Fighterport project state

Active task ID: TASK-RETARGET-NONIDENTICAL-001

Parent milestone: G1/M2, incomplete

Last verified **application** revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (`preview.96`)

Latest accepted product increment: [guarded exact-rig retarget preservation](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md)
Current documentation/tooling checkpoint: committed repository memory setup on `main` based on `041ec87`; no application behavior changed.

Memory setup evidence: [3 November documentation/tooling check](PROJECT_MEMORY_ACCEPTANCE_2026-11-03.md).

Read [`../AGENTS.md`](../AGENTS.md), the [one current task](plans/current.md), relevant [`FEATURE_STATUS.json`](FEATURE_STATUS.json) records and the [approved master scope](MASTER_PLAN.md) after checking actual Git HEAD/status/diff. This state is a handoff, not a substitute for the contracts or dated receipts. A future commit after this setup is a new checkpoint; it does **not** imply the preview.96 native tests were rerun.

## Accepted baseline and scope

Fighterport is the public DoctorFlux/Fighterport BrawlCrate-based Next application, retaining the full native Model Previewer, original Next retargeter and one native editor/Undo owner. The only destination games are **Melee, Brawl, Ultimate and Rivals of Aether 2**; all four are also intended sources. BT3 and other approved titles remain **source-only**. Capability status is per title/build/direction/asset/channel/operation in the ledger and generated [support table](FORMAT_SUPPORT.md). Product requirements remain `REQ01`–`REQ21` in `MASTER_PLAN.md`.

Preview.96 demonstrated that Peach `AttackS3S` on an **identical** Peach rig lost 51 keyed native entries and 7,290 keys in the role-only mapping before CHR0 encoding. The guarded path retains authored tracks in an independent copy after exact-rig admission, then serializes and freshly reopens. The dated record reports zero measured local/world/skin/contact/source-control error for Peach `AttackS3S` (411 samples), Mario `AttackAirHi` (331) and Peach `AttackAirN` (491), plus bounded Peach↔Fox `AttackS3S` trajectory results and 15/15 private-desktop UI checks. The same-rig fix is **VERIFIED_BOUNDED**. Those are historical committed passes, **not tests rerun during this memory setup**. Similar-looking rigs, explicit Grounded/SourceContacts correction and arbitrary unequal-rig actions still use their existing paths and gates.

## Current task and next action

The active task is [non-identical-rig retarget quality](plans/current.md), linked to `FP-RETARGET-QUALITY` and `REQ04/05/08/10/13/14`. Capture one reproducible non-idle failure with exact source, mapping, solver, native-output, skin and contact witnesses; fix the first demonstrated **general** cause without character/action hardcodes or changed accuracy limits. Do not repeat the accepted exact-rig case as the next increment.

**Exact next executable action:** with a fresh current native build and an ignored new evidence directory, run the `Link-Samus-AttackS3S` `--retarget-case` command in `plans/current.md`; record pre-encode and post-reopen witnesses. If it passes, retain that result and select another unqualified unequal-rig member. A fixture name selects evidence, never a special numerical route.

## Relevant files and local prerequisites

- Retarget implementation: `BrawlCrate/Studio/Retargeting/BrawlRetargeter*.cs`, `Rig.cs`, `RetargetKeyframes*.cs`, trajectory/contact files. Quality harness: `tests/RetargetAcceptance/Program.cs` and related checks; `scripts/Build.ps1`, `scripts/Test-BackgroundCapture.ps1`.
- Detailed gates: `docs/engine/AUTOMATIC_RETARGETING.md`, `docs/ENGINE_INTEGRATION.md`, dated acceptance records. Exact fixture identities and hashes are in the ignored local corpus/receipts, not public Git.
- At this checkpoint, the enclosing workspace had the ignored `artifacts/Fighterport-attacks655-20261101/corpus.json`, `artifacts/Fighterport-quality96-build/native/RetargetAcceptance.exe`, `FighterPortStudioV2.exe`, preview.96 receipts and `.tools/dotnet/dotnet.exe`. Recheck availability and hashes before running. Commercial asset bytes, captures and absolute private paths stay out of commits.

## Open failures, limitations and gates

- The preview.96 record does **not** establish general unequal-rig motion, all contacts, continuous-time equivalence, complete material/mesh equality, human usability, physical input/monitor DPI, destination-tool compatibility or actual game playback.
- Advanced 96.2 Apply and full contact/context/surface/moving-support work retain unresolved gates. Check the relevant dated records before changing those routes; missing authentic context must be reported rather than fabricated.
- Melee native output is partial; fractional-time HSD FOBJ and model writing need separate proof. Ultimate and Rivals 2 native adapters are not qualified. BT3 native game output is outside the four-destination policy. Portable interchange and filesize optimization have bounded existing work, not universal format support.
- Keep the editable master, native Undo, source hashes, target binds/weights and protected source-authored root motion intact. No character/action-specific numerical fixes.

## Checkpoint and checks for this documentation increment

- **Completed:** inspected clean `main` at `041ec87` before edits; read the complete old `AGENTS.md` in bounded chunks, master/current plans, reference specification, status files, relevant code/tests and preview.96 receipt; archived the original long files under `docs/history/` before shortening active entrypoints. Confirmed the private corpus, preview.96 native runner and local .NET tool existed at this checkpoint.
- **Documentation/tooling checks passed:** `python tests/test_project_memory.py` (12/12 positive/negative cases), `python scripts/Check-ProjectMemory.py --generate-support`, and `python scripts/Check-ProjectMemory.py` (21 approved requirements, 31 linked features, 66 scoped capabilities; active task agrees). This first committed ledger establishes the baseline for future feature-ID removal checks. The validator checks consistency, not animation correctness.
- **Fresh-context recovery simulation:** a separate read-only subagent used only repository files and correctly recovered the four destinations/source-only policy, preview.96's exact-rig boundary, active task, local prerequisites and exact next action. It found a task/feature link mismatch and a broken retargeting-contract link; both were corrected and its follow-up read confirmed the fixes. This was not a separate new user Codex session or a native test.
- **Not run for this increment:** native app build, retarget/fit/optimizer tests, offscreen capture, physical input, external-tool import and game-runtime tests. The preview.96 acceptance record remains historical evidence for its own revision.
- **Partial work to carry forward:** the next product task has no new failure witness or solver change. Continue exactly at the command in `plans/current.md`, then checkpoint actual observations and changed files.
