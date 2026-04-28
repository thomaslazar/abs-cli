# General library upgrades + dependency security — design

Status: approved 2026-04-28. Targets v0.3.0.

## Summary

Bring all NuGet dependencies off long-stale pins, switch xUnit from the v2
line to v3, and add an opt-in vulnerability gate (NuGet Audit + GitHub
Dependabot security updates). Three logical concerns delivered in one PR
with clearly separated commits:

1. **NuGet Audit policy** in a new repo-root `Directory.Build.props`, plus
   GitHub Dependabot alerts + security updates enabled at the repo Settings
   level (out-of-band, no branch artifact).
2. **Test packages bumped** — `xunit.v3` 3.2.2 replaces `xunit` 2.5.3,
   `Microsoft.NET.Test.Sdk` and `coverlet.collector` move to current stable.
3. **`System.CommandLine` bumped** from `2.0.0-beta4.22272.1` to `2.0.7`
   stable. API-rewriting bump that touches every `Commands/*.cs` plus
   `HelpExtensions.cs` and `Program.cs`.

All version strings remain exact pins (no floating ranges or wildcards).

## Motivation

`System.CommandLine` is pinned to a 2022-vintage beta. Test packages have
been frozen at the v0.1 baseline for over a year. Neither has shifted under
us, but each release that goes by widens the gap and any future
vulnerability advisory leaves us without a recent pin to land on.

Dependency security checking has not been wired up at all — there is no
build-time vuln scan and no GitHub-side advisory subscription. The result
is that a published CVE against any dep would not surface in this project
until a human noticed.

The three concerns are bundled because they all sit in the same NuGet
hygiene workspace and the operator (`thomaslazar`) wants one PR review
covering "package upgrades and security tightening" rather than three
small, related ones.

## Scope (what changes)

### Concern 1 — NuGet Audit + GitHub Dependabot

#### `Directory.Build.props` (new, repo root)

```xml
<Project>
  <PropertyGroup>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <WarningsAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsAsErrors>
  </PropertyGroup>
</Project>
```

- `NuGetAudit=true` — explicit on (default in .NET 8+, but be explicit so
  a future SDK regression doesn't silently disable it).
- `NuGetAuditMode=all` — scan transitive dependencies, not just direct.
- `WarningsAsErrors=NU190x` — the four NuGet audit warning codes get
  promoted to build errors. CI fails on any vulnerable package.

The file applies to all three csproj automatically via MSBuild's
inherit-from-parent-directory rule. No csproj edits needed for this
concern.

#### GitHub repo Settings (out-of-band)

- Settings → Code security → **Dependabot alerts** → Enable
- Settings → Code security → **Dependabot security updates** → Enable

These switches are clicks, not files. They live in repo configuration on
the GitHub side. Implementation must STOP at the start of work and prompt
the operator to flip both before any code lands. No `dependabot.yml` is
added — routine version-bump PRs are explicitly out of scope (vuln-only
policy, per Q4 of brainstorm).

### Concern 2 — Test packages

In `tests/AbsCli.Tests/AbsCli.Tests.csproj`:

| Package | Current | Target |
|---------|---------|--------|
| `xunit` | 2.5.3 | **removed** |
| `xunit.runner.visualstudio` | 2.5.3 | **removed** |
| `xunit.v3` | — | added at 3.2.2 (stable, GA) |
| `xunit.v3.assert` (or whatever sibling v3 packs ship) | — | added at the matching stable version |
| `xunit.runner.visualstudio` (v3 line) | — | added at the version that pairs with `xunit.v3` 3.2.2 |
| `Microsoft.NET.Test.Sdk` | 17.8.0 | current stable |
| `coverlet.collector` | 6.0.0 | current stable |

Exact versions of `Microsoft.NET.Test.Sdk` and `coverlet.collector` get
locked when the implementer runs `dotnet add package` — whatever NuGet
resolves at that moment is the pin. The package list above is approximate
because xUnit v3 ships a different set of NuGets than v2 (assertion
package may be separate; runner package name differs). The implementer
follows the official xUnit v3 migration doc and pins whatever resolves.

Test source code under `tests/AbsCli.Tests/**/*.cs` may need:

- Namespace updates (xUnit v3 keeps `using Xunit;` but reorganises some
  internals).
- Assertion adjustments if v3 changed any signatures we use (the project
  uses `Assert.Equal`, `Assert.True`, `Assert.False`, `Assert.Throws`,
  `Assert.StartsWith`, `Assert.EndsWith`, `Assert.Contains`,
  `Assert.DoesNotContain` — all of these are stable across the v2/v3
  boundary, so changes here are unlikely).
- Test-attribute changes if any. The project uses `[Fact]` exclusively —
  `[Fact]` is unchanged in v3.

The implementer follows the xUnit v3 migration guide
(`https://xunit.net/docs/getting-started/v3/migration`) for any
codebase-wide adjustments.

### Concern 3 — `System.CommandLine`

In `src/AbsCli/AbsCli.csproj`:

```diff
-<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
+<PackageReference Include="System.CommandLine" Version="2.0.7" />
```

This is the bulk of the work. Between `beta4` (July 2022) and `2.0.7`
(April 2026) the API was rewritten. Known rewrites that affect this
codebase:

| Current usage | 2.0.7 equivalent |
|---------------|------------------|
| `new CommandLineBuilder(rootCommand).UseDefaults().UseCustomHelpSections().Build()` | `new CommandLineConfiguration(rootCommand) { ... }` plus invocation pipeline migration |
| `command.SetHandler(handler, options...)` | `command.SetAction(parseResult => …)` |
| `parseResult.GetValueForOption(opt)` | `parseResult.GetValue(opt)` |
| `parser.InvokeAsync(args)` | `rootCommand.Parse(args).InvokeAsync()` |
| `parser.Invoke(args, console)` | `rootCommand.Parse(args, configuration).Invoke()` |
| Custom help-builder code in `HelpExtensions.cs` (`UseCustomHelpSections`, `AddHelpSection`, top/bottom positioning) | rewritten against the new help-model API; details emerge during implementation |
| `Option<T>(name, description)` constructor | name in 2.0 must start with `--`; project already uses `--` prefix everywhere, so this is a no-op |

Files affected (every command file plus the help extension and program
entry point):

- `src/AbsCli/Program.cs`
- `src/AbsCli/Commands/AuthorsCommand.cs`
- `src/AbsCli/Commands/BackupCommand.cs`
- `src/AbsCli/Commands/ChangelogCommand.cs`
- `src/AbsCli/Commands/CommandHelper.cs`
- `src/AbsCli/Commands/ConfigCommand.cs`
- `src/AbsCli/Commands/HelpExtensions.cs`
- `src/AbsCli/Commands/ItemsCommand.cs`
- `src/AbsCli/Commands/LibrariesCommand.cs`
- `src/AbsCli/Commands/LoginCommand.cs`
- `src/AbsCli/Commands/MetadataCommand.cs`
- `src/AbsCli/Commands/SearchCommand.cs`
- `src/AbsCli/Commands/SelfTestCommand.cs`
- `src/AbsCli/Commands/SeriesCommand.cs`
- `src/AbsCli/Commands/TasksCommand.cs`
- `src/AbsCli/Commands/UploadCommand.cs`
- `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs` (uses `CommandLineBuilder`)
- `tests/AbsCli.Tests/Commands/HelpOutputTests.cs` (uses `CommandLineBuilder`)
- `tests/AbsCli.Tests/Commands/ChangelogCommandTests.cs` (uses `CommandLineBuilder`)

The migration is mechanical (find + adapt patterns), not architectural,
but the surface area is wide. Implementer should expect to keep the
existing command shape, options, examples, and help-section semantics —
the user-facing CLI behaviour does not change.

### Files explicitly NOT modified

- `CHANGELOG.md` — owned by release workflow, never feature branches.
- `.github/workflows/build.yml` — no CI changes; existing `dotnet test` and
  AOT publish remain the gates.
- `.github/dependabot.yml` — not added (vuln-only policy).
- `docker/*` — unrelated.

## Sequencing — forced order of work

1. **STOP at start.** Prompt the operator to flip the two GitHub Settings
   switches (Dependabot alerts + security updates). Do not begin code
   work until the operator confirms both show "Enabled" in repo settings.
   No commit at this step.
2. **Concern 1 commit.** Add `Directory.Build.props`. Run `dotnet restore`
   and `dotnet build` to confirm the current package set is clean (no
   `NU1901`/`NU1902`/`NU1903`/`NU1904` errors raised). If it isn't, the
   pre-existing dep set has a known vuln — surface as a finding and
   address before continuing. Commit.
3. **Concern 2 commit.** Replace `xunit` + `xunit.runner.visualstudio`
   with the v3 packages, bump `Microsoft.NET.Test.Sdk` and
   `coverlet.collector`. Adjust any test code the v3 migration requires.
   Run unit tests; expect 103 / 103 unchanged. `dotnet format` clean.
   Commit.
4. **Concern 3 commit (or commit series).** Bump `System.CommandLine` to
   2.0.7. Rewrite each command file to the new API. Rewrite custom
   help-builder code in `HelpExtensions.cs`. Update `Program.cs` parser
   construction. Run unit tests; all 103 must pass. AOT publish; run
   self-test (34 / 34); run live smoke test (`docker/smoke-test.sh`)
   against a freshly seeded ABS — all 116 assertions must pass. Commit
   may be split into smaller per-area commits if the diff would otherwise
   be too large to review (e.g. one commit for `Program.cs` + parser
   migration, one for command files, one for help extensions). The
   implementer decides based on the actual diff.

Each step ends with `dotnet format --verify-no-changes` clean, so a
formatting nit doesn't sneak in via the bumps.

## Verification stages (after each commit)

1. `dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug` — clean.
2. `dotnet test /workspaces/abs-cli/AbsCli.sln` — 103 / 103.
3. `dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes` —
   exit 0.

Plus, after Concern 3 (the SCL bump):

4. AOT Release publish for linux-x64 — succeeds.
5. `abs-cli self-test` against the AOT binary — 34 / 34.
6. `abs-cli changelog` against the AOT binary — prints the topmost `## `
   heading.
7. `docker/smoke-test.sh` against a live, seeded ABS using the AOT binary
   and the container IP (per the project's docker-host-IP guidance) —
   116 / 116.
8. Push branch; the existing CI matrix runs unit-test, smoke-test, and
   the 6-platform AOT build matrix. All green is the merge gate.

If any stage surfaces a real issue (vuln in pre-existing deps, xUnit v3
test incompatibility, SCL behavioural regression), stop and treat as a
scoped finding — fix in branch if cheap, escalate if not.

## Risks and mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `System.CommandLine 2.0.7` help-builder API can't reproduce `UseCustomHelpSections` semantics 1:1 | Medium | Implementer rewrites against new API; if user-facing help drifts, escalate as a design decision. Reference: existing `HelpOutputTests` capture the expected output, regression-checking the rewrite. |
| `xunit.v3 3.2.2` test runner has trouble with this project's source-link patterns | Low | Catch via Concern 2 unit-test run; if hit, fall back to xUnit 2.9 (still a bump from 2.5.3, no major migration cost). |
| Pre-existing dep has a known vuln that NU190x flags after `Directory.Build.props` lands | Low | Concern 1 step deliberately catches this before any bumps. Address as a separate first commit if found. |
| `Microsoft.NET.Test.Sdk` current stable conflicts with the rest of test stack | Low | Pin to a known-compatible version per `xunit.v3` 3.2.2 release notes. |
| AOT publish behaviour changes under SCL 2.0 (e.g. new reflection requirements) | Low | Existing self-test exercises serialisation paths; if AOT introduces new trim warnings, address inline. |
| GitHub Dependabot security updates aren't enabled in time and a CVE drops mid-PR | Negligible | Step 1 of the plan explicitly gates on the switches being on. |

## Out of scope (deferred)

- `xunit.v3` 4.x preview series. Stay on 3.2.2 stable.
- `System.CommandLine` 3.0 preview series. Stay on 2.0.7 stable.
- A `.github/dependabot.yml` for routine non-vuln version bumps.
- Replacing `coverlet.collector` with another coverage tool.
- Any source-code refactor beyond what the SCL API rewrite forces.
- New unit tests that aren't there already.

## Open questions

None. All decisions made during brainstorming.
