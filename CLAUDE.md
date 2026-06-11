# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FOOTDRAFT (in-game name "38-0-20") is a head-to-head football-manager game on the Metaplay SDK:
spin-draft eleven legends into a formation, then take on other managers' XIs in a 20-team double
round-robin "38-0" season league — one matchday simulated per day, transfers between matchdays,
and a full live-service meta (wallet economy, quests, season pass, ranked, LiveOps events).
The client is a Blazor WebAssembly web app.

Metaplay is a backend-as-a-service platform for games, providing infrastructure for player data,
game config, and LiveOps features. All game logic lives in the `Game.Logic` namespace.

> ⚠️ Demo project — the real-player dataset is included for demonstration purposes only.

## Build & Run Commands

```bash
# Build the server
metaplay build server

# Run the server locally
metaplay dev server

# Run the server in watch mode (auto-restart on changes)
metaplay dev server --watch

# Build bot client (for load testing)
metaplay build botclient

# Run bot client locally
metaplay dev botclient

# Run WebClient (Blazor WebAssembly) — the -p:MetaplayWebAssembly=true flag is REQUIRED
dotnet run --project WebClient/WebClient.csproj -p:MetaplayWebAssembly=true

# Build WebClient (always do this after making changes)
dotnet build WebClient/WebClient.csproj -p:MetaplayWebAssembly=true

# Regenerate the pre-built WASM serializer (only when [MetaSerializable] types in SharedCode change)
dotnet run --project tools/SerializerGen -- WebClient/Serializer
```

**Important:** Always build the WebClient after making changes to verify they compile correctly. The
WebClient is a **Blazor WebAssembly** app: the `-p:MetaplayWebAssembly=true` global property is
mandatory on every build/run/publish (it propagates the WASM transport + serializer settings across
the whole project graph). See `WebClient/README.md` for details. When you change `[MetaSerializable]`
game types, regenerate the serializer with the SerializerGen command above.

## Testing

```bash
# Run all project shared code tests (NUnit)
dotnet test Backend/SharedCode.Tests/SharedCode.Tests.csproj

# Run a specific shared code unit test
dotnet test Backend/SharedCode.Tests/SharedCode.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run E2E tests (Playwright, requires server and WebClient running)
dotnet test WebClient.Tests/WebClient.Tests.csproj
```

## Architecture

### Project Structure

- **SharedCode/** - Game logic shared between client and server (C#, namespace `Game.Logic`)
  - `Player/PlayerModel.cs` - Player state and game logic
  - `Player/PlayerActions.cs` - Player actions that modify state (register in `ActionCodes`)
  - `Player/PlayerRewards.cs` - Reward types (extend `PlayerReward`)
  - `GameConfigs/` - Game configuration classes
  - `GlobalOptions.cs` - Core Metaplay options (project ID, logic versions)

- **Backend/Server/** - Server-only code
  - `ServerMain.cs` - Server entry point
  - `Player/PlayerActor.cs` - Server-side player entity actor
  - `Config/Options.*.yaml` - Runtime configuration per environment

- **Backend/SharedCode.Tests/** - Unit tests for shared code (NUnit)

- **WebClientBase/** - Generic Blazor framework library that WebClient derives from. All game-agnostic code (shared UI components, connection management, base services) belongs here.

- **WebClient/** - Game-specific Blazor web client. Derives from WebClientBase. All game-specific code (pages, game UI, typed services for PlayerModel) belongs here.
  - `Services/MetaplayClientService.cs` - Typed client service for PlayerModel

- **WebClient.Tests/** - Playwright E2E tests for WebClient

- **MetaplaySDK/** - The Metaplay SDK (do not modify)



### Key Patterns

**Player Actions**: Game state changes are implemented as `PlayerAction` subclasses with `Execute()` methods. Each action needs a unique action code in `ActionCodes`. Actions are executed against `PlayerModel` on both client and server.

**MetaSerialization**: State classes use `[MetaSerializable]` and `[MetaMember(N)]` attributes for serialization. The member ID ensures stable serialization across versions.

**Schema Migrations**: `PlayerModel` uses `[SupportedSchemaVersions(min, max)]` with migration methods like `[MigrationFromVersion(N)]` for evolving the data schema.

**Game Config**: `SharedGameConfig` holds game configuration data. Use `[GameConfigEntry("Name", isCodeOnly: true)]` for code-defined configs (no spreadsheets).

**Rewards**: Implement `PlayerReward` subclasses with `Consume()` method. Register with `[MetaSerializableDerived(N)]`.

## Coding Standards

- Always use explicit typing. Avoid `var`.
- When iterating over dictionaries, use tuple syntax: `foreach ((keyType key, valueType value) in dictionary)`

### Tool Preferences

- Prefer **Read** over Bash commands like `cat`, `head`, or `tail` for reading file contents.
- Prefer **Glob** over Bash commands like `find` or `ls` for finding files by pattern.
- Prefer **Grep** over Bash commands like `grep` or `rg` for searching file contents.
- Reserve Bash for actual system commands and terminal operations that require shell execution.

## Configuration

Runtime options are in `Backend/Server/Config/`:
- `Options.base.yaml` - Base options for all environments
- `Options.local.yaml` - Local development overrides
- `Options.dev.yaml`, `Options.staging.yaml`, `Options.production.yaml` - Environment-specific
