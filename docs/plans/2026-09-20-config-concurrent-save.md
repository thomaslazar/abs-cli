# Concurrent Config Saves Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop concurrent `abs-cli` invocations from corrupting `~/.abs-cli/config.json`, by giving each writer its own staging file instead of a shared `config.json.tmp`.

**Architecture:** `ConfigManager.Save` already does write-then-rename, and the rename is atomic. The defect is that every process stages through the same fixed path, so concurrent writers tear each other's bytes *before* the rename publishes the damage. The fix is a per-process staging name plus cleanup on failure. A second, smaller change softens the invalid-config error, which currently recommends deleting a file that usually still holds a valid refresh token.

**Tech Stack:** C# / .NET 10, xUnit.

**Spec:** `docs/specs/2026-09-20-config-concurrent-save-design.md`
**Issue:** https://github.com/thomaslazar/abs-cli/issues/88

---

## Context for the implementer

You are working in `abs-cli`, a thin CLI wrapper over the Audiobookshelf HTTP API.

- **Run `dotnet format AbsCli.sln` after every C# edit.** CI fails on
  `dotnet format --verify-no-changes`. No gratuitous blank lines inside method bodies.
- **Conventional Commits** (`type: subject`, imperative, lowercase, no trailing period).
  No `Co-Authored-By:` and no "Generated with Claude Code" attribution — hard repo rule.
- **Do not touch `README.md` or `CHANGELOG.md`.** No verb, flag or user-visible behavior
  changes here, and the changelog belongs to the release process.
- Full unit suite: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj`

Read `src/AbsCli/Configuration/ConfigManager.cs` before starting. Note that
`Save` is already careful — it creates the directory, copies the Unix file mode from the
existing config, and renames rather than writing in place. **Keep all of that.** The only
thing wrong with it is the staging path.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs` | Modify (append) | Two new tests: no tearing under concurrency, no staging leftovers |
| `src/AbsCli/Configuration/ConfigManager.cs` | Modify (`Save`, `Load`) | Per-process staging file; reworded parse error |
| `docs/specs/2026-09-18-upload-streaming-design.md` | Modify (one line) | Correct the access-token lifetime |

---

### Task 1: Per-process staging file

**Files:**
- Test: `tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs` (append two tests)
- Modify: `src/AbsCli/Configuration/ConfigManager.cs` (`Save`)

- [ ] **Step 1: Write the failing tests**

Append these two tests to the existing `ConfigManagerTests` class (it already has a
`_tempDir` field initialized in the constructor — use it):

```csharp
    [Fact]
    public void Save_ConcurrentWriters_NeverLeaveATornFile()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        // Alternating sizes are what make a tear visible: a short write over a
        // long one leaves the long document's tail behind, which is exactly the
        // trailing-brace corruption reported in #88.
        var longConfig = new AppConfig
        {
            Server = "https://long.example.com",
            AccessToken = new string('a', 2000),
            RefreshToken = new string('r', 2000),
            DefaultLibrary = new string('l', 2000)
        };
        var shortConfig = new AppConfig { Server = "https://s.co" };
        for (int round = 0; round < 20; round++)
        {
            Parallel.For(0, 16, i => manager.Save(i % 2 == 0 ? longConfig : shortConfig));
            // Load() throws InvalidOperationException on a torn file.
            var loaded = manager.Load();
            if (loaded.Server == longConfig.Server)
                Assert.Equal(2000, loaded.AccessToken!.Length);
            else
                Assert.Equal(shortConfig.Server, loaded.Server);
        }
    }

    [Fact]
    public void Save_LeavesNoStagingFilesBehind()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        manager.Save(new AppConfig { Server = "https://example.com" });
        manager.Save(new AppConfig { Server = "https://example.com", AccessToken = "tok" });
        var names = Directory.GetFiles(_tempDir).Select(Path.GetFileName).ToArray();
        Assert.Equal(new[] { "config.json" }, names);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter ConfigManagerTests
```

Expected: `Save_ConcurrentWriters_NeverLeaveATornFile` FAILS, with an
`InvalidOperationException` whose message contains "Config file is not valid JSON"
(possibly wrapped by `Parallel.For` in an `AggregateException`).

**This red step is the whole point of the task — a concurrency test that passes before
the fix proves nothing.** If it passes on the first run, the interleaving is not being
provoked. Do NOT proceed and do NOT weaken the test. Instead raise the pressure —
more rounds, more parallelism, larger size gap — and report what you tried. If it still
will not go red, report back with status BLOCKED rather than implementing against a
test you never saw fail.

(`Save_LeavesNoStagingFilesBehind` passes before and after: the old fixed `.tmp` is
consumed by the rename too. It is a guard for the new unique name, not a red test.)

- [ ] **Step 3: Make the staging file per-process**

In `src/AbsCli/Configuration/ConfigManager.cs`, replace the body of `Save` after the
`json` line. Keep the directory creation and the Unix-mode copy exactly as they are:

```csharp
        var json = JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);
        // Write-then-rename through a staging file unique to this writer. A truncated
        // config.json costs a re-login: it holds the only copy of the refresh token.
        // The name must be unique — concurrent invocations sharing one .tmp truncate
        // and overwrite each other's bytes there, and the rename then publishes the
        // damage as a valid-looking replace (#88).
        var tmpPath = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmpPath, json);
            if (!OperatingSystem.IsWindows() && File.Exists(_configPath))
                File.SetUnixFileMode(tmpPath, File.GetUnixFileMode(_configPath));
            File.Move(tmpPath, _configPath, overwrite: true);
        }
        catch
        {
            // The old fixed name was reclaimed by the next run; a unique one would
            // otherwise accumulate orphans in ~/.abs-cli/.
            try { File.Delete(tmpPath); } catch { /* best effort */ }
            throw;
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter ConfigManagerTests
```

Expected: PASS, all tests in the class including the pre-existing
`Save_ReplacesAtomically_SoAnOpenReaderStillSeesWholeOldFile`.

Then run the filtered command **three more times** and confirm it passes every time. A
concurrency test that passes once may still be flaky, and this one is meant to be a
permanent guard. If any run fails after the fix, report it with the failure output rather
than retrying until it goes green — a real residual race in `Save` would show up exactly this way.

- [ ] **Step 5: Run the full suite and format**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet format AbsCli.sln
```

Expected: all tests pass; `dotnet format --verify-no-changes` exits 0.

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Configuration/ConfigManager.cs tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs
git commit -m "fix: give each config writer its own staging file"
```

---

### Task 2: Soften the invalid-config error

**Files:**
- Modify: `src/AbsCli/Configuration/ConfigManager.cs` (`Load`)

The current message leads with the destructive option:

```
Config file is not valid JSON: <path> — delete it and run 'abs-cli login'. (<parser message>)
```

In the reported case the tokens were intact and only the brace structure was damaged, so
repair restores the session while deleting discards a valid 30-day refresh token.

- [ ] **Step 1: Reword the message**

Replace the `throw new InvalidOperationException(...)` inside the `catch (JsonException ex)`
block in `Load`:

```csharp
                // Every command loads the config, so a raw parser message here is the
                // only thing the operator ever sees. Lead with repair: this file holds
                // the only copy of the refresh token, and deleting it to fix a stray
                // byte throws away a valid 30-day session.
                throw new InvalidOperationException(
                    $"Config file is not valid JSON: {_configPath} ({ex.Message}). " +
                    "It holds the only copy of your refresh token — inspect and repair it " +
                    "before deleting. If it is unrecoverable, delete it and run 'abs-cli login'.",
                    ex);
```

The existing test `Load_ThrowsActionableError_WhenFileIsNotJson` asserts the message
contains the config path and `abs-cli login`; both are still present, so it keeps passing.
Do not change that test.

- [ ] **Step 2: Build, test and format**

```bash
dotnet build AbsCli.sln
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet format AbsCli.sln
```

Expected: build succeeds, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Configuration/ConfigManager.cs
git commit -m "fix: lead with repair, not deletion, on an unreadable config"
```

---

### Task 3: Correct the access-token lifetime in the prior spec

**Files:**
- Modify: `docs/specs/2026-09-18-upload-streaming-design.md`

That spec argues the unreplayable-401 path is nearly unreachable because "ABS access
tokens last ~12 hours". The real default is **1 hour**
(`ACCESS_TOKEN_EXPIRY`, `temp/audiobookshelf/server/auth/TokenManager.js:16-21`), which
makes the path reachable for any request running longer than ~59 minutes — well within
range for the multi-GB uploads that spec enabled.

- [ ] **Step 1: Find the claim**

```bash
grep -n "12 hours" docs/specs/2026-09-18-upload-streaming-design.md
```

It appears in the section about the unreplayable 401 retry.

- [ ] **Step 2: Correct it**

Rewrite that sentence so it states the 1-hour default and reverses the conclusion — the
path is reachable on long uploads rather than requiring an implausible 12-hour request.
Keep it to the same one or two sentences; do not restructure the section. Cite the
constant's location (`TokenManager.js:16-21`) the way the surrounding prose cites sources.

Leave the rest of that spec alone. It is a historical design record and everything else
in it is still accurate.

- [ ] **Step 3: Commit**

```bash
git add docs/specs/2026-09-18-upload-streaming-design.md
git commit -m "docs: correct the ABS access-token lifetime to one hour"
```

---

### Task 4: Verify against a live server

**Files:** none — verification only.

Per the repo's pre-PR rule, `docker/smoke-test.sh` must pass before any PR is opened.
It exercises `login`, `config set` and the version-check cadence, all of which write the
config through the changed `Save` path.

- [ ] **Step 1: Bring up a clean stack**

```bash
cd docker && docker compose down -v && docker compose up -d
```

Wait for the healthcheck:

```bash
curl -sf http://docker-audiobookshelf-1.orb.local:80/healthcheck && echo up
```

- [ ] **Step 2: Seed it**

```bash
ABS_URL=http://docker-audiobookshelf-1.orb.local:80 bash docker/seed.sh
```

- [ ] **Step 3: Run the smoke test**

```bash
ABS_URL=http://docker-audiobookshelf-1.orb.local:80 bash docker/smoke-test.sh
```

`smoke-test.sh` is **not idempotent** — it creates and deletes library items, so a second
run against the same stack fails on item-count assertions. Those failures are artifacts of
a dirty stack, not regressions. To re-run, repeat Steps 1-3 from `docker compose down -v`.

- [ ] **Step 4: Report the result**

Report the actual outcome — pass or fail, with the failing output if it failed. Do not
mark the smoke as passed in a PR description without having run it.

---

## Done when

- `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj` passes, including the two new
  `ConfigManagerTests`.
- `dotnet format AbsCli.sln --verify-no-changes` is clean.
- `docker/smoke-test.sh` passes against a freshly seeded stack.
- `grep -n '"\.tmp"' src/AbsCli/Configuration/ConfigManager.cs` finds no fixed staging name.

## Handled outside this plan (controller, not a task)

- Comment on #88 with the real root cause, and retitle it to drop "non-atomic".
- Comment on #89 correcting the access-token lifetime to 1 hour.
