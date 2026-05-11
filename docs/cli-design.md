# CLI Design

## Command Pattern

```
abs-cli <resource> <action> [options]
```

All commands follow the same structure: a resource noun followed by an action verb.

## Items

The core resource. Maps to ABS `library-item` concept. Covers audiobooks, podcasts,
and any future media types.

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli items list` | `GET /api/libraries/{id}/items` | List items. Supports `--filter`, `--sort`, `--desc`, `--limit`, `--page` |
| `abs-cli items get --id <id>` | `GET /api/items/{id}` | Get single item with full metadata |
| `abs-cli items update --id <id>` | `PATCH /api/items/{id}/media` | Update single item metadata |
| `abs-cli items batch-update --input file.json` | `PATCH /api/items/batch/update` | Batch update from JSON file |
| `abs-cli items batch-get --input file.json` | `POST /api/items/batch/get` | Batch get multiple items by ID |
| `abs-cli items scan --id <id>` | `POST /api/items/{id}/scan` | Scan a single item (admin, sync) |
| `abs-cli items cover set --id <id> [--url \| --file \| --server-path]` | `POST/PATCH /api/items/{id}/cover` | Apply a cover image |
| `abs-cli items cover get --id <id> --output <path>` | `GET /api/items/{id}/cover` | Download the cover image |
| `abs-cli items cover remove --id <id>` | `DELETE /api/items/{id}/cover` | Remove the cover |

## Libraries

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli libraries list` | `GET /api/libraries` | List all libraries |
| `abs-cli libraries get --id <id>` | `GET /api/libraries/{id}` | Get single library |
| `abs-cli libraries scan [--force]` | `POST /api/libraries/{id}/scan` | Trigger a library scan (admin, async) |

## Series

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli series list` | `GET /api/libraries/{id}/series` | List series. Supports `--limit`, `--page` |
| `abs-cli series get --id <id>` | `GET /api/series/{id}` | Get single series with its books |

## Authors

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli authors list` | `GET /api/libraries/{id}/authors` | List authors. Paginated with `--limit`, `--page`, `--sort`, `--desc` |
| `abs-cli authors get --id <id>` | `GET /api/authors/{id}` | Get single author |
| `abs-cli authors match --id <id>` | `POST /api/authors/{id}/match` | Apply Audnexus data to an existing author (destructive — writes asin/imagePath/description) |
| `abs-cli authors lookup --name <text>` | `GET /api/search/authors?q=` | Read-only Audnexus probe by name |
| `abs-cli authors update --id <id>` | `PATCH /api/authors/{id}` | Edit name / description / asin. Surfaces ABS's auto-merge-on-rename |
| `abs-cli authors delete --id <id>` | `DELETE /api/authors/{id}` | Delete author and unlink from all books |
| `abs-cli authors image set --id <id> --url <url>` | `POST /api/authors/{id}/image` | Apply an author image (URL only — ABS downloads) |
| `abs-cli authors image get --id <id> --output <path>` | `GET /api/authors/{id}/image` | Download the author image |
| `abs-cli authors image remove --id <id>` | `DELETE /api/authors/{id}/image` | Remove the author image |

## Search

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli search --query <text>` | `GET /api/libraries/{id}/search?q=` | Search library. Returns books, series, authors, narrators, tags, genres grouped |

## Backup

Admin-only. Server-side backup files include the SQLite DB and optionally the metadata directory.

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli backup create` | `POST /api/backups` | Create a server backup |
| `abs-cli backup list` | `GET /api/backups` | List available backups |
| `abs-cli backup apply --id <id>` | `POST /api/backups/{id}/apply` | Restore from a backup |
| `abs-cli backup download --id <id> --output <path>` | `GET /api/backups/{id}/download` | Download a backup file |
| `abs-cli backup delete --id <id>` | `DELETE /api/backups/{id}` | Delete a backup |
| `abs-cli backup upload --file <path>` | `POST /api/backups/upload` | Upload a backup file |

## Upload

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli upload` | `POST /api/upload` | Upload audiobook / ebook files with author/series/sequence naming. Supports `--wait`, `--prefix-source-dir`, `--files-manifest` |

## Metadata providers

Stateless provider discovery — no ABS entity attached. Lookups go to the ABS-configured providers (Audible, Google Books, etc.) and return raw results for the agent to pick from.

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli metadata search` | `GET /api/search/books` | Search a provider for book metadata |
| `abs-cli metadata providers` | `GET /api/search/providers` | List available metadata providers |
| `abs-cli metadata covers` | `GET /api/search/covers` | Search for cover images |

## Tasks

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli tasks list` | `GET /api/tasks` | List active and recent tasks (poll background work) |

## Changelog

Local — reads the bundled `CHANGELOG.md` embedded in the AOT binary. No network needed.

| Command | Description |
|---------|-------------|
| `abs-cli changelog` | Print the most recent release entry |
| `abs-cli changelog --all` | Print the full file |

## Filtering

Mirrors the ABS API filter system. The API uses `filter=group.base64(value)` encoding.
The CLI handles encoding transparently.

Available filter groups (from ABS source):
- `authors` — by author ID (base64-encoded)
- `genres` — by genre name
- `tags` — by tag name
- `series` — by series ID (base64-encoded)
- `narrators` — by narrator name
- `languages` — by language
- `progress` — by listening status
- `issues` — items with metadata/file problems

```bash
abs-cli items list --filter "genres=Sci Fi"        # CLI encodes to genres.U2NpIEZp
abs-cli items list --filter "languages=English"
abs-cli items list --filter "languages="            # items with no language
abs-cli items list --sort "media.metadata.title" --desc
```

## Help Text

Every command includes comprehensive help with copy-paste examples:

```
$ abs-cli items list --help

Description:
  List library items with optional filtering, sorting, and pagination.

Usage:
  abs-cli items list [options]

Options:
  --library <id|name>    Library to list from (uses default if configured)
  --filter <expression>  Filter items (e.g. "genres=Sci Fi", "languages=English")
  --sort <field>         Sort by field path (e.g. "media.metadata.title")
  --desc                 Sort descending
  --limit <n>            Results per page (default: all)
  --page <n>             Page number, 0-indexed (default: 0)

Examples:
  # List all items in default library
  abs-cli items list

  # List English audiobooks sorted by title
  abs-cli items list --filter "languages=English" --sort "media.metadata.title"

  # List items with no language set (for metadata cleanup)
  abs-cli items list --filter "languages="

  # Page through results
  abs-cli items list --limit 50 --page 2

  # Pipe to jq for further filtering
  abs-cli items list | jq '.results[] | select(.media.metadata.isbn == null)'
```

## Self-Test

A built-in AOT integrity check that exercises all serialization paths without
network access. Used by CI to validate every platform binary.

| Command | Description |
|---------|-------------|
| `abs-cli self-test` | Run 45 offline assertions (JSON round-trips, config, filter encoder, token helper, embedded CHANGELOG) |

Returns exit code 0 on success, 1 on failure. Output goes to stderr.
