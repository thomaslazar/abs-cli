# Research: `items delete` + `login` non-interactive + `items update --stdin`

**Status:** Research notes for v0.6.0. Not yet a spec.

## Goal

Three independent primitives that round out existing command ergonomics,
bundled into one PR because each is small and they share the theme of
"finish the obvious gaps in commands we already ship":

- **`items delete` + `items batch-delete`** — remove library items
  (soft = DB only, `--hard` = also delete files from disk).
- **`login --username` / `--password` / `--password-stdin`** —
  non-interactive credentials so agents and CI can authenticate without
  a TTY.
- **`items update --stdin`** — bring `items update` in line with the
  batch-* input shape (`--input <file>` + `--stdin`), retiring the
  current inline-JSON-or-file `--input` dual behavior (breaking).

## 1. `items delete` + `items batch-delete`

### ABS endpoints

| Method | Path | Body | Query | Permission |
|---|---|---|---|---|
| DELETE | `/api/items/:id` | – | `?hard=1` | `canDelete` |
| POST | `/api/items/batch/delete` | `{libraryItemIds:[...]}` | `?hard=1` | `canDelete` |

Single: `LibraryItemController.delete` (`LibraryItemController.js:114-150`),
gated by the controller middleware's `req.method == 'DELETE' && !req.user.canDelete → 403`
(`:1236`). Batch: `LibraryItemController.batchDelete` (`:554`), which
checks `canDelete` inline (`:555`) — note it does NOT go through the
`:id` middleware, so it has its own access check
(`ensureUserCanAccessLibraryItemsForBatch`).

Routing: `ApiRouter.js:109` (single) and `:102` (batch).

### Behavior

- **Soft delete (default):** removes the item + its media records from
  ABS's database. Files on disk are untouched. A subsequent library scan
  re-imports the item.
- **Hard delete (`?hard=1`):** additionally `fs.remove`s the item's
  directory (`:134-139`) — irreversible, files are gone.
- **Cascade:** deleting an item prunes any authors/series that become
  empty as a result (`checkRemoveAuthorsWithNoBooks` /
  `checkRemoveEmptySeries`, `:141-146`). This is the same cascade behavior
  the scanner applies, but triggered explicitly.
- Both endpoints return empty `200`. Batch returns `404` if none of the
  supplied ids resolve, `400` on a malformed body.

### Decisions

- **Both verbs.** `items delete` (single) and `items batch-delete`
  (bulk), matching the batch-* pattern established by `items
  batch-update` / `batch-get` / `batch-update-progress`.
- **`--hard` flag, no confirmation prompt.** Soft by default; `--hard`
  sends `?hard=1`. No interactive `--yes` gate — abs-cli is agent-facing
  and thin-passthrough; a prompt would break scripting. The
  irreversibility and the author/series cascade are documented loudly in
  `--help` instead.
- **Permission tag:** `delete` on both verbs; hint string `'delete'
  permission`.
- **`batch-delete` input:** `--input <file>` / `--stdin`, body
  `{"libraryItemIds":["li_a","li_b"]}` — same shape and dual-flag
  handling as `items batch-get` (which already takes
  `{libraryItemIds:[...]}`).

### Command surface

```
items delete       --id <id> [--hard]
items batch-delete  (--input <file>|--stdin) [--hard]
```

### Output

- `items delete`: `{"success":"true"}` synthesized on 200 (matches
  `authors delete` / `collections delete`).
- `items batch-delete`: `{"success":"true"}` on 200.

### Sharp edges for `--help`

- `items delete`: "Soft delete (default) removes the item from ABS's
  database only; files stay on disk and a rescan re-imports it. `--hard`
  also deletes the files from disk — irreversible. Either way, authors
  and series left empty by the deletion are pruned."
- `items batch-delete`: same `--hard` caveat; plus "All-or-nothing on
  access: if you lack access to any item in the batch, the whole request
  is refused (403). 404 if none of the ids resolve."

## 2. `login` non-interactive credentials

### Current state

`LoginCommand` (`src/AbsCli/Commands/LoginCommand.cs`) is interactive
only: `--server` flag (or stderr prompt), then stderr prompts for
username and a hidden password (`ReadPassword` reads keystrokes with
`Console.ReadKey(intercept:true)`).

### ABS endpoint

`POST /login` with `{username, password}` (the CLI already calls this
via `AbsApiClient.LoginAsync(username, password)`). No change needed
server-side or in the client — only the command's credential-gathering
changes.

### Decisions

- Add three options to `login`:
  - `--username <name>` — supply the username non-interactively.
  - `--password <pw>` — supply the password as a flag (convenient but
    visible in `ps` / shell history — documented risk).
  - `--password-stdin` — read the password from stdin (first line),
    avoiding process-list / history exposure. Mutually exclusive with
    `--password`.
- **Fallback to prompt for any missing field.** If `--username` is
  absent → prompt for it. If neither `--password` nor `--password-stdin`
  → prompt (hidden, as today). `--server` already has this
  flag-or-prompt behavior; extend the same model to username/password.
- This keeps the interactive UX intact for humans while enabling fully
  non-interactive use:
  ```
  abs-cli login --server https://abs.example.com --username agent --password-stdin <<<"$ABS_PW"
  abs-cli login --server https://abs.example.com --username agent --password "$ABS_PW"
  ```

### Validation / errors

- `--password` and `--password-stdin` both supplied → exit 1,
  `Provide --password or --password-stdin, not both.`
- `--password-stdin` with empty stdin → exit 1, `No password on stdin.`
- Existing "username and password are required" / "server URL is
  required" checks stay.

### Sharp edges for `--help`

- "`--password` is visible in the process list and shell history. Prefer
  `--password-stdin` (reads the first line of stdin) for scripted use."
- "Any credential not supplied via flag is prompted for interactively
  (username plain, password hidden)."

## 3. `items update --stdin` (breaking)

### Current state

`items update --input` is dual-mode via `CommandHelper.ReadJsonInput`
(`CommandHelper.cs:45-50`): if the value is an existing file path, read
the file; otherwise treat the literal string as JSON. So today
`--input '{"metadata":...}'` (inline) and `--input payload.json` (file)
both work.

This diverges from `items batch-update` / `batch-get`, which are
`--input <file>` (file only) + `--stdin`.

### Decision: match the batch-* shape, drop inline JSON

- `--input <file>` becomes **file-path-only**.
- Add `--stdin` to read the JSON body from stdin.
- Inline `--input '{json}'` **stops working** — this is the breaking
  change the roadmap explicitly calls for ("retiring the
  inline-JSON-or-file `--input` behavior").
- Exactly one of `--input` / `--stdin` required (same handling as
  `batch-update`).

New surface:
```
items update --id <id> (--input <file>|--stdin)
```

Migration for callers:
```
# before:
abs-cli items update --id X --input '{"metadata":{"title":"T"}}'
# after:
echo '{"metadata":{"title":"T"}}' | abs-cli items update --id X --stdin
```

### Impact: existing smoke test must be updated

`docker/smoke-test.sh` currently uses inline `items update --input
'{...}'` in ~5 places (lines 207, 215, 221-222, 232-233, 247-248). These
must convert to `--stdin` (echo-piped). The one file-based call (line
238, `--input "$TMPFILE"`) stays valid. The spec/plan must include this
smoke rewrite or the smoke breaks.

### `ReadJsonInput` fate

`CommandHelper.ReadJsonInput` is the dual-mode helper. After this change,
`items update` no longer uses its inline-fallback behavior. Check whether
any other command still relies on inline (grep `ReadJsonInput` usages) —
if `items update` was the only inline consumer, consider narrowing
`ReadJsonInput` to file-only (or leaving it; the batch verbs call it only
on confirmed file paths). Decision: **leave `ReadJsonInput` as-is**
(other callers pass file paths and the inline fallback is harmless for
them); just stop routing `items update` through the inline path by
requiring `--input` to be a file / `--stdin`. Simplest: `items update`
adopts the exact `batch-update` input block.

## Bundle: PR shape

All three ship as one PR. They're independent (no shared code beyond the
common `--input`/`--stdin` idiom) but cohesive as "command ergonomics
round-out." One spec, one plan, one PR.

Permission tags: `delete` on the two delete verbs; none on `login`
(pre-auth) or `items update` (already tagged `update`).

## Out of scope

- **`items delete` dry-run / preview** — no ABS endpoint for it; agents
  can `items get` first if they want to confirm before deleting.
- **Undo / soft-delete recovery** — ABS has no trash/restore; soft
  delete relies on rescan re-import. Not a CLI feature.
- **`config set` for credentials** — login already persists tokens; we
  don't store raw credentials.
- **Token-based `login` (paste an existing token)** — separate concern;
  `config set` already lets you write tokens directly if needed.

## Open questions

- **Should `items batch-delete` accept `--hard` per-item or batch-wide?**
  Server takes a single `?hard=1` for the whole batch — so batch-wide.
  No per-item granularity exists. Document that `--hard` applies to every
  id in the batch.
- **`--password-stdin` line handling:** read the first line and strip the
  trailing newline only, or the entire stdin verbatim? Lean: first line,
  trimmed of a trailing `\n`/`\r\n` (so `<<<"$PW"` and `echo "$PW" |`
  both work). A password containing a literal newline is not supportable
  via this path — document it.
