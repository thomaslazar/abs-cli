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
- **Investigate cover handling** — How cover metadata upload works end-to-end:
  which endpoints, request shapes, where ABS stores the image, how this
  interacts with `metadata covers` and `items update`. Scoping exercise
  before any command design — outcome may be a `covers` command or a
  documentation update, decided after investigation.
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

## Planned breaking changes

Scheduled for a future minor release with a prior deprecation window.

| Change | Reason |
|--------|--------|
| Remove `abs-cli items search` | Functional duplicate of top-level `abs-cli search` — same endpoint (`/api/libraries/:id/search`), same response shape. Kept as alias through v0.2.x with a note in its help; remove in the next minor bump. |
