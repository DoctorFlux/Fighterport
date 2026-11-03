# Repository memory setup — documentation/tooling check

Starting `DoctorFlux/Fighterport/main`: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (preview.96). The checkout was clean before this increment. No application, solver, codec, renderer, native writer or launcher code changed. This record accepts only the repository-memory structure and its consistency checks; the last verified **application** revision remains preview.96.

The complete prior `AGENTS.md` was read in bounded chunks alongside the prior master/current plans, referenced specification, status files, relevant implementation/tests and [preview.96 acceptance](RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md). Exact prior documents are indexed and preserved in [`history/`](history/). The active bootstrap is under 6 KiB, and the current state/task are short. `MASTER_PLAN.md` retains `REQ01`–`REQ21`, four destination games and source-only donor policy; the ledger links all 21 approved IDs to 31 features and 66 scoped capabilities. `FORMAT_SUPPORT.md` is generated from the ledger. The current task has one stable ID, `TASK-RETARGET-NONIDENTICAL-001`.

## Actual checks on the documentation/tooling diff

| Command/check | Result |
| --- | --- |
| `python tests/test_project_memory.py` | 12/12 positive and negative cases pass. |
| `python scripts/Check-ProjectMemory.py --generate-support` | Pass; generated the support view from the ledger. |
| `python scripts/Check-ProjectMemory.py` | Pass; 21 requirements, 31 features, 66 capabilities, active task agreement. Historical requirement IDs were checked against preview.96 Git content. |
| Read-only fresh-context subagent recovery | Recovered product direction, preview.96 boundary, local prerequisites, active task and next action from repository files. It identified a requirement-link mismatch and broken retargeting-contract path; both were corrected and rechecked. This simulates recovery; it is **not** a separate new user Codex session. |
| Local prerequisite inspection | Ignored private retarget corpus, preview.96 runner/app and local .NET tool were present; file hashes and product tests were not rerun. |

The first committed feature ledger establishes the comparison baseline for later feature/capability ID retirement checks. Before this commit there was no ledger to compare. The validator checks documented structure and references, not animation quality, future agent compliance, actual game acceptance or human usability. Preview.96 numerical/native/UI passes remain historical evidence for its named fixtures only. Native builds, retarget/fitting/optimizer runs, screen captures, physical input, external-tool import and game-runtime tests were **not run** for this documentation increment.

Next product action: run the exact unequal-rig `Link-Samus-AttackS3S` reproduction in [`plans/current.md`](plans/current.md), then capture source/mapping/solver/native/skin/contact witnesses and fix the first demonstrated general cause. Keep the private inputs and generated outputs ignored.
