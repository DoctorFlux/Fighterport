# Native workspace DPI audit — 24 October 2026

Preview.19 audits the retained .NET Framework 4.7.2/x86 application without
changing its process awareness, renderer, native editor fonts or scaling policy.
This is a source audit, not physical-monitor acceptance.

## Findings

- `Properties/AssemblyInfo.cs` retains the WPF `DisableDpiAwareness` assembly
  attribute. `Program.cs` enables WinForms visual styles. The app project supplies
  no explicit custom DPI manifest or startup DPI setter. The attribute alone is
  not proof of the effective Windows DPI context for every launch/host.
- The outer workspace explicitly measures its five 100–200% text presets.
  `WorkspaceAppearance.SizeDialog` disables automatic scaling and scales the
  dialog once using the selected font ratio, then clamps to the working area.
  These measurements must not be multiplied again by a future automatic scaler.
- The whole native editor retains its existing designer dimensions and original
  fonts. Its minimum CHR0/playback width is used to choose docked panels versus
  overlays. Changing process awareness affects that editor as well as the shell.
- Native GL viewport/scissor regions use control dimensions. ModelPanel mouse
  handling converts client X/Y using Width/Height; pin projection also combines
  viewport regions with ModelPanel.Height. Pin marker size is derived from a
  seven-pixel camera projection offset. These paths currently assume compatible
  client/input/render coordinates; mixed-DPI correctness is not established.
- Offscreen capture composes form/drawer bitmaps with actual native GL images
  using client/screen conversions and window borders. Capture scale, picking,
  control hit targets and displayed rendering require separate verification.

## Required before a DPI migration

1. Record the actual process/thread/window awareness and control, monitor and
   framebuffer dimensions for the standalone app, standard dialogs and native
   viewport. Do this without changing the user's monitor settings automatically.
2. Choose one startup awareness policy before windows/GL contexts are created.
   Review WinForms/WPF interaction and dialog scaling together. Keep application
   text preference distinct from monitor/device scaling and avoid double scaling.
3. Qualify 100/125/150/175/200% OS settings and moves between differently scaled
   monitors on available hardware. Record unavailable cases rather than simulate
   them as physical acceptance. Check restore/maximize, working-area clamping,
   repeated moves, native lists, curves, playback and standard Browse/Save dialogs.
4. Independently verify viewport framebuffer dimensions, camera interaction,
   bone/pin/gizmo picking, drag deltas and overlays after each move. Preserve exact
   native document/selection/frame/Undo and source-only protected-control behavior.
5. Inspect actual images and complete a user-driven input walkthrough. Background
   layout captures and synthetic handler calls cannot establish physical DPI or
   real keyboard/mouse acceptance.

No global DPI switch is enabled by preview.19. A future migration needs its own
bounded current-plan increment and fresh native regression evidence.
