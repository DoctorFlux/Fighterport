# Third-party notices and source provenance

23 October owned-engine integration: see `docs/ENGINE_PROVENANCE.md`. Selected
Core/adapter/CLI/worker sources are pinned to standalone main `153726b632a99f34afe2b837802c1cb29052f2e0`.
No Avalonia application shell is imported. The optional regression renderer uses
the exact MIT Avalonia/Silk.NET dependencies and notices in
`engine/licenses/package-ledger.json` and `engine/licenses/packages`; it is not
part of the native product's authoring UI. Local self-contained engine builds use
Microsoft.NETCore.App.Runtime.win-x64 10.0.13, MIT with bundled third-party notices,
revision `4099f83977f4d29604a9aeb971fb0a7bc1d5f699`. Runtime notices accompany local
outputs. No native or runtime binary is added to source history or released.

FighterPort Studio V2 is derived from BrawlCrate/BrawlLib, pinned at
`d56cbac89dc2cd0e5c3f923e1d03041b16902add`. Preserve the root LGPL-3.0 license,
inherited source headers, credits and the upstream README. See `UPSTREAM.md`.
The independent repository/history does not change that provenance.

| Component | Pinned source/version | Retained notice / distribution status |
| --- | --- | --- |
| BrawlCrate, BrawlBox, BrawlLib | Upstream pin above | `LICENSE`, original headers and `docs/UPSTREAM_README.md`; native application foundation |
| OpenTK | Upstream DLL SHA256 `931e38b47b824c316a55b5a54d55650c978ca90dde1daea02299eddd94e719c0` | `licenses/OpenTK-LICENSE.txt` and provenance file, including Mono/Tao/OpenEXR text; explicitly restored app-local dependency |
| Ookii.Dialogs | Included upstream source | Original `BrawlLib/Internal/Windows/Forms/Ookii.Dialogs/license.txt`; copied to `licenses/IncludedSources` |
| Color Smash | Included upstream Rust source | `ColorSmash/LICENSE` is **AGPL-3.0**; retained separately, helper not built or bundled by this milestone |
| DynamicLanguageRuntime | NuGet 1.3.4 | Package Apache-2.0 license retained under `licenses/NuGet`; inherited scripting runtime, automatic scripting disabled |
| IronPython | NuGet 3.4.1 | Package Apache-2.0 license retained under `licenses/NuGet` |
| IronPython.StdLib | NuGet 3.4.1 | Full package `StdLib.License.txt` retained, including applicable standard-library notices |
| System.IO.Hashing | NuGet 8.0.0 | Package MIT license and third-party notices retained |
| System.Buffers | NuGet 4.5.1 | Package `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` retained |
| System.Memory | NuGet 4.5.5 | Package license and third-party notices retained |
| System.Numerics.Vectors | NuGet 4.5.0 | Package license and third-party notices retained |
| System.Runtime.CompilerServices.Unsafe | NuGet 4.5.3 | Package license and third-party notices retained |
| Doxygen | NuGet 1.8.14, inherited build reference | Documentation tooling only (`PrivateAssets=all`); not invoked or distributed in the application output |

NuGet notice files were copied verbatim from the exact restored packages named
above. Application builds include the retained license directory. Windows/.NET
Framework development and runtime prerequisites are separately installed system
components, not bundled installers.

Compiled dependency artifacts, signing material and the upstream credential helper
are excluded from Git. Four inherited `.tex0` templates with unestablished asset
provenance are excluded from source history and product embedding/distribution.
The retained 460-byte `Static.mdl0` is an empty one-bone helper with no geometry,
materials or textures. User-provided commercial models/motions, all projects and
test captures stay local and ignored; none is included in the code repository.

The original source includes additional component-level author/copyright comments;
those are retained in place. New integrations require their own pinned dependency
and notice review. This inventory does not claim that future adapters or native
writers have been implemented.

## Optional external disc reader

Brawl ISO/WBFS/RVZ source browsing can invoke a user-selected modern
[DolphinTool](https://github.com/dolphin-emu/dolphin), GPL-2.0-or-later.
The integration uses its documented `header --json` and `extract --list/--single`
CLI, records the selected executable's SHA-256 and keeps inputs read-only.
DolphinTool and its dependencies are not included in this source repository or
portable application. The locally tested version is Dolphin 2609a. Older builds
without the extraction command are not sufficient. Upstream command behavior is
documented in [ExtractCommand.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DolphinTool/ExtractCommand.cpp).
