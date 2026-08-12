# Input/Output

## Output

- Commands that return API data write JSON to stdout
- JSON matches ABS API response structure exactly — no transformation
- Errors, warnings and progress go to stderr, never stdout — stdout stays pipeable
- List commands return the ABS pagination envelope: `{ "results": [...], "total": N, "limit": N, "page": N }`

Exceptions to JSON-on-stdout:

- `--output -` streams raw bytes to stdout (`items cover download`,
  `items ebook download`, `authors image download`); with a file path, stdout
  stays empty
- Side-effect-only commands (`login`, `config set`) write a human
  confirmation to stderr and nothing to stdout
- `changelog` writes human-readable text to stdout; `self-test` writes its whole
  report to stderr

## Exit Codes

- `0` — success
- `1` — general error (bad arguments, config missing)
- `2` — API error (401, 404, 500 from ABS)

## Input for Updates

```bash
# Single item from file (--input is file-path only)
abs-cli items update --id abc123 --input update.json

# Single item from stdin (pipe inline JSON)
echo '{"metadata":{"language":"English"}}' | abs-cli items update --id abc123 --stdin

# Batch from file
abs-cli items batch-update --input updates.json

# Batch from stdin (pipe from agent)
cat corrections.json | abs-cli items batch-update --stdin
```

Update JSON format matches what the ABS API expects — `PATCH /api/items/{id}/media`
for single items, `PATCH /api/items/batch/update` for batch. No custom schema.

## Pipeline Support

```bash
# Full agent workflow
abs-cli items list --filter "languages=" > missing.json
cat missing.json | claude-agent-process > corrections.json
abs-cli items batch-update --input corrections.json

# Direct pipe
abs-cli items list --filter "languages=" | agent-process | abs-cli items batch-update --stdin
```
