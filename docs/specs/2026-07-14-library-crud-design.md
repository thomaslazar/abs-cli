# Library CRUD — Design

Date: 2026-07-14
Status: Approved (brainstorm)

## Goal

Add the four admin library-management endpoints to the existing `libraries`
command: create, update, delete, reorder. Thin 1:1 pass-through.

## API reference (verified against ABS v2.35.1 — LibraryController)

All four require **admin** (`isAdminOrUp`).

| Endpoint | Body | Response | Notes |
|---|---|---|---|
| `POST /api/libraries` | `{ name*, folders* [{fullPath}], mediaType=book, provider=google, icon=database, settings{} }` | created `Library` | Server **creates the folder dirs** if missing (server-side paths). `folders` is a required non-empty array. |
| `PATCH /api/libraries/:id` | `{ name?, provider?, mediaType?, icon?, displayOrder?, settings? }` | updated `Library` | **Folders are NOT updatable here.** |
| `DELETE /api/libraries/:id` | — | the deleted `Library` (`res.json(libraryJson)`) | **Destructive cascade:** deletes the library and all its items, collections, and removes it from playlists + playback sessions. |
| `POST /api/libraries/order` | array of `{ id, newOrder }` | `{ libraries: [...] }` | Reorder by display order. |

## Command layout (precedent-driven)

Chosen to introduce no new patterns. Precedents surveyed:
- Scalar edits → flags (`authors/series/collections update`).
- Body carrying an array → `--input`/`--stdin` (`collections reorder`, `items batch-*`).
- Repeatable path list → repeatable flag (`upload --files`).
- Delete → no confirmation prompt anywhere in the CLI.

### `libraries create` (POST /api/libraries)
- `--name` (required), `--folder <path>` (repeatable via `Option<string[]>{ AllowMultipleArgumentsPerToken = true }`, ≥1 required), `--media-type`, `--provider`, `--icon`.
- Body `{ name, folders:[{fullPath}], mediaType?, provider?, icon? }` — omitted optional flags are not sent (server applies defaults book/google/database).
- **Settings dropped (YAGNI)** — the nested settings object would require a new `--settings-json` pattern; out of scope. Can be added later.
- `--help`: folders are server-side and created if missing; ≥1 required.
- Response: reuse existing `Library` model.

### `libraries update` (PATCH /api/libraries/:id)
- `--id` (required), `--name`, `--media-type`, `--provider`, `--icon`, `--display-order`.
- Empty `--name` rejected client-side; at least one edit flag required (mirrors `authors`/`series`/`collections update`).
- `--help`: folders not editable here.
- Response: `Library`.

### `libraries delete` (DELETE /api/libraries/:id)
- `--id` (required). **Confirmation-gated** (unlike every other delete — the blast radius is far larger). The command first `GET`s the library (a deliberate pre-fetch), prints a warning showing the library's name + id + cascade, and requires the operator to type the library's **exact name** on stdin to proceed. Non-matching input aborts (exit 1). **No `--yes` bypass** — a flag is not a real gate against an agent, whereas requiring the typed name is; in a non-interactive context nothing is typed, so it aborts.
- Response: the deleted `Library`.
- `ConfirmationMatches(input, name)` (trimmed, case-sensitive, null-safe) is extracted for unit testing.

### `libraries reorder` (POST /api/libraries/order)
- `--input <file>` / `--stdin` for the JSON array `[{ "id": "...", "newOrder": N }]` (mirrors `collections reorder`); the raw body is passed straight through.
- Response: reuse existing `LibraryListResponse` (`{ libraries:[...] }`).

## Implementation

- New request models (`src/AbsCli/Models/LibraryRequests.cs`): `LibraryFolderRequest { fullPath }`, `LibraryCreateRequest { name, folders, mediaType?, provider?, icon? }`, `LibraryUpdateRequest { name?, mediaType?, provider?, icon?, displayOrder? }`. Optional fields use `[JsonIgnore(Condition = WhenWritingNull)]` (the context has no global `DefaultIgnoreCondition`). Registered in `JsonContext` and **excluded from the response-example generator** (like the tag/genre/narrator request types). Reorder needs no request model (raw pass-through).
- Responses reuse `Library` / `LibraryListResponse` — no new response models.
- `ApiEndpoints`: `Libraries` and `Library(id)` already exist; add `LibrariesOrder = "api/libraries/order"`.
- Extend `LibrariesService` (`CreateAsync`/`UpdateAsync`/`DeleteAsync`/`ReorderAsync`, hint `"admin permission"`) and `LibrariesCommand` (4 subcommands + logger field).

## Docs
- README Commands table: 4 new `libraries …` rows.
- Coverage doc: mark the four rows ✅ and **fix `POST /api/libraries/order` permission `?` → `admin`**; set the create/update/delete permission columns to `admin` where blank.

## Testing
- Unit: request round-trip tests (folders + omitted-null optionals); `ApiEndpoints.LibrariesOrder`; command help/structure (flags, `admin` tags, caveats, repeatable `--folder`, at-least-one/empty-name on update via `BuildUpdateBodyForTesting`). Note: the existing `Libraries_HasListGetScan` assertion must be updated to the full subcommand set.
- Smoke (self-cleaning): create a throwaway library at a server `/tmp` path → assert id/name; update its name; reorder; delete it → assert the returned deleted-library id, then confirm it's gone from `libraries list`. Plus a 403: `libraries create` as non-admin `testuser`.

## Out of scope (YAGNI)
- Library `settings` object (create/update).
- Folder edits via update (ABS doesn't support).

## Notes
- `DELETE` returns the deleted library JSON (not an empty 200), so the command prints the `Library`.
- Permission hint `"admin permission"` per CLAUDE.md; the pre-existing `ScanAsync` uses the older `"'admin' access"` string — left untouched.
- Do NOT edit `CHANGELOG.md`.
