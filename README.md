# abs-cli

A command-line interface for [Audiobookshelf](https://www.audiobookshelf.org/). Built for agent-driven metadata management — pipe JSON in, get JSON out.

Native AOT binary. No runtime required. ~9 MB.

> **Note:** This tool was built using agentic software engineering (AI-assisted coding) and reviewed by a human. See the git history for details.

## Features

- **7 commands** — login, config, libraries, items, series, authors, search
- **JSON-only output** — stdout is always valid JSON matching the ABS API, errors go to stderr
- **Native AOT** — single self-contained binary, no .NET runtime needed
- **Config precedence** — CLI flags > environment variables > config file
- **Token management** — automatic JWT refresh, proactive expiry detection
- **Batch operations** — update or fetch multiple items in one call
- **Filtering & pagination** — base64 filter encoding, sort, page, limit

## Installation

### Download a release

Grab the binary for your platform from the [latest release](https://github.com/thomaslazar/abs-cli/releases/latest):

| Platform | Binary |
|----------|--------|
| Linux x64 | `abs-cli-linux-x64` |
| Linux ARM64 | `abs-cli-linux-arm64` |
| macOS Apple Silicon | `abs-cli-osx-arm64` |
| macOS Intel | `abs-cli-osx-x64` |
| Windows x64 | `abs-cli-win-x64` |

```bash
chmod +x abs-cli-linux-x64
mv abs-cli-linux-x64 ~/.local/bin/abs-cli
```

### Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet publish src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true
# Binary at: src/AbsCli/bin/Release/net8.0/linux-x64/publish/abs-cli
```

## Quick start

```bash
# Authenticate
abs-cli login --server https://your-abs-server.com

# List libraries
abs-cli libraries list

# Set a default library
abs-cli config set default-library <library-id>

# List items with pagination
abs-cli items list --limit 20 --page 0

# Search
abs-cli search --query "Brandon Sanderson"

# Get a single item
abs-cli items get --id <item-id>

# Update metadata (from JSON string)
abs-cli items update --id <item-id> --input '{"metadata":{"title":"New Title"}}'

# Update metadata (from file)
abs-cli items update --id <item-id> --input payload.json

# Batch update (from stdin)
cat updates.json | abs-cli items batch-update --stdin
```

## Configuration

Config is stored at `~/.abs-cli/config.json`. Values resolve in this order:

1. **CLI flags** (`--server`, `--token`, `--library`)
2. **Environment variables** (`ABS_SERVER`, `ABS_TOKEN`, `ABS_LIBRARY`)
3. **Config file**

```bash
# View current config
abs-cli config get

# Set values
abs-cli config set server https://abs.example.com
abs-cli config set default-library <library-id>
```

## Commands

| Command | Description |
|---------|-------------|
| `login` | Authenticate and store tokens |
| `config get` | Show current configuration |
| `config set <key> <value>` | Set a configuration value |
| `libraries list` | List all libraries |
| `libraries get --id <id>` | Get a single library |
| `items list` | List items (supports `--filter`, `--sort`, `--limit`, `--page`, `--desc`) |
| `items get --id <id>` | Get a single item |
| `items search --query <text>` | Search items in a library |
| `items update --id <id> --input <json>` | Update item metadata |
| `items batch-update` | Batch update items (`--input <file>` or `--stdin`) |
| `items batch-get` | Batch get items by ID (`--input <file>` or `--stdin`) |
| `series list` | List series (supports `--limit`, `--page`) |
| `series get --id <id>` | Get a single series |
| `authors list` | List authors |
| `authors get --id <id>` | Get a single author |
| `search --query <text>` | Search across a library |
| `self-test` | Verify binary integrity (AOT validation, no network required) |

## Development

### Dev container (recommended)

The repo includes a dev container with .NET 8, clang, and Docker support. Open in VS Code or GitHub Codespaces.

### Running tests

```bash
# Unit tests (14 tests)
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj

# Self-test (AOT integrity, no network needed)
dotnet run --project src/AbsCli/AbsCli.csproj -- self-test

# Full smoke test against a live ABS instance
docker compose -f docker/docker-compose.yml up -d
bash docker/seed.sh
bash docker/smoke-test.sh                          # builds AOT binary + runs 37 assertions
docker compose -f docker/docker-compose.yml down -v
```

### Project structure

```
src/AbsCli/
  Commands/       # CLI command definitions (System.CommandLine)
  Services/       # Business logic (API orchestration)
  Api/            # HTTP client, endpoints, filter encoder, token helper
  Models/         # DTOs matching ABS API JSON exactly
  Configuration/  # Config file, env var, flag resolution
  Output/         # JSON stdout, stderr error helpers
tests/AbsCli.Tests/
  Configuration/  # ConfigManager unit tests
  Api/            # FilterEncoder, TokenHelper unit tests
docker/
  docker-compose.yml  # Local ABS instance for testing
  seed.sh             # Seed test data
  smoke-test.sh       # End-to-end CLI smoke tests
```

See [docs/](docs/) for architecture, authentication, build targets, and more.

## Compatibility

Tested against Audiobookshelf **2.33.1**. The CLI warns on login if the server version is outside the tested range. See [docs/abs-compatibility.md](docs/abs-compatibility.md) for the compatibility policy.

## License

[MIT](LICENSE)
