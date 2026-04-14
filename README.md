# abs-cli

A command-line interface for [Audiobookshelf](https://www.audiobookshelf.org/). Built for agent-driven metadata management — pipe JSON in, get JSON out.

Native AOT binary. No runtime required. ~10 MB.

> **Note:** This tool was built using agentic software engineering (AI-assisted coding) and reviewed by a human. See the git history for details.

## Features

- **JSON-only output** — stdout is always valid JSON matching the ABS API, errors go to stderr
- **Native AOT** — single self-contained binary, no .NET runtime needed
- **Upload with collision protection** — upload audiobooks/ebooks, detect duplicate filenames, auto-prefix or manifest-rename
- **Metadata search** — query ABS-configured providers (Audible, Google Books, etc.) and apply results
- **Backup & restore** — safety-net backups before bulk operations
- **Config precedence** — CLI flags > environment variables > config file
- **Token management** — automatic JWT refresh, proactive expiry detection
- **Batch operations** — update or fetch multiple items in one call
- **Filtering & pagination** — base64 filter encoding, sort, page, limit (default 50)

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
| Windows ARM64 | `abs-cli-win-arm64` |

```bash
chmod +x abs-cli-linux-x64
mv abs-cli-linux-x64 ~/.local/bin/abs-cli
```

**macOS users:** The binaries are not signed or notarized. macOS Gatekeeper
will block them on first run. Remove the quarantine attribute to allow
execution:

```bash
sudo xattr -d com.apple.quarantine abs-cli-osx-arm64
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
abs-cli config set defaultLibrary <library-id>

# List items
abs-cli items list

# Search
abs-cli search --query "Brandon Sanderson"

# Upload a book
abs-cli upload --title "The Hobbit" --author "J.R.R. Tolkien" --files hobbit.m4b --wait

# Search metadata providers
abs-cli metadata search --provider audible.de --title "The Hobbit" --author "Tolkien"

# Update metadata
abs-cli items update --id <item-id> --input '{"metadata":{"title":"New Title"}}'

# Create a backup before bulk changes
abs-cli backup create
```

## Agent use cases

abs-cli is designed as a set of sharp primitives that AI agents compose into workflows. The CLI handles the ABS API; the agent handles the decisions.

### Upload and catalogue new books

Point an agent at a directory of audiobook or ebook files. The agent:

1. Inspects the local files (filenames, directory structure, embedded metadata)
2. Determines author, title, series, and sequence from what it finds
3. Creates a backup (`abs-cli backup create`) as a safety net
4. Uploads the files (`abs-cli upload --author "..." --title "..." --series "..." --sequence N --wait --files ...`)
5. Searches a metadata provider (`abs-cli metadata search --provider audible.de --title "..." --author "..."`)
6. Reviews the results, picks the best match (checking language, narrator, series info)
7. Applies the metadata (`abs-cli items update --id <id> --input <metadata.json>`)

For multi-part audiobooks with files split across directories (e.g. "Part 1-2", "Part 3"), the agent uses `--prefix-source-dir` to avoid filename collisions, or builds a `--files-manifest` for explicit per-file naming.

### Metadata cleanup on request

You notice a problem in your library — lots of audiobooks missing the language field, inconsistent series naming, books without covers. You tell the agent what to fix:

> "I noticed many audiobooks are missing the language field. Can you check my library, determine the correct language for each, and fix the metadata? Ask me if you're unsure about any."

The agent:

1. Creates a backup (`abs-cli backup create`)
2. Searches for affected items (`abs-cli items search`, `abs-cli items list --limit 200` with pagination for broader checks)
3. For each affected item, inspects existing metadata (author name, title, description) to infer the language — or searches a provider to confirm (`abs-cli metadata search --provider audible.de --title "..."`)
4. Applies fixes it's confident about (`abs-cli items update --id <id> --input '{"metadata":{"language":"German"}}'`)
5. Asks you about ambiguous cases — "This book has a German author but an English title, which language should I set?"

Same pattern works for any metadata issue: missing series assignments, inconsistent author naming, books without ASIN/ISBN, missing covers. You describe the problem, the agent investigates and fixes, escalating when unsure.

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
abs-cli config set defaultLibrary <library-id>
```

## Commands

| Command | Description |
|---------|-------------|
| `login` | Authenticate and store tokens |
| `config get` | Show current configuration |
| `config set <key> <value>` | Set a configuration value (`server`, `defaultLibrary`) |
| `libraries list` | List all libraries |
| `libraries get --id <id>` | Get a single library |
| `libraries scan [--force]` | Trigger a library scan (admin, async) |
| `items list` | List items (`--filter`, `--sort`, `--limit`, `--page`, `--desc`) |
| `items get --id <id>` | Get a single item |
| `items search --query <text>` | Search items in a library |
| `items update --id <id> --input <json>` | Update item metadata |
| `items batch-update` | Batch update items (`--input <file>` or `--stdin`) |
| `items batch-get` | Batch get items by ID (`--input <file>` or `--stdin`) |
| `items scan --id <id>` | Scan a single item (admin, sync) |
| `series list` | List series (`--limit`, `--page`) |
| `series get --id <id>` | Get a single series |
| `authors list` | List authors |
| `authors get --id <id>` | Get a single author |
| `search --query <text>` | Search across a library |
| `upload` | Upload files to a library (`--title`, `--author`, `--series`, `--sequence`, `--wait`, `--files`, `--prefix-source-dir`, `--files-manifest`) |
| `backup create` | Create a server backup (admin) |
| `backup list` | List available backups (admin) |
| `backup apply --id <id>` | Restore from a backup (admin) |
| `backup download --id <id> --output <path>` | Download a backup file (admin) |
| `backup delete --id <id>` | Delete a backup (admin) |
| `backup upload --file <path>` | Upload a backup file (admin) |
| `metadata search` | Search a metadata provider (`--provider`, `--title`, `--author`) |
| `metadata providers` | List available metadata providers |
| `metadata covers` | Search for cover images (`--provider`, `--title`, `--author`) |
| `tasks list` | List active and recent tasks |
| `self-test` | Verify binary integrity (AOT validation, no network required) |

Every command supports `--help` with examples and reference sections.

## Development

### Dev container (recommended)

The repo includes a dev container with .NET 8, clang, and Docker support. Open in VS Code or GitHub Codespaces.

### Running tests

```bash
# Unit tests (14 tests)
dotnet test tests/AbsCli.Tests/AbsCli.Tests.csproj

# Self-test (33 AOT integrity checks, no network needed)
dotnet run --project src/AbsCli/AbsCli.csproj -- self-test

# Full smoke test against a live ABS instance (108 assertions)
docker compose -f docker/docker-compose.yml up -d
bash docker/seed.sh
bash docker/smoke-test.sh                          # builds AOT binary + runs 108 assertions
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
  seed.sh             # Seed test data (15 books, 6 authors, 3 series, 3 users)
  smoke-test.sh       # End-to-end CLI smoke tests
```

See [docs/](docs/) for architecture, authentication, build targets, and more.

## Compatibility

Tested against Audiobookshelf **2.33.1**. The CLI warns on login if the server version is outside the tested range. See [docs/abs-compatibility.md](docs/abs-compatibility.md) for the compatibility policy.

## License

[MIT](LICENSE)
