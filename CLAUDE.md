# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Restore, build, test
dotnet restore CSStat.sln
dotnet build CSStat.sln
dotnet test CSStat.sln

# Run a single test class
dotnet test src/CS2Stats.Tests --filter "FullyQualifiedName~StatsCaptureServiceTests"

# Test with coverage (coverlet.collector already in Tests.csproj)
dotnet test CSStat.sln --collect:"XPlat Code Coverage"

# Build Release WITHOUT triggering auto-packaging (see Gotchas)
dotnet build src/CS2Stats.Plugin/CS2Stats.Plugin.csproj -c Release /p:PackageOnBuild=false

# Generate release packages locally (version must match <Version> in CS2Stats.Plugin.csproj)
./scripts/package-release.ps1 -Configuration Release -Version 1.0.1 -PackageId CS2-STATPLAY -RuntimeIdentifier linux-x64
```

## Configuration

The config template is `config/cs2stats.example.json`. On a CS2 server the live config lives at:
`addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json`

`MySqlConfigGuard` detects when the example placeholder values (`127.0.0.1` / `cs2_stats` / `cs2stats` / `change-me`) are still active and forces `NoOpStatsWriter` — no silent `Access denied` failures.

## Architecture

```
CS2 Game Events → StatsCaptureService → StatsBatch (buffer) → MySqlStatsWriter → MySQL raw tables
                                                                                → AggregationService → stored procedures → aggregate tables
```

**Three projects:**
- `CS2Stats.Contracts` (net8.0) — immutable record types only. `StatsContracts.cs` defines all DTOs (`PlayerDeathEvent`, `PlayerActionEvent`, `PresenceSnapshot`, etc.) and `StatsBatch` (the flush payload).
- `CS2Stats.Plugin` (net8.0) — runtime plugin. Targets CounterStrikeSharp API ≥ v80.
- `CS2Stats.Tests` (net10.0) — xUnit + Moq. References Plugin via `InternalsVisibleTo`.

**Plugin lifecycle (`CS2StatsPlugin.cs`):**
- `OnConfigParsed` wires flush and presence intervals from config.
- `Load` checks `MySqlConfigGuard.IsPackagedPlaceholder` — if placeholder config is still active, the writer is permanently replaced by `NoOpStatsWriter` and MySQL is never touched.
- Two timers run in parallel: one drains `StatsBatch` via `FlushAsync` (default 15 s), one calls `CapturePresenceSnapshot` (default 10 s).
- `FlushAsync` is semaphore-guarded (skip if previous flush is still running). On MySQL error 1045, the writer silently degrades to `NoOpStatsWriter` for the rest of the runtime.
- `Unload` fires a final flush.

**Capture layer (`StatsCaptureService.cs`):**
- All event handlers are thin: they call `StatsCaptureService.On*` methods and return `HookResult.Continue`.
- All writes to `_buffer` are lock-protected (`_gate`). `DrainBatch` atomically swaps and clears.
- `TryGetMapNameSafely` catches exceptions from `Server.MapName` — needed because the CounterStrikeSharp DLL is not available in unit test context.

**Persistence layer (`MySqlStatsWriter.cs` / `DatabaseInitializationService.cs`):**
- Writes are transactional per `StatsBatch`.
- `DatabaseInitializationService` handles `CREATE DATABASE` permission errors gracefully (falls back to verifying the DB is accessible via `SELECT 1`).
- SQL scripts with `DELIMITER` are split by the custom `SplitSqlScript` parser — do not use naive `;` splitting.
- Schema lives inline in `DatabaseInitializationService` (not read from `sql/` files at runtime); the `sql/` files are reference artifacts for manual setup.

**Aggregation (`AggregationService.cs`):**
- Called fire-and-forget (`_ = _aggregationService.RefreshAllStatsAsync(...)`) after each successful flush — if the plugin unloads mid-flush the aggregation run is silently abandoned.
- Delegates to three stored procedures: `sp_refresh_player_lifetime_stats`, `sp_refresh_player_session_stats`, `sp_refresh_player_map_stats`.
- `RefreshAllStatsAsync` also back-fills any closed sessions and map/player combinations that don't yet have an aggregate row (`RefreshPendingSessionStatsAsync` / `RefreshPendingMapStatsAsync`).

## Gotchas

- **Release build auto-packages**: `Directory.Build.targets` runs `package-release.ps1` automatically after any Release build of `CS2Stats.Plugin`, unless you pass `/p:PackageOnBuild=false`. The CI workflow does this explicitly. Omitting the flag on a local release build writes to `artifacts/`.
- **Schema lives in C#, not in `sql/`**: `DatabaseInitializationService` inlines the full schema and stored procedures. The `sql/` files are reference docs for manual setup — not used at runtime.
- **`SteamID64` may be null mid-event**: `TryGetSteamId64` returns `null` for bots, disconnected clients, or partially-initialized controllers — all handlers must guard against this.

## Key Design Rules (from `.github/instructions`)

- Keep event handlers thin; all logic lives in dedicated services.
- Prefer batching over synchronous DB writes in hot event paths.
- Separate raw counters (stored in event tables) from derived metrics (computed by stored procedures into aggregate tables).
- Store timestamps in UTC. Use `SteamID64` as the player identity anchor.
- Guard every event-driven path against duplicate processing on reconnects or plugin reloads.
- Prefer immutable `record` types for all persistence payloads.
- Verify CounterStrikeSharp event availability in official docs before implementing new stat paths.

## Journal Files

Update after meaningful changes:
- `docs/journals/worklog.md` — what was implemented
- `docs/journals/architecture.md` — service boundary or schema changes
- `docs/journals/decisions.md` — accepted/rejected technical decisions
- `docs/journals/sources.md` — new docs or repos consulted
