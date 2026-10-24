> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# Read-only game sources

Supporting contract for `MASTER_PLAN.md` and the ONE M2 `plans/current.md`; this is not another execution plan.

Game sources & builds keeps independent named game/platform/build profiles. Save as new build creates a new ID; Update selected retains its ID while correcting a local path or disc-tool location. SourceRegistry writes local configuration only after its replacement succeeds. A profile is not a target authoring workspace or evidence of a builder/playtest.

## Implemented intake

- Brawl, Project M and Project+ extracted sources: select the exact `fighter` directory, extracted `files/fighter`/`DATA/files/fighter`, `pf/fighter`, or the supported SD build roots. Multiple matching roots are an explicit ambiguity, never merged into an assumed build.
- WBFS/ISO: configure installed WIT in that local profile, or set `FIGHTERPORT_WIT`. The provider runs actual `VERSION`, `FILES --psel DATA --show =SIZE,PATH,DEC`, and selected `EXTRACT --psel DATA --pmode NONE --files ... --dest ...` commands. It does not implement its own Wii decryption or ship a tool executable.
- Browse up to 10,000 canonical native model/motion package entries; prepare 1–32 selected files with a 512 MiB size preflight. Reject traversal, wildcards, ambiguous duplicate identities and reparse-point sources. Recheck image size/modification stamp and tool hash, or extracted file sizes, before preparation. Hold source files for read sharing only. These stamps are change guards, not full-image integrity hashes.
- Run tools off the UI/playback path, one process at a time, with captured output limits, cancellation and a three-minute timeout. Output goes to a new ignored cache stage. Failure/cancellation never publishes a successful intake receipt or changes an authored target; a partial cache stage may remain for diagnosis.
- Verify every requested output size and SHA-256. The durable receipt records source profile/provenance, actual tool version/hash, native entry names, selected byte lengths/hashes and diagnostics. Only a complete selected set gets a receipt. It does not claim cross-file filesystem atomicity or whole-disc validity.

Add Source → Browse registered game / ROM chooses the character folder, desired model/outfit package and optional separate motion. Native decoding/association review happens afterward through the existing Brawl provider. The reviewed registration preserves selected bytes and intake receipt. Original game images and extracted inputs stay private/ignored. A model-only registration contains no invented animation.

CLI (new output directories; user-provided local inputs):

```powershell
FighterPort.Cli.exe browse-brawl-source <disc-or-folder> [installed-wit]
FighterPort.Cli.exe prepare-brawl-source <disc-or-folder> <cache-root> <comma-separated-native-entries> [installed-wit]
./scripts/Test-GameSources.ps1 -Disc <supplied-wbfs> -Wit <installed-wit> -OutputDirectory <new-directory>
```

Fresh supplied-disc results/limitations are recorded in TESTING. The tested external tool is WIT 2.31a r6005 cygwin (2015-02-08), GPL-2.0, with separately installed Cygwin support DLLs. Its actual hashes/license evidence are in `licenses/game-source-reference-audit.json`. It is not copied, linked or redistributed. Command documentation: [WIT FILES](https://wit.wiimm.de/wit/cmd-files.html), [WIT EXTRACT](https://wit.wiimm.de/wit/cmd-extract.html).

## Limits

This is selected native-asset intake, not whole-build indexing, ROM modification, repacking, installation, game testing or complete character building. Current standalone extension filtering focuses on PAC/PCS/BRRES/MDL0/CHR0 packages; unrelated resource providers remain future scope. Native material/morph/visibility resources inside selected packages retain their existing adapter behavior; SCN0 stays preserved/inactive. Supplied-image extra-partition warnings are retained even when selected DATA extraction succeeds. Full-image integrity, split-image edge cases, ISO-specific fixtures, RVZ and other games' disc/install readers remain unverified or unsupported. Selecting another game does not route its data through Brawl.
