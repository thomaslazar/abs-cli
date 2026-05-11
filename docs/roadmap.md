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

## In progress

### v0.4.0 — Author management & items-search cleanup

Expand the author surface so agents can identify and clean up unmatched
authors; drop the duplicate `items search` subcommand.

- **Author pagination** — `abs-cli authors list` switched to the paginated
  response shape (`{ results, total, limit, page }`) with `--limit`,
  `--page`, `--sort` (`name` / `lastFirst` / `addedAt` / `updatedAt` /
  `numBooks`), `--desc`. The unpaginated `{ authors: [...] }` model is
  gone; callers that read `.authors` must switch to `.results`.
- **Author matching** — `abs-cli authors match` (Audnexus-backed
  `POST /api/authors/:id/match` — destructive, writes `asin`, `imagePath`,
  `description`, and emits `author_updated`). A 404 leaves the author
  untouched, which surfaces authors that need cleanup
  (e.g. `"Joe Bloggs - illustrator"`).
- **Author lookup** — `abs-cli authors lookup --name <text>` for a
  read-only Audnexus probe (`GET /api/search/authors?q=`) — no `region`,
  no ASIN path. Use before `match` when you want to inspect candidates.
- **Author edit / delete / image** — `abs-cli authors update` (edit
  `name` / `description` / `asin`; tri-state per field: set / clear via
  empty string / leave alone). Surfaces ABS's silent-merge-on-rename
  behaviour by returning `{ merged: true, author: <target> }` instead
  of `{ updated, author }` when a same-name conflict triggers the merge.
  `abs-cli authors delete` unlinks from all books and deletes.
  `abs-cli authors image set|get|remove` mirrors `items cover`.
- **Remove deprecated `abs-cli items search`** — Hard removal of the
  duplicate subcommand. `items search` and top-level `abs-cli search`
  hit the same endpoint (`GET /api/libraries/:id/search`) with the same
  options and response shape. The `items search` help text has carried
  an "alias of `abs-cli search`; prefer that" note through v0.2.x and
  v0.3.0; v0.4.0 ships the removal.
  Spec: [docs/specs/2026-05-11-remove-items-search-subcommand.md](specs/2026-05-11-remove-items-search-subcommand.md).
  Plan: [docs/plans/2026-05-11-remove-items-search-subcommand.md](plans/2026-05-11-remove-items-search-subcommand.md).

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
| Collections / Playlists resources | Not needed for metadata workflow |
| `items files` / `items progress` | Playback and file management not in scope |
