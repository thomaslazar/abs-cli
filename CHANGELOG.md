# Changelog

All notable changes to abs-cli are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/).

## v1.0.4 — 2026-08-13

Patch release. The server version check now runs once a day instead of only at
login, plus three config-file robustness fixes.

### Highlights

- **The version check no longer depends on logging in.** Self-hosted servers change
  version when the image is pulled, and tokens refresh for months without a fresh
  login — so the old login-only check almost never fired. It now runs on the first
  command after a 24-hour window, via Audiobookshelf's unauthenticated
  `GET /status`, with its own 3-second timeout and silent on failure so it can
  never break the command it precedes.
- **The warning says what to do:** `abs-cli 1.0.4 was tested up to ABS 2.36.0;
  this server is 2.38.0. Check for a newer abs-cli.` It also notes when the
  version changed since the last check.
- **Two new CLI-managed config keys**, `lastVersionCheck` and
  `lastServerVersion`, shown by `abs-cli config get` and not settable.
- **Token refresh no longer writes environment values into `config.json`.**
  Running with `ABS_TOKEN`, `ABS_SERVER` or `ABS_LIBRARY` set meant a refresh
  persisted those into the file you had deliberately kept them out of.
- **An interrupted config write can no longer destroy your refresh token.**
  Saving now writes a temp file and renames over the target, preserving the
  existing file mode, so a config you `chmod 600`'d stays that way.
- **A corrupt `config.json` reports one error line instead of a stack trace**, and
  names the file and the way out.
- **`seed.sh` no longer aborts mid-scan** when a scan poll returns no `total`.

### Changes

- chore: bump version to 1.0.4
- chore: regenerate response examples for ServerStatus
- chore: write release notes to temp/ instead of the repo root
- docs: add server version check cadence spec and plan
- docs: document the 24h runtime version check
- feat: add 24h staleness decision for the server version check
- feat: add ServerStatus DTO for the /status version probe
- feat: check the server version at most daily instead of only at login
- feat: persist server version check state in config
- feat: show version check state in config get
- fix: carry version check state through config resolution
- fix: harden config.json read and write
- fix: stop seed.sh aborting when a scan poll returns no total
- fix: stop token refresh writing env values into config.json
- fix: sync in-memory config when recording the server version
- fix: thread stored version state into login's temp client
- refactor: replace CheckServerVersion with a pure VersionWarning
- test: assert version check cadence in the smoke suite
- test: join the NLog collection from VersionComparisonTests
- test: make the encode-m4b already-processing assertion deterministic

## v1.0.3 — 2026-08-12

Patch release. Fixes two robustness bugs found by an audit from `grimoire-cli`,
which uses this repo as its reference implementation, and makes the `ABS_*`
environment variables discoverable from `--help` (#71).

### Highlights

- **A prerelease or `v`-prefixed server version no longer fails `login`.** The
  compatibility check parsed every dotted segment as an integer, so `2.36.0-beta`
  or `v2.36.0` threw where it should have warned. It also threw *after* the
  credentials were written, leaving you logged in but staring at a cryptic error
  and a non-zero exit. Segments now contribute only their leading digits.
- **Token expiry is read correctly for non-ASCII usernames.** JWT payloads are
  base64url, but the decoder padded without mapping `-` and `_` back, so those
  tokens silently lost their expiry — proactive refresh stopped happening and the
  CLI fell back to refreshing reactively on a 401.
- **`ABS_SERVER`, `ABS_TOKEN` and `ABS_LIBRARY` now appear in `--help`**, with
  their precedence against flags and the config file. They were documented only in
  the README, which is not where an agent driving the CLI looks.
- **`docs/input-output.md` no longer claims every command writes JSON to stdout.**
  Binary `--output -` streams, side-effect-only commands, and `self-test` are now
  called out explicitly.
- No breaking changes; no command, flag, or output shape was removed.

### Changes

- chore: bump version to 1.0.3
- chore: give the dev compose stack a dedicated network
- docs: surface ABS_* env vars in root help, fix output claims
- feat: stamp PR builds with a build id in --version
- fix: tolerate non-numeric versions and base64url token payloads
- fix: use -p: not /p: in the publish step

## v1.0.2 — 2026-07-30

Patch release. Adds Audiobookshelf 2.36.0 to the tested range and stops
`items get --expanded` from discarding the `numFiles` field 2.36.0 introduced.

### Highlights

- **Audiobookshelf 2.36.0 support.** The tested range is now `2.33.1 — 2.36.0`, so
  logging into a 2.36.0 server no longer prints an "untested version" warning.
  Nothing was dropped from the supported range.
- **`items get --expanded` no longer loses a field on 2.36.0 servers.** ABS 2.36.0
  started including `numFiles` in expanded item responses; abs-cli was silently
  discarding it. It now surfaces, and is omitted rather than reported as `0` when
  talking to older servers that don't send it.
- **`docs/abs-api-coverage.md` is accurate again.** Sixteen endpoint rows still
  claimed "not implemented" for commands that already shipped — the tags, genres,
  and playlists verbs — and six of the coverage counts were stale.
- No breaking changes; no command, flag, or output shape was removed.

### Changes

- chore: bump version to 1.0.2
- chore: raise MaxTestedVersion to 2.36.0
- docs: note smoke-test.sh needs a fresh seed per run

## v1.0.1 — 2026-07-23

Patch release. Fixes a crash reading media progress against libraries that
contain legacy numeric `ebookLocation` values.

### Highlights
- `me` (and other commands that read `mediaProgress[]`) no longer crash when Audiobookshelf returns a numeric `ebookLocation`. ABS declares the field as a string, but SQLite type affinity lets older numeric values come back as bare numbers; the CLI now tolerates them and surfaces the value as a string (#65).

### Fixes
- fix: tolerate numeric ebookLocation in mediaProgress (#65)

## v1.0.0 — 2026-07-22

First stable release. abs-cli now covers a major set of Audiobookshelf
management operations for book libraries, with consistent `--help`,
permission tagging, and AOT-compiled single-file binaries for six platforms.

### Highlights
- **Playlists** — full lifecycle: list, get, create (empty allowed), update, reorder, delete, add/remove, batch add/remove, and snapshot `create-from-collection`.
- **Libraries** — create, update, reorder, and a delete guarded by a typed-name confirmation (its cascade removes all contents).
- **Tags, genres & narrators** — list, rename (merge-aware), and delete across a library.
- **Series** — edit name and description.
- **Item files** — per-file `download`, `delete`, and raw `ffprobe`.
- Project conventions consolidated into `CLAUDE.md`; help text tightened to non-obvious caveats only.

### Features
- feat: playlists command — list/get/create/update/reorder/delete/add/remove/batch-add/batch-remove/create-from-collection
- feat: libraries create, update, delete (typed-name gate), and reorder
- feat: tags command — list/rename/delete
- feat: genres command — list/rename/delete
- feat: narrators command — list/rename/delete
- feat: series update
- feat: items file subgroup — download/delete/ffprobe

### Fixes
- fix: correct permission checks for `items file ffprobe`, `libraries reorder`, `narrators delete`, and GET endpoints in the coverage map (landed with their feature commits)

### Docs & internal
- docs: consolidate project conventions into `CLAUDE.md`; trim help-text over-explanation across commands
- test: smoke coverage + 403/permission assertions for all new commands
- chore: install ponytail and answer-first skills in the devcontainer

## 0.6.1 — 2026-06-17

### Highlights
- `upload --wait` now reliably confirms uploads with very long titles. Previously, when a title was long enough that the server truncated the on-disk folder name, the predicted path no longer matched and `--wait` reported a false failure (exit 1) — even though the upload had succeeded — which was confusing and risked duplicate re-uploads (#54).
- A no-confirmation result from `--wait` is now a clear warning that the upload **succeeded** (with guidance not to re-upload), rather than an error implying data loss.

### Fixes
- fix: match upload --wait item by tolerant per-segment relPath
- fix: normalise leading slash in relPath matcher
- fix: reword upload --wait no-match as success-with-warning

### Internal
- feat: add per-segment relPath matcher for upload --wait
- refactor: drop dead client-side path truncation
- test: smoke-cover long-title upload --wait (issue #54)
- docs: add upload --wait relpath matching design and plan

## 0.6.0 — 2026-06-02

### Highlights

- **Collections.** Full collections surface: `collections list|get|create|`
  `update|reorder|delete|add|remove|batch-add|batch-remove`. Library-scoped
  `list` mirrors `series`/`authors` (paginated, `--sort`/`--desc`/`--filter`/
  `--include rssfeed`/`--minified`). `update` (name/description) and `reorder`
  (book-order reshuffle) split ABS's overloaded PATCH; membership changes use
  the add/remove/batch verbs.
- **Listening progress.** `items progress get|set|remove` and
  `items batch-update-progress` let the current user mark books
  listened/read/in-progress (wraps `/api/me/progress/*`; no special
  permission). `items get --include=progress,rssfeed,downloads,share` reads
  that state back (auto-implies `--expanded`).
- **`abs-cli me`.** Show the currently authenticated user (`GET /api/me`):
  `id`, `username`, `type`, `permissions`, … Pairs with the progress verbs so
  agents can confirm whose progress they're touching.
- **Item deletion.** `items delete` and `items batch-delete` remove library
  items — soft delete (DB only, default) vs `--hard` (also removes files from
  disk, irreversible). Requires `delete` permission.
- **Non-interactive login.** `login --username` / `--password` /
  `--password-stdin`, each falling back to the interactive prompt when absent.
  `--password-stdin` keeps credentials out of the process list and shell
  history.
- **Extended help mode.** Plain `--help` now hides the `Response shape:` blocks
  (printing a one-line pointer) to stay scannable; the global `--help-full`
  flag shows the complete help including them.
- **ABS 2.35.1 support.** Tested range widened to `2.33.1 — 2.35.1`. v2.35.1 is
  a patch with internal-only fixes — no response-shape, endpoint, or permission
  changes for endpoints abs-cli consumes.

### ⚠ Breaking change

`items update` now takes its update body via `--input <file>` or `--stdin`,
matching the `batch-*` commands. The previous inline-JSON-or-file `--input`
behavior is removed — `--input` is now strictly a file path. Pipe JSON via
`--stdin` (e.g. `echo '{...}' | abs-cli items update --id X --stdin`) or pass a
file with `--input payload.json`.

### Features

- feat: add Collection and RssFeed models
- feat: add collection request DTOs
- feat: add collections batch-add and batch-remove subcommands
- feat: add CollectionsCommand skeleton and tri-state body builder
- feat: add collections create subcommand
- feat: add collections delete subcommand
- feat: add collections endpoint constants
- feat: add collections get subcommand
- feat: add collections list subcommand
- feat: add collections reorder subcommand
- feat: add CollectionsService skeleton
- feat: add collections single add/remove subcommands
- feat: add collections update subcommand
- feat: add --help-full option and includeShapes plumbing
- feat: add --include decorator fields to LibraryItemExpanded
- feat: add --include to items get with auto-imply expanded
- feat: add items batch-delete endpoint constant
- feat: add items batch-update-progress subcommand
- feat: add items delete and batch-delete subcommands
- feat: add items progress get/set/remove subcommands
- feat: add ItemsService delete and batch-delete
- feat: add me command
- feat: add MediaProgress and ProgressUpdateRequest models
- feat: add Me model and extend UserPermissions with JsonExtensionData
- feat: add me + progress endpoint constants
- feat: add MeService
- feat: add ProgressService
- feat: add SelfTest entries for collections models
- feat: hide response shapes from --help, show via --help-full
- feat: implement CollectionsService batch membership methods
- feat: implement CollectionsService.CreateAsync
- feat: implement CollectionsService.DeleteAsync
- feat: implement CollectionsService read methods
- feat: implement CollectionsService single membership methods
- feat: implement CollectionsService update and reorder
- feat: ItemsService.GetExpandedAsync accepts optional include
- feat: items update uses --input <file> / --stdin (breaking)
- feat: login accepts --username / --password / --password-stdin
- feat: wire CollectionsCommand into root
- feat: wire MeCommand into root and add SelfTest entries

### Fixes

- fix: enforce file-only --input on items update; update input-output doc
- fix: lowercase all-or-nothing in batch-delete help to match test
- fix: parse --finished-at with invariant culture and assume UTC
- fix: preserve unknown RssFeed fields via JsonExtensionData
- fix: rename seed TMPDIR var so multi-ebook fixture mktemp survives
- fix: use READONLY_TOKEN, add trap cleanup, defensive JSON parsing in collections smoke

## 0.5.0 — 2026-05-20

### Highlights

- **File management commands.** New verbs for the audiobook-cleanup loop:
  `items encode-m4b start|cancel` to merge multi-file audiobooks into a
  single tagged `.m4b`; `items chapters lookup|set` for Audnexus-backed
  chapter metadata; `items embed-metadata` + `items batch-embed-metadata`
  to bake ABS's current tags / cover / chapters into the audio files via
  in-place ffmpeg rewrite; `items toggle-ebook-status` to flip which
  ebook file is primary on multi-format items; `cache purge-items` /
  `cache purge` to reclaim disk used by per-item backups.
- **Diagnostic logging.** New `--debug` flag and `ABS_DEBUG=1` env var
  emit one stderr line per HTTP call (method + full URL + status, plus
  the response body on non-2xx) plus token-refresh and version-check
  decisions. New `--log-json` flag switches stderr output to single-line
  JSON (`{"timestamp":"…","level":"…","message":"…"}`). Off by default;
  bearer token, refresh token, and request bodies are never logged.
- **`items get --expanded`.** Opt-in flag returns ABS's expanded shape
  (`libraryFiles[]`, `lastScan`, `scanVersion`, etc.) instead of the
  default minified shape. Required for discovering supplementary ebook
  file inodes that `items toggle-ebook-status` consumes.
- **ABS 2.34 / 2.35 support.** Tested range widened to `2.33.1 — 2.35.0`.
  v2.34 closes the upstream `items batch-update` `canUpdate` gap (now
  returns 403 for users without update permission); v2.35 adds a 60s
  server-side refresh-token grace period (CLI behavior unchanged).
- **Reverse-proxy sub-path fix.** Fixed a latent RFC 3986 § 5.2 bug at
  `AbsApiClient.cs:25` that silently dropped the path component of any
  configured server URL on every request. Users with installs behind a
  reverse proxy at a sub-path (e.g. `https://my.domain.net/audiobookshelf`)
  no longer get `405 Method Not Allowed` on every call.

### ⚠ Breaking change

The stderr format for error and warning messages changes. Before:

```
Error: Permission denied. This operation requires 'update' permission.
Warning: ABS server version 2.36.0 has not been tested with this version of abs-cli.
```

After (default text layout):

```
2026-05-20T14:23:45.123Z ERROR Permission denied. This operation requires 'update' permission.
2026-05-20T14:23:45.123Z WARN  ABS server version 2.36.0 has not been tested with this version of abs-cli.
```

Or with `--log-json`:

```
{"timestamp":"2026-05-20T14:23:45.123Z","level":"Error","message":"Permission denied. …"}
```

stdout (command JSON data output) is unchanged. Message bodies are
unchanged — scripts that substring-match on message content keep
working. Scripts that match the `Error:` / `Warning:` prefix verbatim
need to update.

### Features

- feat: add AddPermissionRequired help section helper
- feat: add cache purge-items and cache purge commands
- feat: add cache service for items + full purge
- feat: add chapter endpoint constants to ApiEndpoints
- feat: add chapter model types
- feat: add ChaptersService with lookup and set
- feat: add ebook-file-status endpoint constant
- feat: add EbookFileStatusReceipt model
- feat: add embed-metadata endpoint constants
- feat: add embed-metadata model types
- feat: add EmbedMetadataService with start, batch, and wait
- feat: add encode-m4b request/receipt models
- feat: add EncodeM4bService
- feat: add GetExpandedAsync to ItemsService
- feat: add items encode-m4b command tree (start, cancel)
- feat: add LibraryItemExpanded model types
- feat: add NLog logging with --debug, ABS_DEBUG, --log-json
- feat: add ToggleEbookFileStatusAsync to ItemsService
- feat: add ToolsItemEncodeM4b endpoint and notFoundHint to AbsApiClient
- feat: apply Permission required tags across all commands
- feat: register chapter models in AppJsonContext
- feat: register EbookFileStatusReceipt in AppJsonContext
- feat: register embed-metadata models in AppJsonContext
- feat: register expanded item models in AppJsonContext
- feat!: route errors and warnings through NLog
- feat: support abs 2.34 and 2.35
- feat: top-level exception handler routes through NLog
- feat: trace HTTP requests, token refresh, version check
- feat: wire items chapters lookup/set commands
- feat: wire items embed-metadata and batch-embed-metadata commands
- feat: wire items get --expanded flag
- feat: wire items toggle-ebook-status command

### Fixes

- fix: add missing 'delete' permissionHint to items cover remove
- fix: compose request URLs against BaseAddress trailing slash
- fix: make HelpExtensions section dictionary thread-safe
- fix: serialize url composition tests with nlog collection

### Refactors

- refactor: drop redundant debug lines in CheckServerVersion

### Tests

- test: add chapter model round-trip and shape-validation tests
- test: add chapter models to self-test round-trip
- test: add EbookFileStatusReceipt round-trip
- test: add EbookFileStatusReceipt to self-test round-trip
- test: add embed-metadata model round-trip tests
- test: add embed-metadata models to self-test round-trip
- test: add expanded item models to self-test round-trip
- test: add help-text tests for items chapters lookup/set
- test: add help-text tests for items embed-metadata commands
- test: add help-text tests for items get --expanded
- test: add help-text tests for items toggle-ebook-status
- test: add LibraryItemExpanded model round-trip tests
- test: add spot checks for Permission required section
- test: assert batch-update 403 for users without update permission
- test: drop docker-exec filesystem checks from embed-metadata smoke
- test: encode-m4b models round-trip and items encode-m4b command help
- test: fix codec:copy assertion to match WriteIndented JSON output
- test: force stereo on encode-m4b smoke fixtures
- test: help-text and smoke coverage for cache purge
- test: seed multi-ebook fixture for toggle-ebook-status smoke
- test: seed readonlyuser and smoke canUpdate denials
- test: self-test round-trip for encode-m4b models
- test: smoke chapters set + gated lookup
- test: smoke coverage + docs for diagnostic logging
- test: smoke coverage for items encode-m4b lifecycle
- test: smoke items embed-metadata + batch-embed-metadata
- test: smoke items get --expanded and replace raw expanded curl in toggle smoke
- test: smoke items toggle-ebook-status + update counts for 16-item seed
- test: smoke the encode-m4b cancel happy path
- test: swap raw curls for CLI calls in toggle-ebook-status smoke
- test: tighten encode-m4b options omission assertion
- test: ungate chapters lookup smoke
- test: use Audnexus-indexed ASIN in chapters lookup smoke

### Chores

- chore: add ffmpeg to devcontainer
- chore: bump abs reference checkout hint to v2.35.0
- chore: bump dev compose and ci abs image to 2.35.0
- chore: regenerate ResponseExamples.g.cs with chapter models
- chore: regenerate ResponseExamples.g.cs with EbookFileStatusReceipt
- chore: regenerate ResponseExamples.g.cs with embed-metadata models
- chore: regenerate ResponseExamples.g.cs with expanded item models
- ci: install ffmpeg in smoke-test runner
- style: drop unnecessary blank lines in AbsApiClient

### Docs

- docs: add cache commands to README table
- docs: align CLAUDE.md prose with bumped tag and unify behavior spelling
- docs: backfill README Commands table and add command implementation conventions
- docs: explain why admin permissionHint is unquoted
- docs: move v0.4.0 to completed milestones
- docs: note refresh-token grace period in abs 2.35
- docs: require post-PR CI verification before declaring work done
- docs: trim redundant content from items get help
- docs: update items get row for --expanded and broaden README-update convention

Plus the specs, plans, and roadmap edits that accumulated during the
milestone — see git history for the full set.


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
