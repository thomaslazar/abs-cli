# Concurrent config saves — design

**Date:** 2026-09-20
**Status:** implemented
**Issue:** [#88](https://github.com/thomaslazar/abs-cli/issues/88)

## Problem

Running several `abs-cli` commands concurrently can corrupt `~/.abs-cli/config.json`,
leaving a file that no later command can read:

```json
{
  "server": "https://<host>",
  ...
  "lastServerVersion": "2.36.1"
}}
```

```
ERROR Config file is not valid JSON: ~/.abs-cli/config.json — delete it and run
'abs-cli login'. ('}' is invalid after a single JSON value. ...)
```

### The reported cause is not the actual cause

The issue attributes this to a non-atomic write and proposes adding write-then-rename.
`ConfigManager.Save` already does write-then-rename (`Configuration/ConfigManager.cs:43-58`),
and `File.Move(overwrite: true)` within a directory is an atomic rename. The defect is
one line up:

```csharp
var tmpPath = _configPath + ".tmp";
```

The staging path is a **fixed name shared by every process**. Concurrent writers all
open `config.json.tmp`, truncate it, and write at their own offsets, tearing each
other's content *in the staging file*. Each then atomically renames the torn result
into place. The rename was never the problem; the thing being renamed was already
damaged.

This also explains the exact reported shape — a longer object written over a shorter
one, leaving the previous writer's trailing `}`.

## The concurrency that does not need fixing

The issue's second suggestion is an inter-process lock, so N processes don't each
refresh and "invalidate each other's tokens". Reading the ABS server source
(`temp/audiobookshelf/server/auth/TokenManager.js:212-271`), that hazard does not
exist:

- Rotation is a conditional update, `WHERE id = session.id AND refreshToken = previous`.
- The loser of a concurrent race gets `numUpdated === 0`, re-reads the row, and returns
  **the token the winner just wrote**. Both callers get HTTP 200 and the same refresh
  token.
- The superseded token remains valid for a grace window — `lastRefreshToken`, default
  10 minutes (`REFRESH_TOKEN_GRACE_PERIOD`).
- There is no reuse-detection lockout; the scheme is deliberately lenient, with a
  comment in the server source saying so.

So concurrent refreshes converge rather than conflict, and a stale write is covered by
the grace window. There is nothing for a lock to protect. Adding one would serialize
processes against a server designed not to need it.

## Design

### 1. Per-process staging file

```csharp
var tmpPath = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
```

Everything else in `Save` is unchanged: serialize, write, copy the Unix file mode from
the existing config, `File.Move(tmpPath, _configPath, overwrite: true)`.

A `try`/`catch` deletes the staging file if anything throws between write and rename,
then rethrows.
The old fixed name self-overwrote on the next run and so never needed cleanup; a unique
name without it would accumulate orphans in `~/.abs-cli/`.

Last-writer-wins on the final file is the intended behavior and is what the issue asks
for: "The last writer wins, but the file is never a mix of two writes."

### 2. Error text

`Load` (`ConfigManager.cs:33-40`) currently opens with "delete it and run 'abs-cli login'".
That is the destructive option offered first, and in the reported case it is the wrong
one — the tokens in the file were intact and only the brace structure was damaged, so
repairing it restores the session while deleting discards a valid 30-day refresh token.

Reworded to say the file may be repairable and holds the only copy of the refresh token,
inspect before deleting, with `abs-cli login` as the fallback. No test pins the current
string.

### 3. Tests

`tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs`:

- **Concurrent saves never tear.** N tasks saving to one path, alternating between a long
  and a short config, repeated enough times to make interleaving likely. Assert `Load()`
  parses on every iteration and returns one of the two inputs intact. The length
  alternation is what makes the failure visible — a short write over a long one leaves the
  longer tail behind, which is precisely the reported corruption. This test must be shown
  to fail against the current fixed-name code before the fix lands; a green-from-the-start
  concurrency test proves nothing.

  **Observed when run:** the red run failed with `FileNotFoundException`, not torn JSON —
  in-process, one thread's `File.Move` consumes the shared `config.json.tmp` out from
  under another before a tear can form. Across processes (#88) the tear is what surfaced.
  Two symptoms of the same shared staging path; the test covers both, since `Load()`
  throws on a torn file and `Save()` throws if the staging file vanishes. The test is
  named `Save_ConcurrentWriters_DoNotCorruptTheConfig` for that reason.
- **No leftovers.** After a save, the config directory contains only `config.json`.

### 4. Serialize the read-modify-write

`UpdateTokens` and `UpdateVersionCheck` (`ConfigManager.cs:67-89`) both `Load()`, mutate
two fields, and `Save()` the **whole** config. That read-modify-write is not atomic across
processes, and the consequence is worse than a lost cache entry:

1. Process B calls `UpdateVersionCheck` and loads the config, holding tokens `A`.
2. Process A refreshes, gets tokens `B`, and persists them.
3. Process B saves — writing its stale tokens `A` back over them.

The superseded refresh token is only honored inside the 10-minute
`REFRESH_TOKEN_GRACE_PERIOD`, while the access token still has up to an hour left. So
nothing attempts a refresh until long after the grace window closes, and when one finally
happens the server rejects it: `Invalid refresh token` → forced re-login. That is the same
user-visible breakage #88 reported, from the same concurrency, and the staging-file fix
does not touch it.

**This requires a lock, and that is not a reversal of the section above.** What was
rejected there is locking the *token-refresh HTTP call*, because the server converges
concurrent refreshes on its own. This is a lock around the *local file's* read-modify-write,
which no server behavior can substitute for — a cross-process read-modify-write cannot be
made safe without mutual exclusion or a compare-and-swap. Writing "only our own fields" is
not an alternative: serializing the JSON document requires reading the other fields anyway.

Both helpers acquire an exclusive handle on `<config>.lock` (`FileShare.None`) and hold it
across `Load` → mutate → `Save`. The shape matters: the OS releases such a handle when the
holder dies, so unlike a lock file whose mere *existence* is the lock, a killed process
cannot wedge every later invocation. Acquisition retries briefly and then proceeds without
the lock rather than hanging a CLI command — the race it protects against is rare, and
blocking forever is a worse failure than losing it.

`Save` itself is not locked. It is already safe on its own after change 1, and
`login`/`config set` call it to write a config the operator just specified, where
last-writer-wins is the intended behavior.

### 5. Correction to a prior spec

`docs/specs/2026-09-18-upload-streaming-design.md` states that ABS access tokens last
~12 hours, and uses that to argue the unreplayable-401 path is nearly unreachable. The
default is **1 hour** (`ACCESS_TOKEN_EXPIRY`, `TokenManager.js:16-21`). With preflight
refreshing only inside a 60-second window, any request running longer than ~59 minutes
can reach that path — well within range for the multi-GB uploads that spec enabled. The
line is corrected there, and the same figure is corrected in issue #89.

## Out of scope

- **Inter-process lock.** See above; the server already handles it.
- **A `.bak` of the last-known-good config.** Suggested in the issue, but with the tear
  fixed there is no known remaining way to corrupt the file. It would guard a hazard
  this change eliminates.
- **A repair tool** for the trailing-brace shape. A recovery path for a bug that will no
  longer occur.
