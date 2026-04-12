# Configuration

## Config File

Location: `~/.abs-cli/config.json`

```json
{
  "server": "https://audiobookshelf.example.com",
  "accessToken": "eyJhbG...",
  "refreshToken": "eyJhbG...",
  "defaultLibrary": "f59e4771-a301-4dc0-a521-bbfa2d256c00"
}
```

## Precedence Order

Highest wins:

1. Command-line flags (`--server`, `--token`, `--library`)
2. Environment variables (`ABS_SERVER`, `ABS_TOKEN`, `ABS_LIBRARY`)
3. Config file (`~/.abs-cli/config.json`)

## Config Commands

| Command | Description |
|---------|-------------|
| `abs-cli config get` | Shows current config (tokens masked) |
| `abs-cli config set <key> <value>` | Generic config setter |
| `abs-cli config set default-library <id\|name>` | Sets default library. Validates it exists on the server. |

## Error Messages

- No server → `Error: No server configured. Run: abs-cli login`
- No token → `Error: Not authenticated. Run: abs-cli login`
- No library + no default → `Error: No library specified. Use --library <id|name> or set a default with: abs-cli config set default-library <id|name>`
- 401 from API → `Error: Authentication failed. Token may be expired. Run: abs-cli login`
