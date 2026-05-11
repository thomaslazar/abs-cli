# Research: encode-m4b (merge audiobook to single `.m4b`)

**Status:** Research notes for v0.5.0. Not yet a spec.

## Goal

Let agents trigger ABS's built-in "merge audiobook into a single .m4b" task
from the CLI, the same flow the ABS web UI exposes under
**Tools → Encode M4B**.

## ABS endpoints

```
POST   /api/tools/item/:id/encode-m4b   # start
DELETE /api/tools/item/:id/encode-m4b   # cancel a running task
```

Source: `temp/audiobookshelf/server/routers/ApiRouter.js:297-298`,
`temp/audiobookshelf/server/controllers/ToolsController.js:29` (`encodeM4b`)
and `:66` (`cancelM4bEncode`).

### Permission

Admin only. The `ToolsController.middleware` (`ToolsController.js:159`)
enforces `req.user.isAdminOrUp`; non-admins get 403.

### Request

- Body: empty (POST).
- Query params (all optional, passed straight to ffmpeg via the
  `AbMergeEncodeOptions` typedef in `AbMergeManager.js:13`):
  - `codec`
  - `channels`
  - `bitrate`

### Validation (ToolsController.js:29-48)

The endpoint refuses with 400/404 when:
- Item missing or invalid (404)
- Not a book (400 "Invalid library item: not a book")
- No audio tracks (400 "Invalid audiobook: no audio tracks")
- An encode-m4b task is already pending for this item (400)

### Response

`res.sendStatus(200)` — empty body. Progress is delivered via:
- WebSocket events (`track_started`, `track_progress`, `task_progress`,
  `track_finished`) — admin-only emitter.
- The standard `/api/tasks` list — type `encode-m4b`, with
  `data.libraryItemId` matching the item.

### Cancel response

`DELETE` returns 200 on success, 404 if no pending task is found.

## What the task actually does

`AbMergeManager.startAudiobookMerge` (`AbMergeManager.js:57`) creates a Task
and runs in two ffmpeg passes:

1. **Concat pass** — `ffmpegHelpers.mergeAudioFiles` concatenates
   `media.includedAudioFiles` into a temp `.m4b` in the server's
   metadata cache (`<MetadataPath>/cache/items/<itemId>/<basename>.m4b`).
2. **Metadata pass** — `ffmpegHelpers.addCoverAndMetadataToFile` embeds
   the cover image and ffmetadata (chapters, tags) into the temp file.

Then (`AbMergeManager.js:203-244`) the manager:

1. For each `originalTrackPath`: **copies** the first track and
   **moves** the remaining tracks into the cache directory (so they no
   longer exist in the library item's directory).
2. Renames/moves the temp `.m4b` over the first track's path (preserving
   permissions), then moves it to the final name
   (`<audiobookBaseName>.m4b`) in the library item's directory.
3. Removes the ffmetadata helper file from the cache.

**Side effect to note for agents:** after a successful task, the library
item's directory contains only the new `.m4b` (plus any non-audio files
like cover/.opf). The original audio tracks live as a backup in the
server's metadata cache (`<MetadataPath>/cache/items/<itemId>/`) and
are not exposed via any public API. No additional CLI cleanup is needed
on the agent side — ABS handles the library-dir delta itself.

## Proposed CLI shape

Two main options:

### Option A: flat verb on `items` (matches `items scan`)

```
abs-cli items encode-m4b --id <id> \
    [--codec <c>] [--channels <n>] [--bitrate <b>] [--wait]
abs-cli items encode-m4b --id <id> --cancel
```

Pros: only one new command, mirrors the ABS UI verb, low surface.
Cons: `--cancel` as a mode flag is a small unit-test smell.

### Option B: subcommand tree (matches `items cover`)

```
abs-cli items encode-m4b start  --id <id> [--codec ...] [--bitrate ...] [--channels ...] [--wait]
abs-cli items encode-m4b cancel --id <id>
abs-cli items encode-m4b status --id <id>      # could be omitted — `tasks list` covers it
```

Pros: clean subcommand semantics, easy to extend.
Cons: extra typing for the common path.

Leaning **A** for now. Cancel is rare and the `--cancel` flag keeps
the happy-path command terse.

`--wait` would poll `/api/tasks`, filter for `encode-m4b` with the
matching `libraryItemId`, and return when the task transitions to
finished/failed/cancelled (same idea as `upload --wait`).

## Open questions

- Do we need to validate the encoder options client-side, or just pass
  whatever the user types as query params and let ABS reject? Probably
  the latter — keeps the CLI thin.
- Should `--wait` also fail the CLI when the task fails server-side
  (non-zero exit)? Almost certainly yes.
- Naming: `encode-m4b` (ABS verb) or `merge-m4b` (more descriptive of
  what users think they're doing)? The ABS UI says "Encode M4b" so
  matching that is one fewer mental remap.
