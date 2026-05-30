# Research: `me` + `items progress` + `items get --include`

**Status:** Research notes for v0.6.0. Not yet a spec.

## Goal

Three closely-related additions to the CLI, bundled because they share the
`/api/me/*` family and overlap semantically:

- **`abs-cli me`** — surface the currently authenticated user (id,
  username, type, permissions, etc.). Today the username appears once at
  login and is unrecoverable without decoding the token.
- **`abs-cli items progress get|set|remove` + `items batch-update-progress`**
  — read and write per-user "listened" / "in progress" / ebook-position
  state. Closes the "I cannot tell the CLI a book is already listened"
  gap.
- **`abs-cli items get --include=<flags>`** — opt into ABS's per-item
  decorators (user progress, RSS feed, share state, podcast download
  state). Lets a single `items get` call return the same progress shape
  the dedicated verbs write.

This bundle ships as one PR because the `/api/me/*` family is cohesive
and `items get --include progress` reads what `items progress set` writes
— splitting the work would break the cross-references during review.

## What ABS calls "progress"

A single `MediaProgress` row per `(user, libraryItem)` pair (or
`(user, podcastEpisode)` for podcasts). Each row carries **both audio
and ebook** state on the same record
(`temp/audiobookshelf/server/models/MediaProgress.js:155-176`):

```
{
  id, userId, libraryItemId, episodeId, mediaItemId, mediaItemType,
  duration, progress,           // computed: currentTime / duration
  currentTime, isFinished,       // audio
  ebookLocation, ebookProgress,  // ebook (CFI string + 0..1 number)
  hideFromContinueListening,
  lastUpdate, startedAt, finishedAt
}
```

Consequences:
- One library item with both an audiobook *and* an ebook (the multi-ebook
  fixture in `docker/seed.sh`) shares a single progress row. Fields for
  the unused half are zeroed.
- `DELETE` is whole-record only. To clear only the ebook position while
  preserving audio position, use `PATCH` with `ebookLocation=""` /
  `ebookProgress=0`. The CLI documents this in `progress remove --help`.
- Podcast episode progress lives on the same model with `episodeId` set
  — out of scope for this milestone (see Decisions).

## ABS endpoint surface

### `/api/me`
| Method | Path | Body | Permission |
|---|---|---|---|
| GET | `/api/me` | – | (authenticated) |

Returns `User.toOldJSONForBrowser()`
(`temp/audiobookshelf/server/models/User.js:604-639`):

```
{
  id, username, email, type, token, isOldToken,
  permissions: { canUpdate, canDelete, canUpload, canDownload,
                 accessAllLibraries, accessAllTags, ... },
  librariesAccessible: [...], itemTagsSelected: [...],
  mediaProgress: [...],   // FULL array — can be MB-size on power users
  bookmarks: [...],
  seriesHideFromContinueListening: [...],
  isActive, isLocked, lastSeen, createdAt, hasOpenIDLink
}
```

No server-side query params to slim the response. A `minimal` argument
exists on `toOldJSONForBrowser` (drops `mediaProgress` + `bookmarks`)
but isn't exposed on this endpoint. CLI passes through verbatim.

### Progress endpoints

All in `MeController.js`. Routing at
`temp/audiobookshelf/server/routers/ApiRouter.js:178-181`.

| Method | Path | Body | Permission | Behavior |
|---|---|---|---|---|
| GET | `/api/me/progress/:libraryItemId/:episodeId?` | – | (auth) | Returns one MediaProgress, 404 if none |
| PATCH | `/api/me/progress/:libraryItemId/:episodeId?` | partial fields | (auth) | Upsert (creates or updates), empty 200 |
| DELETE | `/api/me/progress/:progressId` | – | (auth) | Empty 200. **`:progressId` is the MediaProgress row id, NOT libraryItemId.** |
| PATCH | `/api/me/progress/batch/update` | array of payloads | (auth) | Empty 200 even if some entries fail (errors only logged server-side) |

PATCH body fields (`User.js:67-81`):
- `currentTime` (number, seconds)
- `isFinished` (bool)
- `ebookLocation` (string, CFI)
- `ebookProgress` (number, 0..1)
- `hideFromContinueListening` (bool)
- `finishedAt` (number, ms timestamp)
- `lastUpdate` (number, ms; server usually manages)
- `duration` (number; server-derived — don't override)
- `markAsFinishedTimeRemaining` / `markAsFinishedPercentComplete`
  (server-internal — don't expose)

Permission model: **none beyond authenticated**. Every user manages
their own progress. No `canUpdate` / `canDelete` checks.

### `items get --include` (modifies existing endpoint)

`GET /api/items/:id?expanded=1&include=progress,rssfeed,downloads,share`
(`LibraryItemController.js:60-101`).

**Critical:** the `include` parser runs *inside* `if (req.query.expanded == 1)`.
The non-expanded path ignores `include` entirely. The roadmap text
saying "works on both minified and expanded paths" is incorrect against
the source — include requires expanded.

Valid `include` values (comma-separated, case-insensitive after a `.toLowerCase()`):
| Value | Decorates with | Notes |
|---|---|---|
| `progress` | `userMediaProgress` (one MediaProgress for the calling user) | Same shape `items progress get` returns. `null` if no progress recorded. |
| `rssfeed` | `rssFeed` (Feed.toOldJSONMinified or null) | Same shape collections' `--include rssfeed` returns. |
| `share` | `mediaItemShare` (ShareManager lookup) | **Admin + book-only.** Skipped silently otherwise. |
| `downloads` | `episodeDownloadsQueued`, `episodesDownloading` | **Podcast-only.** Skipped silently for books. |

## Decisions

### `abs-cli me`: thin pass-through, no client-side filtering

```
abs-cli me
```

Wraps `GET /api/me`. No flags. Output is the server response verbatim.
Document in `--help` that `mediaProgress` can be MB-size on power users
and that there is no slimming flag (server limitation).

### `items progress get`: by libraryItemId

```
abs-cli items progress get --library-item <lid>
```

Wraps `GET /api/me/progress/:libraryItemId`. 404 → standard "Not found"
exit 2. Returns the one MediaProgress row (both audio + ebook fields).
`--help` notes: "Returns the single progress record covering both audio
and ebook state for this library item."

### `items progress set`: typed flags, partial PATCH

```
abs-cli items progress set --library-item <lid>
                            [--current-time <seconds>]
                            [--is-finished <true|false>]
                            [--ebook-location <cfi>]
                            [--ebook-progress <0..1>]
                            [--hide-from-continue-listening <true|false>]
                            [--finished-at <iso8601>]
```

- `--library-item` required.
- At least one body flag required (rejection mirrors `authors update`).
- Booleans take explicit `true|false` rather than presence — we need
  ternary (`unchanged | true | false`) semantics, and presence-only
  flags can't express "set to false."
- `--finished-at` accepts ISO 8601, CLI converts to ms-since-epoch
  before sending.
- Server returns empty 200. **CLI does a follow-up GET and prints the
  resulting MediaProgress** so the caller sees the post-update state —
  matches the `items update` pattern of echoing the updated item. The
  follow-up GET is hidden behind one verb; same justification as the
  `progress remove` GET-then-DELETE pattern below (server asymmetry
  workaround, not interpretation).

### `items progress remove`: by libraryItemId, internal GET→DELETE

```
abs-cli items progress remove --library-item <lid>
```

Server's `DELETE /api/me/progress/:id` takes the MediaProgress row id,
which is asymmetric with `get`/`set` (which take `libraryItemId`).
Rather than force users to discover the progress id by hand, the CLI
internally:

1. GETs `/api/me/progress/:libraryItemId` to discover the row's `id`.
2. DELETEs `/api/me/progress/:id`.
3. Prints `{"success":"true"}` on 200.

Two HTTP calls behind one verb. This is on the edge of the
`feedback_cli_thin_passthrough` rule — but the rule's purpose is to
avoid hidden interpretation / smart-defaulting, and here the extra call
is purely a server-side parameter-naming asymmetry workaround. The
operation remains atomic from the caller's perspective (no decision
points between the two calls).

`--help` calls out:
- "Removes both audio and ebook progress in one shot — server has no
  per-half delete."
- "To reset only one half, use `items progress set` with empty values
  for the half you want cleared."

### `items batch-update-progress`: `--input`/`--stdin` array passthrough

```
abs-cli items batch-update-progress (--input <file> | --stdin)
```

Body is a JSON array of partial progress payloads, each containing
`libraryItemId` plus any subset of the PATCH fields. Pass-through to
`PATCH /api/me/progress/batch/update`. Prints `{"success":"true"}` on
200.

Sharp edge documented in `--help`: **server returns 200 even when
individual entries fail.** Errors are logged server-side only; the CLI
has no per-entry feedback. Recommendation: pre-validate the array
client-side (well-formed JSON, each entry has `libraryItemId`), and
follow up with `items progress get` for entries the caller cares about
confirming.

### `items get --include`: auto-imply `--expanded`, all four values

```
abs-cli items get --id <id> [--expanded] [--include progress,rssfeed,share,downloads]
```

Decision: passing `--include` automatically sets `expanded=1` on the
wire. The CLI does this transparently — `items get --id X --include progress`
sends `?expanded=1&include=progress` even without `--expanded`. The
non-expanded path silently ignores `include` server-side, and asking
the user to remember the dependency is the wrong default.

`--help` notes:
- "`--include` automatically requests the expanded shape; passing
  `--include` alone is equivalent to passing `--expanded --include`."
- Each value's scope: "`progress` — adds your media progress for this
  item. `rssfeed` — adds the open RSS feed if any. `share` — admin and
  book-only; silently skipped otherwise. `downloads` — podcast-only;
  silently skipped for books."

Comma-separated (matches the wire format ABS parses).

### Episodes / podcasts: out of scope

All progress endpoints accept an optional `:episodeId?` for podcasts.
This milestone is book-only. No `--episode` flag. Reasons:
- No podcasts in the seeded test set; no smoke coverage available.
- "Episodes resource" is in `docs/roadmap.md`'s Deferred features
  table.
- Adding it later is additive — `items progress set --library-item X
  --episode Y` slots in cleanly when podcast support arrives.

`--help` for the progress verbs notes: "Books only. Podcast episode
progress is out of scope for this milestone."

## Proposed command surface

```
me                                               # GET /api/me

items progress get        --library-item <lid>   # GET /api/me/progress/:lid
items progress set        --library-item <lid>   # PATCH /api/me/progress/:lid
                          [body flags]            #   then follow-up GET for echo
items progress remove     --library-item <lid>   # GET → DELETE /api/me/progress/:pid
items batch-update-progress (--input|--stdin)    # PATCH /api/me/progress/batch/update

items get --id <id> [--expanded] [--include <flags>]  # GET /api/items/:id (modified)
```

7 new verbs + 1 modified verb (`items get` gains `--include`).

Permission tags: **none.** All endpoints require only authentication.
No `command.AddPermissionRequired(...)` calls.

## Sharp edges to document in `--help`

Per `feedback_help_documents_caveats`, each lives in the relevant
command's `--help`:

1. **`me --help`**: "Response includes the full `mediaProgress` array
   (potentially MB on power users). Server does not expose a slim
   variant for this endpoint."
2. **`items progress get --help`**: "Returns the single record covering
   both audio and ebook state for this library item. Books only."
3. **`items progress set --help`**: "Booleans require explicit
   `true|false` (omit to leave unchanged). At least one body flag is
   required. Echoes the post-update progress via a follow-up GET; if
   that GET fails (very rare — record just written), the CLI exits 2
   with the GET's error."
4. **`items progress remove --help`**: "Removes both audio and ebook
   progress in one shot — server has no per-half delete. To reset only
   one half, use `items progress set --library-item X --ebook-location
   "" --ebook-progress 0` (or `--current-time 0 --is-finished false`
   for the audio half)."
5. **`items batch-update-progress --help`**: "Server returns 200 even
   when individual entries fail (errors are logged server-side only).
   No granular feedback from the API. Recommend pre-validating the
   array client-side or following up with `items progress get` for
   entries that matter."
6. **`items get --include --help`**: "`--include` requires the expanded
   item shape — passing `--include` automatically implies `--expanded`.
   Values: `progress`, `rssfeed`, `share` (admin + book only),
   `downloads` (podcast only). Conditional values are silently skipped
   when their conditions aren't met."

## Models / DTOs

New model files in `src/AbsCli/Models/`:

- `MediaProgress.cs` — DTO matching
  `MediaProgress.getOldMediaProgress()`:
  ```
  Id, UserId, LibraryItemId, EpisodeId, MediaItemId, MediaItemType,
  Duration, Progress, CurrentTime, IsFinished,
  HideFromContinueListening, EbookLocation, EbookProgress,
  LastUpdate, StartedAt, FinishedAt
  ```
- `UserResponse.cs` already exists from earlier work — check whether it
  matches `toOldJSONForBrowser()`. If not, add a `Me.cs` or extend the
  existing `UserResponse` (TBD during implementation; the existing one
  may be the login response shape and not match exactly).
- `ProgressRequests.cs` — `ProgressUpdateRequest` (partial fields with
  `JsonIgnore(WhenWritingNull)` so omitted fields stay out of the
  wire body).

JsonContext registrations: `MediaProgress`, `ProgressUpdateRequest`,
plus a list type for batch input. The batch endpoint takes a top-level
JSON array, so the CLI forwards the raw JSON string verbatim (no DTO
needed for the batch container — matches collections `reorder` /
`batch-add` pattern).

## Service / endpoint constants

New file: `src/AbsCli/Services/MeService.cs` (covers GET /api/me).
Possibly fold progress methods into a new `ProgressService.cs` or into
`ItemsService.cs` since the progress verbs live under `items
progress`. Lean: separate `ProgressService.cs` for clarity — the
endpoints are `/api/me/progress/*` (not `/api/items/*`), even though
the CLI surface puts them under `items`.

Endpoint constants for `src/AbsCli/Api/ApiEndpoints.cs`:

```
public const string Me = "api/me";
public static string MeProgress(string libraryItemId) => $"api/me/progress/{libraryItemId}";
public static string MeProgressById(string progressId) => $"api/me/progress/{progressId}";
public const string MeProgressBatchUpdate = "api/me/progress/batch/update";
```

(Note: `MeProgress(lid)` and `MeProgressById(pid)` produce the same URL
shape — distinct helpers for readability at the call site since the
semantic differs.)

For `items get --include`, no new endpoint constants — modifies the
existing `ItemsService.GetExpandedAsync` (or adds an `include`
parameter to the existing call).

## Open questions

- **Does `UserResponse.cs` already match `toOldJSONForBrowser()`?**
  Check during implementation. The existing model is named `UserResponse`
  and was presumably built for `LoginResponse.User` or similar. May need
  a separate `Me.cs` if shapes diverge.
- **Should `--include` parse client-side?** If a user passes
  `--include foo,bar` (invalid values), the CLI could either pass
  through (server silently ignores) or reject client-side. Lean:
  pass-through. Server's permissive parsing matches our thin-passthrough
  rule, and over-strict client validation would block valid future
  additions ABS may add.
- **`items progress set --finished-at` accepting both ISO and ms?**
  Lean: ISO only, CLI converts. ms timestamps are awkward to type.
  Document the conversion in `--help`.
- **Should `me` accept a username param for admin-as-other-user lookups?**
  Server has no such variant on `/api/me`. The admin path is
  `/api/users/:id`, a separate resource. Out of scope; `me` is strictly
  "the calling user."
