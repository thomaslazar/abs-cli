# Help: response-shape examples and derived-resource notices

**Date:** 2026-04-17
**Status:** Approved for planning

## Goals

Two additions to `abs-cli` help output, targeted at AI agents that drive the
CLI and today guess at field names or assume missing commands mean
"unsupported on the server":

1. **Response-shape examples.** Every command with a non-trivial JSON response
   renders a generated example payload inside its `--help` output. The example
   lists every field the model can produce, with arrays truncated to a single
   element and synthetic placeholder values. The payload shape is derived from
   the C# model classes so it cannot drift.
2. **Derived-resource notices.** `abs-cli authors --help` and
   `abs-cli series --help` render a short `Notes:` block explaining that these
   records are lifecycle-driven by item metadata: the way to add or remove one
   is by updating the underlying library items, not by calling a dedicated
   command.

## Non-goals

- Hand-curated realistic values. Placeholders like `"<string>"` / `0` / `null`
  are sufficient for agents to learn field names and types.
- Live-server fixture capture. No dependence on a running ABS instance.
- Covering commands whose response is a trivial body such as
  `{ "success": true }`.
- Extending the derived-resource notice to genres, tags, narrators, or
  publishers. These have no dedicated command groups in abs-cli (they surface
  only as filter values in `items list --filter`), so there is nowhere to
  render a per-resource notice today.
- Introducing a `--full` / expanded-example help flag. The truncated form
  already shows every field; "full" would only repeat array elements.

## Feature 1 — generated response-shape examples

### Rendering

A new help section titled `Response shape:` is rendered via the existing
`HelpExtensions` pipeline, placed after the `Examples:` section. Content is a
JSON code block indented two spaces to match other sections.

Example (`abs-cli authors list --help`), abbreviated:

```
Response shape:
  {
    "authors": [
      {
        "id": "<string>",
        "asin": null,
        "name": "<string>",
        "description": null,
        "imagePath": null,
        "libraryId": "<string>",
        "addedAt": 0,
        "updatedAt": 0,
        "numBooks": 0,
        "lastFirst": null
      }
    ]
  }
```

### Source of the example

Examples are generated from the C# model class that each command deserializes
(for example `AuthorListResponse`, `LibraryItemMinified`,
`PaginatedResponse<LibraryItemMinified>`). A walker inspects public
properties, respects `[JsonPropertyName]`, and emits synthetic values by type:

| C# type                       | Sample value                                      |
| ----------------------------- | ------------------------------------------------- |
| `string`                      | `"<string>"`                                      |
| `string?`                     | `null`                                            |
| `int` / `long` / `double`     | `0`                                               |
| `bool`                        | `false`                                           |
| `List<T>` / `T[]`             | `[<one T rendered recursively>]`                  |
| `Dictionary<string, T>`       | `{ "<key>": <T rendered> }`                       |
| nested class / struct         | `{ ... }` recursively                             |
| `object` / `JsonElement`      | `{}` with comment `// shape varies`               |

Note: abs-cli models do not use `DateTime` / `DateTimeOffset`. ABS returns
all timestamps as epoch milliseconds (`addedAt`, `updatedAt`, `birthtimeMs`,
`mtimeMs`, etc.), typed as `long` in the C# models, so they render as `0`
under the table above. If a `DateTime` field is ever added, the walker must
be extended to emit the correct ISO-8601 string `System.Text.Json` would
produce — simply emitting `0` would be wrong. Flag this during review if it
comes up.

`PaginatedResponse<T>` is rendered with its real envelope fields (`results`,
`total`, `limit`, `page`, and whatever else the model declares) and one
sample `T` inside `results`. Union responses (e.g. `LibraryItem` vs
`LibraryItemMinified`) use whichever concrete model the command actually
deserializes — `ItemsService` already knows this.

### Cycle guard

The walker tracks visited types per traversal. If it re-encounters a type it
is already inside, it emits the literal string `"<recursive>"` instead of
recursing. Unlikely to trigger with the current models, but cheap insurance.

### AOT constraint and implementation choice

`AbsCli.csproj` sets `PublishAot=true`. Runtime reflection over arbitrary
model types would emit AOT warnings and risk trimming the very members we
need. The chosen approach:

- A small console tool lives at `tools/GenerateResponseExamples/` as its own
  csproj, **not** part of the solution's AOT publish configuration. The tool
  references `AbsCli.Models` and uses plain reflection (it runs on a full
  .NET host, not AOT).
- The tool emits `src/AbsCli/Commands/ResponseExamples.g.cs`, a static class
  exposing `ResponseExamples.For(Type)` returning a `const string` per
  registered type. The generated file is checked into the repository.
- `AbsCli.csproj` gains a `BeforeBuild` MSBuild target that runs the tool
  with proper `Inputs`/`Outputs` (every model `.cs` is an input; the generated
  file is the output) so incremental builds do not regenerate unnecessarily.
- A drift test (see *Testing*) runs the generator in-process during
  `dotnet test` and fails if the generated file would differ. CI runs the
  same test.

This keeps the shipped AOT binary free of reflection, while eliminating
drift: renaming a model property regenerates the sample automatically.

### Registering examples on commands

A new helper in `HelpExtensions.cs`:

```csharp
public static void AddResponseExample<T>(this Command command)
{
    var json = ResponseExamples.For(typeof(T));
    command.AddHelpSection("Response shape", json.Split('\n'));
}
```

Each affected command gets one call alongside its existing `AddExamples(...)`.
Commands and their example types are itemised during implementation; at
minimum: `authors list`, `authors get`, `series list`, `series get`,
`items list`, `items get`, `items search`, `items update`, `items batch-get`,
`items batch-update`, `items scan`, `libraries` subcommands that return JSON,
`backup` subcommands that return JSON, `tasks list`, `metadata` subcommands,
`login`. Commands with trivial responses are skipped.

## Feature 2 — derived-resource notices

### Scope

Applies to `authors` and `series` top-level command groups only. These are
the only derived resources with dedicated command groups in abs-cli.

### Rendering

A `Notes:` help section rendered at the top of the resource command's help —
i.e. before `Options:` and subcommand list — when the user runs
`abs-cli authors --help` or `abs-cli series --help`. The section is NOT
repeated on individual subcommand help pages; a user exploring the top-level
group is the intended audience.

The existing `HelpExtensions.AddHelpSection` implementation appends sections
after `Options`. To support top placement, `AddHelpSection` gains an optional
`position` parameter:

```csharp
public enum HelpSectionPosition { Top, Bottom }

public static void AddHelpSection(
    this Command command,
    string title,
    HelpSectionPosition position,
    params string[] lines);
```

Existing overloads default to `Bottom` — no behaviour change for current
call sites. `WriteSections` is updated to emit top-positioned sections
before the default layout and bottom ones after.

### Content — authors

```
Notes:
  Authors are derived from book metadata. An author record exists while at
  least one library item references it. When the last referencing item is
  removed or re-tagged, the scanner deletes the author on its next run
  (unless a custom image is set). To remove an author, update the books
  that reference it.
```

### Content — series

```
Notes:
  Series are derived from book metadata. A series exists while at least one
  library item references it. When the last referencing item is removed or
  re-tagged, the scanner deletes the series on its next run. To remove a
  series, update the books that reference it.
```

### Source references

Verified against ABS server source at `temp/audiobookshelf/` (v2.33.1):

- `server/scanner/BookScanner.js:916-927` — orphan authors destroyed when
  `bookAuthors` count is zero AND `imagePath` is empty.
- `server/scanner/BookScanner.js:950-963` — orphan series destroyed when
  `bookSeries` count is zero.
- `server/routers/ApiRouter.js:224` — `/series/:id` has `PATCH` only, no
  `DELETE` route.
- `server/routers/ApiRouter.js:213-214` — `/authors/:id` has `PATCH` and
  `DELETE`, but the scanner recreates authors from item metadata on subsequent
  runs so the effective lifecycle remains item-driven.

## Testing

### Unit tests (`tests/AbsCli.Tests/`)

- `ResponseExamplesGeneratorTests` — exercises the walker directly against
  small fixture types covering every branch: primitives, nullable primitives,
  `List<T>`, `Dictionary<string, T>`, nested class, `object` / `JsonElement`,
  recursive self-reference.
- `ResponseExamplesDriftTest` — runs the generator tool in-process, compares
  output to the checked-in `ResponseExamples.g.cs`. On mismatch, fails with
  `"Regenerate: dotnet run --project tools/GenerateResponseExamples"`.
- `ResponseExamplesJsonValidTest` — round-trips every generated sample
  through `System.Text.Json` to guarantee syntactic validity.

### Help-output integration tests

- `HelpOutputTests` — invokes the System.CommandLine parser with `--help`
  against a data-driven list of commands and asserts:
  - `Response shape:` section present for every command declared to emit one.
  - `Notes:` section present on `authors` and `series` root commands, absent
    elsewhere.
  - Sections contain expected anchor strings (e.g. `numBooks` in the
    `authors list` sample, `scanner deletes the series` in the series notes).

### Build wiring

- The `BeforeBuild` target runs the generator on a cold build; subsequent
  builds are no-ops thanks to `Inputs`/`Outputs`. Generator failure fails the
  build.
- CI runs `dotnet test`, which includes the drift test.

### Manual verification after implementation

- `abs-cli authors --help` shows the `Notes:` block near the top.
- `abs-cli series --help` shows the `Notes:` block near the top.
- `abs-cli items get --help` shows a `Response shape:` block containing
  `media.metadata.title`, `media.numTracks`, etc.
- Feed `abs-cli items get --help` output into an agent, ask it to craft a
  jq query against real `items get` output, and confirm field paths line up
  with actual responses.

## Open questions

None at design time. Edge cases (e.g. whether `LibraryItem` vs
`LibraryItemMinified` is the right type for a given command) will be decided
per-command during implementation and captured in the plan.
