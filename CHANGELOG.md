# Changelog

All notable changes to abs-cli are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/).

## 0.4.0 — 2026-05-11

### Highlights
- **Full author-management surface.** Agents can now paginate authors,
  match them against Audnexus, look them up read-only, edit them with
  merge-on-rename visibility, delete them, and set / get / remove author
  images. `abs-cli authors lookup --name "..."` is the non-destructive
  Audnexus probe (`GET /api/search/authors?q=`); `abs-cli authors match
  --id <id>` is the destructive write that fills in `asin`, `imagePath`,
  and `description` from Audnexus. `abs-cli authors update` edits `name`,
  `description`, and `asin` — pass a flag to set, pass `--description ""`
  or `--asin ""` to clear, omit to leave alone (`--name ""` is rejected).
  It surfaces ABS's silent-merge-on-rename behaviour explicitly: a
  same-name conflict reassigns all books to the existing author and
  deletes the source, and the CLI returns `{ merged: true, author: <target> }`
  instead of the usual `{ updated, author }` so callers can detect it.
  `abs-cli authors delete` unlinks and removes. The new `abs-cli authors
  image set|get|remove` mirrors the `items cover` set/get/remove shape;
  `set` accepts a single `--url` (ABS downloads from there), `get` writes
  to file or stdout with an optional `--raw` to bypass server-side resize,
  `remove` deletes.
- **`abs-cli authors list` is now paginated (breaking response shape).**
  Moves from the unpaginated `{ authors: [...] }` to the paged shape
  `{ results, total, limit, page }`. New flags: `--limit`, `--page`,
  `--sort` (`name` / `lastFirst` / `addedAt` / `updatedAt` / `numBooks`),
  `--desc`. Any caller that read `.authors` from the response must
  switch to `.results`.
- **Deprecated `abs-cli items search` removed (breaking).** It was a
  duplicate of top-level `abs-cli search` — same endpoint
  (`/api/libraries/{id}/search`), same options, same response. The
  help-text-level deprecation was in place through v0.2.x and v0.3.0
  per `docs/roadmap.md`; v0.4.0 ships the hard removal. Migrate by
  dropping the `items` segment.
- **`--help` quality pass.** Misleading command descriptions corrected
  across the tree; Audnexus provider notes, ASIN support on
  `metadata search`, and the merge-on-rename caveat on `authors update`
  are now documented inline so agents don't have to read specs to use
  the CLI safely.
- **Smoke and docs brought current.** Smoke now covers the entire
  authors lifecycle (lookup / match / update with merge-on-rename /
  delete via throwaway co-author / image set-get-remove) and pinned
  the cover-URL flake by switching from a single provider to ABS's
  `best` meta-provider. README, `docs/cli-design.md`,
  `docs/architecture.md`, `docs/testing.md`, and `docs/roadmap.md`
  updated to reflect the .NET 10 base, the 0.3.0+ command surface
  (`changelog`, `items cover`, the new `authors` subtree), and the
  current test counts (132 unit, 45 self-test, 155 smoke).

### Features
- feat: add 'authors delete' command
- feat: add 'authors image set/get/remove' subcommand group
- feat: add 'authors lookup' command
- feat: add 'authors match' command
- feat: add 'authors update' command with tri-state body
- feat: add author image request/response models and endpoint
- feat: add author match and search endpoints
- feat: add author match/update request and response models
- feat: extend AuthorsService with match/lookup/update/delete
- feat: extend AuthorsService with set/get/remove image
- feat!: paginate authors list

### Refactors
- refactor: drop 'items search' subcommand from items command tree
- refactor: drop unused ItemsService.SearchAsync
- refactor: tighten authors tests and align command signatures

### Fixes
- fix: correct authors list/image flag claims in docs
- fix: correct misleading --help descriptions across commands
- fix: use 'best' provider for smoke cover-URL lookup
- fix: use assert_json_expr for null-body smoke check

### Tests
- test: drop 'items search' smoke assertions
- test: self-test round-trip for author image models
- test: self-test round-trip for author match/update models
- test: smoke coverage for authors delete via throwaway co-author
- test: smoke coverage for authors image set/get/remove
- test: smoke coverage for authors lookup/match/update
- test: smoke coverage for authors update merge-on-rename
- test: smoke coverage for paginated authors list
- test: tighten author update-body tri-state assertions
- test: tighten authors image top-level subcommand assertion

### Chores
- chore: bump version to 0.4.0
- chore: persist Claude Code statusline across devcontainer rebuilds
- chore: regenerate ResponseExamples.g.cs with AuthorImage types
- chore: regenerate ResponseExamples.g.cs with author match/update entries

### Docs
- docs: add 'be brief' main rule to CLAUDE.md
- docs: add v0.4.0 author management roadmap entries
- docs: extend 'items search' removal scope to cover README
- docs: extend authors --help with Audnexus provider note
- docs: flatten v0.4.0 roadmap section (drop shipped/pending split)
- docs: improve line break in authors image remove notes
- docs: note 'items search' removal in v0.4.0 roadmap section
- docs: note ASIN support in metadata search help
- docs: plan for v0.4.0 authors image
- docs: plan for v0.4.0 authors list pagination
- docs: plan for v0.4.0 authors modification
- docs: refresh command surface and project structure for 0.3.0+ work
- docs: refresh README .NET 10 references and install-script examples
- docs: refresh test counts (132 unit, 45 self-test, 155 smoke)
- docs: remove 'items search' from README, CLI reference, and roadmap
- docs: require smoke test before opening PRs
- docs: restructure roadmap — v0.3.0 shipped, v0.4.0 split into shipped/pending
- docs: spec and plan for removing 'items search' subcommand
- docs: spec for v0.4.0 authors image
- docs: spec for v0.4.0 authors list pagination
- docs: spec for v0.4.0 authors modification
- docs: tighten --help notes and drop jq examples

## 0.3.0 — 2026-04-29

### Highlights
- New `abs-cli items cover` subcommand tree — apply, fetch, and remove book covers.
  `set` accepts `--url` (ABS server downloads), `--file` (local upload), or
  `--server-path` (link to a file already on the ABS server's disk). `get` writes
  to a file with a JSON descriptor or streams binary to stdout (`--output -`).
  `remove` deletes the cover. Combined with `items list --filter "missing=cover"`
  and `metadata covers`, agents now have every primitive needed to build a
  cover-handling workflow.
- New `abs-cli changelog` command — print release notes straight from the
  bundled `CHANGELOG.md`. Default output is the latest entry; `--all` prints
  the full file. The file is embedded as an assembly resource, so the command
  works offline and ships in the AOT-published single-file binary.
- Target framework upgraded to **.NET 10 LTS**. Improved AOT trimmer drops the
  Linux-x64 binary from ~11 MB to ~8.7 MB. Dev container, CI matrix, and docs
  all updated to match.
- Added a build-time **NuGet Audit gate** (`Directory.Build.props` with
  `WarningsAsErrors` for NU1901-NU1904). Combined with GitHub Dependabot
  security updates (enabled at the repo level), CVEs in dependencies now
  surface as build failures and as auto-PRs the moment an advisory has a fix.
- `System.CommandLine` finally bumped from the 2022-vintage `2.0.0-beta4` pin
  to the **2.0.7 stable** release. Custom help-section infrastructure
  rewritten against the new action-based help model; user-facing help format
  is byte-for-byte identical to before.
- Test packages refreshed to xUnit v3 (3.2.2), `Microsoft.NET.Test.Sdk`
  18.4.0, `coverlet.collector` 10.0.0. No changes to test sources required.

### Features
- feat: add abs-cli changelog command
- feat: add ChangelogReader.ExtractLatest
- feat: add cover endpoint helper and typed multipart/stream HTTP methods
- feat: add cover request/response models
- feat: add CoversService
- feat: add items cover command tree (set, get, remove)

### Fixes
- fix: harden changelog reader, command, and tests after review

### Refactors
- refactor: tidy UseCustomHelpSections after review

### Tests
- test: assert CHANGELOG.md is embedded in self-test
- test: cover ExtractLatest stop/trim/error cases
- test: cover models round-trip and items cover command help
- test: cover smoke suite for all three set modes
- test: drop redundant CoversServiceTests
- test: end-to-end coverage for changelog command
- test: round-trip cover models in self-test
- test: smoke coverage for items cover lifecycle

### Chores
- chore: add NuGet Audit policy
- chore: bump devcontainer base image to dotnet:10.0
- chore: bump System.CommandLine to 2.0.7
- chore: bump TargetFramework from net8.0 to net10.0
- chore: bump test packages to xUnit v3
- chore: bump version to 0.3.0
- chore: embed CHANGELOG.md as assembly resource
- chore: install gh CLI via devcontainer feature
- chore: regenerate response examples for cover models

### CI
- ci: bump SDK and artifact paths to net10.0

### Docs
- docs: add spec and plan for .NET 10 LTS upgrade
- docs: add spec and plan for changelog command
- docs: add spec and plan for items cover handling
- docs: add spec and plan for library upgrades and dependency security
- docs: add v0.3.0 in-progress section and .NET 10 LTS idea
- docs: drop roadmap step from changelog plan
- docs: drop Task 9 from changelog plan
- docs: fold .NET 10 LTS upgrade into v0.3.0 scope
- docs: schedule general library upgrades under v0.3.0
- docs: schedule .NET 10 LTS upgrade as v0.3.x
- docs: update dev-container doc for .NET 10
- docs: update v0.3.0 cover-handling entry to reflect deliverable

## 0.2.7 — 2026-04-24

### Highlights
- `abs-cli upload --sequence` now accepts any non-empty string, so decimal series positions (`--sequence 1.5`), zero-prefixed labels, and free-form values like `II` or `0a` work the same way the ABS server does. The CLI previously typed the option as an integer, silently blocking valid ABS sequences at the CLI boundary — a limitation the `abs-management` orchestrator hit while uploading books at fractional series positions. Smoke coverage now asserts a `--sequence 1.5` upload round-trips through `relPath` intact.

### Fixes
- fix: accept string sequences on upload --sequence

### Docs
- docs: mark v0.2.0 shipped and restructure roadmap

## 0.2.6 — 2026-04-24

### Highlights
- `abs-cli items batch-update` works again. The CLI was issuing `PATCH` against `/api/items/batch/update`, but ABS only registers that route as `POST` — every call was coming back as a 404. Single-item updates use `PATCH`, which is where the confusion came from. Smoke coverage now fires a two-item batch-update and asserts the changes were persisted, so this class of verb-mismatch regression won't slip through again.

### Fixes
- fix: use POST for batch-update endpoint

## 0.2.5 — 2026-04-20

### Highlights
- `abs-cli upload --wait` is reliable again: path-based matching instead of title substring. The old logic silently timed out whenever `--sequence` was used, because ABS strips the `N. -` prefix from `media.metadata.title` while the CLI kept searching with it. Drift-detection smoke cases guard against future regressions.
- `UploadReceipt` gains a `relPath` field pointing at the exact folder ABS wrote to, so agents using no-`--wait` uploads can locate the resulting library item without replicating ABS's `sanitizeFilename` rules themselves.
- ABS 2.33.2 is now the highest tested version. Login warning stops firing against 2.33.2 servers. Controller / model diff vs 2.33.1 reviewed — zero breaking changes on the abs-cli API surface.
- `--wait-timeout` option removed. It only bounded the polling loop (not the upload itself), and with path-based matching the timeout rarely matters. On timeout the `UploadReceipt` is now emitted to stdout instead of producing empty output.
- `items search --help` no longer misdescribes the response: it hits the same endpoint as top-level `search` and returns the full multi-array `SearchResult`. The command is kept as an alias; removal scheduled on the roadmap.

### Features
- feat: include relPath in upload receipt

### Fixes
- fix: match uploaded item by relPath, port ABS sanitizeFilename

### Docs
- docs: align items search help with actual behavior
- docs: clarify upload help for agents

### Chores
- chore: raise MaxTestedVersion to 2.33.2
- chore: bump docker-compose abs to 2.33.2

### Breaking changes
- `abs-cli upload --wait-timeout <seconds>` is no longer accepted — the option only controlled the post-upload polling window, which is now a fixed 120s internal. Remove it from any scripts that passed it.


## 0.2.4 — 2026-04-17

### Highlights
- `abs-cli <cmd> --help` now shows a generated `Response shape:` JSON sample for every typed command, so agents and humans can see exactly what each endpoint returns without running it first.
- `authors --help` and `series --help` gain `Notes:` blocks explaining those resources are lifecycle-driven by book metadata (can't be created/deleted directly); `series --help` points at `items list --filter "series=<id>"` for listing books in a series.
- `items` / `search` help now includes concrete shapes for `LibraryItemMinified.media` (book vs. podcast variants) and every untyped array inside `SearchResult`.
- `abs-cli upload` (without `--wait`) returns a typed `UploadReceipt` on stdout instead of exiting silent — callers can now tell success from a swallowed error.
- Dev-tooling cleanup: MemPalace and Caveman integrations removed.

### Features
- feat: add top/bottom positioning to help sections
- feat: add SampleJsonWalker for response-shape codegen
- feat: add codegen tool emitting ResponseExamples.g.cs
- feat: regenerate ResponseExamples.g.cs on build, add drift tests
- feat: add AddResponseExample helpers to HelpExtensions
- feat: add notes and response-shape examples to authors and series
- feat: add response-shape examples to items commands
- feat: add response-shape examples to libraries/backup/tasks/metadata/search
- feat: add search wrapper models and register media types
- feat: add property overrides to SampleJsonWalker
- feat: add book/podcast media union hint to items and search help
- feat: return upload receipt JSON when --wait is not set

### Fixes
- fix: render unescaped angle brackets in response samples
- fix: normalise walker output to LF to keep Windows build happy

### Docs
- docs: add spec and plan for agent-friendly help output
- docs: clarify series help does not return books, show series filter
- docs: require PR URL as clickable link in CLAUDE.md

### Refactors
- refactor: remove dead type guard in WriteSections
- refactor: simplify dictionary/enumerable dispatch and fix test name

### Chores
- chore: remove MemPalace and Caveman integrations


## v0.2.3 — 2026-04-14

### Highlights
- **Debian packages** — `.deb` artifacts for amd64 and arm64 are now built and attached to each release. Install with `dpkg -i abs-cli_0.2.3_amd64.deb`.
- **Homebrew tap** — CLI is now available via `brew install thomaslazar/abs-cli/abs-cli`. The tap formula auto-updates on each release.
- **Install scripts** — `install.sh` for macOS/Linux and `install.ps1` for Windows for quick one-liner installation.

### Features
- feat: add install.sh for macOS and Linux
- feat: add install.ps1 for Windows
- feat: add Homebrew formula template

### Fixes
- fix: use exact PATH matching and handle null UserPath in install.ps1

### Other
- ci: add deb package build step for Linux releases
- ci: add Homebrew tap update job on release
- chore: update release skill for deb packages and Homebrew tap
- docs: add Homebrew, install scripts, and deb to installation section
- docs: add package manager distribution spec and implementation plan

## v0.2.2 — 2026-04-16

### Highlights
- Added support for all ABS filter groups: `missing`, `publishers`, `publishedDecades`, `tracks`, `ebooks`
- Use `--filter "missing=language"` to find items with empty fields (language, cover, isbn, etc.)

### Fixes
- fix: add missing filter groups including 'missing' for empty fields

## v0.2.1 — 2026-04-14

### Highlights
- Fixed upload timing out on large files over slow connections — the 100-second default request timeout now no longer applies to uploads

### Fixes
- fix: remove request timeout on upload to prevent failures on large files

### Other
- docs: update README for v0.2.0 commands and agent use cases

## v0.2.0 — 2026-04-14

### Highlights
- New **backup** commands — create, list, apply, download, delete, upload server backups (admin-only). Safety net before bulk metadata changes.
- New **upload** command — upload audiobook/ebook files with author/series/sequence folder naming, `--wait` polling, auto-folder resolution, and duplicate filename protection (`--prefix-source-dir`, `--files-manifest`).
- New **scan** commands — trigger library scans (`libraries scan`) or single-item rescans (`items scan`).
- New **metadata** commands — search ABS-configured providers (Audible, Google Books, etc.) for book metadata and covers. Agent picks the match, applies via existing `items update`.
- New **tasks** command — poll background task status (e.g. scan progress).

### Features
- feat: add backup create, list, apply, download, delete, upload commands
- feat: add upload command with sequence prefix and --wait polling
- feat: add scan, tasks, and metadata commands
- feat: detect upload filename collisions, add prefix and manifest options
- feat: default --limit to 50 for all list/search commands
- feat: add API endpoints, improve error handling, add new HTTP methods

### Fixes
- fix: per-call HTTP timeout, 10min override for backup operations
- fix: bump upload --wait default timeout from 60s to 300s, add override
- fix: config set accepts exact keys from config get output
- fix: login resets default library on server change
- fix: smoke test fixes for user login and backup file extension

### Other
- ci: bump GitHub Actions to Node.js 24 versions (checkout v6, setup-dotnet v5, upload-artifact v7)
- refactor: consolidate API client methods with optional permissionHint default parameter
- test: 108 smoke test assertions (up from 71), 33 self-test checks (up from 25), new uploaduser test user
- docs: spec, implementation plan, ABS source clone instructions in CLAUDE.md

## v0.1.1 — 2026-04-12

### Highlights
- Fixed login failing with 403 on servers behind reverse proxies (e.g. Cosmos)
- Added macOS Gatekeeper bypass instructions to README
- Improved release workflow reliability (clean Docker state, better CI output handling)

### Fixes
- fix: add User-Agent header to HTTP client
- fix: use host.docker.internal in Docker test scripts
- fix: improve release skill reliability

### Docs
- docs: add macOS Gatekeeper instructions and ask-before-commit rule

## v0.1.0 — 2026-04-12

### Highlights
- First public release of abs-cli — a command-line interface for managing Audiobookshelf servers
- Full audiobook metadata management: list, search, view, update, and batch-edit items, series, and authors
- Native AOT binaries for 6 platforms (linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64, win-arm64) — no .NET runtime required
- Token-based authentication with automatic refresh
- Built-in self-test command for offline AOT integrity verification

### Features
- feat: add login command with access+refresh token storage
- feat: add config get/set commands
- feat: add configuration layer with file, env, and flag precedence
- feat: add libraries list and get commands
- feat: add items commands (list, get, search, update, batch-update, batch-get)
- feat: add series list and get commands
- feat: add authors list and get commands
- feat: add global search command
- feat: add self-test command for offline AOT integrity verification
- feat: add API client with auth, token refresh, and endpoint constants
- feat: add DTO models derived from ABS source code
- feat: add ABS filter encoder with base64 encoding
- feat: add ABS server version compatibility check
- feat: add JWT token expiry helper for proactive refresh
- feat: add console output helper for JSON stdout and stderr errors
- feat: add examples to all command help text
- feat: add filter groups and sort field reference to help text
- feat: add GitHub Actions CI with build matrix and integration tests
- feat: add win-arm64 build target via windows-11-arm runner
- feat: CI auto-attaches binaries to GitHub Releases

### Fixes
- fix: proper AOT support via source-gen, restructure testing
- fix: resolve AOT reflection errors in ConfigManager and AbsApiClient
- fix: use macOS cross-compilation for osx-x64 AOT binary
- fix: use X-Return-Tokens header and accessToken instead of legacy user.token
- fix: run self-test on all platforms including osx-x64 via Rosetta 2
