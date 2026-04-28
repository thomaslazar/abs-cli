# General library upgrades + dependency security — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Special handling — forced human handoff at Task 1.** Task 1 asks the operator to flip two GitHub repo Settings switches (Dependabot alerts + security updates). The orchestrator MUST stop, surface the prompt, and wait for confirmation before dispatching Task 2. A subagent that hits this handoff reports `BLOCKED_ON_USER_GITHUB_SETTINGS`; do not auto-continue.

**Goal:** Bump all NuGet dependencies off long-stale pins (System.CommandLine 2.0.0-beta4 → 2.0.7, xUnit 2.5.3 → xunit.v3 3.2.2, Test SDK + coverlet to current stable), add a build-time NuGet Audit gate, and enable GitHub Dependabot vulnerability tooling — all in one PR with logically separated commits.

**Architecture:** Three concerns delivered in sequence. Concern 1 is a new repo-root `Directory.Build.props` that turns NuGet Audit warnings into errors for all three csproj. Concern 2 swaps `xunit` for `xunit.v3` and bumps siblings. Concern 3 rewrites every command file plus `HelpExtensions.cs` and `Program.cs` against System.CommandLine 2.0's new API (`CommandLineConfiguration`, `SetAction`, `ParseResult.GetValue`, action-based help). Each concern is one commit (Concern 3 is the big one and may split if its diff is unreasonable). GitHub-side, Dependabot alerts and security updates are enabled via repo Settings (no `dependabot.yml` in the branch — vuln-only policy).

**Tech Stack:** C# / .NET 10 / `System.CommandLine` 2.0.7 (target) / `xunit.v3` 3.2.2 (target) / `Microsoft.NET.Test.Sdk` + `coverlet.collector` (current stable) / NuGet Audit (built into SDK) / GitHub Dependabot (out-of-band).

**Spec:** [docs/specs/2026-04-28-library-upgrades-and-security.md](../specs/2026-04-28-library-upgrades-and-security.md)

---

## File Structure

**New files:**

- `Directory.Build.props` (repo root) — NuGet Audit policy inherited by all three csproj.

**Modified files:**

- `src/AbsCli/AbsCli.csproj` — bump `System.CommandLine` from `2.0.0-beta4.22272.1` to `2.0.7`.
- `tests/AbsCli.Tests/AbsCli.Tests.csproj` — replace `xunit` and `xunit.runner.visualstudio` (both 2.5.3) with `xunit.v3` 3.2.2 and its v3 runner; bump `Microsoft.NET.Test.Sdk` and `coverlet.collector` to current stable.
- `src/AbsCli/Program.cs` — replace `CommandLineBuilder(...).UseDefaults().UseCustomHelpSections().Build()` invocation pipeline with the SCL 2.0 `CommandLineConfiguration` model.
- `src/AbsCli/Commands/HelpExtensions.cs` — rewrite `UseCustomHelpSections` against the SCL 2.0 help model (action-based help; layout customisation moved). Public API surface that other commands use (`AddHelpSection`, `AddExamples`, `AddResponseExample<T>`, `AddResponseExample(Type, Type)`, `AddMediaUnionShapes`, `AddCommand` etc.) preserves its current signatures so command files don't need behavioural changes.
- All `src/AbsCli/Commands/*.cs` (15 files) — `command.SetHandler(...)` → `command.SetAction(parseResult => …)`, `parseResult.GetValueForOption(opt)` → `parseResult.GetValue(opt)`. Mechanical pass.
- `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs` — replace `CommandLineBuilder` usage with the 2.0 invocation model.
- `tests/AbsCli.Tests/Commands/HelpOutputTests.cs` — same.
- `tests/AbsCli.Tests/Commands/ChangelogCommandTests.cs` — same.

**Files explicitly NOT modified:**

- `CHANGELOG.md` — owned by release workflow.
- `.github/workflows/build.yml` — no CI changes; existing gates suffice.
- `.github/dependabot.yml` — not added (vuln-only policy).

---

## Task 0: Branch + spec/plan commit

**Files:** none new for this step.

- [ ] **Step 1: Confirm clean tree on main**

```bash
cd /workspaces/abs-cli
git checkout main
git pull
git status
```

Expected: `On branch main`, working tree clean (gitignored `.mempalace/` is fine).

- [ ] **Step 2: Create the branch**

```bash
git checkout -b feat/library-upgrades-and-security
```

- [ ] **Step 3: Commit the spec and plan together**

```bash
git add docs/specs/2026-04-28-library-upgrades-and-security.md docs/plans/2026-04-28-library-upgrades-and-security.md
git commit -m "docs: add spec and plan for library upgrades and dependency security"
```

---

## Task 1: GitHub Settings handoff — STOP and prompt operator

This task has no commit. The operator must enable two GitHub repo Settings switches before any code work begins. Implementation cannot proceed otherwise (the spec's whole point is that vuln coverage is live by the time bumps land).

**Files:** none.

- [ ] **Step 1: STOP — surface prompt to operator verbatim**

Output to the user:

> **GitHub repo Settings change required.** Before I do any code work on this branch, please enable these two switches at `https://github.com/thomaslazar/abs-cli/settings/security_analysis`:
>
> 1. **Dependabot alerts** → Enable
> 2. **Dependabot security updates** → Enable
>
> Both should show "Enabled" once flipped. No `dependabot.yml` is added — these settings give us alerts in the Security tab and auto-PRs only when a published advisory has a fix. Reply when both are on and I'll continue with Task 2.

If running under subagent-driven development: the implementer subagent reports `BLOCKED_ON_USER_GITHUB_SETTINGS` here. The orchestrator surfaces the prompt and does **not** dispatch Task 2 until the operator confirms.

---

## Task 2: Add NuGet Audit policy

Adds the repo-wide vuln-checking gate. If the current package set has any known vulns, this build will fail — flag immediately as a finding.

**Files:**
- Create: `Directory.Build.props` (repo root)

- [ ] **Step 1: Create `Directory.Build.props`**

At `/workspaces/abs-cli/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <WarningsAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Restore and build to confirm baseline is clean**

```bash
dotnet restore /workspaces/abs-cli/AbsCli.sln
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: build succeeds with 0 errors. `NU1901`/`NU1902`/`NU1903`/`NU1904` warnings (now promoted to errors) must be absent. If any fire, the pre-existing dep set has a known vuln — STOP and report the finding to the operator before continuing. Do not "fix" by silencing the warning code.

- [ ] **Step 3: Run unit tests**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: 103 / 103 pass.

- [ ] **Step 4: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0.

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.props
git commit -m "chore: add NuGet Audit policy"
```

---

## Task 3: Bump test packages to xUnit v3

Replaces `xunit` 2.5.3 with `xunit.v3` 3.2.2 (+ matching v3 runner) and bumps `Microsoft.NET.Test.Sdk` + `coverlet.collector` to current stable. Test source code may need adjustments per the xUnit v3 migration guide.

**Files:**
- Modify: `tests/AbsCli.Tests/AbsCli.Tests.csproj`
- Modify (likely): `tests/AbsCli.Tests/**/*.cs` if v3 migration requires it

- [ ] **Step 1: Edit `tests/AbsCli.Tests/AbsCli.Tests.csproj`**

Replace the `<ItemGroup>` containing the four package references:

```xml
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  </ItemGroup>
```

with the v3 equivalents at current stable versions. Run:

```bash
cd /workspaces/abs-cli/tests/AbsCli.Tests
dotnet remove package xunit
dotnet remove package xunit.runner.visualstudio
dotnet add package xunit.v3 --version 3.2.2
dotnet add package xunit.v3.assert --version 3.2.2
dotnet add package xunit.runner.visualstudio
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package coverlet.collector
```

The `xunit.v3.assert` package may already be a transitive of `xunit.v3` — if `dotnet add` reports it's redundant, drop that line. The `xunit.runner.visualstudio` line resolves to the latest v3-compatible runner; if NuGet picks an old v2 build, pin explicitly to whatever the xUnit v3 release notes recommend.

After the `dotnet add` commands, `tests/AbsCli.Tests/AbsCli.Tests.csproj` should have exact-version `<PackageReference>` entries for each package. Verify with:

```bash
grep -n PackageReference /workspaces/abs-cli/tests/AbsCli.Tests/AbsCli.Tests.csproj
```

Expected: each `Version` is exact (e.g. `Version="3.2.2"`, not `Version="3.*"` or floating).

- [ ] **Step 2: Restore + build**

```bash
dotnet restore /workspaces/abs-cli/AbsCli.sln
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

If build errors fire from xUnit v3 changes, fix inline:

- Namespace import differences — most v3 code keeps `using Xunit;` but a few classes moved.
- `[Theory]` data-source attribute changes if any (we don't currently use `[Theory]`).
- `Assert.*` signature changes if any (the project uses `Equal`, `True`, `False`, `Throws`, `StartsWith`, `EndsWith`, `Contains`, `DoesNotContain` — all stable across v2/v3).
- `IClassFixture<T>` patterns if any (we don't currently use them).

Reference: `https://xunit.net/docs/getting-started/v3/migration`. Apply only the migrations the build errors actually demand — don't proactively modernise tests beyond what compiling requires.

- [ ] **Step 3: Run unit tests**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: 103 / 103 pass. xUnit v3 may print a different summary line format; the count must remain 103.

- [ ] **Step 4: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: exit 0.

- [ ] **Step 5: Commit**

```bash
git add tests/AbsCli.Tests/AbsCli.Tests.csproj
# Add any test-source files that needed migration adjustments:
git add tests/AbsCli.Tests/
git commit -m "chore: bump test packages to xUnit v3"
```

---

## Task 4: Bump System.CommandLine to 2.0.7 (atomic migration)

The big one. The package bump alone breaks the build until every consumer is migrated. This task is one commit because partial migration doesn't compile. Internal sub-steps are sequenced for sanity but no intermediate commits.

**Files:**
- Modify: `src/AbsCli/AbsCli.csproj`
- Modify: `src/AbsCli/Program.cs`
- Modify: `src/AbsCli/Commands/HelpExtensions.cs`
- Modify: All 15 files matching `src/AbsCli/Commands/*.cs` (except `HelpExtensions.cs` and `ResponseExamples.g.cs`)
- Modify: `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`
- Modify: `tests/AbsCli.Tests/Commands/HelpOutputTests.cs`
- Modify: `tests/AbsCli.Tests/Commands/ChangelogCommandTests.cs`

- [ ] **Step 1: Bump the package version**

In `src/AbsCli/AbsCli.csproj`, change:

```xml
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
```

to:

```xml
<PackageReference Include="System.CommandLine" Version="2.0.7" />
```

- [ ] **Step 2: Restore and observe the failure surface**

```bash
dotnet restore /workspaces/abs-cli/AbsCli.sln
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug 2>&1 | head -50
```

Expected: many compiler errors. Read them — they tell you exactly which APIs need updating. The most common ones in this codebase will be:

- `'CommandLineBuilder' does not exist` — used in `Program.cs` and three test files.
- `'Command.SetHandler' has no equivalent overload` — used in every command file.
- `'ParseResult.GetValueForOption' is obsolete or missing` — used in `ChangelogCommand.cs` (recent) and possibly elsewhere.
- `'HelpBuilder.Default.GetLayout' / HelpSectionDelegate / HelpContext` — used in `HelpExtensions.cs`.

- [ ] **Step 3: Migrate `src/AbsCli/Program.cs`**

The current pattern:

```csharp
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using AbsCli.Commands;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

rootCommand.AddCommand(LoginCommand.Create());
// ... 12 more AddCommand calls

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseCustomHelpSections()
    .Build();

return await parser.InvokeAsync(args);
```

Migrate to the SCL 2.0 model. The exact shape depends on what the new API looks like at 2.0.7 — consult the official migration guide (`https://learn.microsoft.com/en-us/dotnet/standard/commandline/migrate-2.0-rc`) and 2.0.7 release notes. Target shape (subject to API surface):

```csharp
using System.CommandLine;
using AbsCli.Commands;

var rootCommand = new RootCommand("abs-cli — Audiobookshelf CLI");

rootCommand.Subcommands.Add(LoginCommand.Create());
// ... 12 more Subcommands.Add calls

var configuration = new CommandLineConfiguration(rootCommand);
HelpExtensions.ApplyCustomHelpSections(configuration);   // or whatever the new entrypoint is

return await rootCommand.Parse(args, configuration).InvokeAsync();
```

The `AddCommand` method may still exist as a compat alias, in which case keeping `AddCommand` is fine. Prefer the new collection-init / `Subcommands.Add` pattern only if `AddCommand` is gone.

- [ ] **Step 4: Migrate `src/AbsCli/Commands/HelpExtensions.cs`**

This is the most invasive change. The current file uses beta4's `CommandLineBuilder.UseHelp(ctx => …)` + `HelpBuilder.CustomizeLayout` + `HelpSectionDelegate` + `HelpContext`. SCL 2.0 replaces this with action-based help — a `HelpAction` (or similar) on each command, customisable via the action's hook points.

Public API the rest of the codebase calls — these signatures must remain unchanged:

- `Command.AddHelpSection(string title, params string[] lines)`
- `Command.AddHelpSection(string title, HelpSectionPosition position, params string[] lines)`
- `Command.AddExamples(params string[] examples)`
- `Command.GetExampleCount()`
- `Command.AddResponseExample<T>()`
- `Command.AddResponseExample(Type envelopeType, Type elementType)`
- `Command.AddMediaUnionShapes()`

Internal only:
- `HelpExtensions.UseCustomHelpSections(this CommandLineBuilder builder)` is the entry point that gets called from `Program.cs`. Replace with whatever shape `Program.cs` ends up needing — likely a method like `ApplyCustomHelpSections(CommandLineConfiguration config)` or per-command `AttachCustomHelp(this Command command)` invoked recursively from `Program.cs`.

The behaviour to preserve:

- Top-positioned sections render before the default help layout.
- Bottom-positioned sections render after Options.
- Sections in the same position render in registration order.
- Section format: `Title:` line, then `  ` (two-space)-indented lines, then a blank line.

`HelpOutputTests` and `HelpExtensionsTests` already encode the expected output shape — they're your regression check after the rewrite.

If the new help-action model can't reproduce the top-vs-bottom positioning 1:1, escalate as a design decision before proceeding (the test files will fail and you'll need direction).

- [ ] **Step 5: Migrate the command files**

For each of the 15 files in `src/AbsCli/Commands/` (excluding `HelpExtensions.cs` and `ResponseExamples.g.cs`):

1. `AuthorsCommand.cs`
2. `BackupCommand.cs`
3. `ChangelogCommand.cs`
4. `CommandHelper.cs`
5. `ConfigCommand.cs`
6. `ItemsCommand.cs`
7. `LibrariesCommand.cs`
8. `LoginCommand.cs`
9. `MetadataCommand.cs`
10. `SearchCommand.cs`
11. `SelfTestCommand.cs`
12. `SeriesCommand.cs`
13. `TasksCommand.cs`
14. `UploadCommand.cs`

Apply these mechanical replacements:

| Pattern | Replacement |
|---------|-------------|
| `command.SetHandler(handler, opt1, opt2, ...)` (positional binding) | `command.SetAction(parseResult => { var v1 = parseResult.GetValue(opt1); var v2 = parseResult.GetValue(opt2); … return handler(v1, v2, …); });` |
| `command.SetHandler((InvocationContext ctx) => …)` | `command.SetAction(parseResult => …)` — drop `InvocationContext`; everything you need is on `ParseResult` |
| `parseResult.GetValueForOption(opt)` | `parseResult.GetValue(opt)` |
| `parseResult.GetValueForArgument(arg)` | `parseResult.GetValue(arg)` |
| `context.Console.Out.Write(...)` (in `ChangelogCommand.cs`) | Use the SCL 2.0 equivalent — likely `parseResult.Configuration.Output.Write(...)` or just `Console.Out.Write(...)` again now that the `TestConsole`-vs-real-console issue may be moot under the new model |

`ChangelogCommand.cs` specifically: the existing code uses `context.Console.Out.Write(output + "\n")` because that's how `TestConsole` capture worked under beta4. Under SCL 2.0 the test-console pattern likely changed too. Update this command and the corresponding `ChangelogCommandTests.cs` test together; the test must still capture the output somehow. If the new model doesn't expose a console-injection point, fall back to `Console.SetOut(writer)` in the test.

`SelfTestCommand.cs` uses `command.SetHandler(() => …)` (no parameters) — the parameterless form. Migrate to `command.SetAction(parseResult => …)`.

- [ ] **Step 6: Migrate the three test files using `CommandLineBuilder`**

`tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`, `HelpOutputTests.cs`, `ChangelogCommandTests.cs` all wrap a `RootCommand` in a `CommandLineBuilder().UseDefaults().UseCustomHelpSections().Build()` and `parser.Invoke(args, console)`. Migrate each to the SCL 2.0 invocation model:

```csharp
// before
var root = new RootCommand();
root.AddCommand(SomeCommand.Create());
var parser = new CommandLineBuilder(root).UseDefaults().UseCustomHelpSections().Build();
parser.Invoke(args, console);

// after (approximate — verify against 2.0.7 API)
var root = new RootCommand();
root.Subcommands.Add(SomeCommand.Create());
var configuration = new CommandLineConfiguration(root) { Output = console.Out };
HelpExtensions.ApplyCustomHelpSections(configuration);
root.Parse(args, configuration).Invoke();
```

The `TestConsole` type from beta4's `System.CommandLine.IO` may be removed in 2.0. If so, replace with `StringWriter` redirected via the new `Configuration.Output` property (or whatever the field is named). Each test's assertion shape — `Assert.StartsWith`, `Assert.Contains` etc. — stays the same; only the plumbing changes.

- [ ] **Step 7: Build clean**

```bash
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: 0 errors, 0 warnings. Iterate on Steps 3-6 until clean.

- [ ] **Step 8: Run unit tests**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: 103 / 103 pass. The `HelpOutputTests` set is the regression check on the help-section rewrite — every assertion there must pass.

- [ ] **Step 9: Format check**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: `--verify-no-changes` exits 0.

- [ ] **Step 10: Commit**

```bash
git add src/AbsCli/AbsCli.csproj src/AbsCli/Program.cs src/AbsCli/Commands/ tests/AbsCli.Tests/Commands/
git commit -m "chore: bump System.CommandLine to 2.0.7"
```

If the diff is large enough to be unreviewable as one commit (e.g. > 800 lines), split using `git reset HEAD~1` and re-stage in chunks:

```bash
git reset HEAD~1   # un-commit but keep changes staged
git add src/AbsCli/AbsCli.csproj src/AbsCli/Program.cs src/AbsCli/Commands/HelpExtensions.cs
git commit -m "chore: bump System.CommandLine to 2.0.7 — parser + help foundation"
git add src/AbsCli/Commands/
git commit -m "chore: migrate command files to System.CommandLine 2.0 API"
git add tests/
git commit -m "test: migrate test files to System.CommandLine 2.0 API"
```

Decide based on the actual diff size. Default is one commit unless diff > 800 lines.

---

## Task 5: Local AOT publish + self-test

Verifies the SCL 2.0 migration didn't introduce AOT regressions. Most likely place for surprises: the new help-action model may require new trim hints.

**Files:** none.

- [ ] **Step 1: AOT publish for linux-x64**

```bash
dotnet publish /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true
```

Expected: completes with no errors and no new trim warnings beyond what we already see on `main` today.

- [ ] **Step 2: Locate the binary**

```bash
ls -lh /workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli
```

Expected: file exists. Size in the 9-11 MB range.

- [ ] **Step 3: Run `--version`**

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli --version
```

Expected: prints `0.2.7` (or whatever `<Version>` is in `AbsCli.csproj`).

- [ ] **Step 4: Run `--help`**

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli --help
```

Expected: command list renders. Top-level help should look the same as before the migration. Spot-check two or three command-level help outputs:

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli authors --help
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli items list --help
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli changelog --help
```

Each should show the same Notes / Examples / Response shape sections as before the SCL bump. If a section is missing or out of position, that's a regression in the help-extension rewrite — fix in Task 4 and republish.

- [ ] **Step 5: Run `self-test`**

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli self-test
```

Expected: 34 / 34 pass.

- [ ] **Step 6: Run `changelog`**

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli changelog | head -1
```

Expected: stdout starts with `## ` (the topmost heading from the embedded `CHANGELOG.md`).

This task produces no commit.

---

## Task 6: Live smoke test against ABS

End-to-end exercise of the AOT binary against a real ABS API. The migrated SCL parser handles every command-line argument the smoke suite throws at it, so this is the integration check.

**Files:** none.

- [ ] **Step 1: Bring up dockerised ABS**

```bash
docker compose -f /workspaces/abs-cli/docker/docker-compose.yml up -d
```

- [ ] **Step 2: Resolve container IP**

```bash
ABS_IP=$(docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "ABS_IP=$ABS_IP"
```

Use the IP, not `host.docker.internal`, per the project's docker-host-IP guidance. If the container name differs:

```bash
docker ps --format '{{.Names}}' | grep audiobookshelf
```

- [ ] **Step 3: Wait for ABS readiness**

```bash
for i in $(seq 1 30); do
    curl -sf "http://$ABS_IP/healthcheck" > /dev/null && echo "ready after ${i}s" && break
    sleep 1
done
```

Expected: ready within 30 seconds.

- [ ] **Step 4: Seed test data**

```bash
ABS_URL=http://$ABS_IP bash /workspaces/abs-cli/docker/seed.sh
```

Expected: completes with no errors. Reports library ID, test creds.

- [ ] **Step 5: Run smoke suite**

```bash
ABS_URL=http://$ABS_IP \
CLI=/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli \
bash /workspaces/abs-cli/docker/smoke-test.sh
```

Expected: tail summary `Results: 116 passed, 0 failed`. Any failure means the SCL migration introduced a behavioural change in argument parsing or output — diagnose, fix, republish, retest.

- [ ] **Step 6: Tear down ABS**

```bash
docker compose -f /workspaces/abs-cli/docker/docker-compose.yml down
```

This task produces no commit.

---

## Task 7: Push branch and verify CI

The 6-platform CI matrix is the final merge gate. Same drill as the .NET 10 PR.

**Files:** none.

- [ ] **Step 1: Push the branch**

```bash
git push -u origin feat/library-upgrades-and-security
```

- [ ] **Step 2: Open PR**

Use `gh pr create`. Title: `chore: upgrade libraries and add dependency security gate`. Body should mention:

- Three concerns: NuGet Audit + Dependabot, test packages → xUnit v3, System.CommandLine → 2.0.7
- GitHub Settings switches enabled out-of-band (call out so reviewer doesn't think they're missing)
- Local verification: 103 unit tests, 34 self-test, AOT publish, 116-assertion live smoke
- Reviewer to-do: pull, rebuild dev container if base image changed (it didn't on this branch), run tests locally

Per `CLAUDE.md`: no `Co-Authored-By`, no AI-attribution footer. Present PR URL as a clickable link to the user.

- [ ] **Step 3: Watch CI**

```bash
gh pr checks --watch
```

Expected: all 8 jobs (`unit-test`, `smoke-test`, 6 `build` matrix entries) green. `update-homebrew` skipped (release-only).

If a platform fails, diagnose by failure type:

- `unit-test` red: xUnit v3 runner behaves differently in CI than locally — check the actual stack trace.
- `smoke-test` red: the SCL migration introduced a CLI behavioural regression that local smoke didn't catch (different ABS image version in CI).
- `build (any platform)` red: AOT trim warning got promoted to error on a platform our local publish doesn't catch. Most often `osx-x64` (Rosetta self-test) or the Windows variants.

This task produces no commit.

---

## Self-Review

**1. Spec coverage check**

Walking the spec's "Scope (what changes)" sections:

- **Concern 1 — Directory.Build.props** → Task 2 Step 1.
- **Concern 1 — GitHub Settings** → Task 1 Step 1 (the operator handoff).
- **Concern 2 — xUnit v3 + sibling test packages** → Task 3 Step 1.
- **Concern 2 — test source migration** → Task 3 Step 2 inline guidance.
- **Concern 3 — System.CommandLine 2.0.7 csproj bump** → Task 4 Step 1.
- **Concern 3 — Program.cs migration** → Task 4 Step 3.
- **Concern 3 — HelpExtensions.cs rewrite** → Task 4 Step 4.
- **Concern 3 — command files migration** → Task 4 Step 5.
- **Concern 3 — test files migration** → Task 4 Step 6.

Spec's "Sequencing — forced order of work" → Tasks 1-4 in the same order.

Spec's "Verification stages" → Task 2 verifies after Concern 1, Task 3 after Concern 2, Tasks 4 (build/test/format) + 5 (AOT/self-test) + 6 (smoke) + 7 (CI) after Concern 3.

Spec's "Risks and mitigations" → addressed inline:
- Help-builder semantic 1:1 risk → Task 4 Step 4 explicitly flags escalation if positioning breaks.
- xUnit v3 runner risk → Task 3 Step 2 fallback note (drop to xUnit 2.9 if needed).
- Pre-existing vuln risk → Task 2 Step 2 explicit instruction to STOP and surface as finding.

No spec section is uncovered.

**2. Placeholder scan**

The plan contains a few "approximate" or "subject to API surface" phrases for the SCL 2.0 migration code samples (Task 4 Steps 3, 4, 6). These are not "TBD" placeholders — they're explicit acknowledgements that the SCL 2.0.7 API surface is the authoritative reference at implementation time, not a code snippet I write today. Each step instructs the implementer to consult specific docs (the migration guide URL) and apply patterns; the patterns are described concretely. This is the appropriate level of specificity given that I can't ship literal SCL 2.0.7 code without verifying against the actual package, and the guide-following step is part of the implementer's job.

The fallback split-commit instruction in Task 4 Step 10 is explicit: "Default is one commit unless diff > 800 lines." That's a concrete decision rule, not vagueness.

**3. Type/name consistency**

- Branch name: `feat/library-upgrades-and-security` — Tasks 0 Step 2 and 7 Step 1.
- Spec/plan filename: `2026-04-28-library-upgrades-and-security.md` — Task 0 Step 3.
- `Directory.Build.props` (singular, capitalised correctly) — Task 2 Step 1, plus File Structure section.
- `xunit.v3` package name (lowercase, dotted) — Task 3 Step 1.
- `HelpExtensions.ApplyCustomHelpSections` — proposed new entrypoint name in Task 4 Steps 3 and 4 and 6 (consistent across all three uses).
- `BLOCKED_ON_USER_GITHUB_SETTINGS` — Task 1 Step 1, header callout. Same spelling.
- `116 passed, 0 failed` — smoke-test expected count, Task 6 Step 5.
- `103 / 103` — unit-test expected count across Tasks 2, 3, 4, 5.
- `34 / 34` — self-test expected count, Task 5 Step 5.
- Commit message types — `chore`, `test`, `docs` used per project convention (`test` for the xUnit migration, `chore` for package bumps, `docs` for the spec/plan commit).

No drift detected.
