# FighterPort Studio V2 — 2.0.0-preview.4

Windows portable testing preview, built in Release mode for x86 using .NET
Framework 4.7.2. Windows x64 can run this x86 application. The framework must
already be installed. This is an unsigned preview, not a stable final release.

Extract the complete ZIP into a writable folder, then open
**FighterPortStudioV2.exe**. Keep its DLLs, configuration, lib and licenses
folders together. No installer or file association changes are needed.

Choose **New Fighter → Open Model → Add Animation → Play → Save**. Model-only
projects work too. To reuse a motion, select it and choose **Retarget to This
Model**, then select the original animation's model. Leave ground contact mode
on **Auto** for automatic support inference; use **Airborne**, **Grounded** or
**Source Contacts** when the source data needs an explicit interpretation.
The Ground Contact View menu offers a normal floor and low side camera.
The camera stays fixed during playback, including root motion. Retargeting bakes
once into ordinary native keys; playback does not rerun the retarget/contact solver. The player follows
elapsed time and samples native subframes to avoid delayed-frame catch-up bursts.
This preview reduces repeated main-editor work during body-only playback and
spreads library search/tree updates across short UI batches.
The main player now presents the existing native OpenGL image through a shared
GPU surface and Windows DXGI flip presentation when supported. Original MotionEtc
and saved Mario-to-Snake idle playback reach approximately 60 displayed FPS on
the tested Windows/NVIDIA system, without a live retarget solver. A bounded paint
clock requests one frame at a time; minimized rendering stops. Native screenshots,
scrubbing and editor controls remain available. Unsupported graphics paths keep
the smoother legacy UI timer; 60 FPS is not promised on untested hardware.

**Library → Add Source** registers an original model and whole motion set once.
Review their association and game/build, then browse **Game → Character → Exact
Model → animations**. All group levels collapse; source-model expansion lists
its complete indexed animation set. Clear removes an active filter. **Preview Original** plays the
exact source read-only inside the drawer; **Use on This Model** resolves it
automatically and creates an independent target revision in the native editor.
One shared catalogue persists across all fighter projects. **Repair** requires
the same original bytes. **Sources…** removes registrations without deleting
original files or saved target revisions, and configures per-game/build folder
or archive paths, including multiple Project M SD builds and additional typed
game names. **Browse Native Files** reads selected Brawl ISO/WBFS/RVZ or extracted
fighter folders through a modern installed DolphinTool (chosen by you). Only
selected character folders are cached; the original image is read-only. Other
games' saved paths remain configuration until their native adapters exist.
ISO/WBFS have independent native acceptance; RVZ remains separately unvalidated.
See docs/ANIMATION_LIBRARY.md in the source.

This preview reads Brawl MDL0 models/CHR0 motions in supported native containers,
retargets characterized humanoid rigs and exports fresh CHR0/PAC output. It
preserves the native BrawlCrate editor, timeline and resource tools. New fighter
projects use .fpstudio. Export to a new file; keep your original inputs.
Commercial models and motions are not included. Other planned games do not
have validated native import/export adapters in this preview.

Auto uses skin geometry, motion and supporting contact patches, without
character or animation profiles. It releases unsupported flight poses and
corrects grounded soles, hands and body contacts across different proportions.
Some game animations omit movement supplied by gameplay. This preview does not
invent a missing jump arc or change authored actor travel to place it above the
floor. Ambiguous poses can need a mode override or mapping review. Non-humanoid
rigs and every possible animation/body proportion are not yet validated.
Native Euler curve baking is approximate between keys; difficult body-contact
bakes can take several minutes. Persistent editor preferences on some Windows
profiles can fall back to in-memory settings; fighter projects save separately.

The archive contains licenses, upstream attribution, a manifest and SHA-256
checksums, plus the exact committed V2 source ZIP. Build from that source using
scripts/Build.ps1 (Visual Studio 2022 and .NET Framework 4.7.2 targeting tools).
scripts/Restore-Dependencies.ps1 verifies pinned OpenTK; NuGet restores the
declared managed packages. See UPSTREAM.md and THIRD_PARTY_NOTICES.md for
provenance and dependency licensing. Automatic scripts, update checks and
registry integration inherited from upstream remain disabled by default.
