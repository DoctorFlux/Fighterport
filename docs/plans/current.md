# Current bounded task — non-identical-rig retarget quality

Task ID: TASK-RETARGET-NONIDENTICAL-001

Parent: incomplete G1/M2

Starting verified application revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (preview.96)

Latest tested code checkpoint: `601466c600d4ab868ebe9331842670ad74a3617f`, following clean `main` at `7eefe96cdfaa9069db5aae5592ea99062c48ead1`. This correction is not another accepted preview.

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
- `tests/RetargetAcceptance/Program.cs`, `CorrectionTrajectoryChecks.cs`, `MappingContactChecks.cs`, `BranchedHeadCharacterization.cs`, `TransferWitness.cs`, `SameRigHelperOnly.cs`, `SameRigQualityCase.cs`; `tests/ControlsAcceptance/WorkspaceSameRigRetarget.cs` for native workflow protection.
- `scripts/Test-RetargetQualityPairs.ps1`, `scripts/Test-BackgroundCapture.ps1`, `scripts/Build.ps1`.
- [Automatic retargeting contract](../engine/AUTOMATIC_RETARGETING.md), [engine boundary](../ENGINE_INTEGRATION.md), [preview.96 evidence](../RETARGET_QUALITY_ACCEPTANCE_2026-11-03.md), [first unequal-rig checkpoint](../RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md), [head-guard correction receipt](../RETARGET_HEAD_GUARD_CORRECTION_2026-11-03.md).

### Actual command pattern and local evidence

From the checkout, the current native build and exact-member tests use the ignored fixture manifest and a **fresh** ignored output directory. The authentic input hashes, member identity, actual runs and findings are in [the 3 November unequal-rig checkpoint](../RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md). Do not publish the manifest's private paths or generated CHR0s.

```powershell
$workspace = (Resolve-Path ../..).Path
$native = Join-Path $workspace 'artifacts/Fighterport-quality-headguard-build/native'
& scripts/Build.ps1 -NativeOnly -NativeDirectory $native
& <VS2022-MSBuild> tests/RetargetAcceptance/RetargetAcceptance.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false "/p:OutDir=$native\" /nologo /v:minimal
& (Join-Path $native 'RetargetAcceptance.exe') --branched-head-characterization <fresh-ignored-output>
& (Join-Path $native 'RetargetAcceptance.exe') --same-rig-helper-only <exact-source-model> <fresh-ignored-output>
& (Join-Path $native 'RetargetAcceptance.exe') --cranial-branch-witness <source-model> <motion> <exact-fixture> <fresh-ignored-output>
& (Join-Path $native 'RetargetAcceptance.exe') --retarget-case <source-model> <target-model> <motion> <exact-fixture> <fresh-ignored-output>
& (Join-Path $native 'RetargetAcceptance.exe') --verify-trajectory <source-model> <target-model> <motion> AttackS3S <saved-chr0> <fresh-ignored-output> <exact-fixture>
& scripts/Test-BackgroundCapture.ps1 -Model <exact-Mario-model> -Motion <exact-Mario-motion> -Clip AttackAirHi -WorkspaceSameRigRetarget -NativeDirectory $native -OutputDirectory <fresh-ignored-output>
```

The historical Sonic→Mario master/default outputs were generated **before** the mapping guard and retained for stage comparison. The current exact Sonic→Mario run must refuse ambiguous automatic head mapping before output. Link↔Samus post-guard runs qualify only their named-head and existing contact/native cases. Do not treat a refused Sonic clip or a reopened CHR0 as accepted motion. The latest correction's fixture hashes, successful and failed commands, native-host limitation and ignored receipt locations are pinned in its dated receipt.

## Progress and exact next step

- **Accepted historical baseline:** preview.96 guarded identical-rig native track retention; Peach `AttackS3S`, Mario `AttackAirHi`, Peach `AttackAirN` and the recorded Peach/Fox pairs passed their bounded checks. These were **not rerun** by the memory setup.
- **Current task progress:** Link↔Samus `AttackS3S` passed fresh exact-member native/contact checks in both directions on `601466c`. The exact Sonic→Mario `AttackS3S` case reproduced an early whole-body floor crossing **before** CHR0 writing; its source already crosses its floor and the target crossing is larger. The first stage harness distinguished source, mapping, raw/corrected solver, finished master and reopened native skin values. Sonic's automatic `head` role was demonstrably an animated quill branch; the general guard refuses an unequal-rig solve. The latest correction retains provisional anatomy only for complete exact-rig admission and prevents cached/replayed maps from treating it as reviewed. A new read-only eight-group/12-time native skin witness shows direct neck skin dominates the quill branches; it does not itself select an anatomical head.
- **Repository-memory checkpoint:** 12 documentation validator tests and 21/31/66 ledger coverage checks passed; a fresh-context read-only recovery simulation found the task and exposed two links that were corrected. These checks do not advance the retarget-quality acceptance gate.
- **Checks passed on `601466c`:** fresh native application and scoped retarget/ControlsAcceptance builds; 28/28 renamed/permuted branched-head assertions; synthetic Sonic helper-only exact-copy/reopen with zero maximum world error, unchanged sources/models and unavailable rather than invented contact evidence; exact Sonic→Mario refusal without output; Link↔Samus full-quality/default native reopen and 430-sample support/trajectory oracles in both directions, protected root-local error zero; native cranial witness across 6,844 vertices at 12 times. Private-desktop Mario `AttackAirHi` Use on This Model/native edit/Undo/Redo/Save/reopen passed 15/15, and its capture was inspected. Earlier 94 coordinate-frame checks, six Brawl captures and wrong-direction manifest rejection belong to the preceding checkpoint and were not rerun on `601466c`. All private fixtures/outputs remain ignored.
- **Failed/limited observations:** authentic Sonic same-rig `AttackS3S` and `Wait1` reach copy but fail the preexisting nonzero CHR0 scaling-rule preservation gate; no output/reopen pass. The historical Sonic target reaches -2.874385 native units below its reviewed floor after reopen, versus -2.095862 in source floor coordinates. Source penetration prevents treating this alone as a universal absolute floor failure. The quill-as-head mapping is wrong and refused. The fresh UI capture is overhead and supplies no contact/side-view claim. A fresh engine build failed because .NET 10 SDK is unavailable locally; the passing UI test used an unchanged existing engine host. Finite samples do not prove continuous motion or game acceptance.
- **Dependency:** `Rig`, `HumanoidRetargeter`, body-surface capture and UI review assume a real distinct `head` joint. Sonic lacks an automatically established one. Assigning `head=neck` can duplicate local motion and misclassify skin. The next policy needs an explicit reviewed virtual cranial anchor, not a duplicate joint role. Whole-body clearance/source intent and authentic Sonic scaling-rule copying remain separate unresolved gates.
- **Repository-memory checks:** 12/12 documentation tests passed; the generator and checker passed 21 requirements, 31 features and 66 capabilities with one active task. `git diff --check` passed after removing accidental Markdown trailing spaces. These checks do not test animation quality.
- **Next unfinished action:** add the virtual cranial anchor contract using reviewed neck-local skin geometry; keep `head` absent from FK mapping, transfer neck once, and use the anchor for head/hand goals and skin-patch classification. Freeze positive/negative renamed/permuted fixtures and Sonic source-to-target head/hand expectations. Rerun authentic Sonic→Mario, native reopen/re-save, full skin/contact/root checks and inspected private-desktop side view. Then address source-relative body clearance without inventing floor contact or weakening gates. Keep this task open until a corrected non-identical-rig output qualifies.
