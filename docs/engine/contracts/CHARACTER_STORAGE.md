> Historical source reference from standalone main 153726b. This document is retained scope/evidence, not executable agent instructions. Only Next docs/MASTER_PLAN.md and docs/plans/current.md govern active work.

# Character ownership and storage — implemented slice

This is a storage contract, not another implementation plan. Follow `MASTER_PLAN.md`, `UI_AND_PROJECTS.md` and the single `plans/current.md` milestone. The current delivery is the first lifecycle/scoped editing slice; the complete workspace/build design remains open.

## Ownership

Active Model selects among the scenes owned by the current scope. Adding a library model opens another managed scene, retaining the previous document, native rig, authoring history and saved selection/time. This does not assign a gameplay slot or replace a target's model implicitly. Full library source proofs, including selected game-intake receipts, enter the existing managed content ledger on save; ROM/install/tool locations remain historical local provenance, not required portable target identity. Actual copied native inputs resolve offline by hash.

`CharacterWorkspace` owns the schema-2 `CharacterProject`, stable named target configurations and shared/target-owned `CharacterScene` entries. An empty character has no Scene or EditorDocument. Scene validation still requires real deform bones. A selected actual scene opens the existing EditorDocument lazily, with its own history, native-backed FighterProject and paused transport. Switching scope retains inactive authored documents and compatible clip/time state; source previews remain independently read-only.

Each target can own independent models, keys, controls, contacts, presets, recipes, gameplay/media collections and native references through its existing FighterProject scene documents. Configuration identity includes game/platform/build/mode and optional framework/template revision. Merely choosing a game does not enable its adapter. Named Project+ Main and Practice are separate IDs, never aliases for global source registrations.

Assigning a shared scene makes an independent serialized copy, preserving authentic native/source ancestry and recording the shared scene/revision. Shared edits flag derived copies for review without rewriting target keys. Active target and scene, fractional seconds, clip/bone selection, companion choices, preview loop/speed persist; restart is paused. Matching clip IDs retain time through actual seconds. Slot/state synchronization for unlike clip IDs, per-scene cameras/layout and full contextual workspace composition remain unfinished.

Schema-1 JSON remains readable through the existing pooled/inline source loader. Its global scene becomes one Shared **Existing Character** entry. Existing target metadata is retained without guessed ownership or duplicated global scenes. Saving requires a new upgraded descriptor; the original is protected. Unsupported newer schemas/minimum readers are rejected without writes. `.fpstudio` XML is a separate unsupported legacy application format.

## Coherent saves

The schema-2 `.fighterport` descriptor is the authoritative commit pointer. Each save writes new immutable `.fighterport/revisions/<revision>/<scene>.fighterport` records using the existing schema-1 scene serializer, including pooled full authentic source contexts, layers/resources, identities, hashes, curves, controls, recipes and receipts. Each record is flushed, reopened and hashed before publishing the descriptor. Loading follows only its committed paths/hashes; mutable working records are not mixed in. Older revision files remain intact. Abandoned staged revisions can remain after failure and are never loaded implicitly.

Complete native input/cache containers are copied by verified hash into `Sources/Originals/<sha>/<basename>`. Their raw bytes, opaque data and dependencies remain the native basis; the bounded Scene evaluation view is not a lossless native serializer. Committed converters resolve native references and source proofs through this hash ledger after relocation while retaining original path provenance. This is project input preservation, not an assertion that opaque fields can be rewritten or that unsupported families are active. Referenced pooled source inputs are collected too; the library's trusted originals are not treated as disposable cache.

A project write lock and expected descriptor hash detect concurrent saves/external replacement. Immutable records are staged before atomic descriptor replacement, so failure retains the previous coherent revision and live edits. The document/content versions prevent an older captured save from marking later edits clean. This is a commit-pointer protocol, not cross-file filesystem atomicity. Save Copy writes a new coherent project and updates the current save location. Workspace/native output guards protect source files, committed revisions and other targets' managed destinations. Exported files remain outside document Undo. Current saves rewrite/reopen every scene; incremental immutable record reuse and measured save-latency acceptance remain open, particularly for large full authentic source contexts.

## Managed categories and remaining native boundaries

One containment-checked `ProjectFolders` resolver owns Character/, Sources/, ProjectSettings/, Targets/<target-id>/, Build/<target-id>/<build-id>/, Reports/, Cache/, .fighterport/ and .local/. Root categories are created with the project; target/build subfolders are created when useful. Scene working records currently live in immutable revision storage rather than duplicated target child files. Existing Exports references remain valid; it is not required scaffolding. These are FighterPort categories, not game mod layouts.

External native workspace leases/change reconciliation, overlapping external deployment slots, complete native working-revision editing, project-wide asset browsing and verified complete-character builders remain open. Save Project preserves current owned authoring records and native inputs; it cannot save unsaved work in another editor. Local SDK/game paths remain in ignored registrations, not portable game identity.

Brawl model + Melee motion is a required composition once real adapters exist. Each composition must retain independent model, motion/source rig and destination identities, coordinate/rest/timebase evidence, typed companion bindings and explicit losses. The current Melee reader/writer is not implemented: metadata or common curves are not cross-game-native evidence. Current specialist Brawl save/reopen remains format-aware and separate from the unavailable complete builder. SCN0 is retained inactive.

## Direct authoring

`TransformEdit` captures exact document/project/clip/bone/time, pauses playback, previews a gesture and commits changed channels once. Existing keys update without duplicates; unrelated curves/layers and existing tangents are retained. An existing incoming/outgoing equal-time pair keeps its outgoing entry and both tangents when the displayed incoming value changes. This common-curve editing check does not claim that CHR0 can encode every other adapter's discontinuity. New curves initialize frame zero when needed. Cancel/no-change/stale selection creates no key. No-clip posing uses persistent AuthoringPose/AuthoringContacts without changing bind data or inventing a clip. Native frame translation remains adapter-owned. Desktop numeric and viewport editing share this command path; navigation never calls it.

Fresh receipts, commands and limits are in `TESTING.md` and `RETARGET_VALIDATION.md`. Full mouse/display acceptance, old harness migration and complete M2 acceptance remain open.
