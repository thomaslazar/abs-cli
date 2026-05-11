# Remove `abs-cli items search` subcommand

**Status:** Approved, ready for implementation plan.
**Target release:** 0.4.0.

## Background

`abs-cli items search` and the top-level `abs-cli search` are functional
duplicates. Both call `GET /api/libraries/{id}/search` with the same
`--query`, `--library`, and `--limit` options, return the same
`SearchResult` shape, and accept identical inputs. The duplication exists
because `items search` shipped first (in `f62a3b3`, with the original
`items` command suite) and the top-level `search` was added later
(`ed4ad85`, "add global search command") as the canonical entry point.

Since then the `items search` `--help` has carried a note saying it is an
alias of `abs-cli search` and instructing users to "prefer that". The
roadmap (`docs/roadmap.md`, "Planned breaking changes") committed to
removing it in the next minor bump after v0.2.x. We are now on 0.3.0,
which is past that window, so the deprecation contract has been met.

The duplication has a real maintenance cost: `ItemsService.SearchAsync`
and `SearchService.SearchAsync` are byte-for-byte the same code path,
and every help-text or response-shape change has to be applied twice.

## Goal

Remove `abs-cli items search` and the `ItemsService.SearchAsync` method
that backs it, in version 0.4.0. Top-level `abs-cli search` becomes the
single way to invoke the library search endpoint.

## Non-goals

- No runtime deprecation warning or transitional alias. The
  help-text-level deprecation has been in place through 0.2.x and
  0.3.0; a stderr warning at this point would violate the
  "thin pass-through, no smart behavior" CLI principle without adding
  meaningful protection.
- No changes to top-level `abs-cli search` (command, help text,
  service, or response shape).
- No changes to `SearchResult`, `AppJsonContext.Default.SearchResult`,
  or `ResponseExamples.g.cs` — these are still consumed by the
  top-level command and by `self-test`.
- No CHANGELOG.md edit. The release workflow owns CHANGELOG; feature
  branches must not touch it.

## Changes

### Source

- `src/AbsCli/Commands/ItemsCommand.cs`
  - Remove the `private static Command CreateSearchCommand()` method
    (currently lines ~105–150).
  - Remove the registration line
    `command.Subcommands.Add(CreateSearchCommand());`
    inside `Create()` (currently line 15).
  - Remove any `using` directives that become unused (e.g. an unused
    `AbsCli.Models` import, if applicable — verify after the edit).

- `src/AbsCli/Services/ItemsService.cs`
  - Remove `public async Task<SearchResult> SearchAsync(string libraryId,
    string query, int? limit)`.
  - Remove any `using` directives that become unused as a result.

No other production code references the deleted method
(grep-verified: only `ItemsCommand.CreateSearchCommand` called
`ItemsService.SearchAsync`).

### Smoke test

- `docker/smoke-test.sh`
  - Remove `"items search"` from the leaf-command help+examples loop
    (currently in the list at line 104).
  - Delete the two `items search` assertion blocks (currently lines
    ~205–211): the "Final Empire" positive case and the
    `zzz_nonexistent_xyz` empty-result case.
  - Top-level `abs-cli search` assertions already exist (currently
    lines ~492–499) and continue to cover the endpoint behavior, so no
    new smoke coverage is needed.

### Docs

- `docs/cli-design.md`
  - Remove the table row for `abs-cli items search --query <text>`
    (currently line 20). The corresponding row for top-level
    `abs-cli search` remains.

- `docs/roadmap.md`
  - Remove the "Remove `abs-cli items search`" row from the
    "Planned breaking changes" table. If that leaves the section
    empty, remove the section header as well; it can be re-added when
    the next breaking change is queued.

### NOT touched

- `CHANGELOG.md` — release workflow only.
- `tests/AbsCli.Tests/Commands/HelpOutputTests.cs` and other tests —
  grep-verified, no `items search`-specific assertion exists.
- `src/AbsCli/Models/SearchResult.cs`, `Models/JsonContext.cs`,
  `Commands/ResponseExamples.g.cs`, `Commands/SelfTestCommand.cs`,
  `tools/GenerateResponseExamples/Program.cs` — all still required by
  top-level `search` and self-test.

## User-facing impact

After 0.4.0 ships:

- `abs-cli items search ...` returns the standard System.CommandLine
  "unrecognized command or argument" error and a nonzero exit code.
- `abs-cli items --help` no longer lists `search` in its subcommands.
- `abs-cli search ...` is unchanged in every observable way.

No silent behavior change exists: there is no command whose response
shape, options, or exit codes change as a result of this work. The
only delta is that one path stops existing.

## Verification

Before the PR is opened:

- `dotnet test` — all unit tests pass. `HelpOutputTests` has no
  `items search`-specific assertion, so no test edits are required.
- `dotnet format AbsCli.sln --verify-no-changes` — formatting clean.
- Local docker smoke run, per `CLAUDE.md`:
  - `cd docker && docker compose up -d`
  - `ABS_URL=http://<container-ip>:80 bash docker/seed.sh` if the
    stack was just created.
  - `ABS_URL=http://<container-ip>:80 bash docker/smoke-test.sh` —
    must pass; the loop iterates one fewer leaf command and the two
    `items search` assertions are gone, but top-level `search`
    coverage is unchanged.
- Eyeball checks:
  - `abs-cli items --help` no longer mentions `search`.
  - `abs-cli items search --query x` fails with the standard CLI
    "unknown command" message.
  - `abs-cli search --query "Mistborn"` is byte-for-byte unchanged
    against the dev stack.

## Risks

- **Anyone scripting against `items search`.** Mitigated by the
  documented deprecation in `--help` and the roadmap entry that have
  been in place for two minor releases. The migration is mechanical:
  drop the `items` segment.
- **Hidden coupling we missed.** Grep across `src/`, `tests/`,
  `tools/`, `docker/`, and `docs/` is the basis for "no other
  references". The implementation plan should re-run that grep as a
  guard step before deletion, to catch any post-spec additions.
