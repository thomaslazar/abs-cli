# Tags & Genres Management — Design

Date: 2026-07-10
Status: Approved (brainstorm)

## Goal

Add CLI coverage for Audiobookshelf's tag and genre management endpoints:
list, rename (merge), and delete, for both tags and genres. Thin 1:1
pass-through — no smart defaults, no response interpretation.

## API reference (verified against ABS `MiscController.js`, v2.35.1)

All six endpoints are gated behind `isAdminOrUp` — **admin required**, including
the two list endpoints.

| Verb | Endpoint | Body / param | Response |
|---|---|---|---|
| list | `GET /api/tags` | — | `{ tags: string[] }` (server-sorted, case-insensitive) |
| rename | `POST /api/tags/rename` | `{ tag, newTag }` | `{ tagMerged: bool, numItemsUpdated: int }` |
| delete | `DELETE /api/tags/:tag` | `:tag` base64-encoded | `{ numItemsUpdated: int }` |
| list | `GET /api/genres` | — | `{ genres: string[] }` (**not** sorted) |
| rename | `POST /api/genres/rename` | `{ genre, newGenre }` | `{ genreMerged: bool, numItemsUpdated: int }` |
| delete | `DELETE /api/genres/:genre` | `:genre` base64-encoded | `{ numItemsUpdated: int }` |

Notes from the controller:
- **Base64 path param**: delete decodes `Buffer.from(decodeURIComponent(param), 'base64')`.
  The service must base64-encode the value, then URI-encode it into the path.
- **Merge-on-rename**: if the target already exists, items are merged onto it and
  the response reports `tagMerged`/`genreMerged: true`.
- **Sorting asymmetry**: `tags` is sorted server-side; `genres` is returned in
  discovery order. We pass both through verbatim (no client-side sort).

## Command structure

Two top-level commands mirroring the one-resource-per-verb pattern
(`authors`, `series`, `collections`). Rename/delete use **positional arguments**,
consistent with `config set <key> <value>` — avoids the `tags rename --tag`
stutter.

```
abs-cli tags list
abs-cli tags rename <old-tag> <new-tag>
abs-cli tags delete <tag>

abs-cli genres list
abs-cli genres rename <old-genre> <new-genre>
abs-cli genres delete <genre>
```

Every subcommand (including the two `list`s) calls
`command.AddPermissionRequired("admin")`. Write calls pass the permission hint
`"admin permission"` (no quotes — `admin` is a user type, not a flag key).

## Files

New:
- `src/AbsCli/Commands/TagsCommand.cs`
- `src/AbsCli/Commands/GenresCommand.cs`
- `src/AbsCli/Services/TagsService.cs`
- `src/AbsCli/Services/GenresService.cs`
- Response models in `src/AbsCli/Models/` (e.g. `TagModels.cs`, `GenreModels.cs`):
  `TagList { tags }`, `TagRenameResponse { tagMerged, numItemsUpdated }`,
  `TagDeleteResponse { numItemsUpdated }`, and the genre equivalents.

Edited:
- `src/AbsCli/Models/JsonContext.cs` — register the new models.
- `src/AbsCli/Api/ApiEndpoints.cs` — add the six endpoints (delete helpers accept
  the raw value and handle base64 + URI encoding, or the service does it).
- `src/AbsCli/Program.cs` — register `TagsCommand.Create()` and `GenresCommand.Create()`.
- `README.md` — Commands table (new verbs).
- `docs/abs-api-coverage.md` — **fix**: the two GETs require `admin`, not blank.

## --help content (caveats to surface)

- `tags`/`genres` top-level: note all subcommands require admin.
- `rename`: document merge-on-rename — renaming onto an existing value merges
  items and returns `tagMerged`/`genreMerged: true`.
- `delete`: removes the tag/genre from every item that has it; returns count of
  items updated. No confirmation prompt (consistent with `authors delete`).
- `genres list`: note results are unsorted (server behavior).

## Testing

- Unit tests for the services (endpoint URL incl. base64 encoding, request body,
  response deserialization) following the existing service test pattern.
- Extend `docker/seed.sh` if needed so the smoke test can assert non-empty
  tag/genre lists and exercise rename/delete against real data.
- **Update `docker/smoke-test.sh`** in two places:
  1. Add `tags` and `genres` to the help-examples enumeration loops (leaf
     commands must expose ≥2 examples; parent commands listed too).
  2. Add per-command assertion blocks — `tags list`/`genres list` return the
     expected key and non-empty array; a `rename` roundtrip (rename then
     rename back, asserting `numItemsUpdated`); a `delete` of a seeded
     throwaway tag/genre asserting `numItemsUpdated`. Mirror the existing
     section style (e.g. the authors/collections blocks).
- Run `docker/smoke-test.sh` against the compose stack before the PR and only
  then mark it passed.

## Out of scope (YAGNI)

- Confirmation prompt on delete.
- Client-side filtering or sorting of list output.
- "List items that have tag X" — that is `items list --filter`, a separate resource.
