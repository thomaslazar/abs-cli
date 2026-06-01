# Roadmap

## Completed milestones

### v0.2.0 — Upload, Metadata & Backup (shipped 2026-04-14)

Enabled AI agents to upload books to ABS, apply metadata from providers, and
create safety-net backups. CLI provides sharp primitives; agents orchestrate.

- **Backup** — `abs-cli backup create|list|apply|download|delete|upload` (admin-only).
- **Upload** — `abs-cli upload` with author/series/sequence folder naming, `--wait` polling.
- **Scan** — `abs-cli libraries scan` (async) and `abs-cli items scan` (sync).
- **Metadata** — `abs-cli metadata search|providers|covers` against ABS-configured providers.
- **Tasks** — `abs-cli tasks list` for polling background task status.
- **Permission errors** — Improved error messages for 403/400/404/500 across all commands.
- **Testing** — `uploaduser` test user, permission denial smoke tests, gated external provider tests.

Full spec: [docs/specs/2026-04-12-v0.2.0-upload-metadata-backup.md](specs/2026-04-12-v0.2.0-upload-metadata-backup.md)

Follow-ups shipped in v0.2.1 – v0.2.7: AOT serialization fixes, ABS 2.33.x
compatibility, upload `relPath` + sanitize-drift coverage, batch-update verb fix.

---

### v0.3.0 — Changelog, cover handling & .NET 10 LTS (shipped 2026-04-29)

Bundled release notes, agent-driven cover management, and a .NET 10 LTS upgrade.

- **`abs-cli changelog` command** — Print the most recent entry by default;
  `--all` prints the full file. Source of truth is the bundled
  `CHANGELOG.md`, embedded in the assembly so the command works offline.
- **`items cover set|get|remove`** — Apply (`--url` / `--file` /
  `--server-path`), fetch (to file or stdout), and remove book covers.
  Composes with `items list --filter "missing=cover"` and `metadata covers`
  for a full cover-handling workflow.
- **Target framework upgraded to .NET 10 LTS** — Dev container, CI matrix,
  and all three csproj files. AOT trimmer improvements dropped the
  Linux-x64 binary from ~11 MB to ~8.7 MB.
- **`System.CommandLine` 2.0.0-beta4 → 2.0.7 stable** — Custom help-section
  infrastructure rewritten against the new action-based help model;
  user-facing help format is byte-for-byte identical to before.
- **Build-time NuGet audit gate** — `Directory.Build.props` with
  `WarningsAsErrors` for NU1901-NU1904. Combined with Dependabot security
  updates, CVEs in dependencies now surface as build failures.

Full notes: see [CHANGELOG.md](../CHANGELOG.md) or `abs-cli changelog`.

---

### v0.4.0 — Author management & items-search cleanup (shipped 2026-05-11)

Expand the author surface for agent-driven metadata cleanup, and drop the
duplicate `items search` subcommand.

- **Author pagination (breaking)** — `abs-cli authors list` switched to the
  paginated response shape `{ results, total, limit, page }` with
  `--limit`, `--page`, `--sort` (`name` / `lastFirst` / `addedAt` /
  `updatedAt` / `numBooks`), `--desc`. Callers that read `.authors` must
  switch to `.results`.
- **`abs-cli authors match` / `lookup`** — Audnexus-backed match
  (`POST /api/authors/:id/match`, destructive — writes
  `asin`/`imagePath`/`description`) and the read-only probe
  (`GET /api/search/authors?q=`).
- **`abs-cli authors update` / `delete`** — Edit `name` / `description` /
  `asin` (tri-state per field: set / clear via empty string / leave alone).
  Surfaces ABS's silent-merge-on-rename by returning
  `{ merged: true, author: <target> }` instead of `{ updated, author }`.
  `delete` unlinks from all books.
- **`abs-cli authors image set|get|remove`** — Author images via URL
  download (`set --url`), file/stdout (`get --output`), or DELETE
  (`remove`).
- **Deprecated `abs-cli items search` removed (breaking)** — Duplicate of
  top-level `abs-cli search`. The help-text-level deprecation was in place
  through v0.2.x and v0.3.0; v0.4.0 ships the hard removal.

Full notes: see [CHANGELOG.md](../CHANGELOG.md) or `abs-cli changelog`.

---

### v0.5.0 — File management (shipped 2026-05-20)

Four primitives for the audiobook-cleanup loop, all on top of existing
ABS endpoints: merge multi-file audiobooks into a single tagged `.m4b`,
pull and write external chapter metadata, embed ABS's current tags into
the audio files themselves, and toggle which ebook file is primary on
multi-format items. Plus diagnostic logging for troubleshooting, an
`items get --expanded` flag for discovering ebook file inodes, and ABS
2.34 / 2.35 support.

- **`items encode-m4b start|cancel`** — Wraps ABS's
  `POST/DELETE /api/tools/item/:id/encode-m4b` (admin). Concatenates
  `media.includedAudioFiles` through ffmpeg, embeds chapters + cover,
  writes a single tagged `.m4b` to the library dir, and moves the
  originals into the server's `cache/items/` directory as a backup.
  `start --wait` polls via the standard tasks endpoint.
- **`items chapters lookup|set`** — Audnexus-backed
  `GET /api/search/chapters?asin&region` for read-only lookup, and
  `POST /api/items/:id/chapters` for writing. Diffing across the two
  is the agent's job; the CLI ships the primitives, including the
  units note (Audnexus ms vs. write-endpoint seconds).
- **`items embed-metadata` / `items batch-embed-metadata`** — Wraps the
  admin-only `POST /api/tools/item/:id/embed-metadata` and
  `/api/tools/batch/embed-metadata`. In-place ffmpeg rewrite that bakes
  ABS's current tags / cover / chapters into the audio files
  themselves; ABS's `items update` and friends only persist to the DB
  and sidecar. `--force-embed-chapters` for multi-file books; `--backup`
  keeps a server-side copy before the rewrite.
- **`items toggle-ebook-status`** — Wraps
  `PATCH /api/items/:id/ebook/:fileid/status` for multi-ebook items
  (e.g. `.epub` + `.pdf`). True toggle: flips the targeted file's
  `isSupplementary` and auto-demotes the previous primary; calling it
  on the current primary unsets without promoting another. `:fileid`
  is the file inode from `libraryFiles[].ino`.
- **`cache purge-items` / `cache purge`** — Wraps the admin-only
  `POST /api/cache/items/purge` and `/api/cache/purge`. Reclaims the
  per-item track backups left by `items encode-m4b` and `embed-metadata
  --backup`; the broader form also drops `cache/covers/` and
  `cache/images/`, which ABS rebuilds lazily.
- **Diagnostic logging** — `--debug` flag and `ABS_DEBUG=1` env var
  emit one stderr line per HTTP call (method + full URL + status, body
  on non-2xx) plus token-refresh and version-check decisions.
  `--log-json` switches stderr to single-line JSON
  (`{"timestamp","level","message"}`). Off by default; bearer token,
  refresh token, and request bodies are never logged.
- **`items get --expanded`** — Opt-in for ABS's `?expanded=1` shape
  (`libraryFiles[]`, `lastScan`, `scanVersion`, full media payload).
  Required to discover the `ino` that `items toggle-ebook-status`
  consumes end-to-end.
- **ABS 2.34 / 2.35 support** — `MinSupportedVersion` /
  `MaxTestedVersion` widened to `2.33.1 — 2.35.0`. v2.34 closes the
  upstream `items batch-update` `canUpdate` gap (now returns 403 for
  users without update permission); v2.35 adds a 60s server-side
  refresh-token grace period, no CLI-side change required.
- **Reverse-proxy sub-path fix** — Fixed a latent RFC 3986 § 5.2 bug
  at `AbsApiClient.cs:25` that silently dropped the URL path component
  on every request. Installs behind a reverse proxy at a sub-path
  (e.g. `https://my.domain.net/audiobookshelf`) no longer get `405 Method
  Not Allowed`.

Breaking change: stderr error / warning prefix moved from
`Error: <message>` / `Warning: <message>` to
`<iso8601> ERROR <message>` / `<iso8601> WARN <message>` (or
single-line JSON under `--log-json`). stdout (command JSON data) is
unchanged; substring matches on message bodies keep working.

Full notes: see [CHANGELOG.md](../CHANGELOG.md) or `abs-cli changelog`.

---

## Next

### v0.6.0 — TBD

- **`items delete` + `items batch-delete`** — Remove library items via `DELETE /api/items/:id` and `POST /api/items/batch/delete`. Soft delete (DB only, default) vs `--hard` (also removes files from disk, irreversible). `delete` permission; author/series cascade-prune. Research: [docs/specs/research/2026-05-30-delete-login-update-stdin.md](specs/research/2026-05-30-delete-login-update-stdin.md).
- **`login --username` / `--password` / `--password-stdin`** — Non-interactive credential parameters on the `login` command; each falls back to the interactive prompt when absent. `--password-stdin` avoids process-list/history exposure. See same research doc.
- **`items update --stdin`** — Bring `items update` in line with the batch-* shape (`--input <file>` or `--stdin`), retiring the inline-JSON-or-file `--input` behavior (breaking). See same research doc.
- **Smoke-test tidying** — Dedicated follow-up after the delete/login/update-stdin PR de-curls the smoke. Collapse the repeated `python3 -c "import sys,json…"` JSON-extraction one-liners into a `json_get` helper, and parameterize the near-identical per-section `*_cleanup()` traps (`encode_cleanup` / `chapters_cleanup` / `embed_cleanup` / `progress_cleanup` / `collections_cleanup`) into one reusable helper. Pure test-harness DRY; no CLI behavior change. Kept out of the delete/login PR to keep that diff focused.
- **Extended help mode** — Hide response-shape blocks from `--help` by default; surface them via an explicit flag (e.g. `--help-shape`, `--help-full`) or a separate subcommand. Today every command renders one or two `Response shape:` blocks, and `items get --expanded` adds a second one — without a way to opt out, the help output drifts toward unreadable as more commands ship.
- **`items get --include=<flags>`** — Opt-in for ABS's `?include=progress,rssfeed,downloads,share` query parameter. Adds user-progress, RSS feed, download, and share state to the response (each as a nullable field). **Auto-implies `--expanded`** — the server-side include parser only fires under `expanded=1` (`LibraryItemController.js:72-99`). Bundled with `me` + `items progress` since `--include progress` reads what `items progress set` writes — see same research doc.
- **`items progress get|set|remove` + `items batch-update-progress`** — Mark books as listened / read / in-progress for the current user. Wraps `GET /api/me/progress/:id`, `PATCH /api/me/progress/:libraryItemId` (body: `isFinished`, `currentTime`, `ebookProgress`, …), `DELETE /api/me/progress/:id`, and `PATCH /api/me/progress/batch/update`. No special permission — every authenticated user manages their own progress. Closes the "I cannot tell the CLI a book is already listened" gap; pairs with `items get --include=progress` for read-back. Research: [docs/specs/research/2026-05-30-me-progress-include.md](specs/research/2026-05-30-me-progress-include.md).
- **`abs-cli me`** — Show the currently authenticated user. Wraps `GET /api/me` and returns the user object (`id`, `username`, `type`, `permissions`, …). Today `config get` only shows server + masked tokens; the username is printed once at login and then unrecoverable without a token decode. No special permission. Pairs with the new `items progress` verbs so agents can confirm whose progress they're about to touch. Bundled with `items progress` for shipping — see same research doc.
- **Collections** — `abs-cli collections list|get|create|update|reorder|delete|add|remove|batch-add|batch-remove` covering the full ABS collections endpoint set (`CollectionController.js` plus `GET /api/libraries/:id/collections`). Library-scoped (`list` mirrors `series list` / `authors list` — paginated, `--sort` / `--desc` / `--filter` / `--include rssfeed` / `--minified`; global `GET /api/collections` is dropped). Two-verb split on ABS's overloaded `PATCH /api/collections/:id`: `update` for name/description, `reorder` for the books-array reshuffle (which does not add or remove — that's `add` / `remove` / `batch-add` / `batch-remove`). Permissions: `update` for create / update / reorder / membership changes; `delete` for collection delete. Sharp edges flagged in research (no empty collections, single-add 400s on duplicate vs batch-add silent skip, books referenced by libraryItemId everywhere despite the server's `:bookId` URL naming, RSS feed auto-close on delete). Research: [docs/specs/research/2026-05-22-collections.md](specs/research/2026-05-22-collections.md).

---

## Ideas

Not yet scoped — notes to pick up later.

_None currently. New ideas land here before getting scheduled into a release._

## Deferred features

All are additive — nothing in the v1 architecture blocks them.

| Feature | Reason for deferral |
|---------|-------------------|
| Table output (`--table` via Spectre.Console) | JSON-only sufficient for v1; agents don't need tables |
| Episodes resource | No podcast libraries in current use |
| Playlists resource | Per-user playlists, distinct from library-scoped collections; not needed for metadata workflow |
| `items files` | File management not in scope |
