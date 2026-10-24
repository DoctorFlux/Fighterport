# Shared animation library — L0

Open a target model in the native editor, then choose **Library** (or
**Fighter → Animation Library**). The drawer is optional and leaves the native
viewport, timeline, resource tools and playback controls in place.

1. **Add Source**: select the original model package, exact native model and
   whole motion package. Choose the source game/build. Review the model/motion
   association once for the complete body-motion set. Filenames and matching
   bone names alone never approve ownership.
2. Search and expand **Game → Character → Exact source model → animations**.
   All three group levels collapse. The editable character label is independent
   of exact native model/variant, resource and rig identities. Unverified
   articles/channels remain under their owning source character. Every matching
   character appears immediately, independently of clip ordering. Expand its
   source model to list its complete indexed animation set; no global first-page
   limit hides the rest. Clear removes a leftover search. Counts distinguish
   matching animations from the complete catalogue.
3. **Preview Original**: the drawer's native renderer plays/scrubs the exact
   saved original model and motion, read-only. It cannot replace or pose the
   editable target in the main viewport.
4. **Use on This Model**: reverify and resolve the original automatically,
   run the existing humanoid retargeter and add a new independent target CHR0
   revision. Play, scrub and edit it using the original editor. Repeating Use
   creates another revision. Export CHR0, or a matching-slot target motion PAC
   using an authentic target template, to a new output file.
5. **Repair**: locate missing model/motion files or disc images with exactly the indexed
   bytes. Repair retains every immutable model/clip ID and approval. Changed
   bytes require a new source and review; they are never silently accepted.

**Sources… → Registered Sources** removes a selected source from the library
without deleting its original files or any previously baked fighter revisions.
Undo Source reverses the most recent catalogue change, including first import,
removal, repair or game-path configuration. Removing a source prevents new
reuse until it is restored or registered again; saved target animations remain
self-contained and editable.

**Sources… → Game / Build Paths** saves a folder, ROM, SD card or archive path per
game/version for Brawl, Project M/Project+, Melee, Ultimate, Rivals II, FighterZ,
Sparking! ZERO and Xenoverse 1/2. Type an additional game name to save future
sources. Use distinct build/version labels for multiple Project M SD roots or
other builds; selecting one entry restores its editable path. Choose ROM / SD /
File accepts disc/ROM/archive images; Choose Folder accepts installs, SD builds,
dumps and Modkit projects. Pointers persist independently of fighters and do
not establish an unimplemented game's native reader or writer.

For Brawl-family sources, **Browse Native Files** lists characters from an
extracted `fighter` folder or a selected ISO/WBFS/RVZ. The first disc browse asks
for an installed modern **DolphinTool.exe** supporting `header -j` and `extract`.
Its SHA-256 is saved; no external executable is bundled or downloaded by the app.
Choose characters or Select All, confirm the original build, then add their whole
native resource sets in one transaction. Only selected fighter folders are
cached. The original image remains read-only; its hash, native game ID/revision,
partition-relative filenames and every decoded resource identity are retained.
The Brawl source route does not implement Melee DAT or other games' animation
adapters, a disc writer or FAT SD-image mounting. Recognized extracted PM/P+
layouts do not yet merge a base game and overlays.

## Identity and persistence

One shared metadata catalogue lives under
`%LOCALAPPDATA%\FighterPortStudioV2\AnimationLibrary`, independently of every
fighter project. `FIGHTERPORT_LIBRARY_ROOT` provides an explicit alternate
directory for isolated tests. The XML catalogue contains metadata only. Disc
sources keep selected native resources in a separate local NativeCache; these
commercial files are never part of a fighter descriptor or public source tree.

Each mount pins provider, game/build, platform, interpretation evidence,
container SHA-256 and size. Models pin native version, exact container child
indices, display resource path, raw resource SHA-256, bind interpretation and
skeleton fingerprint. Clips pin channel/version, resource indices/path/hash,
frame count, loop, timebase, interpolation interpretation and association
evidence. Repeated resource names in different branches remain distinct.
Resource hashes cover the native uncompressed resource slice; the container
hash also pins shared string pools and other dependencies outside that slice.
CHR0 has no stored FPS: Brawl's 60 game ticks/second is recorded as an adapter
interpretation, never as a parsed clip field.

Opening a reusable clip rechecks both native container hashes under read
locks, then the exact native resources, skeleton, versions and timing. Browsing
uses bounded metadata and cached source health. Native clips are decoded
only for selected Preview/Use operations. A changed disc timestamp/size is
reported immediately; exact image hash verification happens in cancellable
registration/Repair, never in rendering or search. Search and initial catalogue
loading run away from the UI thread, cache a validated generation and discard
superseded results. Source and target playback are mutually exclusive. Missing, changed, unassociated and
unsupported channel/version states block those operations with an explanation.

Each generation caches sorted searchable metadata and ID lookups. A query checks
each unique file once, sharing the image's filesystem metadata across its cached
native resources. It does not repeatedly walk every ancestor directory per clip.
Preview/Use still performs full path/link, native hash and resource verification
under read locks. Rebuilding the tree ignores transient null selection events.

The tree initially creates the game/character outline. Source-model groups and
clip leaves are created when expanded, rather than inserting the entire roster
into native controls. Ordinary character sets list together; exceptionally large
model sets offer Show more after each 1,000 entries. Broad searches keep the
outline compact instead of expanding thousands of leaves during playback.
Leaf insertion is time-budgeted across dispatcher ticks so expansion can
continue while the main viewport plays. Fighter playback uses a normal,
nonblocking UI scheduler; the native evaluator reads baked animation keys.
No live retarget/IK solve is added to the playback loop. Realtime playback samples
the baked native keys against elapsed time, including native subframes; delayed
UI ticks do not queue a burst of stale poses. The camera remains fixed during
playback, including authored root motion. Manual framing and native camera
controls remain available.

Updates publish a flushed new immutable XML generation by same-directory
rename, guarded by a bounded cross-process mutex. Readers see complete
generations. The latest three are retained for recovery; legacy catalogue.xml
is readable. This avoids ReplaceFile failures on encrypted Windows profiles.
Cancellation leaves the prior committed generation intact. Malformed XML,
external entities, oversized documents and unsafe paths fail without rewriting
the original index.

Each generated target revision saves a pinned source reference, source/target
rigs, target-model bytes, mapping hash, solver settings, initial bake hash and
last validated native export receipt. Its local native animation can change
without changing the shared source or another fighter. Existing schema-1
projects remain readable; baked library projects can reopen offline without
the catalogue or original donor files. New reuse still requires the originals.

## Current evidence and limits

Real Mario/Snake packages contain 1,136 indexed animation resources, including
652 CHR0 motions. 614 body clips pass the reviewed pair plus native body-track
binding rules. Other clips remain unassociated or unsupported; this release
does not convert facial/material/article channels into body animations.
The supplied packages are user-reported Dolphin extractions. The supplied
WBFS has an observed RSBE01 revision 1 disc header. Loose package membership in
that disc is not assumed. Separate image-source tests extract native files
directly from WBFS/ISO and retain that exact origin; source models can differ
from the supplied loose examples.

Native build association requires reviewed ownership, a unique canonical body
candidate, mapped body tracks and at least 90% native track binding. Naming alone
does not approve an ambiguous model. Unbound auxiliary tracks (for example native
motion tracks absent from its original model) are listed explicitly and retained
in provenance. They are not silently advertised as exported accessory motion.
Native retargeting still validates source bind translations before baking.

The existing `bind-world-fk-body-contact-ik-v7` solver is unchanged. Source-bound
Wait1 retargeting is natively verified Mario → Snake, Snake → Mario and Mario
→ Mario. Native CHR0 quantization is approximate; the library suite retains
the established 0.06-unit round-trip bound, with zero tested TransN Z change.
Universal rigs, additional native writers, reusable authored control rigs,
slots/misc fill, retiming and multi-game builds remain separate milestones in
the [permanent master addendum](V2_SOURCE_BOUND_UNIVERSAL_ANIMATION_LIBRARY.md).

Library outline preparation runs off the UI thread. Tree replacement removes
leaves incrementally, and expansion inserts short batches without a bulk
EndUpdate layout flush. A new search cancels stale insertions; all indexed
clips remain accessible. Target playback uses the saved native curves, without
rig capture, contact solving, source hashes or catalogue queries per frame.
Inactive native companion channels reset once during body-only playback;
manual edits, seeks and active companion channels retain full native evaluation.
