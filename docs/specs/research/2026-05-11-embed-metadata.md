# Research: embed ABS metadata into the audio files

**Status:** Research notes for v0.5.0. Not yet a spec.

## Goal

Let agents and users push ABS's current metadata (tags, cover, chapters)
into the audio files themselves. Without this step, all the existing
write commands in the CLI (`items update`, `items chapters set`, the
`authors *` writes, etc.) only persist to ABS's database and sidecar
metadata file — the audio files on disk stay untouched.

## The implicit-DB-only gotcha

Every ABS metadata write endpoint we currently wrap or plan to wrap
operates on ABS state, not on the audio files:

| CLI command (existing or planned) | What it writes | Audio file touched? |
|---|---|---|
| `items update` | DB + `saveMetadataFile()` sidecar | No |
| `items chapters set` (planned v0.5.0) | DB + sidecar | No |
| `authors update` / `authors match` | DB only | N/A (no audio file) |
| `items cover set` | DB + cover image on disk | No |

`POST /api/tools/item/:id/embed-metadata` is ABS's way of bridging the
gap — read the current ABS state and ffmpeg-rewrite the audio files in
place with that state baked in. Useful when:

- Another player (or a future re-import) will read the files outside of
  ABS and expects the tags to be correct.
- You've just done a bulk metadata cleanup and want the changes to
  survive a re-scan from scratch.
- You're about to move a library off ABS and want the files to carry
  their own metadata.

## ABS endpoints

```
POST /api/tools/item/:id/embed-metadata        # single item
POST /api/tools/batch/embed-metadata           # batch
```

Source: `temp/audiobookshelf/server/routers/ApiRouter.js:299-300`,
controller at `ToolsController.embedAudioFileMetadata`
(`ToolsController.js:84`) and `ToolsController.batchEmbedMetadata`
(`ToolsController.js:112`).

### Permission

Admin only (`ToolsController.middleware` enforces `isAdminOrUp`). Same
bar as `encode-m4b`. Note this is **higher** than the
`POST /api/items/:id/chapters` write itself (only `canUpdate`), which
is why agents using only standard-update permissions can change
chapters in ABS state but can't push them into the file.

### Validation (single)

- Item must exist and not be missing (404 / 400 otherwise).
- Item must be a book.
- Item must have audio tracks.
- Item must not already be queued or processing (400 "Library item is
  already in queue or processing").

### Request

- Body: empty (POST).
- Query params:
  - `forceEmbedChapters=1` — required to embed chapters when the item
    has more than one audio file. `AudioMetadataManager.updateMetadataForItem`
    only auto-includes chapters when `audioFiles.length == 1`; multi-file
    items get tags + cover only unless this flag is set.
  - `backup=1` — keeps a copy of each audio file in the server metadata
    cache before the in-place rewrite. Cheap insurance; probably worth
    defaulting on.

### Batch request

```
POST /api/tools/batch/embed-metadata
Content-Type: application/json

{ "libraryItemIds": ["...", "..."] }
```

Per-item validation is the same as the single endpoint and runs
upfront: any one item failing causes the whole batch to abort with a
400/403/404 (`ToolsController.js:119-141`). Same query params apply.

### Response

Both endpoints return `res.sendStatus(200)` — empty body. Progress is
tracked via the standard Task system; poll `/api/tasks` for tasks with
type related to embed-metadata and `data.libraryItemId` matching the
target.

## What the task actually does

`AudioMetadataManager.updateMetadataForItem` builds a Task whose data
includes a per-audio-file plan: each file is rewritten in place via
ffmpeg with the cover, the ffmetadata object (tags + per-file or
whole-book chapters depending on `forceEmbedChapters`), and the
detected `mimeType`. Files are processed sequentially; if `backup=1`
each original is copied into `<MetadataPath>/cache/items/<id>/<filename>`
first.

For a single-file `.m4b` item, this is conceptually a no-op idempotent
"flush ABS state to the file" pass. For a multi-file MP3 book, this
writes whole-book chapter atoms into every file when
`forceEmbedChapters=1` — every track ends up with a copy of the full
chapter list (so a player that picks any track sees the same
navigation).

## Proposed CLI shape

Single-item:

```
abs-cli items embed-metadata --id <id> [--force-embed-chapters] [--backup] [--wait]
```

Batch:

```
abs-cli items batch-embed-metadata --input <file>     # JSON: { "libraryItemIds": [...] }
abs-cli items batch-embed-metadata --stdin            # same shape on stdin
```

Or a unified verb that takes either:

```
abs-cli items embed-metadata --id <id> [...]
abs-cli items embed-metadata --input <file> [...]    # batch when --input given
```

Matches the `items batch-update` / `items batch-get` precedent we
already use. Probably leaning toward two distinct verbs
(`embed-metadata` / `batch-embed-metadata`) since the request bodies
differ enough that conflating them would be confusing.

`--wait` would poll `/api/tasks` until the matching embed task(s)
complete (or any of them fail). Same pattern as `upload --wait` and
the proposed `items encode-m4b --wait`.

### Default for `--backup`

The `backup=1` flag is a real safety net — the embed pass is a
destructive in-place rewrite. The ABS UI defaults this on for the
Tools menu action. The CLI should match: **default `--backup` to
true**, expose a `--no-backup` flag for callers who deliberately want
to skip it (CI runs against throwaway data, bulk jobs where the cache
disk is tight, etc.).

## Open questions

- Should we expose a `tools` subcommand tree (`abs-cli items tools
  encode-m4b` / `abs-cli items tools embed-metadata`) to mirror the
  REST URL structure, or keep both at the `items` flat verb level?
  Leaning flat for now — `items` is already a busy namespace, and a
  `tools` sublevel would be unique to two commands.
- Naming: `embed-metadata` (matches the endpoint) vs. `embed`
  (terser). Lean toward `embed-metadata` for 1:1 with the REST path.
- Should `--force-embed-chapters` default on for multi-file items?
  No — surprise destructive behaviour. Let the user opt in.
- Should the batch endpoint share infrastructure with `items
  batch-update` / `items batch-get` (e.g. accept the same input
  shape)? Probably not — the batch payload is `{ libraryItemIds: [...] }`
  here, vs. the per-item update bodies for batch-update. Different
  shapes warrant different inputs.
