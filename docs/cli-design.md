# CLI Design

## Command Pattern

```
abs-cli <resource> <action> [options]
```

All commands follow the same structure: a resource noun followed by an action verb.

## Resources

### items

The core resource. Covers both audiobooks and podcasts — "items" was chosen over "books"
because it's future-proof and maps to the Audiobookshelf API's `library-item` concept.

| Command | Description |
|---------|-------------|
| `items list` | List all items, optionally filtered |
| `items get --id <id>` | Get a single item by ID |
| `items search --query <text>` | Search items by text |
| `items update --id <id>` | Update a single item |
| `items files --id <id>` | List files for an item |
| `items progress --id <id>` | Get playback progress |

**Examples:**

```bash
abs-cli items list
abs-cli items list --type audiobook
abs-cli items get --id 123
abs-cli items search --query "Dune"
```

### episodes

Podcast-specific commands.

```bash
abs-cli episodes list --podcast-id 123
abs-cli episodes get --id 456
abs-cli episodes update --id 456 --played true
```

### series

```bash
abs-cli series list
abs-cli series get --id 1
abs-cli series books --id 1
abs-cli series check --id 1       # validate series data
abs-cli series fix --id 1 --apply  # fix detected issues
```

### authors

```bash
abs-cli authors list
abs-cli authors get --id 1
abs-cli authors items --id 1       # list items by author
```

### libraries

```bash
abs-cli libraries list
abs-cli libraries get --id 1
abs-cli items list --library 1     # list items in a library
```

### search

Global search across resource types.

```bash
abs-cli search --query "Dune" --type items
abs-cli search --query "Dune" --type series
abs-cli search --query "Dune" --type authors
```

## Media Type Filter

Applicable to items commands:

```bash
--type audiobook
--type podcast
```

## Output

- `--json` — Default. Machine-readable JSON. Source of truth.
- `--table` — Optional. Human-readable table formatting via Spectre.Console.

## Input

- `--input file.json` — Read input from a JSON file
- `--stdin` — Read input from standard input

## Filtering

Simple filter expressions for batch operations:

```
language=null
series!='Dune'
language=null AND series!=null
```

## Update Modes

**Single item:**

```bash
abs-cli items update --id 123 --language en
```

**Batch update with filter:**

```bash
abs-cli items update --filter "language=null" --set "language=en"
```

**Batch update from file (with confidence threshold for agent workflows):**

```bash
abs-cli items update --input updates.json --min-confidence 0.9
```

## Pipeline Support

Commands can be piped together, using JSON as the interchange format:

```bash
abs-cli items list --filter "language=null" --json | abs-cli items update --stdin
```

## Agent Workflow

The CLI is designed to support this pattern:

1. **List** — use the CLI to find items with missing or incorrect data
2. **Analyze** — an AI agent processes the output and determines corrections
3. **Update** — feed corrections back through the CLI with a confidence threshold
