# Fighterport project state

Active task ID: TASK-RETARGET-NONIDENTICAL-001

Parent milestone: G1/M2, incomplete

Last verified **application** revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (`preview.96`)

Latest accepted product increment: [guarded exact-rig retarget preservation](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md)
Current product-work starting checkout: clean `main` at `ede8f9623de475c5fe26e77e3b34c97bc2061d4c`. The active code/test changes are a bounded ambiguous-head mapping guard and a read-only stage witness; this is **not** another accepted preview or a universal quality claim. Its [dated checkpoint](RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md) pins actual evidence. The earlier repository-memory setup is recorded in [its own receipt](PROJECT_MEMORY_ACCEPTANCE_2026-11-03.md).

Memory setup evidence: [3 November documentation/tooling check](PROJECT_MEMORY_ACCEPTANCE_2026-11-03.md).

Read [`../AGENTS.md`](../AGENTS.md), the [one current task](plans/current.md), relevant [`FEATURE_STATUS.json`](FEATURE_STATUS.json) records and the [approved master scope](MASTER_PLAN.md) after checking actual Git HEAD/status/diff. This state is a handoff, not a substitute for the contracts or dated receipts. A future commit after this setup is a new checkpoint; it does **not** imply the preview.96 native tests were rerun.

## Accepted baseline and scope

Fighterport is the public DoctorFlux/Fighterport BrawlCrate-based Next application, retaining the full native Model Previewer, original Next retargeter and one native editor/Undo owner. The only destination games are **Melee, Brawl, Ultimate and Rivals of Aether 2**; all four are also intended sources. BT3 and other approved titles remain **source-only**. Capability status is per title/build/direction/asset/channel/operation in the ledger and generated [support table](FORMAT_SUPPORT.md). Product requirements remain `REQ01`–`REQ21` in `MASTER_PLAN.md`.

Preview.96 demonstrated that Peach `AttackS3S` on an **identical** Peach rig lost 51 keyed native entries and 7,290 keys in the role-only mapping before CHR0 encoding. The guarded path retains authored tracks in an independent copy after exact-rig admission, then serializes and freshly reopens. The dated record reports zero measured local/world/skin/contact/source-control error for Peach `AttackS3S` (411 samples), Mario `AttackAirHi` (331) and Peach `AttackAirN` (491), plus bounded Peach↔Fox `AttackS3S` trajectory results and 15/15 private-desktop UI checks. The same-rig fix is **VERIFIED_BOUNDED**. Those are historical committed passes, **not tests rerun during this memory setup**. Similar-looking rigs, explicit Grounded/SourceContacts correction and arbitrary unequal-rig actions still use their existing paths and gates.

## Current task and next action

The active task is [non-identical-rig retarget quality](plans/current.md), linked to `FP-RETARGET-QUALITY` and `REQ04/05/08/10/13/14`. Capture one reproducible non-idle failure with exact source, mapping, solver, native-output, skin and contact witnesses; fix the first demonstrated **general** cause without character/action hardcodes or changed accuracy limits. Do not repeat the accepted exact-rig case as the next increment.

**Exact next executable action:** implement a reviewed, structural/skin-backed cranial-parent policy for a source whose neck carries several head branches but no distinct central head joint. Freeze source-to-target head and hand-motion expectations on exact Sonic `AttackS3S` → Mario, then rerun that authentic case with the existing stage/whole-skin witness and the unaffected Link↔Samus/root/native regressions. After safe mapping, address whole-body clearance separately using source-intent and support context. Do not map an appendage tip as the head or assign `head=neck` without handling shared local translation/scale.

## Relevant files and local prerequisites

- Retarget implementation: `BrawlCrate/Studio/Retargeting/SkeletonCharacterizer.cs`, `HumanoidRetargeter.cs`, `BrawlRetargeter*.cs`, `Rig.cs`, `RetargetKeyframes*.cs` and contact files. Quality harness: `tests/RetargetAcceptance/TransferWitness.cs`, `BranchedHeadCharacterization.cs`, `NativeTransferCase.cs`, `CorrectionTrajectoryChecks.cs`; `scripts/Build.ps1`, `scripts/Test-BackgroundCapture.ps1`.
- Detailed gates: `docs/engine/AUTOMATIC_RETARGETING.md`, `docs/ENGINE_INTEGRATION.md`, dated acceptance records. Exact fixture identities and hashes are in the ignored local corpus/receipts, not public Git.
- Ignored local fixtures include the exact Link↔Samus corpus and the Sonic→Mario source/member/target manifest described in the dated checkpoint. The changed native build and local receipts are under ignored `artifacts/Fighterport-quality-headguard-*`. Recheck hashes and availability before rerunning. Commercial asset bytes, captures and absolute private paths stay out of commits.

## Open failures, limitations and gates

- The preview.96 record and this checkpoint do **not** establish general unequal-rig motion, all contacts, continuous-time equivalence, complete material/mesh equality, human usability, physical input/monitor DPI, destination-tool compatibility or actual game playback.
- Exact Sonic→Mario pre-guard motion has a wrongly inferred animated quill as `head`. The new guard safely refuses automatic output until mapping review/cranial proxy support exists. The same historical result has a whole-target-mesh floor crossing before CHR0 encoding; the source also crosses its own floor. Passing foot-patch/root/native gates do not qualify the full-body result. No fresh visual claim is made.
- Advanced 96.2 Apply and full contact/context/surface/moving-support work retain unresolved gates. Check the relevant dated records before changing those routes; missing authentic context must be reported rather than fabricated.
- Melee native output is partial; fractional-time HSD FOBJ and model writing need separate proof. Ultimate and Rivals 2 native adapters are not qualified. BT3 native game output is outside the four-destination policy. Portable interchange and filesize optimization have bounded existing work, not universal format support.
- Keep the editable master, native Undo, source hashes, target binds/weights and protected source-authored root motion intact. No character/action-specific numerical fixes.

## Current checkpoint and actual checks

- **Starting Git state:** clean `main` at `ede8f962`; reconciled preview.96 plus the committed memory checkpoint. No unrelated work was reset. The 3 November [unequal-rig checkpoint](RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md) records exact private fixture hashes and pre/post-guard results.
- **Completed:** a read-only native stage witness now measures exact source identity, source-native sampling, mapping, raw/contact solver, whole skin floor minima and same-finished-target versus reopened CHR0. An ambiguous branched-head inference now requires review instead of mapping Sonic's animated quill to Mario's native head. This changes no root/solver tolerance, target bind/weights, writer or viewer.
- **Passed on changed code:** fresh native application and harness builds; 20 renamed/permuted branched-head procedural assertions; 94 coordinate-frame assertions; six ordinary Brawl model characterizations; expected exact Sonic mapping refusal with no output; Link↔Samus `AttackS3S` exact-member full-quality/default reopen and guarded trajectory oracles in both directions; wrong-direction manifest rejection; source/native ownership and protected root checks; `git diff --check`. Documentation tests passed 12/12 and the memory validator passed 21 requirements, 31 features and 66 capabilities.
- **Failed/limited observation:** before the guard, the exact Sonic source whole-mesh minimum was -2.095862 native units against its own floor, target raw/corrected was -2.874432 and the finished/reopened native result was -2.874385 at finite checked times. The target floor crossing predates CHR0 encoding. Source intent and absolute collision acceptance are unresolved; the guard does not repair body clearance or produce a usable Sonic target animation.
- **Not run for this code checkpoint:** fresh visual/offscreen inspection, human/physical input, monitor DPI, external-editor import, actual game runtime and broad action/contact quality. Full melee/optimizer/workflow suites were not rerun. Repository-memory checks validate documents, not motion.
- **Partial work to carry forward:** the active task remains open. A safe shared-neck/head or reviewed cranial mapping and independent head/hand motion witness are its next dependency. After that, qualify whole-body floor behavior and a corrected native/displayed result without loosening gates. No arbitrary cross-proportion acceptance is claimed.
