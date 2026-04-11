# Architecture

## Overview

A C#/Native AOT CLI wrapping the Audiobookshelf API. Designed as a dumb data pipe for
AI agents (Claude, Codex) to drive. JSON-only output. The CLI reads and writes data;
agents provide the intelligence layer (metadata validation, consistency enforcement,
rule-based corrections).

**Primary use case:** Metadata quality pipeline — find items with missing or inconsistent
metadata, let an agent analyze and produce corrections, apply corrections via batch update.

**Scope (v1):** Core read/write commands for items, series, authors, libraries, and search.
Enough to support the full agent metadata workflow end-to-end.

## Layers

Four layers, no frameworks, no magic:

```
CLI  →  Service  →  API Client  →  DTOs
```

- **CLI** — Command parsing, argument validation, help text, output to stdout/stderr.
  Uses System.CommandLine.
- **Service** — Business logic and orchestration. Coordinates API calls.
- **API Client** — HTTP communication with Audiobookshelf. Single `AbsApiClient` class
  wrapping `HttpClient`. Handles auth headers, base URL, filter encoding.
- **DTOs** — Explicit data transfer objects. Serialized with System.Text.Json source
  generators (AOT-compatible, no reflection).

## Project Structure

```
abs-cli/
  src/
    AbsCli/
      Program.cs
      Commands/
        ItemsCommand.cs
        LibrariesCommand.cs
        SeriesCommand.cs
        AuthorsCommand.cs
        SearchCommand.cs
        ConfigCommand.cs
        LoginCommand.cs
      Services/
        ItemsService.cs
        LibrariesService.cs
        SeriesService.cs
        AuthorsService.cs
        SearchService.cs
      Api/
        AbsApiClient.cs
        ApiEndpoints.cs
      Models/
        LibraryItem.cs
        Library.cs
        Series.cs
        Author.cs
        SearchResult.cs
        ...
      Configuration/
        AppConfig.cs
        ConfigManager.cs
      Output/
        JsonOutput.cs
  tests/
    AbsCli.Tests/
```

## Hard Rules

- No Newtonsoft.Json — incompatible with AOT trimming
- No `dynamic` — breaks AOT compilation
- No reflection — breaks AOT trimming
- Explicit DTOs — every API response has a typed model
- JSON is the only output format in v1
- JSON output matches ABS API response structure exactly — no transformation

## Stack

| Concern | Library | Why |
|---------|---------|-----|
| CLI framework | System.CommandLine | .NET standard, good AOT support |
| HTTP | HttpClient | Built-in, no dependencies |
| JSON | System.Text.Json (source generators) | AOT-compatible, no reflection |
| Output formatting | Deferred to roadmap | Spectre.Console for `--table` later |

## Design Principles

- **CLI dumb, agent smart** — the CLI is a data pipe. Metadata rules, validation,
  and intelligence live in the agent layer (Claude with a CLAUDE.md defining the ruleset).
- **Mirror the API** — expose what ABS provides, don't invent abstractions on top.
  Filtering uses the API's native system. JSON output matches API responses exactly.
- **Deterministic** — same input always produces same output.
- **Strong help text** — every command has copy-paste-ready examples. The help text
  serves as documentation for both humans and agents.
- **Fail loudly** — clear error messages that tell you exactly what's wrong and how to fix it.
