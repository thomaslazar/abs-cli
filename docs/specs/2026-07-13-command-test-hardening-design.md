# Command Test-Hardening — Design

Date: 2026-07-13
Status: Approved (brainstorm)

## Goal

Close command-test coverage gaps identified in an audit of the CLI. Seven
command areas have no dedicated command tests (only generic help/permission
coverage or none). This adds tests, and — for the logic-heavy commands — a
small, behavior-preserving refactor that makes their client-side validation
unit-testable. One PR.

## Approach

Two tiers:

- **Logic-heavy (Upload, Items base verbs, Config):** the client-side validation
  currently lives inline in `SetAction` lambdas calling `Environment.Exit(1)`,
  so it cannot be unit-tested. Extract each check into an `internal static` pure
  helper returning `string?` (`null` = valid; otherwise the exact error message).
  The action calls the helper, and on a non-null result does
  `_logger.Error(msg); Environment.Exit(1);`. **No behavior change** — same
  messages, same exit codes. Unit-test the helpers across every branch. Mirrors
  the existing `BuildUpdateBodyForTesting` precedent (Authors/Series).
- **Thin (Libraries, Backup, Search, Metadata):** help/structure tests only, no
  production changes — subcommand set, key options present, permission tags,
  documented caveats — matching `TagsCommandTests`/`AuthorsCommandTests` depth.

I/O-bound checks (file existence, manifest file read, JSON parse) stay inline and
are NOT unit-tested (smoke covers them); where a pure helper needs a file-exists
fact, the action computes it and passes it in as a bool so the helper stays pure.

## Per-command scope

### 1. UploadCommand (refactor + tests)
Extract into `internal static` helpers in `UploadCommand`:
- `ValidateUploadArgs(string? series, string? sequence, string? manifestPath, int fileCount, bool prefixSourceDir) → string?` — the five arg-combination branches: `--sequence` requires `--series`; `--sequence` non-empty; `--files` XOR `--files-manifest`; `--prefix-source-dir` XOR `--files-manifest`; require one of `--files`/`--files-manifest`. Returns the exact existing message per branch.
- `ValidateManifestEntries(List<UploadManifestEntry>? entries) → string?` — empty/null array; entry missing `src`/`as`. (JSON parse + file read remain in `BuildFromManifestAsync`.)
- `DetectDuplicates(IReadOnlyList<(string LocalPath, string UploadName)>) → string?` — refactor the existing `CheckForDuplicates` to return the multi-line duplicate message (or null); action logs+exits.

Tests (`UploadCommandTests.cs`): each `ValidateUploadArgs` branch (happy + each error), manifest-entry validation branches, duplicate detection (none / one collision / case-insensitive collision), plus help/structure (options present, `upload` permission tag).

### 2. ItemsCommand base verbs (refactor + tests)
Extract shared input-source resolution used by `update`/`batch-update`/`batch-get`:
- `ValidateInputSource(string? input, bool stdin, bool inputIsExistingFile) → string?` — `--input` given but not an existing file → "must be a file path…"; neither `--input` nor `--stdin` → "Provide --input <file> or --stdin". Action computes `File.Exists(input)` and passes it in.

Tests (`ItemsCommandTests.cs`): each `ValidateInputSource` branch; help/structure for `list` (filter groups + sort fields already asserted in `HelpOutputTests` — do NOT duplicate; cover the option set), `get`, `update`, `batch-get`, `batch-update`, `batch-delete`, `scan`, including permission tags (`update` on the write verbs).

### 3. ConfigCommand (refactor + tests)
Extract the `set` key handling:
- `ApplyConfigSet(AppConfig config, string key, string value) → string?` — `server`/`defaultLibrary` mutate the config and return null; unknown key returns "Unknown config key: '<key>'. Valid keys: server, defaultLibrary". Action calls it, logs+exits on non-null, else saves.

Tests (`ConfigCommandTests.cs`): set `server`, set `defaultLibrary`, unknown key; help/structure for `get`/`set`/`path` (positional args on `set`).

### 4–7. Thin commands (help/structure tests only)
- `LibrariesCommandTests.cs` — subcommands (list/get/scan/…), options, permission tags.
- `BackupCommandTests.cs` — subcommands (create/list/apply/download/delete/upload), admin permission tags, documented caveats.
- `SearchCommandTests.cs` — `--query` required, other options.
- `MetadataCommandTests.cs` — subcommands (search/providers/covers), options.

Exact subcommand/option lists to be enumerated from the sources in the plan.

## Out of scope (YAGNI)
- No new HTTP-behavior tests (no injectable client seam; smoke covers live paths).
- No behavior changes beyond mechanical extraction (identical messages/exit codes).
- No new smoke assertions.
- No changes to already-well-covered commands (Authors, Collections, Tags, Genres,
  Narrators, Series, Me, Cache, Changelog, Login, Items sub-features).

## Testing / verification
- Unit: the new helper tests + help/structure tests above.
- Full `dotnet test AbsCli.sln` green; `dotnet format --verify-no-changes` clean.
- Run `docker/smoke-test.sh` against the compose stack before the PR to confirm
  the Upload/Items/Config refactors did not regress the live paths. NO new smoke
  assertions are added — this run is a regression check only.

## Notes
- This is the follow-up recorded after the series/narrators PR (#58). See
  `docs/roadmap.md` / memory `project_test_hardening_followup`.
- No README/coverage-doc changes (no user-visible CLI surface change).
- Do NOT edit `CHANGELOG.md` (release-owned).
