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

```bash
# Tag the release
git tag -a v0.1.0 -m "v0.1.0"
git push origin v0.1.0
```

This triggers the CI release workflow which:
1. Runs unit tests
2. Builds AOT binaries for all 5 platforms
3. Runs self-test on each binary
4. Runs smoke tests against live ABS (linux-x64)
5. Creates a GitHub Release with binaries attached

### 3. Verify

- Check the GitHub Release page — all 5 binaries should be attached
- Download at least one binary and run `abs-cli self-test`
- Verify the release notes render correctly

## CI Release Job

The release workflow is triggered by `release: created` events. It needs a
dedicated job that:

1. Builds all 5 platform binaries
2. Attaches them to the GitHub Release as assets
3. Includes the release notes

**Note:** The release CI job automation is planned for implementation after
the v0.1.0 PR is merged. The first release may require manually attaching
binaries downloaded from the PR's CI artifacts.

## Release Checklist

For agents executing a release:

```
- [ ] All tests pass on main (unit + smoke)
- [ ] Write release notes (highlights + grouped commits)
- [ ] Human reviews release notes
- [ ] Tag: git tag -a v{version} -m "v{version}"
- [ ] Push tag: git push origin v{version}
- [ ] CI builds and publishes release (or manually attach artifacts)
- [ ] Verify: download binary, run self-test
- [ ] Verify: GitHub Release page looks correct
```

## First Release (v0.1.0)

Since the release CI automation doesn't exist yet:

1. Merge the implementation PR
2. Write release notes (agent + human review)
3. Create a GitHub Release manually via `gh release create`
4. Download CI artifacts from the last green PR run
5. Attach binaries to the release
6. After this release, implement the automated release workflow
