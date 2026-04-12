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
  Uses System.CommandLine. `HelpExtensions` adds custom help sections (Examples,
  Filter groups, Sort fields) via `HelpBuilder.CustomizeLayout`. `CommandHelper`
  provides shared client/config resolution for all commands.
- **Service** — Business logic and orchestration. Returns typed DTOs (not raw strings)
  to validate the API contract at the service boundary.
- **API Client** — HTTP communication with Audiobookshelf. Single `AbsApiClient` class
  wrapping `HttpClient`. Handles auth headers, token refresh, base URL. Generic
  `GetAsync<T>` overloads deserialize responses through `JsonTypeInfo<T>`.
- **DTOs** — Explicit data transfer objects. All types that cross the serialization
  boundary are registered in `JsonContext.cs` (`[JsonSerializable]` attributes)
  for AOT source-generated serialization. This is critical — missing a type causes
  runtime crashes under AOT.

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
        SelfTestCommand.cs         # AOT integrity verification
        CommandHelper.cs           # Shared client/config resolution
        HelpExtensions.cs          # Custom help sections (Examples, Filter groups, etc.)
      Services/
        ItemsService.cs
        LibrariesService.cs
        SeriesService.cs
        AuthorsService.cs
        SearchService.cs
      Api/
        AbsApiClient.cs
        ApiEndpoints.cs
        FilterEncoder.cs           # Base64 filter encoding for ABS API
        TokenHelper.cs             # JWT expiry decoding
      Models/
        JsonContext.cs              # Source-generated serialization (AOT-critical)
        LibraryItem.cs
        Library.cs
        Series.cs
        Author.cs
        SearchResult.cs
        UpdateMediaResponse.cs
        BatchGetResponse.cs
        ...
      Configuration/
        AppConfig.cs
        ConfigManager.cs
      Output/
        ConsoleOutput.cs
  tests/
    AbsCli.Tests/
```

## Hard Rules

- No Newtonsoft.Json — incompatible with AOT trimming
- No `dynamic` — breaks AOT compilation
- No reflection — breaks AOT trimming
- Explicit DTOs — every API response deserializes through a typed model (contract validation)
- Every serialized type must be registered in `JsonContext.cs` — untested types crash under AOT
- JSON is the only output format in v1
- JSON output is re-serialized from typed DTOs (validates contract, may drop unknown fields)

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
