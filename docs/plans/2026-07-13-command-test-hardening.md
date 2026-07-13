# Command Test-Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add command tests for seven under-tested command areas; for the logic-heavy ones (Upload, Items, Config) extract inline client-side validation into pure, unit-testable helpers — with zero behavior change.

**Architecture:** Extract each inline validation check into an `internal static` helper returning `string?` (`null` = valid; else the exact error message the command already prints). The `SetAction` calls the helper and, on non-null, does `_logger.Error(msg); Environment.Exit(1);` — identical behavior. Thin commands get help/structure tests only.

**Tech Stack:** C# / .NET, System.CommandLine, xUnit.

**Spec:** `docs/specs/2026-07-13-command-test-hardening-design.md`

**Conventions:** No unnecessary blank lines in method bodies. `dotnet format AbsCli.sln` before each commit. Conventional Commits, imperative lowercase no period; NO `Co-Authored-By`, NO "Generated with Claude Code". Do NOT edit `CHANGELOG.md`. No README/coverage-doc changes (no user-visible surface change).

**Invariant for all refactor tasks:** the extracted helper must return the EXACT message string the command currently prints, and the branch order must be preserved, so the first-failing message is unchanged.

---

## File Structure

New test files:
- `tests/AbsCli.Tests/Commands/UploadCommandTests.cs`
- `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs`
- `tests/AbsCli.Tests/Commands/ConfigCommandTests.cs`
- `tests/AbsCli.Tests/Commands/LibrariesCommandTests.cs`
- `tests/AbsCli.Tests/Commands/BackupCommandTests.cs`
- `tests/AbsCli.Tests/Commands/SearchCommandTests.cs`
- `tests/AbsCli.Tests/Commands/MetadataCommandTests.cs`

Modified (refactor only):
- `src/AbsCli/Commands/UploadCommand.cs`
- `src/AbsCli/Commands/ItemsCommand.cs`
- `src/AbsCli/Commands/ConfigCommand.cs`

---

## Task 1: UploadCommand — extract validation helpers + tests

**Files:** Modify `src/AbsCli/Commands/UploadCommand.cs`; create `tests/AbsCli.Tests/Commands/UploadCommandTests.cs`.

- [ ] **Step 1: Write the failing tests**

Create `tests/AbsCli.Tests/Commands/UploadCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using AbsCli.Models;
using Xunit;

namespace AbsCli.Tests.Commands;

public class UploadCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(UploadCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Theory]
    [InlineData(null, null, null, 1, false)]              // files only -> ok
    [InlineData(null, null, "m.json", 0, false)]          // manifest only -> ok
    [InlineData("S1", "1", null, 1, false)]               // series+sequence+files -> ok
    public void ValidateUploadArgs_Valid_ReturnsNull(string? series, string? sequence, string? manifest, int fileCount, bool prefix)
    {
        Assert.Null(UploadCommand.ValidateUploadArgs(series, sequence, manifest, fileCount, prefix));
    }

    [Fact]
    public void ValidateUploadArgs_SequenceWithoutSeries()
    {
        Assert.Equal("--sequence requires --series.",
            UploadCommand.ValidateUploadArgs(null, "1", null, 1, false));
    }

    [Fact]
    public void ValidateUploadArgs_SequenceEmpty()
    {
        Assert.Equal("--sequence must be a non-empty string.",
            UploadCommand.ValidateUploadArgs("S1", "   ", null, 1, false));
    }

    [Fact]
    public void ValidateUploadArgs_FilesAndManifestExclusive()
    {
        Assert.Equal("--files and --files-manifest are mutually exclusive.",
            UploadCommand.ValidateUploadArgs(null, null, "m.json", 2, false));
    }

    [Fact]
    public void ValidateUploadArgs_PrefixAndManifestExclusive()
    {
        Assert.Equal("--prefix-source-dir and --files-manifest are mutually exclusive.",
            UploadCommand.ValidateUploadArgs(null, null, "m.json", 0, true));
    }

    [Fact]
    public void ValidateUploadArgs_NeitherSource()
    {
        Assert.Equal("Pass --files <path>... or --files-manifest <path|->.",
            UploadCommand.ValidateUploadArgs(null, null, null, 0, false));
    }

    [Fact]
    public void ValidateManifestEntries_NullOrEmpty()
    {
        var expected = "Manifest is empty or null. Provide a non-empty array of {src, as} entries.";
        Assert.Equal(expected, UploadCommand.ValidateManifestEntries(null));
        Assert.Equal(expected, UploadCommand.ValidateManifestEntries(new List<UploadManifestEntry>()));
    }

    [Fact]
    public void ValidateManifestEntries_MissingField()
    {
        var entries = new List<UploadManifestEntry> { new() { Src = "a.mp3", TargetName = "" } };
        Assert.Equal("Manifest entry missing 'src' or 'as'. Each entry must have both.",
            UploadCommand.ValidateManifestEntries(entries));
    }

    [Fact]
    public void ValidateManifestEntries_Valid()
    {
        var entries = new List<UploadManifestEntry> { new() { Src = "a.mp3", TargetName = "01.mp3" } };
        Assert.Null(UploadCommand.ValidateManifestEntries(entries));
    }

    [Fact]
    public void DetectDuplicates_NoneReturnsNull()
    {
        var list = new List<(string, string)> { ("/a/1.mp3", "1.mp3"), ("/a/2.mp3", "2.mp3") };
        Assert.Null(UploadCommand.DetectDuplicates(list));
    }

    [Fact]
    public void DetectDuplicates_CaseInsensitiveCollision()
    {
        var list = new List<(string, string)> { ("/a/1.mp3", "Track.mp3"), ("/b/1.mp3", "track.mp3") };
        var msg = UploadCommand.DetectDuplicates(list);
        Assert.NotNull(msg);
        Assert.Contains("Duplicate filenames", msg);
    }

    [Fact]
    public void Upload_Help_ShowsUploadPermissionAndOptions()
    {
        var output = RenderHelp("upload");
        Assert.Contains("Permission required:", output);
        Assert.Contains("upload", output);
        Assert.Contains("--files", output);
        Assert.Contains("--files-manifest", output);
    }
}
```

Note: confirm `UploadManifestEntry`'s property names are `Src` and `TargetName` (they are — used in `UploadCommand.BuildFromManifestAsync`). `DetectDuplicates` takes `IReadOnlyList<(string LocalPath, string UploadName)>`; the test passes `List<(string,string)>` which is assignable.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AbsCli.Tests --filter UploadCommandTests`
Expected: FAIL (helpers don't exist — compile error).

- [ ] **Step 3: Extract the helpers in `UploadCommand.cs`**

(a) Replace the five inline validation `if` blocks in the upload action (currently `if (sequence != null && series == null) { ... }` through the `if (manifestPath == null && files.Length == 0) { ... }`) with:

```csharp
            var argError = ValidateUploadArgs(series, sequence, manifestPath, files.Length, prefixSourceDir);
            if (argError != null)
            {
                _logger.Error(argError);
                Environment.Exit(1);
                return 1;
            }
```

(b) In `BuildFromManifestAsync`, replace the `if (entries == null || entries.Count == 0) { ... }` block AND the per-entry `foreach (... ) { if (string.IsNullOrWhiteSpace(...)) { ... } }` validation with a single call BEFORE building the result list:

```csharp
        var entryError = ValidateManifestEntries(entries);
        if (entryError != null)
        {
            _logger.Error(entryError);
            Environment.Exit(1);
        }
        var result = new List<(string LocalPath, string UploadName)>(entries!.Count);
        foreach (var entry in entries)
        {
            result.Add((entry.Src, entry.TargetName));
        }
        return result;
```

(c) Replace the `CheckForDuplicates` method with `DetectDuplicates` returning `string?`, and update its call site (`CheckForDuplicates(uploadList);`) to:

```csharp
            var dupError = DetectDuplicates(uploadList);
            if (dupError != null)
            {
                _logger.Error(dupError);
                Environment.Exit(1);
            }
```

(d) Add the three helpers to the class:

```csharp
    /// <summary>
    /// Validates the upload argument combination. Returns null when valid,
    /// otherwise the error message. Branch order matches the historical inline
    /// checks so the first-failing message is unchanged.
    /// </summary>
    internal static string? ValidateUploadArgs(string? series, string? sequence, string? manifestPath, int fileCount, bool prefixSourceDir)
    {
        if (sequence != null && series == null)
            return "--sequence requires --series.";
        if (sequence != null && string.IsNullOrWhiteSpace(sequence))
            return "--sequence must be a non-empty string.";
        if (manifestPath != null && fileCount > 0)
            return "--files and --files-manifest are mutually exclusive.";
        if (manifestPath != null && prefixSourceDir)
            return "--prefix-source-dir and --files-manifest are mutually exclusive.";
        if (manifestPath == null && fileCount == 0)
            return "Pass --files <path>... or --files-manifest <path|->.";
        return null;
    }

    /// <summary>
    /// Validates deserialized manifest entries. Returns null when valid,
    /// otherwise the error message.
    /// </summary>
    internal static string? ValidateManifestEntries(List<UploadManifestEntry>? entries)
    {
        if (entries == null || entries.Count == 0)
            return "Manifest is empty or null. Provide a non-empty array of {src, as} entries.";
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Src) || string.IsNullOrWhiteSpace(entry.TargetName))
                return "Manifest entry missing 'src' or 'as'. Each entry must have both.";
        }
        return null;
    }

    /// <summary>
    /// Returns a multi-line warning if any upload filenames collide
    /// (case-insensitive), otherwise null.
    /// </summary>
    internal static string? DetectDuplicates(IReadOnlyList<(string LocalPath, string UploadName)> uploadList)
    {
        var groups = uploadList
            .GroupBy(e => e.UploadName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        if (groups.Count == 0)
            return null;
        var lines = new List<string> { "Duplicate filenames in upload — ABS would silently overwrite:" };
        foreach (var group in groups)
        {
            lines.Add($"  \"{group.Key}\" maps to {group.Count()} source files:");
            foreach (var entry in group)
            {
                lines.Add($"    {entry.LocalPath}");
            }
        }
        lines.Add("");
        lines.Add("Pass --prefix-source-dir to prefix each upload filename with its parent");
        lines.Add("directory name, or --files-manifest <path> for explicit per-file naming.");
        return string.Join("\n", lines);
    }
```

Delete the old `CheckForDuplicates` method (its body is now in `DetectDuplicates`). Keep the `using` directives; `UploadManifestEntry` is in `AbsCli.Models` (already imported).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests --filter UploadCommandTests`
Expected: PASS.

- [ ] **Step 5: Confirm no regression in the full suite**

Run: `dotnet test AbsCli.sln`
Expected: all pass (no existing test broken by the refactor).

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/UploadCommand.cs tests/AbsCli.Tests/Commands/UploadCommandTests.cs
git commit -m "refactor: extract upload validation into testable helpers, add tests"
```

---

## Task 2: ItemsCommand — extract input-source validation + tests

**Files:** Modify `src/AbsCli/Commands/ItemsCommand.cs`; create `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs`.

- [ ] **Step 1: Write the failing tests**

Create `tests/AbsCli.Tests/Commands/ItemsCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ItemsCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ItemsCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void ValidateInputSource_Stdin_Ok()
    {
        Assert.Null(ItemsCommand.ValidateInputSource(null, stdin: true, inputIsExistingFile: false));
    }

    [Fact]
    public void ValidateInputSource_ExistingFile_Ok()
    {
        Assert.Null(ItemsCommand.ValidateInputSource("body.json", stdin: false, inputIsExistingFile: true));
    }

    [Fact]
    public void ValidateInputSource_InputNotAFile()
    {
        Assert.Equal("--input must be a file path (got '{\"x\":1}'). For inline JSON, pipe via --stdin.",
            ItemsCommand.ValidateInputSource("{\"x\":1}", stdin: false, inputIsExistingFile: false));
    }

    [Fact]
    public void ValidateInputSource_NeitherProvided()
    {
        Assert.Equal("Provide --input <file> or --stdin",
            ItemsCommand.ValidateInputSource(null, stdin: false, inputIsExistingFile: false));
    }

    [Fact]
    public void Items_HasBaseVerbs()
    {
        var verbs = ItemsCommand.Create().Subcommands.Select(c => c.Name).ToList();
        foreach (var v in new[] { "list", "get", "update", "batch-update", "batch-get", "delete", "batch-delete", "scan" })
            Assert.Contains(v, verbs);
    }

    [Fact]
    public void ItemsUpdate_RequiresUpdatePermission()
    {
        var output = RenderHelp("items", "update");
        Assert.Contains("Permission required:", output);
        Assert.Contains("update", output);
    }

    [Fact]
    public void ItemsScan_RequiresAdminPermission()
    {
        var output = RenderHelp("items", "scan");
        Assert.Contains("Permission required:", output);
        Assert.Contains("admin", output);
    }

    [Fact]
    public void ItemsList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("items", "list"));
    }
}
```

Note: the exact `--input must be a file path (got '<value>')...` message interpolates the input value — the test passes `{"x":1}` as the value, matching the format string.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AbsCli.Tests --filter ItemsCommandTests`
Expected: FAIL (`ValidateInputSource` doesn't exist).

- [ ] **Step 3: Extract the helper and apply it**

Add the helper to `ItemsCommand`:

```csharp
    /// <summary>
    /// Resolves the update/batch input source. Returns null when valid,
    /// otherwise the error message. <paramref name="inputIsExistingFile"/> is
    /// the caller's <c>File.Exists(input)</c> result; batch verbs that never
    /// pre-checked existence pass <c>true</c> so only the "neither provided"
    /// branch applies to them.
    /// </summary>
    internal static string? ValidateInputSource(string? input, bool stdin, bool inputIsExistingFile)
    {
        if (stdin)
            return null;
        if (input != null)
            return inputIsExistingFile ? null : $"--input must be a file path (got '{input}'). For inline JSON, pipe via --stdin.";
        return "Provide --input <file> or --stdin";
    }
```

In the `update` action, replace the `if (stdin) { ... } else if (input != null) { if (!File.Exists(input)) {...} ... } else { ... }` block with:

```csharp
            var stdin = parseResult.GetValue(stdinOption);
            var inputError = ValidateInputSource(input, stdin, input != null && File.Exists(input));
            if (inputError != null)
            {
                _logger.Error(inputError);
                Environment.Exit(1);
                return 1;
            }
            string jsonBody = stdin
                ? await Console.In.ReadToEndAsync(cancellationToken)
                : CommandHelper.ReadJsonInput(input!);
```

In the `batch-update` and `batch-get` actions, replace their `if (stdin) ... else if (input != null) ... else { _logger.Error("Provide --input <file> or --stdin"); ... }` blocks with (these verbs never pre-checked file existence — pass `true`):

```csharp
            var inputError = ValidateInputSource(input, stdin, inputIsExistingFile: true);
            if (inputError != null)
            {
                _logger.Error(inputError);
                Environment.Exit(1);
                return 1;
            }
            string jsonBody = stdin
                ? await Console.In.ReadToEndAsync()
                : CommandHelper.ReadJsonInput(input!);
```

Preserve each action's existing `stdin`/`input` variable retrieval (the `parseResult.GetValue(...)` lines) — only the validation+read block changes. Confirm `CommandHelper.ReadJsonInput` is the method used (it is, in the current code).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests --filter ItemsCommandTests`
Expected: PASS.

- [ ] **Step 5: No-regression check**

Run: `dotnet test AbsCli.sln`
Expected: all pass.

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ItemsCommand.cs tests/AbsCli.Tests/Commands/ItemsCommandTests.cs
git commit -m "refactor: extract items input-source validation, add base-verb tests"
```

---

## Task 3: ConfigCommand — extract set handler + tests

**Files:** Modify `src/AbsCli/Commands/ConfigCommand.cs`; create `tests/AbsCli.Tests/Commands/ConfigCommandTests.cs`.

- [ ] **Step 1: Write the failing tests**

Create `tests/AbsCli.Tests/Commands/ConfigCommandTests.cs`:

```csharp
using System.CommandLine;
using AbsCli.Commands;
using AbsCli.Configuration;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ConfigCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(ConfigCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void ApplyConfigSet_Server()
    {
        var config = new AppConfig();
        Assert.Null(ConfigCommand.ApplyConfigSet(config, "server", "https://abs.example.com"));
        Assert.Equal("https://abs.example.com", config.Server);
    }

    [Fact]
    public void ApplyConfigSet_DefaultLibrary()
    {
        var config = new AppConfig();
        Assert.Null(ConfigCommand.ApplyConfigSet(config, "defaultLibrary", "lib_abc"));
        Assert.Equal("lib_abc", config.DefaultLibrary);
    }

    [Fact]
    public void ApplyConfigSet_UnknownKey_ReturnsErrorAndLeavesConfig()
    {
        var config = new AppConfig { Server = "orig" };
        var err = ConfigCommand.ApplyConfigSet(config, "bogus", "x");
        Assert.Equal("Unknown config key: 'bogus'. Valid keys: server, defaultLibrary", err);
        Assert.Equal("orig", config.Server);
    }

    [Fact]
    public void Config_HasGetAndSet()
    {
        var verbs = ConfigCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "get", "set" }, verbs);
    }

    [Fact]
    public void ConfigSet_Help_ShowsPositionalArgs()
    {
        var output = RenderHelp("config", "set");
        Assert.Contains("key", output);
        Assert.Contains("value", output);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AbsCli.Tests --filter ConfigCommandTests`
Expected: FAIL (`ApplyConfigSet` doesn't exist).

- [ ] **Step 3: Extract the helper**

Add to `ConfigCommand`:

```csharp
    /// <summary>
    /// Applies a config key/value onto <paramref name="config"/>. Returns null
    /// on success, otherwise the error message for an unknown key.
    /// </summary>
    internal static string? ApplyConfigSet(AppConfig config, string key, string value)
    {
        switch (key)
        {
            case "server":
                config.Server = value;
                return null;
            case "defaultLibrary":
                config.DefaultLibrary = value;
                return null;
            default:
                return $"Unknown config key: '{key}'. Valid keys: server, defaultLibrary";
        }
    }
```

Replace the `switch (key) { ... }` block in the `set` action with:

```csharp
            var error = ApplyConfigSet(config, key, value);
            if (error != null)
            {
                _logger.Error(error);
                Environment.Exit(1);
                return 1;
            }
            configManager.Save(config);
            Console.Error.WriteLine($"Set {key} = {value}");
            return 0;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AbsCli.Tests --filter ConfigCommandTests`
Expected: PASS.

- [ ] **Step 5: No-regression check**

Run: `dotnet test AbsCli.sln`
Expected: all pass.

- [ ] **Step 6: Format and commit**

```bash
dotnet format AbsCli.sln
git add src/AbsCli/Commands/ConfigCommand.cs tests/AbsCli.Tests/Commands/ConfigCommandTests.cs
git commit -m "refactor: extract config set handler, add config command tests"
```

---

## Task 4: LibrariesCommand — help/structure tests

**Files:** Create `tests/AbsCli.Tests/Commands/LibrariesCommandTests.cs` (no production change).

- [ ] **Step 1: Write the tests**

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class LibrariesCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(LibrariesCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Libraries_HasListGetScan()
    {
        var verbs = LibrariesCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "list", "get", "scan" }, verbs);
    }

    [Fact]
    public void LibrariesScan_RequiresAdminPermission()
    {
        var output = RenderHelp("libraries", "scan");
        Assert.Contains("Permission required:", output);
        Assert.Contains("admin", output);
    }

    [Fact]
    public void LibrariesList_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("libraries", "list"));
    }

    [Fact]
    public void LibrariesGet_RequiresId()
    {
        Assert.Contains("--id", RenderHelp("libraries", "get"));
    }
}
```

- [ ] **Step 2: Run**

Run: `dotnet test tests/AbsCli.Tests --filter LibrariesCommandTests`
Expected: PASS (LibrariesCommand already exists with these verbs — verify `list`/`get`/`scan` order; if it differs, fix the test's expected array to match `LibrariesCommand.Create()`).

- [ ] **Step 3: Commit**

```bash
dotnet format AbsCli.sln
git add tests/AbsCli.Tests/Commands/LibrariesCommandTests.cs
git commit -m "test: add libraries command tests"
```

---

## Task 5: BackupCommand — help/structure tests

**Files:** Create `tests/AbsCli.Tests/Commands/BackupCommandTests.cs`.

- [ ] **Step 1: Write the tests**

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class BackupCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(BackupCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Backup_HasExpectedSubcommands()
    {
        var verbs = BackupCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "create", "list", "apply", "download", "delete", "upload" }, verbs);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("list")]
    [InlineData("apply")]
    [InlineData("download")]
    [InlineData("delete")]
    [InlineData("upload")]
    public void BackupSubcommands_RequireAdmin(string sub)
    {
        var output = RenderHelp("backup", sub);
        Assert.Contains("Permission required:", output);
        Assert.Contains("admin", output);
    }

    [Fact]
    public void BackupDownload_RequiresIdAndOutput()
    {
        var output = RenderHelp("backup", "download");
        Assert.Contains("--id", output);
        Assert.Contains("--output", output);
    }
}
```

- [ ] **Step 2: Run**

Run: `dotnet test tests/AbsCli.Tests --filter BackupCommandTests`
Expected: PASS (verify subcommand order matches `BackupCommand.Create()`; adjust expected array if needed).

- [ ] **Step 3: Commit**

```bash
dotnet format AbsCli.sln
git add tests/AbsCli.Tests/Commands/BackupCommandTests.cs
git commit -m "test: add backup command tests"
```

---

## Task 6: SearchCommand — help/structure tests

**Files:** Create `tests/AbsCli.Tests/Commands/SearchCommandTests.cs`. (`search` is a single leaf top-level command, no subcommands.)

- [ ] **Step 1: Write the tests**

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class SearchCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(SearchCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Search_Help_ShowsOptions()
    {
        var output = RenderHelp("search");
        Assert.Contains("--query", output);
        Assert.Contains("--library", output);
        Assert.Contains("--limit", output);
    }

    [Fact]
    public void Search_HasNoPermissionSection()
    {
        Assert.DoesNotContain("Permission required:", RenderHelp("search"));
    }

    [Fact]
    public void Search_QueryIsRequired()
    {
        // Parsing without --query yields a parse error.
        var root = new RootCommand();
        root.Subcommands.Add(SearchCommand.Create());
        var result = root.Parse(new[] { "search" });
        Assert.NotEmpty(result.Errors);
    }
}
```

- [ ] **Step 2: Run**

Run: `dotnet test tests/AbsCli.Tests --filter SearchCommandTests`
Expected: PASS. (If the required-option parse error assertion behaves differently in this System.CommandLine version, adjust to assert the error only surfaces on invoke; keep the two help assertions regardless.)

- [ ] **Step 3: Commit**

```bash
dotnet format AbsCli.sln
git add tests/AbsCli.Tests/Commands/SearchCommandTests.cs
git commit -m "test: add search command tests"
```

---

## Task 7: MetadataCommand — help/structure tests

**Files:** Create `tests/AbsCli.Tests/Commands/MetadataCommandTests.cs`.

- [ ] **Step 1: Write the tests**

```csharp
using System.CommandLine;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class MetadataCommandTests
{
    private static string RenderHelp(params string[] path)
    {
        var root = new RootCommand();
        root.Subcommands.Add(MetadataCommand.Create());
        root.UseCustomHelpSections();
        var output = new StringWriter();
        var config = new InvocationConfiguration { Output = output };
        var args = path.Concat(new[] { "--help-full" }).ToArray();
        root.Parse(args).Invoke(config);
        return output.ToString();
    }

    [Fact]
    public void Metadata_HasSearchProvidersCovers()
    {
        var verbs = MetadataCommand.Create().Subcommands.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "search", "providers", "covers" }, verbs);
    }

    [Fact]
    public void MetadataSearch_Help_ShowsProviderAndTitle()
    {
        var output = RenderHelp("metadata", "search");
        Assert.Contains("--provider", output);
        Assert.Contains("--title", output);
        Assert.Contains("--author", output);
    }

    [Fact]
    public void MetadataCovers_Help_ShowsProviderAndTitle()
    {
        var output = RenderHelp("metadata", "covers");
        Assert.Contains("--provider", output);
        Assert.Contains("--title", output);
    }
}
```

- [ ] **Step 2: Run**

Run: `dotnet test tests/AbsCli.Tests --filter MetadataCommandTests`
Expected: PASS (verify subcommand order matches `MetadataCommand.Create()`; adjust if needed).

- [ ] **Step 3: Commit**

```bash
dotnet format AbsCli.sln
git add tests/AbsCli.Tests/Commands/MetadataCommandTests.cs
git commit -m "test: add metadata command tests"
```

---

## Task 8: Full verification (incl. smoke regression check)

- [ ] **Step 1: Full unit test run**

Run: `dotnet test AbsCli.sln`
Expected: all pass, including the seven new test classes.

- [ ] **Step 2: Format check (matches CI)**

Run: `dotnet format AbsCli.sln --verify-no-changes`
Expected: no changes. If it fails, `dotnet format AbsCli.sln` and commit as `chore: fix formatting`.

- [ ] **Step 3: Smoke regression check**

Per CLAUDE.md, against the compose stack — this confirms the Upload/Items/Config refactors did not regress the live paths (NO new smoke assertions were added):
```bash
cd docker && docker compose up -d
IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
ABS_URL=http://$IP:80 bash docker/seed.sh
ABS_URL=http://$IP:80 bash docker/smoke-test.sh
```
Expected: all pass, exit 0 (same pass count as before this branch). Only mark "smoke passed" after seeing it.

---

## Self-Review Notes (author checklist — completed during planning)

- **Spec coverage:** Upload (Task 1: ValidateUploadArgs / ValidateManifestEntries / DetectDuplicates), Items base verbs (Task 2: ValidateInputSource + help), Config (Task 3: ApplyConfigSet + help), thin four (Tasks 4-7: help/structure), smoke regression (Task 8). No README/coverage/CHANGELOG edits, per spec.
- **Behavior-preservation invariant:** every extracted helper returns the exact original message; branch order preserved; the action still `_logger.Error(...) + Environment.Exit(1)` on non-null. The batch verbs pass `inputIsExistingFile: true` to preserve their historical no-existence-check behavior.
- **Placeholder scan:** none; all test code and edits are concrete.
- **Type consistency:** helper signatures match their call sites and tests (`ValidateUploadArgs(series, sequence, manifestPath, fileCount, prefixSourceDir)`, `ValidateManifestEntries(List<UploadManifestEntry>?)`, `DetectDuplicates(IReadOnlyList<(string,string)>)`, `ValidateInputSource(input, stdin, inputIsExistingFile)`, `ApplyConfigSet(AppConfig, key, value)`).
- **Verify expected subcommand-order arrays** against each `Create()` at implementation time (Libraries/Backup/Metadata/Config) — noted inline in those tasks.
