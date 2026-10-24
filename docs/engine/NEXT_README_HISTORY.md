# FighterPort Studio V2

A new independent BrawlCrate-derived character editor. The first milestone is a
small, usable native workflow: **New Fighter -> Open Model -> Add Animations ->
Play / scrub -> Save**. A fighter can contain a model without animations, or be
saved empty and completed later.

The portable **2.0.0-preview.2** testing build uses Release/x86 and .NET Framework
4.7.2. See [preview instructions](docs/RELEASE_PREVIEW.md).
`scripts/Package-Release.ps1` builds a clean runtime ZIP with licenses, checksums
and corresponding source; outputs remain local under ignored `artifacts/`.

This is an early Brawl / Project M / Project+ milestone. Automatic humanoid
characterization uses bone-name evidence, hierarchy and bind geometry, with a
compact mapping review for ambiguous rigs. There are no character-specific
retarget profiles. Retargeting includes bind-space skeleton calibration,
FK, two-bone IK, separate limb proportions and skinned ground contacts across
the hands, feet, trunk, head, arms and legs. Stationary contacts lock in place;
a simultaneous body solve fits sitting, lying and supported poses without
stretching limbs. The native preview shows a floor and an oblique contact view;
use **Fighter -> Ground Contact View** to reframe the current pose.
**Fighter -> Retarget Options** chooses Auto, Grounded, Airborne or Source contacts;
airborne retargets keep FK, proportions and limb IK without floor correction.
Auto stabilizes persistent near-floor stance and transfers supported hip bounce
using measured leg proportions. Source contacts preserves small authored lifts
when that stance interpretation is inappropriate.
Auto also detects raised-hand suspension and later standing support within a
motion, using source geometry and movement. The preview keeps its normal floor.
Auto requires credible supporting geometry before treating a reference-plane
crossing as ground contact. It continues validated heel/toe roll near the plane,
then releases unsupported flight. Game-driven clips can omit their jump arc;
retargeting preserves their authored actor movement rather than inventing it.
The shared source-bound Brawl library supports **Add Source → Preview Original
→ Use on This Model**, with one persistent catalogue across all fighters.
Expand/collapse games, characters and original native models, search clips,
repair missing files or remove a source without deleting files or target revisions.
Expanding a source model lists its complete indexed animations; **Clear** removes
an active search. All indexed characters remain available in the outline.
**Sources… → Game / Build Paths** saves your own ROM, SD, dump or installation
paths, including multiple Project M builds and additional typed game names.
Brawl ISO/WBFS browsing reads the native character roster through an explicitly
selected modern DolphinTool. Select characters to index all their native motion
resources. Other games' native adapters remain pending.
See [the library workflow](docs/ANIMATION_LIBRARY.md).
General rig authoring, slots, multi-game builds, Melee/Ultimate/Rivals II
adapters and Dragon Ball imports are requirements in [the master plan](docs/MASTER_PLAN.md),
not claims of current support. Gameplay scripting and in-game testing come later.

Use the **Fighter** menu in the real native editor to create or open a fighter.
The native Model Previewer keeps its animation list, model viewport, timeline,
resource editing and playback. `.fpstudio` stores project metadata and verified
local asset copies. Sources are staged before editing, and native export uses a
new filename. Projects reopen paused at the saved clip/frame.

To use Mario's motions on Snake: open Snake's model, add Mario's motion PAC,
select a body clip and click **Retarget to This Model**. Choose Mario's source
model once. The new **clip → Snake** appears in the original animation list;
play, scrub, edit and save it normally. **Export** writes a new native CHR0, or
replaces its matching slot in a new copy of a Snake motion PAC template.
Original inputs remain intact. See [retargeting](docs/RETARGETING.md) for limits
and measured native-output results.

Actor travel is preserved independently of limb proportions; skin contact IK
corrects supported anatomy without multiplying native root movement. The
[research notes](docs/RETARGET_RESEARCH.md) explain the Xenoverse-tool and engine
references, current normalized-axis contract and remaining limits.

The same workflow takes Snake's motions onto Mario: open Mario as the target,
add Snake's motions and choose Snake as their source. Solver math uses semantic
roles and measured geometry; it does not depend on those character names.
The same characterizer handles common naming conventions and anonymous-joint
humanoids; uncertain anatomy/orientation requires review under **Fighter ->
Review Skeleton Mapping**. Current characterization assumes a humanoid rest rig
in the adapter's Y-up space. Other topologies and additional games require their
own rig definitions and native adapters. Universal-model support is the product goal.

Build instructions and exact checks: [TESTING](docs/TESTING.md).
Current evidence and limitations: [DEVELOPMENT_STATUS](docs/DEVELOPMENT_STATUS.md).
Per-operation support: [FORMAT_SUPPORT](docs/FORMAT_SUPPORT.md).

Source pin: `d56cbac89dc2cd0e5c3f923e1d03041b16902add` from
[BrawlCrate](https://github.com/soopercool101/BrawlCrate). The independent initial
commit is `7d2810eb547b9b6860cd0b0515d8076fd8ec922c`. See
[UPSTREAM](UPSTREAM.md), [component notices](THIRD_PARTY_NOTICES.md), the retained
[upstream README](docs/UPSTREAM_README.md) and [LGPL license](LICENSE).

The old FighterPort project is historical reference only. No old custom UI,
services, rig-mapping forms, retargeter, project schema or Git history was imported.
Commercial assets, project copies, generated executables and local evidence are
excluded from source history. The independent public repository is
[DoctorFlux/fighterport-studio-next](https://github.com/DoctorFlux/fighterport-studio-next).

---

The original upstream README is retained in `docs/UPSTREAM_README.md`.
