# Retarget head-guard correction and cranial branch witness

Code revision: `601466c600d4ab868ebe9331842670ad74a3617f` on public Fighterport `main`, following the [first unequal-rig checkpoint](RETARGET_NONIDENTICAL_CHECKPOINT_2026-11-03.md). Starting checkout for this correction was clean `7eefe96cdfaa9069db5aae5592ea99062c48ead1`. The last accepted **product preview** remains preview.96, `041ec87a6f7d4d6b42f311cc91fe48fee5f4191b`. This is a tested, bounded correction and diagnostic checkpoint within `TASK-RETARGET-NONIDENTICAL-001`, not acceptance of Sonic → Mario or of arbitrary proportions.

## Cause, correction and limits

The preceding guard correctly withheld an ambiguous source `head` role when several substantial branches descend from a neck. It also stopped an **identical-rig** native copy before the exact hierarchy, transform and skin admission check could run. A supplied or replayed role map could lose the ambiguity marker and take the unsafe unequal-rig path. The correction keeps a provisional candidate internal to characterization, allows it through capture only for exact-rig admission, and rejects it for any unequal-rig solve. Saved-map replay cannot convert the provisional role into a reviewed role. The exact-copy result omits that provisional anatomy from recorded mapping and marks support evidence unavailable; explicit contact correction still requires reviewed context. The BT3 direct route also rejects the provisional target.

The native editor defers the target mapping review until a donor is selected, then permits the guarded exact-copy candidate. This preserves the complete native Model Previewer and original Next route. No solver tolerance, root policy, target binds, hierarchy, skin weights, CHR0 codec or source bytes were changed. The correction does **not** invent a central head joint. `head=neck` is still unqualified because naïve role duplication can apply the same local translation or scale twice.

The exact Sonic `AttackS3S` and `Wait1` same-rig runs now reach the copy boundary but stop at the existing `NativeAnimationOptimization.Copy` rejection of a **nonzero CHR0 scaling rule**. They did not export or reopen, and no quality pass is claimed. The synthetic Sonic helper-only clip passed exact copy/reopen, which establishes the guard behavior for that input only. The scaling-rule rejection remains a separate native metadata preservation task; it must not be bypassed by silently discarding that field.

## Exact fixture and read-only witness

The authentic non-idle Sonic `AttackS3S` source model container SHA-256 is `51e9a8b4f51911dc378ab612934f547339b1e557f04d82290c76fcba1350bad1`, motion container SHA-256 `a18af4d294aa61c46f64d7151c8bca63cceea91f0cec0aabf5204608cc3cf036`, native member `root/1/0/0/58`, 44 frames, CHR0 v4, non-looping. This correction's Mario target model container SHA-256 is `d4937cac8986f60440afdc14ed90c212a68d20a7bfe092d97a87b8dc20a82963`. The first checkpoint used a different historical Mario container hash; its result is not silently identified with this target. Source paths and commercial bytes remain in ignored local artifacts.

The new `--cranial-branch-witness` validates the exact asset/member identity and samples native skin data without assigning an anatomical role. It counts all 6,844 source vertices, the 4,523 influenced by the neck or a descendant, each immediate neck-child subtree and direct-neck influence, and native trajectories at 12 frozen integer/fractional times. It records weighted/majority vertex counts, bind centroids/radii, neck-local centroid motion and distances to both hands. The direct-neck group carries 4,236.3 weighted vertices (4,219 majority) and its centroid moves about 0.0013 native units in neck-local space over those samples. Each of six quill branches carries only about 39–49 weighted vertices and moves about 1.5–2.6 native units in neck-local space. The remaining immediate branch has no usable skin mass. This supports investigating a skin-backed cranial anchor rather than the previously selected animated quill tip; it does **not** prove a universal head role, collision-free transfer, continuous-time bound or corrected target motion.

Other exact private fixture identities used on the correction build:

| Case | Model SHA-256 | Motion SHA-256 | Native member |
| --- | --- | --- | --- |
| Link `AttackS3S` → Samus | Source `2445e84617b15f0389c562463a07de806db51bf31a5e2d752a4d89b340e97d23`; target `5b18ad1ed975eb098569bf5dc6760e4b577eea63b38817b33bfd2dedbcc176f8` | `f843a2ad3ef2a3e5b703fab068ae40ec4c64c6d6efa5415fb8a2b09b81f0de14` | `root/1/0/0/58` |
| Samus `AttackS3S` → Link | Source `5b18ad1ed975eb098569bf5dc6760e4b577eea63b38817b33bfd2dedbcc176f8`; target `2445e84617b15f0389c562463a07de806db51bf31a5e2d752a4d89b340e97d23` | `6f0f8944897cf3bcb4b9eeac0ebba30dd09d581fcb6b494d52541851c60314de` | `root/1/0/0/59` |
| Mario `AttackAirHi` private-desktop UI | `e73cc6f7c00cebe5587bda20bd7484d9dc49fb32ffddb5663c0284fc16fe62a6` | `afa97c15732854f8798a93bc52bdc833cd020cd4711d8ecf68228175f4fc3288` | Resolved by the UI harness from exact model and motion inputs |

## Background checks on this code revision

The fresh native application, retarget harness and ControlsAcceptance harness built. The native application SHA-256 was `be74c2c9ef26b144c556f7775e78a7a7fcac1bb0a07c30ef7eda047aa0e5ac5c`. `--branched-head-characterization` passed **28/28** procedural assertions across renamed/permuted conventions, including snapshot/cache replay refusing an unequal-rig solve. The synthetic six-frame same-rig Sonic helper test copied/reopened exactly, measured maximum world error **0**, preserved source/model bytes, omitted provisional head provenance and rejected explicit Grounded without reviewed support.

Exact Sonic `AttackS3S` → Mario was refused before output with unchanged source and target ownership. Authentic Link → Samus and Samus → Link `AttackS3S` each produced a full-quality master and source-channel-time native result, freshly reopened with protected controls/source/model ownership unchanged. Their exact-member trajectory checks passed 430 sampled times in each direction: protected root-local error **0**; maximum supported-patch penetration **0.0026871** versus **0.0106652** bound, and **0.0005804** versus **0.0081846** bound, respectively. These are support-patch oracles, not whole-skin or broad motion-quality acceptance.

The private-desktop `-WorkspaceSameRigRetarget` run passed **15/15** checks on Mario `AttackAirHi`: actual Use on This Model, responsive native timeline, body edit with Undo/Redo, and project Save/reopen. Its inspected capture shows the native target viewport, bone/animation lists and source pane; the camera is overhead, so it supplies no side-view or contact-quality judgment. The run used an existing unchanged engine host (SHA-256 `d12e269418c160ba2593117d346ad2c45b93d38cbddacfccf09931ea10ec0aa8`) because the local machine lacks the .NET 10 SDK needed for a fresh engine build. An initial private-desktop attempt was denied by sandbox `CreateDesktop`; an intermediate run lacked the engine host. The successful run used the private desktop, no OS input injection and no active-screen capture. Human physical input, external editor/game runtime and monitor DPI were not tested.

Commands exercised, with private fixture paths replaced by placeholders and generated output retained under ignored `../../artifacts/`:

```powershell
& scripts/Build.ps1 -NativeOnly -NativeDirectory <ignored-native-build>
& <VS2022-MSBuild> tests/RetargetAcceptance/RetargetAcceptance.csproj /restore /t:Build /p:Configuration=Release /p:Platform=x86 /p:BuildProjectReferences=false /p:OutDir=<ignored-native-build> /nologo /v:minimal
& <ignored-native-build>/RetargetAcceptance.exe --branched-head-characterization <ignored-output>
& <ignored-native-build>/RetargetAcceptance.exe --same-rig-helper-only <exact-Sonic-model> <ignored-output>
& <ignored-native-build>/RetargetAcceptance.exe --cranial-branch-witness <exact-Sonic-model> <exact-Sonic-motion> <exact-member-manifest> <ignored-output>
& <ignored-native-build>/RetargetAcceptance.exe --retarget-case <exact-source-model> <exact-target-model> <exact-motion> <exact-fixture-manifest> <ignored-output>
& <ignored-native-build>/RetargetAcceptance.exe --verify-trajectory <exact-source-model> <exact-target-model> <exact-motion> AttackS3S <saved-CHR0> <ignored-output> <exact-fixture-manifest>
& scripts/Test-BackgroundCapture.ps1 -Model <exact-Mario-model> -Motion <exact-Mario-motion> -Clip AttackAirHi -WorkspaceSameRigRetarget -NativeDirectory <ignored-native-build> -OutputDirectory <ignored-output>
```

Local receipts: `../../artifacts/Fighterport-quality-headguard-correction-branched-final/branched-head-characterization.json`, `...-sonic-synthetic-final/helper-only.json`, `...-cranial-final/cranial-branch-witness.json`, `...-sonic-exact/same-rig-quality.json`, `...-sonic-wait1-exact/same-rig-quality.json`, `...-sonic-mario-refusal2/case.json`, `...-regression/{Link-Samus-AttackS3S,Samus-Link-AttackS3S}` and `...-ui-mario-exact-host/receipt.json` plus the inspected PNG. These receipts are ignored and contain private local paths; they are not public evidence artifacts.

## Next executable action

Keep `TASK-RETARGET-NONIDENTICAL-001` open. Implement a reviewed structural/skin-backed cranial-parent transfer policy that maps source neck motion once and derives a head-surface anchor for target head/hand goals. Freeze head/hand expectations on the exact Sonic source, add positive and negative renamed/permuted fixtures, then rerun authentic Sonic → Mario with source/mapping/solver/native/whole-skin/contact witnesses, fresh reopen/re-save and inspected private-desktop side view. Keep source-relative whole-body clearance and the authentic Sonic scaling-rule copy gate explicit until independently resolved. Neither this checkpoint nor finite cranial sampling qualifies all characters/animations or in-game output.
