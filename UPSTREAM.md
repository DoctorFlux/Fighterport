# 24 October 2026 public source import

The active public product repository is https://github.com/DoctorFlux/Fighterport.
This imports the BrawlCrate-based Next source at 07cb03299b8a3e63f30642dd8e45a3403dacaaa7
with its already integrated owned engine and reconciled product plan. Both predecessor
repositories are private preserved histories. See docs/SOURCE_IMPORT_2026-10-24.md.
Older no-owned-import, reset, new-repository and destination wording below records
historical decisions; current AGENTS.md and the sole master/current plans govern.
All upstream source authorship, licenses, notices and the native source pin remain.

# Pinned upstream source

FighterPort Studio V2 is an independent derivative of BrawlCrate/BrawlLib.

- Source: https://github.com/soopercool101/BrawlCrate
- Pinned commit: `d56cbac89dc2cd0e5c3f923e1d03041b16902add`
- Import: tracked source exported with `git archive`, without any upstream Git objects/history.
- Submodules: none at this pin.
- License: inherited LGPL-3.0 text in `LICENSE`; preserve all copyright headers and component notices.

The first commit imports the tracked source, adds this provenance record, and
extends ignore rules. It intentionally excludes these tracked binary artifacts
from source history: `BrawlCrate/discord-rpc.dll`, `BrawlLib/OpenTK.dll`,
`BrawlLib/Internal/Windows/Controls/Hex Editor/HexBox.snk`, and
`Updater/CredentialsHelper.exe`. The pinned OpenTK runtime dependency is restored
locally, verified by SHA256
`931e38b47b824c316a55b5a54d55650c978ca90dde1daea02299eddd94e719c0`;
it is not original FighterPort code. No signing key or credential helper is used.

Four inherited embedded game texture templates under
`BrawlLib/HardcodedFiles/Textures/*.tex0` are also excluded from source history
and V2 distribution. Their asset provenance was not established. Native import
from user-supplied textures remains available. The 460-byte `Static.mdl0` helper
is retained: native inspection found one bone and zero geometry/materials/textures.

Original BrawlCrate source was built in Debug/x86 and Release/x86 with VS2022
MSBuild and the .NET Framework 4.7.2 targeting pack before this import. An
external MSBuild tail import suppressed the inherited updater/Cargo/copy/delete
pre/post-build hooks; no tracked upstream source was changed. Native model and
separate motion loading, rendering and playback were observed in the original
GUI. Local evidence stays outside source history.

BrawlCrate contributors include soopercool101, NuclearNecrosis, Simon381, Onium
and Squidgy. BrawlBox/BrawlLib originated with Bryan Moulton (Kryal), BlackJax96,
libertyernie and Sammi Husky. Color Smash includes work originally by Peter
Hatch. See the retained upstream README, headers and component licenses for
complete applicable credits.

`DoctorFlux/fighterport-studio` is historical reference only. No code, custom
UI/services, retargeter, project schema, Git objects or task queue were imported
from it. New product modifications are recorded as subsequent independent
commits and described in the V2 development status.

V2's first retargeting implementation adds independent format-neutral humanoid
characterization, bind-world FK/IK, measured proportions and skinned end/body
contacts. The native adapter writes existing Brawl CHR0/PAC formats; native
Hermite tangents are retained through I12 decode/edit/re-export. The original
viewer adds a measured-height filled floor and contact camera presets, with
separate retarget options and a compact mapping review in the Fighter menu.
Godot's public retargeting documentation informed design review; no Godot or
retired FighterPort retargeting implementation was imported.

18 October 2026: independent changes separate actor trajectory from anatomical
proportion correction, preserve native trajectory cubics, and infer per-surface
floor availability while rising through the reference plane. XenoKit source and
Godot documentation informed the design review recorded in
`docs/RETARGET_RESEARCH.md`; no implementation from either was transplanted.

The source-bound library increment independently adds shared immutable source
records, native inventory/reuse and read-only game-source browsing. Optional
DolphinTool remains external and hash-pinned, with notices in THIRD_PARTY_NOTICES.md.
Native model playback gains bounded UI dispatch, elapsed-time curve sampling and
passive diagnostics. Retarget camera tracking is removed. No retired FighterPort
library, UI, project service or retargeter implementation was imported.

The preview.3 playback audit compares the clean pinned upstream's timer, native
pose evaluation, renderer and editor callbacks directly. The MDL0 bone/skin
evaluator is retained. V2 replaces the opted-in busy-loop/coarse wakeup scheduler
with a bounded WinForms message timer, reduces transport notifications, and resets
inactive companion channels once during body-only playback. Explicit edits and
active material/morph/scene animations keep the complete native evaluation path.
Library source preview still owns a separate read-only model. No live retarget
solver is added to either playback path.

The message-timer choice follows a visible original-MotionEtc reproduction on
this machine, including the pristine editor component. Near-60 swap-call counts
did not prove smooth display. Faster polling and buffer/context experiments
were rejected after visual regression; no such rendering workaround is shipped.

Preview.4 adds an independently written Windows presentation adapter around the
retained native renderer. It copies the existing OpenGL back buffer on the GPU
into a shared D3D11 texture, then presents through a windowed DXGI flip chain.
Unsupported paths retain the preview.3 message timer. The high-resolution
one-shot clock only invalidates the viewport; model work stays in its UI paint.
The adapter changes neither native animation evaluation nor retarget math.

API references: [Khronos WGL_NV_DX_interop2](https://registry.khronos.org/OpenGL/extensions/NV/WGL_NV_DX_interop2.txt),
[Microsoft DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model),
[D3D11 device/swap-chain creation](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-d3d11createdeviceandswapchain).
SDL's [Windows OpenGL presentation discussion](https://github.com/libsdl-org/SDL/issues/5797)
informed the investigation; no SDL, retired FighterPort or third-party adapter
implementation was transplanted. COM declarations were checked against the
installed Windows SDK and validated by actual native rendering/presentation.
