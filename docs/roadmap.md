# Roadmap

## v0.2.0 — Upload, Metadata & Backup

Enable AI agents to upload books to ABS, apply metadata from providers, and
create safety-net backups. CLI provides sharp primitives; agents orchestrate.

**Backup** — `abs-cli backup create|list|apply|download|delete|upload` (admin-only).
Safety net before bulk changes. Backups contain the SQLite database + metadata.

**Upload** — `abs-cli upload` with author/series/sequence folder naming.
Supports `--wait` to poll until the item appears in the library after upload.

**Scan** — `abs-cli libraries scan` (full library, async) and `abs-cli items scan`
(single item, sync). Trigger scans after upload to create library items.

**Metadata** — `abs-cli metadata search|providers|covers`. Search ABS-configured
providers (Audible, Google Books, etc.) for book metadata and covers. Agent picks
the match and applies via existing `abs-cli items update`.

**Tasks** — `abs-cli tasks list`. Poll background task status (e.g. scan progress).

**Permission errors** — Improved error messages for 403/400/404/500 across all commands.

**Testing** — New `uploaduser` test user, permission denial smoke tests, external
provider tests gated behind local-only flag.

Full spec: [docs/specs/2026-04-12-v0.2.0-upload-metadata-backup.md](specs/2026-04-12-v0.2.0-upload-metadata-backup.md)

---

## Deferred features

All are additive — nothing in the v1 architecture blocks them.

| Feature | Reason for deferral |
|---------|-------------------|
| Table output (`--table` via Spectre.Console) | JSON-only sufficient for v1; agents don't need tables |
| Episodes resource | No podcast libraries in current use |
| Collections / Playlists resources | Not needed for metadata workflow |
| `items files` / `items progress` | Playback and file management not in scope |
| Auto-update mechanism | Check for new versions, prompt/apply updates from GitHub releases |

## Planned breaking changes

Scheduled for a future minor release with a prior deprecation window.

| Change | Reason |
|--------|--------|
| Remove `abs-cli items search` | Functional duplicate of top-level `abs-cli search` — same endpoint (`/api/libraries/:id/search`), same response shape. Kept as alias through v0.2.x with a note in its help; remove in the next minor bump. |
