# Build & Distribution

## Targets

| Target | RID | Runner | Notes |
|--------|-----|--------|-------|
| Linux x64 | `linux-x64` | `ubuntu-latest` | Primary. Full smoke test in CI |
| Linux ARM64 | `linux-arm64` | `ubuntu-24.04-arm` | Native ARM runner |
| macOS Apple Silicon | `osx-arm64` | `macos-latest` | Native build |
| macOS Intel | `osx-x64` | `macos-latest` | Cross-compiled via Xcode toolchain |
| Windows x64 | `win-x64` | `windows-latest` | Produces `abs-cli.exe` |

Native AOT cannot cross-compile across operating systems, but macOS supports
arm64-to-x64 cross-compilation via the Xcode toolchain. `macos-13` (Intel)
runners were retired in December 2025.

## Build Command

```bash
dotnet publish src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true
```

Single, self-contained binary. No .NET runtime dependency. ~9 MB.

## AOT Requirements

Native AOT disables reflection-based JSON serialization. All types that cross
the JSON boundary must be registered in `src/AbsCli/Models/JsonContext.cs`:

```csharp
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(LoginResponse))]
// ... etc
public partial class AppJsonContext : JsonSerializerContext;
```

Use `AppJsonContext.Default.TypeName` instead of generic `JsonSerializer.Serialize<T>`.
The `self-test` command verifies all registered types work correctly.

## NuGet Dependencies

- `System.CommandLine` — CLI framework
- No Spectre.Console in v1
- No Newtonsoft.Json (hard rule)
- `HttpClient` and `System.Text.Json` are built-in

## GitHub Actions CI

- **unit-test** — xUnit tests
- **smoke-test** — AOT binary against live ABS Docker instance (linux-x64)
- **build** — 5-platform AOT matrix, each runs `self-test` (except osx-x64 cross-compile)
- Artifacts uploaded per platform
- Triggered on pull requests and releases
