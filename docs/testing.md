# Testing

## Principle

Always test the compiled AOT binary, not JIT-mode `dotnet run`. If `dotnet run`
works but the AOT binary crashes, you've shipped a broken release. AOT disables
reflection-based serialization — bugs only surface in the real binary.

## Three Test Layers

### 1. Unit Tests (xUnit)

14 tests covering pure logic with no network or binary dependency:

- `ConfigManagerTests` — config load/save round-trip, precedence resolution
- `FilterEncoderTests` — base64 encoding, pass-through, error cases
- `TokenHelperTests` — JWT expiry parsing, edge cases

```bash
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj
```

### 2. Self-Test (built-in command)

25 checks exercising all AOT-sensitive code paths without network access:

- Source-generated JSON round-trips for all 15 types in `JsonContext`
  (AppConfig, LoginRequest, LoginResponse, Dictionary, LibraryListResponse,
  Library, PaginatedResponse, LibraryItemMinified, SearchResult,
  UpdateMediaResponse, BatchUpdateResponse, BatchGetResponse, SeriesItem,
  AuthorItem, AuthorListResponse)
- Config save/load round-trip and precedence resolution
- Filter encoder (encode, pass-through, reject)
- Token helper (JWT parse, no-exp, garbage input)
- Console output (WriteJson, WriteRawJson)

```bash
abs-cli self-test
```

Runs on every CI build for all 6 platforms. Catches missing `[JsonSerializable]`
attributes, broken source generators, and platform-specific AOT issues.

### 3. Smoke Tests (bash, against live ABS)

71 assertions running the AOT binary against a real Audiobookshelf Docker
instance seeded with 15 books, 6 authors, and 3 series:

- All help screens render correctly (parent commands + leaf commands with examples)
- Filter groups and sort fields sections present in `items list --help`
- Config set/get round-trip
- Libraries list/get — correct count, correct library by ID and name
- Items list — 15 items, pagination (5 per page, total preserved)
- Items get — single item by ID with media metadata
- Items search — finds books by title, empty for garbage queries
- Items update — single field (title), multi-field (description + genres),
  update from file (publisher), all verified via get after write
- Items batch-get — fetch 2 items by ID via stdin
- Series list — 3 series with pagination, single series get by ID
- Authors list — 6 authors, find by name (Brandon Sanderson), get by ID
- Search — finds books by title, finds series by name

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
| smoke-test | AOT binary + live ABS (71 assertions) | linux-x64 only (Docker required) |
| build | AOT publish + self-test (25 checks) | linux-x64, linux-arm64, osx-arm64, osx-x64, win-x64, win-arm64 |

The smoke test is Linux-only because it needs a Docker ABS container.
Self-test runs on all platforms to validate AOT integrity everywhere.
