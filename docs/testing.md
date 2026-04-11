# Testing

## Principle

Always test the compiled binary, not the source code. If the CLI output is correct,
the internals are correct.

## Integration Tests

Tests run against a real ABS instance in Docker:

1. **Docker Compose** — spin up `advplyr/audiobookshelf:2.33.1` (pinned version)
2. **Seed** — create a deterministic dataset via the ABS API (20-30 items, 2 libraries,
   a few series and authors, intentional metadata gaps)
3. **Execute** — run the compiled `abs-cli` binary
4. **Assert** — validate JSON output matches expected results

## What Gets Tested

- Every command produces valid JSON on stdout
- Errors go to stderr with correct exit codes
- Auth flow — login, token storage, refresh, 401 handling
- Filtering — base64 encoding works correctly
- Batch update — items actually get modified
- Pipeline — `items list | items batch-update --stdin` end-to-end
- Config precedence — flags > env vars > config file

## Local Dev

Same Docker-based ABS instance, but persistent:

- `docker compose up -d` starts ABS (stays running)
- Seed once on first setup or after container recreate
- Run tests repeatedly during development
- Works from inside the dev container (Docker-outside-of-Docker)

## CI

- Starts fresh ABS container per run
- Seeds, runs tests, tears down
- Linux only (Docker required)
- Can test against multiple ABS versions (latest + oldest supported)
