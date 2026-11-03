# Unequal-rig retarget checkpoint — head mapping and whole-body witness

Starting checkout: clean `Fighterport/main` at `ede8f9623de475c5fe26e77e3b34c97bc2061d4c`. The tested code/test diff fingerprint is `HEAD+diff:5654c133ab368d41e3f57201d28c109924e6b835` (Git object hash of the staged binary diff for the six implementation/harness files relative to that starting commit). The last accepted **application** increment remains preview.96 at `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b`. This checkpoint adds a conservative automatic-mapping guard and background diagnostic coverage. It does **not** qualify Sonic's transfer, all-body floor clearance, arbitrary proportions, contact quality or game playback.

## Exact private fixture and first failing boundary

The authentic non-idle case is Sonic `AttackS3S` → Mario. Its source model container SHA-256 is `51e9a8b4f51911dc378ab612934f547339b1e557f04d82290c76fcba1350bad1`; motion container SHA-256 is `a18af4d294aa61c46f64d7151c8bca63cceea91f0cec0aabf5204608cc3cf036`; the exact native member is `root/1/0/0/58`, CHR0 v4, 44 frames, non-looping. The historical target container SHA-256 is `e73cc6f7c00cebe5587bda20bd7484d9dc49fb32ffddb5663c0284fc16fe62a6`. The source/target rig fingerprints differ (`c3813c2d…944aa` / `87b31347…44aa`). Paths and game bytes remain in ignored local artifacts.

On the unmodified application code from the starting commit, an exact-member `--retarget-case` exported a 282,964-byte full-quality target master (23,806 keys) and a 275,996-byte source-channel-time result (23,247 keys). Both reopened; source and target native ownership, original files and source-owned `TopN`, `TransN`, `XRotN`, `YRotN` stayed unchanged. The default result matched the finished target master at the optimizer's 3,847 sampled native times, with zero reported local/world/skin difference. This is a representation check against the **finished target**, not evidence that the retargeted action is correct.

The new read-only stage witness measured every integer frame and 34 additional quarter/withheld fractional times near the four lowest integer poses, evaluating all 6,844 source and 3,913 target skinned vertices. Minimum signed heights relative to each captured rig's reviewed floor were:

| Stage | Minimum native units | Time |
| --- | ---: | ---: |
| Sonic source, before retarget | -2.095862 | 7.137 |
| Raw and contact-corrected Mario solver pose | -2.874432 | 12.5 |
| Finished target CHR0 and fresh native reopen | -2.874385 | 12.5 |

The source itself goes below its floor in this move; these finite samples do not prove a universally collision-free source or a continuous-time bound. They do prove that the target's large floor crossing is present **before** encoding, and that the writer does not introduce it. The exact-fixture trajectory oracle separately passed 474 native samples and 345 supported-contact samples: maximum protected-root local error 0, supported-patch penetration 0.0004131 against bound 0.0039408, and planar lock drift 0.0001834. Those checks cover selected support patches, not whole-body clearance. The old visual record showed early body/head intersection, but no fresh capture was made for this code checkpoint; visual correction remains unverified.

The source rig's automatic `head` role was `UStingNb`, an animated quill branch with 75 authored keys. It is a child of `UStingNa` under `NeckN`; several other substantial quill/facial branches also descend separately from that neck. The target `head` is the native `HeadN` bone. The geometry fallback had chosen a highest-tip path where no central head joint was identified, then treated that appendage as the head for mapping, head-size calibration and hand/head goals. That is a demonstrated general **mapping** defect. Its numerical contribution to the floor crossing has not been isolated; whole-body clearance and a safe shared-neck/head policy remain separate work.

## Bounded change

`SkeletonCharacterizer.InferGeometry` now declines to auto-assign `head` when the proposed highest-tip path is one of several distinct bind-space branches below the mapped neck, or does not belong to that neck. It reports the ambiguity and requires reviewed mapping. A named head and a structurally central unnamed head with taller descendants still qualify. This changes no model bind, native weights, source file, retarget solver, CHR0 writer, viewer or contact tolerance. It does not invent a head joint or map an appendage by name.

The exact Sonic source now reports `Missing=head` with an explicit appendage warning. The exact Sonic→Mario automatic retarget refuses before writing a target CHR0. A reviewed mapping can supply a genuine distinct head where the source has one; Sonic's possible shared-neck/head anatomy needs a separately qualified policy before this case can be automatically resumed. This is a safe rejection, **not** a successful Sonic→Mario port or a fix for body penetration.

## Checks actually run

All native runs were background-only; no OS input injection, active-desktop capture or game launch occurred. The new harness adds an exact optional member manifest to `--verify-trajectory`, retains its old invocation, records pre-bake source/native agreement and mapping/solver evidence, sweeps whole-mesh floor clearance, and compares the same finished target master to freshly reopened/default native output. It labels finite sampling and support-patch coverage explicitly rather than claiming complete skin proof from the trajectory oracle.

- Fresh native application build and scoped `RetargetAcceptance` build passed on the changed code. Visual Studio's existing fixture-deserializer CS0649 warnings remain.
- Tested native `FighterPortStudioV2.exe` SHA-256: `92e65bf36a83f112d618e9aadef6d08d82dc81b3792e9e41cfe3ac73c6a11d50`; `RetargetAcceptance.exe` SHA-256: `2332ba8253c33e3e16eeb48dc32f7d591cd715af22de0d82614c2ef61d37d041`.
- `--branched-head-characterization` passed 20 checks across renamed and storage-permuted procedural ambiguous, central and named-head cases. `--coordinate-frames` passed 94 checks.
- Exact Sonic `--mapping-audit` reported no assigned head and the review warning; the exact `--retarget-case` negative check returned the expected mapping error and wrote no target CHR0.
- Six existing Brawl rigs (Mario, Snake, Peach, Fox, Link, Samus) still characterized with their native named `HeadN`.
- Link→Samus and Samus→Link `AttackS3S` both passed post-change exact-member `--retarget-case`: full-quality and default CHR0 reopened, protected controls and original/native ownership stayed unchanged. Their exact-manifest trajectory oracles passed 430 samples each. Supported-patch penetration was 0.0026871 / 0.0005804 against bounds 0.0106652 / 0.0081846; root local error was zero.
- A wrong-direction exact fixture manifest was rejected before trajectory verification. `python tests/test_project_memory.py` passed 12 positive/negative cases; `python scripts/Check-ProjectMemory.py --generate-support` and its check mode passed 21 approved requirements, 31 features and 66 capabilities. `git diff --check` passed. These documentation checks are not motion tests. No post-change human input, visual acceptance, BrawlCrate import test or actual game runtime was performed.

The local receipts are under ignored `artifacts/Fighterport-quality-next-sonic-mario-historical-witness`, `artifacts/Fighterport-quality-next-sonic-mario-guarded-trajectory` and `artifacts/Fighterport-quality-headguard-*`. These contain private fixture paths and generated CHR0s and are not published. Reproduction uses the exact manifest/asset hashes above, `RetargetAcceptance.exe --retarget-case <source-model> <target-model> <motion> <fixture> <fresh-output>`, then `--verify-trajectory <source-model> <target-model> <motion> AttackS3S <saved-chr0> <fresh-output> <fixture>`. The expected post-guard Sonic run is a rejection; the pre-guard witness output is retained separately.

## Next executable quality work

Keep `TASK-RETARGET-NONIDENTICAL-001` open. First establish a reviewed structural/skin policy for source rigs whose neck is also the effective cranial parent, without duplicating local translation or scale or inventing a head bone. Add an independent source-to-target head/hand motion witness and rerun the exact Sonic fixture. Then tackle whole-body floor clearance with source-intent and support context; a supported foot patch passing does not authorize the head/body to penetrate. Preserve the existing contact and native-fidelity gates, and keep failed cases visible. No character/action-specific numerical route is permitted.
