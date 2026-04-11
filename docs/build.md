# Build & Distribution

## Targets

| Target | RID | Binary |
|--------|-----|--------|
| Linux x64 | `linux-x64` | `abs-cli` |
| Linux ARM | `linux-arm64` | `abs-cli` |
| macOS x64 | `osx-x64` | `abs-cli` |
| macOS ARM | `osx-arm64` | `abs-cli` |
| Windows x64 | `win-x64` | `abs-cli.exe` |
| Windows ARM | `win-arm64` | `abs-cli.exe` |

## Build Command

```bash
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishAot=true
```

Single, self-contained binary. No .NET runtime dependency.

## NuGet Dependencies

- `System.CommandLine` — CLI framework
- No Spectre.Console in v1
- No Newtonsoft.Json (hard rule)
- `HttpClient` and `System.Text.Json` are built-in

## GitHub Actions CI

- Build matrix across all 6 targets (parallel)
- Integration tests on Linux only (Docker required)
- Upload binaries as release artifacts
- Tag-based releases
