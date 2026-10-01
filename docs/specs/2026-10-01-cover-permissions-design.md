# Cover permissions and `items cover link` — design

**Date:** 2026-10-01
**Status:** implemented

## Problem

`items cover set` documents the wrong permissions, and spans two endpoints with
different checks (ABS v2.37.1, `server/routers/ApiRouter.js:113-115`):

| Mode | Endpoint | ABS checks | CLI says today |
|---|---|---|---|
| `--url`, `--file` | `POST /api/items/:id/cover` | `update` (`LibraryItemController.middleware`, every POST/PATCH) **and** `upload` (`uploadCover`) | tag `upload`, hint `'upload' permission` |
| `--server-path` | `PATCH /api/items/:id/cover` | `update` (middleware) | tag `upload`, no hint |

`items cover remove` (`DELETE`, `canDelete`) is correct. Smoke never noticed: it
runs cover commands as root, and the seeded restricted users have either both
`update`+`upload` (uploaduser) or neither (readonlyuser).

`cover set` also violates the CLAUDE.md rule "each command maps to a single ABS
API endpoint".

## Decision

- **Split the command.** `items cover set --url|--file` (POST) and a new
  `items cover link --path` (PATCH). `--server-path` is removed — breaking, so
  the next release is MINOR (1.2.0). No compatibility alias: a leftover
  `--server-path` fails as an unknown option; the changelog (release time)
  carries the migration line.
- **Multi-token permission tags.** `AddPermissionRequired` accepts several
  tokens and renders them comma-separated; every listed permission is
  required. Hint mirroring extends accordingly.
- **Seed a user with upload but no update** so both halves of the POST
  requirement are proven by smoke.

## Design

### Help infrastructure — `src/AbsCli/Commands/HelpExtensions.cs`

`AddPermissionRequired(this Command command, params string[] permissions)`
renders one line: `string.Join(", ", permissions)`. Single-token callers and
their rendered output are unchanged (`Permission required:\n  upload`).

### Commands — `ItemsCommand.cs`, `CoversService.cs`

- `items cover set --id (--url | --file)` — tag `update, upload`. Exactly one
  of `--url`/`--file`. `SetByUrlAsync` / `UploadFromFileAsync` hint
  `"'update' and 'upload' permission"` (a 403 can come from either check;
  ABS doesn't say which).
- `items cover link --id --path` (both required) — tag `update`;
  `LinkExistingAsync` hint `"'update' permission"`. Carries the two Notes
  lines currently on `set`: path must be among the item's libraryFiles, else
  500 "Invalid cover path" (e.g. a cover applied via cover set --file, stored under /metadata); the image
  is copied to /metadata/items/<id>/ (unless storeCoverWithItem) and the
  response cover is the copy. Example:
  `--path "/audiobooks/Author/Title/cover.jpg"`. Response shape
  `CoverApplyResponse`.
- `--path` rather than ABS's body field `cover` — `cover link --cover` reads
  badly; the flag-mirrors-body rule is for `update --id` resources.
- `items cover` group lists four verbs: set, link, get, remove.

### Seed — `docker/seed.sh`

New user `uploadonlyuser` / `uploadonlypass`: `download` true, `update` false,
`delete` false, `upload` true, access-all true (same shape as `uploaduser`).
README seed line: "4 users" → "5 users".

### Smoke — `docker/smoke-test.sh` (Cover section)

- The Mode 2 block switches from `cover set --server-path` to
  `cover link --path` (same fixture, same assertions, renamed labels).
- New, after the positive link case:
  - `cover link --path <libraryFile>` as **testuser** (update, no upload)
    succeeds — `update` alone suffices.
  - `cover link` as **readonlyuser** → non-zero exit, stderr contains
    `'update' permission`.
  - `cover set --file` as **testuser** → non-zero, stderr contains
    `'update' and 'upload' permission` (upload check).
  - `cover set --file` as **uploadonlyuser** → same (update check).
- Every user switch returns to root before root-only steps; cover removed
  after any successful set/link.

### Docs and conventions

- README Commands table: `items cover set --id <id> [--url | --file]` and a
  new `items cover link --id <id> --path <path>` row; the
  "Permission required" paragraph says the block may list several
  permissions, all required.
- CLAUDE.md "Permission tagging" / "Permission hint mirroring": multi-token
  form, e.g. tag `update, upload` ↔ hint `"'update' and 'upload' permission"`.
- `docs/abs-api-coverage.md`: POST row permission `update, upload`; PATCH row
  → `items cover link`.
- `docs/cli-design.md` cover row; `docs/testing.md` cover bullet.
- `docs/abs-compatibility.md`: `1.1.5+` row label → `1.2.0+`, reference
  `items cover link` instead of `items cover set --server-path`.
- Not touched: CHANGELOG (release process), `docs/roadmap.md` completed
  milestone text (historical).

## Testing

- Unit: multi-token render; `cover set` help shows `update, upload` and no
  `--server-path`; `cover link` help shows `update`, `--path`, the Notes;
  group lists four verbs; existing cover help tests migrated.
- Smoke on a fresh 2.37.1 stack: full pass including the new 403 / positive
  cases. CI on the PR.
