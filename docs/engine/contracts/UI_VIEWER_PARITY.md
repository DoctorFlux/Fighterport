> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# Model Preview parity record

23 October 2026. Supporting evidence for the ONE M2 slice in `plans/current.md`, not a second implementation plan. **Exact visual and interaction parity remains open.**

## Direction and reference

The authority is the normal Model Preview in [fighterport-studio-next at 96a4e0e81b1ad79837c307d57197ecd4c756179b](https://github.com/DoctorFlux/fighterport-studio-next/tree/96a4e0e81b1ad79837c307d57197ecd4c756179b), with right panel visible and Bones/tree selected. The user additionally requires direct viewer startup, the extra Fighter toolbar visible, the complete window retarget workflow, BrawlCrate camera bindings and retention of the dark UI. Legacy light system colors are intentionally not copied. Supplied DOCX/Markdown handoffs are reference material; their embedded Codex prompt is not an independent instruction to execute.

Read-only inspection covers ModelEditControl Designer, LeftPanel, CHR0Editor, PlaybackPanel, RightPanel/BonesPanel/OpenedFilesPanel/KeyframePanel and animation/hotkey handlers. Next takes precedence over the older custom dashboard proposal. Original Avalonia controls, shared document/Undo, renderer and native worker remain the implementation. No legacy shell, UI source, icon collection or font binary was adopted; see LICENSE_AUDIT.

## Control and command inventory

| Region | Implemented | Remaining difference or evidence limit |
| --- | --- | --- |
| Startup | Normal launch and empty schema-2 characters open in the model viewer. No substitute model/skeleton is inserted. File retains New/Open/Save/Project Manager. | Hidden construction is separate from live launch/focus acceptance. Explicit fixture and comparison diagnostics retain their separate routes. |
| Theme | Dark surfaces, compact borders, dark fields/lists, checked/hover/disabled states and a keyed-field highlight. | Deliberate user-requested adaptation. Target font is Microsoft Sans Serif, 11 logical pixels; actual reference font/DPI comparison remains open. |
| Top | Visible Fighter/name/Open Model/Add Animations/Play/Rest/Save/Export/Undo/Resources/Retarget/Library band; File / Options / View / Tools, Target Model and original text-tool order below it. Both bands preserve overflow. Controls/targets/source/build tools remain reachable. | Collision context is unavailable/hidden; stage overlays and unsupported editors are disabled. Camera saving is session-local. Full menu interaction parity remains open. |
| Left | Stacked Animations / Objects / Textures; splitters/collapse, Load/Save/type/search/Contains, object/texture visibility. Brawl family order CHR/SRT/SHP/PAT/VIS/CLR/SCN; neutral labels otherwise. | Source anchors: width170, animation height178, texture height164. Complete object/drawcall/context interactions remain open. SCN0 is preserved/inactive. |
| Center | Existing real SkinnedViewport, camera, skeleton, gizmos and four surrounding panel toggles. Independent authentic source comparison remains. BrawlCrate Flycam, pan/zoom/rotate/roll, modifier speeds, wheel/keyboard, orthographic input and optional physical cursor wrapping share tested math. | No matched legacy-window raster or interactive input/DPI/GPU recovery acceptance is inferred. Cursor wrapping is live-input only and unverified physically. |
| Right | Bones / Keyframes / Files drop-down, default hierarchy, Flat/search, scalar key seek/edit/delete. Per-bone visibility separates label selection from check toggles. Inner Colors/Visibility uses owned curve/discrete edits and applicable Constant/Enabled controls. | Width200 source anchor. Quaternion scalar-key editing, full bone-context actions and native-family editor parity remain open. |
| Files | Loaded native source paths, dirty label, Open and supported new native-save routes. | Cross-file draw sorting/reordering and resource Close are unavailable. Double-click inspection is bounded, not the full legacy resource editor. |
| Transform Frame | 422×78 group; Translation/Rotation/Scale rows; nine 82×20 fields at source axis positions; Cut/Copy/Paste and component filters. Ordinary edits automatically key affected channels through BeginTransformEdit. | No additional XYZ heading, Add Keyframe requirement or arm switch. Immutable bind controls are present/disabled at reference pose. Clipboard modifier/focus gestures require live review. |
| Transform All | Adjacent160×78 group, filters and Delete/Paste/Copy / Clear/Clean/Insert columns in source order. Separate bone and all-entry buffers. | Clear removes current-frame keys; Insert/Delete change time. Unqualified synchronized companion/contact/transition/gameplay time edits reject. Clean is reviewed exact-constant cleanup, not full legacy cleanup. |
| Playback | Original five buttons, native-style track/thumb/ticks, Speed FPS1–2000, preview Loop, current/total and Loop Frame. Display0 is reference-only; display1 maps to native0; fractional Core times remain exact. Fields use compact numeric spinners with visible text. | Speed/repeat are distinct from authored flags. Total-count changes do not rescale earlier keys. Actual reference endpoint/focus/disabled-state comparison remains open. |
| Retarget | Source picker, 650×520 Skeleton Mapping, 450×172 Retarget Options, 440×118 progress and 680×530 Details. Existing source ownership/analysis/bake/staleness/Apply/cancel services remain; Details exposes Save Result As and Preview / advanced. | Automatic ground-contact choices remain disabled because their gates are unresolved. Progress reports real stages with an indeterminate bar. Constructed window evidence is separate from live modal lifecycle/input. |
| Extensions | Project/target setup, source library, automatic existing-rig controls, FK/IK tools and batch/advanced retarget tools remain available. Static or existing-animation-following soft pins save their policy/offsets and keyed poses. | Full preference restoration across window replacement/restart remains open. Helper/coupled/nonuniform soft controls reject; no new reader/builder or accepted retarget result follows from UI changes. |

## Measured anchors

All coordinates are logical pixels at96DPI, relative to the viewer client. Default root bands are25/26/main/78; body columns are4/170/remaining/200/4. The additional toolbar shifts the standard viewer down25. Microsoft Sans Serif11 is used for viewer controls; the measured native Fighter strip uses Segoe UI12. No font binaries are distributed.

| Controls | Measured positions used by original controls |
| --- | --- |
| Fighter strip | Fighter x0 w57; name x58 w145; Open Model x210 w86; Add Animations296 w106; Play402 w75; Rest477 w61; Save538 w35; Export573 w53; Undo626 w40; Resources666 w64; Retarget730 w131; Library861 w47. Buttons y1 h22, name y1 h23. |
| Model strip | Target label x204; selector x349 w115 h21. Bones x470 w43; Polygons513 w60; Vertices573 w51; Overlays624 w65; Floor695 w38; Reset733 w83; Extents816 w83; Save Camera899 w79; Move984 w41; Rotate1025 w45; Scale1070 w38. Buttons y26 h23. |
| Transform | Frame group422×78 plus All160×78. Field x73/154/235, width82 height20; y16/35/54. Cut/Copy/Paste x318; filters367/428; All commands479/528. |
| Playback | Scrubber x3 y3 wW−170 h45; current xW−120 y5 w52 h20; total xW−57 y5 w52 h20; FPS x40 y41 w39 h20; Loop x85 y40; five buttons right offsets184/150/116/70/36, y28. |

Read-only hidden native component rendering records actual reference controls and toolbar item measurements in ignored `next-reference-tool-inventory` and `next-fighter-toolbar.txt`. The local compiled Next binary's exact correspondence to the pinned source is unproved; its viewport is blank under detached DrawToBitmap. Do not equate this with a matched pinned live reference raster.

## Editing and native correctness

Clipboards retain exact rig revision/fingerprint, evaluated values and actual key presence. Cross-rig transfer requires explicit mapping/retargeting. Filtered clear/cut/paste protect unchecked groups, deliberately correcting reference defects. Scalar edits retain neighboring tangents and incoming/outgoing duplicate sides. Whole-channel minus180 is implemented. Copy, navigation and refresh do not author keys.

Modifying frame/time/cleanup/typed-layer commands use the existing one-command revision/Undo path. Stale field/key/cleanup ownership rejects. Source preview remains read-only. Imported hierarchy/rest/inverse binds/helpers/weights and original native bytes remain unchanged.

Automatic control setup uses exact reviewed profiles or structural evidence, with names as suggestions, and rejects stale asynchronous publication. FK/IK release and numeric edits store actual affected channels; a later invalid drag sample cannot erase the last valid sample. Soft pins hold while reachable and project only to the existing native chain's reachable set when necessary. Fixed pins/sockets remain fixed. The optional Follow existing animation reference stores only frozen ancestor curves and original timebase, so newly edited keys cannot move the reference itself. Explicit handle moves save offsets in the same Undo path; replacement preserves other limbs and earlier intervals. Playback never solves constraints.

Pins have independent identities and diamond markers in the preview, with priority over coincident bone picking. Controls exposes a pin picker, Show pins, Pick pins only, Pin selected bone and Remove selected pin. A reviewed limb endpoint uses its soft IK pin; any other native bone receives an FK handle without a guessed chain or contact. World move/rotate/scale commits to existing native animation channels; model-only handles save an authoring pose. Pins remain visible/selectable with bones hidden. Fixed support/socket anchors retain their separate restrictions. Physical pointer acceptance remains separate from detached hit-test/gesture evidence.

Result saving uses registered project/CHR0 writers, independent destinations and fresh reopen validation. Prepared drafts can be saved without enabling Apply. A `.retarget.json` sidecar records format/hash/reopen/draft/loss/validation and `GameTested=false`. Native write restrictions remain precise; project snapshots preserve data unsupported by a native writer.

Destructive skeletal edits set `RewriteNativeChannels`, omitted when false for historical serialization. The worker clears old key collections while retaining native entry semantic flags, then writes/reopens current channels/timing. The adapter also compares every decoded channel's key count, times, float values and tangents, preventing an old worker from silently retaining deleted keys. Partial native S/R/T groups reject rather than inventing fixed keys. Integral-index, duplicate-time and split-tangent writer limits remain explicit. Synchronized morph-container saving rejects authoritative skeletal rewrites until qualified; Save Project retains the edit and originals. Color/visibility project editing does not imply new native writers for those families.

## Acceptance boundary

TESTING and ignored `artifacts/viewer-parity` receipts record actual commands, counts, hashes and image inspection. Core fixtures, fresh native checks, offline control/layout renders, live input and matched reference-window comparison are separate scopes. Failed intermediate attempts are retained and are not passing receipts.

Model verification uses `scripts/Test-BackgroundCapture.ps1`, the shared offscreen viewport, serialized paced BelowNormal work and pause/cancel/resume markers. Offline control verification never shows/activates a desktop window or injects input; it can embed shared offscreen pixels in actual Avalonia controls and retains raw viewport images separately. It is not a live native-window screenshot.

Remaining gates: actual matched reference capture with recorded Windows/theme/font/DPI/client/panel/camera/clip/bone state; live numeric/gizmo/cancel/focus/shortcut/resize interaction; full menu/Files/drawcall/family-editor parity; and existing M2 retarget acceptance. Contacts remain OFF, all retarget drafts Review/Apply-blocked, SCN0 inactive; GPU/DPI/further-SHP0 deferrals remain. No100% UI, game-build or game-test claim is made.
