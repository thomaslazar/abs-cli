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

### v0.6.0 — Deletion, progress, collections & help polish (shipped 2026-06-02)

Rounds out the agent-driven management surface: deleting items, managing
per-user listening progress, full collections support, non-interactive login,
and help output that stays scannable as the command set grows.

- **`items delete` / `items batch-delete`** — Remove library items via
  `DELETE /api/items/:id` and `POST /api/items/batch/delete`. Soft delete
  (DB only, default) vs `--hard` (also removes files from disk, irreversible).
  `delete` permission; author/series cascade-prune.
- **Non-interactive `login`** — `--username` / `--password` /
  `--password-stdin`, each falling back to the interactive prompt when absent.
  `--password-stdin` avoids process-list / shell-history exposure.
- **`items update --stdin`** — Aligns `items update` with the batch-* shape
  (`--input <file>` or `--stdin`), retiring the inline-JSON-or-file `--input`
  behavior (breaking).
- **`items progress get|set|remove` + `items batch-update-progress`** — Mark
  books listened / read / in-progress for the current user. Wraps the
  `GET/PATCH/DELETE /api/me/progress/*` endpoints. No special permission.
- **`items get --include=<flags>`** — Opt-in for ABS's
  `?include=progress,rssfeed,downloads,share`; auto-implies `--expanded`.
- **`abs-cli me`** — Show the currently authenticated user (`GET /api/me`):
  `id`, `username`, `type`, `permissions`, …
- **Collections** — `collections list|get|create|update|reorder|delete|add|`
  `remove|batch-add|batch-remove` covering the full ABS collections endpoint
  set. Library-scoped; `update`/`reorder` split on ABS's overloaded PATCH;
  `update` permission for mutations, `delete` for collection delete.
- **Extended help mode** — Plain `--help` hides the `Response shape:` blocks
  (printing a one-line pointer) to stay scannable; the global recursive
  `--help-full` flag shows the complete help including them.
- **Smoke-test tidying** — Pure test-harness DRY: `json_get` and
  `cleanup_items` helpers collapse the repeated JSON-extraction one-liners and
  per-section cleanup traps. No CLI behavior change.

Full notes: see [CHANGELOG.md](../CHANGELOG.md) or `abs-cli changelog`.

---

## Next

_Nothing scheduled. The next milestone lands here once scoped._

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
