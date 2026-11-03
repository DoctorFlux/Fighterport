# Preview.96 — exact-rig retarget quality

Starting public `Fighterport/main` commit: `85e9873446a27dcd545a0dccf68521506a111b84`.
This is one quality-first increment inside the incomplete G1/M2 milestone.
The post-guard paired corpus, direct Peach/Mario cases, helper-only guard case
and private-desktop UI rerun passed on the pinned current application. It
does not replace the original Next solver, the complete BrawlCrate Model
Previewer, native editing, or the existing CHR0 optimizer.

## Current capability audit

`BrawlCrate/Studio/NativeGameAdapters.cs` registers Brawl/Project M/Project+
and Melee native source adapters. It does not register Ultimate or Rivals 2.
BT3 has a separate bounded native source/viewer route recorded by the preview.88
through preview.95 receipts. The following status describes this checkout, not
the intended final product:

| Game | Source | Destination |
| --- | --- | --- |
| Brawl | **Implemented, bounded:** native model/motion library, preview and original Next source path | **Implemented, bounded:** MDL0/CHR0 target editing and native animation output; game acceptance and all containers remain separate |
| Melee | **Partial:** HSD DAT library, model/motion preview and shared retarget bridge | **Partial:** editable target and native DAT action writing; HSD FOBJ fractional-time and broader native model output remain open |
| Ultimate | **Missing:** no registered native source adapter | **Missing:** no qualified native model/motion writer or game test |
| Rivals of Aether 2 | **Missing:** no registered native source adapter | **Missing:** no qualified Modkit/FBX target route or game test |
| BT3 | **Partial, source-only:** verified selected USA ISO form/costume/action reading, native preview, portable output and bounded Brawl retarget drafts | **Outside destination scope:** the BT3 viewer/editor does not qualify native BT3 game output |
| Other named donor games | **Research-only:** no qualified native adapter in this checkout | **Outside destination scope** |

The only intended destination games are Brawl, Melee, Ultimate and Rivals of
Aether 2. Each must ultimately also work as a source. All other games remain
source-only, even when their assets can be inspected or exported portably.

## Demonstrated defect and change

The actual Brawl source → rig mapping → solver → generated curve → serialized
CHR0 path was checked with an exact Peach model and its non-idle `AttackS3S`.
Before this change, the semantic-role plan mapped 48 of 106 identical target
joints. It left 51 authored native entries with keys (7,290 keys,
including genuinely changing curves) outside the plan. The difference was
already visible before curve encoding: maximum world
bone difference was 15.478363 native units. The old finished, freshly reopened
CHR0 differed from the original same-rig motion by 15.436804 world bone units,
17.053335 skinned-vertex units and 14.237677 contact-patch units across 411
integer, eighth-frame, additional fractional and authored-key samples. Root
control error was zero. This identifies a mapping/solver loss, not a CHR0 codec
size problem.

The new native-rig path checks corresponding bone transforms, bind/inverse
binds, hierarchy, scale-compensation flags, coordinate frame and authored-track
membership before using a complete joint map. It also compares corresponding
polygon inventories, vertex positions and all represented skin influences; it
does not prove equality of topology, materials, normals or UVs. When those
checked mesh/skin properties agree, it retains every native CHR0 track in an independent
master copy, rather than solving the same motion as a changed-proportion target.
The master copy is serialized and freshly reopened before it can be returned.
The shortcut applies only with automatic or airborne contact mode, or when
correction is disabled. Explicit Grounded and SourceContacts requests continue
through the original Next contact path. A helper-only clip may use the proved
exact-rig path without a role-track prerequisite; the source bind/association
checks still apply. Admission enforces at most 4,096 joints and two million
authored keys. A requested source-key timing mode is a separately verified,
error-bounded projection of the exact master; it is not described as a lossless
copy merely because a fixture measures zero difference. A merely
similar rig does not take this shortcut; different-proportion transfers still
use the original Next solver, contact path and existing native quality gates.
The same-rig output identity cases below qualify the default automatic contact
path. The helper-only regression checks that explicit Grounded does not take
the copy shortcut; complete same-rig contact correction and SourceContacts
quality need separate evidence.
No source bytes, target MDL0 bind/rest data, skin weights, root-control policy,
target document or sibling animation is changed by this path.

## Bounded background evidence

Local game assets and generated outputs are kept outside Git. Exact private
member identities and SHA-256 hashes are in the ignored local receipts under
`artifacts/Fighterport-quality96-*`; the fixture names select data only and do
not select a numerical retarget policy.

| Exact fixture | Fresh native result | Coverage |
| --- | --- | --- |
| Peach `AttackS3S` → identical Peach rig, 42 frames, CHR0 v4, non-looping, `SourceChannelTimes` | **Post-guard pass.** 106 mapped joints, no omitted animated entries; zero measured local translation/rotation/scale, world bone, skinned vertex, contact patch and source-control difference; reopen and re-save pass | 411 samples; standalone CHR0 19,112 bytes; source model SHA-256 `4777ed504a6da9597459927711bdae59db4d99d8e1ddb6666112587adb1f00b4`; motion `b4e70f1a6f7690b89500d80c8f5f0b083739cd5b0e302f12d866f9fc33e10a30` |
| Mario `AttackAirHi` → identical Mario rig, 34 frames, CHR0 v4, non-looping, dense | **Post-guard pass.** 63 mapped joints, no omitted animated entries; zero measured local, world bone, skin, contact and source-control difference; reopen and re-save pass | 331 samples; standalone CHR0 13,608 bytes; source model SHA-256 `d4937cac8986f60440afdc14ed90c212a68d20a7bfe092d97a87b8dc20a82963`; motion `b1de9496061b10cad49c753bd9ea518815e76758872004381cee3aea740380cf` |
| Peach `AttackAirN` → identical Peach rig, independent paired-corpus regression, dense | **Post-guard pass.** Zero measured local, world bone, skin, contact and source-control difference; reopen and re-save pass | 491 samples; standalone CHR0 16,884 bytes; exact member `root/1/0/0/75` |
| Peach `AttackS3S` → Fox `FitFox00`, 42 frames | Original dense and default source-channel-time outputs both freshly reopen and pass the existing trajectory oracle. Default maximum anchor orientation 0.0182466°, world bone 0.00117856, floor penetration 0.00203335 and planar lock drift 0.00307933, under the reviewed contact-position bound 0.00596133; root local difference zero. | 452 trajectory samples; all native skin influences represented. Dense/default standalone CHR0: 199,356/151,872 bytes. |
| Fox `AttackS3S` → Peach `FitPeach00`, 37 frames | Original dense and default source-channel-time outputs both freshly reopen and pass the existing trajectory oracle. Default maximum anchor orientation 0.0155461°, world bone 0.00103715, floor penetration 0.000851676 and planar lock drift 0.000606545, under the reviewed contact-position bound 0.00787171; root local difference zero. | 397 trajectory samples; all native skin influences represented. Dense/default standalone CHR0: 187,040/179,808 bytes. |

The two changed-proportion results are compared against each transfer's
full-quality solved target and existing contact limits, not against the donor's
unretargeted world-space pose. All three same-rig cases compare the complete
native motion because source and target rigs are independently identical. The
paired corpus also reports unchanged source files and native ownership, exact
protected source controls, and no preparation failure. The direct same-rig
receipts report unchanged source and target model ownership and source files.

The post-guard paired corpus receipt is
`artifacts/Fighterport-quality96-final-pairs3/results.json` and pins the
current application SHA-256
`DCCBC9D623C738C340AE45E292B36150765FF67A335BA466A4CB44C443D9A06F`.
The separate post-guard same-rig receipts are
`artifacts/Fighterport-quality96-postguard-peach/same-rig-quality.json`
and `artifacts/Fighterport-quality96-postguard-mario/same-rig-quality.json`.
Its exact case IDs are `Peach-Fox-AttackAirN`, `Peach-Fox-AttackS3S` and
`Fox-Peach-AttackS3S`. The script fails if a case is absent, if source/target
directions do not pair, or if either dense/default trajectory verification
fails. It retains existing output directories instead of overwriting them.

The separate private-desktop UI regression passed **15 of 15** checks on the
actual Peach `AttackS3S` Use on This Model path (`42` frames, `106` complete
bindings). It exercised explicit review, native timeline seek and next-frame,
body editing with Undo/Redo, immutable source/target resources, and project
Save/reopen retaining source identity, every binding and animation channel.
The post-guard UI rerun receipt is
`artifacts/Fighterport-quality96-postguard-ui/receipt.json`;
`same-rig-workspace.json` pins the exact model/clip hashes and the private
offscreen capture. The inspected `same-rig-use-on-target.png` (SHA-256
`9EAFD28EEC5903896B5E7E88F26DDA06308163921BD3FEBD55E823D494D4F796`)
shows the full
Peach viewport with bone, object and texture lists, timeline and small source
preview. The test reports a private desktop, no OS input injection, no active
desktop windows and no screen copy. It is a UI harness result, not a human
usability or physical-input acceptance test.

A procedural six-frame CHR0 on the authentic Mario rig exercises two
structurally selected non-role bones: a non-bind fixed translation and an
animated rotation with authored tangents and transform flags. The exact-copy
Auto route serialized and freshly reopened the same content; five fractional
world-joint samples had zero measured error. Explicit Grounded rejected the
helper-only clip through the original Next path instead of silently copying
it. The source clip and both models remained unchanged. The bounded receipt is
`artifacts/Fighterport-quality96-helper-case-final/helper-only.json`. This
checks guard routing and native representation, not a gameplay action.

## Actual commands and qualification state

The local Release build and native test commands used for this increment are
shown in relocatable form below. `$workspace` is the enclosing workspace of
this checkout. The private corpus manifest carries exact local input paths and
content hashes; its asset bytes and the ignored output directories are not
published.

```powershell
$workspace = (Resolve-Path ../..).Path
$dotnet = Join-Path $workspace '.tools/dotnet/dotnet.exe'
$native = Join-Path $workspace 'artifacts/Fighterport-quality96-build/native'
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
$env:DOTNET_CLI_HOME = Join-Path $workspace '.local/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspace '.local/packages'
& scripts/Build.ps1 -Configuration Release -DotNet $dotnet -NativeDirectory $native
& scripts/Build.ps1 -Configuration Release -DotNet $dotnet -NativeDirectory $native -NativeOnly
$manifest = Join-Path $workspace 'artifacts/Fighterport-attacks655-20261101/corpus.json'
& scripts/Test-RetargetQualityPairs.ps1 -NativeDirectory $native -Manifest $manifest -SameRigCaseId Peach-Fox-AttackAirN -ForwardCaseId Peach-Fox-AttackS3S -ReverseCaseId Fox-Peach-AttackS3S -OutputDirectory (Join-Path $workspace 'artifacts/Fighterport-quality96-final-pairs3')
$peach = (Get-Content -Raw $manifest | ConvertFrom-Json).Cases | Where-Object Id -eq Peach-Fox-AttackS3S
& (Join-Path $native 'ControlsAcceptance.exe') $peach.SourceModel $peach.Motion (Join-Path $workspace 'artifacts/Fighterport-quality96-postguard-ui') --private-desktop --workspace-same-rig-retarget --clip AttackS3S
```

The helper guard was run as `RetargetAcceptance.exe --same-rig-helper-only`
with the ignored local Mario model matching the SHA-256 in its receipt and a
new ignored output directory. Its absolute private asset path is omitted.

The named post-guard paired run passed, including both dense and default native
trajectory oracles. Isolated `RetargetAcceptance.exe --same-rig-keymode` on Peach
`AttackS3S` with `SourceChannelTimes` and `--same-rig-quality` on Mario
`AttackAirHi` also passed after fresh serialization, reopen and re-save. The
native-only compile after the admission guards passed. A paired rerun against
that current binary passed; its hash is pinned above. The post-guard UI rerun
passed its 15 private-desktop checks. The helper-only guard regression passed
with explicit Grounded refusal. The ignored local launcher setting resolves
the preview.96 native build through `Start-FighterPort.ps1 -Check`; that
setting, generated binaries and private fixtures are excluded from Git. These
local receipts are bounded numeric evidence. They do not establish
continuous-time equivalence, arbitrary rig or
action quality, human usability, editor parity on other formats, game playback
or acceptance in any destination game. Unchecked mesh topology, material,
normal and UV properties are not proven identical by exact-copy admission;
sampled skinned geometry is verified for the fixtures. Physical input, monitor
DPI and actual game checks remain separate gates.

## Destination policy and next work

Only **Melee, Brawl, Ultimate and Rivals of Aether 2** are intended destination
games; each can also be a source after its own verified adapter exists. Current
native implementation is bounded to Brawl and Melee, with Melee fractional-time
FOBJ output still open. Ultimate and Rivals 2 need native source and destination
adapters. BT3, FighterZ, Sparking ZERO, Zenkai, Battle Stadium D.O.N, Xenoverse,
Naruto, Jump Force, One Piece and future donor games are source-only. Existing
BT3 viewer/editing and portable output do not qualify a BT3 game-native
destination.

The next retarget-quality step is a non-identical-rig failure with complete
source, mapping, solver, native output and skinned/contact witnesses. Preserve
the existing numeric bounds and distinguish missing context, unsupported
anatomy, failed solving and presentation defects. UI simplification, portable
GLB import/direct FBX writing, Brawl/Melee native output completion, Ultimate,
Rivals 2, additional source adapters and further filesize work remain bounded
later increments in the same sole incomplete G1/M2 milestone.
