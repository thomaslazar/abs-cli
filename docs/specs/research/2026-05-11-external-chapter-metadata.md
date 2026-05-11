# Research: external chapter metadata (Audnexus)

**Status:** Research notes for v0.5.0. Not yet a spec.

## Goal

Let agents fetch authoritative chapter timings for an audiobook from
Audnexus (audnex.us — same backend ABS uses for author data and the
match/lookup author flows in v0.4.0) and write those chapters back
onto an ABS item. Useful for items that have no chapters, items
whose chapters were lost during a previous encode, or items where
the existing chapters drift from the canonical Audible release.

## ABS endpoints

### Lookup (read-only)

```
GET /api/search/chapters?asin=<asin>&region=<r>
```

- `asin` (required) — Audible/Audnexus ASIN. Must pass ABS's
  `isValidASIN` check (`SearchController.js:194`).
- `region` (optional, defaults to `us`) — Audnexus region.

Permissions: standard authenticated user; no admin gate
(`SearchController.findChapters` at `SearchController.js:186`, only
guarded by the global auth middleware).

Response shape (passed straight through from Audnexus —
`Audnexus.getChaptersByASIN` at `Audnexus.js:152`, returns `res.data`):

```json
{
  "asin": "B0XXXXXXX",
  "brandIntroDurationMs": 0,
  "brandOutroDurationMs": 0,
  "chapters": [
    {
      "lengthMs": 12345,
      "startOffsetMs": 0,
      "startOffsetSec": 0,
      "title": "Opening Credits"
    }
  ],
  "isAccurate": true,
  "runtimeLengthMs": 0,
  "runtimeLengthSec": 0
}
```

When Audnexus has nothing for the ASIN, ABS responds with
`{"error":"Chapters not found","stringKey":"MessageChaptersNotFound"}`
(HTTP 200 with an error body — non-conventional but consistent with the
rest of the search controller).

### Write to ABS state

```
POST /api/items/:id/chapters
```

(`temp/audiobookshelf/server/routers/ApiRouter.js:122`,
controller at `LibraryItemController.updateMediaChapters`,
`LibraryItemController.js:850-904`).

Permissions: `req.user.canUpdate` — the standard update permission
(not admin). Same level as `items update`.

Validation:
- Item must not be missing
- Item must be a book
- Item must have audio tracks
- Body must be `{ "chapters": [{ title: string, start: number, end: number }, ...] }`
- Each chapter requires all three fields; missing or wrong type → 400.

**This endpoint does NOT touch the audio file.** What it does:
- Updates `media.chapters` in the ABS database.
- Writes the sidecar metadata file (`metadata.json` / `metadata.opf`)
  next to the book (`LibraryItem.saveMetadataFile`, `LibraryItem.js:616`).
- Emits an `item_updated` socket event.

The audio file's embedded chapter atoms / ID3 frames stay untouched.
Anything that reads chapters directly off the file (a different player,
a future re-import, ffprobe in a script) will still see the old data.

**Units mismatch with the lookup endpoint.** Audnexus returns
`startOffsetMs` / `startOffsetSec` / `lengthMs`. The write endpoint
takes `start` / `end` in **seconds** (numeric). Translating between
the two requires:

```text
chapters[i].start = audnexus[i].startOffsetSec
chapters[i].end   = audnexus[i].startOffsetSec + audnexus[i].lengthMs / 1000
```

Or compute everything from `startOffsetMs` for higher precision.
Either way, the conversion belongs in the agent (or in a CLI
`apply` mode if we ship one) — the lookup endpoint itself returns
the raw Audnexus shape.

### Embed chapters into the audio file (separate endpoint)

```
POST /api/tools/item/:id/embed-metadata?forceEmbedChapters=1&backup=1
```

Source: `temp/audiobookshelf/server/routers/ApiRouter.js:299`,
`ToolsController.embedAudioFileMetadata` (`ToolsController.js:84`),
`AudioMetadataManager.updateMetadataForItem`.

If callers actually want the chapter timings to live inside the audio
file (not just inside ABS's DB + sidecar), they need this follow-up
call. It runs an ffmpeg pass that rewrites the audio files in place
with the cover and the ffmetadata (tags + chapters).

Permissions: **admin only** (same `ToolsController.middleware` as
`encode-m4b`). Note this is a higher bar than the
`POST /api/items/:id/chapters` write itself, which only needs
`canUpdate`.

Query params:
- `forceEmbedChapters=1` — required for multi-file audiobooks.
  AudioMetadataManager only embeds chapters automatically when the
  item has a single audio file (`audioFiles.length == 1`); for
  multi-file books you must opt in via this flag or chapters stay
  out of the embedded ffmetadata.
- `backup=1` — keeps a backup of each audio file in the server
  metadata cache before the in-place rewrite. Recommended for any
  bulk run.

Body: empty (POST). Response: `res.sendStatus(200)` — empty, like
`encode-m4b`. Progress is tracked via the Task system; poll
`/api/tasks` for type `embed-metadata` with `data.libraryItemId`.

A batch variant exists at `POST /api/tools/batch/embed-metadata`
that takes `{ libraryItemIds: [...] }` in the body.

### Two- vs. three-step workflow

So the actual "lookup → apply to audio file" loop is two or three
calls, not one:

1. **Lookup** — `GET /api/search/chapters?asin=...&region=...`
2. **Write to ABS state** — `POST /api/items/:id/chapters` (DB +
   sidecar). Stop here if you only care about ABS-aware clients.
3. **Embed into file** — `POST /api/tools/item/:id/embed-metadata?forceEmbedChapters=1[&backup=1]`,
   then poll `/api/tasks`. Only needed if the audio file itself
   has to carry the chapters.

Step 3 is a destructive, in-place rewrite — `--backup` is cheap
insurance and probably worth defaulting on.

## Proposed CLI shape

Three commands form a natural triple matching `authors` (lookup is
read-only probe, apply is the write):

```
abs-cli items chapters lookup --asin <asin> [--region <r>]
abs-cli items chapters get    --id <id>
abs-cli items chapters set    --id <id> [--input <file> | --stdin]
```

- `lookup` — Calls `GET /api/search/chapters`. Returns the raw
  Audnexus shape so agents can see `isAccurate`, `runtimeLengthSec`
  vs. the item's `media.duration`, etc.
- `get` — Convenience for reading `media.chapters` off the item
  without piping `items get` through `jq`. Optional; could be
  deferred if `items get` is considered sufficient.
- `set` — Writes the chapters array. Input is a JSON file or stdin
  with `{ chapters: [...] }` already in the **write shape**
  (`{ title, start, end }` in seconds). The CLI does not silently
  convert from Audnexus's ms shape — that's the agent's job, and
  documenting it explicitly is safer than guessing which input is
  which.

### Why no auto-apply

A `--apply` mode that does `lookup → translate → set` in one step is
tempting but violates the "thin pass-through" principle. The agent
needs to inspect the lookup result (especially `isAccurate` and
duration alignment) before committing — auto-apply would hide that.

Better: document the two-step pattern in `--help` and a worked
example in the README "agent workflows" section.

## Open questions

- Should `lookup` accept `--title`/`--author` as an alternative input
  and resolve ASIN via the existing metadata search providers? Out of
  scope for the first cut — agents can use `metadata search` to find
  the ASIN, then pass it here. Keeps each command focused.
- Should `set` validate `end > start` and non-overlapping chapters
  before sending? ABS doesn't enforce overlap, but it's a likely
  agent-side bug. Leaning yes for `set` (validate at the CLI boundary
  matches the "fail loud" principle from architecture.md).
- Naming: `items chapters` vs. `items chapter` — match the JSON key
  (`chapters`, plural). The verb tree (lookup / get / set) reuses
  patterns already established by `authors` and `items cover`.
