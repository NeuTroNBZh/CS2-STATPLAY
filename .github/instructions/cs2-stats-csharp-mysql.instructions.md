---
name: CS2 Stats CSharp MySQL Standards
description: "Use when designing or implementing the CounterStrikeSharp CS2 stats plugin, especially for C# services, event handling, configuration, SQL schema design, MySQL persistence, repositories, and migration-like schema updates. Covers strict coding and data rules for the repo."
applyTo:
  - "**/*.cs"
  - "**/*.sql"
  - "**/*.json"
---

# CS2 Stats C# And MySQL Standards

## Research First

- Verify CounterStrikeSharp capabilities in official documentation before implementing any new stat collection path.
- Distinguish clearly between direct game data, derived metrics, and assumptions.
- If a stat is not confirmed, mark it as pending instead of silently implementing an inferred behavior.

## C# Design Rules

- Keep the plugin modular: split event capture, aggregation, persistence, configuration, and mapping into separate services.
- Prefer small, explicit interfaces for services that may evolve independently.
- Keep CounterStrikeSharp event handlers thin; push aggregation and persistence work into dedicated services.
- Avoid mixing game-event parsing, business rules, and SQL access inside the same class.
- Use clear names over abbreviations unless the term is a standard CS2 stat such as KDA.
- Prefer immutable DTOs or record types for snapshots and persistence payloads when the codebase structure allows it.
- Make nullable handling explicit for player identities, disconnected clients, and partially available game data.
- Guard every event-driven write path against duplicate processing when reconnects, round transitions, or plugin reloads can occur.

## C# Reliability Rules

- Treat server callbacks and player lifecycle events as unreliable boundaries: validate entity state before reading data.
- Do not assume a player object remains valid after asynchronous or deferred work.
- Prefer batching or queued persistence over synchronous database writes directly in hot event paths.
- Emit structured logs for failed persistence, dropped events, and schema/config errors.
- Keep configuration parsing strict and fail early on invalid MySQL connection settings.

## Statistics Rules

- Separate raw counters from derived metrics.
- Compute ratios such as KD from stored counters instead of using them as primary stored facts unless a reporting snapshot explicitly needs them.
- Store timestamps in UTC.
- Keep map-level, session-level, and global player aggregates as distinct persistence concepts.
- When collecting equipment or stuff stats, model the source event or action explicitly so the data remains auditable.

## MySQL Schema Rules

- Use normalized core tables for players, sessions, matches/maps, and event-derived aggregates.
- Use stable external identifiers such as SteamID as the primary player identity anchor.
- Add created-at and updated-at timestamps on durable tables unless there is a clear reason not to.
- Add explicit foreign keys where operationally safe and where CounterStrikeSharp ingestion flow will not be harmed by insert ordering.
- Add indexes for expected lookups: player identity, session, map/match, and time ranges.
- Avoid storing the same aggregate in multiple tables unless the duplication is intentional for query performance and documented.
- Keep nullable columns intentional; do not use nullable fields to hide unknown schema decisions.

## SQL Change Rules

- Prefer additive schema evolution.
- Document every new table, column, index, and aggregate purpose in the journals.
- If destructive schema changes become necessary, document rollback and migration impact before implementing them.

## Configuration Rules

- Keep MySQL configuration grouped and explicit.
- Keep stat-module toggles independent so unsupported modules can be disabled without code edits.
- Expose synchronization intervals and flush thresholds as configuration, not hard-coded constants.

## Documentation Rules

- Update `docs/journals/worklog.md` after each meaningful implementation step.
- Update `docs/journals/sources.md` whenever new docs, examples, or repositories are used to justify behavior.
- Update `docs/journals/architecture.md` when service boundaries, schema shape, or data flow changes.
- Update `docs/journals/decisions.md` whenever a technical decision is accepted, revised, or rejected.