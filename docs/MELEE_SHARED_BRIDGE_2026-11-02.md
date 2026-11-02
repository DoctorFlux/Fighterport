# Shared Melee rig and animation bridge — bounded evidence

Starting source commit: `cbf814e3b7c164022f54ff119e29c7048af0a379`.
Active product: `.local/Fighterport`; publication destination: public `DoctorFlux/Fighterport/main`.
Qualified version: `preview.69-melee-shared-bridge`, file `2.0.0.44`.
One G1/M2 milestone remains incomplete. This is a source and background-test increment, not game installation acceptance.

## Implementation

- Native readers supply a format-neutral policy for hierarchy, scale compensation, authored inverse binds, skin owner/envelope semantics, all influence weights and structural roles. Original Next and the owned controls/fitting engine evaluate those policies without converting the native rig into a new skeleton. The complete BrawlCrate Model Previewer remains the UI and Undo owner.
- The normal source pane accepts a Melee animation for `Use on This Model`. Native source and target snapshots are independently verified and retain their exact original hashes. Existing source-local root controls and numeric quality/work budgets still govern the result.
- `Open as target` owns the chosen native character's complete animation archive as one editable set. Every prepared member receives its exact original action key and hash. The serialized set is freshly reopened, and every member is checked against its own native motion. Unsupported members are listed rather than represented by a bind pose.
- A source animation's native scale flags are evaluated per selected clip, including within a target's animation set. Fitting imports each reference with that reference's flags. A new authored row seeds native bind channels and uses only a provable row-scale policy; mixed policies require review.
- Native skin policies contribute to the deformation dependency hash. Invalid owner indices, missing policies, inconsistent influences and nonfinite authored inverse binds are rejected. A proven fixed skin equation can use the original fast contact path; a moving owner basis uses the complete native evaluator.
- A topology-proven merged spine/chest role is supported without inventing a bone. Distinct source torso scale and authored translation contributions are coalesced once; nonuniform or reflected animated coalescing remains unavailable pending a qualified affine policy. No character/action names or storage positions change the numerical algorithm.
- Nonzero native FOBJ start offsets use the raw authored curve with its original offset. A negative offset retains bind values before the track begins. This avoids the previous silent partial character sets. Original native packets and their source bytes stay immutable.

## Background evidence

The real inputs are the supplied Brawl WBFS and Melee USA 1.02 ISO. They remain local and ignored; the test uses their exact native members. All captures use a private desktop and the shared offscreen renderer.

The independent native hierarchy and full-skin oracle covers integer and withheld fractional samples. Mario maximum bone/vertex discrepancy was `3.2537802781007485e-6` native units; Fox was `2.6575919491733657e-6`. The owned engine's full-skin results versus the independent Melee evaluator measured `3.1259269235306419e-6` and `3.0157830224197824e-6`, respectively, under the frozen `0.0002` bound. These are sampled checks, not a continuous-time proof.

The prepared target sets contain all `191/191` Mario and `217/217` Fox native action members, with zero unavailable members for those two selected archives. Four lower-level Wait1 transfers (Melee→Brawl and Brawl→Melee for both actors) pass exact native I12 serialization/reopen, full character-set project Save/reopen, source/target rig identities and unchanged native bind/weight checks (`bridge21`, 40 checks).

The actual source-pane **Use on This Model** button, exact root-key comparison, native IK edit and Undo/Redo, body edit and Undo/Redo, fit preview, Apply/Undo/Redo, exact CHR0 export/reopen, and unchanged model hash pass for both Mario directions (`workflow14`, 57 checks). A separate Mario Brawl→Melee run also saved and reopened the fitted result together with the complete target animation set (`workflow23`, 38 checks). The same UI path passes Fox Brawl→Melee (`workflow18` through its complete first direction) and Fox Melee→Brawl (`workflow20`, 41 checks). Fox required the existing explicit Skeleton Mapping review for both rigs; the Brawl target's four IK chains also required the normal Rig Coordinates review. These are recorded test reviews of the native fixture, not character-specific solver rules.

Shared Core control regressions passed 59 checks (`core14`); boundary fitting passed 436 (`core-fit18`). The Melee procedural native suite passed 79 checks (`native10`), including 9 and 17 influence skin fixtures and positive/negative FOBJ offsets. Two UI captures of the completed Fox transfers and the Mario Melee target were actually inspected. A fitted endpoint is an endpoint pose result; this evidence does not qualify foot contact or in-game behavior. All generated reports/captures are ignored under `artifacts/Fighterport-melee69`.

Representative commands actually run: `MSBuild tests/ControlsAcceptance/ControlsAcceptance.csproj /restore /m /p:Configuration=Release /p:Platform=x86`; `scripts/Build-Engine.ps1 -DotNet <bundled SDK 10> -NativeDirectory <x86 build>`; `scripts/Test-BackgroundCapture.ps1 -WorkspaceMelee -MeleeSharedBridge -DiscImage <supplied Brawl WBFS> -Model <supplied Melee ISO> -Motion <same ISO> -NativeDirectory <x86 build> -OutputDirectory <ignored evidence>`; `dotnet run --project engine/tests/FighterPort.Tests/FighterPort.Tests.csproj -- --viewer-controls` and `--boundary-fitting`, from `engine/` with its pinned SDK.

## Open boundaries

Native model geometry/bind/weight writing, destination action-slot assignment for newly retargeted Melee clips, complete native archive repacking, native contact-evidence persistence, compact codecs and game runtime tests remain unqualified. A CHR0 authoring master is not a native Melee action-file installation. Other character pairs, costumes, modpacks, physical input and human usability need their own evidence. The earlier finite-ledge milestone remains queued. No broad contact quality follows from Wait1 endpoint fitting alone.

## Local package and publication

`scripts/Build.ps1 -Configuration Release -DotNet <bundled SDK 10> -NativeDirectory <ignored native1>` completed, including the owned engine and worker. The packaged executable reports file `2.0.0.44` and informational version `2.0.0-preview.69-melee-shared-bridge`. The check-only `Start-FighterPort.ps1 -Check` resolves the ignored `artifacts/Fighterport-melee69/native1` build without opening a window. The launcher preference is local/ignored; no generated binary or private fixture belongs in source publication.
