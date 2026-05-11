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

Follow-ups shipped in v0.2.1 – v0.2.6: AOT serialization fixes, ABS 2.33.x
compatibility, upload `relPath` + sanitize-drift coverage, batch-update verb fix.

---

## In progress

### v0.3.0 — Changelog, cover handling & .NET 10 LTS

Four roadmap items being delivered together as the next minor release.

- **`abs-cli changelog` command** — Print the most recent entry by default;
  `--all` prints the full file. Source of truth is the bundled
  `CHANGELOG.md`, embedded in the assembly so the command works offline.
  Spec: [docs/specs/2026-04-27-changelog-command.md](specs/2026-04-27-changelog-command.md).
  Plan: [docs/plans/2026-04-27-changelog-command.md](plans/2026-04-27-changelog-command.md).
- **`items cover` command** — Apply, fetch, and remove book covers via the
  ABS API. Three primitives (`set` with `--url` / `--file` / `--server-path`,
  `get` to file or stdout, `remove`) that the agent composes with
  `items list --filter "missing=cover"` and `metadata covers` to build a
  cover-handling workflow.
  Spec: [docs/specs/2026-04-28-items-cover-handling.md](specs/2026-04-28-items-cover-handling.md).
  Plan: [docs/plans/2026-04-28-items-cover-handling.md](plans/2026-04-28-items-cover-handling.md).
- **Upgrade target framework to .NET 10 LTS** — Move from `net8.0` to
  `net10.0`. Covers the dev container, CI, and all three csproj files;
  test packages and `System.CommandLine` are deferred to the next item.
  Spec: [docs/specs/2026-04-28-dotnet-10-lts-upgrade.md](specs/2026-04-28-dotnet-10-lts-upgrade.md).
  Plan: [docs/plans/2026-04-28-dotnet-10-lts-upgrade.md](plans/2026-04-28-dotnet-10-lts-upgrade.md).
- **General library upgrades** — Bump `System.CommandLine` from the long-stale
  `2.0.0-beta4` pin to the now-stable 2.0.7 (likely API-breaking; needs its
  own spec) and refresh test-tooling packages (`Microsoft.NET.Test.Sdk`,
  `xUnit`, `coverlet`). Spec/plan to follow.

---

## Next

### v0.4.0 — Author management

Improving how the CLI works with authors: pagination on listing, a matching
primitive so agents can identify and clean up unmatched authors, and edit /
delete / image primitives so agents can act on what they find.

- **Author pagination support** — `abs-cli authors list` currently uses the
  unpaginated response shape (`{ authors: [...] }`). The endpoint
  `GET /api/libraries/:id/authors` actually supports pagination when both
  `limit` and `page` query params are numeric, switching to
  `{ results, total, limit, page, sortBy, sortDesc, filterBy, minified, include }`.
  Add `--limit`, `--page`, `--sort` (`name` / `lastFirst` / `addedAt` /
  `updatedAt` / `numBooks`), `--desc`, `--filter`. The response-shape change
  requires a separate paged response model alongside the existing one.
- **Author matching** — Expose ABS's author match flow via
  `POST /api/authors/:id/match` (body: `q` name or `asin`, optional `region`,
  default `us`; backed by Audnexus). Match is destructive — on a hit it
  immediately writes `asin`, `imagePath`, and `description` onto the author
  and emits `author_updated`. A 404 leaves the author untouched, which is
  the signal we want for surfacing authors that couldn't be matched
  (e.g. names like `"Joe Bloggs - illustrator"`) so they can be cleaned up.
  Pair it with `GET /api/search/authors?q=<name>` (also Audnexus-backed,
  read-only — no `region`, no ASIN path) as a non-destructive probe.
- **Namespace decision** — Both commands live under `authors`, not
  `metadata`. The CLI is organized by ABS resource (`items`, `libraries`,
  `authors`, ...); `metadata` is reserved for stateless provider discovery
  with no ABS entity attached. Match requires an author ID and mutates
  that author's record — that's an entity operation. Likely shape:
  `abs-cli authors match <id> [--asin <asin>] [--region <r>]` and
  `abs-cli authors lookup --name "..."` for the read-only probe, plus a
  workflow for iterating the library and reporting unmatched authors.
- **Author edit / delete / image** — Once agents can find unmatched
  authors, they need primitives to fix them. ABS exposes:
  `PATCH /api/authors/:id` (editable fields: `name`, `description`,
  `asin`; pass `null` to clear), `DELETE /api/authors/:id` (removes from
  all books and deletes), and image endpoints
  (`POST` / `DELETE` / `GET /api/authors/:id/image`). Likely shape:
  `abs-cli authors update <id> [--name] [--description] [--asin]`,
  `abs-cli authors delete <id>`, and `authors image set|get|remove`
  mirroring `items cover`.

  **Merge-on-rename quirk** — `PATCH /api/authors/:id` has a surprising
  side effect: if you rename an author to a name that already exists in
  the same library, ABS auto-merges them. All books of the source author
  are reassigned to the existing author and the source is deleted. The
  response in this case is `{ author: <existingAuthor>, merged: true }`
  instead of the usual `{ author, updated: bool }`. This is a single
  endpoint serving two operations, and a typo-fix can silently destroy
  an author. The CLI must surface this clearly: detect `merged: true` in
  the response, stderr-warn (or fail-loud unless `--allow-merge` is
  passed), and the help text for `authors update` must call it out so
  agents and users don't get bitten.

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
