# ABS upstream bugs worth reporting

Bugs observed in **Audiobookshelf itself**, not in abs-cli. They are recorded here
so they are not re-diagnosed from scratch the next time they bite, and so there is
something concrete to paste into an upstream issue.

Nothing here is actionable in this repo beyond the workarounds noted. If an entry
gets reported or fixed upstream, update its status and drop any workaround that
exists only to tolerate it.

| Bug | Observed on | Reported | Workaround in this repo |
|---|---|---|---|
| Backup apply crashes the server (DB disconnect race) | 2.36.0 | not yet | `restart: unless-stopped` + post-apply health wait |
| Batch update with no `mediaPayload` exits the server | 2.36.0 | not yet | client-side guard in `PrepareBatchUpdateBody` |

---

## Backup apply crashes the server (DB disconnect race)

**Status:** not reported upstream.
**Observed:** twice on 2026-08-13/14, against `advplyr/audiobookshelf:2.36.0`, during
`docker/smoke-test.sh`. Intermittent — several full runs on the same image did not
trigger it.

### Symptom

The container exits with code 1 partway through `POST /api/backups/:id/apply`
(`abs-cli backup apply`). Because the dev stack previously had no restart policy,
the server stayed down and the rest of the smoke suite failed against a dead host —
appearing as an abort under `set -euo pipefail` (exit 2, no `FAIL` lines, output
ending mid-section) rather than as an assertion failure.

Container log:

```
Error: ConnectionManager.getConnection was called after the connection manager was closed!
    at ConnectionManager.getConnection (/app/node_modules/sequelize/lib/dialects/abstract/connection-manager.js:70:13)
    at Sequelize.query (/app/node_modules/sequelize/lib/sequelize.js:300:12)
    at SQLiteQueryInterface.select (/app/node_modules/sequelize/lib/dialects/abstract/query-interface.js:407:33)
    at library.findAll (/app/node_modules/sequelize/lib/model.js:1140:47)
    at async library.findOne (/app/node_modules/sequelize/lib/model.js:1240:12)
    at async library.findByPk (/app/node_modules/sequelize/lib/model.js:1221:12)
```

### Mechanism

In `server/managers/BackupManager.js`, `requestApplyBackup`:

1. `await Database.disconnect()` (~`:214`)
2. extracts `absdatabase.sqlite` to a temp path, removes the live DB file, moves the
   temp file into place
3. extracts the `metadata-items/` and `metadata-authors/` folders into `/metadata`
4. `await Database.reconnect()` (~`:253`)

Steps 2–3 take seconds. Throughout that window the connection manager is closed, so
**any** request touching the database throws.

The request that throws is ABS's own: writing into `/metadata` at step 3 wakes the
folder watcher, whose handler performs `library.findByPk` — matching the stack above.
Node exits on the unhandled rejection, killing the server mid-restore.

That it is self-inflicted explains the intermittency: it depends on watcher debounce
timing relative to how long the extract takes, which varies with library size and
disk speed.

### Reproduction

1. Seed a library with enough metadata that the extract in step 3 takes a moment
   (`docker/seed.sh` is sufficient).
2. `abs-cli backup create`, then `abs-cli backup apply --id <id>`.
3. Watch `docker logs`. Expect an exit on some runs, not all.

### Workaround in this repo

Both landed in PR #85:

- `docker/docker-compose.yml` sets `restart: unless-stopped`, so a crash costs
  seconds rather than the whole suite.
- `docker/smoke-test.sh` waits for `/healthcheck` to answer after `backup apply`
  before continuing, and asserts on it, so a restart cannot cascade into every later
  assertion. It also checks `backup apply`'s own exit code, which it previously
  assumed.

Remove both if this is fixed upstream.

### What an upstream report should contain

- ABS version (2.36.0) and that it is the Docker image.
- The error and stack above, and that `library.findByPk` is ABS's **own** watcher,
  not a client request — a maintainer's first instinct will otherwise be to blame the
  client.
- The `requestApplyBackup` line references, since the window between `disconnect()`
  and `reconnect()` is the whole bug.
- That extracting into `/metadata` is what wakes the watcher, so the trigger is
  inside the same function that closed the connection.
- Possible fixes are theirs to choose, but the obvious ones are to pause or drain the
  watcher for the duration of the restore, or to have the DB layer reject queries
  gracefully while disconnected rather than throwing an unhandled rejection that
  exits the process.

---

## Batch update with no `mediaPayload` exits the server

**Status:** not reported upstream.
**Observed:** 2026-09-21, against `advplyr/audiobookshelf:2.36.0`. Deterministic —
unlike the backup-apply race above, this reproduces on every attempt.

### Symptom

`POST /api/items/batch/update` with an entry lacking `mediaPayload` returns 502 and
the container exits 1. With `restart: unless-stopped` it restarts, and retrying the
same body kills it again — a client that doesn't know to stop sending the bad body
gets an indefinite crash loop.

### Mechanism

In `server/controllers/LibraryItemController.js`, the batch-update handler loop:

- `:665` — `const mediaPayload = updatePayload.mediaPayload`
- `:668` — `libraryItem.isPodcast && mediaPayload.autoDownloadSchedule && ...` — a
  podcast item throws here first, since this line has no `?.` at all.
- `:673` — `await libraryItem.media.updateFromRequest(mediaPayload)` — not where it
  throws: `Book.updateFromRequest` is itself defensive
  (`server/models/Book.js:371` is `if (!payload) return false`), so this call
  returns cleanly.
- `:675` — `Array.isArray(mediaPayload.metadata?.series)` — where a book item
  throws. The `?.` guards `.series`, but `.metadata` is dereferenced directly off
  `mediaPayload` one level above it, so a missing payload throws
  `TypeError: Cannot read properties of undefined (reading 'metadata')` before the
  optional chain ever applies.

The throw happens inside an `async` route handler with nothing catching it, so it
surfaces as an unhandled promise rejection. `server/Server.js:214-216` handles that
event generically:

```js
process.on('unhandledRejection', async (reason, promise) => {
  await Logger.fatal('[Server] Unhandled rejection:', reason, '\npromise:', util.format('%O', promise))
  process.exit(1)
})
```

### Why this is upstream, not abs-cli's

Any client can trigger this — malformed input from any HTTP caller, not just this
CLI, and a malformed request body should produce a 400, not take the process down.

### Reproduction

```bash
curl -X POST "$ABS_URL/api/items/batch/update" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '[{"id": "<existing-item-id>"}]'
```

No `mediaPayload` key on the entry is enough; watch `docker logs` for the unhandled
rejection and the container exit.

### Workaround in this repo

`PrepareBatchUpdateBody` (added alongside the `mediaPayload` wrapper fix) refuses any
entry whose `mediaPayload` is absent or null before the request is sent, naming the
offending index. Combined with correcting the documented request shape so
`--help-full` shows the wrapper, a caller following the CLI's own help can no longer
construct the body that trips this.

### What an upstream report should contain

- ABS version (2.36.0) and that it is the Docker image.
- The exact line references above — `:665`, `:668`, `:673`, `:675` — since the fix is
  one `?.` deeper than where it currently sits.
- That the podcast branch (`:668`) and the book branch (`:675`) are two separate
  throw sites, not one.
- That a 400 on a malformed body is the expected behavior; `process.exit(1)` on any
  unhandled rejection is a separate, broader hardening question also worth raising.
