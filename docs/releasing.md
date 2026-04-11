# Releasing

## Versioning

Semantic versioning: `MAJOR.MINOR.PATCH`

- **0.x.y** — pre-1.0, breaking changes allowed between minor versions
- **MINOR** — new commands, new features
- **PATCH** — bug fixes, doc updates, dependency bumps, ABS compatibility updates (tested version range bump with no CLI changes)

## Release Workflow — Agentic Flow with Human Gates

The release process is a multi-step agentic workflow. An agent drives each
step, but pauses at human gates for review before proceeding. This will be
implemented as a `/release` slash command (`.claude/commands/release.md`)
after the v0.1.0 PR is merged.

### Step 1: Preflight (agent)

Agent verifies prerequisites:
- On `main` branch, working tree clean
- All unit tests pass (`dotnet test`)
- Determine version: agent proposes based on commits since last tag, human confirms

**Human gate:** Confirm the version number.

### Step 2: Generate release notes (agent)

Agent generates release notes with two sections:

**Highlights** — 3-5 bullet points in plain language. Agent writes these
by reading the commits and summarizing what users care about.

**Changes** — auto-grouped from conventional commits since last tag:

```bash
git log --oneline $(git describe --tags --abbrev=0)..HEAD --pretty="- %s" \
    | grep -E "^- (feat|fix|refactor|docs|ci|test):" | sort
```

Format:
```markdown
## Highlights
- First release of abs-cli with full audiobook metadata management
- Native AOT binaries for 6 platforms — no runtime required
- ...

## Features
- feat: add login command with access+refresh token storage
- ...

## Fixes
- fix: resolve AOT reflection errors in ConfigManager and AbsApiClient
- ...
```

Agent saves notes to `release-notes.md` (gitignored).

**Human gate:** Review and approve the release notes. Edit if needed.

### Step 3: Create GitHub Release (agent)

```bash
gh release create v{version} --title "v{version}" --notes-file release-notes.md
```

This creates the tag, publishes the release with notes, and triggers CI.

**Human gate:** Confirm the release was created. Agent shows the URL.

### Step 4: Wait for CI (agent)

Agent monitors the CI run:
```bash
gh run watch <run-id> --exit-status
```

Reports status. If CI fails, agent diagnoses and stops — no further steps
until the failure is resolved.

**Human gate:** CI passed — confirm before attaching binaries.

### Step 5: Attach binaries (agent)

```bash
gh run download <run-id> --dir ./release-artifacts
gh release upload v{version} ./release-artifacts/abs-cli-*/*
rm -rf release-artifacts release-notes.md
```

### Step 6: Verify (agent + human)

Agent downloads one binary and runs `self-test`. Reports result.

**Human gate:** Check the GitHub Release page — all 6 binaries attached,
notes render correctly, everything looks right.

### Step 7: Done

Agent reports the release URL and a summary.

## Human Gates Summary

| After step | Agent pauses for | Why |
|------------|-----------------|-----|
| 1. Preflight | Version confirmation | Human decides the version |
| 2. Release notes | Notes review | Human quality-checks what users see |
| 3. Create release | Release URL confirmation | Point of no return for the tag |
| 4. CI completion | CI result acknowledgement | Don't attach to a broken release |
| 6. Verify | Final visual check | Human confirms the public-facing page |

## Implementation Plan

After v0.1.0 merges, implement as `.claude/commands/release.md` — a
project-local slash command invoked via `/release`. The command file
contains the full flow above as agent instructions with explicit
`AskUserQuestion` gates at each human checkpoint.
