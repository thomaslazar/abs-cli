# Extended Help Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hide the response-shape family of help sections from plain `--help`, and surface the complete help (today's output) behind a new global `--help-full` flag, with a one-line discoverability hint on plain `--help`.

**Architecture:** All help sections are registered per-command in `HelpExtensions.cs`. We tag shape sections with an `IsShape` flag, gate them in the renderer behind an `includeShapes` bool, and install a second recursive `--help-full` option whose action renders with `includeShapes: true`. Plain `--help`'s action renders with `includeShapes: false` and appends the hint when shapes were skipped. No command logic or JSON output changes.

**Tech Stack:** C# / .NET 10, System.CommandLine 2.0.7 (`HelpOption`, `HelpAction`, `SynchronousCommandLineAction`, settable `Option.Action` + `Option.Recursive` — all confirmed present in 2.0.7), xUnit.

---

## Baseline & conventions

- **Spec:** `docs/specs/2026-06-02-extended-help-mode-design.md`
- **Primary file:** `src/AbsCli/Commands/HelpExtensions.cs` (the help engine).
- **Shape creators today:** `AddResponseExample<T>()` / `AddResponseExample(envelope, element)` → private `AddResponseExampleSection`; `AddMediaUnionShapes()`; and **10 direct** `AddHelpSection("Response shape…", HelpSectionPosition.Bottom, …)` calls in `CollectionsCommand.cs:249`, `AuthorsCommand.cs:237`, `HelpExtensions.cs:86`, `ItemsCommand.cs:115,269,300,339,492,836`, `UploadCommand.cs:72`.
- **Build/test:** `dotnet build AbsCli.sln`, `dotnet test AbsCli.sln`. Run `dotnet format AbsCli.sln` before each commit (CI enforces `--verify-no-changes`).
- **Style:** no unnecessary blank lines inside method bodies (see CLAUDE.md).
- The live smoke (`docker/smoke-test.sh`) does NOT assert help text — unit tests are the gate here.

## File structure

- Modify: `src/AbsCli/Commands/HelpExtensions.cs` — `Section` record, `AddShapeSection`, route helpers, gate `WriteSections`, hint, `CustomHelpAction`, `UseCustomHelpSections`.
- Modify (call-site reroute): `CollectionsCommand.cs`, `AuthorsCommand.cs`, `ItemsCommand.cs`, `UploadCommand.cs` — 9 direct shape calls → `AddShapeSection`.
- Modify (tests): `HelpExtensionsTests.cs` (split helper + new behavior tests), and flip render helper `--help`→`--help-full` in `HelpOutputTests.cs`, `ItemsCoverCommandTests.cs`, `ItemsToggleEbookStatusCommandTests.cs`, `ItemsChaptersCommandTests.cs`, `AuthorsCommandTests.cs`, `ItemsEncodeM4bCommandTests.cs`, `AuthorsImageCommandTests.cs`, `ItemsEmbedMetadataCommandTests.cs`, `MeCommandTests.cs`, `ItemsGetExpandedCommandTests.cs`.
- Modify: `README.md` — document `--help-full` in the global-flags area.

---

## Task 1: Mark shape sections (pure refactor, no behavior change)

**Files:**
- Modify: `src/AbsCli/Commands/HelpExtensions.cs`
- Modify: `src/AbsCli/Commands/CollectionsCommand.cs`, `AuthorsCommand.cs`, `ItemsCommand.cs`, `UploadCommand.cs`

After this task, shapes are *tagged* but still render in `--help` (no gating yet), so the existing suite stays green.

- [ ] **Step 1: Add `IsShape` to the `Section` record**

In `HelpExtensions.cs`, change:
```csharp
private record Section(string Title, string[] Lines, HelpSectionPosition Position);
```
to:
```csharp
private record Section(string Title, string[] Lines, HelpSectionPosition Position, bool IsShape = false);
```
(The default keeps every existing `new Section(title, lines, position)` call compiling unchanged.)

- [ ] **Step 2: Add the `AddShapeSection` helper**

Add next to `AddHelpSection` in `HelpExtensions.cs`:
```csharp
public static void AddShapeSection(this Command command, string title, params string[] lines)
{
    var sections = CommandSections.GetOrAdd(command, _ => new List<Section>());
    sections.Add(new Section(title, lines, HelpSectionPosition.Bottom, IsShape: true));
}
```

- [ ] **Step 3: Route the internal shape helpers through `AddShapeSection`**

In `HelpExtensions.cs`, change `AddResponseExampleSection`:
```csharp
private static void AddResponseExampleSection(Command command, string json)
    => command.AddShapeSection("Response shape", json.Split('\n'));
```
And in `AddMediaUnionShapes`, change both `command.AddHelpSection(...)` calls to `command.AddShapeSection(...)` (drop the `HelpSectionPosition.Bottom` argument):
```csharp
command.AddShapeSection(
    "Book media shape (when mediaType is \"book\")",
    ResponseExamples.For(typeof(AbsCli.Models.BookMediaMinified)).Split('\n'));
command.AddShapeSection(
    "Podcast media shape (when mediaType is \"podcast\")",
    ResponseExamples.For(typeof(AbsCli.Models.PodcastMedia)).Split('\n'));
```

- [ ] **Step 4: Reroute the 9 direct `AddHelpSection("Response shape…")` call sites**

In `CollectionsCommand.cs:249`, `AuthorsCommand.cs:237`, `ItemsCommand.cs:115,269,300,339,492,836`, and `UploadCommand.cs:72`, change each:
```csharp
command.AddHelpSection("Response shape…", HelpSectionPosition.Bottom, <lines>);
```
to:
```csharp
command.AddShapeSection("Response shape…", <lines>);
```
Preserve each title string verbatim (`"Response shape"`, `"Response shape (--expanded)"`, `"Response shape (with --wait, on success)"`) and the `<lines>` argument unchanged. (Find them with: `grep -rn 'AddHelpSection("Response shape' src/AbsCli/Commands/`. The 10th hit is the `HelpExtensions.cs:86` definition already handled in Step 3.)

- [ ] **Step 5: Format, build, run the existing help tests**

Run:
```bash
dotnet format AbsCli.sln
dotnet build AbsCli.sln --nologo
dotnet test AbsCli.sln --nologo --filter "FullyQualifiedName~Help|FullyQualifiedName~CommandTests"
```
Expected: build succeeds; tests PASS (shapes still render under `--help` because gating isn't added yet).

- [ ] **Step 6: Commit**

```bash
git add src/AbsCli/Commands/
git commit -m "refactor: tag response-shape help sections with IsShape"
```

---

## Task 2: Add `--help-full` option + `includeShapes` plumbing (still no hiding)

**Files:**
- Modify: `src/AbsCli/Commands/HelpExtensions.cs`
- Test: `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`

This task adds the flag and threads `includeShapes` through, but BOTH `--help` and `--help-full` render with shapes (plain `--help` behavior unchanged). The behavior flip happens in Task 3. Suite stays green.

- [ ] **Step 1: Write the failing test for `--help-full`**

Add to `HelpExtensionsTests.cs` a full-help render helper and a test:
```csharp
private static string RenderHelpFull(Command command)
{
    var root = new RootCommand { command };
    root.UseCustomHelpSections();
    var output = new StringWriter();
    var config = new InvocationConfiguration { Output = output };
    root.Parse(new[] { command.Name, "--help-full" }).Invoke(config);
    return output.ToString();
}

[Fact]
public void HelpFull_RendersShapeSection()
{
    var cmd = new Command("demo", "Demo");
    cmd.AddResponseExample<AbsCli.Models.AuthorItem>();
    var output = RenderHelpFull(cmd);
    Assert.Contains("Response shape:", output);
    Assert.Contains("\"numBooks\"", output);
}
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `dotnet test AbsCli.sln --nologo --filter "FullyQualifiedName~HelpFull_RendersShapeSection"`
Expected: FAIL — `--help-full` is an unrecognized token (no such option yet), so help for it does not render the section / parse errors.

- [ ] **Step 3: Parameterize `CustomHelpAction` with `includeShapes`**

In `HelpExtensions.cs`, replace the `CustomHelpAction` class:
```csharp
private sealed class CustomHelpAction : SynchronousCommandLineAction
{
    private readonly HelpAction _inner;
    private readonly bool _includeShapes;
    public CustomHelpAction(HelpAction inner, bool includeShapes)
    {
        _inner = inner;
        _includeShapes = includeShapes;
    }

    public override int Invoke(ParseResult parseResult)
    {
        var command = parseResult.CommandResult.Command;
        var output = parseResult.InvocationConfiguration.Output;
        WriteSections(command, output, HelpSectionPosition.Top, _includeShapes);
        var rc = _inner.Invoke(parseResult);
        WriteSections(command, output, HelpSectionPosition.Bottom, _includeShapes);
        return rc;
    }
}
```

- [ ] **Step 4: Add the `includeShapes` parameter to `WriteSections`**

Change `WriteSections` signature and skip-logic:
```csharp
private static void WriteSections(Command command, TextWriter output, HelpSectionPosition position, bool includeShapes)
{
    if (!CommandSections.TryGetValue(command, out var sections)) return;
    foreach (var section in sections.Where(s => s.Position == position))
    {
        if (section.IsShape && !includeShapes) continue;
        output.WriteLine($"{section.Title}:");
        foreach (var line in section.Lines)
            output.WriteLine($"  {line}");
        output.WriteLine();
    }
}
```

- [ ] **Step 5: Install both actions and the recursive `--help-full` option**

Replace `UseCustomHelpSections`:
```csharp
public static void UseCustomHelpSections(this RootCommand root)
{
    var helpOption = root.Options.OfType<HelpOption>().FirstOrDefault()
        ?? throw new InvalidOperationException(
            "RootCommand has no HelpOption — cannot install CustomHelpAction.");
    if (helpOption.Action is not HelpAction defaultAction)
        throw new InvalidOperationException(
            $"HelpOption.Action is {helpOption.Action?.GetType().Name ?? "null"}, expected HelpAction.");
    helpOption.Action = new CustomHelpAction(defaultAction, includeShapes: true);
    var fullHelp = new Option<bool>("--help-full")
    {
        Description = "Show full help including response-shape blocks.",
        Recursive = true,
        Action = new CustomHelpAction(defaultAction, includeShapes: true),
    };
    root.Options.Add(fullHelp);
}
```
(Note: plain `--help` still uses `includeShapes: true` here — no behavior change yet. Task 3 flips it to `false`.)

- [ ] **Step 6: Run the new test + full suite**

Run:
```bash
dotnet format AbsCli.sln
dotnet test AbsCli.sln --nologo
```
Expected: `HelpFull_RendersShapeSection` PASSES; all pre-existing tests still PASS (plain `--help` unchanged).

- [ ] **Step 7: Commit**

```bash
git add src/AbsCli/Commands/HelpExtensions.cs tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs
git commit -m "feat: add --help-full option and includeShapes plumbing"
```

---

## Task 3: Hide shapes on plain `--help` + hint, migrate affected tests (behavior change)

**Files:**
- Modify: `src/AbsCli/Commands/HelpExtensions.cs`
- Test: `tests/AbsCli.Tests/Commands/HelpExtensionsTests.cs`
- Test (helper flip): `HelpOutputTests.cs`, `ItemsCoverCommandTests.cs`, `ItemsToggleEbookStatusCommandTests.cs`, `ItemsChaptersCommandTests.cs`, `AuthorsCommandTests.cs`, `ItemsEncodeM4bCommandTests.cs`, `AuthorsImageCommandTests.cs`, `ItemsEmbedMetadataCommandTests.cs`, `MeCommandTests.cs`, `ItemsGetExpandedCommandTests.cs`

This is the atomic behavior change: plain `--help` stops showing shapes and prints the hint. That breaks the 11 existing shape-asserting files, so they migrate to `--help-full` in the same commit.

- [ ] **Step 1: Write the new behavior tests (in `HelpExtensionsTests.cs`)**

Add a plain-help helper if not present (the file already has `RenderHelp(Command)` using `--help` — reuse it) and these tests:
```csharp
[Fact]
public void PlainHelp_HidesShapeSection_AndShowsHint()
{
    var cmd = new Command("demo", "Demo");
    cmd.AddResponseExample<AbsCli.Models.AuthorItem>();
    var output = RenderHelp(cmd);
    Assert.DoesNotContain("Response shape:", output);
    Assert.Contains("Run --help-full to see response shape", output);
}

[Fact]
public void HelpFull_ShowsShape_AndOmitsHint()
{
    var cmd = new Command("demo", "Demo");
    cmd.AddResponseExample<AbsCli.Models.AuthorItem>();
    var output = RenderHelpFull(cmd);
    Assert.Contains("Response shape:", output);
    Assert.DoesNotContain("Run --help-full", output);
}

[Fact]
public void PlainHelp_NoShapeSection_OmitsHint()
{
    var cmd = new Command("demo", "Demo");
    cmd.AddHelpSection("Examples", "abs-cli demo");
    var output = RenderHelp(cmd);
    Assert.DoesNotContain("Run --help-full", output);
}
```

- [ ] **Step 2: Run them to confirm failure**

Run: `dotnet test AbsCli.sln --nologo --filter "FullyQualifiedName~PlainHelp|FullyQualifiedName~HelpFull_ShowsShape"`
Expected: `PlainHelp_HidesShapeSection_AndShowsHint` FAILS (shapes still shown, no hint) — the others may pass incidentally.

- [ ] **Step 3: Flip plain `--help` to `includeShapes: false`**

In `UseCustomHelpSections`, change the plain-help action:
```csharp
helpOption.Action = new CustomHelpAction(defaultAction, includeShapes: false);
```
(Leave the `fullHelp` option at `includeShapes: true`.)

- [ ] **Step 4: Emit the hint when shapes were skipped**

Add a hint writer to `HelpExtensions.cs`:
```csharp
private static void WriteShapeHint(Command command, TextWriter output)
{
    if (!CommandSections.TryGetValue(command, out var sections)) return;
    if (!sections.Any(s => s.IsShape)) return;
    output.WriteLine("Run --help-full to see response shape(s).");
    output.WriteLine();
}
```
And call it at the end of `CustomHelpAction.Invoke`, only when shapes are hidden:
```csharp
public override int Invoke(ParseResult parseResult)
{
    var command = parseResult.CommandResult.Command;
    var output = parseResult.InvocationConfiguration.Output;
    WriteSections(command, output, HelpSectionPosition.Top, _includeShapes);
    var rc = _inner.Invoke(parseResult);
    WriteSections(command, output, HelpSectionPosition.Bottom, _includeShapes);
    if (!_includeShapes) WriteShapeHint(command, output);
    return rc;
}
```

- [ ] **Step 5: Migrate the 10 per-command test helpers to `--help-full`**

In each of these files, change the single render-helper line `var args = path.Concat(new[] { "--help" }).ToArray();` to `var args = path.Concat(new[] { "--help-full" }).ToArray();`:
`HelpOutputTests.cs`, `ItemsCoverCommandTests.cs`, `ItemsToggleEbookStatusCommandTests.cs`, `ItemsChaptersCommandTests.cs`, `AuthorsCommandTests.cs`, `ItemsEncodeM4bCommandTests.cs`, `AuthorsImageCommandTests.cs`, `ItemsEmbedMetadataCommandTests.cs`, `MeCommandTests.cs`, `ItemsGetExpandedCommandTests.cs`.

This is safe: `--help-full` is a superset of `--help` (all options/examples/notes still render) plus shapes, minus the hint. The only shape-absence assertion (`HelpOutputTests.cs:123`, raw-json commands) still holds because those commands have no shape section.

- [ ] **Step 6: Migrate the two existing shape tests in `HelpExtensionsTests.cs`**

Switch `AddResponseExample_Generic_RendersResponseShapeSection` and `AddResponseExample_EnvelopeAndElement_SubstitutesResultsArray` from `RenderHelp(cmd)` to `RenderHelpFull(cmd)` (their assertions check shape content, which now lives in full help). Leave the three ordering tests (`TopSection_RendersBeforeOptions`, `BottomSection_RendersAfterOptions`, `ExistingOverload_DefaultsToBottom`) on `RenderHelp` — they use non-shape `Notes`/`Examples` sections that still render in plain `--help`.

- [ ] **Step 7: Format and run the FULL suite**

Run:
```bash
dotnet format AbsCli.sln
dotnet test AbsCli.sln --nologo
```
Expected: ALL tests PASS (260+ baseline plus the new ones). If any `*CommandTests` still fail on a shape assertion, that file's helper wasn't flipped — fix it.

- [ ] **Step 8: Manual smoke of real commands**

Run:
```bash
dotnet run --project src/AbsCli/AbsCli.csproj -- items get --help | grep -c "Response shape" || true   # expect 0
dotnet run --project src/AbsCli/AbsCli.csproj -- items get --help | grep "help-full"                     # expect the hint line
dotnet run --project src/AbsCli/AbsCli.csproj -- items get --help-full | grep -c "Response shape"         # expect >=1
dotnet run --project src/AbsCli/AbsCli.csproj -- login --help | grep -c "help-full"                       # expect 0 (no shapes → no hint)
```

- [ ] **Step 9: Commit**

```bash
git add src/AbsCli/Commands/HelpExtensions.cs tests/AbsCli.Tests/Commands/
git commit -m "feat: hide response shapes from --help, show via --help-full"
```

---

## Task 4: Document `--help-full` in README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Find the global-flags / options documentation**

Run: `grep -n "\-\-debug\|ABS_DEBUG\|Global\|--log-json" README.md`
Locate the section where global flags (`--debug`, `--log-json`) are described.

- [ ] **Step 2: Add a `--help-full` entry**

Add, in the same format as the neighboring global-flag entries:
```
`--help-full` — Show full help including the `Response shape:` blocks. Plain
`--help` omits them (and prints a one-line pointer) to stay scannable.
```
Match the surrounding markdown style (table row vs bullet) exactly. Do NOT add a row to the Commands verb table — `--help-full` is a global flag, not a verb.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: document --help-full global flag"
```

---

## Self-Review (completed)

- **Spec coverage:** mark shapes (Task 1) ✓; `--help-full` recursive option (Task 2) ✓; gating + hint on plain `--help` (Task 3) ✓; scope = shape family only, other sections untouched (gating keys on `IsShape`) ✓; tests for hide/show/no-hint/recursive + regression via migrated `HelpOutputTests` ✓; README (Task 4) ✓.
- **Placeholder scan:** none — every code step shows complete code; the `"Response shape…"` ellipsis in Task 1 Step 4 explicitly means "preserve the exact existing title."
- **Type/name consistency:** `IsShape`, `AddShapeSection`, `includeShapes`, `WriteShapeHint`, `CustomHelpAction(HelpAction, bool)`, and the hint string `"Run --help-full to see response shape(s)."` are used identically across tasks. Recursion test for nested subcommands is covered by the migrated per-command helpers (e.g. `items progress get --help-full` via `MeCommandTests`/`ItemsGetExpandedCommandTests` patterns) plus the `Recursive = true` registration.
