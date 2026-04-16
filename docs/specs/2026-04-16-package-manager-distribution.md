# Package Manager Distribution & Install Scripts

**Date:** 2026-04-16
**Status:** Draft
**Goal:** Make abs-cli installable and upgradeable through Homebrew, deb packages, and standalone install scripts, all automated in the release pipeline.

## Motivation

Currently abs-cli is distributed only as raw binaries on GitHub Releases. Users must manually download, place, and update the binary. Package managers and install scripts provide OS-native upgrade paths and discoverability.

## Scope

| Channel | Platforms | Auto-upgrade | Priority |
|---------|-----------|-------------|----------|
| Homebrew tap | macOS (arm64, x64), Linux (x64, arm64) | `brew upgrade` | Primary for macOS |
| Deb package | Linux (amd64, arm64) | Manual `dpkg -i` | Convenience for Debian/Ubuntu |
| `install.sh` | macOS + Linux (all supported archs) | Re-run script | Universal fallback |
| `install.ps1` | Windows (x64, arm64) | Re-run script | Primary for Windows |

**Out of scope:** Winget, Chocolatey, Scoop, APT repository, NuGet, Docker.

## 1. Homebrew Tap

### New Repository

`thomaslazar/homebrew-abs-cli` — contains a single formula.

### Formula: `abs-cli.rb`

- Downloads the pre-built binary from GitHub Releases (no compilation)
- Platform/arch selection via Homebrew's `on_macos`/`on_linux` + `Hardware::CPU` blocks
- SHA256 verification per binary
- Installs binary to Homebrew's bin prefix
- `test` block runs `abs-cli --version`

### User Experience

```bash
brew tap thomaslazar/abs-cli
brew install abs-cli

# subsequent updates
brew upgrade abs-cli
```

### Release Automation

After binaries are uploaded to the GitHub Release, the pipeline:

1. Computes SHA256 checksums for the 4 Homebrew-relevant binaries (osx-arm64, osx-x64, linux-x64, linux-arm64)
2. Renders the formula with updated version + checksums
3. Pushes a commit to `thomaslazar/homebrew-abs-cli`

**Auth:** Requires a PAT or GitHub App token with push access to the tap repo, stored as a repository secret in `thomaslazar/abs-cli`.

## 2. Deb Packages

### Build

Two `.deb` packages built per release: `abs-cli_{version}_amd64.deb` and `abs-cli_{version}_arm64.deb`.

Built with `dpkg-deb --build` in the pipeline. No Debian toolchain needed — the AOT binary is already compiled.

### Package Structure

```
abs-cli_{version}_{arch}/
├── DEBIAN/
│   └── control
└── usr/
    └── local/
        └── bin/
            └── abs-cli
```

### Control File

```
Package: abs-cli
Version: {version}
Section: utils
Priority: optional
Architecture: {amd64|arm64}
Maintainer: Thomas Lazar <thomas@razal.de>
Description: Command-line interface for Audiobookshelf
 A CLI tool for managing Audiobookshelf servers, libraries, and media.
Homepage: https://github.com/thomaslazar/abs-cli
```

No dependencies — self-contained AOT binary.

### Distribution

Attached to GitHub Releases alongside raw binaries. Users install with:

```bash
# download
curl -LO https://github.com/thomaslazar/abs-cli/releases/latest/download/abs-cli_{version}_amd64.deb

# install
sudo dpkg -i abs-cli_{version}_amd64.deb
```

## 3. Install Scripts

Both scripts live in the repository root. They always fetch the latest release, so they don't need updating per-release.

### `install.sh` (macOS + Linux)

**Location:** `/install.sh` (repo root)

**Behavior:**
1. Detect OS via `uname -s` → `Linux` or `Darwin`
2. Detect arch via `uname -m` → `x86_64`, `aarch64`, `arm64`
3. Map to RID: `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`
4. Query GitHub API for latest release tag (or use `ABS_CLI_VERSION` override)
5. Download binary from `https://github.com/thomaslazar/abs-cli/releases/download/{tag}/abs-cli-{rid}`
6. Install to `~/.local/bin/abs-cli` (or `ABS_CLI_INSTALL_DIR` override)
7. Warn if install directory is not on PATH
8. Verify with `abs-cli --version`

**Environment variable overrides:**

| Variable | Default | Purpose |
|----------|---------|---------|
| `ABS_CLI_VERSION` | latest release | Pin to specific version, e.g. `v0.2.2` |
| `ABS_CLI_INSTALL_DIR` | `~/.local/bin` | Custom install directory |

**Usage:**
```bash
# default (latest, ~/.local/bin)
curl -fsSL https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.sh | bash

# pinned version
curl -fsSL https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.sh | ABS_CLI_VERSION=v0.2.2 bash

# custom directory
curl -fsSL https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.sh | ABS_CLI_INSTALL_DIR=/usr/local/bin bash
```

**Idempotent:** Works as both install and update. Overwrites existing binary.

**Error handling:**
- Fail if OS/arch unsupported
- Fail if download fails (HTTP error, network)
- Warn if install directory not on PATH

### `install.ps1` (Windows)

**Location:** `/install.ps1` (repo root)

**Behavior:**
1. Detect arch via `$env:PROCESSOR_ARCHITECTURE` → `AMD64` or `ARM64`
2. Map to RID: `win-x64`, `win-arm64`
3. Query GitHub API for latest release tag (or use `ABS_CLI_VERSION` override)
4. Download `.exe` from GitHub Releases
5. Install to `$env:LOCALAPPDATA\abs-cli\abs-cli.exe` (or `ABS_CLI_INSTALL_DIR` override)
6. Add install directory to user-level PATH if not already present
7. Verify with `abs-cli --version`

**Environment variable overrides:**

| Variable | Default | Purpose |
|----------|---------|---------|
| `ABS_CLI_VERSION` | latest release | Pin to specific version, e.g. `v0.2.2` |
| `ABS_CLI_INSTALL_DIR` | `$env:LOCALAPPDATA\abs-cli` | Custom install directory |

**Usage:**
```powershell
# default (latest, LOCALAPPDATA)
irm https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.ps1 | iex

# pinned version
$env:ABS_CLI_VERSION = "v0.2.2"; irm https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.ps1 | iex

# custom directory
$env:ABS_CLI_INSTALL_DIR = "C:\tools\abs-cli"; irm https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.ps1 | iex
```

**Idempotent:** Works as both install and update. Overwrites existing binary. PATH modification is additive and skipped if already present.

## 4. Pipeline Changes

### Modified: `.github/workflows/build.yml`

The `build` job's release-event steps are extended. After existing binary upload:

#### Step: Build Deb Packages

- Runs on the `linux-x64` and `linux-arm64` matrix entries only
- Creates deb package directory structure
- Generates `DEBIAN/control` from template with current version
- Runs `dpkg-deb --build`
- Uploads `.deb` to GitHub Release via `gh release upload`

#### New Job: Update Homebrew Tap

Separate job with `needs: build` — runs after all matrix entries complete and all binaries are uploaded.

- Downloads all 4 Homebrew-relevant binaries from the release (osx-arm64, osx-x64, linux-x64, linux-arm64)
- Computes SHA256 checksums
- Renders formula from template
- Clones `thomaslazar/homebrew-abs-cli`, commits updated formula, pushes

### No Changes for Install Scripts

`install.sh` and `install.ps1` always resolve `latest` at runtime. No pipeline step needed.

## 5. Documentation

### README Updates

New "Installation" section with methods ordered by recommendation:

1. **Homebrew** (macOS/Linux) — `brew tap` + `brew install`
2. **Install script** (macOS/Linux) — curl-pipe one-liner
3. **Install script** (Windows) — irm-pipe one-liner
4. **Deb package** (Debian/Ubuntu) — download + dpkg
5. **Manual download** (all platforms) — GitHub Releases link
6. **Build from source** — existing instructions

## 6. New Secrets / Configuration

| Secret | Repo | Purpose |
|--------|------|---------|
| `HOMEBREW_TAP_TOKEN` | `thomaslazar/abs-cli` | PAT with push access to `thomaslazar/homebrew-abs-cli` |

## 7. New Repository

| Repo | Contents |
|------|----------|
| `thomaslazar/homebrew-abs-cli` | Homebrew formula (`Formula/abs-cli.rb`) + README |
