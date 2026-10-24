# Owned-engine integration ledger — 23 October 2026

24 October preview.14 adds original owned manual bone-contact authoring and
bounded native derivative projection through the existing isolated host, using
retained owned Core IK/contact/curve APIs. Original Next v7 and existing
Contacts.cs numerics remain unchanged. Native profile inventory/export uses
existing BrawlLib resource APIs; no external source or package is added.
BrawlLib TKContext/GLPanel receive Windows window/DC ownership cleanup patches;
the component revision/dependencies/licenses/notices remain those pinned below.
The existing OpenTK.dll hash is reverified by Build.ps1. Microsoft's primary
[ReleaseDC documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc)
and [window DC lifecycle documentation](https://learn.microsoft.com/en-us/windows/win32/gdi/window-display-device-contexts)
were consulted for the ownership audit; no external implementation was copied.
Supplied game inputs, reference bytes, private captures and binaries stay local.
See `CONTACT_AUTHORING_ACCEPTANCE_2026-10-24.md` for final scope and evidence.

24 October preview.13 adds original owned contact dependency protection and custom
slot synchronization. The native adapter reuses the existing original Next
support-region capture and skin weights with the exact reviewed mapping; it does
not change the retargeter, contact solvers, rig data or numerical acceptance bounds.
Native slot controls/metadata and the Core workspace transport use existing
framework/serialization APIs. No new third-party component, package, binary or
notice obligation is introduced. Fixtures, snapshots and compiled outputs remain
local. See `CONTACT_SLOTS_ACCEPTANCE_2026-10-24.md`.

24 October preview.12 adds an original owned Core analytic Hermite boundary-fit
operation, its isolated host transport and native review/metadata adapters. It
uses existing Core curve/storage contracts and native BrawlLib key APIs, plus
the already shared native model previewer renderer. It introduces no external
source, package, binary dependency or notice obligation. The original Next
retargeter and existing contact solvers are unchanged. Frozen native reference
snapshots and supplied fixtures stay local; source-only publication includes
procedural checks and generic acceptance records.

24 October preview.11 ports the existing owned Core scene/body fitting review
and exact companion-context reader through native windows and the isolated
host. It reuses native ModelPanel/MDL0 material/shader rendering and extracts the
existing model-overlay drawing into a shared native method. Numerical algorithms,
the original Next retargeter, package versions and native component notices are
unchanged. No new third-party component or dependency is introduced; all model
and reference resources remain local and excluded from source publication.

24 October preview.10 reuses the already imported owned Core
`AnimationControls.DefaultPole` geometry and `AnimationControls.Solve` algorithm
from the revisions recorded below, adapting their native display/transport only.
It adds native source-bound batch review and original curve timing resampling
using existing BrawlLib APIs. The original Next retargeter is unchanged. No new
third-party component, package, native binary or notice obligation is introduced.
Engine build commands now disable the network NuGet audit and shared compiler
for repeatable cached-package builds; package versions and source notices remain.

Preview.9 ports existing owned Core chain/pole/role review through a new native
window and the same isolated host. It adds no external component, dependency or
solver tuning. Native print painting recolors only the new window's unused tab
strip. The original Next retarget implementation and component notices remain.

Preview.8 adds native FK/IK/pin selection, shared gizmos, original transform and
interpolation controls, and a Core character-workspace transport. Workspace
storage reuses the already audited owned CharacterWorkspace/CharacterStore;
new WinForms adapters remain in the native app. System.IO.Compression and its
FileSystem assembly are part of the existing .NET Framework prerequisite; no
new downloaded dependency is added. BrawlLib's endian-single conversion now
swaps integer bits to avoid x86 signalling-NaN payload corruption. This changes
reader correctness, not the original Next retarget algorithm. Exact-bit and
fresh supplied-model curve evidence are required. Native snapshots, commercial
inputs, captures and compiled outputs remain excluded from source publication.
Follow-up: the default native retarget action is restored from this repository's
existing Next revision `96a4e0e81b1ad79837c307d57197ecd4c756179b` at the user's
explicit request. Its numerical solver and native dependencies remain unchanged;
added reporting enumerates excluded tracks and the UI snapshots/protects inputs.
The .NET10 engine continues to supply editor controls/pins and retained CLI
contracts. No additional component or external dependency is introduced.

The user explicitly selected FighterPort Studio Next as the main product on
23 October 2026 and authorized integrating their standalone work. The source
revision is `DoctorFlux/fighterport-studio` `153726b632a99f34afe2b837802c1cb29052f2e0`;
its application implementation is `4752bb4ff4fa04e216b5e3977c1675a7debf3286`.

Selected components are the original FighterPort Core, Brawl adapter, CLI,
compatibility worker and their verification sources. The Avalonia application
shell is not imported. Core and the adapter have no external package dependencies;
they target .NET 10. The worker targets .NET Framework 4.7.2/x86, references the
replaceable BrawlLib/OpenTK already audited in this product and uses
Microsoft.NETFramework.ReferenceAssemblies 1.0.3 for compilation only (MIT).
The CLI and bridge are local command/process interfaces, not embedded editors.

Preserve all original source headers. The native foundation retains this
repository's LGPL-3.0 license and existing component notices. .NET is a separately
installed prerequisite unless a separately audited runtime package is produced.
No proprietary model, animation, settings, capture, native binary or package cache
is imported. Source-only publication is authorized; this ledger does not authorize
binary release distribution.

The original full product scope and numerical contracts are retained under
`docs/engine/`; historical evidence is evidence for the old application revision,
not acceptance of this integration. Automatic retarget/contact failures remain
failures. Only `docs/MASTER_PLAN.md` and `docs/plans/current.md` govern execution.
