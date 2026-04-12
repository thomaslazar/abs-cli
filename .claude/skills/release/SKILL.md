---
name: release
description: Create a new abs-cli release with human review gates. Runs preflight checks, generates release notes, creates GitHub release, monitors CI, attaches binaries.
disable-model-invocation: true
allowed-tools:
  - Bash
  - Read
  - Write
  - Glob
  - Grep
  - Edit
  - AskUserQuestion
---

# Release abs-cli

Multi-step release workflow with human gates. You drive each step, pause at
gates for human approval before proceeding. Never skip a gate.

## Step 1: Preflight

Verify prerequisites:

```bash
# Must be on main
BRANCH=$(git branch --show-current)
[ "$BRANCH" = "main" ] || { echo "ERROR: must be on main, currently on $BRANCH"; exit 1; }

# Working tree must be clean
git diff --quiet && git diff --cached --quiet || { echo "ERROR: working tree not clean"; git status --short; exit 1; }

# Pull latest
git pull

# Unit tests must pass
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
```

If any check fails, stop and report the issue. Do not proceed.

Determine the version number:
- Get the last tag: `git describe --tags --abbrev=0 2>/dev/null || echo "none"`
- Read commits since last tag: `git log --oneline $(git describe --tags --abbrev=0 2>/dev/null || echo "HEAD~50")..HEAD`
- Propose a version based on conventional commits:
  - Any `feat:` commits → bump MINOR
  - Only `fix:`, `docs:`, `test:`, `ci:`, `chore:` → bump PATCH
  - See `docs/releasing.md` for versioning rules

**GATE: Ask the human to confirm the version number.** Show them the proposed
version and the commit summary. Wait for their response. If they want a
different version, use that instead.

## Step 2: Generate Release Notes

Generate release notes with two sections:

**Highlights** — write 3-5 bullet points describing what's new in plain
language. Focus on what users care about, not implementation details. Read
the commits to understand the changes.

**Changes** — auto-grouped from conventional commits:

```bash
# Get commits since last tag (or all if first release)
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
if [ -n "$LAST_TAG" ]; then
    RANGE="${LAST_TAG}..HEAD"
else
    RANGE="HEAD"
fi

echo "## Features"
git log --oneline $RANGE --pretty="- %s" | grep "^- feat:" | sort
echo ""
echo "## Fixes"
git log --oneline $RANGE --pretty="- %s" | grep "^- fix:" | sort
echo ""
echo "## Other"
git log --oneline $RANGE --pretty="- %s" | grep -E "^- (refactor|docs|test|ci|chore):" | sort
```

Combine highlights and changes into a single markdown document. Save to
`release-notes.md` in the repo root (this file is gitignored).

**GATE: Show the release notes to the human.** Ask them to review and approve.
If they want edits, make them and show again. Do not proceed until approved.

## Step 3: Create GitHub Release

```bash
VERSION="v{version}"  # from step 1
gh release create "$VERSION" --title "$VERSION" --notes-file release-notes.md
```

Show the release URL to the human.

**GATE: Ask the human to confirm the release was created correctly.** Show
them the URL. This is a point of no return — the tag is published.

## Step 4: Wait for CI

Find the CI run triggered by the release and monitor it:

```bash
# Wait a moment for the run to appear, then find it
RUN_ID=$(gh run list --limit 5 --json databaseId,event,headBranch -q '.[] | select(.event=="release") | .databaseId' | head -1)
gh run watch "$RUN_ID" --exit-status
```

If CI fails:
- Show the failure details: `gh run view "$RUN_ID" --log-failed`
- Stop and report. Do not proceed. The human needs to decide how to handle it.

If CI passes, report the results (all jobs, times).

**GATE: Tell the human CI passed and ask them to confirm before attaching
binaries.** Show the job summary.

## Step 5: Attach Binaries

Download CI artifacts and attach them to the release:

```bash
VERSION="v{version}"
gh run download "$RUN_ID" --dir ./release-artifacts

# Attach all binaries
for dir in ./release-artifacts/abs-cli-*/; do
    PLATFORM=$(basename "$dir")
    # Find the binary (abs-cli or abs-cli.exe)
    BIN=$(find "$dir" -name "abs-cli" -o -name "abs-cli.exe" | head -1)
    if [ -n "$BIN" ]; then
        # Rename to include platform
        EXT=""
        [[ "$BIN" == *.exe ]] && EXT=".exe"
        cp "$BIN" "./release-artifacts/${PLATFORM}${EXT}"
        gh release upload "$VERSION" "./release-artifacts/${PLATFORM}${EXT}"
        echo "Attached: ${PLATFORM}${EXT}"
    fi
done
```

## Step 6: Verify

Download the binary for the current platform and run self-test:

```bash
# Download and test the linux-x64 binary (we're in a Linux devcontainer)
gh release download "$VERSION" --pattern "abs-cli-linux-x64" --dir /tmp/release-verify
chmod +x /tmp/release-verify/abs-cli-linux-x64
/tmp/release-verify/abs-cli-linux-x64 self-test
rm -rf /tmp/release-verify
```

Report the self-test result.

Clean up:
```bash
rm -rf release-artifacts release-notes.md
```

**GATE: Ask the human to check the GitHub Release page.** They should verify:
- All 6 platform binaries are attached
- Release notes render correctly
- Everything looks right

## Step 7: Done

Report:
- Release URL
- Version number
- Number of binaries attached
- Self-test result

## Rules

- NEVER skip a human gate
- NEVER proceed past a failed check
- If anything unexpected happens, stop and ask
- Do not push code changes — this is a release-only workflow
- Clean up temporary files (release-notes.md, release-artifacts/) at the end
