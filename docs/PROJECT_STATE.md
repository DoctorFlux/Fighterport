# Fighterport project state

Active task ID: TASK-RETARGET-NONIDENTICAL-001

Parent milestone: G1/M2, incomplete

Last accepted **product** revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (preview.96)

Latest tested **code** checkpoint: `dda9d2eff162016fb0d9456ed9299cd1dfade250` (reviewed cranial mapping; not another accepted preview)

This checkpoint began from clean public `DoctorFlux/Fighterport/main` at `b0044905d0302a5af1e082566fc674c0b9a37000`. Recheck HEAD/status/diff when resuming. Read [AGENTS.md](../AGENTS.md), the [current task](plans/current.md), relevant [feature records](FEATURE_STATUS.json), [approved requirements](MASTER_PLAN.md), and the [dated cranial receipt](RETARGET_CRANIAL_REVIEW_CHECKPOINT_2026-11-03.md). The [repository-memory setup](PROJECT_MEMORY_ACCEPTANCE_2026-11-03.md) is documentation evidence, not animation acceptance.

## Accepted scope and baseline

Fighterport keeps its complete BrawlCrate Model Previewer, original Next retargeter, native editor and one Undo owner. Only **Melee, Brawl, Ultimate and Rivals of Aether 2** are destinations and intended sources; BT3 and other approved games remain source-only. The [generated support table](FORMAT_SUPPORT.md) separates game/build/direction/asset/channel/operation evidence. Approved requirements remain `REQ01`–`REQ21`.

Preview.96 fixed an identical-rig Peach `AttackS3S` loss of 51 keyed native entries and 7,290 keys before encoding. Its native copy, reopened samples and 15/15 private-desktop UI checks are [historical bounded evidence](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md), not changed-rig acceptance. Human input and actual game playback remain unverified.

## Current result and exact next action

`TASK-RETARGET-NONIDENTICAL-001` links `FP-RETARGET-QUALITY` to `REQ04/05/08/10/13/14`. Authentic Sonic `AttackS3S` → Mario exposed a mapping defect: an animated quill branch had been classified as Sonic's head. At `dda9d2e`, a reviewed **neck-local cranial skin point** represents the central head region without creating a bone or applying neck motion twice. Mario's real head keeps its own bind-local transform under the transferred neck. The UI shows skin and child-branch evidence plus an exact rig/skin digest, requires explicit session review and rejects stale evidence. A narrow head-only source case has procedural coverage. Target-side merged head/neck and one-versus-two shoulder chains remain unqualified.

The native Sonic→Mario result serializes and freshly reopens. Its 44-frame CHR0 is 283,752 bytes. Existing checks pass 474/474 trajectory samples, 345 supported-contact samples and exact protected root-local motion; 33/33 virtual-cranial, 94/94 coordinate-frame and 32/32 branched-head procedural checks pass. A 10/10 private-desktop Skeleton Mapping case passed, with actual PNGs inspected. An offscreen orthographic comparison captured and inspected 87 half-frame samples. These are bounded mapping, representation and supported-contact results, **not** a full-quality accepted port.

The all-vertex witness finds a fully `HatN`-weighted target vertex **3.79684 native units below the target bind-plane guide** at frame 5; source whole-mesh minimum there is 1.52706 units below its own guide. Inspected side frames show Mario's head, hat and body crossing the guide around frames 5–12. There is no reviewed hat/floor contact then. A foot-only pass or visual guide cannot establish gameplay collision quality.

**Exact next executable action:** measure frame-5 source and target head, hat and body skin geometry relative to both rigs and reviewed floor context. Determine whether extra target crossing begins in mapping, body-proportion goals, solving or source action intent; freeze source-relative expectations before a general correction. Retest the exact reopened result, whole-mesh/support/root checks and private-desktop side view without changing binds, weights, root policy or gates. Then add a reviewed split/merged cranial and shoulder chain-composition contract with bidirectional unequal-rig tests. Never map one physical joint twice or infer a head from quills.

## Files, fixtures and checks

- Code: `BrawlCrate/Studio/Retargeting/BrawlRetargeter.Cranial.cs`, `BrawlRetargeter.CranialReview.cs`, `BrawlRetargeter.cs`, `Rig.cs`, `HumanoidRetargeter.cs`, `SkeletonCharacterizer.cs`, and `BrawlCrate/UI/ModelForm.SkeletonMapping.cs`.
- Tests: `tests/RetargetAcceptance/VirtualCranialAnchorChecks.cs`, `ReviewedCranialRetargetCase.cs`, `BranchedHeadCharacterization.cs`, `TransferWitness.cs`, and ControlsAcceptance `--workspace-cranial-review`. Contracts and previous revisions: [automatic retargeting](engine/AUTOMATIC_RETARGETING.md), [engine boundary](ENGINE_INTEGRATION.md), [head guard](RETARGET_HEAD_GUARD_CORRECTION_2026-11-03.md), [current receipt](RETARGET_CRANIAL_REVIEW_CHECKPOINT_2026-11-03.md).
- Exact private hashes/member are in the dated receipt. Ignored outputs remain under `../../artifacts/Fighterport-cranial-*` and `../../artifacts/Fighterport-reviewed-cranial-*`. Verify hashes before reuse. No commercial assets, captures, absolute private paths or binaries enter Git.
- **Passed on `dda9d2e`:** fresh native application and x86 harness builds; scoped procedural, authentic native copy/bake/reopen, trajectory/support/root, private-desktop UI and inspected side-view checks above. The native build initially hit sandbox Windows SDK registry denial, then passed with escalation.
- **Failed/limited:** whole-target-mesh head/hat guide crossing; no accepted whole-body clearance/contact quality. The UI case checks mapping review, not a full user-driven Use/import path.
- **Not run/qualified:** broad rigs/actions, target-side merged head/neck, split shoulders, physical input/human usability, monitor DPI, external editor, continuous-time proof and actual game runtime. The local .NET 10 SDK is absent, so no fresh owned-engine build was claimed. No full Melee, optimizer or all-workflow suite was rerun. No accepted product preview advanced beyond preview.96.

Documentation checks for this handoff are recorded in the dated receipt after ledger/support-table regeneration. They validate repository memory, not animation quality.
