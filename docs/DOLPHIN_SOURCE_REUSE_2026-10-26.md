# Dolphin disc-source dependency — 26 October 2026

Historical preview.35 record. Superseded by preview.36's
[integrated reader](INTEGRATED_DISC_READER.md): the current application does not
require or launch this external component. Preserve this record for older receipts.

This record identifies the external reader used by the Brawl library workflow.
It records component provenance and the integration boundary; the increment's
acceptance record separately identifies which images and workflows were tested.

## Pinned reference and provenance

| Item | Recorded value |
|---|---|
| Upstream project | Dolphin Emulator Project, `dolphin-emu/dolphin` |
| Release used for local qualification | `2609a`, Windows x64 portable distribution |
| Upstream source revision | `409881c34357d0f105fd473167d15ab0bd9c1628` |
| Executable | `DolphinTool.exe` |
| Local executable SHA-256 | `63DD9BAD68996F4C75302FDE1A2374EBECFA767DF6780619D658F41684F97E8A` |
| Official archive | `dolphin-2609a-x64.7z` |
| Local archive SHA-256 | `BBFD13BF9E6D2A4164A15C392B1F32A9B18150E5136746B388704608FBB60F76` |

The [official release page](https://dolphin-emu.org/download/release/2609a/)
identifies release 2609a and links its Windows x64 archive. The recorded package
comes from that [official archive](https://dl.dolphin-emu.org/releases/2609a/dolphin-2609a-x64.7z).
The hashes above identify the local qualification copies; they are not presented
as an upstream signature or a promise that another reader has identical behavior.
The corresponding [source revision](https://github.com/dolphin-emu/dolphin/tree/409881c34357d0f105fd473167d15ab0bd9c1628)
is retained as the protocol reference.

## License and dependency boundary

The referenced [header command](https://github.com/dolphin-emu/dolphin/blob/409881c34357d0f105fd473167d15ab0bd9c1628/Source/Core/DolphinTool/HeaderCommand.cpp)
and [extraction command](https://github.com/dolphin-emu/dolphin/blob/2609a/Source/Core/DolphinTool/ExtractCommand.cpp)
declare `GPL-2.0-or-later`. Upstream's [COPYING record](https://github.com/dolphin-emu/dolphin/blob/409881c34357d0f105fd473167d15ab0bd9c1628/COPYING)
distinguishes that licensing from third-party file terms and describes the
repository aggregate as GPLv3-compatible. Its per-file SPDX declarations and
dependency notices remain authoritative.

Fighterport invokes the executable as a separate process. It does not import
Dolphin source, link Dolphin libraries into the native app or Core, or vendor the
executable, archive, matching distribution files or runtime dependencies into the
public repository. No new managed or native in-process dependency is added by this
workflow. Existing Fighterport/BrawlCrate/BrawlLib licenses and credits remain.

The runtime dependency is a compatible, explicitly chosen DolphinTool installation
with its matching upstream distribution files. The [official download page](https://dolphin-emu.org/download/)
also specifies the Windows x64 Visual C++ v14 runtime requirement. The application
does not download, install or replace Dolphin or a system runtime automatically.
The ignored local portable qualification copy does not change the user's existing
emulator installations. Public delivery remains source only.

## CLI protocol used

The process entry points use explicit arguments with shell execution disabled:

```text
--help
extract -h
header -h
header -i <image> -j
extract -i <image> -p DATA -s fighter -l
extract -i <image> -p DATA -s fighter/<selected-character> -o <new-cache> -q
```

The upstream [header implementation](https://github.com/dolphin-emu/dolphin/blob/409881c34357d0f105fd473167d15ab0bd9c1628/Source/Core/DolphinTool/HeaderCommand.cpp)
provides JSON game identity and revision metadata. The upstream
[extraction implementation](https://github.com/dolphin-emu/dolphin/blob/2609a/Source/Core/DolphinTool/ExtractCommand.cpp)
provides partition selection, listing and extraction of a selected file or
directory. Fighterport uses those interfaces for DATA roster discovery and
selected character folders, then parses the extracted native resources through
its existing Brawl reader. It does not invoke Dolphin emulation or disc conversion.

Preflight checks help capabilities and Brawl header identity before full image
hashing or extraction. Older convert/verify-only tools are rejected with a
contextual error. Help/header subprocesses have output and time bounds and honor
cancellation. Indexing is explicit, offline preparation: it holds originals for
reading, hashes the complete supported image, writes a fresh cache, verifies
native identities, and atomically registers the selected source set. The source
receipt records the actual extractor hash, image identity, partition, virtual
paths and native resource hashes. Playback performs none of these operations.

## Format limits

The current integration is for Brawl-family native resources. The disc route
accepts single-file ISO/WBFS and the existing RVZ reader route when the selected
tool supports them. Split WBFS companions are rejected because their full source
identity is not yet represented. A valid game header is required; a filename or
saved path alone does not establish a supported game or resource.

Extracted Brawl-family folders use the existing native folder reader and require
no Dolphin executable. Costume enumeration, original preview, independent target
opening and retarget-source use remain separate native operations after indexing.
An installed path for another game does not imply its model, animation, archive or
output adapter exists. RVZ and additional image/build claims require their own
acceptance evidence. No disc writer, base/mod overlay merger, new game adapter or
actual in-game acceptance is introduced here.
