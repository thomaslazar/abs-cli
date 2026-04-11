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

14 assertions exercising all AOT-sensitive code paths without network access:

- Source-generated JSON serialization (all types in `JsonContext`)
- Config save/load round-trip
- Filter encoder
- Token helper
- Console output

```bash
abs-cli self-test
```

Runs on every CI build for all 5 platforms. Catches missing `[JsonSerializable]`
attributes, broken source generators, and platform-specific AOT issues.

### 3. Smoke Tests (bash, against live ABS)

37 assertions running the AOT binary against a real Audiobookshelf Docker instance:

- All 22 help screens render correctly
- Config set/get round-trip
- Libraries list/get return valid JSON with correct data
- Items list/search return paginated results
- Series list, authors list, search all return expected JSON structure

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
| smoke-test | AOT binary + live ABS (37 assertions) | linux-x64 only (Docker required) |
| build | AOT publish + self-test (14 assertions) | linux-x64, linux-arm64, osx-arm64, osx-x64, win-x64 |

The smoke test is Linux-only because it needs a Docker ABS container.
Self-test runs on all platforms to validate AOT integrity everywhere.
