# Research: Collections

**Status:** Research notes for v0.6.0. Not yet a spec.

## Goal

Expose ABS's Collections feature through the CLI so agents and users can
create curated, manually-maintained lists of book library items. The ABS
web UI exposes this under each library's "Collections" tab. Today this
sits in `docs/roadmap.md`'s "Deferred features" table; this doc bumps it
into v0.6.0.

## What collections are (and aren't)

A collection is a flat, ordered, manually-curated list of book library
items inside a single library. Membership is stored as a join table
(`CollectionBook(collectionId, bookId, order)` —
`temp/audiobookshelf/server/models/CollectionBook.js`).

- **Library-scoped.** Every collection belongs to exactly one library.
  Cross-library membership is rejected (`CollectionController.js:241-243`,
  "Book in different library").
- **Books only.** Podcasts are filtered out at the library-item lookup
  (`mediaType: 'book'`, e.g. `CollectionController.js:60`).
- **Manual membership, no smart rules.** ABS has no saved-filter or
  auto-membership concept. If you want a collection like "all light
  novels," you enumerate the books, post them at create time, and
  add/remove yourself as the library evolves. Building and resyncing
  that membership is the agent's job, not the CLI's and not ABS's.
- **Can't be empty.** `POST /api/collections` returns 400 unless `books`
  is a non-empty array (`CollectionController.js:48-51`).
- **Order matters.** Each `CollectionBook` row carries an `order` int.
  Create assigns `1..N` by array index, single/batch add appends, single
  remove compacts. There's also a reorder-via-PATCH path (see below).
- **References by libraryItemId.** Everywhere on the wire the books are
  identified by their library item id, *not* the internal book media id —
  including the `:bookId` URL param on `DELETE /:id/book/:bookId`, which
  is a server-side misnomer (server has a `TODO` comment about it,
  `CollectionController.js:264-266`).

## ABS endpoint surface

10 endpoints in `CollectionController.js` plus one library-scoped list in
`LibraryController.js`:

| Method | Path | Body | Permission | Response (success) |
|---|---|---|---|---|
| GET | `/api/libraries/:id/collections` | – | (read) | Paginated `{ results, total, limit, page, sortBy, sortDesc, filterBy, minified, include }` |
| GET | `/api/collections` | – | (read) | `{ collections: [...] }` flat, filtered to libraries the caller can access |
| GET | `/api/collections/:id?include=rssfeed` | – | (read) | Expanded collection; optional `rssFeed` |
| POST | `/api/collections` | `{ libraryId, name, description?, books: [libraryItemId, ...] }` | `update` | Expanded collection |
| PATCH | `/api/collections/:id` | `{ name?, description?, books? }` | `update` | Expanded collection |
| DELETE | `/api/collections/:id` | – | `delete` | `200`, empty body |
| POST | `/api/collections/:id/book` | `{ id: libraryItemId }` | `update` | Expanded collection |
| DELETE | `/api/collections/:id/book/:libraryItemId` | – | `update` | Expanded collection |
| POST | `/api/collections/:id/batch/add` | `{ books: [libraryItemId, ...] }` | `update` | Expanded collection |
| POST | `/api/collections/:id/batch/remove` | `{ books: [libraryItemId, ...] }` | `update` | Expanded collection |

Routing: `temp/audiobookshelf/server/routers/ApiRouter.js:80` (library
collections) and `:146-154` (collection endpoints).

### Permission tags (per project rule)

- `update` — create, edit name/description, reorder, single/batch
  add/remove. Source: `CollectionController.middleware` lines 446-453
  (any `POST`/`PATCH` requires `canUpdate`).
- `delete` — collection delete only. Same middleware block.
- No permission tag for read endpoints (any authenticated user with
  library access).

### Collection JSON shape

`Collection.toOldJSON()` / `toOldJSONExpanded()`
(`temp/audiobookshelf/server/models/Collection.js:260-289`):

```
{
  id, libraryId, name, description,
  books: [LibraryItem | LibraryItemExpanded],
  lastUpdate, createdAt,
  rssFeed?  // only on get/list when include=rssfeed and a feed exists
}
```

The library-scoped list (`/api/libraries/:id/collections`) wraps it as
`{ results: [...], total, limit, page, ... }`, matching the
`abs-cli authors list` paginated shape that v0.4.0 standardised on.

## The PATCH overload — rename, redescribe, reorder

`PATCH /api/collections/:id` accepts three independent fields:

- `name` — rename. If a non-empty string and differs from current, applied
  via `htmlSanitizer.stripAllTags` (so HTML in the name is dropped).
- `description` — redescribe. Any string (including empty) — `null` clears.
- `books` — **reorder existing members only.** Decidedly not add/remove.

The reorder path (`CollectionController.js:170-197`):

1. Load the collection's current `CollectionBook` rows, joined to their
   library items.
2. Sort them by `findIndex` of each book's libraryItemId in the supplied
   array.
3. Renumber `order` to the new positions and `save()`.

Consequences worth documenting:

- **No add.** A libraryItemId in `books` that isn't already in the
  collection is silently ignored (`findIndex` matches no row).
- **No remove.** A current member not present in `books` lands at sort
  position `-1` — shoved to the front in undefined relative order. In
  practice this means you should pass the *full* current membership in
  the new order; partial lists produce surprising shuffles.
- **Idempotent.** If the resulting `order` values match the existing
  ones, nothing is written (`collectionBooksUpdated` stays false).

Rename/redescribe and reorder are the same HTTP call but two distinct
mental models. The CLI splits them (see below).

## Decisions

### List: library-scoped only

Drop the global `GET /api/collections` shape. `abs-cli collections list`
takes the library context from `--library` or the configured default,
matching `series list` / `authors list`. Hitting
`GET /api/libraries/:id/collections` always returns the paginated shape.

Reasoning:
- Consistency with the rest of the CLI (every other `list` already
  requires a library).
- The paginated endpoint supports `sort` / `desc` / `filter` / `include`
  / `minified` query params; the global one doesn't. So the
  library-scoped path is strictly more capable.
- An agent that wants "all my collections across libraries" iterates
  `libraries list` → `collections list --library <id>`. Same pattern as
  the other resources; the global shape's only meaningful difference is
  "no pagination," which is not a feature.

### `update` and `reorder` as separate verbs

The PATCH endpoint conflates rename/redescribe with reorder. Each call
either edits metadata, or reshuffles `order` integers — they don't
interact, and exposing them under one verb would force the help text to
explain "this flag flips behavior between two unrelated things." Two
verbs, both hitting the same PATCH:

| CLI verb | PATCH body |
|---|---|
| `collections update --id <id> [--name <n>] [--description <d>\|""]` | `{ name?, description? }` |
| `collections reorder --id <id> --book <lid> [--book <lid> ...]` | `{ books: [...] }` |

`description ""` clears the field (tri-state per `AuthorsCommand.BuildUpdateBodyForTesting`).
`reorder`'s `--help` spells out the "pass the full current order, this
does not add or remove" rule plus a worked example. This is one of the
few places we'll have two verbs behind one URL; it's worth the slight
break from strict-1:1 because the API itself is overloaded.

### Membership: four separate verbs

Following the existing `items batch-*` pattern and the
`feedback_cli_thin_passthrough` rule, one verb per endpoint:

| CLI verb | Endpoint |
|---|---|
| `collections add` | `POST /api/collections/:id/book` |
| `collections remove` | `DELETE /api/collections/:id/book/:libraryItemId` |
| `collections batch-add` | `POST /api/collections/:id/batch/add` |
| `collections batch-remove` | `POST /api/collections/:id/batch/remove` |

Considered: folding into two verbs that switch endpoint based on
`--book` arg count. Rejected — two API calls behind one verb hides
behaviour. The single and batch endpoints have different error
semantics (batch-add silently skips duplicates, single-add 400s on
duplicate; batch-remove tolerates non-members, single-remove 404s on
missing library item). Surfacing those as distinct verbs keeps the
error semantics honest.

## Proposed command surface

```
collections list   [--library <id>] [--limit] [--page] [--sort] [--desc]
                   [--filter] [--include rssfeed] [--minified]
collections get    --id <collectionId> [--include rssfeed]
collections create --library <id> --name <n> [--description <d>]
                   --book <libraryItemId> [--book <libraryItemId> ...]
collections update --id <id> [--name <n>] [--description <d>|""]
collections reorder --id <id> --book <libraryItemId>
                                 --book <libraryItemId> ...
collections delete --id <id>
collections add    --id <id> --book <libraryItemId>
collections remove --id <id> --book <libraryItemId>
collections batch-add    --id <id> --book <libraryItemId>
                                    --book <libraryItemId> ...
collections batch-remove --id <id> --book <libraryItemId>
                                    --book <libraryItemId> ...
```

10 verbs, 10 endpoints (`list` covers the library-scoped endpoint; the
global `/api/collections` is dropped; `update` and `reorder` split the
PATCH).

Permission tags:
- `create`, `update`, `reorder`, `add`, `remove`, `batch-add`,
  `batch-remove`: `update` (matches server middleware).
- `delete`: `delete`.
- `list`, `get`: untagged (read).

Permission hint strings on the service: `'update' permission` and
`'delete' permission` per the existing convention
(`CLAUDE.md` → "Permission hint mirroring").

## Sharp edges to document in `--help`

Per `feedback_help_documents_caveats`, each of these must live in the
relevant command's `--help`, not just here:

1. **`create` needs at least one book.** Empty `books` array returns 400
   "Invalid collection data. No books". You cannot create an empty
   collection.
2. **`reorder` does not add or remove.** Pass the full current order;
   partial lists produce undefined shuffles. (Worked example in `--help`.)
3. **Single-add 400s on duplicate; batch-add skips silently.** Same data,
   different error semantics — call out in both help blurbs.
4. **Books are libraryItemId everywhere.** Including in `remove`'s URL
   path. The server's `:bookId` parameter name is a known misnomer
   (server-side TODO).
5. **Single-library only.** Adding a book from a different library
   returns 400 "Book in different library". For cross-library curation,
   one collection per library.
6. **HTML in `name` is stripped silently.** `htmlSanitizer.stripAllTags`
   runs server-side at create and update. Worth a `--help` note since
   `<` / `>` characters would otherwise look like they round-trip.
7. **`delete` closes RSS feeds.** Server closes any open RSS feed for
   the collection (`CollectionController.js:217-218`) before destroying
   the row. Mention in `delete --help`.

## Models / DTOs

New model files in `src/AbsCli/Models/`:

- `Collection.cs` — DTO matching `toOldJSONExpanded()`: `Id`, `LibraryId`,
  `Name`, `Description`, `Books` (array of existing
  `LibraryItemExpanded`), `LastUpdate`, `CreatedAt`, optional `RssFeed`.
- `CollectionRequests.cs` — `CollectionCreateRequest`,
  `CollectionUpdateRequest` (tri-state on `Description`),
  `CollectionReorderRequest`, `CollectionBookRequest` (single-add body
  shape `{ id }`), `CollectionBooksRequest` (batch body shape
  `{ books }`).

`PaginatedResponse<Collection>` reuses the existing generic shape; no new
pagination type needed. `JsonContext.cs` gets new `[JsonSerializable]`
entries for the above and for `PaginatedResponse` over `Collection`.

## Service / endpoint constants

New `src/AbsCli/Services/CollectionsService.cs` modeled on
`AuthorsService.cs`. Endpoint constants in
`src/AbsCli/Api/ApiEndpoints.cs`:

```
public const string Collections = "api/collections";
public static string Collection(string id) => $"api/collections/{id}";
public static string CollectionBook(string id) => $"api/collections/{id}/book";
public static string CollectionBookById(string cid, string libraryItemId) => $"api/collections/{cid}/book/{libraryItemId}";
public static string CollectionBatchAdd(string id) => $"api/collections/{id}/batch/add";
public static string CollectionBatchRemove(string id) => $"api/collections/{id}/batch/remove";
public static string LibraryCollections(string libraryId) => $"api/libraries/{libraryId}/collections";
```

## Open questions

- **Expose `--minified` and `--include rssfeed` on `list`?** The endpoint
  supports both. `--minified` drops the expanded library items (sends
  raw ids only) — useful for agents that just want collection metadata.
  `--include rssfeed` decorates rows with an open feed if one exists.
  Lean: yes for both, mirroring `items get --expanded` from v0.5.0.
- **`--stdin` on `create`, `batch-add`, `batch-remove`?** The batch
  endpoints take `{ books: [...] }` — a natural fit for a stdin variant
  reading JSON arrays of libraryItemIds (matches the planned
  `items update --stdin` v0.6.0 work). Lean: yes, scoped under the same
  v0.6.0 entry.
- **`abs-cli collections feed` for the RSS subresource?** ABS also has
  `POST /api/feeds/collection/:id/open` and the standard feed close
  path. Out of scope for this milestone — RSS feeds are their own
  resource family, not collection-specific. Surface them in a future
  feeds-focused milestone if at all.
