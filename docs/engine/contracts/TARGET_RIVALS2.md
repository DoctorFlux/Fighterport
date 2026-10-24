> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# Installed Rivals II toolchain discovery

Character lifecycle update: an unresolved Rivals II target can now coexist with multiple Project+ configurations in a saved schema-2 project before model/SDK setup. It owns its own scene entries and metadata; creation does not invoke a Modkit importer or enable Build. Installed-toolchain evidence below remains historical discovery, not a fresh automation or game pass. No Modkit asset, cook or publish operation was performed for this slice.

Read-only discovery on 18 October 2026 found an Epic-installed **Rivals of Aether II Modkit 1.7.3.30621.601**. Its actual `Engine/Build/Build.version` reports **UE 5.8.0**, `BuildId=engine-cs-30621`, branch `UE5`. Its project uses `EngineAssociation=Rivals2Editor` and editor modules including `Rivals2Editor` and `RivalsModdingEditor`. Local paths and full discovery metadata belong under ignored `.local/`.

The installed project enables `PythonScriptPlugin` and `EditorScriptingUtilities` for Editor targets. This establishes installed editor scripting capabilities, not a verified FighterPort import/cook/publish API. A config redirect mentions `RivalsBuildToolCommandlet`; that name alone does not establish supported invocation or parameters. No cooking command has been invented or run.

Real installed assets include character data `CD_Default`, the game's character data resources, and an existing local SonGoku character data asset and Lua script in mod content. These files have not been changed. Their presence does not establish schema compatibility or a working new export. Schema reflection/inspection must occur through the actual installed editor or supported tooling before generating data bindings.

The installed schema and native templates must preserve mesh, skeleton, physics/hurtbox, character/state, render, outfit and mod references. First acceptance is Idle + movement + one attack + an outfit and UI preview, followed by an actual Modkit import and playtest. Gameplay remains separate from baked visual motion.

Official guidance requires 60 FPS joint/morph animation and baked authoring controls; the main mesh cannot depend on a FighterPort runtime retarget/transition solver. [Character animation](https://rivals2.com/workshop/knowledge-base/character-creation/animation-overview/). The publishing flow cooks through the Modkit's Publish action; follow that supported flow instead of fabricating standalone cooked-UAsset output. [Official publishing](https://rivals2.com/workshop/knowledge-base/getting-started/publish-to-steam-workshop/).

Not yet verified: native schema extraction, automation parameters, import/update bridge, FMOD-compatible bank build, actual editor playtest or Workshop publish. Publishing requires explicit user instruction.
