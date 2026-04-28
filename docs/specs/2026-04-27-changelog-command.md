# `abs-cli changelog` — design

Status: approved 2026-04-27. Targets v0.3.0.

## Summary

Add a top-level `abs-cli changelog` command that prints release-note content
from the bundled `CHANGELOG.md`. Default output is the latest version's entry;
`--all` prints the full file. Output is raw markdown — readable by humans and
parseable by AI agents using the file's `##` / `###` heading structure.

Source of truth is the repo-root `CHANGELOG.md`, embedded into the assembly at
build time so the command works offline and ships in a single artifact.

## Motivation

Listed on the roadmap. Agents and humans currently have no in-CLI way to ask
"what changed in this version?" without leaving the terminal to read GitHub or
the bundled file. Returning the authored markdown verbatim avoids re-encoding
loss and keeps the command honest about its source.

## Command surface

```
abs-cli changelog            # latest version's entry
abs-cli changelog --all      # entire CHANGELOG.md
```

- Top-level command (sibling of `search`, `upload`, `tasks`, etc.).
- One flag: `--all` (boolean). No value.
- No login required. No network. Works offline.
- Exit 0 on success. Non-zero only on internal misconfiguration (see below).

## Output format

Raw markdown, written to stdout via `Console.Out`. No JSON envelope, no
ANSI rendering, no trailing prompt. The file's own structure is the contract:

- `## <version> — <date>` introduces a release entry.
- `### Highlights` / `### Features` / `### Fixes` / `### Docs` / `### Chores` /
  `### Breaking changes` are the per-section sub-headings already used in the
  file.

The CLI does not parse subsections, reorder content, or add commentary.

## Latest-entry extraction

Algorithm for the default (no `--all`) path:

1. Read the embedded `CHANGELOG.md` as UTF-8 text.
2. Scan lines from top until the first line that starts with `## ` (two
   hashes + space). This is the latest version heading.
3. Emit that line and every subsequent line.
4. Stop (exclusive) at the next line that starts with `## ` or at EOF.
5. Trim trailing blank lines from the captured block.
6. Write the result to stdout, terminated with a single newline.

The `## ` heading line itself is included — it carries the version and date,
which is the first thing an agent or human will want.

`--all` simply writes the file verbatim.

## Bundling

`CHANGELOG.md` is embedded as an assembly manifest resource so it ships
inside the AOT-published single-file binary.

In `src/AbsCli/AbsCli.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="..\..\CHANGELOG.md" LogicalName="CHANGELOG.md" />
</ItemGroup>
```

The path is repo-relative so a single file remains the source of truth — the
root `CHANGELOG.md` that contributors edit is the file the command serves.

Read at runtime via:

```csharp
var stream = typeof(ChangelogCommand).Assembly
    .GetManifestResourceStream("CHANGELOG.md");
```

## Components

### `ChangelogCommand.cs` (new)

Location: `src/AbsCli/Commands/ChangelogCommand.cs`.

Responsibilities:
- Define the `changelog` System.CommandLine `Command`.
- Define the `--all` boolean option.
- Set the handler: load resource → call `ChangelogReader.ReadAll` or
  `ChangelogReader.ReadLatest` → write to stdout.
- Wire help text (one-line description, examples). Follows the compact style
  in `AuthorsCommand.cs` (the reference cited in `CLAUDE.md`).

Registered alongside the other top-level commands wherever
`Program.cs` / `RootCommand` composition currently lives.

### `ChangelogReader` (new, static helper)

Location: same file or `src/AbsCli/Services/ChangelogReader.cs` — pick whichever
matches existing conventions for similarly small, command-local helpers.

Two methods:

```csharp
public static string ReadAll();
public static string ReadLatest();
```

`ReadAll` reads the embedded resource and returns its contents.
`ReadLatest` reads the embedded resource, applies the extraction algorithm
above, and returns the captured block.

Both throw `InvalidOperationException` on failure modes below — never return
empty/null.

## Error handling

Three failure modes, all internal misconfigurations rather than user errors:

| Condition | Behavior |
|-----------|----------|
| `GetManifestResourceStream("CHANGELOG.md")` returns null | Throw `InvalidOperationException("CHANGELOG.md resource not embedded")` |
| `ReadLatest` finds no line starting with `## ` | Throw `InvalidOperationException("CHANGELOG.md has no version entries")` |
| Embedded file is empty | Same as previous |

These propagate through the standard CLI error path (non-zero exit, stderr
message). They cannot occur on a correctly built binary; the build/test
pipeline is responsible for catching them, not end users.

No user-input validation is needed — the only flag is a boolean and there is
no network call.

## Testing

### Unit tests — `ChangelogReaderTests`

Fixture: a small in-memory markdown string containing two `## ` entries and a
preamble. Cover:

- `ReadLatest` returns content starting with the topmost `## ` line.
- `ReadLatest` stops before the second `## ` line.
- `ReadLatest` includes `### ` sub-headings within the latest entry.
- `ReadLatest` strips trailing blank lines from the captured block.
- `ReadLatest` throws `InvalidOperationException` when input has no `## `.
- `ReadAll` returns the input verbatim.

(For these the helper needs a seam — either an internal overload that takes a
string, or `ReadLatestFromString(string)` exposed via `InternalsVisibleTo`.
Pick whichever fits existing patterns; the public `ReadLatest()` reads the
embedded resource and delegates.)

### Integration test

Invoke the `changelog` command end-to-end through the same test host the other
commands use. Assert:

- stdout starts with `## ` (i.e. a version heading).
- stdout contains the topmost version string from the actual repo
  `CHANGELOG.md` (read the file directly in the test for the expected value).
- Exit code is 0.
- `--all` returns content whose length is greater than the default output's
  length.

### Self-test smoke

Add a `Check` block to `SelfTestCommand.cs` that asserts:

- `GetManifestResourceStream("CHANGELOG.md")` is non-null.
- `ChangelogReader.ReadLatest()` returns a non-empty string starting with
  `## `.

This catches a build that silently drops the embedded resource.

## Release-process integration

`docs/release-process.md` (or wherever the version-bump checklist lives)
should already have a "update CHANGELOG.md" step. No new step is needed —
the embedded resource is rebuilt every time `dotnet publish` runs, so as long
as `CHANGELOG.md` is updated before the release build, the shipped binary
matches.

## Out of scope (deferred)

These were considered and explicitly rejected for v0.3.0. Add only when a real
workflow demands them.

- `--version <ver>` to fetch a specific historical entry.
- `--versions <n>` to fetch the last N entries.
- `--json` / structured output.
- Pretty rendering via Spectre.Console.
- Online fetch / GitHub release lookup.

## Open questions

- **Login bypass plumbing.** `changelog` does not require auth or network, so
  it should not trip whatever pre-flight login check the other commands run.
  The mechanism used by `self-test` and `--help` to skip that check needs to
  be applied here too. Confirm the exact registration path during
  implementation; no design impact expected.
