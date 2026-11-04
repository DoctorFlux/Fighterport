# Sonic forward-tilt coupled hand trials — 4 November 2026

Task `TASK-RETARGET-NONIDENTICAL-001` continued from clean `DoctorFlux/Fighterport/main` at `d40ce6265319b773e29ca5ba85ba2dccd811786e`. These are **rejected, isolated solver trials**, not an accepted motion correction or a new product preview. All temporary solver and native-bake edits were reverted; production code remains exactly at the starting commit. The current review warning and failed hand gate from `16708fa` remain in force.

## Exact input and unchanged limits

The authentic 44-frame, non-looping Sonic `AttackS3S` CHR0 v4 snapshot SHA-256 was `9815094dcbed35a6ce671b65562e4a271e5dc647224cbce2efa55e27bc630943`. The Sonic model PAC SHA-256 was `51e9a8b4f51911dc378ab612934f547339b1e557f04d82290c76fcba1350bad1`; the Mario target model PAC SHA-256 was `d4937cac8986f60440afdca14ed90c212a68d20a7bfe092d97a87b8dc20a82963`. The ignored exact-member fixture manifest SHA-256 was `d1a0a22596c96d727fa9a3c5abaff3ded9256e7b8bc09b050ee5a7258e3be70a`. No private game data or derived CHR0 is included in Git.

The unchanged reference is a **283,752-byte** freshly reopened full-quality target. Its sampled early palm, forearm and extra-trunk errors are respectively `0.432063` target arm lengths, `0.261322` target arm lengths and `1.949278` target units. The proposed unchanged quality limits are `0.05` arm, `0.10` arm and `0.01 × 3.940794 = 0.039408` target units. Exact protected root-local motion, 474 trajectory samples, 345 supported-contact samples and 129 withheld fractions pass on the reference; these passes do not certify its unreviewed hand pose.

## Offline candidates

All six trials used the same source and target, an environment-gated temporary implementation, the real native CHR0 writer, fresh native reopen where available, the independent hand/body witness and the existing root/support checks. They raised the target pelvis by the source-relative skinned trunk difference and sought the source's arm-normalized skinned palm clearance. This was **not** treated as a reviewed floor or planted-hand contact. Trial code was never exposed in the launcher or applied to a user document.

| Trial | Difference from prior trial | Reopened CHR0 bytes | Palm / forearm errors (arm lengths) | Extra trunk error | Outcome |
| --- | --- | ---: | ---: | ---: | --- |
| v1 | One palm pass, direct upper-arm reach adjustment | 349,488 | 0.298109 / 0.282220 | 0.000021 | Fails hand and forearm |
| v2 | Five palm passes, posed-arm reach | none | not measured | not measured | Native decoded translation `0.000202134 > 0.0002` at frame 9.05275; no result imported |
| v3 | v2 with tighter **internal** curve fitting target, unchanged final bound | 344,112 | 0.141779 / 0.259013 | 0.000021 | Fails hand, forearm and shoulder-seam proxy |
| v4 | Source elbow bend plane supplied | 344,472 | 0.143351 / 0.259794 | 0.000021 | No useful forearm improvement |
| v5 | Forearm twist search while restoring wrist world orientation | 369,240 | 0.162942 / 0.252687 | 0.000021 | Palm worsens; still fails forearm |
| v6 | Allow elbow pole solve even when wrist already reaches goal | 344,472 | 0.143351 / 0.259794 | 0.000021 | Same measured hand failure as v4 |

The v3 output SHA-256 was `693aa9511170ebca4f36451815a2581ff49df6e4c8fd1397afc0ea4acdc7ec4d`. It passed the **existing bounded native** suite after reopen, including exact protected roots; that suite is not the hand/body acceptance gate. Its source-relative shoulder bind-neighbor diagnostic measured maximum normalized gap changes `0.463` left and `0.533` right against **provisional diagnostic** allowances `0.259` and `0.301`. Those allowances are not yet independently qualified acceptance bounds, but the large deviations identify arm attachment as an additional risk. v1, v4, v5 and v6 also exceeded both provisional seam allowances. v3 recorded 447 clamped-limb samples and a `0.961855` maximum limb-goal residual. The candidate grew by 60,360 bytes versus the unchanged reference; size was not the reason for rejecting it.

The source elbow pole and wrist twist did not fix the forearm because a scalar palm-height goal does not specify the three-dimensional arm/shoulder arrangement. Direct animated translation of the upper arm to recover reach creates large attachment-gap changes. A body lift by itself changes source-relative torso clearance but leaves the arm too short for several desired hand positions. These are observations on this exact fixture, not a proof that all possible joint trajectories are infeasible.

## Commands and next action

Visual Studio 2022 built the native app and x86 `RetargetAcceptance` harness for each isolated version under ignored `../../artifacts/Fighterport-coupled-hand-trial-v1` through `-v6`. Each ran `RetargetAcceptance.exe --reviewed-cranial-retarget <hashed Sonic PAC> <hashed Mario PAC> <audited CHR0 snapshot> <exact ignored manifest> <fresh ignored output> <explicit test-fixture review>`, with the temporary trial flag set. Receipts and logs remain ignored beside each trial. v1/v3/v4/v5/v6 returned a bounded-native pass; v2 returned a native validation failure. No new offscreen GIF or corrected animation is claimed because every motion candidate failed its separate quality check. Physical input and game runtime were not run.

The exact temporary **v6** source diff against clean `d40ce6265319b773e29ca5ba85ba2dccd811786e` is preserved only in ignored `../../artifacts/Fighterport-coupled-hand-trial-v6/source.patch` (SHA-256 `e76f091c5f6307343610c37b25d84537c0a2e5197e7761a7aa057c679a778069`). This `HEAD+diff` identity reproduces v6; v1–v5 retain their local binaries, logs and measurements, but their distinct source diffs were not retained and must not be treated as reproducible from the v6 patch. The source diff was removed from the checkout after recording it.

**Next executable step:** construct a source-relative **three-dimensional skinned palm and forearm target** with a target-only shoulder-chain control and explicit attachment continuity. Evaluate a pose-level constrained solution before baking; keep the source's authored root local motion and all target weights/binds. Freeze and independently qualify shoulder-seam/attachment bounds, then test the full time-continuous candidate against palm, forearm, trunk, all-skin, support and post-serialization native fractional checks together. Preserve the current result when those checks fail. Do not repeat a guide-height-only body lift, independent per-frame elbow scan or direct unbounded upper-arm translation.
