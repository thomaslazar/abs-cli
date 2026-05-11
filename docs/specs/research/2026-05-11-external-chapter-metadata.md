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

### Write back to item

```
POST /api/items/:id/chapters
```

(`temp/audiobookshelf/server/routers/ApiRouter.js:122`,
controller at `LibraryItemController.updateMediaChapters`).

Permissions: `req.user.canUpdate` — the standard update permission
(not admin). Same level as `items update`.

Validation (`LibraryItemController.js`):
- Item must not be missing
- Item must be a book
- Item must have audio tracks
- Body must be `{ "chapters": [{ title: string, start: number, end: number }, ...] }`
- Each chapter requires all three fields; missing or wrong type → 400.

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
