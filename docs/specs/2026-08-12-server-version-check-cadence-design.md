# Server version check cadence — design

**Date:** 2026-08-12
**Status:** approved, not yet implemented

## Problem

`AbsApiClient.CheckServerVersion` is called from exactly one place —
`LoginCommand.cs:101` — reading `serverSettings.version` from the login response.
It compares against `MinSupportedVersion` / `MaxTestedVersion` (2.33.1 / 2.36.0)
and warns when the server is below the floor or above the tested ceiling.

Binding that check to login is wrong, for one reason above all others: **nothing
about a login correlates with the server changing.** These are self-hosted servers,
typically a `:latest` image with `restart: unless-stopped`. The version changes when
the image is pulled — an event involving no login, which the CLI cannot observe.
Meanwhile tokens persist and refresh (ABS access tokens last 1 hour, refresh tokens
30 days), so a working install can go weeks or months without a fresh login. The
check fires at the moment the answer is least likely to have changed, and never at
the moment it is most likely.

The concrete failure this is meant to prevent has already happened: an ABS live
stack was updated and the operator did not notice for weeks, so the CLI was never
bumped and no warning was ever printed.

Two clarifications about the current harm, to keep the design honestly scoped:

- The in-range case logs at **Debug** level, invisible without `--debug` /
  `ABS_DEBUG=1`. So between logins the CLI is not emitting a *stale claim* about
  the server version — it emits nothing. The defect is a **missing warning**, not
  false confidence.
- The check is warn-only and stays that way. Real API drift announces itself
  concretely (a deserialization failure, a 404, a vanished field — how #65 and the
  `numFiles` regression were both actually found). The version check's job is
  **provenance**: "you are off the versions the maintainer exercised." It is not
  protection, and must not pretend to be.

## Goals

- Surface a version verdict within 24 hours of the server changing, without
  depending on the operator logging in.
- Never let the check cost a round-trip on every invocation.
- Never let the check fail, slow, or interrupt the command it precedes.
- Keep the warning actionable: name the versions and say what to do.

## Non-goals

- Blocking or refusing to run on an out-of-range server. Warn-only, unchanged.
- Checking whether a newer *CLI* exists (no calls to GitHub or any release feed).
- Making the interval configurable. See "Rejected alternatives".
- Any change to the `MinSupportedVersion` / `MaxTestedVersion` model itself.

## Design

### Trigger

`AbsApiClient` gains a single `PreflightAsync()`, and all **nine** existing
`EnsureValidTokenAsync()` call sites become `PreflightAsync()` calls, so the two
pre-flight concerns cannot drift apart as endpoints are added.

```csharp
private bool _versionCheckDone;

private async Task PreflightAsync()
{
    await EnsureValidTokenAsync();
    await EnsureVersionCheckedAsync();
}
```

The once-per-process guard belongs to the **version check only**.
`EnsureValidTokenAsync` must keep running on every call, because a long-running
command (`upload`, `encode-m4b`) can cross a token expiry mid-run.

Commands that never construct a client — `config`, `self-test`, `changelog` — are
unaffected, for free.

### Staleness decision

A pure, internal, directly testable function:

```csharp
internal static readonly TimeSpan VersionCheckInterval = TimeSpan.FromHours(24);

internal static bool ShouldCheckVersion(DateTimeOffset? lastCheck, DateTimeOffset now)
    => lastCheck is null
       || now - lastCheck.Value >= VersionCheckInterval
       || lastCheck.Value > now;   // clock moved backwards — treat as stale
```

24 hours, hardcoded. Worst-case staleness is a day, well inside the multi-week
window that motivated this.

### Probe

`GET {server}/status` — verified unauthenticated in ABS 2.36.0
(`server/Server.js:350-365`), returning `serverVersion` among other fields. Being
unauthenticated means it works even when the stored token is dead, and needs no
ordering relative to token refresh.

- Add `ApiEndpoints.Status = "/status"` (root, not under `/api`).
- New `ServerStatus` DTO carrying `serverVersion`, registered in `AppJsonContext`
  with a round-trip assertion in `self-test`, per the AOT convention.
- Its **own short timeout** (3s) via a linked `CancellationTokenSource`, not the
  client default. A hung server must not stall the real command behind a
  diagnostic.

### Comparison and message

`CompareVersions` and `CheckServerVersion` are reused unchanged — both already
`internal` and unit-tested (PR #72). Only the ceiling message changes, to address
the case that actually occurs:

```
Warning: abs-cli 1.0.3 was tested up to ABS 2.36.0; this server is 2.38.0.
Check for a newer abs-cli.
```

When the observed version differs from `lastServerVersion`, the message names the
change, since that is the operator's actual signal:

```
Warning: this server moved from ABS 2.36.0 to 2.38.0 since the last check.
abs-cli 1.0.3 was tested up to 2.36.0. Check for a newer abs-cli.
```

`ClientVersion` is already available in `AbsApiClient`. The floor message keeps its
current wording. Warnings go to stderr, as now.

Rate limiting is implicit: the check runs at most once per 24 hours, so an
out-of-range server produces at most one warning per day. No separate
"already warned" bookkeeping.

### Persistence

Two new `AppConfig` fields, camelCase keys matching the existing four:

```csharp
[JsonPropertyName("lastVersionCheck")]
public DateTimeOffset? LastVersionCheck { get; set; }

[JsonPropertyName("lastServerVersion")]
public string? LastServerVersion { get; set; }
```

`lastServerVersion` costs nothing and pays for itself twice: it makes the debug
line meaningful, and it lets the warning say the version *changed* since the last
check rather than merely stating the current one. It also answers "what was I
talking to?" after a failure.

**These fields are written by a read-modify-write of the on-disk config, not by
`Save(_config)`.** New `ConfigManager` method:

```csharp
public void UpdateVersionCheck(string? serverVersion, DateTimeOffset checkedAt)
{
    var onDisk = Load();          // file only — deliberately not the resolved config
    onDisk.LastServerVersion = serverVersion;
    onDisk.LastVersionCheck = checkedAt;
    Save(onDisk);
}
```

Rationale: `Resolve()` merges environment variables into the in-memory
`AppConfig`, so `Save(_config)` would persist an `ABS_TOKEN` from the environment
into `~/.abs-cli/config.json` — writing to disk a secret the operator deliberately
kept out of it. `RefreshTokenAsync` already has a milder form of this (it persists
an env-provided `server`); a daily version check would make it routine.

Consequence to accept: for an operator with no config file at all, the first check
**creates** `~/.abs-cli/config.json` containing only these two fields. No secrets,
and it stops a re-probe on every invocation. If the file cannot be written, the
failure is swallowed and the next invocation simply probes again.

### Failure handling

Any probe failure — unreachable, timeout, non-2xx, non-JSON, missing
`serverVersion` — is caught, logged at Debug, and **does not advance the
timestamp**, so the next invocation retries. If the server is genuinely down, the
real command fails a moment later with a useful error; the diagnostic must never be
the thing that reports it.

### The recorder

One method owns "a version was observed", so probe and login share it exactly:

```csharp
// Warns per the rules above, then persists via ConfigManager.UpdateVersionCheck.
internal void RecordServerVersion(string? observed);
```

`EnsureVersionCheckedAsync` reads `LastVersionCheck` from the in-memory resolved
config (reading merged values is fine — only *writing* them is the hazard), calls
`ShouldCheckVersion`, probes if stale, and hands the result to
`RecordServerVersion`.

### Login

`LoginCommand` keeps reading the version from the login response it already has and
passes it to `RecordServerVersion`, so it both warns and updates the two fields.
Logging in therefore does not trigger a redundant `/status` probe moments later.

### `config get`

`config get` prints a fixed dictionary; the two new fields are added to it so the
state is inspectable. Neither is settable via `config set` (which stays limited to
`server` and `defaultLibrary`).

## Rejected alternatives

- **Piggyback on `/auth/refresh`.** The refresh response does carry the full login
  payload including `serverSettings` (`Auth.js:329-357`, `Auth.js:96-105`), and the
  CLI already deserializes and discards it — so this would have been free and
  roughly hourly. Rejected because `/status` is strictly better: unauthenticated,
  works with a dead token, and does not depend on a refresh happening to occur.
  Two mechanisms would have been worse than one.
- **Probe on every invocation, no persistence.** Simplest code, always current, but
  a round-trip per command cuts against the thin pass-through rule for no benefit
  over a 24-hour window.
- **Configurable interval.** A config key plus env var plus help text plus docs, to
  tune a constant that has one sensible value. If 24 hours ever proves wrong,
  changing the constant is a patch release.
- **Changing the floor/ceiling model.** Upstream not respecting semver (see the
  portability note) means a range cannot promise compatibility — but it never did.
  As a provenance claim it stays meaningful under any versioning scheme.

## Testing

- **Unit:** `ShouldCheckVersion` — null → true, 23h → false, exactly 24h → true
  (the boundary is `>=`), 25h → true, future timestamp → true.
  Ceiling and floor message formatting. `UpdateVersionCheck` preserving unrelated
  on-disk fields and *not* persisting env-derived values.
- **Any new test that makes production code log must join `[Collection("NLog")]`.**
  NLog's configuration is process-global; a stray line lands in a log-asserting
  test's `MemoryTarget` and fails it on count. This is exactly what broke the
  v1.0.3 release CI (PR #74).
- **`self-test`:** `ServerStatus` DTO round-trip, for AOT.
- **Smoke:** with `ABS_DEBUG=1` and a temp `HOME` (to control config state), assert
  the probe happens on a fresh config and is skipped on a second invocation inside
  the window.

## Docs

- `docs/abs-compatibility.md:17-25` — rewrite. It currently claims the check happens
  "on first API call", which is false today; this change makes something like it
  true.
- `docs/configuration.md` — document the two new keys as CLI-managed state.
- README Commands table — no change; no verb or flag is added or removed.

## Portability to grimoire-cli

grimoire-cli has the identical design in a worse form: its token lasts 30 days with
no refresh, so login is at most monthly, and its window is a single point
(1.5.6 to 1.5.6), so every upstream release trips the untested branch. Upstream
there does not treat patch releases as behaviour-preserving — 1.5.6 shipped both a
new feature and a fix for a database-destroying bug.

Everything above ports except the endpoint and the interval. The single-point window
tripping on every release is **correct behaviour** given the wording above
("untested, check for a newer CLI"), not a bug to design around. Given that
project's release cadence, a shorter interval is defensible there — but it stays a
hardcoded constant, not a config key.
