# `items cover` — design

Status: approved 2026-04-28. Targets v0.3.0.

## Summary

Add three new verbs under `items cover` that wrap the four ABS cover endpoints
the CLI does not currently expose: `set` (POST or PATCH depending on source
mode), `get` (GET, optional file output or stdout binary), `remove` (DELETE).

The new verbs are deliberate primitives, not a magic "fix all covers"
command. Detection (`items list --filter "missing=cover"`) and
provider-search (`metadata covers`) already exist; agents compose
detection → search → set as a multi-step workflow.

## Motivation

The CLI today can search for cover URLs against ABS-configured metadata
providers (`metadata covers`) and can list items missing covers
(`items list --filter "missing=cover"`). It has no way to **apply** a cover
to an item, **download** the current cover bytes, or **remove** a cover.
That gap blocks the agent workflow the user wants:

1. Find books with no cover after import + metadata sweep
2. Search providers for candidate URLs
3. Apply a chosen URL (or local file, or existing on-disk file) to the item
4. Optionally fall back through different providers / search flavours

The CLI's job is to hand the agent each step as a sharp primitive. The
agent's job is to orchestrate the policy ("which provider first?", "what's
a good fallback?", "is this URL good enough?").

## What already works (no changes needed)

These are pre-existing capabilities the agent uses today; the spec lists
them only to make the gap analysis explicit.

| Capability | Mechanism | Verified |
|------------|-----------|----------|
| Find items with no cover (or empty `coverPath`) | `abs-cli items list --filter "missing=cover"` | Server logic at `libraryItemsBookFilters.js:220` matches `coverPath IS NULL OR coverPath = ''`; CLI's `FilterEncoder` accepts `missing` and base64-encodes the value. |
| Inspect a single item's cover state | `abs-cli items get --id X` exposes `media.coverPath` | Existing response model `BookMediaMinified.CoverPath` is `string?`; returns `null` for coverless items; documented in `items get --help` response shape. |
| Search providers for cover URLs | `abs-cli metadata covers --provider X --title T --author A` | Existing command wraps `GET /api/search/covers`; returns `CoverSearchResponse { results: List<string> }`. |

## Command surface

```
items cover set --id <itemId> --url <url>          # ABS downloads from URL
items cover set --id <itemId> --file <localPath>   # CLI uploads a local file
items cover set --id <itemId> --server-path <path> # Point to file already on the ABS server's disk

items cover get --id <itemId> --output <file>      # Save bytes to file, JSON descriptor on stdout
items cover get --id <itemId> --output -           # Stream binary bytes to stdout (nothing else on stdout)
items cover get --id <itemId> --output <...> --raw # Fetch original unprocessed bytes (default lets ABS resize)

items cover remove --id <itemId>                    # Remove the current cover
```

- All three subcommands of `set` map to the same ABS endpoint family but
  use different HTTP verbs and bodies internally. Exactly one of
  `--url`/`--file`/`--server-path` must be supplied; CLI validates and
  errors otherwise before any HTTP call.
- `--output` on `get` is **required** (no implicit default). Value `-`
  means "binary to stdout, nothing else"; any other value is treated as a
  filesystem path.
- `--id` is required on every subcommand.

## ABS endpoints touched

| CLI verb | Endpoint | Body | Notes |
|----------|----------|------|-------|
| `set --url` | `POST /api/items/:id/cover` | `{"url":"<url>"}` (JSON) | Server downloads. Returns `CoverApplyResponse`. |
| `set --file` | `POST /api/items/:id/cover` | multipart, field name `cover` | Returns `CoverApplyResponse`. Supported types come from server's `globals.SupportedImageTypes` (.jpg, .jpeg, .png, .webp at minimum). |
| `set --server-path` | `PATCH /api/items/:id/cover` | `{"cover":"<server-path>"}` (JSON) | Path must exist on ABS server's filesystem and not start with `http:`/`https:` (server validates per `CoverManager.validateCoverPath`). Returns `CoverApplyResponse`. |
| `get` | `GET /api/items/:id/cover[?raw=1]` | — | Returns binary image. `raw=1` returns original; without it ABS may resize/transcode (default webp/jpeg negotiation). |
| `remove` | `DELETE /api/items/:id/cover` | — | Returns HTTP 200 with empty body. |

Routes confirmed against the bundled ABS source at
`temp/audiobookshelf/server/routers/ApiRouter.js:112-115` and controller
methods at
`temp/audiobookshelf/server/controllers/LibraryItemController.js:271-373`.

## Components

### New: `src/AbsCli/Services/CoversService.cs`

Static-instance service following the project's existing service-per-domain
pattern (mirrors `MetadataService`, `ItemsService`). Methods:

```csharp
public Task<CoverApplyResponse> SetByUrlAsync(string itemId, string url);
public Task<CoverApplyResponse> UploadFromFileAsync(string itemId, string localFilePath);
public Task<CoverApplyResponse> LinkExistingAsync(string itemId, string serverPath);
public Task<Stream> GetAsync(string itemId, bool raw);
public Task RemoveAsync(string itemId);
```

`GetAsync` returns a `Stream` that the command handler copies to either a
`FileStream` (for `--output <file>`) or `Console.OpenStandardOutput()`
(for `--output -`). Service does not buffer the full image into memory.

### New: `ItemsCommand.CreateCoverCommand()`

Method added to `src/AbsCli/Commands/ItemsCommand.cs`. Returns a
`Command("cover", "...")` with three subcommands wired in. Registered as a
subcommand of `items` alongside the existing `list`, `get`, `update`, etc.

Per project convention: top-level command help shows all verb subcommands;
each subcommand has its own examples and response-shape sample
(`AddExamples` + `AddResponseExample<T>`).

### Modified: `src/AbsCli/Api/ApiEndpoints.cs`

Add four constants:

```csharp
public const string ItemCover = "/api/items/{0}/cover";              // GET, POST
public const string ItemCoverPatch = "/api/items/{0}/cover";         // PATCH (alias used for clarity at call sites; same path)
```

(Or one constant if duplicating feels noisy. Decide at implementation
time based on local pattern.)

### New: `src/AbsCli/Models/CoverModels.cs`

Three model files / classes (split into one file per related-type group,
or bundled as the project convention dictates):

- `CoverApplyResponse` — typed envelope for `POST` and `PATCH` responses.
  Exact shape confirmed at implementation time by inspecting the ABS
  server response (likely `{ success: bool, cover: string? }` or similar).
  Source-generation registered via `[JsonSerializable]`.
- `CoverApplyByUrlRequest` — `{ Url: string }`. Used to serialise the POST
  body when applying by URL.
- `CoverLinkExistingRequest` — `{ Cover: string }`. Used to serialise the
  PATCH body when linking to an existing server-side path.
- `CoverFileSavedDescriptor` — `{ Path: string, Bytes: long, Format: string }`.
  Returned to stdout after `cover get --output <file>` writes.

All four types registered in `src/AbsCli/Models/JsonContext.cs` so AOT
serialization works (see "AOT considerations" below).

### Modified: `src/AbsCli/Commands/SelfTestCommand.cs`

Add a `=== Cover Models ===` section after the existing model-roundtrip
groups. Round-trip serialise + deserialise each new type via
`AppJsonContext.Default.<TypeName>`. Same pattern as today's 34 checks.
Self-test count goes from 34 to ~38.

## AOT considerations

Native AOT discipline (`PublishAot=true`) imposes hard constraints. The
spec calls these out explicitly to lock the discipline in:

- **No reflection-based JSON.** Every new request/response model is
  registered in `AppJsonContext` via `[JsonSerializable(typeof(T))]`. All
  serialisation goes through `AppJsonContext.Default.<TypeName>`. Calls
  to `JsonSerializer.Serialize<T>(...)` (the reflection-based overload) are
  banned — same as the rest of the codebase.
- **Multipart upload uses `MultipartFormDataContent` + `StreamContent`.**
  These are AOT-safe (pure stream wiring, no reflection). Same primitives
  the existing `upload` command uses for audio files.
- **Binary download streams.** `HttpResponseMessage.Content.ReadAsStreamAsync()`
  →  `stream.CopyToAsync(target)`. No buffering into a `byte[]` (AOT-safe
  but wastes memory for large covers). `target` is a `FileStream` for
  `--output <file>` or `Console.OpenStandardOutput()` for `--output -`.
- **Self-test gates AOT serialization.** Each new model gets a round-trip
  check in `SelfTestCommand` so the existing CI pipeline catches any
  source-gen drift in the published binary.
- **No new packages.** All AOT-friendly primitives (`HttpClient`,
  `MultipartFormDataContent`, source-gen JSON) are already in the project.

## Output formats

Default: every command emits a single JSON document on stdout, matching
the project's JSON-everywhere convention.

Exception: `items cover get --output -` emits raw image bytes on stdout
and writes nothing else there. The user opts into binary mode by passing
`-` as the output target.

| Command | stdout |
|---------|--------|
| `items cover set --url ...` | `CoverApplyResponse` JSON |
| `items cover set --file ...` | `CoverApplyResponse` JSON |
| `items cover set --server-path ...` | `CoverApplyResponse` JSON |
| `items cover get --output cover.jpg` | `CoverFileSavedDescriptor` JSON |
| `items cover get --output -` | binary image bytes (no JSON) |
| `items cover remove` | `{"success": true}` JSON envelope (CLI synthesises this since the server returns an empty 200) |

## Error handling

Standard CLI error patterns; reuse existing helpers:

| Condition | Behavior |
|-----------|----------|
| 403 (user lacks `canUpload` privilege for set/upload, lacks update for set-server-path/remove) | Existing permission-denied formatter prints the user-friendly message; exit 1 |
| 400 (no URL/file in POST, no cover path in PATCH) | Existing 400 formatter; exit 1 |
| 404 (item ID unknown) | "Item not found: `<id>`"; exit 1 |
| 500 (CoverManager validation: path doesn't exist on server, not a file, http:/https: in `--server-path`) | "Server error: `<message>`" (server response message body forwarded); exit 1 |
| Local `--file <path>` does not exist or unreadable | CLI-side check before HTTP call: "File not found: `<path>`"; exit 1 |
| `--output <file>` write fails (disk full, permission denied) | After bytes are fetched: "Failed to write `<path>`: `<reason>`"; exit 1. Partial file may be left on disk; the CLI does not rollback. Mirror the `backup download` pattern. |
| Mutex violation on `set` (zero or two-plus of `--url`/`--file`/`--server-path`) | CLI-side validation before HTTP call: "Specify exactly one of --url, --file, --server-path"; exit 1 |
| Required `--id` missing | System.CommandLine native validation; exit 1 |
| Required `--output` missing on `get` | System.CommandLine native validation; exit 1 |

## Testing

### Unit tests (xUnit)

- **`CoversServiceTests`** — round-trip JSON for `CoverApplyResponse`,
  `CoverApplyByUrlRequest`, `CoverLinkExistingRequest`,
  `CoverFileSavedDescriptor` via `AppJsonContext`. (Stand-up of an
  in-process HTTP fake to verify the service emits the right verb +
  endpoint + body shape is allowed but not required if the smoke suite
  already covers it.)
- **`ItemsCoverCommandTests`** — mutex validation on `set`: zero flags
  set, one flag set, two flags set, all three flags set. Asserts the
  specific error message for the zero/two-plus cases. Does not need an
  HTTP mock; just exercises the parser + validator path.
- **`ItemsCoverGetTests`** — `--output -` vs `--output <file>` routing
  exercised against an injected stream sink (no real network).

### Self-test additions

Round-trip serialise + deserialise each new type. Adds ~4 checks to the
existing 34 — total ~38.

### Smoke test additions

`docker/smoke-test.sh` gains a new `=== Cover Commands ===` section that
exercises the full lifecycle against a live ABS:

1. Pick or upload a test item with no cover (smoke already does an upload
   in the existing Upload section — extend it or use a fresh one).
2. `items cover set --id X --url <fixture-URL>` (could be the test ABS
   itself returning a known image, or a publicly-reachable fixture)
   → assert exit 0; assert response JSON has expected shape.
3. `items get --id X` → assert `media.coverPath` is non-null.
4. `items cover get --id X --output /tmp/cover.bin` → assert the file
   exists with non-zero size; assert stdout JSON descriptor reports
   matching byte count.
5. `items cover get --id X --output -` → pipe to `wc -c` → assert
   non-zero byte count.
6. `items cover remove --id X` → assert exit 0; assert
   `items get` now shows `media.coverPath: null`.
7. (Optional) `items cover set --id X --file /tmp/cover.bin` →
   round-trips the bytes back through upload-from-file. Assert success.

If a publicly-reachable fixture URL is awkward to set up in CI, fall
back to "upload a local fixture file via `--file`" only and skip the
`--url` smoke variant (the unit-level shape test still covers the URL
path).

## Roadmap update

`docs/roadmap.md` v0.3.0 section currently lists "Investigate cover
handling" — that phrasing was misleading. Update during implementation
to: "**`items cover` command** — apply, fetch, and remove book covers
via the ABS API; primitives the agent composes with `items list
--filter "missing=cover"` and `metadata covers`. Spec/plan: ..."

## Files NOT modified

- `CHANGELOG.md` — owned by release workflow.
- `metadata covers` — already exists, unrelated to cover application.
- `items list` filter logic — already supports `missing=cover`.
- `BookMedia` / `BookMediaMinified` models — `CoverPath` is already
  there.

## Out of scope (deferred)

These were considered and explicitly rejected for v0.3.0. Each can be
picked up in a future spec if a real workflow demands it.

- **`get` width/height/format query params** — ABS supports
  `?width=&height=&format=` for resized variants. Adds flag surface no
  current workflow needs.
- **"Try every provider" convenience command** — agent orchestrates this.
- **"Missing cover" shortcut command** — `items list --filter "missing=cover"`
  already works.
- **Cover-URL extraction from `metadata search` general results** — that
  data lives elsewhere; if exposed, it's a `metadata search` output-shape
  question, not cover handling.
- **Format detection on stdout binary** — agent uses libmagic / `file -i`
  externally if needed; CLI does not propagate the response
  `Content-Type` header.
- **Atomic rollback on `--output <file>` write failure** — partial file
  left on disk; agent cleans up. Mirrors `backup download`.

## Open questions

None. All decisions made during brainstorming.
