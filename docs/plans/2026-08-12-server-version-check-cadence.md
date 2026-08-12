# Server version check cadence — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Check the ABS server version at most once per 24 hours on any command, instead of only at login, and warn with an actionable message naming both versions.

**Architecture:** `AbsApiClient` gains a `PreflightAsync()` that runs the existing token check plus a new once-per-process version check. When the stored `lastVersionCheck` timestamp is older than 24h, an unauthenticated `GET /status` probe reads `serverVersion`; the verdict is produced by a pure `VersionWarning` function and persisted through a read-modify-write of the on-disk config. Probe failures are swallowed and do not advance the timestamp.

**Tech Stack:** C# / .NET 10, System.CommandLine 2.0.7, System.Text.Json source-generated contexts (Native AOT), xUnit, NLog.

**Spec:** `docs/specs/2026-08-12-server-version-check-cadence-design.md`

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `src/AbsCli/Configuration/AppConfig.cs` | Two new persisted fields | Modify |
| `src/AbsCli/Configuration/ConfigManager.cs` | `UpdateVersionCheck` read-modify-write | Modify |
| `src/AbsCli/Models/ServerStatus.cs` | DTO for `GET /status` | Create |
| `src/AbsCli/Models/JsonContext.cs` | Register `ServerStatus` for AOT | Modify |
| `src/AbsCli/Api/ApiEndpoints.cs` | `Status` endpoint constant | Modify |
| `src/AbsCli/Api/AbsApiClient.cs` | `ShouldCheckVersion`, `VersionWarning`, `RecordServerVersion`, probe, `PreflightAsync` | Modify |
| `src/AbsCli/Commands/LoginCommand.cs` | Route login's free version through the recorder | Modify |
| `src/AbsCli/Commands/ConfigCommand.cs` | Surface the two fields in `config get` | Modify |
| `src/AbsCli/Commands/SelfTestCommand.cs` | `ServerStatus` round-trip; update version check | Modify |
| `tests/AbsCli.Tests/Api/VersionComparisonTests.cs` | Rename the `CheckServerVersion` test | Modify |
| `tests/AbsCli.Tests/Api/VersionCheckCadenceTests.cs` | `ShouldCheckVersion` + `VersionWarning` | Create |
| `tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs` | `UpdateVersionCheck` behaviour | Modify |
| `docker/smoke-test.sh` | Probe-happens / probe-skipped assertions | Modify |
| `docs/abs-compatibility.md` | Rewrite the Runtime Version Check section | Modify |
| `docs/configuration.md` | Document the two new keys | Modify |

**Naming contract used by every task below** (do not diverge):

- `AppConfig.LastVersionCheck` → JSON `lastVersionCheck`, type `DateTimeOffset?`
- `AppConfig.LastServerVersion` → JSON `lastServerVersion`, type `string?`
- `ConfigManager.UpdateVersionCheck(string? serverVersion, DateTimeOffset checkedAt)`
- `AbsApiClient.VersionCheckInterval` → `TimeSpan`, 24 hours
- `AbsApiClient.ShouldCheckVersion(DateTimeOffset? lastCheck, DateTimeOffset now)` → `bool`
- `AbsApiClient.VersionWarning(string observed, string? previous)` → `string?` (null = in range)
- `AbsApiClient.RecordServerVersion(string? observed)` → `void`
- `ApiEndpoints.Status` → `"status"`
- `ServerStatus.ServerVersion` → JSON `serverVersion`

**Note on `CheckServerVersion`:** this plan **replaces** it with the pure `VersionWarning` plus `RecordServerVersion`. Task 5 removes it and updates its two existing callers (`SelfTestCommand`, `VersionComparisonTests`). Do not leave a dead wrapper behind.

---

## Task 1: Config fields and `UpdateVersionCheck`

**Files:**
- Modify: `src/AbsCli/Configuration/AppConfig.cs:17`
- Modify: `src/AbsCli/Configuration/ConfigManager.cs:39` (after `Save`)
- Test: `tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs` (inside the existing class):

```csharp
    [Fact]
    public void UpdateVersionCheck_WritesBothFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"abs-cli-test-{Guid.NewGuid()}.json");
        var manager = new ConfigManager(path);
        var checkedAt = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        manager.UpdateVersionCheck("2.38.0", checkedAt);
        var reloaded = manager.Load();
        Assert.Equal("2.38.0", reloaded.LastServerVersion);
        Assert.Equal(checkedAt, reloaded.LastVersionCheck);
        File.Delete(path);
    }

    [Fact]
    public void UpdateVersionCheck_PreservesExistingFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"abs-cli-test-{Guid.NewGuid()}.json");
        var manager = new ConfigManager(path);
        manager.Save(new AppConfig
        {
            Server = "https://file.example.com",
            AccessToken = "file-token",
            RefreshToken = "file-refresh",
            DefaultLibrary = "lib-1"
        });
        manager.UpdateVersionCheck("2.38.0", DateTimeOffset.UtcNow);
        var reloaded = manager.Load();
        Assert.Equal("https://file.example.com", reloaded.Server);
        Assert.Equal("file-token", reloaded.AccessToken);
        Assert.Equal("file-refresh", reloaded.RefreshToken);
        Assert.Equal("lib-1", reloaded.DefaultLibrary);
        File.Delete(path);
    }

    [Fact]
    public void UpdateVersionCheck_DoesNotPersistEnvValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"abs-cli-test-{Guid.NewGuid()}.json");
        var manager = new ConfigManager(path);
        manager.Save(new AppConfig { Server = "https://file.example.com" });
        // Resolve() merges env into memory; UpdateVersionCheck must ignore that and
        // rewrite only what is on disk, so an env token never reaches the file.
        var resolved = manager.Resolve(envLookup: key => key switch
        {
            "ABS_TOKEN" => "env-secret",
            "ABS_SERVER" => "https://env.example.com",
            _ => null
        });
        Assert.Equal("env-secret", resolved.AccessToken);
        manager.UpdateVersionCheck("2.38.0", DateTimeOffset.UtcNow);
        var reloaded = manager.Load();
        Assert.Null(reloaded.AccessToken);
        Assert.Equal("https://file.example.com", reloaded.Server);
        File.Delete(path);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~ConfigManagerTests"`
Expected: compile error — `'ConfigManager' does not contain a definition for 'UpdateVersionCheck'`, and `AppConfig` has no `LastServerVersion` / `LastVersionCheck`.

- [ ] **Step 3: Add the two `AppConfig` fields**

In `src/AbsCli/Configuration/AppConfig.cs`, after the `DefaultLibrary` property:

```csharp
    // Written by the runtime version check, not by `config set`. See
    // docs/specs/2026-08-12-server-version-check-cadence-design.md.
    [JsonPropertyName("lastVersionCheck")]
    public DateTimeOffset? LastVersionCheck { get; set; }

    [JsonPropertyName("lastServerVersion")]
    public string? LastServerVersion { get; set; }
```

- [ ] **Step 4: Add `UpdateVersionCheck`**

In `src/AbsCli/Configuration/ConfigManager.cs`, immediately after `Save`:

```csharp
    /// <summary>
    /// Persist the version-check state by rewriting only the on-disk config.
    /// Deliberately re-reads from disk instead of taking a resolved
    /// <see cref="AppConfig"/>: <see cref="Resolve"/> merges environment
    /// variables into memory, so saving that would write an ABS_TOKEN the
    /// operator kept out of the file.
    /// </summary>
    public void UpdateVersionCheck(string? serverVersion, DateTimeOffset checkedAt)
    {
        var onDisk = Load();
        onDisk.LastServerVersion = serverVersion;
        onDisk.LastVersionCheck = checkedAt;
        Save(onDisk);
    }
```

- [ ] **Step 4b: Thread both fields through `Resolve()`**

Found during review of the first implementation attempt, and load-bearing for Task 5:
`Resolve()` builds a fresh `AppConfig` and would otherwise leave both fields `null`
no matter what is on disk. Since Task 5 reads `_config.LastVersionCheck` to decide
whether the window has lapsed, always-null means "never checked" and the CLI would
probe on **every invocation** — silently defeating the design. It also stops
`RefreshTokenAsync`'s `Save(_config)` from zeroing the state on disk.

In the object initializer inside `Resolve()`, alongside `RefreshToken = fileConfig.RefreshToken`:

```csharp
            LastVersionCheck = fileConfig.LastVersionCheck,
            LastServerVersion = fileConfig.LastServerVersion,
```

Both come from the file only — there is no flag or env override for either. Cover it
with a test that `UpdateVersionCheck` → `Resolve()` round-trips both values, and one
proving a `Save()` of a resolved config preserves them.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~ConfigManagerTests"`
Expected: PASS, all tests in the class.

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Configuration/AppConfig.cs src/AbsCli/Configuration/ConfigManager.cs tests/AbsCli.Tests/Configuration/ConfigManagerTests.cs
git commit -m "feat: persist server version check state in config"
```

---

## Task 2: `ShouldCheckVersion` staleness decision

**Files:**
- Modify: `src/AbsCli/Api/AbsApiClient.cs` (next to `MinSupportedVersion` / `MaxTestedVersion`)
- Test: `tests/AbsCli.Tests/Api/VersionCheckCadenceTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `tests/AbsCli.Tests/Api/VersionCheckCadenceTests.cs`:

```csharp
using AbsCli.Api;

namespace AbsCli.Tests.Api;

public class VersionCheckCadenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldCheckVersion_NeverChecked_ReturnsTrue()
    {
        Assert.True(AbsApiClient.ShouldCheckVersion(null, Now));
    }

    [Fact]
    public void ShouldCheckVersion_InsideWindow_ReturnsFalse()
    {
        Assert.False(AbsApiClient.ShouldCheckVersion(Now.AddHours(-23), Now));
    }

    [Fact]
    public void ShouldCheckVersion_AtBoundary_ReturnsTrue()
    {
        Assert.True(AbsApiClient.ShouldCheckVersion(Now.AddHours(-24), Now));
    }

    [Fact]
    public void ShouldCheckVersion_OutsideWindow_ReturnsTrue()
    {
        Assert.True(AbsApiClient.ShouldCheckVersion(Now.AddHours(-25), Now));
    }

    [Fact]
    public void ShouldCheckVersion_TimestampInFuture_ReturnsTrue()
    {
        // Clock moved backwards — treat as stale rather than trusting it.
        Assert.True(AbsApiClient.ShouldCheckVersion(Now.AddHours(1), Now));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~VersionCheckCadenceTests"`
Expected: compile error — `'AbsApiClient' does not contain a definition for 'ShouldCheckVersion'`.

- [ ] **Step 3: Implement**

In `src/AbsCli/Api/AbsApiClient.cs`, directly below the `MinSupportedVersion` / `MaxTestedVersion` fields:

```csharp
    internal static readonly TimeSpan VersionCheckInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether the server version is due for a re-check. A timestamp in the
    /// future means the clock moved backwards, which counts as stale.
    /// </summary>
    internal static bool ShouldCheckVersion(DateTimeOffset? lastCheck, DateTimeOffset now)
        => lastCheck is null
           || now - lastCheck.Value >= VersionCheckInterval
           || lastCheck.Value > now;
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~VersionCheckCadenceTests"`
Expected: PASS, 5 tests.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/AbsApiClient.cs tests/AbsCli.Tests/Api/VersionCheckCadenceTests.cs
git commit -m "feat: add 24h staleness decision for the server version check"
```

---

## Task 3: `VersionWarning` message function

**Files:**
- Modify: `src/AbsCli/Api/AbsApiClient.cs` (replaces `CheckServerVersion`, currently at `:249-276`)
- Test: `tests/AbsCli.Tests/Api/VersionCheckCadenceTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside `VersionCheckCadenceTests`:

```csharp
    [Fact]
    public void VersionWarning_InRange_ReturnsNull()
    {
        Assert.Null(AbsApiClient.VersionWarning("2.36.0", previous: null));
    }

    [Fact]
    public void VersionWarning_AboveCeiling_NamesBothVersions()
    {
        var warning = AbsApiClient.VersionWarning("2.38.0", previous: null);
        Assert.NotNull(warning);
        Assert.Contains("2.38.0", warning);
        Assert.Contains("2.36.0", warning);
        Assert.Contains("Check for a newer abs-cli", warning);
    }

    [Fact]
    public void VersionWarning_AboveCeilingAfterChange_NamesTheChange()
    {
        var warning = AbsApiClient.VersionWarning("2.38.0", previous: "2.36.0");
        Assert.NotNull(warning);
        Assert.Contains("moved from ABS 2.36.0 to 2.38.0", warning);
    }

    [Fact]
    public void VersionWarning_SameVersionAsBefore_DoesNotClaimAChange()
    {
        var warning = AbsApiClient.VersionWarning("2.38.0", previous: "2.38.0");
        Assert.NotNull(warning);
        Assert.DoesNotContain("moved from", warning);
    }

    [Fact]
    public void VersionWarning_BelowFloor_MentionsMinimum()
    {
        var warning = AbsApiClient.VersionWarning("2.30.0", previous: null);
        Assert.NotNull(warning);
        Assert.Contains("2.30.0", warning);
        Assert.Contains("older than the minimum supported version", warning);
    }

    [Fact]
    public void VersionWarning_NonNumericVersion_DoesNotThrow()
    {
        AbsApiClient.VersionWarning("2.36.0-beta", previous: null);
        AbsApiClient.VersionWarning("v2.36.0", previous: null);
        AbsApiClient.VersionWarning("nightly", previous: null);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~VersionCheckCadenceTests"`
Expected: compile error — `'AbsApiClient' does not contain a definition for 'VersionWarning'`.

- [ ] **Step 3: Replace `CheckServerVersion` with `VersionWarning`**

In `src/AbsCli/Api/AbsApiClient.cs`, delete the whole `CheckServerVersion` method (`public static void CheckServerVersion(string? version)`, currently `:249-276`) and put this in its place:

```csharp
    /// <summary>
    /// The warning to show for an observed server version, or null when it sits
    /// inside the tested range. Pure so the wording is unit-testable; the caller
    /// decides whether to log it. <paramref name="previous"/> is the last version
    /// this install saw, used to name the change when the server has moved.
    /// </summary>
    internal static string? VersionWarning(string observed, string? previous)
    {
        var moved = previous != null && previous != observed
            ? $"This server moved from ABS {previous} to {observed} since the last check. "
            : "";

        if (CompareVersions(observed, MinSupportedVersion) < 0)
        {
            return $"{moved}ABS server version {observed} is older than the minimum supported version ({MinSupportedVersion}). Some features may not work.";
        }
        if (CompareVersions(observed, MaxTestedVersion) > 0)
        {
            return $"{moved}abs-cli {ClientVersion} was tested up to ABS {MaxTestedVersion}; this server is {observed}. Check for a newer abs-cli.";
        }
        return null;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj --filter "FullyQualifiedName~VersionCheckCadenceTests"`
Expected: PASS, 11 tests. The build will still fail overall — `SelfTestCommand`, `LoginCommand` and `VersionComparisonTests` still reference the deleted `CheckServerVersion`; Task 4 and Task 5 fix those. To keep this task's commit green, do Step 5 before committing.

- [ ] **Step 5: Update the three remaining `CheckServerVersion` callers**

In `tests/AbsCli.Tests/Api/VersionComparisonTests.cs`, replace the test named `CheckServerVersion_DoesNotThrow_OnNonNumericVersion` (its cases now live in `VersionCheckCadenceTests`) by deleting it, and update the class comment — it currently says `CheckServerVersion logs`, which is no longer true:

```csharp
// Kept in the NLog collection: this class exercises version comparison, and any
// future test here that makes production code log must not run in parallel with
// the log-asserting tests. See PR #74.
[Collection("NLog")]
public class VersionComparisonTests
```

In `src/AbsCli/Commands/SelfTestCommand.cs`, replace the body of the `"Non-numeric version does not throw"` check:

```csharp
            Check("Non-numeric version does not throw", () =>
            {
                AbsApiClient.VersionWarning("2.36.0-beta", null);
                AbsApiClient.VersionWarning("v2.36.0", null);
                AbsApiClient.VersionWarning("nightly", null);
            });
```

In `src/AbsCli/Commands/LoginCommand.cs:101`, temporarily delete the line
`AbsApiClient.CheckServerVersion(loginResponse.ServerSettings?.Version);` — Task 5
replaces it with the recorder call.

- [ ] **Step 6: Verify the whole suite builds and passes**

Run: `dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj`
Expected: PASS, 0 failed.

- [ ] **Step 7: Format and commit**

```bash
dotnet format AbsCli.sln
git add -A src tests
git commit -m "refactor: replace CheckServerVersion with a pure VersionWarning"
```

---

## Task 4: `ServerStatus` DTO and the `/status` endpoint

**Files:**
- Create: `src/AbsCli/Models/ServerStatus.cs`
- Modify: `src/AbsCli/Models/JsonContext.cs:6` (add near the other registrations)
- Modify: `src/AbsCli/Api/ApiEndpoints.cs:6` (after `AuthRefresh`)
- Modify: `src/AbsCli/Commands/SelfTestCommand.cs` (API Response DTOs section)

- [ ] **Step 1: Create the DTO**

`src/AbsCli/Models/ServerStatus.cs`:

```csharp
using System.Text.Json.Serialization;

namespace AbsCli.Models;

/// <summary>
/// GET /status — unauthenticated, and the only field we need is the version.
/// ABS also returns app, isInit, language and auth settings; ignored here.
/// </summary>
public class ServerStatus
{
    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; set; }
}
```

- [ ] **Step 2: Register it for AOT**

In `src/AbsCli/Models/JsonContext.cs`, add below `[JsonSerializable(typeof(LoginRequest))]`:

```csharp
[JsonSerializable(typeof(ServerStatus))]
```

- [ ] **Step 3: Add the endpoint constant**

In `src/AbsCli/Api/ApiEndpoints.cs`, directly after the `AuthRefresh` line:

```csharp
    // Unauthenticated, and outside /api — used by the runtime version check.
    public const string Status = "status";
```

- [ ] **Step 4: Add the self-test round-trip check**

In `src/AbsCli/Commands/SelfTestCommand.cs`, in the `=== API Response DTOs (source-generated) ===` section, after the `LibraryListResponse round-trip` check:

```csharp
            Check("ServerStatus round-trip", () =>
            {
                var obj = new ServerStatus { ServerVersion = "2.36.0" };
                var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.ServerStatus);
                var back = JsonSerializer.Deserialize(json, AppJsonContext.Default.ServerStatus)!;
                Assert(back.ServerVersion == "2.36.0", $"version mismatch: {back.ServerVersion}");
            });
```

- [ ] **Step 5: Verify**

Run: `dotnet build AbsCli.sln && dotnet run --project src/AbsCli -- self-test 2>&1 | grep "ServerStatus round-trip"`
Expected: `PASS: ServerStatus round-trip`

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Models/ServerStatus.cs src/AbsCli/Models/JsonContext.cs src/AbsCli/Api/ApiEndpoints.cs src/AbsCli/Commands/SelfTestCommand.cs
git commit -m "feat: add ServerStatus DTO for the /status version probe"
```

---

## Task 5: The recorder, the probe, and pre-flight wiring

**Files:**
- Modify: `src/AbsCli/Api/AbsApiClient.cs` (nine `EnsureValidTokenAsync()` call sites at `:63, 79, 96, 113, 130, 139, 150, 166, 175`; new methods near `EnsureValidTokenAsync` at `:189`)
- Modify: `src/AbsCli/Commands/LoginCommand.cs:101`

- [ ] **Step 1: Add the recorder and the probe**

In `src/AbsCli/Api/AbsApiClient.cs`, add a field beside the other private fields at the top of the class:

```csharp
    private bool _versionCheckDone;
```

Then add these three methods directly above `EnsureValidTokenAsync`:

```csharp
    /// <summary>
    /// Runs before every request. The token check runs every time — a long
    /// command can cross a token expiry mid-run — while the version check is
    /// once per process.
    /// </summary>
    private async Task PreflightAsync()
    {
        await EnsureValidTokenAsync();
        await EnsureVersionCheckedAsync();
    }

    private async Task EnsureVersionCheckedAsync()
    {
        if (_versionCheckDone) return;
        _versionCheckDone = true;
        if (!ShouldCheckVersion(_config.LastVersionCheck, DateTimeOffset.UtcNow))
        {
            _logger.Debug($"server version checked at {_config.LastVersionCheck:u}, inside the {VersionCheckInterval.TotalHours}h window");
            return;
        }
        // Distinct line so tests can tell a probe apart from login recording the
        // version it already had.
        _logger.Debug("version check due, probing /status");
        var observed = await ProbeServerVersionAsync();
        if (observed != null)
            RecordServerVersion(observed);
    }

    /// <summary>
    /// Reads the version from the unauthenticated /status endpoint. Returns null
    /// on any failure: this is a diagnostic and must never be the thing that
    /// fails the command the caller actually asked for. A failure deliberately
    /// leaves the stored timestamp alone so the next invocation retries.
    /// </summary>
    private async Task<string?> ProbeServerVersionAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _http.GetAsync(ApiEndpoints.Status, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Debug($"version probe returned {(int)response.StatusCode}, skipping");
                return null;
            }
            var json = await response.Content.ReadAsStringAsync(cts.Token);
            var status = JsonSerializer.Deserialize(json, AppJsonContext.Default.ServerStatus);
            if (string.IsNullOrEmpty(status?.ServerVersion))
            {
                _logger.Debug("version probe returned no serverVersion, skipping");
                return null;
            }
            return status.ServerVersion;
        }
        catch (Exception ex)
        {
            _logger.Debug($"version probe failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Warn (once per check) and persist. Shared by the probe and by the login
    /// path, which gets the version for free in its response.
    /// </summary>
    internal void RecordServerVersion(string? observed)
    {
        if (string.IsNullOrEmpty(observed)) return;
        var warning = VersionWarning(observed, _config.LastServerVersion);
        if (warning != null)
            _logger.Warn(warning);
        else
            _logger.Debug($"server version {observed} (in tested range {MinSupportedVersion}-{MaxTestedVersion})");
        try
        {
            _configManager.UpdateVersionCheck(observed, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            // An unwritable config just means we check again next time.
            _logger.Debug($"could not persist version check state: {ex.Message}");
        }
    }
```

- [ ] **Step 2: Replace all nine token-check call sites**

In `src/AbsCli/Api/AbsApiClient.cs`, replace every occurrence of `await EnsureValidTokenAsync();` **inside the public request methods** with `await PreflightAsync();`. There are nine, at lines `63, 79, 96, 113, 130, 139, 150, 166, 175`. Do **not** touch the call inside `PreflightAsync` itself.

Verify the count:

```bash
grep -c "await PreflightAsync();" src/AbsCli/Api/AbsApiClient.cs   # expect 9
grep -c "await EnsureValidTokenAsync();" src/AbsCli/Api/AbsApiClient.cs   # expect 1
```

- [ ] **Step 3: Route the login path through the recorder**

In `src/AbsCli/Commands/LoginCommand.cs`, where the removed `CheckServerVersion` call was (immediately after `configManager.Save(config);` at `:100`), add:

```csharp
                client.RecordServerVersion(loginResponse.ServerSettings?.Version);
```

If the local variable holding the `AbsApiClient` is not named `client`, use whatever
name is already in scope — do not construct a second client.

- [ ] **Step 4: Verify the suite and the AOT binary**

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
dotnet run --project src/AbsCli -- self-test 2>&1 | tail -3
```
Expected: tests PASS with 0 failed; self-test `0 failed`.

- [ ] **Step 5: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Api/AbsApiClient.cs src/AbsCli/Commands/LoginCommand.cs
git commit -m "feat: check the server version at most daily instead of only at login"
```

---

## Task 6: Surface the state in `config get`

**Files:**
- Modify: `src/AbsCli/Commands/ConfigCommand.cs:27-33` (the `display` dictionary)

Note: the subcommand is `config get`, not `config show`.

- [ ] **Step 1: Add the two entries**

In the `display` dictionary, after the `defaultLibrary` entry and before `configPath`:

```csharp
                ["lastVersionCheck"] = config.LastVersionCheck?.ToString("u") ?? "(never)",
                ["lastServerVersion"] = config.LastServerVersion ?? "(unknown)",
```

- [ ] **Step 2: Verify**

Run: `dotnet run --project src/AbsCli -- config get`
Expected: JSON on stdout containing `lastVersionCheck` and `lastServerVersion` keys.

- [ ] **Step 3: Commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ConfigCommand.cs
git commit -m "feat: show version check state in config get"
```

---

## Task 7: Smoke assertions

**Files:**
- Modify: `docker/smoke-test.sh` (append a section before the trailing `Results:` summary block)

- [ ] **Step 1: Add the section**

The helpers `pass` and `fail` are defined at `docker/smoke-test.sh:39-40`. Two
constraints this block has to respect:

1. The suite authenticates with `abs_login root root` (`:83-86`) and does **not**
   export `ABS_TOKEN`, so these runs must log in themselves.
2. Login records the version from its own response, so a command run straight after
   login is legitimately *inside* the window. Testing the probe path therefore needs
   the timestamp backdated.

A temp `HOME` isolates all of it from the suite's real config:

```bash
echo ""
echo "=== Runtime Version Check ==="

VC_HOME=$(mktemp -d)
VC_CONFIG="$VC_HOME/.abs-cli/config.json"

# Login records the version it already has, without probing.
HOME="$VC_HOME" $CLI login --server "$ABS_URL" --username root --password-stdin <<<"root" >/dev/null 2>&1
if python3 -c "import json,sys; d=json.load(open('$VC_CONFIG')); sys.exit(0 if d.get('lastServerVersion') and d.get('lastVersionCheck') else 1)" 2>/dev/null; then
    pass "version check: login records version and timestamp"
else
    fail "version check: login records version and timestamp" "lastServerVersion/lastVersionCheck missing from $VC_CONFIG"
fi

# Immediately afterwards the check is inside the window, so no probe.
vc_fresh=$(HOME="$VC_HOME" ABS_DEBUG=1 $CLI libraries list 2>&1 >/dev/null || true)
if echo "$vc_fresh" | grep -q "inside the 24h window"; then
    pass "version check: skipped inside the 24h window"
else
    fail "version check: skipped inside the 24h window" "expected the skip debug line"
fi
if echo "$vc_fresh" | grep -q "probing /status"; then
    fail "version check: no probe inside the window" "probed despite a fresh timestamp"
else
    pass "version check: no probe inside the window"
fi

# Backdate the timestamp two days: the next command must probe.
python3 -c "
import json
p = '$VC_CONFIG'
d = json.load(open(p))
d['lastVersionCheck'] = '2026-01-01T00:00:00+00:00'
json.dump(d, open(p, 'w'))
"
vc_stale=$(HOME="$VC_HOME" ABS_DEBUG=1 $CLI libraries list 2>&1 >/dev/null || true)
if echo "$vc_stale" | grep -q "probing /status"; then
    pass "version check: probes once the window lapses"
else
    fail "version check: probes once the window lapses" "expected 'probing /status' in debug output"
fi
if python3 -c "import json,sys; d=json.load(open('$VC_CONFIG')); sys.exit(0 if not d['lastVersionCheck'].startswith('2026-01-01') else 1)" 2>/dev/null; then
    pass "version check: timestamp advances after a probe"
else
    fail "version check: timestamp advances after a probe" "lastVersionCheck still backdated"
fi

rm -rf "$VC_HOME"
```

- [ ] **Step 2: Run the smoke test**

The stack must be freshly seeded — `smoke-test.sh` is not idempotent:

```bash
cd docker && docker compose down -v && docker compose up -d && cd ..
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
until curl -sf "http://$IP:80/healthcheck" >/dev/null; do sleep 1; done
ABS_URL=http://$IP:80 bash docker/seed.sh
ABS_URL=http://$IP:80 bash docker/smoke-test.sh 2>&1 | tail -20
```
Expected: `0 failed`, including the three new assertions.

- [ ] **Step 3: Commit**

```bash
git add docker/smoke-test.sh
git commit -m "test: assert version check cadence in the smoke suite"
```

---

## Task 8: Docs

**Files:**
- Modify: `docs/abs-compatibility.md:17-25`
- Modify: `docs/configuration.md:7-13` and its config-file example

- [ ] **Step 1: Rewrite the compatibility section**

Replace the `## Runtime Version Check` section body in `docs/abs-compatibility.md` with:

```markdown
## Runtime Version Check

The CLI reads the server version at most once every 24 hours, from the
unauthenticated `GET /status` endpoint, on whichever command runs first after the
window lapses. `login` uses the version already in its own response instead of
probing. The result is stored in `~/.abs-cli/config.json` as `lastVersionCheck`
and `lastServerVersion`.

If the version is outside the known-compatible range, a warning goes to stderr:

- **Newer than tested:** `abs-cli 1.0.3 was tested up to ABS 2.36.0; this server is 2.38.0. Check for a newer abs-cli.`
- **Older than supported:** `ABS server version 2.30.0 is older than the minimum supported version (2.33.1). Some features may not work.`

When the version changed since the last check, the warning says so:
`This server moved from ABS 2.36.0 to 2.38.0 since the last check.`

Warnings only — the CLI does not refuse to run. A failed probe is silent and does
not update the timestamp, so the next invocation retries.
```

- [ ] **Step 2: Document the config keys**

In `docs/configuration.md`, extend the config-file example with the two keys and add a note under it:

```json
{
  "server": "https://audiobookshelf.example.com",
  "accessToken": "eyJhbG...",
  "refreshToken": "eyJhbG...",
  "defaultLibrary": "f59e4771-a301-4dc0-a521-bbfa2d256c00",
  "lastVersionCheck": "2026-08-12T10:00:00Z",
  "lastServerVersion": "2.36.0"
}
```

```markdown
`lastVersionCheck` and `lastServerVersion` are CLI-managed state for the runtime
version check (see `docs/abs-compatibility.md`). They are not settable via
`config set`.
```

- [ ] **Step 3: Commit**

```bash
git add docs/abs-compatibility.md docs/configuration.md
git commit -m "docs: document the 24h runtime version check"
```

---

## Task 9: Final verification and PR

- [ ] **Step 1: Commit the spec and plan**

Per `CLAUDE.md`, spec and plan land on the implementation branch with the code:

```bash
git add docs/specs/2026-08-12-server-version-check-cadence-design.md docs/plans/2026-08-12-server-version-check-cadence.md
git commit -m "docs: add server version check cadence spec and plan"
```

- [ ] **Step 2: Full local verification**

```bash
dotnet format AbsCli.sln --verify-no-changes
dotnet test AbsCli.sln
dotnet publish src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true -o ./publish
./publish/abs-cli self-test
```
Expected: format clean; tests `0 failed`; self-test `0 failed`.

- [ ] **Step 3: Full smoke on a freshly seeded stack**

```bash
cd docker && docker compose down -v && docker compose up -d && cd ..
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
until curl -sf "http://$IP:80/healthcheck" >/dev/null; do sleep 1; done
ABS_URL=http://$IP:80 bash docker/seed.sh
CLI=./publish/abs-cli ABS_URL=http://$IP:80 bash docker/smoke-test.sh 2>&1 | tail -10
cd docker && docker compose down -v && cd .. && rm -rf publish/
```
Expected: `0 failed`.

- [ ] **Step 4: Open the PR**

Branch name: `feat/version-check-cadence`. Substitute the real counts from Steps 2-3
into the body — do not claim the smoke test passed unless it did.

```bash
git push -u origin feat/version-check-cadence
gh pr create --title "feat: check the server version daily instead of only at login" --base main --body "$(cat <<'BODY'
Implements `docs/specs/2026-08-12-server-version-check-cadence-design.md`.

## Why

`CheckServerVersion` ran from exactly one place — the login path — and nothing about a login correlates with the server changing. These are self-hosted servers on `:latest`; the version changes when the image is pulled, an event involving no login. Tokens persist and refresh (ABS access tokens last 1h, refresh tokens 30 days), so a working install can go months without a fresh login and never see a warning. That happened: a live stack was updated and went unnoticed for weeks.

## What changed

- `PreflightAsync()` replaces the nine `EnsureValidTokenAsync()` call sites. The token check still runs per request (a long command can cross an expiry mid-run); the version check is once per process.
- When `lastVersionCheck` is older than 24h, an unauthenticated `GET /status` probe reads `serverVersion`. 3s timeout of its own, so a hung server cannot stall the command behind a diagnostic.
- `CheckServerVersion` is replaced by a pure `VersionWarning(observed, previous)` returning the text or null, so the wording is unit-testable. The ceiling message now names the CLI version, the tested ceiling and the running server, and says to check for a newer `abs-cli`; when the version moved since the last check, it says so.
- State persists to `lastVersionCheck` / `lastServerVersion` via a read-modify-write of the **on-disk** config, not `Save(_config)` — `Resolve()` merges env vars into memory, so saving that would write an `ABS_TOKEN` the operator kept out of the file.
- Every probe failure path returns null and leaves the timestamp alone, so the next invocation retries.
- `login` keeps using the version from its own response instead of probing.

## Verification

- `dotnet test AbsCli.sln` — N passed, 0 failed
- `dotnet format --verify-no-changes` — clean
- AOT `self-test` — N passed, 0 failed
- `docker/smoke-test.sh` against a freshly seeded 2.36.0 stack — N passed, 0 failed, including 5 new version-check assertions
BODY
)"
```

- [ ] **Step 5: Watch CI to terminal state**

`gh pr checks <num>` until all 8 checks are terminal, then report.

---

## Notes for the implementer

- **No new CLI verb or flag**, so the README Commands table does not change.
- **Do not add a CHANGELOG entry** — that file belongs to the release process.
- **Any new test that makes production code log must be in `[Collection("NLog")]`.**
  NLog config is process-global; a stray line lands in a log-asserting test's
  `MemoryTarget` and fails it on count. This broke the v1.0.3 release CI. The tests
  in this plan are pure (`VersionWarning` returns a string rather than logging), so
  `VersionCheckCadenceTests` needs no attribute — keep it that way.
- **`item_total`-style discipline:** a diagnostic that breaks the operation is worse
  than no diagnostic. That is why every probe failure path returns null.
