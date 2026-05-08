# `authors list` pagination — design

Status: draft 2026-05-08. Targets v0.4.0 (Spec A of three).

## Summary

Switch `abs-cli authors list` from its current unpaginated `{authors: [...]}`
shape to the paginated `{results, total, limit, page}` shape that the
sibling `items list` and `series list` commands already use. Add `--limit`,
`--page`, `--sort`, and `--desc` flags. The change makes "list" verbs
uniform across the CLI and exposes a consistent response shape regardless
of the underlying ABS endpoint's quirks.

This is a **breaking change** in the user-visible response shape and the
default behaviour: previously a single `authors list` call returned every
author; the new default returns the first 50. Users wanting the old "all
in one shot" behaviour pass `--limit 999` (or any sufficiently high value).

## Motivation

`authors list` today is the only top-level `list` verb in the CLI that
returns an unpaginated `{authors: [...]}` response. `items list` and
`series list` both return `PaginatedResponse`. This asymmetry forces
agents to special-case authors output handling, and the lack of pagination
flags means iterating a large library's authors (the user's production
instance has 629) requires either pulling them all at once or going around
the CLI entirely.

The asymmetry comes from the ABS server: `GET /api/libraries/:id/authors`
is the only `list` endpoint that returns two different response shapes
depending on whether `limit` and `page` are both supplied as numeric query
params. Per `LibraryController.js:1022-1101`:

- No `limit`/`page` (or non-numeric) → `{authors: [...]}` (everything,
  no slicing).
- Both `limit` and `page` numeric → `{results, total, limit, page,
  sortBy, sortDesc, filterBy, minified, include}` (sliced).

Items and series do not have this dual-shape — they always return the
paginated shape (`limit=0` means "no slicing", same shape regardless).

The cleanest fix is to make the CLI always send numeric `limit` and
`page`, hiding the dual-shape behaviour from users. The CLI surfaces a
single response shape (`PaginatedResponse`) for every list verb.

## What already works (no changes needed)

| Capability | Mechanism | Verified |
|------------|-----------|----------|
| Get a single author | `abs-cli authors get --id X` | Existing, unchanged. |
| Sort options understood by ABS | `name`, `lastFirst`, `addedAt`, `updatedAt`, `numBooks` | `LibraryController.js:1041-1052`. |
| `numBooks` is post-query sort | ABS pulls all authors, sorts in-memory, slices | `LibraryController.js:1083-1091`. |
| `PaginatedResponse` model and `AppJsonContext` registration | Reuses existing model | `Models/PaginatedResponse.cs:6-19`, `Models/JsonContext.cs:12`. |

## Command surface

```
authors list [--library <id>]
             [--limit <n>]   default 50
             [--page <n>]    default 0  (0-indexed)
             [--sort <field>] default name (one of name|lastFirst|addedAt|updatedAt|numBooks)
             [--desc]
```

- `--limit` matches the items/series convention. CLI default `50`. There
  is no documented hard cap server-side, but the CLI does not enforce
  one either.
- `--page` matches items/series convention. 0-indexed.
- `--sort` defaults to `name` because ABS does not impose a default order
  when `sort` is omitted, leaving users with implementation-defined DB
  insertion order. `name` is the most useful default for human/agent
  consumption.
- `--desc` is a boolean flag matching items/series convention.
- No `--filter`, `--minified`, or `--include` — see "ABS quirks
  excluded" below.

## ABS endpoints touched

| CLI verb | HTTP | Endpoint | Query params sent | Notes |
|---|---|---|---|---|
| `list` | GET | `/api/libraries/:id/authors` | `limit`, `page`, `sort`, `desc` (`1` when `--desc`) | Always numeric `limit`/`page` so ABS returns paginated shape unconditionally. |

Permission: list endpoint is reachable to any authenticated user. No
new permission concerns.

Routes confirmed against `temp/audiobookshelf/server/routers/ApiRouter.js:86`
and the controller at `temp/audiobookshelf/server/controllers/LibraryController.js:1015-1102`.

## ABS quirks excluded

The endpoint accepts `filter`, `minified`, and `include` query params and
echoes them back in the response, but the controller does not actually
use them in the SQL `where` clause or the result shaping. The `where` is
hardcoded to `{ libraryId }` (with permission filtering on the included
book join). Surfacing CLI flags for these would be misleading — agents
would expect filtering/minification that does not happen.

If ABS adds real support upstream, the CLI can add the flags then.

## JSON handling and AOT compatibility

No new types. Existing `PaginatedResponse` is reused.

```csharp
// Models/PaginatedResponse.cs (existing, unchanged)
public class PaginatedResponse
{
    [JsonPropertyName("results")] public List<JsonElement> Results { get; set; } = new();
    [JsonPropertyName("total")]   public int Total { get; set; }
    [JsonPropertyName("limit")]   public int Limit { get; set; }
    [JsonPropertyName("page")]    public int Page { get; set; }
}
```

`Results` is `List<JsonElement>` so any element shape passes through
deserialization. The CLI emits the deserialized payload back through
`ConsoleOutput.WriteJson`; element shape (here, `AuthorItem`) is
preserved.

Help-rendering uses the existing two-arg `AddResponseExample` helper:

```csharp
command.AddResponseExample(typeof(PaginatedResponse), typeof(AuthorItem));
```

This produces a sample paginated response with one `AuthorItem` in
`results` for the `Response shape:` help section, matching the pattern
items/series already use.

### Removed types

- `AuthorListResponse` (in `Models/Author.cs`) is no longer used. Delete
  the class and its `[JsonSerializable]` registration in
  `Models/JsonContext.cs:22`. `AuthorItem` stays — it is still the
  element type for `PaginatedResponse.Results` (and is used directly by
  `authors get`).

## Caveats

No command-level `--help` Notes section is required. The pagination
shape is documented via the standard `Response shape:` example. The
group-level `authors --help` Notes (lifecycle + Audnexus) still apply
unchanged.

The one server-side quirk worth mentioning here as design rationale —
**not** in `--help`, since it is invisible to the client and not
actionable: `numBooks` sort is post-query on the ABS side
(`LibraryController.js:1083-1091`). ABS pulls all authors that match
the libraryId + permission filter, sorts in memory by book count, then
slices for pagination. On small libraries this is unobservable; on
libraries with thousands of authors it is noticeably slower than the
SQL-driven sorts (`name`, `lastFirst`, `addedAt`, `updatedAt`). The
client cannot work around this and the response shape is identical, so
surfacing it in `--help` would only add noise.

## Permissions and errors

Same as the existing `authors list`. Read-access only; no new permission
concerns. 404 returns the standard not-found friendly error from
`AbsApiClient`.

## Breaking change

The response shape changes from `{authors: [...]}` to `{results, total,
limit, page}`. The default behaviour changes from "return all authors"
to "return the first 50".

The release skill (`.claude/skills/release/SKILL.md`) does not currently
auto-detect breaking changes from commit syntax — it only branches
MINOR vs PATCH on the verb (`feat:` → MINOR, etc.). The "Breaking
changes" section of `CHANGELOG.md` is written manually during the
release workflow (see existing v0.2.5 entry for the pattern).

To make the breaking change easy to spot at release time:
- Commit subject uses Conventional Commits `!` syntax:
  `feat!: paginate authors list`
- Commit body includes a `BREAKING CHANGE:` footer per Conventional
  Commits:
  ```
  BREAKING CHANGE: 'authors list' now returns PaginatedResponse
  ({results, total, limit, page}) instead of {authors: [...]}, and
  defaults to --limit 50. Pass --limit 999 (or higher) to restore the
  previous "all authors in one shot" behaviour.
  ```

The release agent picks this up when scanning commits since the last
tag and writes the CHANGELOG entry. The `!` and footer are signal
only — humans reading git log will see the marker even if no automated
tooling acts on it.

The smoke test will be updated to consume the new shape; the existing
`d['authors']` assertions become `d['results']` plus a `len(d['results'])`
total check, plus a paginated round-trip.

## Testing

- **Unit / help-render tests** in `AuthorsCommandTests`:
  - `--limit`, `--page`, `--sort`, `--desc` appear in `authors list --help`.
  - Response shape section shows `"results"`, `"total"`, `"limit"`, `"page"`.
- **Smoke** in `docker/smoke-test.sh`:
  - Default invocation (`authors list`) returns 6 authors in `results`,
    `total: 6`, `limit: 50`, `page: 0`.
  - `authors list --limit 3 --page 0` returns 3 authors, total still 6.
  - `authors list --limit 3 --page 1` returns 3 authors, total still 6,
    no overlap with page 0.
  - `authors list --sort name --desc` returns authors in reverse
    alphabetical order (top-of-list assertion, e.g. first name starts
    with a late letter).

## Files affected

**Modified:**
- `src/AbsCli/Commands/AuthorsCommand.cs` — extend `CreateListCommand`
  with the four new options, update the action to pass them to the
  service, switch
  `AddResponseExample<AuthorListResponse>()` to the two-arg
  `AddResponseExample(typeof(PaginatedResponse), typeof(AuthorItem))`.
  No new Notes section.
- `src/AbsCli/Services/AuthorsService.cs` — update `ListAsync` signature
  to take `(libraryId, limit, page, sort, desc)`; build the query string;
  return `PaginatedResponse`.
- `src/AbsCli/Models/JsonContext.cs` — remove
  `[JsonSerializable(typeof(AuthorListResponse))]`.
- `src/AbsCli/Models/Author.cs` — remove `AuthorListResponse` class.
  Keep `AuthorItem`.
- `tests/AbsCli.Tests/Commands/AuthorsCommandTests.cs` — extend with
  paged-list help tests.
- `docker/smoke-test.sh` — update authors-list assertions to paginated
  shape; add pagination round-trip and `--sort --desc` check.

**Files explicitly NOT modified:**
- `CHANGELOG.md` — release-owned.
- `docs/roadmap.md` — feature-level abstraction; no spec-mapping
  additions.
- Other list commands (`items list`, `series list`) — reference
  precedent only.

## Out of scope

- **Author image management** — separate spec (Spec C, v0.4.0).
- **Real `--filter` support for authors** — needs server-side support
  ABS does not currently provide.
- **`--minified` and `--include` flags** — same as filter.
- **A `--all` convenience flag** — users pass `--limit 999` to get the
  same effect; adding a separate flag doubles the surface for marginal
  ergonomic gain.
