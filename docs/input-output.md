# Input/Output

## Output

- All commands write JSON to stdout
- JSON matches ABS API response structure exactly — no transformation
- Errors go to stderr, never stdout
- List commands return the ABS pagination envelope: `{ "results": [...], "total": N, "limit": N, "page": N }`

## Exit Codes

- `0` — success
- `1` — general error (bad arguments, config missing)
- `2` — API error (401, 404, 500 from ABS)

## Input for Updates

```bash
# Single item from JSON string
abs-cli items update --id abc123 --input '{"metadata":{"language":"English"}}'

# Single item from file
abs-cli items update --id abc123 --input update.json

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
