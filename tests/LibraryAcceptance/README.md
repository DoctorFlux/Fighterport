# Source-bound library acceptance

Build this net472/x86 console harness with VS2022 MSBuild, Release/x86, using
the same OutDir as the actual application. It loads the real BrawlCrate-derived
product and BrawlLib. No commercial fixtures are included or generated.

Run with `--mario-root <local-folder> --snake-root <local-folder>
--output <new-ignored-folder>`. Supply actual costume and motion PACs using
the same fixture layout as NativeAcceptance. All input files are hashed before
and after. Test copies used for missing/changed/repair cases stay in output.

Checks cover whole-pack indexing, explicit association approval, deduplication,
immutable resource indices with repeated names, cancellation, Undo, exact-byte
repair, missing model/motion, changed bytes, unsupported version, timing
mismatch, malformed/DTD metadata, fresh-process persistence, independent
target edits, removal without deleting sources, all planned game-path settings,
and 5,000-row bounded metadata search without native decoding. Real Wait1
bakes cover Mario → Snake, Snake → Mario and original-model reuse, including
quarter-frame native reopen, unchanged bind proportions, exact TransN Z and
fresh native target PAC output. Existing retarget suites retain their broader
ground/air/contact and unrelated-slot checks.

Run the actual desktop script separately, with Windows PowerShell 5.1:
`tests/GuiAcceptance/Run-LibraryAcceptance.ps1 -ActualExePath <exe>
-MarioRoot <local-folder> -SnakeRoot <local-folder> -Output <new-folder>
-GameVersion <known-build-or-explicit-unknown>`. Optional `-LibraryRoot` isolates
the main catalogue; otherwise registration persists in the user's real shared
library. Optional `-GamePath` tests saving that game's archive/folder pointer.
It exercises the buttons, game/character/model collapse controls, actual original and
target rendering, playback, native export/reimport, two project restarts and
source removal/repair/Undo on disposable copies. Never overlap GUI runners.

For real disc sources, supply `--disc-image <local-ISO-or-WBFS>` and
`--dolphin-tool <installed-modern-DolphinTool.exe>` instead of the loose-source
arguments. `--characters mario,snake,captain` selects three acceptance fixtures;
the production browser discovers characters from the native filesystem.
Optional `--snake-root` also bakes Captain's original motion onto Snake.
`--compare-index <previous-SharedLibrary-folder>` compares extracted file hashes
between different containers. Original image SHA is checked before and after;
the test also exercises offline-image repair and cancellation.
