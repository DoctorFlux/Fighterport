# Preview.70 — source-coherent idle foot contact

Starting commit: `40cf61800792a26409fdbdafd93d42d9086e0922`.
Qualified package: `preview.70-foot-contact-coherence`, file `2.0.0.45`.
One G1/M2 milestone remains incomplete. This is bounded background evidence, not universal contact or game acceptance.

## Change

The original Next retargeter and full native Model Previewer remain the owners. Contact detection now measures motion of the **same source skin patch** on adjacent samples. The former difference between independently chosen lowest patches could report a false velocity spike when the sole rolled. A support span reaching the start or end of a clip keeps its lock at that boundary; only an actual internal contact transition is eased. An internal release with a large target-rig correction receives a bounded, source-speed- and limb-scale-dependent exit span (4–64 quarter-frame samples), instead of dropping that correction in four samples. No model, bind, inverse bind, hierarchy, weight, root-control or source asset is modified.

This numerical policy is recorded as original Next algorithm `bind-world-fk-body-contact-ik-v11`. Existing v10 provenance remains readable and is never rewritten on reopen. The support-context evidence carries the new algorithm ID. No character, animation, game name, hash or bone index selects the numerical rule.

## Measured native results

Measurements follow a fixed skinned foot patch through every quarter frame. The maximum step is a displacement per quarter frame in native units. Source and target poses are independently evaluated; target figures below are from the serialized and freshly reopened animation. A patch selected anew at every frame would introduce false jumps and is not the metric.

| Wait1 transfer | Source maximum, left/right | Prior target maximum | Preview.70 target maximum | Observation |
|---|---:|---:|---:|---|
| Melee Fox → Brawl Fox | 0.0128 / 0.0122 | 0.1488 / 0.0507 | **0.00290 / 0.000623** | Both feet lock for 481/481 samples; reported jitter corrected. |
| Brawl Mario → Melee Mario | 0.0827 / 0.0518 | 0.0961 / 0.2809 | **0.0860 / 0.0784** | Internal release is less abrupt; target motion is still above source. |
| Brawl Fox → Melee Fox | 0.0964 / 0.0900 | 0.2287 / 0.3392 | **0.1221 / 0.2021** | Internal release is less abrupt; moving-contact quality remains open. |
| Melee Mario → Brawl Mario | 0.0156 / 0.0167 | no prior foot trace | **0.0195 / 0.0257** | Auto found no support on this exact source; no planted lock is claimed. |
| Brawl Snake → Brawl Mario | 0.00229 / 0.00169 | no prior foot trace | **0.0000034 / 0.0000062** | Both feet lock for 341/341 samples. |
| Brawl Mario → Brawl Snake | 0.0827 / 0.0518 | no prior foot trace | **0.1058 / 0.1195** | Exact native reopen passes; moving-contact excess remains open. |

The reference for each step comparison is the corresponding source foot motion, not the donor's file size or a guessed idle pose. The original source and target rigs and complete native skin influences remain unchanged. The generated CHR0s retain exact identities and reopen; this evidence does not establish visual style, contact with an in-game floor, or continuous-time bounds.

## Background verification

- `Test-BackgroundCapture.ps1 -WorkspaceMelee -MeleeSharedBridge` with the supplied Melee USA 1.02 ISO and Brawl WBFS, reused verified Brawl catalogue, private desktop and offscreen renderer: all four Mario/Fox directions passed **45** native assertions, including fresh CHR0 reopen and historical v10 project reopen (`artifacts/Fighterport-foot70/final-decoded-four`). Source, solver and decoded output traces are kept locally.
- The unrelated Brawl Mario↔Snake Wait1 pair passed **22** native assertions, including exact CHR0 reopen and the quiet Snake-foot locks (`artifacts/Fighterport-foot70/final-decoded-brawl2`).
- The final v11 package passed **30** assertions on the reported Melee Fox→Brawl Fox case and reopened a saved v10 Next project without rewriting its algorithm receipt (`artifacts/Fighterport-foot70/final-v11-fox`).
- The procedural native terminal-contact regression, `RetargetAcceptance.exe --terminal-contacts`, exited successfully. `scripts/Build.ps1` built the complete native application, self-contained owned engine and worker with the pinned local .NET 10 SDK. `Start-FighterPort.ps1 -Check` resolved the ignored `artifacts/Fighterport-foot70/native2` package; file/informational versions were read directly. No active-desktop capture, OS input injection or game runtime test was used.

The fixture assets, packages, traces and reports remain ignored and local. Source-only publication uses DoctorFlux attribution.

## Still open

The internal moving-contact cases above do not yet match the source step rate. A source-guided moving support trajectory and explicit step/replant review need their own acceptance, including locomotion, turns, airborne and multi-contact actions on more rigs. The Melee Mario source also lacks automatic support evidence in this exact Wait1 replay. Do not infer a universal planted-foot fix, broad animation quality or in-game acceptance from the qualified Fox idle result. Preview.66 finite ledges and all other G1/M2 gates stay open.
