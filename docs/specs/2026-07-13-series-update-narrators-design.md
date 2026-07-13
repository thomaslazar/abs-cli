# Series Update & Narrator Management — Design

Date: 2026-07-13
Status: Approved (brainstorm)

## Goal

Add CLI coverage for two independent ABS metadata-management features, shipped
together in one PR:

1. **Series update** — `PATCH /api/series/:id`
2. **Narrator management** — `GET`/`PATCH`/`DELETE /api/libraries/:id/narrators`

Thin 1:1 pass-through — no smart defaults, no response interpretation. Both lean
on patterns already in the codebase: series update ≈ `authors update`; narrators
≈ the tags/genres commands.

## API reference (verified against ABS v2.35.1)

### Series update — `SeriesController.update`
- Permission: `update` (`req.user.canUpdate`; middleware 403s otherwise).
- Body: `{ name?, description? }` — only these two keys, only when the value is a
  string. Empty payload → `400 "No valid fields to update"`.
- Response: the updated series object (`toOldJSON()`), shape = existing
  `SeriesItem` model (`id, name, nameIgnorePrefix, description, addedAt,
  updatedAt, libraryId`).
- Middleware also 404s if the series doesn't exist or the user has no accessible
  books in it.
- **No merge-on-rename.** The handler has a `// TODO: should check for duplicate
  name` and currently doesn't — renaming to an existing series name produces two
  same-named series. Server accepts empty `name: ""` (sets it).

### Narrators — `LibraryController`
Narrators are derived from book `media.narrators`; the narrator's key IS its name.
| Verb | Endpoint | Perm | Param / body | Response |
|---|---|---|---|---|
| list | `GET /api/libraries/:id/narrators` | (none) | — | `{ narrators: [{ id, name, numBooks }] }`, natural-sorted by name |
| rename | `PATCH /api/libraries/:id/narrators/:narratorId` | `update` | `:narratorId` base64(name); body `{ name }` | `{ updated: <int> }` |
| delete | `DELETE /api/libraries/:id/narrators/:narratorId` | `update` | `:narratorId` base64(name) | `{ updated: <int> }` |

Notes:
- `id` in the list response = `encodeURIComponent(base64(name))` — same encoding
  as tags/genres delete. Reuse `ApiEndpoints.EncodePathValue`.
- Rename merges: items on the old name move to the new name; empty new name →
  `400`.
- **DELETE requires `update`, NOT `delete`** — the controller checks `canUpdate`.
  The coverage doc currently says `delete`; that is wrong and must be fixed.

## Command structure

### Series (extend existing `series` command)
```
abs-cli series update --id <id> [--name <n>] [--description <d>]
```
- Tag `update`; service hint `"'update' permission"`.
- Mirror `authors update` behavior: reject empty `--name` client-side; require at
  least one of `--name`/`--description` (else client error, exit 1);
  `--description ""` clears (sends `""`).
- Response model: reuse `SeriesItem`.
- `--help` caveats: no merge-on-rename (duplicate names allowed, unlike authors);
  empty name rejected client-side.

### Narrators (new top-level command, library-scoped)
```
abs-cli narrators list
abs-cli narrators rename <old-narrator> <new-narrator>
abs-cli narrators delete <narrator>
```
- Library-scoped: `--library` override + default library, via
  `CommandHelper.RequireLibrary` (same as `series list` / `authors list`).
- `list`: no permission tag. `rename`/`delete`: tag `update`, hint
  `"'update' permission"`.
- Positional args (clone of tags/genres).
- `--help` caveats: rename merges if the target exists; delete needs `update`
  (not `delete`); base64 path encoding handled internally.

## Files

New:
- `src/AbsCli/Commands/NarratorsCommand.cs`
- `src/AbsCli/Services/NarratorsService.cs`
- `src/AbsCli/Models/NarratorModels.cs` — `NarratorListResponse { narrators }`,
  `NarratorItem { id, name, numBooks }`, `NarratorUpdateResponse { updated }`,
  `NarratorRenameRequest { name }`.

Modified:
- `src/AbsCli/Commands/SeriesCommand.cs` — add `update` subcommand.
- `src/AbsCli/Services/SeriesService.cs` — add `UpdateAsync(id, body)`.
- `src/AbsCli/Api/ApiEndpoints.cs` — add `LibraryNarrators(libraryId)` and
  `LibraryNarratorByName(libraryId, name)` (reuse private `EncodePathValue`).
  (`SeriesById` already exists.)
- `src/AbsCli/Models/JsonContext.cs` — register the new narrator types.
- `tools/GenerateResponseExamples/Program.cs` — exclude `NarratorRenameRequest`
  (request body, like the tag/genre request types).
- `src/AbsCli/Commands/ResponseExamples.g.cs` — regenerated.
- `src/AbsCli/Program.cs` — register `NarratorsCommand`.
- `README.md`, `docs/abs-api-coverage.md`, `docker/seed.sh`,
  `docker/smoke-test.sh`.

## Docs

- README Commands table: add `series update` row and 3 `narrators` rows.
- Coverage doc: mark ✅ for `PATCH /api/series/:id`,
  `GET/PATCH/DELETE /api/libraries/:id/narrators/*`, AND **fix** the DELETE
  narrator permission from `delete` → `update`.

## Testing

- Unit:
  - Model round-trips for `NarratorListResponse`/`NarratorItem`/
    `NarratorUpdateResponse`/`NarratorRenameRequest`.
  - `ApiEndpoints` test for `LibraryNarratorByName` base64+URI encoding.
  - Command tests: `series update` (subcommand present, `--id` required, empty
    `--name` rejected, at-least-one-flag rule, `update` permission tag, help
    caveats); `narrators` (three subcommands, positional args, permission tags on
    rename/delete but not list, help caveats).
- Seed (`docker/seed.sh`): add narrators to a couple items (via
  `PATCH /api/items/:id/media` body `{ metadata: { narrators: [...] } }`),
  including a throwaway `smoke-temp-narrator`. Series are already seeded (3).
- Smoke (`docker/smoke-test.sh`):
  1. Add `series update` + `narrators list/rename/delete` to the help-example
     enumeration loops.
  2. `series update`: pick a seeded series id (via `series list`), update its
     description, assert the returned `id`/`description`.
  3. `narrators`: `list` returns non-empty `narrators` with `id`/`name`/
     `numBooks`; rename roundtrip on `smoke-temp-narrator` (assert `updated`);
     delete `smoke-temp-narrator` (assert `updated`).
  4. **403 assertions (new features)**: `series update` and `narrators rename`
     as `readonlyuser` (has `update:false`) → exit 2 / permission denial,
     mirroring existing 403 sections.
  5. **403 backfill (tags/genres)**: the existing tags/genres smoke section has
     NO admin-denial assertions even though all six endpoints require admin.
     Backfill representative denials in that section — e.g. `tags list` and
     `genres list` as a non-admin user (`testuser`, type `user`) → exit 2 /
     admin permission denial. This closes a gap in the previously merged work.
  - Run `docker/smoke-test.sh` against the compose stack before the PR; only
    then mark it passed.

## Out of scope (YAGNI)

- No confirmation prompts on narrator delete.
- No client-side sorting/filtering of list output.
- No series-merge convenience (ABS has no merge endpoint; consolidating is
  per-book via `items update`).
- No library-scoped `series get`/narrator-by-id lookups beyond what's above.
