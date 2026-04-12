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
| `abs-cli items search --query <text>` | `GET /api/libraries/{id}/search?q=` | Search items in library |
| `abs-cli items update --id <id>` | `PATCH /api/items/{id}/media` | Update single item metadata |
| `abs-cli items batch-update --input file.json` | `PATCH /api/items/batch/update` | Batch update from JSON file |
| `abs-cli items batch-get --input file.json` | `GET /api/items/batch` | Batch get multiple items by ID |

## Libraries

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli libraries list` | `GET /api/libraries` | List all libraries |
| `abs-cli libraries get --id <id>` | `GET /api/libraries/{id}` | Get single library |

## Series

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli series list` | `GET /api/libraries/{id}/series` | List series. Supports `--limit`, `--page` |
| `abs-cli series get --id <id>` | `GET /api/series/{id}` | Get single series with its books |

## Authors

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli authors list` | `GET /api/libraries/{id}/authors` | List authors in library |
| `abs-cli authors get --id <id>` | `GET /api/authors/{id}` | Get single author |

## Search

| Command | ABS Endpoint | Description |
|---------|-------------|-------------|
| `abs-cli search --query <text>` | `GET /api/libraries/{id}/search?q=` | Search library. Returns books, series, authors, tags grouped |

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
| `abs-cli self-test` | Run 14 offline assertions (JSON round-trips, config, filter encoder, token helper) |

Returns exit code 0 on success, 1 on failure. Output goes to stderr.
