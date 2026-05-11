# Testing

## Principle

Always test the compiled AOT binary, not JIT-mode `dotnet run`. If `dotnet run`
works but the AOT binary crashes, you've shipped a broken release. AOT disables
reflection-based serialization — bugs only surface in the real binary.

## Three Test Layers

### 1. Unit Tests (xUnit)

132 tests covering pure logic, help-output assertions, and JSON-shape drift
guards with no network or binary dependency:

- `Api/` — `FilterEncoderTests`, `TokenHelperTests`, `FilenameSanitizerTests`
- `Commands/` — `HelpOutputTests`, `HelpExtensionsTests`, `AuthorsCommandTests`,
  `AuthorsImageCommandTests`, `ChangelogCommandTests`, `ChangelogReaderTests`,
  `ItemsCoverCommandTests`, `ResponseExamplesDriftTest`,
  `ResponseExamplesJsonValidTest`, `SampleJsonWalkerTests`
- `Configuration/` — `ConfigManagerTests`

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
```

### 2. Self-Test (built-in command)

45 checks exercising all AOT-sensitive code paths without network access:

- Source-generated JSON round-trips for every type registered in
  `JsonContext.cs` — config + auth, library / item / series / author resources,
  search + paginated responses, batch update/get, scan, backup, task, upload,
  cover, metadata, and the author match / image / request DTOs.
- Config save/load round-trip and precedence resolution
- Filter encoder (encode, pass-through, reject)
- Token helper (JWT parse, no-exp, garbage input)
- Console output (WriteJson, WriteRawJson)
- CHANGELOG resource embedded and parseable

```bash
abs-cli self-test
```

Runs on every CI build for all 6 platforms. Catches missing `[JsonSerializable]`
attributes, broken source generators, and platform-specific AOT issues.

### 3. Smoke Tests (bash, against live ABS)

155 assertions running the AOT binary against a real Audiobookshelf Docker
instance seeded with 15 books, 6 authors, and 3 series:

- All help screens render correctly (parent commands + leaf commands with examples)
- Filter groups and sort fields sections present in `items list --help`
- Config set/get round-trip
- Libraries list/get/scan — correct count, correct library by ID and name, scan kicks off
- Items list — pagination, total preserved, filter groups including `missing=`
- Items get — single item by ID with media metadata
- Items update — single field, multi-field, update from file, all verified via get after write
- Items batch-update / batch-get — stdin + file inputs
- Items scan — single-item sync scan
- Items cover — set via `--file` / `--server-path` / `--url`, get to file and to stdout, remove
- Series list — pagination, single series get by ID
- Authors list/get — pagination, filter by name, fetch by ID
- Authors match / lookup / update / delete / image — Audnexus-backed match,
  read-only lookup, edit fields, delete, image set/get/remove
- Search — top-level search finds books by title, finds series by name
- Backup — create, list, apply, download, delete, upload (admin)
- Upload — single file, multi-file, `--prefix-source-dir`, `--files-manifest`,
  sanitize-drift coverage, `--wait` polling
- Metadata — providers list, gated external provider tests
- Tasks — list active and recent
- Permission errors — non-admin user gets clear 403 messages

```bash
docker compose -f docker/docker-compose.yml up -d
bash docker/seed.sh
CLI=./path/to/abs-cli bash docker/smoke-test.sh
```

## Local Dev

- `docker compose up -d` starts ABS (stays running)
- Seed once on first setup or after container recreate
- `smoke-test.sh` builds AOT binary automatically if `CLI` is not set
- Works from inside the dev container (Docker-outside-of-Docker)

## CI Pipeline

| Job | What | Platforms |
|-----|------|-----------|
| unit-test | xUnit tests | ubuntu (any) |
| smoke-test | AOT binary + live ABS (155 assertions) | linux-x64 only (Docker required) |
| build | AOT publish + self-test (45 checks) | linux-x64, linux-arm64, osx-arm64, osx-x64, win-x64, win-arm64 |

The smoke test is Linux-only because it needs a Docker ABS container.
Self-test runs on all platforms to validate AOT integrity everywhere.
