# Integrated Brawl disc reader

Preview.36 replaces the external-reader route with an original managed C# reader
inside Fighterport. ISO/WBFS discovery, decryption, filesystem validation and
complete fighter-library extraction run in the application. No Dolphin executable,
Dolphin library, key.bin, console dump, PATH entry, automatic download or image
conversion is used by this route. Extracted Brawl folders keep their existing path.

## Reference audit and dependencies

The preserved Studio Next source inspected was revision
`1cc96940bb58a6bab2f5a5c6ab9fff03e3d4165d`. Its library GameSource implementation
used an external reader. Its inherited BrawlLib `ISONode.cs` contains ISO/partition
structure and AES code, but prompts for key.bin, has no complete native fighter
filesystem population, and is unsuitable as the bounded WBFS library service.
It remains unchanged as an inherited native editor component. This finding applies
to that exact checkout, not every historical build or the user's earlier session.
The [source import record](SOURCE_IMPORT_2026-10-24.md) covers its licenses/revision.

The new `BrawlDiscImage` does not use ResourceNode disc mapping, unsafe pointers,
UI dialogs, subprocesses or an additional NuGet/native dependency. It uses .NET
Framework 4.7.2 FileStream, AES-CBC and SHA-1 implementations. The known retail Wii
format constant is internal; no user/console-specific key is loaded or stored.

Format facts were checked against upstream Dolphin's public reference code:
[WBFS mapping](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/DiscIO/WbfsBlob.cpp),
[Wii partition sampling and cluster hashes](https://github.com/dolphin-emu/dolphin/blob/409881c34357d0f105fd473167d15ab0bd9c1628/Source/Core/DiscIO/VolumeWii.cpp),
[filesystem entries](https://github.com/dolphin-emu/dolphin/blob/409881c34357d0f105fd473167d15ab0bd9c1628/Source/Core/DiscIO/FileSystemGCWii.cpp),
[ticket layout](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/IOS/ES/Formats.cpp)
and [retail format constants](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/IOS/IOSC.cpp).
Unpinned links identify the public reference examined on 26 October 2026; pinned
links identify release 2609a source. Those files declare GPL-2.0-or-later. No Dolphin
implementation is compiled, linked, copied into a dependency, or distributed here.
The independent C# module is part of Fighterport under its existing repository
license. Existing upstream notices and the historical dependency record remain.

## Reader boundary

The reader identifies a Wii/Brawl header, resolves one DATA partition across the
four partition groups, validates ticket/data/FST extents, and reads native files
using Wii word offsets. Encrypted hashed, unencrypted hashed and unencrypted raw
partition layouts are distinct. Encrypted data uses retail common-key index zero;
other ticket/key types reject. Requested hashed clusters verify H0/H1/H2/H3 before
publication to the caller. This does not certify Nintendo signatures, an entire
image's authenticity, or in-game behavior.

WBFS maps logical disc blocks to bounded physical allocations, including a single
occupied later slot and noncontiguous/permuted sectors. Missing required blocks,
duplicate/overlapping allocations, multiple discs and split companions reject.
ISO access streams 64-bit offsets without mapping a whole disc into address space.
Reads are capped at 32 MB; FST metadata at 32 MB/200,000 entries/64 directory levels;
fighter files at 10,000; complete fighter extraction at 4 GB and individual files at the
existing 1 GB native asset bound. Unsafe, ambiguous, duplicate or out-of-range
paths/extents reject. New cache files use CreateNew; no original is overwritten.
Cancellation is checked through reading, hashing, verification and copying.
Unpublished candidate files can remain local on failure; no catalogue generation
or target revision is published from a failed preparation.

New disc provenance is `fighterport-brawl-disc-v1` with the complete original image
hash, DATA partition, native virtual paths and resource/bind hashes. External-tool
fields are null. Existing `dolphin-brawl-disc-v1` catalogues and saved recipes keep
that historical identity and stay readable/repairable from their cached native
files without running or locating their previous executable. No schema migration
or competing project state is added. The legacy request property is retained for
source compatibility and is ignored by the new reader.

Only supported native Brawl resources proceed through existing association review,
costume eligibility, source preview, independent target opening and original Next.
Reader work is explicit preparation only; playback and scrubbing use prepared
native data. RVZ, other games, split WBFS, other key/ticket families, disc writing,
repacking and game acceptance remain outside this reader's qualification.

Adding a disc or extracted Brawl source indexes the complete fighter subtree in
one background preparation and atomic catalogue transaction, including shared
fighter resources. No character-selection dialog is shown. The bounded component
API retains optional explicit scopes for existing callers. Native MotionEtc
family/container identity disambiguates body models; exact same-resource-scope
track binding can associate local articles/effects. Authored bone bindings and
resource hashes are checked independently. Ambiguous associations and unsupported
channels remain visible and unavailable for conversion until reviewed. Costumes
retain exact native identities and existing hierarchy/bind compatibility checks.

Each decoded native package has a separate disposed lifetime before metadata-only
association. During explicit background preparation, bounded checkpoints compact
reclaimable 32-bit native-key/geometry heap allocations and complete pending native-buffer finalizers every eight
files. This is not a playback, scrub, search or renderer operation.
