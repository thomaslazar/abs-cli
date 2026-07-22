# CLAUDE.md

## Main rule
be brief

## Git Conventions

- **Ask before committing** after ad-hoc or exploratory changes — report what changed, then ask. Exception: when executing a pre-approved implementation plan whose tasks specify commit messages, commit per the plan without pausing each task (the plan is the approval). Never autonomous for amends, force pushes, or commits to `main`.
- **Conventional Commits** format required: `type: subject`
- Types: `feat`, `fix`, `docs`, `test`, `ci`, `refactor`, `chore`
- Subject line: imperative mood, lowercase, no period, max ~72 chars
- Body (optional): explain *why*, not *what*. Wrap at 72 chars.
- Do NOT include `Co-Authored-By:` lines in commit messages.
- Do NOT add "Generated with Claude Code" or similar attribution lines to PRs, commits, or any auto-generated content.
- After creating a pull request, always present the PR URL as a clickable link (plain URL on its own line or markdown link format) so the user can open it directly.

Examples:
```
feat: add backup create and restore commands
fix: use accessToken instead of legacy user.token
docs: update testing strategy for AOT validation
test: add metadata update assertion to smoke tests
```

## Pre-PR verification

- Run `docker/smoke-test.sh` against the local docker-compose dev stack before opening any PR. Unit tests and `self-test` are not enough — many regressions only surface in the live HTTP path.
- The compose stack lives at `docker/docker-compose.yml`; bring it up with `cd docker && docker compose up -d`. Resolve the container IP via `docker inspect docker-audiobookshelf-1 -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}'` and run the smoke as `ABS_URL=http://<container-ip>:80 bash docker/smoke-test.sh` — the `host.docker.internal` default does not work from inside the dev container.
- Seed first if the stack is freshly created: `ABS_URL=http://<container-ip>:80 bash docker/seed.sh`.
- Only mark "smoke test passed" in a PR description after actually running it. Do not copy the checkbox forward unverified.

## Post-PR verification

- After `gh pr create`, watch CI until every check is in a terminal state (pass / fail / skipping). A PR is not done at "PR open" — it is done at "all required checks green." Surface the result back to the user without prompting.
- `gh pr checks <num>` for one-shot status; `gh run watch <run-id>` or a polling Monitor for long-running jobs.
- If a check fails, diagnose before declaring the PR ready. Flaky races warrant a rerun and a follow-up fix in the same PR (e.g. the HelpExtensions concurrency fix that landed in PR #43 alongside the 2.35.0 bump).

## Docs, specs & roadmap

- **Specs** go in `docs/specs/YYYY-MM-DD-<topic>-design.md`, **plans** in `docs/plans/YYYY-MM-DD-<topic>.md` — never `docs/superpowers/…`, whatever a skill defaults to.
- **Hold spec/plan commits until the implementation branch exists**, then commit spec + plan + code together on that branch so design and delivery are reviewed as one unit. Don't commit them to `main` up front.
- **Once a feature branch exists, keep its docs/roadmap edits on that branch** — they reach `main` via the PR. Don't detour to a separate direct-to-`main` commit for a change the PR will carry anyway.
- **`CHANGELOG.md` is owned by the release process** (`release/v{version}` branches only). Never add or edit changelog entries from a feature/fix branch, and never add an "update CHANGELOG" step to a plan.
- **`docs/roadmap.md`:** don't mark individual items done mid-milestone; the whole milestone moves from `## Next` to `## Completed milestones` at release time. Adding spec/plan pointers to in-progress bullets is fine.

## Code Formatting

- `.editorconfig` (from dotnet/runtime) enforces style. CI checks with `dotnet format --verify-no-changes`.
- Run `dotnet format AbsCli.sln` after writing or modifying C# files.
- If formatting check fails in CI, run the format command and commit the fix.
- **No unnecessary blank lines** inside method bodies: no blanks between consecutive `AddCommand`/`AddOption` calls, no blank before `return` after setup calls, no blanks between consecutive variable declarations of the same kind. Keep methods compact — see `AuthorsCommand.cs` as reference.

## CLI design principles

- **Thin pass-through.** Each command maps to a single ABS API endpoint. No smart defaults that pre-fetch extra data, no reading the response to emit derived warnings, no client-side mirroring of server policy. Required inputs match the endpoint's required inputs; workflows spanning multiple endpoints are the caller's job to compose, not folded into one verb. Presentation-only output shaping (e.g. `--output -` for binary streaming) is the only exception.

## Command implementation conventions

- **Permission tagging.** Every command whose underlying ABS endpoint requires a non-default permission MUST call `command.AddPermissionRequired("<token>")` immediately after construction, where `<token>` is one of `admin`, `update`, `upload`, `download`, `delete`. Commands callable by any authenticated user (reads, lookups) get no call. The token must agree with the controller's permission check in `temp/audiobookshelf/server/controllers/`.
- **Permission hint mirroring.** When the underlying service method's HTTP call needs a `permissionHint`, the hint string MUST match the tag: tag `update` ↔ hint `"'update' permission"`; tag `delete` ↔ hint `"'delete' permission"`; tag `upload` ↔ hint `"'upload' permission"`; tag `admin` ↔ hint `"admin permission"` (NO quotes around `admin` — `admin` is a user *type* in ABS's model, not a flag in the `user.permissions` object; the quoted forms name literal flag keys like `permissions.update`). The help-section tag and the 403 error message should always agree.
- **README Commands table.** Any PR that adds, renames, or removes a CLI verb, OR adds/removes a user-visible flag on an existing command, MUST update the Commands table in `README.md` in the same change.
- **Positional args for value-only subcommands.** Subcommands whose parameters ARE the values (no ID key) take positional args, not flags: `tags rename <old> <new>`, `tags delete <tag>` (mirrors `config set <key> <value>`). ID-keyed resources still use `update --id --name` where flags mirror ABS body field names.
- **`libraries delete` is intentionally confirm-gated** — it pre-fetches the library to show its name and requires the operator to type the exact name on stdin (no `--yes`/`--force` bypass). A deliberate exception to thin pass-through, justified by its destructive cascade. Do not "fix" it, add a bypass flag, or propagate the gate to other delete commands (they stay prompt-free).

## Help text

`--help` is the primary interface for the AI agents that consume this CLI, and every word costs tokens. Keep it terse and self-contained.

- **Terse.** One-liners over prose, bullets over paragraphs, no "useful when…" framing. Calibrate against the leaner existing commands (e.g. `AuthorsCommand.cs`).
- **Document every non-obvious caveat** at the call site — destructive side effects, hidden API behaviors (silent merge-on-rename, scanner auto-delete), unit mismatches, outcome-affecting defaults. The CLI is thin, so API quirks leak through; help text is where they must surface, not spec docs.
- **Don't state what's already visible.** Skip anything apparent from the flags, subcommand list, or response-shape sample: no verb-by-verb group narration, no "X cannot change" when there's no such flag, no restating a flag's own description or a response field.
- **Cross-references are one-way** (consumer → producer) and allowed only when required to use *this* command: where a required input comes from, a behavior warning, a piping/unit pitfall, a shared external dependency. Never sell another command's use case.

## ABS Source Reference

- The ABS server source is the authoritative reference for API behavior, request/response shapes, and routing — `https://api.audiobookshelf.org` is **stale** and unreliable.
- Expected location: `temp/audiobookshelf/` (gitignored). If missing, clone the currently supported version before referencing API code:
  ```bash
  # Supported version is set in src/AbsCli/Api/AbsApiClient.cs (MinSupportedVersion / MaxTestedVersion)
  git clone --depth 1 --branch v2.35.1 https://github.com/advplyr/audiobookshelf.git temp/audiobookshelf
  ```
- Replace the version tag with whatever `MaxTestedVersion` is currently set to.
- Use this checkout to verify endpoints, controllers, request/response shapes, and permission checks before designing or changing CLI commands.

