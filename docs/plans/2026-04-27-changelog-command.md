# `abs-cli changelog` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a top-level `abs-cli changelog` command that prints the latest entry from the bundled `CHANGELOG.md` by default, or the full file with `--all`.

**Architecture:** `CHANGELOG.md` is embedded as an assembly manifest resource at build time. A small `ChangelogReader` static helper reads the resource and either returns it verbatim (`ReadAll`) or extracts the topmost `## ` block (`ReadLatest`). `ChangelogCommand` wires the command surface and writes the result to stdout. The command does not call `CommandHelper.BuildClient`, so it inherits the same no-auth/no-network status as `self-test`.

**Tech Stack:** C# / .NET 8 / System.CommandLine 2.0-beta4 / xUnit 2.5. Target CLI is published with `PublishAot=true`. `InternalsVisibleTo("AbsCli.Tests")` is already configured.

**Spec:** [docs/specs/2026-04-27-changelog-command.md](../specs/2026-04-27-changelog-command.md)

---

## File Structure

**New files:**

- `src/AbsCli/Services/ChangelogReader.cs` — static helper exposing `ReadAll()`, `ReadLatest()`, and `internal static string ExtractLatest(string)` (the test seam).
- `src/AbsCli/Commands/ChangelogCommand.cs` — System.CommandLine `Command` factory; defines `--all`; sets handler.
- `tests/AbsCli.Tests/Commands/ChangelogReaderTests.cs` — unit tests for `ExtractLatest`.
- `tests/AbsCli.Tests/Commands/ChangelogCommandTests.cs` — end-to-end test invoking the command via the parser.

**Modified files:**

- `src/AbsCli/AbsCli.csproj` — add `<EmbeddedResource>` for repo-root `CHANGELOG.md`.
- `src/AbsCli/Program.cs` — register `ChangelogCommand.Create()` on the root command.
- `src/AbsCli/Commands/SelfTestCommand.cs` — add a `Check` block asserting the embedded resource is present and parseable.
- `CHANGELOG.md` — add a `0.3.0` (or next-version) entry covering the new command. The version bump is part of the release process; this plan only adds the entry.
- `docs/roadmap.md` — move "abs-cli changelog command" from Ideas table to a new shipped-milestone subsection (or leave for the release commit; see Task 8).

---

## Task 1: Embed `CHANGELOG.md` as an assembly resource

Wire the build so the file ships inside the binary.

**Files:**
- Modify: `src/AbsCli/AbsCli.csproj`

- [ ] **Step 1: Add the embedded resource ItemGroup**

In `src/AbsCli/AbsCli.csproj`, add a new `<ItemGroup>` near the existing `InternalsVisibleTo` block:

```xml
  <ItemGroup>
    <EmbeddedResource Include="..\..\CHANGELOG.md" LogicalName="CHANGELOG.md" />
  </ItemGroup>
```

The `LogicalName` attribute makes the resource accessible as `"CHANGELOG.md"` rather than the default `AbsCli.CHANGELOG.md` derived from the include path.

- [ ] **Step 2: Verify the resource is embedded**

Run from `src/AbsCli/`:

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: build succeeds. Then list resources in the produced assembly:

```bash
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- --version 2>/dev/null; \
  strings /workspaces/abs-cli/src/AbsCli/bin/Debug/net8.0/abs-cli.dll | grep -E '^CHANGELOG\.md$'
```

Expected: a line `CHANGELOG.md` is printed.

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/AbsCli.csproj
git commit -m "chore: embed CHANGELOG.md as assembly resource"
```

---

## Task 2: Add failing test for `ExtractLatest` — single-entry extraction

TDD red step: tests come before any implementation.

**Files:**
- Create: `tests/AbsCli.Tests/Commands/ChangelogReaderTests.cs`

- [ ] **Step 1: Write the first failing test**

Create `tests/AbsCli.Tests/Commands/ChangelogReaderTests.cs`:

```csharp
using AbsCli.Services;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ChangelogReaderTests
{
    private const string TwoEntries =
        "# Changelog\n" +
        "\n" +
        "All notable changes are documented here.\n" +
        "\n" +
        "## 0.2.7 — 2026-04-24\n" +
        "\n" +
        "### Highlights\n" +
        "- latest highlight\n" +
        "\n" +
        "### Fixes\n" +
        "- fix: latest fix\n" +
        "\n" +
        "## 0.2.6 — 2026-04-24\n" +
        "\n" +
        "### Highlights\n" +
        "- older highlight\n";

    [Fact]
    public void ExtractLatest_ReturnsBlockStartingAtTopmostVersionHeading()
    {
        var result = ChangelogReader.ExtractLatest(TwoEntries);
        Assert.StartsWith("## 0.2.7 — 2026-04-24", result);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails to compile**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~ChangelogReaderTests"
```

Expected: build error — `The name 'ChangelogReader' does not exist`.

---

## Task 3: Implement `ChangelogReader.ExtractLatest` with the minimal logic to pass

Green step. Implement just enough to pass the test from Task 2.

**Files:**
- Create: `src/AbsCli/Services/ChangelogReader.cs`

- [ ] **Step 1: Create `ChangelogReader.cs` with stubs and the extraction algorithm**

Create `src/AbsCli/Services/ChangelogReader.cs`:

```csharp
using System.Reflection;
using System.Text;

namespace AbsCli.Services;

public static class ChangelogReader
{
    private const string ResourceName = "CHANGELOG.md";

    public static string ReadAll()
    {
        return ReadEmbeddedResource();
    }

    public static string ReadLatest()
    {
        return ExtractLatest(ReadEmbeddedResource());
    }

    internal static string ExtractLatest(string changelog)
    {
        if (string.IsNullOrEmpty(changelog))
        {
            throw new InvalidOperationException("CHANGELOG.md has no version entries");
        }

        var lines = changelog.Split('\n');
        var startIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0)
        {
            throw new InvalidOperationException("CHANGELOG.md has no version entries");
        }

        var endIndex = lines.Length;
        for (var i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                endIndex = i;
                break;
            }
        }

        var lastNonBlank = endIndex - 1;
        while (lastNonBlank > startIndex && string.IsNullOrWhiteSpace(lines[lastNonBlank]))
        {
            lastNonBlank--;
        }

        var sb = new StringBuilder();
        for (var i = startIndex; i <= lastNonBlank; i++)
        {
            sb.Append(lines[i]);
            if (i < lastNonBlank)
            {
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string ReadEmbeddedResource()
    {
        var assembly = typeof(ChangelogReader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException("CHANGELOG.md resource not embedded");
        }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
```

- [ ] **Step 2: Run the test and verify it passes**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~ChangelogReaderTests"
```

Expected: 1 passed.

- [ ] **Step 3: Commit**

```bash
git add src/AbsCli/Services/ChangelogReader.cs tests/AbsCli.Tests/Commands/ChangelogReaderTests.cs
git commit -m "feat: add ChangelogReader.ExtractLatest"
```

---

## Task 4: Cover the remaining `ExtractLatest` rules with tests

Add the rest of the unit coverage promised by the spec. Each `Fact` is one assertion; if any fails, fix `ExtractLatest` and re-run.

**Files:**
- Modify: `tests/AbsCli.Tests/Commands/ChangelogReaderTests.cs`

- [ ] **Step 1: Add the four remaining `Fact`s**

Append to `ChangelogReaderTests`:

```csharp
    [Fact]
    public void ExtractLatest_StopsBeforeNextVersionHeading()
    {
        var result = ChangelogReader.ExtractLatest(TwoEntries);
        Assert.DoesNotContain("## 0.2.6", result);
        Assert.DoesNotContain("older highlight", result);
    }

    [Fact]
    public void ExtractLatest_IncludesSubHeadingsWithinLatestEntry()
    {
        var result = ChangelogReader.ExtractLatest(TwoEntries);
        Assert.Contains("### Highlights", result);
        Assert.Contains("### Fixes", result);
        Assert.Contains("- latest highlight", result);
        Assert.Contains("- fix: latest fix", result);
    }

    [Fact]
    public void ExtractLatest_TrimsTrailingBlankLines()
    {
        var input =
            "## 1.0.0 — 2026-01-01\n" +
            "\n" +
            "body\n" +
            "\n" +
            "\n";
        var result = ChangelogReader.ExtractLatest(input);
        Assert.EndsWith("body", result);
        Assert.False(result.EndsWith("\n", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractLatest_ThrowsWhenNoVersionHeadingPresent()
    {
        var input = "# Changelog\n\nNo entries yet.\n";
        var ex = Assert.Throws<InvalidOperationException>(
            () => ChangelogReader.ExtractLatest(input));
        Assert.Equal("CHANGELOG.md has no version entries", ex.Message);
    }
```

- [ ] **Step 2: Run the tests**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~ChangelogReaderTests"
```

Expected: 5 passed.

- [ ] **Step 3: Commit**

```bash
git add tests/AbsCli.Tests/Commands/ChangelogReaderTests.cs
git commit -m "test: cover ExtractLatest stop/trim/error cases"
```

---

## Task 5: Wire `ChangelogCommand` and register it

Build the command surface; integration test follows in Task 6.

**Files:**
- Create: `src/AbsCli/Commands/ChangelogCommand.cs`
- Modify: `src/AbsCli/Program.cs`

- [ ] **Step 1: Create `ChangelogCommand.cs`**

```csharp
using System.CommandLine;
using AbsCli.Services;

namespace AbsCli.Commands;

public static class ChangelogCommand
{
    public static Command Create()
    {
        var allOption = new Option<bool>(
            "--all",
            "Print the entire CHANGELOG.md instead of just the latest entry");
        var command = new Command(
            "changelog",
            "Print release notes from the bundled CHANGELOG.md") { allOption };
        command.AddExamples(
            "abs-cli changelog",
            "abs-cli changelog --all");
        command.SetHandler((bool all) =>
        {
            var output = all ? ChangelogReader.ReadAll() : ChangelogReader.ReadLatest();
            Console.Out.WriteLine(output);
        }, allOption);
        return command;
    }
}
```

Note: do **not** call `CommandHelper.BuildClient` — this command needs neither auth nor network.

- [ ] **Step 2: Register the command in `Program.cs`**

In `src/AbsCli/Program.cs`, add the registration alongside the existing top-level commands. Place it after `SelfTestCommand` to keep utility commands together:

```csharp
rootCommand.AddCommand(SelfTestCommand.Create());
rootCommand.AddCommand(ChangelogCommand.Create());
```

- [ ] **Step 3: Build and smoke-run**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- changelog
```

Expected: stdout begins with `## 0.2.7 — 2026-04-24` (or whatever the topmost entry in the repo's `CHANGELOG.md` currently is) and contains its `### Highlights`/`### Fixes` blocks. No login prompt. Exit 0.

- [ ] **Step 4: Smoke-run `--all`**

```bash
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- changelog --all | head -3
```

Expected: first line is `# Changelog` (the file header).

- [ ] **Step 5: Commit**

```bash
git add src/AbsCli/Commands/ChangelogCommand.cs src/AbsCli/Program.cs
git commit -m "feat: add abs-cli changelog command"
```

---

## Task 6: End-to-end integration test

Verify the full command path through the parser, including the embedded resource.

**Files:**
- Create: `tests/AbsCli.Tests/Commands/ChangelogCommandTests.cs`

- [ ] **Step 1: Write the integration test**

The test reads the actual repo `CHANGELOG.md` to compute the expected first heading, then asserts the command's stdout starts with it. The repo path is resolved relative to the test assembly's `BaseDirectory` and walking up to the repo root.

```csharp
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using AbsCli.Commands;
using Xunit;

namespace AbsCli.Tests.Commands;

public class ChangelogCommandTests
{
    private static int Invoke(TestConsole console, params string[] args)
    {
        var root = new RootCommand();
        root.AddCommand(ChangelogCommand.Create());
        var parser = new CommandLineBuilder(root)
            .UseDefaults()
            .Build();
        return parser.Invoke(args, console);
    }

    private static string FirstVersionHeading()
    {
        // tests/AbsCli.Tests/bin/Debug/net8.0 -> repo root is four levels up.
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 6 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "CHANGELOG.md");
            if (File.Exists(candidate))
            {
                foreach (var line in File.ReadLines(candidate))
                {
                    if (line.StartsWith("## ", StringComparison.Ordinal))
                    {
                        return line;
                    }
                }
                throw new InvalidOperationException("Repo CHANGELOG.md has no version heading");
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not locate repo CHANGELOG.md from test base directory");
    }

    [Fact]
    public void Default_PrintsLatestEntryStartingAtTopmostHeading()
    {
        var console = new TestConsole();
        var exit = Invoke(console, "changelog");
        var stdout = console.Out.ToString() ?? "";

        Assert.Equal(0, exit);
        Assert.StartsWith(FirstVersionHeading(), stdout);
    }

    [Fact]
    public void All_PrintsFullFileStartingWithTopHeader()
    {
        var console = new TestConsole();
        var exit = Invoke(console, "changelog", "--all");
        var stdout = console.Out.ToString() ?? "";

        Assert.Equal(0, exit);
        Assert.StartsWith("# Changelog", stdout);
    }

    [Fact]
    public void All_OutputIsLongerThanDefault()
    {
        var defaultConsole = new TestConsole();
        Invoke(defaultConsole, "changelog");
        var allConsole = new TestConsole();
        Invoke(allConsole, "changelog", "--all");

        var defaultLen = (defaultConsole.Out.ToString() ?? "").Length;
        var allLen = (allConsole.Out.ToString() ?? "").Length;
        Assert.True(allLen > defaultLen,
            $"--all output ({allLen} chars) should be longer than default ({defaultLen})");
    }
}
```

- [ ] **Step 2: Run the integration tests**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln --filter "FullyQualifiedName~ChangelogCommandTests"
```

Expected: 3 passed.

- [ ] **Step 3: Commit**

```bash
git add tests/AbsCli.Tests/Commands/ChangelogCommandTests.cs
git commit -m "test: end-to-end coverage for changelog command"
```

---

## Task 7: Add a self-test check for the embedded resource

Catch a future build that silently drops the embedded file.

**Files:**
- Modify: `src/AbsCli/Commands/SelfTestCommand.cs`

- [ ] **Step 1: Add a `Check` block**

Locate the last `Check(...)` block in `SelfTestCommand.cs`'s handler. Append a new section header and check immediately after it (before the summary `Console.Error.WriteLine` lines that report `pass`/`fail`):

```csharp
            Console.Error.WriteLine();
            Console.Error.WriteLine("=== Embedded resources ===");

            Check("CHANGELOG.md embedded and parseable", () =>
            {
                var latest = AbsCli.Services.ChangelogReader.ReadLatest();
                Assert(!string.IsNullOrWhiteSpace(latest), "ReadLatest returned empty");
                Assert(latest.StartsWith("## ", StringComparison.Ordinal),
                    $"ReadLatest did not start with '## ': '{latest[..Math.Min(40, latest.Length)]}'");
            });
```

If the file already has a final summary section header, place the new section header just above it. The exact `Console.Error.WriteLine` summary lines below it must remain unchanged.

- [ ] **Step 2: Run the self-test**

```bash
dotnet run --project /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -- self-test
```

Expected: stderr includes `PASS: CHANGELOG.md embedded and parseable`. Exit 0.

- [ ] **Step 3: Run the full unit-test suite to confirm no regressions**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/AbsCli/Commands/SelfTestCommand.cs
git commit -m "test: assert CHANGELOG.md is embedded in self-test"
```

---

## Task 8: Format check + final verification

Match the project's formatting and confirm everything still builds.

**Files:** none new.

- [ ] **Step 1: Run `dotnet format`**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln
```

Expected: no errors. Either there are no diffs, or the diffs are whitespace-only.

- [ ] **Step 2: Verify formatting passes the CI check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0, no diagnostics.

- [ ] **Step 3: Build and run the full test suite once more**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Release
dotnet test /workspaces/abs-cli/AbsCli.sln -c Release
```

Expected: build succeeds; all tests pass.

- [ ] **Step 4: AOT publish smoke check**

```bash
dotnet publish /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -c Release
# Find the published binary path:
find /workspaces/abs-cli/src/AbsCli/bin/Release -name abs-cli -type f -executable | head -1
```

Then run the published binary's `changelog` and `self-test`:

```bash
PUB=$(find /workspaces/abs-cli/src/AbsCli/bin/Release -name abs-cli -type f -executable | head -1)
"$PUB" changelog | head -1
"$PUB" self-test | tail -5
```

Expected: `changelog` prints a `## ` heading; `self-test` reports the new check as PASS and exits 0.

- [ ] **Step 5: Commit any formatting fixes**

If `dotnet format` made changes, commit them:

```bash
git add -u
git commit -m "chore: dotnet format"
```

If there were no changes, skip this step.

---

## Note on `CHANGELOG.md`

`CHANGELOG.md` is **owned by the release process** (`docs/releasing.md`,
invoked via `/release` on a `release/v{version}` branch). Feature branches —
including this one — must not edit it. The release agent picks new entries
up from the commit log when v0.3.0 is cut. Any plan that includes a
"document the change in CHANGELOG.md" task is wrong; skip such steps.

---

## Self-Review

**Spec coverage check:**

- Top-level command, `--all` flag → Task 5.
- Latest-entry extraction algorithm (`## ` start, stop at next `## `, trim trailing blanks, heading included) → Tasks 3, 4.
- Raw markdown output, no JSON envelope → Task 5 handler uses `Console.Out.WriteLine`.
- Embedded resource at build, single-artifact ship → Task 1.
- `ChangelogReader.ReadAll`, `ReadLatest` → Task 3.
- Error handling (resource missing, no headings, empty file) → Task 3 implementation; Task 4 covers the no-headings unit case.
- Unit tests (5 cases) → Tasks 2, 4.
- Integration tests (parser end-to-end) → Task 6.
- Self-test smoke → Task 7.
- Login bypass open question → resolved during planning: not calling `CommandHelper.BuildClient` is sufficient, since `self-test` follows the same pattern. Noted in Task 5 Step 1.

No spec section is uncovered.

**Placeholder scan:** none. All steps include exact paths, exact commands, and full code blocks.

**Type/name consistency:**

- `ChangelogReader.ReadAll` / `ReadLatest` / `ExtractLatest` — used identically across Tasks 2-7.
- Resource logical name `"CHANGELOG.md"` — consistent in csproj (Task 1) and `ResourceName` constant (Task 3).
- Error messages `"CHANGELOG.md resource not embedded"` and `"CHANGELOG.md has no version entries"` — consistent across Task 3 implementation and Task 4 test assertion.
- `--all` option name — consistent across Tasks 5, 6, 9.
