# Source candidate audit

Review date: 18 October 2026. Publication was explicitly authorized by the user:
push V2 to its new repository and make it public. This audit records the source
review supporting that request.

## Scope and provenance

The source foundation is BrawlCrate/BrawlLib commit
`d56cbac89dc2cd0e5c3f923e1d03041b16902add`. The independent V2 root commit is
`7d2810eb547b9b6860cd0b0515d8076fd8ec922c`, with no parents. The review compared
that import with tracked modifications and unignored candidate additions.
Ignored runtime assets, local evidence, dependencies and build output are outside
the source candidate set.

The remote was independently checked as
`DoctorFlux/fighterport-studio-next`, private, `fork=false`, size zero, with no
remote refs before first publication. The destination owner ID is 100806234.
V2 publication uses only this new repository and independent history.
For this follow-up, the repository was rechecked as public, `fork=false`,
`disabled=false`, with the same owner ID and authenticated administrator/push
rights. Its exact origin remains the independent V2 URL. The previous project's
push authorization was not used.

## Candidate contents and privacy

- No candidate executable, DLL, signing-key, credential-helper or commercial
  model/motion archive was found. PE-header checks covered the candidate files.
- The trajectory/contact follow-up audit covered 6,309 source candidates and all
  6,323 blobs reachable from the three preceding commits. No excluded executable/game-asset extension,
  PE header, current-user private path or credential pattern was found. The
  largest reachable blob was 1,418,354 bytes. The new implementation commit
  is checked again before push; local detailed receipts stay ignored.
- The sole retained native model helper is the upstream 460-byte `Static.mdl0`,
  previously inspected as one bone with no geometry, materials or textures.
  Upstream editor artwork and generated reference documentation remain present.
- Four inherited texture templates are excluded from history and embedding.
  User-provided game assets, generated project copies and test captures stay in
  ignored locations.
- Non-generated candidate source/document files and retained generated HTML/JS
  were checked for local home paths, current-host paths, recognizable GitHub
  tokens, private-key headers and Discord webhook credentials. No such remaining
  match was found. This is a pattern audit, not a guarantee about arbitrary data.
- Twenty-three inert upstream developer-cache paths were removed from project
  metadata. No current-user path was added. Authored source passes CRLF-aware
  whitespace checks; copied third-party notices retain their original whitespace.

## Notices and dependencies

The original LGPL-3.0 text, full GPL-3.0 text, source headers, credits and upstream
README are retained. Color Smash's separate AGPL-3.0 source license is retained;
that helper is not built or included in the application output.

The runtime license inventory in `THIRD_PARTY_NOTICES.md` covers the restored
packages and the pinned OpenTK dependency, including its Mono, Tao Framework and
OpenEXR notices. Application builds copy the retained license directory. OpenTK
is restored explicitly and checked by SHA-256; its binary stays ignored.

## Default execution boundaries

The documented build targets the actual x86/net472 application. Legacy
updater/Cargo/copy/delete hooks require explicit opt-in and are disabled by
default. Automatic updater, Discord, script-loader and association-write paths
are disabled. No updater, Discord DLL or credential helper was present in the
reviewed Release application output.

Preference-file write failures retain runtime values in memory. Persistent
preferences are not claimed verified on the current host; fighter project and
native-output persistence remain separate and report their own failures. See
`DEVELOPMENT_STATUS.md` for actual executable acceptance results and limitations.
