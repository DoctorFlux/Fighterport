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

### Actual command pattern and local evidence

From the checkout, the current native build and exact-member tests use the ignored fixture manifest and a **fresh** ignored output directory. The authentic input hashes, member identity, actual runs and findings are in [the 3 November unequal-rig checkpoint](../RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md). Do not publish the manifest's private paths or generated CHR0s.

```powershell
$workspace = (Resolve-Path ../..).Path
$native = Join-Path $workspace 'artifacts/Fighterport-quality-headguard-build/native'
& scripts/Build.ps1 -NativeOnly -NativeDirectory $native
& <VS2022-MSBuild> tests/RetargetAcceptance/RetargetAcceptance.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false "/p:OutDir=$native\" /nologo /v:minimal
& (Join-Path $native 'RetargetAcceptance.exe') --branched-head-characterization <fresh-ignored-output>
& (Join-Path $native 'RetargetAcceptance.exe') --retarget-case <source-model> <target-model> <motion> <exact-fixture> <fresh-ignored-output>
& (Join-Path $native 'RetargetAcceptance.exe') --verify-trajectory <source-model> <target-model> <motion> AttackS3S <saved-chr0> <fresh-ignored-output> <exact-fixture>
```

The historical Sonic→Mario master/default outputs were generated **before** the mapping guard on the same starting application revision and retained for stage comparison. The post-guard exact Sonic run must refuse the ambiguous automatic head role before output. Link↔Samus post-guard runs qualify only their named-head and existing contact/native cases. Do not treat a refused Sonic clip or a reopened CHR0 as accepted motion.

## Progress and exact next step

- **Accepted historical baseline:** preview.96 guarded identical-rig native track retention; Peach `AttackS3S`, Mario `AttackAirHi`, Peach `AttackAirN` and the recorded Peach/Fox pairs passed their bounded checks. These were **not rerun** by the memory setup.
- **Current task progress:** Link↔Samus `AttackS3S` passed fresh exact-member native/contact checks in both directions. The exact Sonic→Mario `AttackS3S` case reproduced an early whole-body floor crossing before CHR0 writing; its source already crosses its floor, and its target crossing is larger. The read-only stage harness now distinguishes source, mapping, raw/corrected solver, finished master and reopened native skin values. Sonic's automatic head role was demonstrably an animated quill branch; the general ambiguity guard refuses that mapping rather than producing a wrongly driven target head. See the dated checkpoint for hashes, finite coverage and numbers. This does **not** fix shared-neck/head anatomy or whole-body clearance.
- **Repository-memory checkpoint:** 12 documentation validator tests and 21/31/66 ledger coverage checks passed; a fresh-context read-only recovery simulation found the task and exposed two links that were corrected. These checks do not advance the retarget-quality acceptance gate.
- **Checks passed on the changed code:** native build; scoped harness build; 20 branched-head procedural assertions; 94 coordinate-frame checks; six existing Brawl rig captures; exact Sonic ambiguity refusal without output; Link↔Samus native/full-quality/default reopen and guarded trajectory oracles; wrong-direction exact manifest rejection; `git diff --check`; 12 documentation-memory tests and 21/31/66 ledger checks. Existing fixtures and outputs remain ignored. These latter checks do not test animation quality.
- **Failed/limited observations:** the historical Sonic target reaches -2.874385 native units below its reviewed floor after fresh native reopen, versus -2.095862 in the source's own floor coordinates; foot-patch checks still pass. The source itself penetrates, so this is not a standalone proof of an unacceptable absolute floor criterion. The quill-as-head mapping is independently wrong and now refused. Finite diagnostic samples do not prove continuous motion or visual/game acceptance. No fresh visual, human, external-editor or runtime check was run.
- **Dependency:** Sonic's skeleton has no automatically established distinct central head joint. A safe shared-neck/head or reviewed cranial policy is needed before automatic porting can resume; assigning the neck to both roles without a qualified transfer rule may duplicate scale/translation. Whole-body clearance/source intent remains a separate unresolved quality gate.
- **Next unfinished action:** implement a general reviewed cranial-parent mapping/solver policy for rigs with branching head geometry, using structural and complete skin-influence evidence; freeze a source-to-target head/hand motion witness on the exact Sonic fixture. Rerun the exact case and Link↔Samus/native/root regressions. Then use the new full-mesh witness to address source-relative body clearance without inventing a floor contact or weakening foot/contact gates. Keep this task open until a corrected non-identical-rig output and inspected offscreen result qualify.
