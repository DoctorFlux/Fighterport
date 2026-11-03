# Current bounded task — non-identical-rig retarget quality

Task ID: TASK-RETARGET-NONIDENTICAL-001

Parent: incomplete G1/M2

Starting verified application revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (preview.96)

Linked requirements: `REQ04`, `REQ05`, `REQ08`, `REQ10`, `REQ13`, `REQ14`
Linked feature: `FP-RETARGET-QUALITY`

The repository memory setup is a documentation/tooling checkpoint. It does not itself qualify another animation result. The latest accepted product increment is the guarded exact-rig preservation route in [preview.96](../RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md). That fix must not be repeated as a substitute for the task below.

## Objective and acceptance boundary

Find one **reproducible real non-identical-rig** retarget-quality failure, then fix its first demonstrated general cause if the evidence identifies one. Trace native source interpretation → authentic rig/bind data → reviewed role/joint mapping → solver/body/contact goals → generated local curves → serialized/freshly reopened native result → skinned/contact motion. Preserve original Next, target hierarchy/rest/inverse binds/weights, source-owned root motion, all represented skin influences and existing frozen quality gates. No character/action hardcodes or accuracy relaxation.

Completion of this one task requires a pinned fixture and before/after witness showing the original failure, the cause, a general code change, independent numeric checks, fresh native reopen/re-save, unaffected-regression coverage and actual inspected background captures where visual behavior is claimed. A measured solution may instead prove missing context or unsupported anatomy; record that honestly and leave the task open or narrow the next action. A partial pass cannot qualify all cross-proportion motion, contacts or game runtime.

## Scope and method

1. Inspect current HEAD/status/diff and the linked feature record. Verify exact private native members and hashes without copying commercial bytes to Git. Start with a non-idle unequal-rig pair in the ignored `artifacts/Fighterport-attacks655-20261101/corpus.json`; Link→Samus and Samus→Link `AttackS3S` are present and have not been accepted by preview.96's paired quality receipt. Use another exact pair if these pass without a failure. Keep fixture names out of numerical dispatch.
2. Reproduce the result with the current native harness and record local/source/mapping/solver/final-native/skin/contact values at integer, existing quarter-frame, boundary and withheld fractional times. Compare against the **same full-quality finished target** for serialization fidelity; compare absolute action/contact gates separately. Preserve complete original/target rig and weight invariants. Explain whether failure begins in source interpretation, mapping, solving, generated curves, writer, or viewer presentation. Do not blame the codec when the pre-encode result already differs.
3. Make the smallest general correction at the first proven failing boundary. Add a permanent procedural/renamed/storage-permuted regression and the authentic case with frozen expectations. Do not alter unrelated app, optimizer or donor adapters.
4. Run relevant native/engine tests, fresh export→reopen→re-save and background offscreen UI capture. Inspect images and retain failed/untested cases. Update dated acceptance, `FEATURE_STATUS.json`, `PROJECT_STATE.md` and this task at checkpoints, then publish source only after its own qualification.

## Relevant code, tests and contracts

- `BrawlCrate/Studio/Retargeting/BrawlRetargeter.cs`, `BrawlRetargeter.SharedBones.cs`, `BrawlRetargeter.ExactRig.cs`, `Rig.cs`, `RetargetKeyframes*.cs` and contact/trajectory files.
- `tests/RetargetAcceptance/Program.cs`, `CorrectionTrajectoryChecks.cs`, `MappingContactChecks.cs`, `SameRigQualityCase.cs`; `tests/ControlsAcceptance/WorkspaceSameRigRetarget.cs` for native workflow protection.
- `scripts/Test-RetargetQualityPairs.ps1`, `scripts/Test-BackgroundCapture.ps1`, `scripts/Build.ps1`.
- [Automatic retargeting contract](../engine/AUTOMATIC_RETARGETING.md), [engine boundary](../ENGINE_INTEGRATION.md), [preview.96 evidence](../RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md).

### Actual command starting point

The following is a **next-task command**, not a test run during the memory setup. From the checkout, with the same ignored local tool/fixture arrangement as preview.96:

```powershell
$workspace = (Resolve-Path ../..).Path
$manifest = Get-Content -LiteralPath (Join-Path $workspace 'artifacts/Fighterport-attacks655-20261101/corpus.json') -Raw | ConvertFrom-Json
$case = @($manifest.Cases | Where-Object Id -eq 'Link-Samus-AttackS3S')
if ($case.Count -ne 1) { throw 'Exact fixture unavailable' }
$native = Join-Path $workspace 'artifacts/Fighterport-quality96-build/native'
$output = Join-Path $workspace 'artifacts/Fighterport-quality-next-link-samus'
if (Test-Path -LiteralPath $output) { throw 'Use a new ignored evidence directory' }
& (Join-Path $native 'RetargetAcceptance.exe') --retarget-case $case[0].SourceModel $case[0].TargetModel $case[0].Motion $case[0].Fixture $output
```

This first reproduction needs added complete mapping/skin/contact witnesses before it can satisfy the task. The old preview.96 binary is a reference, not automatically the current build after a code edit. The exact local fixture and tools were present when this memory checkpoint was prepared; a future session must check again. Native build commands and tool versions are pinned in the preview.96 record. Use a fresh build for changed application code.

## Progress and exact next step

- **Accepted historical baseline:** preview.96 guarded identical-rig native track retention; Peach `AttackS3S`, Mario `AttackAirHi`, Peach `AttackAirN` and the recorded Peach/Fox pairs passed their bounded checks. These were **not rerun** by the memory setup.
- **Current task progress:** fixture availability inspected, one non-idle unequal-rig pair selected for first reproduction. No new product failure witness or solver change has yet been recorded for this task.
- **Repository-memory checkpoint:** 12 documentation validator tests and 21/31/66 ledger coverage checks passed; a fresh-context read-only recovery simulation found the task and exposed two links that were corrected. These checks do not advance the retarget-quality acceptance gate.
- **Known open limits:** arbitrary unequal-rig action/contact quality, complete game adapters, human use, physical input/DPI and actual game playback are unverified. Some contact/context and advanced Apply routes retain prior failures; consult their dated records.
- **Blockers:** none known for starting read-only reproduction; private fixtures remain local and must be rechecked. If the selected pair passes, use another unqualified real pair and record the no-failure result, not an invented failure.
- **Next unfinished action:** run the exact `Link-Samus-AttackS3S` native reproduction above on a fresh current binary and capture before-encode mapping/solver plus post-reopen skin/contact witnesses. This is the first action of the next product-work session.
