# `authors image` (set, get, remove) — design

Status: draft 2026-05-08. Targets v0.4.0 (Spec C of three).

## Summary

Add three new verbs under `authors image` that wrap the three ABS author
image endpoints: `set` (POST a URL for ABS to download), `get` (GET the
image bytes to a file or stdout), `remove` (DELETE the image). Mirrors
the shape of `items cover set/get/remove` minus the modes ABS does not
support.

## Motivation

Spec B added `authors match`, which writes an Audnexus image onto the
ABS author record as a side effect. Spec C exposes the underlying image
endpoints directly so agents can also: replace an author's image with
a specific URL, fetch the bytes for inspection or backup, or remove an
image without going through `authors delete`.

Together with the matching/edit primitives from Spec B and the
pagination from Spec A, this completes the v0.4.0 author-management
surface.

## What already works (no changes needed)

| Capability | Mechanism | Verified |
|------------|-----------|----------|
| `items cover set/get/remove` precedent | `ItemsCommand.cs` cover subtree; `CoversService.cs`. The author image follows the same shape. | Existing v0.3.0 work. |
| Stream-based binary download | `AbsApiClient.GetStreamAsync(string endpoint)` | Existing helper used by `items cover get`. |
| `CoverFileSavedDescriptor { path, bytes }` | Stdout descriptor for "saved to file" mode | Existing; reused by author image get. |
| `AuthorItem` model | The author payload returned in set/remove responses | Existing since v0.2.0. |

## Command surface

```
authors image set    --id <author-id> --url <https-url>
authors image get    --id <author-id> --output <file|->  [--raw]
authors image remove --id <author-id>
```

- **`set`**: `--id` and `--url` both required. ABS validates that the
  URL starts with `http:` or `https:` and rejects anything else with
  HTTP 400. Server downloads the URL synchronously.
- **`get`**: `--id` and `--output` both required. `--output <file>`
  writes bytes to the file and prints `{path, bytes}` on stdout.
  `--output -` streams binary to stdout (no other stdout output).
  `--raw` requests the original stored file; without it ABS returns a
  resized/transcoded image (default webp/jpeg negotiation).
- **`remove`**: `--id` required. No flags. Returns the updated
  `AuthorItem` (with `imagePath: null`).

The CLI does **not** expose `--file` (multipart upload) or
`--server-path` because the ABS author image endpoint does not support
those modes. It does **not** expose `--width`, `--height`, `--format`
on `get` even though ABS accepts them, matching the `items cover get`
precedent of "raw or default".

## ABS endpoints touched

| CLI verb | HTTP | Endpoint | Body / query | Notes |
|---|---|---|---|---|
| `set`    | POST   | `/api/authors/:id/image` | `{"url":"<url>"}` (JSON) | Permission: `canUpload`. URL must be http/https. |
| `get`    | GET    | `/api/authors/:id/image[?raw=1]` | — | No special permission. Without `?raw=1` ABS resizes. |
| `remove` | DELETE | `/api/authors/:id/image` | — | Permission: `canDelete`. Returns HTTP 400 if author already has no image. |

Routes confirmed against `temp/audiobookshelf/server/routers/ApiRouter.js:216-218`
and `temp/audiobookshelf/server/controllers/AuthorController.js:267-329, 391-419`.

## JSON handling and AOT compatibility

### New types

```csharp
// Models/AuthorRequests.cs (existing file from Spec B — append)
public class AuthorImageRequest
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}

// Models/AuthorImageResponse.cs (new file)
public class AuthorImageResponse
{
    [JsonPropertyName("author")]
    public AuthorItem? Author { get; set; }
}
```

`AuthorImageResponse` is the wrapper ABS returns for both `set` and
`remove`: `{author: <AuthorItem>}`. A new typed wrapper is preferred
over reusing `AuthorMatchResponse` (which has `{updated, author}`) or
`AuthorUpdateResponse` (`{updated?, merged?, author}`) because their
field names misrepresent the actual shape.

`get` returns binary bytes — no model. `CoverFileSavedDescriptor` from
v0.3.0 is reused for the `--output <file>` stdout descriptor; same shape,
same purpose.

### JsonContext additions

```csharp
[JsonSerializable(typeof(AuthorImageRequest))]
[JsonSerializable(typeof(AuthorImageResponse))]
```

`CoverFileSavedDescriptor` is already registered.

### ApiEndpoints addition

```csharp
public static string AuthorImage(string id) => $"/api/authors/{id}/image";
```

Placed alongside the existing `AuthorById`, `AuthorMatch` helpers.

### Stdout per command

| Command | Stdout | Why |
|---|---|---|
| `set`    | Typed `AuthorImageResponse` via `ConsoleOutput.WriteJson` | Stable shape from ABS. |
| `get` (`--output <file>`) | `CoverFileSavedDescriptor` (bytes saved + path) via `ConsoleOutput.WriteJson` | Reuse the items-cover descriptor. |
| `get` (`--output -`) | Binary bytes (no JSON, nothing else on stdout) | Pipe-friendly. |
| `remove` | Typed `AuthorImageResponse` via `ConsoleOutput.WriteJson` | Same stable shape. |

## Caveats (every one of these goes in the relevant `--help`)

### `authors image set`

No Notes section needed. The required `--url` option and its description
already convey what the command does. ABS validates the URL format and
returns 400 on bad input — the standard friendly-error path covers that.

### `authors image get`

No Notes section needed. `--raw` and `--output` semantics are
self-explanatory in the option descriptions.

### `authors image remove`

```
No current image → exit 2, stderr "Bad request. Author has no image
path set". Check imagePath via 'authors get' first if needed.
```

Tight pattern matches the `authors match` 404 documentation in Spec B.

## Permissions and errors

| Operation | Permission | Failure modes |
|---|---|---|
| `set`    | `canUpload` | 400 (URL not http/https or ABS download failed), 403 (no permission), 404 (author not found) |
| `get`    | none        | 404 (author not found, or author has no imagePath, or file missing on disk) |
| `remove` | `canDelete` | 400 (author has no current image), 403 (no permission), 404 (author not found) |

403 and 404 messages follow the existing v0.2.0 friendly-error pattern
(`AbsApiClient` + service-level `permissionHint` strings). 400 surfaces
as `Bad request. <body>` per `AbsApiClient.cs:285`.

## Testing

- **Unit / help-render tests** in a new `AuthorsImageCommandTests`:
  - `--id` and `--url` appear in `authors image set --help`; `--file`
    and `--server-path` do NOT (ABS doesn't support them).
  - `--id`, `--output`, `--raw` appear in `authors image get --help`.
  - `--id` only in `authors image remove --help`; `--url` does NOT.
  - `Response shape:` section on `set` and `remove` shows
    `"author"` with an embedded `AuthorItem`.
- **Self-test round-trip** in `SelfTestCommand.cs`:
  - `AuthorImageRequest` round-trips with a sample URL.
  - `AuthorImageResponse` round-trips with an embedded `AuthorItem`.
- **Smoke** in `docker/smoke-test.sh`, extending the Authors section:
  - `set` with a known small public PNG URL (e.g. an audnex.us image
    URL or a stable test image) — assert response has `author` and
    that `author.imagePath` is non-null.
  - `get --output <tmpfile>` — assert tmpfile exists and is non-empty.
  - `get --output -` — assert binary bytes appear on stdout (count).
  - `get --raw --output <tmpfile>` — assert tmpfile exists and is
    non-empty.
  - `remove` — assert response has `author` with `imagePath: null`.
  - `remove` again immediately after — assert exit 2 with the 400
    stderr message (the documented quirk).

The smoke must stay idempotent: each run leaves the test author with
no image, so the second `set → get → remove` cycle works the same.
The `set` URL must be reliably available in CI (so a URL on the dev
ABS instance itself, or a permissionless public test image — picked
during implementation).

## Files affected

**New:**
- `src/AbsCli/Models/AuthorImageResponse.cs` — single-class response.
- `tests/AbsCli.Tests/Commands/AuthorsImageCommandTests.cs` — help-render
  tests for the three subcommands.

**Modified:**
- `src/AbsCli/Models/AuthorRequests.cs` — append `AuthorImageRequest`.
- `src/AbsCli/Models/JsonContext.cs` — register both new types.
- `src/AbsCli/Api/ApiEndpoints.cs` — add `AuthorImage(string id)`.
- `src/AbsCli/Services/AuthorsService.cs` — add `SetImageAsync`,
  `GetImageStreamAsync`, `RemoveImageAsync`.
- `src/AbsCli/Commands/AuthorsCommand.cs` — register a new `image`
  subcommand group with `set`/`get`/`remove` factories.
- `src/AbsCli/Commands/SelfTestCommand.cs` — round-trip checks for the
  two new types.
- `docker/smoke-test.sh` — extend Authors section with the
  set/get/remove cycle described above.

**Files explicitly NOT modified:**
- `CHANGELOG.md` — release-owned.
- `docs/roadmap.md` — feature-level abstraction; no spec-mapping
  additions.

## Out of scope

- **Multipart file upload** (`--file`) — ABS does not support this for
  author images. Adding a flag that always errors would be anti-help.
- **`--server-path` mode** — same reason.
- **`--width`, `--height`, `--format`** on `get` — ABS supports them,
  but `items cover get` doesn't expose them either, and there is no
  concrete use case from the v0.4.0 motivating workflow. Easy to add
  later if needed.
- **Bulk image operations** — agents compose `authors list` →
  `authors image set` per author, same primitives-only philosophy as
  the rest of v0.4.0.
