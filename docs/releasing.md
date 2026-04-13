# Releasing

## Versioning

Semantic versioning: `MAJOR.MINOR.PATCH`

- **0.x.y** — pre-1.0, breaking changes allowed between minor versions
- **MINOR** — new commands, new features
- **PATCH** — bug fixes, doc updates, dependency bumps, ABS compatibility updates (tested version range bump with no CLI changes)

## Release Workflow — Agentic Flow with Human Gates

The release process is a multi-step agentic workflow. An agent drives each
step, but pauses at human gates for review before proceeding. Invoked via
`/release` in Claude Code.

### Step 1: Preflight (agent)

Agent verifies prerequisites:
- On `main` branch, working tree clean
- Format check (`dotnet format --verify-no-changes`)
- All unit tests pass (`dotnet test`)
- AOT build + self-test (25 checks)
- Smoke test against ABS if Docker available (71 assertions)
- Determine version based on commits since last tag

**Human gate:** Confirm the version number.

### Step 2: Create release branch and bump version (agent)

```bash
git checkout -b release/v{version}
```

Update `<Version>` in `src/AbsCli/AbsCli.csproj` to match the new version.
This is what `abs-cli --version` reports and what gets sent in the
HTTP `User-Agent` header. Forgetting this leaves the binary self-reporting
the previous version.

After editing, rebuild the AOT binary and confirm `abs-cli --version`
prints exactly the new version (no `+sha` suffix — that is suppressed
in the csproj). If it doesn't match, stop and investigate before
committing.

```bash
git add src/AbsCli/AbsCli.csproj
git commit -m "chore: bump version to {version}"
```

### Step 3: Generate release notes (agent)

Agent writes `release-notes.md` with two sections:

**Highlights** — 3-5 bullet points in plain language.

**Changes** — auto-grouped from conventional commits since last tag.

**Human gate:** Review and approve the release notes.

Agent then prepends the notes to `CHANGELOG.md` and commits.

### Step 4: Open PR for CI validation (agent)

```bash
git push -u origin release/v{version}
gh pr create --title "release: v{version}" --base main
```

CI runs the full pipeline (unit tests, 6-platform AOT builds + self-test,
smoke test against live ABS). Agent monitors and reports results.

**Human gate:** CI passed — review and merge the PR.

### Step 5: Tag and create GitHub Release (agent)

After merge, agent switches to main and creates the release using the
same `release-notes.md` from step 3:

```bash
git checkout main && git pull
gh release create v{version} --title "v{version}" --notes-file release-notes.md
```

**Human gate:** Confirm the release was created.

### Step 6: Wait for release CI (agent)

The release event triggers CI which builds all 6 platforms and
**automatically attaches binaries** to the GitHub Release.

Agent monitors the run and reports results.

### Step 7: Verify (agent + human)

Agent downloads one binary and runs `self-test`.

**Human gate:** Check the GitHub Release page — all 6 binaries attached,
notes render correctly.

### Step 8: Done

Agent cleans up (`release-notes.md`, any temp files) and reports summary.

## Human Gates Summary

| After step | Agent pauses for | Why |
|------------|-----------------|-----|
| 1. Preflight | Version confirmation | Human decides the version |
| 3. Release notes | Notes review | Human quality-checks what users see |
| 4. CI validation | Merge approval | Full CI must pass before tagging |
| 5. Create release | Release URL confirmation | Point of no return for the tag |
| 7. Verify | Final visual check | Human confirms the public-facing page |

## Implementation

Implemented as `.claude/skills/release/SKILL.md` — a project-local skill
invoked via `/release` in Claude Code. The skill is human-invocable only
(`disable-model-invocation: true`) to prevent accidental releases.

`CHANGELOG.md` is the permanent record. GitHub Release notes mirror it.
`release-notes.md` is the working document (gitignored) — written by agent,
reviewed by human, prepended to CHANGELOG.md, passed to `gh release create`.

CI auto-attaches binaries to GitHub Releases via `gh release upload` in
the build job (`permissions: contents: write`).
