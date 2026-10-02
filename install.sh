#!/bin/bash
set -euo pipefail

# Fetches a release binary from GitHub Releases and verifies it against the
# release's SHA256SUMS before installing. Both come from the same release, so
# this catches a corrupt or truncated download, not a compromised release.

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
ASSET="abs-cli-${RID}"

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

BASE_URL="https://github.com/${REPO}/releases/download/${VERSION}"

# Download into a temp dir first, so a failed or corrupt download never
# replaces a working install.
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT
curl -fsSL "${BASE_URL}/${ASSET}" -o "${TMP_DIR}/${ASSET}"

# macOS ships shasum rather than sha256sum.
sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print $1}'
  else
    echo "Error: sha256sum or shasum is required to verify the download" >&2
    exit 1
  fi
}

# Verify
if ! curl -fsSL "${BASE_URL}/SHA256SUMS" -o "${TMP_DIR}/SHA256SUMS"; then
  echo "Error: Could not download SHA256SUMS for ${VERSION}." >&2
  echo "Releases before checksums were added have none; download from the release page instead." >&2
  exit 1
fi
# Lines are "<hash>  <file>"; a "*" before the file name marks binary mode.
EXPECTED="$(awk -v name="$ASSET" \
  '{ f = $2; sub(/^\*/, "", f); if (f == name) { print tolower($1); exit } }' \
  "${TMP_DIR}/SHA256SUMS")"
if [ -z "$EXPECTED" ]; then
  echo "Error: SHA256SUMS has no entry for ${ASSET}" >&2
  exit 1
fi
ACTUAL="$(sha256_of "${TMP_DIR}/${ASSET}" | tr 'A-F' 'a-f')"
if [ "$EXPECTED" != "$ACTUAL" ]; then
  echo "Error: Checksum mismatch for ${ASSET}" >&2
  echo "  expected: ${EXPECTED}" >&2
  echo "  actual:   ${ACTUAL}" >&2
  exit 1
fi
echo "Checksum verified."

# Install
mkdir -p "$INSTALL_DIR"
chmod +x "${TMP_DIR}/${ASSET}"
mv -f "${TMP_DIR}/${ASSET}" "${INSTALL_DIR}/abs-cli"

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
