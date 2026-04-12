---
name: release
description: Create a new abs-cli release with human review gates. Creates release branch, generates changelog, opens PR for CI validation, then tags and publishes after merge.
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

# Format check
dotnet format AbsCli.sln --verify-no-changes

# Unit tests must pass
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj

# Build AOT binary and run self-test (catches AOT serialization issues)
dotnet publish src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true -o ./publish
./publish/abs-cli self-test
rm -rf publish/
```

If Docker is available and an ABS instance is running (or can be started),
also run the full smoke test:

```bash
docker compose -f docker/docker-compose.yml up -d
bash docker/seed.sh
CLI=./publish/abs-cli bash docker/smoke-test.sh
docker compose -f docker/docker-compose.yml down -v
```

If Docker is not available, note this in the preflight report — the smoke
test will still run in CI after the PR is created.

If any check fails, stop and report the issue. Do not proceed.

Determine the version number:
- Get the last tag: `git describe --tags --abbrev=0 2>/dev/null || echo "none"`
- Read commits since last tag
- Propose a version based on conventional commits:
  - Any `feat:` commits → bump MINOR
  - Only `fix:`, `docs:`, `test:`, `ci:`, `chore:` → bump PATCH
  - ABS compatibility bumps with no CLI changes → PATCH
  - See `docs/releasing.md` for versioning rules

**GATE: Ask the human to confirm the version number.** Show them the proposed
version and the commit summary. Wait for their response.

## Step 2: Create Release Branch

```bash
VERSION="v{version}"  # from step 1, e.g. "v0.1.0"
git checkout -b "release/${VERSION}"
```

## Step 3: Generate Release Notes

Generate release notes into `release-notes.md` with two sections:

**Highlights** — 3-5 bullet points describing what's new in plain language.
Focus on what users care about, not implementation details.

**Changes** — auto-grouped from conventional commits since last tag:

```bash
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
if [ -n "$LAST_TAG" ]; then
    RANGE="${LAST_TAG}..HEAD"
else
    RANGE="HEAD"
fi
git log --oneline $RANGE --pretty="- %s" | grep -E "^- (feat|fix|refactor|docs|test|ci|chore):" | sort
```

Write `release-notes.md` in this format:

```markdown
## {version} — YYYY-MM-DD

### Highlights
- ...

### Features
- feat: ...

### Fixes
- fix: ...
```

**GATE: Show the release notes to the human.** Ask them to review and approve.
If they want edits, make them and show again.

Then prepend the release notes to `CHANGELOG.md` (create the file if it
doesn't exist). Keep a header at the top:

```markdown
# Changelog

All notable changes to abs-cli are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/).

{contents of release-notes.md}

{previous entries...}
```

Commit the changelog:

```bash
git add CHANGELOG.md
git commit -m "docs: add v{version} changelog entry"
```

## Step 4: Open PR for CI Validation

Push the release branch and open a PR:

```bash
git push -u origin "release/${VERSION}"
gh pr create --title "release: ${VERSION}" --body "Release ${VERSION}. See CHANGELOG.md for details." --base main
```

Wait for CI to complete:

```bash
RUN_ID=$(gh run list --branch "release/${VERSION}" --limit 1 --json databaseId -q '.[0].databaseId')
gh run watch "$RUN_ID" --exit-status
```

If CI fails:
- Show failure details: `gh run view "$RUN_ID" --log-failed`
- Stop and report. Fix issues on the release branch, push, and re-check.

Report CI results (all jobs, times).

**GATE: Tell the human CI passed. Ask them to review and merge the PR.**
Show the PR URL. Wait for them to confirm the merge is done.

## Step 5: Tag and Create GitHub Release

After the PR is merged, switch back to main and create the release.
The `release-notes.md` from step 3 is still available (gitignored, not committed).

```bash
git checkout main
git pull
gh release create "${VERSION}" --title "${VERSION}" --notes-file release-notes.md
```

Clean up after the release is created:
```bash
rm release-notes.md
```

Show the release URL.

**GATE: Confirm the release was created.** Show the URL.

## Step 6: Wait for Release CI

The release triggers CI which builds all 6 platforms and **automatically
attaches binaries** to the GitHub Release. Monitor it:

```bash
# Wait for the run to appear
for i in $(seq 1 10); do
    RUN_ID=$(gh run list --limit 5 --json databaseId,event -q '[.[] | select(.event=="release")] | .[0].databaseId')
    [ -n "$RUN_ID" ] && break
    sleep 3
done
gh run watch "$RUN_ID" --exit-status
```

If CI fails, show failure details and stop.

Report CI results (all jobs, times).

## Step 7: Verify

Download and test one binary:

```bash
gh release download "${VERSION}" --pattern "abs-cli-linux-x64" --dir /tmp/release-verify
chmod +x /tmp/release-verify/abs-cli-linux-x64
/tmp/release-verify/abs-cli-linux-x64 self-test
rm -rf /tmp/release-verify
```

**GATE: Ask the human to check the GitHub Release page.** They should verify:
- All 6 platform binaries are attached
- Release notes render correctly
- Everything looks right

## Step 8: Done

Report:
- Release URL
- Version number
- Number of binaries attached (should be 6)
- Self-test result
- Changelog committed to repo

## Rules

- NEVER skip a human gate
- NEVER proceed past a failed check
- If anything unexpected happens, stop and ask
- Clean up temporary files at the end
- The CHANGELOG.md entry is the source of truth — GitHub Release notes mirror it
