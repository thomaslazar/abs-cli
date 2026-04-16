# Package Manager Distribution & Install Scripts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Homebrew tap, deb packages, and standalone install scripts to abs-cli, all automated in the release pipeline.

**Architecture:** Install scripts live in repo root, always fetch latest release at runtime. Deb packages built in existing CI matrix on Linux entries. Homebrew formula template in `.github/homebrew/`, rendered and pushed to tap repo by a new CI job. README updated with all install methods.

**Tech Stack:** Bash, PowerShell, GitHub Actions YAML, Ruby (Homebrew formula)

**Spec:** `docs/specs/2026-04-16-package-manager-distribution.md`

---

## File Map

**New files:**
- `install.sh` — macOS + Linux install/update script (repo root)
- `install.ps1` — Windows install/update script (repo root)
- `.github/homebrew/abs-cli.rb.template` — Homebrew formula template with `@@PLACEHOLDER@@` tokens

**Modified files:**
- `.github/workflows/build.yml` — deb package step + Homebrew tap update job
- `README.md` — expanded installation section

**External (manual setup by user):**
- `thomaslazar/homebrew-abs-cli` GitHub repo with `Formula/` directory and a README

---

## Task 1: Create install.sh

**Files:**
- Create: `install.sh`

- [ ] **Step 1: Write the install script**

```bash
#!/bin/bash
set -euo pipefail

REPO="thomaslazar/abs-cli"
INSTALL_DIR="${ABS_CLI_INSTALL_DIR:-$HOME/.local/bin}"
VERSION="${ABS_CLI_VERSION:-}"

# Detect OS
OS="$(uname -s)"
case "$OS" in
  Linux)  OS_RID="linux" ;;
  Darwin) OS_RID="osx" ;;
  *)      echo "Error: Unsupported OS: $OS" >&2; exit 1 ;;
esac

# Detect architecture
ARCH="$(uname -m)"
case "$ARCH" in
  x86_64)  ARCH_RID="x64" ;;
  aarch64) ARCH_RID="arm64" ;;
  arm64)   ARCH_RID="arm64" ;;
  *)       echo "Error: Unsupported architecture: $ARCH" >&2; exit 1 ;;
esac

RID="${OS_RID}-${ARCH_RID}"

# Resolve version
if [ -z "$VERSION" ]; then
  VERSION="$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
    | grep '"tag_name"' | sed -E 's/.*"([^"]+)".*/\1/')"
  if [ -z "$VERSION" ]; then
    echo "Error: Could not determine latest version" >&2
    exit 1
  fi
fi

echo "Installing abs-cli ${VERSION} (${RID})..."

# Download
DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${VERSION}/abs-cli-${RID}"
mkdir -p "$INSTALL_DIR"
curl -fsSL "$DOWNLOAD_URL" -o "${INSTALL_DIR}/abs-cli"
chmod +x "${INSTALL_DIR}/abs-cli"

# PATH check
case ":${PATH}:" in
  *":${INSTALL_DIR}:"*) ;;
  *)
    echo ""
    echo "Warning: ${INSTALL_DIR} is not on your PATH." >&2
    echo "Add it to your shell profile:" >&2
    echo "  export PATH=\"${INSTALL_DIR}:\$PATH\"" >&2
    ;;
esac

# Verify
"${INSTALL_DIR}/abs-cli" --version
echo "abs-cli installed to ${INSTALL_DIR}/abs-cli"
```

- [ ] **Step 2: Verify syntax**

Run: `bash -n install.sh`
Expected: no output (clean parse)

- [ ] **Step 3: Verify it's executable**

Run: `chmod +x install.sh && file install.sh`
Expected: output contains "Bourne-Again shell script" or "shell script"

- [ ] **Step 4: Commit**

```bash
git add install.sh
git commit -m "feat: add install.sh for macOS and Linux"
```

---

## Task 2: Create install.ps1

**Files:**
- Create: `install.ps1`

- [ ] **Step 1: Write the install script**

```powershell
$ErrorActionPreference = "Stop"

$Repo = "thomaslazar/abs-cli"
$InstallDir = if ($env:ABS_CLI_INSTALL_DIR) { $env:ABS_CLI_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA "abs-cli" }
$Version = $env:ABS_CLI_VERSION

# Detect architecture
$Arch = $env:PROCESSOR_ARCHITECTURE
switch ($Arch) {
    "AMD64" { $Rid = "win-x64" }
    "ARM64" { $Rid = "win-arm64" }
    default { Write-Error "Unsupported architecture: $Arch"; exit 1 }
}

# Resolve version
if (-not $Version) {
    $Release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
    $Version = $Release.tag_name
}

Write-Host "Installing abs-cli $Version ($Rid)..."

# Download
$DownloadUrl = "https://github.com/$Repo/releases/download/$Version/abs-cli-${Rid}.exe"
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$BinaryPath = Join-Path $InstallDir "abs-cli.exe"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $BinaryPath -UseBasicParsing

# Add to user PATH if not already present
$UserPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($UserPath -notlike "*$InstallDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$UserPath;$InstallDir", "User")
    $env:Path = "$env:Path;$InstallDir"
    Write-Host "Added $InstallDir to user PATH."
}

# Verify
& $BinaryPath --version
Write-Host "abs-cli installed to $BinaryPath"
```

- [ ] **Step 2: Verify syntax**

Run: `pwsh -Command "Get-Content install.ps1 | Out-Null" 2>&1 || echo "pwsh not available, skip syntax check"`
Expected: no errors (or skip if pwsh not installed in dev container)

- [ ] **Step 3: Commit**

```bash
git add install.ps1
git commit -m "feat: add install.ps1 for Windows"
```

---

## Task 3: Create Homebrew formula template

**Files:**
- Create: `.github/homebrew/abs-cli.rb.template`

- [ ] **Step 1: Create the template directory**

Run: `mkdir -p .github/homebrew`

- [ ] **Step 2: Write the formula template**

```ruby
class AbsCli < Formula
  desc "Command-line interface for Audiobookshelf"
  homepage "https://github.com/thomaslazar/abs-cli"
  version "@@VERSION@@"
  license "MIT"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/thomaslazar/abs-cli/releases/download/v@@VERSION@@/abs-cli-osx-arm64"
      sha256 "@@SHA256_OSX_ARM64@@"
    else
      url "https://github.com/thomaslazar/abs-cli/releases/download/v@@VERSION@@/abs-cli-osx-x64"
      sha256 "@@SHA256_OSX_X64@@"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/thomaslazar/abs-cli/releases/download/v@@VERSION@@/abs-cli-linux-arm64"
      sha256 "@@SHA256_LINUX_ARM64@@"
    else
      url "https://github.com/thomaslazar/abs-cli/releases/download/v@@VERSION@@/abs-cli-linux-x64"
      sha256 "@@SHA256_LINUX_X64@@"
    end
  end

  def install
    binary_name = stable.url.split("/").last
    bin.install binary_name => "abs-cli"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/abs-cli --version")
  end
end
```

- [ ] **Step 3: Verify placeholders are correct**

Run: `grep -c '@@' .github/homebrew/abs-cli.rb.template`
Expected: `6` (VERSION appears twice via sed global replace, plus 4 SHA256 placeholders — but grep counts lines, so 6 unique lines with `@@`)

Actually run: `grep -o '@@[A-Z0-9_]*@@' .github/homebrew/abs-cli.rb.template | sort -u`
Expected output:
```
@@SHA256_LINUX_ARM64@@
@@SHA256_LINUX_X64@@
@@SHA256_OSX_ARM64@@
@@SHA256_OSX_X64@@
@@VERSION@@
```

- [ ] **Step 4: Commit**

```bash
git add .github/homebrew/abs-cli.rb.template
git commit -m "feat: add Homebrew formula template"
```

---

## Task 4: Add deb package build step to build.yml

**Files:**
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Add deb package step after the binary upload step**

Add this step at the end of the `build` job's `steps:` list (after the "Attach binary to GitHub Release" step):

```yaml
      - name: Build and upload deb package
        if: github.event_name == 'release' && startsWith(matrix.rid, 'linux-')
        shell: bash
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          VERSION="${{ github.event.release.tag_name }}"
          VERSION_NUM="${VERSION#v}"
          if [ "${{ matrix.rid }}" = "linux-x64" ]; then
            ARCH="amd64"
          else
            ARCH="arm64"
          fi
          PKG_DIR="abs-cli_${VERSION_NUM}_${ARCH}"
          mkdir -p "${PKG_DIR}/DEBIAN"
          mkdir -p "${PKG_DIR}/usr/local/bin"
          cat > "${PKG_DIR}/DEBIAN/control" << EOF
          Package: abs-cli
          Version: ${VERSION_NUM}
          Section: utils
          Priority: optional
          Architecture: ${ARCH}
          Maintainer: Thomas Lazar <thomas@razal.de>
          Description: Command-line interface for Audiobookshelf
           A CLI tool for managing Audiobookshelf servers, libraries, and media.
          Homepage: https://github.com/thomaslazar/abs-cli
          EOF
          BIN="src/AbsCli/bin/Release/net8.0/${{ matrix.rid }}/publish/abs-cli"
          cp "$BIN" "${PKG_DIR}/usr/local/bin/abs-cli"
          chmod 755 "${PKG_DIR}/usr/local/bin/abs-cli"
          dpkg-deb --build --root-owner-group "$PKG_DIR"
          gh release upload "${{ github.event.release.tag_name }}" "${PKG_DIR}.deb"
```

**Important:** The `cat > ... << EOF` block must NOT have leading spaces on the content lines — the `DEBIAN/control` format requires fields to start at column 0. Use a heredoc that strips indentation, or write the file without indentation. The code block above shows the content indented for readability in the plan, but in the actual YAML the heredoc content lines must be unindented or use `<<-` with tabs.

- [ ] **Step 2: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/build.yml'))" && echo "valid"`
Expected: `valid`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/build.yml
git commit -m "ci: add deb package build step for Linux releases"
```

---

## Task 5: Add Homebrew tap update job to build.yml

**Files:**
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Add the update-homebrew job**

Add this new job after the `build` job in `build.yml`:

```yaml
  update-homebrew:
    if: github.event_name == 'release'
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - name: Download release binaries
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          TAG="${{ github.event.release.tag_name }}"
          for RID in osx-arm64 osx-x64 linux-x64 linux-arm64; do
            gh release download "$TAG" -R thomaslazar/abs-cli -p "abs-cli-${RID}" -D .
          done
      - name: Update Homebrew tap
        env:
          HOMEBREW_TAP_TOKEN: ${{ secrets.HOMEBREW_TAP_TOKEN }}
        run: |
          VERSION="${{ github.event.release.tag_name }}"
          VERSION_NUM="${VERSION#v}"
          SHA_OSX_ARM64=$(sha256sum abs-cli-osx-arm64 | cut -d' ' -f1)
          SHA_OSX_X64=$(sha256sum abs-cli-osx-x64 | cut -d' ' -f1)
          SHA_LINUX_X64=$(sha256sum abs-cli-linux-x64 | cut -d' ' -f1)
          SHA_LINUX_ARM64=$(sha256sum abs-cli-linux-arm64 | cut -d' ' -f1)
          git clone https://x-access-token:${HOMEBREW_TAP_TOKEN}@github.com/thomaslazar/homebrew-abs-cli.git tap
          mkdir -p tap/Formula
          sed -e "s/@@VERSION@@/${VERSION_NUM}/g" \
              -e "s/@@SHA256_OSX_ARM64@@/${SHA_OSX_ARM64}/g" \
              -e "s/@@SHA256_OSX_X64@@/${SHA_OSX_X64}/g" \
              -e "s/@@SHA256_LINUX_X64@@/${SHA_LINUX_X64}/g" \
              -e "s/@@SHA256_LINUX_ARM64@@/${SHA_LINUX_ARM64}/g" \
              .github/homebrew/abs-cli.rb.template > tap/Formula/abs-cli.rb
          cd tap
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add Formula/abs-cli.rb
          git diff --cached --quiet && echo "No formula changes" && exit 0
          git commit -m "Update abs-cli to ${VERSION}"
          git push
```

- [ ] **Step 2: Validate YAML syntax**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/build.yml'))" && echo "valid"`
Expected: `valid`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/build.yml
git commit -m "ci: add Homebrew tap update job on release"
```

---

## Task 6: Update README installation section

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Replace the Installation section**

Replace the current `## Installation` section (from `## Installation` up to but not including `## Quick start`) with:

```markdown
## Installation

### Homebrew (macOS / Linux)

```bash
brew tap thomaslazar/abs-cli
brew install abs-cli
```

### Install script (macOS / Linux)

```bash
curl -fsSL https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.sh | bash
```

Installs to `~/.local/bin/abs-cli`. Override with environment variables:

```bash
# specific version
curl -fsSL https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.sh | ABS_CLI_VERSION=v0.2.2 bash

# custom directory
curl -fsSL https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.sh | ABS_CLI_INSTALL_DIR=/usr/local/bin bash
```

### Install script (Windows)

```powershell
irm https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.ps1 | iex
```

Installs to `%LOCALAPPDATA%\abs-cli\`. Override with environment variables:

```powershell
# specific version
$env:ABS_CLI_VERSION = "v0.2.2"; irm https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.ps1 | iex

# custom directory
$env:ABS_CLI_INSTALL_DIR = "C:\tools\abs-cli"; irm https://raw.githubusercontent.com/thomaslazar/abs-cli/main/install.ps1 | iex
```

### Deb package (Debian / Ubuntu)

Download from the [latest release](https://github.com/thomaslazar/abs-cli/releases/latest):

```bash
sudo dpkg -i abs-cli_0.2.2_amd64.deb
```

### Download a release

Grab the binary for your platform from the [latest release](https://github.com/thomaslazar/abs-cli/releases/latest):

| Platform | Binary |
|----------|--------|
| Linux x64 | `abs-cli-linux-x64` |
| Linux ARM64 | `abs-cli-linux-arm64` |
| macOS Apple Silicon | `abs-cli-osx-arm64` |
| macOS Intel | `abs-cli-osx-x64` |
| Windows x64 | `abs-cli-win-x64.exe` |
| Windows ARM64 | `abs-cli-win-arm64.exe` |

```bash
chmod +x abs-cli-linux-x64
mv abs-cli-linux-x64 ~/.local/bin/abs-cli
```

**macOS users:** The binaries are not signed or notarized. macOS Gatekeeper
will block them on first run. Remove the quarantine attribute to allow
execution:

```bash
sudo xattr -d com.apple.quarantine abs-cli-osx-arm64
```

### Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet publish src/AbsCli/AbsCli.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true
# Binary at: src/AbsCli/bin/Release/net8.0/linux-x64/publish/abs-cli
```
```

- [ ] **Step 2: Verify README renders**

Run: `head -70 README.md`
Expected: new installation section visible with all 6 methods

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add Homebrew, install scripts, and deb to installation section"
```

---

## Pre-release Checklist (manual, not automated)

Before the first release that uses this pipeline:

- [ ] Create `thomaslazar/homebrew-abs-cli` repo on GitHub with an empty `Formula/` directory and a README
- [ ] Create a PAT with `repo` scope (or fine-grained with push access to `homebrew-abs-cli`)
- [ ] Add the PAT as `HOMEBREW_TAP_TOKEN` secret in `thomaslazar/abs-cli` repo settings
- [ ] Run a release to verify deb packages and Homebrew formula are published correctly
