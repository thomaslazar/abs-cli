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
