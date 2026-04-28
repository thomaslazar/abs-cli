# Dev Container

## Overview

Development happens inside a VS Code dev container. The container provides a
consistent environment with .NET 10, Native AOT tooling, and all Claude Code
integrations pre-configured.

## Base Image

`mcr.microsoft.com/devcontainers/dotnet:10.0` — Microsoft's official .NET 10
devcontainer image (Ubuntu 24.04 LTS).

## What's Installed

### Build Tools

| Tool | Purpose |
|------|---------|
| .NET 10 SDK | C# compilation, project tooling |
| clang | Native AOT linker (required for AOT publish on Linux) |
| zlib1g-dev | Native AOT compression dependency |
| Python 3 + pip | Required by peon-ping |

### Claude Code Integrations

The dev container includes several tools that enhance Claude Code sessions.
All are installed automatically during container creation via `post-create.sh`.

#### Claude Code CLI

Installed via the native installer (`curl -fsSL https://claude.ai/install.sh`).
This is the recommended install method — standalone binary with auto-updates,
no Node.js dependency.

#### Superpowers — Structured Development Workflow

**Repo:** [obra/superpowers](https://github.com/obra/superpowers)

**What it does:** 14 composable skills that guide development through structured
phases: brainstorming, planning, test-driven implementation, debugging, and
code review.

**Why it's here:** The TDD workflow aligns with the project's testing philosophy
(test the compiled binary against a real Audiobookshelf instance). The planning
and brainstorming skills are valuable for a greenfield project. The systematic
debugging skill claims ~95% first-time fix rate.

**Setup:**
- Plugin installed from official marketplace in post-create.sh
- Skills activate automatically based on context
- ~5-7KB session overhead (lightweight)

**Key skills:**
- `brainstorming` — transforms vague ideas into detailed specs before any code
- `writing-plans` — creates implementation plans with exact file paths and tasks
- `test-driven-development` — enforces RED-GREEN-REFACTOR cycles
- `systematic-debugging` — four-phase methodology for root cause analysis
- `verification-before-completion` — evidence-based completion gating
- `subagent-driven-development` — dispatches fresh subagents per task

**Selective usage:** Skills are composable. You can use just the planning skill
without TDD, or just debugging without the full workflow.

#### peon-ping — Audio Notifications

Installed in the Dockerfile with `REMOTE_CONTAINERS=true` so it delegates
audio playback to the host relay. Sound packs are bind-mounted from the host
at `~/.openpeon/packs`.

### Docker Support

The container includes Docker-outside-of-Docker (DooD) via the
`ghcr.io/devcontainers/features/docker-outside-of-docker:1` feature. This lets
you run `docker build` and `docker run` against the host daemon (e.g. OrbStack)
from inside the container — used for integration testing later.

## Session Continuity

Claude Code sessions are shared between host and container via:

1. **Bind mount:** `~/.claude/projects` is mounted from the host into the container
2. **Symlink:** post-create.sh links the container path key to the host path key

This means you can work on the same session from both environments — useful when
the container can't build and you need to fix the Dockerfile from the host.

## Mounts

| Host Path | Container Path | Purpose |
|-----------|---------------|---------|
| `~/.claude/projects` | `/home/vscode/.claude/projects` | Session continuity |
| `~/.openpeon/packs` | `/home/vscode/.claude/hooks/peon-ping/packs` | Sound packs |

## Future: Everything Claude Code

**Repo:** [affaan-m/everything-claude-code](https://github.com/affaan-m/everything-claude-code)

A comprehensive framework with 38 agents, 156 skills, and language-specific rules.
Not currently integrated because:

- 30-50k tokens of context overhead (15-25% of context window)
- Its C# rules assume ASP.NET Core patterns (EF Core, FluentValidation) that don't
  apply to a bare CLI project
- Risk of conflicts increases with many hooks and plugins active

**When to reconsider:** Once the project has real code and would benefit from:
- The dedicated C# reviewer agent
- Security rules (parameterized queries, input validation)
- The selective install: `./install.sh --modules framework-language` to load
  only C# rules without the full 38-agent system
