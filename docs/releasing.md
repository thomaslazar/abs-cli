# Releasing

## Versioning

Semantic versioning: `MAJOR.MINOR.PATCH`

- **0.x.y** — pre-1.0, breaking changes allowed between minor versions
- **MINOR** — new commands, new features
- **PATCH** — bug fixes, doc updates, dependency bumps, ABS compatibility updates (tested version range bump with no CLI changes)

## Release Workflow

### 1. Write release notes

An agent (or human) writes release notes with two sections:

**Highlights** — 3-5 bullet points describing what's new in plain language.
Written by an agent, reviewed by a human. Focus on what users care about,
not implementation details.

**Changes** — auto-grouped from conventional commits since the last tag:

```
### Highlights
- First release of abs-cli with full audiobook metadata management
- Native AOT binaries for 5 platforms — no runtime required
- ...

### Features
- feat: add login command with access+refresh token storage
- feat: add items commands (list, get, search, update, batch-update, batch-get)
- ...

### Fixes
- fix: resolve AOT reflection errors in ConfigManager and AbsApiClient
- ...
```

Generate the changes section:
```bash
# Changes since last tag (or all commits for first release)
git log --oneline v0.0.0..HEAD --pretty="- %s" | grep -E "^- (feat|fix|refactor|docs|ci|test):" | sort
```

### 2. Create the release

Save the release notes to a file (e.g. `release-notes.md`), then create
the GitHub Release with the notes attached:

```bash
gh release create v0.1.0 --title "v0.1.0" --notes-file release-notes.md
```

This creates a git tag, a GitHub Release with the notes, and triggers
the CI release workflow which:
1. Runs unit tests
2. Builds AOT binaries for all 6 platforms
3. Runs self-test on each binary
4. Runs smoke tests against live ABS (linux-x64)
5. Uploads binaries as CI artifacts (to be attached to the release)

After CI completes, download the artifacts and attach them:

```bash
# Download all artifacts from the release CI run
gh run download <run-id> --dir ./release-artifacts

# Attach binaries to the release
gh release upload v0.1.0 ./release-artifacts/abs-cli-*/*
```

### 3. Verify

- Check the GitHub Release page — all 6 binaries should be attached
- Download at least one binary and run `abs-cli self-test`
- Verify the release notes render correctly

## CI Release Job

The release workflow is triggered by `release: created` events. It builds
all 6 platform binaries and uploads them as CI artifacts. Attaching
artifacts directly to the GitHub Release as assets is planned for
automation after v0.1.0 — until then, download and attach manually.

## Release Checklist

For agents executing a release:

```
- [ ] All tests pass on main (unit + smoke)
- [ ] Write release notes to release-notes.md (highlights + grouped commits)
- [ ] Human reviews release notes
- [ ] Create release: gh release create v{version} --title "v{version}" --notes-file release-notes.md
- [ ] Wait for CI to complete (builds 6 platform binaries)
- [ ] Download artifacts: gh run download <run-id> --dir ./release-artifacts
- [ ] Attach binaries: gh release upload v{version} ./release-artifacts/abs-cli-*/*
- [ ] Verify: download a binary, run self-test
- [ ] Verify: GitHub Release page looks correct
- [ ] Clean up: rm release-notes.md release-artifacts/
```
