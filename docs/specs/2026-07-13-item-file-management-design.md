# Item File Management — Design

Date: 2026-07-13
Status: Approved (brainstorm)

## Goal

Add CLI coverage for per-file operations on a library item: download a file,
delete a file, and read raw ffprobe data. Thin 1:1 pass-through. New `items file`
subgroup.

## API reference (verified against ABS v2.35.1 — LibraryItemController)

`:fileid` is the file's **inode** (`ino`), the same identifier used by
`items toggle-ebook-status --ino` and surfaced in
`items get --expanded → libraryFiles[].ino`. The controller middleware resolves
`req.libraryFile = getLibraryFileWithIno(fileid)`; unknown ino → 404.

| Endpoint | Handler | Behavior | Permission |
|---|---|---|---|
| `GET /api/items/:id/file/:fileid/download` | `downloadLibraryFile` | Streams raw file bytes as an attachment (filename set). | `download` (`canDownload`) |
| `DELETE /api/items/:id/file/:fileid` | `deleteLibraryFile` | **Deletes the file from disk** (`fs.remove`) and updates the item: drops it from `libraryFiles`/`audioFiles`/`ebookFile` (or removes the podcast episode); if no media files remain, sets `isMissing`. Returns `200` empty. | `delete` (`canDelete`, checked in middleware for DELETE) |
| `GET /api/items/:id/ffprobe/:fileid` | `getFFprobeData` | Runs ffprobe server-side on the audio file, returns the raw ffprobe JSON verbatim. **Audio files only** — non-audio ino → 404. | **admin** (`isAdminOrUp`) |

Not exposed: `GET /api/items/:id/file/:fileid` (`getLibraryFile`) — streams the
same bytes inline without the download-permission check; deliberately omitted as
redundant with the `/download` variant.

**Coverage-doc bug:** `GET /api/items/:id/ffprobe/:fileid` is listed with a blank
permission — it is actually **admin**. Must be fixed.

## Command structure

New `items file` subgroup registered in `ItemsCommand.Create()` (alongside
`cover`/`chapters`/`progress`). Common options: `--id` (item ID, required),
`--ino` (file inode, required; help points at `items get --expanded →
libraryFiles[].ino`).

```
abs-cli items file download --id <id> --ino <ino> --output <path|->
abs-cli items file delete   --id <id> --ino <ino>
abs-cli items file ffprobe  --id <id> --ino <ino>
```

### download
- Tag `download`; service hint `"'download' permission"`.
- Streams to `--output <path>` file, or `-` for raw bytes to stdout — mirrors
  `items cover get` (`CoversService.GetStreamAsync` → copy to file/stdout).
- `--output` required.
- On file save, print the saved-file descriptor (`{ path, bytes }`) like
  `items cover get` / `authors image get`; `-` writes bytes to stdout only.

### delete
- Tag `delete`; service hint `"'delete' permission"`.
- Returns `200` empty → CLI prints `{ "success": "true" }` (matches
  `authors delete`).
- No confirmation prompt. `--help` warns prominently: permanently deletes the
  file from disk (not just a DB record); if it is the item's last media file the
  item becomes missing.

### ffprobe
- Tag `admin`; service hint `"admin permission"`.
- Raw JSON passed straight through (`ConsoleOutput.WriteRawJson`, like
  `authors lookup`). No model.
- `--help`: admin-only; audio files only (non-audio ino → exit 2, "Not found").

## Files

Modified:
- `src/AbsCli/Api/ApiEndpoints.cs` — add:
  - `ItemFileDownload(string id, string ino) => $"api/items/{id}/file/{ino}/download"`
  - `ItemFile(string id, string ino) => $"api/items/{id}/file/{ino}"`
  - `ItemFfprobe(string id, string ino) => $"api/items/{id}/ffprobe/{ino}"`
  (Plain interpolation of `ino`, matching the existing `ItemEbookFileStatus`
  precedent — no base64.)
- `src/AbsCli/Services/ItemsService.cs` — add:
  - `Task<Stream> DownloadFileStreamAsync(string id, string ino)` → `GetStreamAsync(endpoint, "'download' permission")`
  - `Task DeleteFileAsync(string id, string ino)` → `DeleteAsync(endpoint, "'delete' permission")`
  - `Task<string> FfprobeAsync(string id, string ino)` → `GetAsync(endpoint, "admin permission")` (raw JSON string)
- `src/AbsCli/Commands/ItemsCommand.cs` — add `CreateFileCommand()` (the `file`
  group + three leaf subcommands) and register it in `Create()`.
- `README.md`, `docs/abs-api-coverage.md`.

No new models, `JsonContext`, or response-example generator changes (bytes /
empty-200 / raw-JSON passthrough).

## Docs
- README Commands table: add three `items file …` rows.
- Coverage doc: mark `download`/`delete`/`ffprobe` rows ✅, and **fix the
  `ffprobe` permission column blank → admin**. `getLibraryFile` row stays `—`
  (intentionally not exposed).

## Testing
- Unit:
  - `ApiEndpointsTests` — the three new helpers build the expected paths.
  - `ItemsCommandTests` — `items file` has `download`/`delete`/`ffprobe`;
    permission tags (`download`/`delete`/`admin`); `--id`/`--ino`/`--output`
    present where expected; delete `--help` documents on-disk deletion; ffprobe
    `--help` documents admin/audio-only.
- Smoke (`docker/smoke-test.sh`):
  - Add `items file`/`items file download`/`items file delete`/`items file
    ffprobe` to the help-example enumeration loops.
  - `download`: pick an audio item, read a `libraryFiles[].ino` via
    `items get --expanded`, download to a temp path, assert bytes > 0.
  - `ffprobe`: same audio ino → assert JSON has `streams` and `format`.
  - `delete`: use a **throwaway** file — the seeded multi-ebook fixture has two
    ebook files; read one ebook file's ino and delete it, assert
    `{ success: "true" }` and that the item still exists (survives with the other
    file). (If the fixture is unavailable, upload a small dedicated throwaway item
    and delete one of its files.)
  - 403s: `items file delete` as `readonlyuser` (no delete) → `'delete'`
    denial; `items file ffprobe` as `testuser` (non-admin) → admin denial.
  - Run `docker/smoke-test.sh` before the PR; only then mark it passed.

## Out of scope (YAGNI)
- `getLibraryFile` (redundant with `download`).
- No confirmation prompt on delete.
- No file-metadata command (that data is in `items get --expanded`).

## Notes
- Do NOT edit `CHANGELOG.md` (release-owned).
