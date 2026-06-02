# Extended help mode — design

**Roadmap item:** v0.6.0 "Extended help mode" (`docs/roadmap.md`).
**Date:** 2026-06-02
**Scope:** Pure CLI/help change in `src/AbsCli/Commands/`. Plain `--help` hides the
response-shape family of help sections; a new global `--help-full` flag shows the
complete help (byte-identical to today's `--help` output). No API or command-behavior
change.

## Background

Every command renders one or more `Response shape:` blocks at the bottom of its
`--help`. Commands like `items get` carry up to four (`Response shape`,
`Response shape (--expanded)`, plus the `Book media shape` / `Podcast media shape`
union pair). As more commands ship, help output drifts toward unreadable. The fix:
hide the shape family by default, surface it on demand via `--help-full`.

Audience note: this CLI targets AI-agent consumption, so plain `--help` should stay
terse and scannable; agents that need the response shape opt in with `--help-full`.

### Help-rendering mechanics (current state)

`HelpExtensions.cs` registers per-command help sections in a static
`ConcurrentDictionary<Command, List<Section>>`. Each `Section` has a `Title`,
`Lines`, and a `Position` (`Top` / `Bottom`). `UseCustomHelpSections` swaps the root
`HelpOption`'s action for a `CustomHelpAction` that writes Top sections before the
default layout and Bottom sections after; the `HelpOption` is recursive, so it fires
for every subcommand.

Shape sections are created via several paths:
- `AddResponseExample<T>()` and `AddResponseExample(envelope, element)` →
  `AddResponseExampleSection` → `"Response shape"`.
- `AddMediaUnionShapes()` → `"Book media shape (when mediaType is \"book\")"` +
  `"Podcast media shape (when mediaType is \"podcast\")"`.
- ~8 **direct** `AddHelpSection("Response shape…", Bottom, …)` calls with custom
  titles (`(--expanded)`, `(with --wait, on success)`, plain) in `ItemsCommand`,
  `AuthorsCommand`, `CollectionsCommand`, `UploadCommand`.

Because titles vary, the design marks shapes explicitly in code rather than matching
on title strings.

## Decisions (locked during brainstorming)

- **Mechanism:** a `--help-full` flag (not env var, not subcommand).
- **Additive:** `--help-full` = normal help **plus** the shape blocks (the full,
  current output). Plain `--help` omits shapes and prints a one-line hint.
- **Gated scope:** only the response-shape family. `Permission required`, `Notes`,
  `Examples`, and `Filter` groups remain visible in plain `--help`.

## Changes

### 1. Mark shape sections explicitly

- Add a `bool IsShape` field to the internal `Section` record (default `false`).
- Add `AddShapeSection(this Command, string title, params string[] lines)` that
  registers a section with `IsShape = true`, `Position = Bottom`.
- Route every shape creator through it:
  - `AddResponseExampleSection`, the `AddResponseExample(envelope, element)` overload,
    and `AddMediaUnionShapes` call `AddShapeSection` internally — no change to their
    ~45 caller sites.
  - The ~8 direct `AddHelpSection("Response shape…", Bottom, …)` calls switch to
    `AddShapeSection("Response shape…", …)` (mechanical 1:1, custom titles preserved).

### 2. Gate rendering + discoverability hint

`WriteSections` (and the action that calls it) takes an `includeShapes` bool:

- **Plain `--help`** (`includeShapes: false`): render Top → default layout → Bottom
  sections **with `IsShape == false`**. If the command had ≥1 shape section that was
  skipped, print one terse line at the very end:
  ```
  Run --help-full to see response shape(s).
  ```
  Commands with no shape sections print no hint.
- **`--help-full`** (`includeShapes: true`): render all sections in registration
  order — reproducing today's exact output. No hint.

### 3. The `--help-full` option

In `UseCustomHelpSections`:
- Extract the default `HelpAction` once (as today).
- Set `--help`'s action to `CustomHelpAction(defaultAction, includeShapes: false)`.
- Register a new **recursive** `--help-full` option on the root whose action is
  `CustomHelpAction(defaultAction, includeShapes: true)`.

Both actions reuse the same inner `HelpAction` for the standard layout, so
`--help-full` differs from `--help` only by the gating bool and the absence of the
hint. Recursive registration makes it fire on every subcommand, exactly like `--help`.

Flag description: `Show full help including response-shape blocks.`

**Implementation note for the plan:** confirm the exact System.CommandLine 2.0.7 API
for adding a second recursive help-style option — either a `HelpOption` subclass/alias
or an `Option<bool>` with `Recursive = true` and an assigned `Action`. Pick whichever
the library supports cleanly; the behavior above is the contract.

## Out of scope

- Gating any non-shape section (Examples, Notes, etc.).
- Env-var or subcommand surfacing mechanisms.
- Any change to command logic, request/response handling, or the JSON the commands emit.

## Testing

Extend the existing `HelpExtensions` test file (do not add a new one):

- **Regression anchor:** capture `items get`'s `--help-full` output and assert it
  equals the command's complete section set (proves `--help-full` reproduces today's
  `--help`, no drift).
- **Plain `--help` on a shape-bearing command** (`items get`): the four shape blocks
  are absent; the hint line is present; `Examples` / `Notes` / `Permission required`
  are still present.
- **Shape-less command** (e.g. `login`): plain `--help` prints **no** hint line.
- **Recursive depth:** `items progress get --help-full` renders its shape block
  (recursion reaches nested subcommands).
- **Hint appears once**, at the end, only when shapes were skipped.

CI runs `dotnet test`; the live smoke (`docker/smoke-test.sh`) does not assert help
text, so unit tests are the gate here. Run `dotnet format AbsCli.sln` after edits.

## Docs

- README: add `--help-full` to the global-flags note. It is a global flag, not a verb,
  so the Commands table is unchanged (per the CLAUDE.md flag-table rule, a global flag
  warrants a mention but no table row).
