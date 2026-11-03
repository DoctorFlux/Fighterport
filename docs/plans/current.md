# Current bounded task — non-identical-rig retarget quality

Task ID: TASK-RETARGET-NONIDENTICAL-001

Parent: incomplete G1/M2

Starting accepted application revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (preview.96). Current increment began from clean `b0044905d0302a5af1e082566fc674c0b9a37000`; latest tested code checkpoint is `dda9d2eff162016fb0d9456ed9299cd1dfade250`. This is not another accepted preview.

Linked requirements: `REQ04`, `REQ05`, `REQ08`, `REQ10`, `REQ13`, `REQ14`

Linked feature: `FP-RETARGET-QUALITY`

Preview.96's guarded identical-rig preservation is the last accepted product increment. The [reviewed cranial checkpoint](../RETARGET_CRANIAL_REVIEW_CHECKPOINT_2026-11-03.md) pins the current source, fixture hashes, commands and limitations; older receipts retain their own tested revisions.

## Objective and acceptance boundary

Find a real non-identical-rig retarget-quality failure and correct its first demonstrated general cause. Trace native source interpretation → authentic rig/bind data → reviewed role/joint mapping → solver/body/contact goals → generated local curves → serialized/freshly reopened native result → skinned/contact motion. Preserve original Next, target hierarchy/rest/inverse binds/weights, source-owned root motion, all skin influences and frozen quality gates. No character/action hardcodes or accuracy relaxation.

Task completion requires a pinned before/after fixture showing the cause, general correction, independent numerical checks, fresh native reopen/re-save, unaffected regressions and inspected background captures wherever visual behavior is claimed. A measured result may instead prove missing context or unsupported anatomy; record it honestly. A partial pass does not qualify broad motion, contacts or game runtime.

## Progress and remaining failure

1. **Corrected mapping cause:** Sonic's animated quill was a false automatic `head`. `dda9d2e` adds a reviewed direct-neck skin anchor with an exact rig/skin digest. Source neck maps once; Mario's actual head remains under its native hierarchy. Close-hand goals use matching skin measurements. The UI requires explicit review and rejects stale evidence. Ordinary physical-head Next behavior remains unchanged.
2. **Scoped pass:** 33/33 virtual-cranial, 94/94 coordinate-frame and 32/32 branched-head checks; authentic Sonic `AttackS3S`→Mario native serialize/reopen; 474/474 trajectory and 345 supported-contact samples; zero protected root-local error; 10/10 private-desktop UI review; inspected 87 half-frame orthographic side comparison. Exact donor member copy/reopen preserved 68 entries and nonzero scaling metadata.
3. **Open quality failure:** a `HatN` vertex reaches -3.79684 native units against Mario's target bind-plane guide at frame 5, while the source whole-mesh minimum is -1.52706 against its own guide. Inspected frames show target head/hat/body crossing around frames 5–12. No reviewed hat/floor contact applies. Supported-foot results do not qualify whole-body clearance.
4. **Anatomy boundary:** the single-joint cranial route is source-side and reviewed. Target-side merged head/neck and one-versus-two shoulder chain composition remain unimplemented or unqualified. Never duplicate one physical joint's motion across semantic roles or treat a quill as a head.

## Exact next executable action

Measure frame-5 source and target head, hat and body skin points against each rig's geometry, reviewed floor context and baked pre-encode versus reopened motion. Determine whether extra target crossing comes from reviewed mapping, solver/body-proportion goals or source action intent. Freeze source-relative expected values before a general correction. If demonstrated, implement without character/action dispatch or changes to roots, binds, hierarchy, weights or gates; rerun the same exact fixture plus renamed/storage-permuted, unrelated real-head and support-contact regressions. Inspect fresh background side captures and record either acceptance or precise refusal. Follow with a separate reviewed split/merged cranial and shoulder chain-composition increment, with bidirectional unequal-rig tests where authentic assets exist.

Keep preparation, user review, native Apply and output dependency checks distinct. Do not mark this task or G1/M2 complete from this cranial subcase. Preserve historical receipts and private assets in ignored locations.

## Relevant files and scoped commands

Code: `BrawlCrate/Studio/Retargeting/Rig.cs`, `SkeletonCharacterizer.cs`, `BrawlRetargeter.Cranial.cs`, `BrawlRetargeter.CranialReview.cs`, `BrawlRetargeter.cs`, `HumanoidRetargeter.cs`, `BrawlCrate/UI/ModelForm.SkeletonMapping.cs`. Tests: `tests/RetargetAcceptance/VirtualCranialAnchorChecks.cs`, `ReviewedCranialRetargetCase.cs`, `BranchedHeadCharacterization.cs`, `TransferWitness.cs` and ControlsAcceptance cranial review. Contracts: [automatic retargeting](../engine/AUTOMATIC_RETARGETING.md), [engine integration](../ENGINE_INTEGRATION.md), [current receipt](../RETARGET_CRANIAL_REVIEW_CHECKPOINT_2026-11-03.md).

Run from the checkout, resolving exact private fixture paths only from the ignored manifest and using fresh ignored outputs:

```powershell
& scripts/Build.ps1 -NativeOnly -NativeDirectory ../../artifacts/Fighterport-cranial-next-build/native
& <VS2022-MSBuild> tests/RetargetAcceptance/RetargetAcceptance.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<ignored-native-build> /nologo /v:minimal
& <ignored-native-build>/RetargetAcceptance.exe --virtual-cranial-anchor <ignored-output>
& <ignored-native-build>/RetargetAcceptance.exe --coordinate-frames <ignored-output>
& <ignored-native-build>/RetargetAcceptance.exe --branched-head-characterization <ignored-output>
& <ignored-native-build>/RetargetAcceptance.exe --reviewed-cranial-retarget <Sonic-model> <Mario-model> <Sonic-motion> <pinned-ignored-fixture> <ignored-output> <review-evidence>
& <ignored-native-build>/ControlsAcceptance.exe <Sonic-model> <Sonic-motion> <ignored-output> --workspace-cranial-review --target-model <Mario-model> --clip AttackS3S --private-desktop
python tests/test_project_memory.py
python scripts/Check-ProjectMemory.py --generate-support
python scripts/Check-ProjectMemory.py
```

Use `scripts/Test-BackgroundCapture.ps1` and the shared offscreen renderer for new captures; serialize paced private-desktop work. Command placeholders protect private paths; actual command forms and ignored output identities are pinned in the receipt. Native reopen and visual-guide alignment do not establish game acceptance.
