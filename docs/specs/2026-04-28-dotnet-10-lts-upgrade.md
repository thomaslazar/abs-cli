# .NET 10 LTS upgrade — design

Status: approved 2026-04-28. Targets v0.3.0.

## Summary

Move the project from `net8.0` to `net10.0` LTS. A focused framework bump:
TargetFramework in three csproj files, the devcontainer base image, the CI
workflow's SDK pin and artifact paths, and the docs that name the .NET
version. No source-code changes anticipated; the existing 103 unit tests, 34
self-test checks, smoke test, and 6-platform CI matrix are the verification
gate.

C# language version rides the SDK default and goes from C# 12 to C# 14. No
`<LangVersion>` pin is added; new compiler/analyzer warnings (if any) are
addressed inline as part of the build verification.

`System.CommandLine 2.0.0-beta4` keeps its current pin. Bumping it is its own
future work — captured in this branch as a roadmap addition (see "Roadmap"
below) but explicitly out of scope here.

## Motivation

.NET 8 is supported until November 2026. .NET 10 went LTS in November 2025
and is the next supported long-term track. Upgrading now while the project
is small keeps the churn cheap and unblocks any features that benefit from
the .NET 10 SDK or runtime (improved AOT trimmer, refreshed `System.Text.Json`
source-gen, faster JIT/AOT codegen).

## Scope (what the upgrade touches)

### Build and source projects

`<TargetFramework>net8.0</TargetFramework>` → `net10.0` in:

- `src/AbsCli/AbsCli.csproj`
- `tests/AbsCli.Tests/AbsCli.Tests.csproj`
- `tools/GenerateResponseExamples/GenerateResponseExamples.csproj`

No other csproj edits. `<LangVersion>` is not added. Test packages
(`Microsoft.NET.Test.Sdk 17.8.0`, `xUnit 2.5.3`, `coverlet 6.0.0`) and
`System.CommandLine 2.0.0-beta4` keep their current pins.

### Dev container

`.devcontainer/Dockerfile` line 1:

```diff
-FROM mcr.microsoft.com/devcontainers/dotnet:8.0
+FROM mcr.microsoft.com/devcontainers/dotnet:10.0
```

No other devcontainer changes. The native AOT toolchain (clang, zlib1g-dev)
stays in place.

### CI workflow (`.github/workflows/build.yml`)

Three `dotnet-version: '8.0.x'` entries → `'10.0.x'`. Two artifact paths
`src/AbsCli/bin/Release/net8.0/${{ matrix.rid }}/publish/` →
`src/AbsCli/bin/Release/net10.0/${{ matrix.rid }}/publish/` (one in the
"Run self-test" step, one in the "Attach binary to GitHub Release" /
"Build and upload deb package" step). The matrix of 6 RIDs is unchanged.

### Documentation

- `docs/dev-container.md` — three `.NET 8` references → `.NET 10`. Phrasing in
  the overview, base image section, and "What's Installed" table.
- `docs/build.md` — verify and update if any `.NET 8`-specific phrasing
  surfaces during the bump (none expected today).
- `docs/roadmap.md` — see "Roadmap" below.

### Roadmap (`docs/roadmap.md`)

In `### v0.3.0 — Changelog, cover handling & .NET 10 LTS`, add a fourth
bullet for "general library upgrades" capturing the deferred
`System.CommandLine` bump and any future stale-package sweep:

```markdown
- **General library upgrades** — Bump `System.CommandLine` from the long-stale
  `2.0.0-beta4` pin to the now-stable 2.0.7 (likely API-breaking; needs its
  own spec) and refresh test-tooling packages (`Microsoft.NET.Test.Sdk`,
  `xUnit`, `coverlet`). Spec/plan to follow.
```

The .NET 10 upgrade itself stays as the existing third bullet; the heading
is updated only at release time on a `release/v0.3.0` branch, not here.

### Files explicitly NOT modified

- `CHANGELOG.md` — owned by the release workflow, never feature branches.
- `docker/smoke-test.sh` — uses `-o "$REPO_ROOT/src/AbsCli/bin/smoke-test"` so
  its publish path doesn't reference the framework version.
- `docker/docker-compose.yml`, `docker/seed.sh` — ABS-side, not .NET-side.
- `.editorconfig` — no .NET-version-specific rules today.
- `.github/homebrew/abs-cli.rb.template` — ABI-agnostic.

## Sequencing — forced order of work

The upgrade has a forced sequence because the dev container must be rebuilt
between the Dockerfile change and any `<TargetFramework>` bump. Inside the
.NET 8 container the SDK cannot compile a `net10.0` project; running tests
would fail confusingly. The order:

1. Bump `.devcontainer/Dockerfile`. Commit.
2. **STOP**. Prompt the user to run `Dev Containers: Rebuild Container` in
   VS Code. Wait for confirmation. Verify `dotnet --version` reports `10.x.x`
   in a fresh shell.
3. Bump the three csproj `<TargetFramework>` entries. Build, run tests,
   format, commit.
4. Bump `.github/workflows/build.yml` (SDK pin × 3, artifact paths × 2).
   Commit.
5. Update `docs/dev-container.md` and `docs/build.md` if needed. Commit.
6. Update `docs/roadmap.md` to add the "general library upgrades" bullet.
   Commit.
7. AOT publish + self-test smoke run + `docker/smoke-test.sh` against ABS.
8. Push branch; CI runs the 6-platform matrix.

The rebuild handoff in step 2 is non-negotiable — it cannot be triggered
from inside the container, so the implementation must explicitly halt and
ask the user.

## Verification stages

All stages are run *after* the rebuild (step 2 above). Earlier-numbered
stages must pass before later ones run.

1. **Local Debug build** — `dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug`.
   Must succeed. Address any new compiler/analyzer warnings inline
   (likely C# 14 diagnostics on existing code).
2. **Local unit tests** — `dotnet test /workspaces/abs-cli/AbsCli.sln`.
   All 103 tests must pass.
3. **Format** — `dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes`.
   Exit 0.
4. **Local AOT Release publish** —
   `dotnet publish src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true`.
   The native linker step is the most likely place for surprises.
5. **Self-test on the AOT binary** —
   `bin/Release/net10.0/linux-x64/publish/abs-cli self-test`. 34 checks
   must pass.
6. **Smoke test against live ABS** — bring the dockerised ABS up
   (`docker compose -f docker/docker-compose.yml up -d`), resolve the
   container IP (the docker-internal hostname is unreliable from the dev
   container, see the project's docker-host-IP guidance), run
   `ABS_URL=http://<container-ip>:80 CLI=<published-binary> bash docker/smoke-test.sh`.
   All assertions must pass.
7. **CI matrix** — push the branch, the existing `build.yml` runs unit-test,
   smoke-test (linux-x64), and 6-platform AOT publish + self-test. All
   green is the merge gate.

If any stage surfaces a real issue (AOT incompatibility, broken trimmer
behavior, package compat), stop and treat it as a scoped finding — fix in
this branch if cheap, or revert and reroute to a deeper investigation.

## Risks and mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `System.CommandLine 2.0.0-beta4` doesn't run cleanly on net10 | Low (it targets netstandard2.0) | Verification stage 1 catches it. Fallback: bump to a newer SCL prerelease in this branch (would expand scope). |
| AOT publish breaks on one platform but not others | Low | CI matrix surfaces it before merge. Per-platform debug. |
| New C# 14 analyzer warnings break `dotnet format --verify-no-changes` | Low | Fix inline. Only affects style/diagnostic surface, not behavior. |
| Devcontainer rebuild loses session state | Negligible | Sessions are bind-mounted from the host (`~/.claude/projects`), survive container teardown. |
| `mcr.microsoft.com/devcontainers/dotnet:10.0` tag doesn't exist yet | Negligible (.NET 10 GA was Nov 2025) | Verify tag availability as the first action in step 1. |

## Out of scope (deferred)

These were considered and explicitly excluded from this spec. Each can be
picked up in its own future spec/plan once this lands.

- **`System.CommandLine` bump** from `2.0.0-beta4` to `2.0.7` stable. Likely
  API-breaking (rewrites between beta4 and 2.0 GA). Captured on the
  v0.3.0 roadmap as part of "general library upgrades".
- **Test-package refresh** (`Microsoft.NET.Test.Sdk`, `xUnit`, `coverlet`).
  Independent of the framework move; same future spec.
- **`global.json`**. Rejected — devcontainer + CI self-pin, no out-of-band
  contributors expected.
- **AOT optimization tweaks** unlocked by .NET 10 (binary-size analysis,
  new trimmer features). Evaluate after the upgrade lands.
- **C# `<LangVersion>` pin**. Rejected — take the SDK default (C# 14).
- **`Microsoft.NET.Sdk` minimum-required version pin** in csproj. Not used
  in the project today; not added.

## Open questions

None. All decisions made during brainstorming.
