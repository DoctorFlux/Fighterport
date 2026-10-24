# FIGHTERPORT STUDIO V2 — CODEX CLEAN-RESTART BOOTSTRAP

**Date:** 2026-10-17  
**Authority:** `FighterPort_Studio_V2_MASTER_PLAN.md` (the new authoritative feature specification).  
**Upstream foundation:** https://github.com/soopercool101/BrawlCrate  
**Legacy reference, DO NOT modify:** https://github.com/DoctorFlux/fighterport-studio  
**Proposed NEW independent GitHub repo:** `DoctorFlux/fighterport-studio-next` (not yet created; user may choose another name).  
**Proposed NEW local workspace:** `<new-workspace>` **only if that path is free and writable**; choose a separate empty directory after inspection otherwise.  
**Proposed backup location:** a distinct non-repository folder outside BOTH workspaces; inspect free space first.  
**Prior V1 remote HEAD observed when drafting:** `49ecd9a7192ab79f00bf81883d5259d352ab256d`, not an instruction to pin or reset the old project.

## 0. Hard reset of DEVELOPMENT APPROACH, not of user files

We are intentionally abandoning the custom architecture and UX of the old FighterPort implementation. Its current `docs/plans/current.md` command to fix Mario/Snake standing knee transitions does NOT carry into V2. Neither does its `AGENTS.md` or the old origin's automatic push authorization.

Start directly from a pristine, pinned **full BrawlCrate/BrawlLib application checkout**. Do not carry forward the former FighterPort `application/BrawlCrate/Authoring` layer, `PreviewAuthoringSession`, `PreviewWorkspacePane`, `CharacterProject` architecture, custom panel stack, source-browser implementations, application scripts or legacy data schemas. Do not build a second WPF shell or an external-editor launcher. The inherited BrawlCrate GUI, resource tree, renderer, timeline, curve editors and existing native format handling are the actual core.

The old project may be read later for numerical test ideas only. A specific small algorithm could be reimplemented or ported AFTER an independent quality review, with a documented reason, provenance and test. There is NO blanket migration.

## 1. Security and recovery first — never delete the old project

Before any source alteration:

1. Record the actual local old workspace, `git status --porcelain=v1`, current branch, remotes, remote HEAD and disk space. Do not assume the old workspace path, state or untracked assets from chat history is still accurate.
2. Create a **new local-only backup** of the complete old working directory, including `.git`, untracked files, ignored assets, local settings and test evidence. The backup must be outside both the old and new repositories, with appropriate access protections.
3. Independently create a **Git mirror backup** of the old public remote with `git clone --mirror https://github.com/DoctorFlux/fighterport-studio.git <safe-backup-path>`. This captures remote refs/history but NOT old untracked local files; both backups matter.
4. Verify backup file sizes/counts, representative source/config hashes, Git mirror `git fsck --full`, and recoverability of uncommitted/untracked work. Preserve a backup receipt containing path, timestamp, exact old HEAD and verification results.
5. Do not run destructive clean, reset, mirror copy into an occupied destination, or automatic deletion. `robocopy /MIR` is not an acceptable backup command for an existing destination. Keep both old repositories unchanged.
6. Keep all user-provided game assets, extracted resources, originals, credentials and private paths out of the new Git repository. The backup is LOCAL ONLY and must never become a repository commit.

If backup cannot be completed or verified, stop BEFORE importing/resetting anything. Explain exact blocker; do not guess success.

## 2. Independent BrawlCrate source import

After verified recovery:

1. Create a NEW empty local development directory and a disposable upstream checkout. Do not clone upstream into the old FighterPort directory.
2. `git clone --recursive https://github.com/soopercool101/BrawlCrate.git <upstream-scratch>`; capture `git rev-parse HEAD` and check `.gitmodules`, all included third-party dependencies, license files, generated artifacts, credentials and build hooks.
3. PIN that upstream commit in `UPSTREAM.md`. Do not automatically track latest changes while building the baseline.
4. Build the exact upstream application project `BrawlCrate/BrawlCrate.csproj` (WinForms, .NET Framework 4.7.2, x86) with Visual Studio 2022 MSBuild and the 4.7.2 targeting pack when available. Start with its normal build instructions, inspect custom prebuild steps. Disable network/install/update side effects during the safety audit. If a missing local tool or updater hook prevents an otherwise valid build, apply only a clearly documented minimal local build fix, retaining untouched source evidence.
5. Run unmodified application (or the minimally build-repaired baseline) and exercise actual native model, CHR0 motion, playback/scrubbing, saved new-file output and fresh reopen using **original self-created test fixtures**. Test basic native BrawlCrate file editing and confirm unchanged inputs.
6. Create the new independent Git repository from this exact clean upstream **working tree** (tracked source snapshot, preserving license/notice files). Do not push/copy upstream Git object graph as a `git clone --mirror`; do NOT use GitHub's Fork operation, and do NOT graft old FighterPort commits. A new initial commit is the imported upstream snapshot, with exact pin and source provenance recorded. Preserve required attribution.
7. Create a separate V2 code/documentation area. Do not apply the old FighterPort patches. Keep all native BrawlCrate functionality available throughout the project.
8. Before shipping any executables audit inherited update-checking, Discord/reporting, association/registry, automatic plugins, telemetry, updater endpoints, signing resources, untrusted parsers and local settings: disable inappropriate inherited automatic network actions for the independent product and check the replacement behavior with tests.

### Example local shell sequence, only after path review

The following is an illustration for Codex to adapt, **not an instruction to execute without preflight**:

```powershell
# 1. Define NEW empty destinations after verifying that they do not exist.
$upstream = 'D:\Dev\BrawlCrate-Upstream-Scratch'
$v2 = 'D:\Dev\FighterPortStudioV2'
if (Test-Path $upstream) { throw 'Scratch path already exists.' }
if (Test-Path $v2) { throw 'V2 path already exists.' }

# 2. Use a disposable upstream clone to pin and build unmodified BrawlCrate.
git clone --recursive https://github.com/soopercool101/BrawlCrate.git $upstream
$pin = (git -C $upstream rev-parse HEAD).Trim()
Write-Output "BrawlCrate upstream commit: $pin"

# 3. AFTER baseline build and notice audit: export upstream TRACKED files
# into a separate new directory. Use Git Bash / git archive extraction,
# or an equivalent copy based on `git ls-files`; preserve all notices.
New-Item -ItemType Directory -Force -Path $v2 | Out-Null
# Example when `tar` is available; do not run arbitrary shell code from input:
# git -C $upstream archive --format=tar HEAD | tar -xf - -C $v2
# Note: git archive does not include git-submodule contents. Audit and pin
# required submodules independently before treating the import as complete.

# 4. Initialize a NEW unrelated Git history AFTER the source snapshot exists.
# git -C $v2 init -b main
# git -C $v2 add .
# git -C $v2 diff --cached --check
# git -C $v2 commit -m 'Import pinned upstream BrawlCrate source'
```

If the installed PowerShell/git/tar environment lacks a safe extraction route, use a verified file copy. Never copy upstream `.git` into V2. `git archive` does NOT export submodule content; declare and resolve that explicitly. Do not commit upstream credentials, proprietary assets, build products, or historical private data.

## 3. New GitHub and new Codex environment

The new GitHub repository should be created **without GitHub's Fork button**. Prefer making it PRIVATE initially if the user wishes to keep the development work non-public. Private visibility does not erase source license obligations if binaries/source are later distributed; public code similarity and required attribution must not be hidden or misrepresented.

**Repository UI steps (user or authorized local Codex GitHub CLI):** GitHub → New repository → owner `DoctorFlux` → name (proposed `fighterport-studio-next`) → Private → do not pre-initialize README/.gitignore/license if pushing a pre-existing initial import → Create repository. Verify the actual resulting URL before `git remote add origin`. The currently available GitHub connector cannot create repositories; do not claim the remote exists until it does.

After that, use `git remote -v`, `git status`, `git log -1`, and `git diff --cached --stat` to verify the new source-only commit and the exact NEW remote before a normal initial push. The old repo's 2026 standing permission to push updates to `DoctorFlux/fighterport-studio` is **not** authorization to push to the new repo. Check authorization for the new destination and its intended visibility before publishing. Do NOT modify the old repo or force-push either.

Create a NEW Codex project/environment, connected **only to the new repository**. Set workspace to the new root. Add the new V2 `AGENTS.md` below; attach `FighterPort_Studio_V2_MASTER_PLAN.md` and `FighterPort_Studio_V2_FIRST_TASK.md` as the governing files. Do not import old Codex prompts or cached assumptions. Check that the new Codex task displays the new repo's path and remote before actions.

## 4. First-party V2 docs to create and maintain

Minimum new files:

```text
AGENTS.md
README.md
LICENSE                             # intact inherited license and notices
UPSTREAM.md                        # pinned source + full BrawlCrate/BrawlBox/BrawlLib credits
THIRD_PARTY_NOTICES.md             # audited included tools/components
.github/                           # only reviewed appropriate settings/workflows
docs/MASTER_PLAN.md                # this V2 master plan
docs/plans/current.md              # ONLY new V2 executable acceptance criterion
docs/DEVELOPMENT_STATUS.md         # actual V2 tests, not old receipts
docs/FORMAT_SUPPORT.md             # per game, format, version, operation
docs/TESTING.md                     # documented baseline and new vertical tests
```

New `AGENTS.md` should say:

> This is a NEW independent project starting from a pinned pristine BrawlCrate source import. Read the new `docs/MASTER_PLAN.md` and new `docs/plans/current.md`. The old DoctorFlux/fighterport-studio is historical reference, NOT this project's codebase, task queue, Git remote or auto-push authority. Build the real BrawlCrate application, preserve licensing and native editors. Prioritize a ONE-window simple character editor: New Fighter → Open Model → Add Animation → Play → Save, then one-action verified retargeting. Do not copy the retired `Authoring`/`Sources`/previewer panel and old manifest architectures. Use tests and protected original fixtures, track actual capability per operation. Never include commercial assets, private paths, binaries or signing credentials in Git. Avoid untrusted automatic network code and global installs. User must verify the NEW remote and visibility before publishing.

## 5. Explicit design constraint: the UI is NOT allowed to grow back into V1

Before implementing each feature, check that it solves a real action without demanding multiple windows and setup forms. The default BrawlCrate Model Previewer must show the selected character, animations list, timeline and one compact inspector. A normal user should not see source hashes, revision IDs, opaque native paths, algorithms, rig-grid rows or helper flags until they ask for Details/Advanced.

Normal controls: `New Fighter`, `Open Model`, `Add Animations`, `Play`, `Retarget to This Model`, `Assign to Slot`, `Save`, `Export`. `Retarget` does not need to work in the first milestone; don't add a fake button prematurely. Read-only animation audition must allow playing. The inherited advanced native resource tree remains accessible, not removed.

Reject technical theater: empty architecture-only classes, lots of inert placeholder tabs, isolated tool windows with divergent project selections, and automatic retarget claims based solely on names.

## 6. Milestones AFTER the upstream build (the master plan is long term)

**V2-M0 (first actual deliverable):** Clean upstream BrawlCrate build, safe separate new repo identity, native editor works, initial brand/settings safety, actual New Fighter/model-only project, Add Animation / list / play / scrub / Save, native original/source hashes unchanged. Verify this through the real GUI and a restarted product.

**V2-M1:** Source-bound global animation library, selected rig identity, one-action cross-rig review and minimal tested FK retarget, independent target clip and genuine Brawl CHR0 native export/reopen. Both directions separately, original humanoid fixtures first; Snake/Mario local assets later, never in Git.

**V2-M2:** Automatic exact-profile mapping, meaningful humanoid controls, separate IK/FK editor, proportions, feet/poles/hands, profile reuse, safe correction layers and native output validation. Use the old knee-edge failure as a regression idea if relevant, not inherited code.

**V2-M3:** Game-verified slot templates and multi-part motion groups, target Idle/Fall/reference fitting within existing clip duration, animation speed/style controls including FighterZ stepped holds, batch Fill Missing Miscellaneous with protected custom motions.

**V2-M4:** Actual Melee/Ultimate/Rivals II adapters and a first cross-game native animation route. Then additional Dragon Ball skeletal/motion source readers. Each format and output path has separate decode/reopen acceptance. Independent target branches from one fighter project.

**V2-M5+:** Wider model/material/morph/costume support, additional extraction providers (GameCube/Wii ISO/RVZ; Ultimate data.arc/dumps; mod folders; Xenoverse containers; Unreal cooked archives), game-specific native mod asset packaging, optional visual scripting and gameplay systems and faster in-game testing. Extraction utilities support the central studio; they do not replace it.

The ordering may be adjusted after real quality results, but **do not begin with a huge game-adapter framework before the user can operate the one-model/one-animation workflow**.

## 7. Completion receipt, what Codex must report

Report actual source commit and upstream pin; old backup location and verification; new local workspace; whether NEW GitHub repo exists and what visibility it really has; new Codex project root/remote; build prerequisites; exact native executable path; which original fixture was opened in GUI; playback and save/reopen results; unchanged hashes; current V2 task; and what has NOT been done. Avoid vague 'everything is ready' claims.

**Stop safely on a real blocker, keep a coherent local checkout and actual test evidence; never fabricate a pushed repo, successful UI interaction or working cross-game output.**
