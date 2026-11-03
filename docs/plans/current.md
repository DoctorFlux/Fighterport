# Current bounded task — non-identical-rig retarget quality

Task ID: TASK-RETARGET-NONIDENTICAL-001

Parent: incomplete G1/M2

Starting accepted application revision: `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b` (preview.96). The reviewed-cranial increment began from clean `b0044905d0302a5af1e082566fc674c0b9a37000` and its latest committed tested code checkpoint is `dda9d2eff162016fb0d9456ed9299cd1dfade250`. The current body/hand investigation begins from `9913e6d8addd244418e5c63a676565ebbf62650c` plus a read-only acceptance-harness diff. All experimental solver edits were reverted; this is not another accepted preview.

Linked requirements: `REQ04`, `REQ05`, `REQ08`, `REQ10`, `REQ13`, `REQ14`

Linked feature: `FP-RETARGET-QUALITY`

Preview.96's guarded identical-rig preservation is the last accepted product increment. The [reviewed cranial checkpoint](../RETARGET_CRANIAL_REVIEW_CHECKPOINT_2026-11-03.md) pins the accepted mapping evidence; the [source-relative body/hand checkpoint](../RETARGET_EARLY_BODY_HAND_CHECKPOINT_2026-11-03.md) records reverted partial corrections, failed authentic hand-depth and native fractional-bake gates, and the fresh read-only baseline witness. Older receipts retain their own tested revisions.

## Objective and acceptance boundary

Find a real non-identical-rig retarget-quality failure and correct its first demonstrated general cause. Trace native source interpretation → authentic rig/bind data → reviewed role/joint mapping → solver/body/contact goals → generated local curves → serialized/freshly reopened native result → skinned/contact motion. Preserve original Next, target hierarchy/rest/inverse binds/weights, source-owned root motion, all skin influences and frozen quality gates. No character/action hardcodes or accuracy relaxation.

Task completion requires a pinned before/after fixture showing the cause, general correction, independent numerical checks, fresh native reopen/re-save, unaffected regressions and inspected background captures wherever visual behavior is claimed. A measured result may instead prove missing context or unsupported anatomy; record it honestly. A partial pass does not qualify broad motion, contacts or game runtime.

## Progress and remaining failure

1. **Corrected mapping cause:** Sonic's animated quill was a false automatic `head`. `dda9d2e` adds a reviewed direct-neck skin anchor with an exact rig/skin digest. Source neck maps once; Mario's actual head remains under its native hierarchy. Close-hand goals use matching skin measurements. The UI requires explicit review and rejects stale evidence. Ordinary physical-head Next behavior remains unchanged.
2. **Scoped pass:** 33/33 virtual-cranial, 94/94 coordinate-frame and 32/32 branched-head checks; authentic Sonic `AttackS3S`→Mario native serialize/reopen; 474/474 trajectory and 345 supported-contact samples; zero protected root-local error; 10/10 private-desktop UI review; inspected 87 half-frame orthographic side comparison. Exact donor member copy/reopen preserved 68 entries and nonzero scaling metadata.
3. **Open quality failure:** a `HatN` vertex reaches -3.79684 native units against Mario's target bind-plane guide at frame 5, while the source whole-mesh minimum is -1.52706 against its own guide. Inspected frames show target head/hat/body crossing around frames 5–12. No reviewed hat/floor contact applies. Supported-foot results do not qualify whole-body clearance.
4. **Anatomy boundary:** the single-joint cranial route is source-side and reviewed. Target-side merged head/neck and one-versus-two shoulder chain composition remain unimplemented or unqualified. Never duplicate one physical joint's motion across semantic roles or treat a quill as a head.
5. **Current body/hand investigation:** a general rest-relative aggregate trunk/body-surface candidate reduces the exact reopened frame-5 target whole-mesh minimum from -3.79684 to -1.92549 native units while the source is -1.52706. Root-local motion remains exact. A 49-quarter-frame early-hand witness finds no planted-hand proposal or ground evidence at frames 8/10/12. Sonic's signed hand depth is negative while Mario's body-corrected palms hover positive. Initial and widened hand-depth corrections passed procedural checks but their fresh authentic outputs failed the frozen normalized early-hand bound `0.36615 > 0.05`, essentially unchanged from body-only. At frames 5 and 7.25 the desired palms are farther from Mario's shoulders than his fixed two-bone arm reach. This candidate was reverted; a hand-only IK adjustment cannot solve the measured conflict. Subsequent coupled shoulder/arm attempts failed unchanged native fractional bake at frames 2–4 with 21.8–59.9° rotation errors. The fresh restored baseline passes native bake/reopen but its read-only 49-quarter-frame witness measures maximum normalized palm error 0.43206, forearm error 0.26132, and source-relative aggregate trunk/body-surface extra descent 1.9493 target units over frames 5–12. No new quality correction is accepted.
6. **Read-only shoulder topology witness:** Mario has extra unmapped L/R shoulder parents that retain bind-local transforms, while Sonic's upper arms attach to the chest parent. Source and target bind pelvis→chest vertical spans are 1.282664 and 2.354357 native units. At frames 5/8/11, target minus source animated span scaled by their bind ratio is -0.2388/-0.2406/-0.2371 target units. This supersedes a rough ~0.17 estimate and is much smaller than the absolute chest gap. The topology and skinned hand-patch mismatch are diagnostic leads, not demonstrated solver causes. The freshly reopened baseline output remains 283,752 bytes.
7. **Temporal trial rejected:** an isolated mapped-arm candidate reopened with exact roots and supported-contact checks and early palm/forearm errors `0.047087/0.084355` under the proposed `0.05/0.10` bounds. It still had `1.949278` target-unit extra trunk/body-surface descent and `-3.79684` worst HatN guide depth. A 49-quarter-frame, 3,913-vertex same-target check found up to `2.810823` native-unit skin displacement, `1.322490` near shoulder/arm, and maximum nearest-bind shoulder/torso gap changes `0.682011` left and `1.182349` right. No reviewed seam limit exists, so this hand-only improvement does not qualify whole-body quality. The final `353,340`-byte diagnostic and all failed native trials remain ignored; all production hooks were reverted. See [dated receipt](../RETARGET_EARLY_BODY_HAND_CHECKPOINT_2026-11-03.md).

## Exact next executable action

Develop a **time-continuous offline, morphology-aware** coupled skinned torso, shoulder and arm solve for the pinned Sonic→Mario result. The body-only output improved guide depth but made hands hover; hand-only solutions met either some motion or native gates, never combined trunk/skin quality. Keep source-relative passing hands as **noncontact** goals, established contacts authoritative and source-owned roots exact. Freeze reviewed shoulder-seam and attachment bounds before tuning; retain palm `0.05`, forearm `0.10` target arm length, trunk extra descent `0.01` target leg length, and unchanged native fractional/post-decode, root and support gates. Inspect chest-to-upper-arm and skinned hand/torso motion across the target-only extra shoulder parent without assuming it caused the error. Prove no shoulder/arm branch jump at frames 2–4, then fresh serialize/reopen and compare protected roots, all weights, supported contacts, full skin, seam and attachment geometry. Inspect new background side captures only after numeric gates pass. Qualify reviewed split/merged cranial and shoulder composition separately. Never use character/action dispatch or change roots, binds, hierarchy or weights.

Keep preparation, user review, native Apply and output dependency checks distinct. Do not mark this task or G1/M2 complete from this cranial subcase. Preserve historical receipts and private assets in ignored locations.

## Relevant files and scoped commands

Code to investigate: `BrawlCrate/Studio/Retargeting/Rig.cs`, `SkeletonCharacterizer.cs`, `BrawlRetargeter.Cranial.cs`, `BrawlRetargeter.CranialReview.cs`, `BrawlRetargeter.cs`, `HumanoidRetargeter.cs`, `ContactMotion.cs`, `BodyContactSolver.cs`, `BrawlCrate/UI/ModelForm.SkeletonMapping.cs`. Current read-only test: `tests/RetargetAcceptance/ReviewedCranialRetargetCase.cs`; regressions: `VirtualCranialAnchorChecks.cs`, `BranchedHeadCharacterization.cs`, `TransferWitness.cs` and ControlsAcceptance cranial review. Contracts: [automatic retargeting](../engine/AUTOMATIC_RETARGETING.md), [engine integration](../ENGINE_INTEGRATION.md), [cranial receipt](../RETARGET_CRANIAL_REVIEW_CHECKPOINT_2026-11-03.md), [body/hand receipt](../RETARGET_EARLY_BODY_HAND_CHECKPOINT_2026-11-03.md).

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
