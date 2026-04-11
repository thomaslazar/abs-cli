# Architecture

## Goal

A thin CLI over the Audiobookshelf API. Designed to be agent-friendly (Claude, Codex),
deterministic, cross-platform, and fast to start.

## Language

C# with Native AOT compilation.

**Rationale:** Familiarity, good enough performance, and produces a single self-contained
binary with no runtime dependency. Native AOT eliminates the .NET runtime requirement on
target machines and gives fast cold-start times suitable for CLI usage.

## Layers

The architecture is intentionally simple — four layers, no frameworks, no magic:

```
CLI  →  Service  →  API Client  →  DTOs
```

- **CLI** — Command parsing, argument validation, output formatting. Uses System.CommandLine.
- **Service** — Business logic and orchestration. Coordinates API calls, applies filters.
- **API Client** — HTTP communication with Audiobookshelf. Thin wrapper around HttpClient.
- **DTOs** — Explicit data transfer objects. Serialized with System.Text.Json source generators.

## Stack

| Concern | Library | Why |
|---------|---------|-----|
| CLI framework | System.CommandLine | .NET standard, good AOT support |
| HTTP | HttpClient | Built-in, no dependencies |
| JSON | System.Text.Json (source generators) | AOT-compatible, no reflection |
| Output formatting | Spectre.Console (optional) | Rich table output for `--table` mode |

## Rules

These are hard constraints, not guidelines:

- **No Newtonsoft.Json** — incompatible with AOT trimming
- **No `dynamic`** — breaks AOT compilation
- **No reflection** — breaks AOT trimming, makes behavior non-deterministic
- **Explicit DTOs** — every API response has a typed model, no anonymous objects
- **All commands support `--json`** — JSON is the default and source of truth
- **JSON = source of truth** — the `--json` output is the canonical, machine-readable format

## Build Targets

- Native AOT compiled
- Trimmed (unused code removed)
- Single binary output (no runtime dependency)

## Key Principles

- **CLI dumb, agent smart** — the CLI is a data pipe, not an intelligent tool. Agents
  (Claude, Codex) provide the intelligence layer on top.
- **Deterministic output** — same input always produces same output. No randomness, no
  non-deterministic formatting.
- **Strong help text** — every command has clear, copy-paste-ready examples.
