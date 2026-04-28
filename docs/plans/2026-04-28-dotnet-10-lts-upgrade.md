# .NET 10 LTS upgrade — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Special handling — forced human handoff between Task 1 and Task 2.** Task 1 changes the dev container base image. Before any later task can run, the user must rebuild the dev container in VS Code. The orchestrator (or executing human) MUST stop after Task 1's commit, surface the rebuild prompt, and wait for user confirmation that `dotnet --version` reports `10.x.x` in a fresh shell before dispatching Task 2. A subagent-driven flow handles this by reporting `BLOCKED_ON_USER_REBUILD` after Task 1; do not auto-continue.

**Goal:** Move the project from `net8.0` to `net10.0` LTS, including the dev container base image, CI workflow, all three csproj files, and the docs that name the version. No source code changes anticipated; existing tests + CI matrix are the verification gate.

**Architecture:** A pure tooling/build-config bump. C# language version rides the SDK default and goes from C# 12 → C# 14; `<LangVersion>` is not pinned. `System.CommandLine 2.0.0-beta4` keeps its current pin and is verified to run on net10 by the existing test suite. Test packages also keep their current pins. A new roadmap bullet captures the deferred bump of `System.CommandLine` and other stale packages as separate future work.

**Tech Stack:** C# / .NET 10 (target) / .NET 8 (current) / `mcr.microsoft.com/devcontainers/dotnet` base image / GitHub Actions / `actions/setup-dotnet@v5` / xUnit 2.5.3 (unchanged) / Native AOT via `clang` + `zlib1g-dev`.

**Spec:** [docs/specs/2026-04-28-dotnet-10-lts-upgrade.md](../specs/2026-04-28-dotnet-10-lts-upgrade.md)

---

## File Structure

**Modified files:**

- `.devcontainer/Dockerfile` — base image `:8.0` → `:10.0`
- `src/AbsCli/AbsCli.csproj` — `<TargetFramework>net8.0</TargetFramework>` → `net10.0`
- `tests/AbsCli.Tests/AbsCli.Tests.csproj` — same
- `tools/GenerateResponseExamples/GenerateResponseExamples.csproj` — same
- `.github/workflows/build.yml` — three `dotnet-version: '8.0.x'` → `'10.0.x'`; four `bin/Release/net8.0/...` paths → `bin/Release/net10.0/...`
- `docs/dev-container.md` — three `.NET 8` references → `.NET 10`; base-image and SDK-table phrasing
- `docs/build.md` — verify; update only if `.NET 8`-specific phrasing surfaces
- `docs/roadmap.md` — add a fourth bullet under `### v0.3.0` for "general library upgrades"

**Files explicitly NOT modified:**

- `CHANGELOG.md` — owned by release workflow, never feature branches
- `docker/smoke-test.sh` — its publish output path is framework-agnostic (`-o "$REPO_ROOT/src/AbsCli/bin/smoke-test"`)
- `docker/docker-compose.yml`, `docker/seed.sh` — ABS-side
- `.editorconfig` — no .NET-version-specific rules
- `.github/homebrew/abs-cli.rb.template` — ABI-agnostic

---

## Task 0: Create the implementation branch

Sets up the workspace before any file changes.

**Files:** none.

- [ ] **Step 1: Confirm clean tree on main**

```bash
cd /workspaces/abs-cli
git checkout main
git pull
git status
```

Expected: `On branch main`, working tree clean (the gitignored `.mempalace/` directory is fine).

- [ ] **Step 2: Create the feature branch**

```bash
git checkout -b feat/dotnet-10-upgrade
```

- [ ] **Step 3: Commit the spec and plan together as the first commit on the branch**

```bash
git add docs/specs/2026-04-28-dotnet-10-lts-upgrade.md docs/plans/2026-04-28-dotnet-10-lts-upgrade.md
git commit -m "docs: add spec and plan for .NET 10 LTS upgrade"
```

---

## Task 1: Bump devcontainer base image — STOP at end for user rebuild

This task must commit and stop. The dev container has to be rebuilt before any csproj bump can be tested. Inside the .NET 8 container, building a `net10.0` project will fail; running tests would produce confusing errors.

**Files:**
- Modify: `.devcontainer/Dockerfile:1`

- [ ] **Step 1: Verify the `:10.0` image tag is available before committing**

```bash
docker pull mcr.microsoft.com/devcontainers/dotnet:10.0
```

Expected: pull succeeds (or "Image is up to date"). If the tag does not exist, stop and escalate — the upgrade is blocked until Microsoft publishes the image.

- [ ] **Step 2: Edit `.devcontainer/Dockerfile`**

Change line 1 from:

```dockerfile
FROM mcr.microsoft.com/devcontainers/dotnet:8.0
```

to:

```dockerfile
FROM mcr.microsoft.com/devcontainers/dotnet:10.0
```

No other lines in the file change.

- [ ] **Step 3: Verify the diff**

```bash
git diff .devcontainer/Dockerfile
```

Expected: a single one-line change, `8.0` → `10.0` on line 1.

- [ ] **Step 4: Commit**

```bash
git add .devcontainer/Dockerfile
git commit -m "chore: bump devcontainer base image to dotnet:10.0"
```

- [ ] **Step 5: STOP — surface rebuild prompt to the user**

Output the following to the user verbatim (this is a hard handoff, not a status update):

> **Dev container rebuild required.** I've committed the Dockerfile change but I cannot rebuild the container from inside it. Please run **Dev Containers: Rebuild Container** in VS Code (Command Palette → that command). When the new shell comes up, run `dotnet --version` and confirm it prints `10.x.x`. Reply when done so I can continue with Task 2.

If running under subagent-driven development: the implementer subagent reports `BLOCKED_ON_USER_REBUILD` here. The orchestrator surfaces the prompt and does **not** dispatch Task 2 until the user confirms.

---

## Task 2: Bump TargetFramework in all three csproj files

After the rebuild, the .NET 10 SDK is available and a `net10.0` project will compile.

**Files:**
- Modify: `src/AbsCli/AbsCli.csproj:5`
- Modify: `tests/AbsCli.Tests/AbsCli.Tests.csproj:4`
- Modify: `tools/GenerateResponseExamples/GenerateResponseExamples.csproj:4`

- [ ] **Step 1: Confirm the rebuild took effect**

```bash
dotnet --version
```

Expected: prints `10.x.x`. If not, stop — the user did not actually rebuild, or VS Code reattached to the old container.

- [ ] **Step 2: Edit `src/AbsCli/AbsCli.csproj`**

Change line 5 from:

```xml
    <TargetFramework>net8.0</TargetFramework>
```

to:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 3: Edit `tests/AbsCli.Tests/AbsCli.Tests.csproj`**

Change line 4 from:

```xml
    <TargetFramework>net8.0</TargetFramework>
```

to:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 4: Edit `tools/GenerateResponseExamples/GenerateResponseExamples.csproj`**

Change line 4 from:

```xml
    <TargetFramework>net8.0</TargetFramework>
```

to:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

- [ ] **Step 5: Restore and Debug build**

```bash
dotnet restore /workspaces/abs-cli/AbsCli.sln
dotnet build /workspaces/abs-cli/AbsCli.sln -c Debug
```

Expected: build succeeds. If new compiler/analyzer warnings appear (likely C# 14 diagnostics on existing code), fix them inline before continuing. Common cases:

- New nullable-flow warnings (`CS86xx`) → narrow with `?` / `!` annotations or `is not null` patterns at the flagged site.
- New analyzer suggestions (`IDE`/`CA`) → fix in place; the project's `.editorconfig` already governs severity.

If `System.CommandLine 2.0.0-beta4` produces warnings or errors specific to net10 that cannot be silenced trivially, stop and escalate — bumping `System.CommandLine` was explicitly deferred and would expand scope.

- [ ] **Step 6: Run the full unit-test suite**

```bash
dotnet test /workspaces/abs-cli/AbsCli.sln
```

Expected: `Passed: 103, Failed: 0, Skipped: 0`.

- [ ] **Step 7: Run `dotnet format` and verify no diffs**

```bash
dotnet format /workspaces/abs-cli/AbsCli.sln
dotnet format /workspaces/abs-cli/AbsCli.sln --verify-no-changes
```

Expected: `--verify-no-changes` exits 0.

- [ ] **Step 8: Commit**

```bash
git add src/AbsCli/AbsCli.csproj tests/AbsCli.Tests/AbsCli.Tests.csproj tools/GenerateResponseExamples/GenerateResponseExamples.csproj
git commit -m "chore: bump TargetFramework from net8.0 to net10.0"
```

If Step 5 required source-code fixes for new warnings, include those files in the same commit and adjust the message accordingly (e.g. `chore: bump TargetFramework to net10.0 and silence new C# 14 warnings`).

---

## Task 3: Bump CI workflow

Three SDK-version pins and four hardcoded `net8.0` artifact paths.

**Files:**
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Update the three `dotnet-version` pins**

In `.github/workflows/build.yml`, lines 19, 37, and 85, change each occurrence of:

```yaml
          dotnet-version: '8.0.x'
```

to:

```yaml
          dotnet-version: '10.0.x'
```

- [ ] **Step 2: Update the four `bin/Release/net8.0/...` paths**

In the same file, lines 93, 100, 107, and 138 each reference `src/AbsCli/bin/Release/net8.0/`. Change each to `src/AbsCli/bin/Release/net10.0/`:

| Line | Context | New value |
|------|---------|-----------|
| 93 | self-test step BIN var | `BIN="src/AbsCli/bin/Release/net10.0/${{ matrix.rid }}/publish/abs-cli"` |
| 100 | upload-artifact path | `path: src/AbsCli/bin/Release/net10.0/${{ matrix.rid }}/publish/` |
| 107 | release-attach BIN var | `BIN="src/AbsCli/bin/Release/net10.0/${{ matrix.rid }}/publish/abs-cli"` |
| 138 | deb-package BIN var | `BIN="src/AbsCli/bin/Release/net10.0/${{ matrix.rid }}/publish/abs-cli"` |

- [ ] **Step 3: Verify there are no remaining `net8` or `8.0.x` references in the workflow**

```bash
grep -nE 'net8|8\.0\.x' /workspaces/abs-cli/.github/workflows/build.yml
```

Expected: no output (exit 1 from grep). If any line is printed, fix it before committing.

- [ ] **Step 4: Verify the workflow YAML still parses**

```bash
python3 -c "import yaml; yaml.safe_load(open('/workspaces/abs-cli/.github/workflows/build.yml'))" && echo OK
```

Expected: `OK`.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/build.yml
git commit -m "ci: bump SDK and artifact paths to net10.0"
```

---

## Task 4: Update `docs/dev-container.md`

Three `.NET 8` references update to `.NET 10`. Phrasing changes only — no structural rework.

**Files:**
- Modify: `docs/dev-container.md`

- [ ] **Step 1: Update the overview line (line 6)**

Change:

```markdown
consistent environment with .NET 8, Native AOT tooling, and all Claude Code
```

to:

```markdown
consistent environment with .NET 10, Native AOT tooling, and all Claude Code
```

- [ ] **Step 2: Update the base-image line (line 11)**

Change:

```markdown
`mcr.microsoft.com/devcontainers/dotnet:8.0` — Microsoft's official .NET 8
devcontainer image (Debian Bookworm).
```

to:

```markdown
`mcr.microsoft.com/devcontainers/dotnet:10.0` — Microsoft's official .NET 10
devcontainer image (Debian Bookworm).
```

(Verify the Debian release name is still Bookworm under the `:10.0` tag. If Microsoft has moved to `Trixie`, update the parenthetical to match — confirm by inspecting `/etc/os-release` from inside the rebuilt container.)

- [ ] **Step 3: Update the SDK-table row (line 20)**

Change:

```markdown
| .NET 8 SDK | C# compilation, project tooling |
```

to:

```markdown
| .NET 10 SDK | C# compilation, project tooling |
```

- [ ] **Step 4: Verify no remaining `.NET 8` or `dotnet:8.0` references in the file**

```bash
grep -nE '\.NET 8|dotnet:8\.0' /workspaces/abs-cli/docs/dev-container.md
```

Expected: no output.

- [ ] **Step 5: Commit**

```bash
git add docs/dev-container.md
git commit -m "docs: update dev-container doc for .NET 10"
```

---

## Task 5: Audit `docs/build.md` for .NET-version-specific phrasing

The current spec inventory says `build.md` has no version-specific phrasing today, but verify before declaring it untouched.

**Files:**
- Modify (conditionally): `docs/build.md`

- [ ] **Step 1: Search for version-specific terms**

```bash
grep -nE '\.NET 8|net8\.0|dotnet 8|dotnet:8' /workspaces/abs-cli/docs/build.md
```

If output is empty: skip to Step 3 (no commit needed for this task).

- [ ] **Step 2: Update each match**

For each line returned by Step 1, change the version mention to .NET 10 / `net10.0` / `dotnet:10.0` as appropriate. Keep surrounding wording intact.

- [ ] **Step 3: Decide whether to commit**

If Step 1 was empty: this task produces no commit; proceed to Task 6.

If Step 2 made changes:

```bash
git add docs/build.md
git commit -m "docs: update build doc for .NET 10"
```

---

## Task 6: Add "general library upgrades" bullet to roadmap

Captures the deferred `System.CommandLine` bump and other stale-package work as a fourth scoped item under v0.3.0.

**Files:**
- Modify: `docs/roadmap.md`

- [ ] **Step 1: Edit the v0.3.0 section**

Locate this block in `docs/roadmap.md`:

```markdown
- **Upgrade target framework to .NET 10 LTS** — Move from `net8.0` to
  `net10.0` once .NET 10 LTS is generally available. Verify AOT publish,
  `System.CommandLine` 2.x compatibility, and `System.Text.Json`
  source-gen behaviour. Spec/plan to follow.
```

Append a fourth bullet immediately after the closing line:

```markdown
- **General library upgrades** — Bump `System.CommandLine` from the long-stale
  `2.0.0-beta4` pin to the now-stable 2.0.7 (likely API-breaking; needs its
  own spec) and refresh test-tooling packages (`Microsoft.NET.Test.Sdk`,
  `xUnit`, `coverlet`). Spec/plan to follow.
```

The "Three roadmap items being delivered together as the next minor release" lead-in line above the bullets is now stale; update it to:

```markdown
Four roadmap items being delivered together as the next minor release.
```

- [ ] **Step 2: Verify the change**

```bash
grep -nE 'roadmap items|General library upgrades' /workspaces/abs-cli/docs/roadmap.md
```

Expected: shows the updated lead-in (`Four roadmap items...`) and the new bullet's bold heading.

- [ ] **Step 3: Commit**

```bash
git add docs/roadmap.md
git commit -m "docs: schedule general library upgrades under v0.3.0"
```

---

## Task 7: Local AOT publish + self-test

Verifies that AOT works on .NET 10 against the actual project. The most likely place for surprises is the native linker step.

**Files:** none.

- [ ] **Step 1: AOT publish for the local platform**

```bash
dotnet publish /workspaces/abs-cli/src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true
```

Expected: completes with no errors. Native AOT publishes typically take 1–3 minutes.

- [ ] **Step 2: Confirm the binary lives at the new path**

```bash
ls -lh /workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli
```

Expected: file exists, ~10 MB. If the path still says `net8.0/`, the csproj edit from Task 2 did not take effect — stop and investigate.

- [ ] **Step 3: Run `--version`**

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli --version
```

Expected: prints the version from `<Version>` in `AbsCli.csproj` (currently `0.2.7`) with no `+sha` suffix.

- [ ] **Step 4: Run `self-test`**

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli self-test
```

Expected: stderr ends with `Results: 34 passed, 0 failed`. Exit 0.

- [ ] **Step 5: Run `changelog` against the AOT binary as a quick smoke check**

```bash
/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli changelog | head -1
```

Expected: stdout begins with `## ` (the topmost heading from the embedded `CHANGELOG.md`).

This task produces no commit — it's verification of work committed in Tasks 1-3.

---

## Task 8: Smoke test against live ABS

End-to-end exercise of the AOT binary against a live ABS API. Catches issues a unit-test suite can't see (HTTP serialization, real auth flow, file uploads, etc.).

**Files:** none.

- [ ] **Step 1: Bring up the dockerised ABS server**

```bash
docker compose -f /workspaces/abs-cli/docker/docker-compose.yml up -d
```

Expected: container `audiobookshelf` (or `docker-audiobookshelf-1`) running.

- [ ] **Step 2: Resolve the container's IP address**

```bash
docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'
```

If the container name differs, list candidates:

```bash
docker ps --format '{{.Names}}' | grep audiobookshelf
```

Use the resolved IP — the docker-internal hostname (`host.docker.internal`) does not resolve reliably from this dev container, per the project's docker-host-IP guidance.

- [ ] **Step 3: Wait for ABS to be ready**

```bash
ABS_IP=<ip from Step 2>
for i in $(seq 1 30); do
    curl -sf "http://$ABS_IP:80/healthcheck" && break
    sleep 1
done
```

Expected: receives the healthcheck response within 30 seconds.

- [ ] **Step 4: Seed test data**

```bash
ABS_URL=http://$ABS_IP:80 bash /workspaces/abs-cli/docker/seed.sh
```

Expected: completes with no errors.

- [ ] **Step 5: Run the smoke suite against the AOT binary built in Task 7**

```bash
ABS_URL=http://$ABS_IP:80 \
CLI=/workspaces/abs-cli/src/AbsCli/bin/Release/net10.0/linux-x64/publish/abs-cli \
bash /workspaces/abs-cli/docker/smoke-test.sh
```

Expected: all assertions pass. The script reports a `passed/failed` summary at the end; failures must be zero before continuing.

If any assertion fails: stop, diagnose, decide whether the failure indicates a real net10 regression (fix in this branch) or pre-existing flakiness (file as a separate concern, do not fold into this PR).

- [ ] **Step 6: Tear down ABS**

```bash
docker compose -f /workspaces/abs-cli/docker/docker-compose.yml down
```

This task produces no commit.

---

## Task 9: Push branch and verify CI

The 6-platform CI matrix is the final merge gate.

**Files:** none.

- [ ] **Step 1: Push the branch**

```bash
cd /workspaces/abs-cli
git push -u origin feat/dotnet-10-upgrade
```

- [ ] **Step 2: Open the PR**

Use `gh pr create` with the standard format. Title: `chore: upgrade target framework to .NET 10 LTS`. Body should mention:

- Spec + plan paths
- Three csproj + Dockerfile + CI + docs touched
- Roadmap updated to add "general library upgrades" follow-up
- Local verification: 103 unit tests, 34 self-test checks, AOT publish on linux-x64, smoke test against live ABS — all green
- Reviewer to-do: pull and run `dotnet --version` after VS Code rebuilds their devcontainer

Per `CLAUDE.md`: no `Co-Authored-By` trailer, no AI-attribution footer, present the PR URL as a clickable link to the user.

- [ ] **Step 3: Watch the CI run**

```bash
gh pr checks --watch
```

Expected: all 8 jobs (`unit-test`, `smoke-test`, and the 6 `build` matrix entries) complete green.

If any platform fails:
- `linux-x64` / `linux-arm64` failure: most likely a real AOT regression. Fix in branch.
- `osx-*` failure: cross-compile or Rosetta 2 path issue. Investigate before merge.
- `win-*` failure: rare, but native linker on Windows differs. Investigate.

This task produces no commit.

---

## Self-Review

**1. Spec coverage check**

Walking the spec's "Scope" sections against tasks:

- `src/AbsCli/AbsCli.csproj` TargetFramework → Task 2 Step 2.
- `tests/AbsCli.Tests/AbsCli.Tests.csproj` TargetFramework → Task 2 Step 3.
- `tools/GenerateResponseExamples/GenerateResponseExamples.csproj` TargetFramework → Task 2 Step 4.
- `.devcontainer/Dockerfile` base image → Task 1 Step 2.
- CI dotnet-version pins → Task 3 Step 1.
- CI artifact paths (4 of them) → Task 3 Step 2.
- `docs/dev-container.md` updates → Task 4.
- `docs/build.md` audit → Task 5.
- `docs/roadmap.md` "general library upgrades" bullet → Task 6.
- `LangVersion` not pinned → enforced by Task 2 Step 5 ("take SDK default") with no LangVersion edits in any task.
- `CHANGELOG.md` not modified → enforced by no task touching it; `git status` after Task 6 should not show CHANGELOG.md changed.

Spec's "Sequencing — forced order of work" → exact mapping to Tasks 0–6 (with Task 7-8 being verification, Task 9 being push/CI).

Spec's "Verification stages" 1–7 → covered by Task 2 (build/tests/format), Task 7 (AOT publish + self-test), Task 8 (smoke), Task 9 (CI matrix).

Spec's "Risks and mitigations" → addressed by the inline guidance in Task 1 Step 1 (image availability), Task 2 Step 5 (warning fix), and Task 9 Step 3 (per-platform diagnosis).

No gaps.

**2. Placeholder scan**

No "TBD", "implement later", "fill in details", or vague-error-handling phrases. Conditional edits (Task 5) are gated by an explicit grep with concrete next steps for each branch. The PR body in Task 9 Step 2 says "should mention" rather than dictating exact text — acceptable because the surrounding context provides every fact the body needs.

**3. Type/name consistency**

- `feat/dotnet-10-upgrade` — branch name used in Task 0 Step 2 and Task 9 Step 1. Consistent.
- `feat/dotnet-10-upgrade` (not `feat/net10-upgrade` or other variants) — only one form used.
- `mcr.microsoft.com/devcontainers/dotnet:10.0` — used in Task 1 Step 1 and Step 2. Consistent.
- `'10.0.x'` (with quotes, matching YAML format) — Task 3.
- `bin/Release/net10.0/...` paths — same casing in Task 3, 7, 8.
- Commit message types — `chore`, `ci`, `docs` used appropriately and consistently with the project's other recent commits.
- `BLOCKED_ON_USER_REBUILD` — appears once in the header callout and once in Task 1 Step 5. Same spelling.
