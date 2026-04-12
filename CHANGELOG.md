# Changelog

All notable changes to abs-cli are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/).

## v0.1.1 — 2026-04-12

### Highlights
- Fixed login failing with 403 on servers behind reverse proxies (e.g. Cosmos)
- Added macOS Gatekeeper bypass instructions to README
- Improved release workflow reliability (clean Docker state, better CI output handling)

### Fixes
- fix: add User-Agent header to HTTP client
- fix: use host.docker.internal in Docker test scripts
- fix: improve release skill reliability

### Docs
- docs: add macOS Gatekeeper instructions and ask-before-commit rule

## v0.1.0 — 2026-04-12

### Highlights
- First public release of abs-cli — a command-line interface for managing Audiobookshelf servers
- Full audiobook metadata management: list, search, view, update, and batch-edit items, series, and authors
- Native AOT binaries for 6 platforms (linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64, win-arm64) — no .NET runtime required
- Token-based authentication with automatic refresh
- Built-in self-test command for offline AOT integrity verification

### Features
- feat: add login command with access+refresh token storage
- feat: add config get/set commands
- feat: add configuration layer with file, env, and flag precedence
- feat: add libraries list and get commands
- feat: add items commands (list, get, search, update, batch-update, batch-get)
- feat: add series list and get commands
- feat: add authors list and get commands
- feat: add global search command
- feat: add self-test command for offline AOT integrity verification
- feat: add API client with auth, token refresh, and endpoint constants
- feat: add DTO models derived from ABS source code
- feat: add ABS filter encoder with base64 encoding
- feat: add ABS server version compatibility check
- feat: add JWT token expiry helper for proactive refresh
- feat: add console output helper for JSON stdout and stderr errors
- feat: add examples to all command help text
- feat: add filter groups and sort field reference to help text
- feat: add GitHub Actions CI with build matrix and integration tests
- feat: add win-arm64 build target via windows-11-arm runner
- feat: CI auto-attaches binaries to GitHub Releases

### Fixes
- fix: proper AOT support via source-gen, restructure testing
- fix: resolve AOT reflection errors in ConfigManager and AbsApiClient
- fix: use macOS cross-compilation for osx-x64 AOT binary
- fix: use X-Return-Tokens header and accessToken instead of legacy user.token
- fix: run self-test on all platforms including osx-x64 via Rosetta 2
