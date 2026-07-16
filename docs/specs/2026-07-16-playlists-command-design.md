# Playlists command — design

Date: 2026-07-16

## Summary

Add a `playlists` command group to abs-cli, mirroring the existing
`collections` surface but adapted to Audiobookshelf's user-owned playlist
model. Books-only for this pass (no podcast episode support), plus the
playlist-specific `create-from-collection` convenience endpoint.

## Context

- ABS playlists are **user-owned**, not shared/admin like collections. The
  `PlaylistController.middleware` enforces that the caller owns the playlist
  and can access its library. No `user.permissions` flag is involved.
- The endpoint set maps almost 1:1 to the collections surface, with two
  playlist-only wrinkles: podcast-episode items (`libraryItemId` + optional
  `episodeId`) and `createFromCollection`.
- Reference: `temp/audiobookshelf/server/controllers/PlaylistController.js`,
  routes in `server/routers/ApiRouter.js`.

## Command surface

Books-only. `--book` names a library item id, matching `collections add`.

| Verb | Endpoint | Notes |
|------|----------|-------|
| `playlists list [--library <id>] [--limit] [--page]` | `GET /libraries/:id/playlists` | Library resolved via flag → `defaultLibrary` config → error (`RequireLibrary`). Paginated. Uses the non-deprecated per-library endpoint. |
| `playlists get --id <id>` | `GET /playlists/:id` | Expanded. |
| `playlists create [--library <id>] --name <n> [--description <d>] [{--input\|--stdin}]` | `POST /playlists` | Items **optional** — empty playlist allowed. Input JSON `{"books":["lid",...]}`, mapped to body `items:[{libraryItemId}]`. |
| `playlists update --id <id> [--name <n>] [--description <d>]` | `PATCH /playlists/:id` | Cannot change library/owner (400). Empty string is ignored server-side, so **description cannot be cleared**. |
| `playlists reorder --id <id> {--input\|--stdin}` | `PATCH /playlists/:id` (items) | Full ordered `{"books":[...]}`. Length must equal current item count; does NOT add/remove (400 on length mismatch or unknown id). |
| `playlists delete --id <id>` | `DELETE /playlists/:id` | Simple, no confirmation gate (user-owned, cheap — unlike `libraries delete`). |
| `playlists add --id <id> --book <lid>` | `POST /playlists/:id/item` | Item must be in the **same library** as the playlist (400 otherwise). |
| `playlists remove --id <id> --book <lid>` | `DELETE /playlists/:id/item/:lid` | **Removing the last item deletes the playlist.** |
| `playlists batch-add --id <id> {--input\|--stdin}` | `POST /playlists/:id/batch/add` | Body `{"books":[...]}` → `items:[{libraryItemId}]`. Silently skips duplicates. |
| `playlists batch-remove --id <id> {--input\|--stdin}` | `POST /playlists/:id/batch/remove` | Tolerates missing ids; **emptying the playlist deletes it.** |
| `playlists create-from-collection --collection <id>` | `POST /playlists/collection/:id` | Copies collection name + description and all its books (in collection order) into a new playlist owned by the caller. 400 if the collection has no books. Books-only. Snapshot — no live link to the collection. |

## Permissions

None. Playlists are user-owned; the server enforces ownership + library
access in middleware. Therefore:

- **Zero** `command.AddPermissionRequired(...)` calls anywhere in this group.
- **No** `permissionHint` strings on the service HTTP calls.

## Structure

- New `src/AbsCli/Commands/PlaylistsCommand.cs` and
  `src/AbsCli/Services/PlaylistsService.cs`, modeled on
  `CollectionsCommand`/`CollectionsService`.
- Reuse the `{"books":[...]}` input shape for `create` / `reorder` /
  `batch-add` / `batch-remove`. Either reuse `CollectionBooksRequest` or add a
  parallel `PlaylistBooksRequest`; the service maps `books` → the ABS body
  field (`items:[{libraryItemId}]`, or the ordered id array for reorder).
- Register AOT JSON contexts in `AppJsonContext` for any new request/response
  types. Add response examples via `AddResponseExample<Playlist>()` as the
  sibling commands do.
- Wire the group into the root command next to `collections`.

## `--help` caveats to document

Per project convention every quirk lives in the relevant command's `--help`:

- `create`: an empty playlist is allowed (unlike collections).
- `remove` / `batch-remove`: removing the last item **deletes the playlist**.
- `reorder`: pure reorder — length must match current; cannot add/remove.
- `add`: the library item must be in the same library as the playlist.
- `update`: description cannot be cleared (empty string is ignored server-side);
  library and owner cannot be changed.
- `create-from-collection`: books-only snapshot; no live link; 400 if the
  source collection is empty.

## README

Add a `playlists *` block to the Commands table in `README.md` in the same PR.

## Testing

- Extend `docker/smoke-test.sh` with a playlist lifecycle: create → add →
  reorder → get/verify order → remove down to empty → assert the playlist is
  gone (auto-delete). Also cover `create-from-collection` against a seeded
  collection.
- Extend `docker/seed.sh` if new fixtures (a seeded collection with books) are
  required; do not silently drop assertions.

## Out of scope

- Podcast episodes (`episodeId`) — no podcast support in the CLI yet.
- The deprecated global `GET /playlists` (all playlists across libraries).
