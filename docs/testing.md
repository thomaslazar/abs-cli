# Testing Strategy

## Principle

Always test the compiled binary, not the source code. The CLI is an AOT-compiled single
binary — tests should exercise it the same way a user would.

## Integration Testing

Tests run against a real Audiobookshelf instance:

1. **Docker** — Spin up an Audiobookshelf container
2. **Seed** — Load a fixed, deterministic dataset
3. **Execute** — Run the compiled abs-cli binary against the instance
4. **Assert** — Validate JSON output matches expected results

This catches issues that unit tests miss: serialization bugs, API contract changes,
AOT trimming problems, and end-to-end data flow issues.

## CI/CD (GitHub Actions)

- **Build matrix** — Linux, Windows, macOS
- **Publish** — AOT-compiled binaries for each platform
- **Test** — Run Docker-based integration tests
- **Artifacts** — Upload binaries as release artifacts
