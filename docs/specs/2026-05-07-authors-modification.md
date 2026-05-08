# `authors` modification (match, lookup, update, delete) — design

Status: draft 2026-05-07. Targets v0.4.0 (Spec B of three).

## Summary

Add four new verbs under `authors` that wrap the ABS endpoints the CLI does
not currently expose: `match` (apply Audnexus author data), `lookup` (read-only
Audnexus probe by name), `update` (PATCH editable fields), and `delete`
(remove the author and unlink from books).

Each command is a thin pass-through to a single ABS API call. No smart
defaults, no client-side response interpretation, no policy decisions.
The CLI surfaces every API quirk explicitly in `--help`. Agents compose
the four primitives plus existing `authors list` / `authors get` to build
workflows like "find unmatched authors" or "clean up author data".

## Motivation

The CLI today can list and inspect authors (`authors list`, `authors get`)
but cannot modify them. The ABS web UI exposes a "Match" button per author
that pulls Audnexus data and applies it; users edit name/description/asin
directly; users can delete authors. None of this is reachable via the CLI,
so agents cannot:

- Identify authors that have never been matched (no ASIN, no description).
- Probe Audnexus for a candidate without writing to the author record.
- Apply Audnexus data to an existing author programmatically.
- Edit author metadata (e.g. strip "- illustrator" suffixes from names).
- Delete authors that are wrong or no longer wanted.

The driving use case is **finding authors that couldn't be matched** so
they can be cleaned up — names like "Joe Bloggs - illustrator" never
match Audnexus, and an agent should be able to enumerate them, probe
upstream, and either rename or delete them. The four verbs in this spec
provide the primitives that workflow needs.

## What already works (no changes needed)

| Capability | Mechanism | Verified |
|------------|-----------|----------|
| List authors in a library | `abs-cli authors list --library X` | Existing, response shape `{authors: [...]}` per `AuthorsService.cs:17`. |
| Inspect a single author | `abs-cli authors get --id X` | Existing, returns `AuthorItem` with `?include=items` per `AuthorsService.cs:22`. |
| Author lifecycle awareness | `authors --help` "Notes" section | Existing per `AuthorsCommand.cs:13-18`. |

## Command surface

```
authors match  --id <id> (--name "..." | --asin <asin>) [--region <r>]
authors lookup --name "..."
authors update --id <id> [--name <v>] [--description <v>] [--asin <v>]
authors delete --id <id>
```

- **`match`** — `--id` required. Exactly one of `--name` / `--asin` required
  (mutually exclusive, CLI rejects empty / both before HTTP). `--region`
  optional, defaults to `us`.
- **`lookup`** — `--name` required. No `--id`, no `--region`, no `--asin`
  (the underlying ABS endpoint accepts none of these).
- **`update`** — `--id` required. At least one of `--name` / `--description`
  / `--asin` required (CLI rejects empty payload before HTTP to avoid the
  400 ABS would otherwise return). Empty string for `--description` or
  `--asin` clears the field server-side (CLI sends JSON `null`). Empty
  `--name` rejected client-side.
- **`delete`** — `--id` required. No flags, no confirmation prompt
  (consistent with `backup delete`, `items cover remove`).

All four require the existing `--id` flag style (matches `authors get`).
None take positional arguments.

## ABS endpoints touched

| CLI verb | HTTP | Endpoint | Body | Notes |
|---|---|---|---|---|
| `match` | POST | `/api/authors/:id/match` | `{q? \| asin?, region?}` | Destructive on hit. Permission: `canUpdate`. |
| `lookup` | GET  | `/api/search/authors?q=<name>` | — | Read-only Audnexus probe. No permission check. |
| `update` | PATCH | `/api/authors/:id` | `{name?, description?, asin?}` (partial; null clears) | Permission: `canUpdate`. May trigger merge — see Caveats. |
| `delete` | DELETE | `/api/authors/:id` | — | Permission: `canDelete`. |

Routes confirmed against the bundled ABS source at
`temp/audiobookshelf/server/routers/ApiRouter.js:212-218` and
`temp/audiobookshelf/server/routers/ApiRouter.js:284`. Controller methods
at `temp/audiobookshelf/server/controllers/AuthorController.js:96-258`
and `temp/audiobookshelf/server/controllers/SearchController.js:165-178`.

## JSON handling and AOT compatibility

Project pattern (per `Models/JsonContext.cs`): every JSON-bridged type
must be `[JsonSerializable]`-registered in `AppJsonContext`, with
`[JsonPropertyName]` annotations on fields. Reads use the source-gen
accessor `AppJsonContext.Default.<TypeName>`. Pass-through without a
schema uses `ConsoleOutput.WriteRawJson` (precedent: `metadata search`).

### New types

```csharp
// Models/AuthorRequests.cs
public class AuthorMatchRequest
{
    [JsonPropertyName("q")]      public string? Q { get; set; }
    [JsonPropertyName("asin")]   public string? Asin { get; set; }
    [JsonPropertyName("region")] public string? Region { get; set; }
}

// Models/AuthorResponses.cs
public class AuthorMatchResponse
{
    [JsonPropertyName("updated")] public bool Updated { get; set; }
    [JsonPropertyName("author")]  public AuthorItem? Author { get; set; }
}

public class AuthorUpdateResponse
{
    [JsonPropertyName("updated")] public bool? Updated { get; set; }
    [JsonPropertyName("merged")]  public bool? Merged { get; set; }
    [JsonPropertyName("author")]  public AuthorItem? Author { get; set; }
}
```

`AuthorMatchRequest` annotates each property with
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` so unset
fields are omitted from the wire body. Match never needs to send `null`
for these fields (it sends either `q` or `asin`, optionally with `region`),
so `WhenWritingNull` is sufficient — no tri-state needed.

`AuthorUpdateResponse` keeps both `updated` and `merged` as nullable
booleans because ABS returns one or the other depending on path:
- Normal update: `{updated: bool, author: {...}}`
- Merge path:    `{merged: true,  author: <existingAuthor>}`

The CLI does not editorialize — it deserializes whichever fields are
present and emits the response as JSON to stdout. Consumers read whichever
boolean is non-null.

### Tri-state for `authors update`

The PATCH endpoint distinguishes "field absent" (no change) from "field
null" (clear). The CLI must express both. STJ source-gen does not have a
native annotation for tri-state on a typed property. Solution: build the
body at runtime as `Dictionary<string, string>` (already registered in
`JsonContext.cs:9`). Nullable annotations are erased at runtime, so the
dictionary accepts null values and STJ emits them as JSON `null`.

```csharp
var body = new Dictionary<string, string>();
if (name        is not null) body["name"]        = name;          // empty rejected upstream of this
if (descSet)                 body["description"] = desc == "" ? null! : desc;
if (asinSet)                 body["asin"]        = asin == "" ? null! : asin;
// JsonSerializer.Serialize(body, AppJsonContext.Default.DictionaryStringString)
// Emits {"description": null, ...} for cleared keys; omits unset keys entirely.
```

Detecting "set vs not set" comes from System.CommandLine: `string?`
options default to `null` when not supplied. Empty string from the user
maps to JSON `null`; non-empty to the literal string; null (not supplied)
to omission. No new dictionary types needed in `JsonContext.cs`.

### Stdout per command

| Command | Stdout | Why |
|---|---|---|
| `match`  | Typed `AuthorMatchResponse` via `ConsoleOutput.WriteJson` | Stable shape from ABS. |
| `lookup` | Raw JSON via `ConsoleOutput.WriteRawJson` | Audnexus payload shape (`{asin, name, description, image}`) plus literal `null` on miss; matches `metadata search` pattern. |
| `update` | Typed `AuthorUpdateResponse` via `ConsoleOutput.WriteJson` | Two-shape response handled by nullable bools. |
| `delete` | `{"success": "true"}` via existing `Dictionary<string, string>` accessor | Empty-body convention from `items cover remove` (`ItemsCommand.cs:369`). |

### JsonContext additions

```csharp
[JsonSerializable(typeof(AuthorMatchRequest))]
[JsonSerializable(typeof(AuthorMatchResponse))]
[JsonSerializable(typeof(AuthorUpdateResponse))]
```

`Dictionary<string, string>` and `AuthorItem` are already registered.

## Caveats (every one of these goes in the relevant `--help`)

### Group-level (`authors --help`, "Notes" section, extending existing)

The existing notes (`AuthorsCommand.cs:13-18`) cover the scanner-driven
lifecycle. Extend them to mention provider behavior:

> Author matching uses the Audnexus provider (audnex.us), the same
> backend the ABS web UI uses. `match` writes ASIN/description/image
> onto the ABS author record; `lookup` is a read-only probe that does
> not touch ABS state.

### `authors match`

- **Destructive on hit.** Writes `asin`, `imagePath`, `description` to
  the author and emits an `author_updated` socket event. Image is only
  written when there is no prior image or the ASIN changed
  (per `AuthorController.js:357`).
- **Single-result reduction.** Audnexus may return multiple candidates
  for a name. ABS picks the closest-Levenshtein match and silently
  discards alternatives (per `Audnexus.js:120-130`). Tie-break is
  array-order from the upstream response — effectively arbitrary. For
  two real-world authors with the same name, the wrong one may be
  written silently. Pass `--asin` to disambiguate.
- **404 means "no upstream match found"** — useful signal when scanning
  for unmatched authors. The author record is untouched on 404.
- **Region defaults to `us`.** ABS defaults this server-side
  (`AuthorController.js:339`); the CLI does not duplicate the default.

### `authors lookup`

- **Read-only.** Calls `GET /api/search/authors?q=<name>`. Does not touch
  any ABS author record.
- **Single-result reduction.** Same as `match`: ABS returns the closest
  Levenshtein match only. The candidate list is not exposed.
- **Returns literal JSON `null` on miss.** ABS responds with HTTP 200
  and a body of `null` when no match is found (per
  `SearchController.js:170` calling `findAuthorByName` which returns
  `null`). The CLI does not convert this to a non-zero exit.
- **No `--region`, no `--asin`.** The underlying endpoint accepts
  neither (per `SearchController.js:167-169`).

### `authors update`

- **Merge-on-rename.** If `--name` changes the author's name to one that
  already exists in the same library, ABS auto-merges them: all books of
  the source author are reassigned to the existing author and the source
  author is deleted (per `AuthorController.js:116-175`). Books are
  reassigned, not lost; the operation is recoverable by re-editing
  books. Response shape becomes `{merged: true, author: <existingAuthor>}`
  instead of `{updated, author}`. Detection happens server-side; the CLI
  cannot pre-flight-check it (TOCTOU race against concurrent author
  creation, and ABS doesn't expose a separate merge endpoint).
- **Merge silently drops other fields.** When the merge path runs, any
  also-supplied `--description` or `--asin` is ignored — only the merge
  happens. The merged-into author keeps its original description and
  asin (per `AuthorController.js:170-174`, the early `return` skips the
  `req.author.set(payload)` block).
- **Empty string clears.** `--description ""` or `--asin ""` sends JSON
  `null` and clears the field server-side. Empty `--name` is rejected
  client-side (every author has a name).
- **All three fields can be updated in one call** in the normal path —
  PATCH accepts a partial payload combining any subset.

### `authors delete`

- **Hard delete.** Removes the author from all books and deletes the
  record (per `AuthorController.js:233-258`). Books lose their author
  tag; the scanner may re-derive the author on its next run if file
  metadata still references the name (per the existing lifecycle note).

## Permissions and errors

| Operation | Required permission | Failure modes |
|---|---|---|
| `match`  | `canUpdate` | 404 (author not found OR no upstream match), 403 (no permission), 400 (invalid ASIN format) |
| `lookup` | none | none expected; null body for "no match" |
| `update` | `canUpdate` | 404 (author not found), 403 (no permission), 400 (empty payload — CLI prevents) |
| `delete` | `canDelete` | 404 (author not found), 403 (no permission) |

403 and 404 messages follow the existing v0.2.0 friendly-error pattern
(`AbsApiClient` + service-level message overrides). No new error
infrastructure required.

## Testing

Match the existing CLI testing layout (`tests/AbsCli.Tests/...`):

- **AOT / source-gen smoke**: each new model round-trips correctly via
  `AppJsonContext.Default.<Type>`. `AuthorUpdateResponse` deserializes
  both shapes (`{updated, author}` and `{merged: true, author}`) without
  loss. `Dictionary<string, string>` with null values serializes to
  `{"key": null}` not omitted.
- **`--help` rendering**: each new subcommand's help renders without
  throwing. `match` and `update` attach response examples via
  `AddResponseExample<AuthorMatchResponse>` /
  `AddResponseExample<AuthorUpdateResponse>` (existing pattern); examples
  match the registered model. `lookup` does not attach a response example
  because the shape is provider-defined; help notes describe it instead.
- **Live integration smoke** (gated like existing external-provider
  tests):
  - `lookup` against a known-good name returns an object; against an
    obvious miss returns null.
  - `match --asin` against an existing author writes the ASIN; against
    an invalid ASIN returns 400.
  - `match --name` against an author whose name has no Audnexus match
    returns 404 (the unmatched-author signal).
  - `update --description` sets the description; `--description ""`
    clears it; the merge-on-rename path works against a test library
    whose authors include a known collision-friendly pair.
  - `delete` against a temporary author succeeds with `{"success":"true"}`.
  - Permission denial against a user without `canUpdate` / `canDelete`
    returns the friendly-error 403 message. The implementation plan
    determines whether existing test users (`uploaduser`, etc.) cover
    these permission cuts or whether a new reduced-permission user is
    needed.

## Files affected

**New**:
- `src/AbsCli/Models/AuthorRequests.cs` — `AuthorMatchRequest`
- `src/AbsCli/Models/AuthorResponses.cs` — `AuthorMatchResponse`, `AuthorUpdateResponse`
- Tests under `tests/AbsCli.Tests/Authors/` mirroring the layout used by `Items`/`Backup`

**Modified**:
- `src/AbsCli/Services/AuthorsService.cs` — add `MatchAsync`, `LookupAsync`, `UpdateAsync`, `DeleteAsync`
- `src/AbsCli/Commands/AuthorsCommand.cs` — add four subcommand factories; extend the group-level "Notes" section with the Audnexus provider note
- `src/AbsCli/Models/JsonContext.cs` — add `[JsonSerializable]` for the three new models
- `src/AbsCli/Api/ApiEndpoints.cs` — add `AuthorMatch(id)` and `SearchAuthors()` paths

## Out of scope

- **Pagination on `authors list`** — separate spec (Spec A, v0.4.0).
- **Author images** (`POST/GET/DELETE /api/authors/:id/image`) — separate
  spec (Spec C, v0.4.0).
- **Higher-level workflow commands** (e.g. `authors find-unmatched`) —
  the four primitives plus existing `authors list`/`get` are sufficient
  for agent composition. Following the items-cover-handling precedent
  of "primitives, not magic".
- **Client-side merge prevention** — the CLI does not pre-flight-check
  for name collisions. Documented in `--help`; agents/users orchestrate.
