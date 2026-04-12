# Roadmap

## v0.2.0 — Backup & Restore (safety net for agent operations)

ABS has a full backup API (`/api/backups/*`, admin-only). Wrapping it in the CLI
gives agents a rollback mechanism before making bulk metadata changes.

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli backup create` | `POST /api/backups` | Snapshot current state before changes |
| `abs-cli backup list` | `GET /api/backups` | List available backups |
| `abs-cli backup apply --id <id>` | `GET /api/backups/:id/apply` | Restore from a backup |
| `abs-cli backup download --id <id>` | `GET /api/backups/:id/download` | Download backup file |
| `abs-cli backup delete --id <id>` | `DELETE /api/backups/:id` | Delete a backup |
| `abs-cli backup upload --file <path>` | `POST /api/backups/upload` | Upload a backup file |

Intended agent workflow:
```bash
# Before bulk changes
BACKUP_ID=$(abs-cli backup create | jq -r '.id')

# Agent makes changes...
abs-cli items batch-update --stdin < changes.json

# If something went wrong
abs-cli backup apply --id "$BACKUP_ID"
```

Backups contain the SQLite database + metadata — all library items, metadata,
series, authors, user data. Restore replaces the current state entirely.

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
