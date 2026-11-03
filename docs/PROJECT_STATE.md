# Fighterport project state

Active task ID: TASK-RETARGET-NONIDENTICAL-001

Parent milestone: G1/M2, incomplete

Last accepted **product** revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (preview.96)

Latest tested **code** checkpoint: `601466c600d4ab868ebe9331842670ad74a3617f` (head-guard correction; not another accepted preview)

At this handoff, code commit `601466c` followed clean `main` at `7eefe96cdfaa9069db5aae5592ea99062c48ead1`. The preceding [unequal-rig checkpoint](RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md) retains its own earlier revision and evidence; the [new correction receipt](RETARGET_HEAD_GUARD_CORRECTION_2026-11-03.md) pins what was actually tested on `601466c`. Recheck actual HEAD/status/diff when resuming. The [repository-memory setup](PROJECT_MEMORY_ACCEPTANCE_2026-11-03.md) is documentation/tooling evidence, not animation acceptance.

Read [`../AGENTS.md`](../AGENTS.md), [current task](plans/current.md), relevant [`FEATURE_STATUS.json`](FEATURE_STATUS.json) entries and [approved requirements](MASTER_PLAN.md). This short handoff does not replace the subsystem contracts or dated receipts.

## Accepted scope and baseline

Fighterport is the public DoctorFlux/Fighterport BrawlCrate-based Next application. Keep the complete native Model Previewer, original Next retargeter and one native editor/Undo owner. Only **Melee, Brawl, Ultimate and Rivals of Aether 2** are destinations and intended sources; BT3 and other approved titles remain source-only. Per-title/build/direction/asset/channel/operation evidence lives in the ledger and generated [support table](FORMAT_SUPPORT.md). Approved requirements remain `REQ01`–`REQ21`.

Preview.96 fixed an identical-rig Peach `AttackS3S` loss of 51 keyed native entries and 7,290 keys before encoding. Its guarded native copy, reopened Peach/Mario samples and 15/15 private-desktop UI checks are [historical bounded evidence](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md), not universal changed-rig acceptance. Human input, actual game playback and all contact contexts remain open.

## Current task, result and exact next action

`TASK-RETARGET-NONIDENTICAL-001` links `FP-RETARGET-QUALITY` to `REQ04/05/08/10/13/14`. The authentic non-idle Sonic `AttackS3S` → Mario case demonstrates a **mapping** defect: Sonic's former automatic `head` was an animated quill branch, not a central head joint. Its historical target also crosses its reviewed floor before CHR0 encoding; the source crosses its own floor. A safe refusal is progress, not a usable port.

The latest correction retains a provisional head candidate internally so identical-rig admission can run, but cached/saved mappings cannot promote that candidate into an unequal-rig solve. Synthetic Sonic exact-copy/reopen passes; authentic Sonic `AttackS3S` and `Wait1` exact copies reach a separate existing **nonzero CHR0 scaling-rule** preservation rejection. Sonic → Mario remains refused before writing. A new read-only native skin witness captures eight neck/direct-influence groups at 12 times: 4,219 majority-influenced vertices belong to direct neck skin versus about 37–50 on each moving quill branch. This is evidence for a possible cranial anchor, not a qualified role or transfer policy.

**Exact next executable action:** add an explicit reviewed structural/skin-backed **virtual cranial anchor** contract (neck joint plus reviewed bind-local skin point/radius), keeping `head` absent from joint FK mapping and source neck motion mapped only once. Use that anchor for head/hand goals and skin patch selection. Freeze positive/negative renamed and storage-permuted tests plus source-to-target head/hand motion expectations on exact Sonic `AttackS3S` → Mario; rerun native reopen/re-save, full-skin/contact and inspected private-desktop side-view checks. Do not use `head=neck` or a quill tip as a shortcut. Address source-relative whole-body clearance after mapping. The authentic Sonic scaling-rule copy gate is a separate metadata-preservation dependency; do not discard its field to force a pass.

## Relevant implementation, fixtures and constraints

- Retarget code: `BrawlCrate/Studio/Retargeting/SkeletonCharacterizer.cs`, `Rig.cs`, `BrawlRetargeter*.cs`, `HumanoidRetargeter.cs`, contact/trajectory and native metadata code; UI mapping and provenance in `BrawlCrate/UI/ModelForm.SkeletonMapping.cs`. Harness: `tests/RetargetAcceptance/BranchedHeadCharacterization.cs`, `TransferWitness.cs`, `SameRigHelperOnly.cs`, `SameRigQualityCase.cs`, `tests/ControlsAcceptance/WorkspaceSameRigRetarget.cs`.
- Contracts: `docs/engine/AUTOMATIC_RETARGETING.md`, `docs/ENGINE_INTEGRATION.md`, [preview.96 acceptance](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md), [first unequal-rig checkpoint](RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md) and [correction receipt](RETARGET_HEAD_GUARD_CORRECTION_2026-11-03.md).
- Exact Sonic source model/motion/member and both Mario target container hashes are in the dated receipts. Ignored private fixtures and generated results remain under `../../artifacts/Fighterport-quality-headguard-*`. Verify manifest and hashes before rerunning; commercial asset bytes, captures and absolute private paths never enter Git.
- Preserve target hierarchy/rest/inverse binds/weights, immutable sources, source-owned root controls, full original Next, frozen numeric/contact gates and native editor/Undo. No character/action-specific numerical route. The UI remains responsive and background tests avoid OS input injection and active-screen capture.

## Checks and limitations at code revision `601466c`

- **Passed:** fresh native application and retarget/ControlsAcceptance harness builds; 28/28 renamed/permuted branched-head assertions; synthetic six-frame Sonic helper exact-copy/reopen with maximum world error 0 and unchanged model/source bytes; exact Sonic→Mario refusal with no imported result; authentic Link→Samus and Samus→Link `AttackS3S` full-quality/default native reopen and unchanged protected controls/ownership; both exact-member trajectory oracles passed 430 samples with root-local error 0 and supported-patch penetration below their recorded bounds. The cranial witness covered 6,844 vertices across 12 native times. Private-desktop Mario `AttackAirHi` Use on This Model/native edit/Undo/Redo/Save/reopen passed 15/15; its actual PNG was inspected.
- **Failed/limited:** authentic Sonic same-rig `AttackS3S` and `Wait1` stop at the native snapshot's nonzero scaling-rule gate, leaving no output. Historical Sonic→Mario whole-target-mesh minimum after native reopen was -2.874385 native units; source minimum was -2.095862 against its own floor. The current guard stops this case before an output. The private-desktop screenshot is overhead and proves no side-view/contact quality. The local .NET 10 SDK is absent, so fresh engine build failed; successful native UI test used an unchanged existing engine host. Initial sandbox private-desktop creation was denied, then a fresh build lacked the host; the final escalated private-desktop run passed.
- **Not run/qualified:** corrected Sonic→Mario motion, complete head/hand/whole-skin/contact acceptance, fresh side-view quality, continuous-time proof, broad actions/rigs, human physical input, monitor DPI, external editor and game runtime. No full Melee, optimizer or all-workflow suite was rerun. No accepted product preview advanced beyond preview.96.

Documentation checks on the handoff: `python tests/test_project_memory.py` passed 12/12; `python scripts/Check-ProjectMemory.py --generate-support` and its check mode passed 21 requirements, 31 features, 66 capabilities and the one active task. `git diff --check` passed after removing accidental Markdown trailing spaces. These checks validate documentation consistency, not animation correctness.
