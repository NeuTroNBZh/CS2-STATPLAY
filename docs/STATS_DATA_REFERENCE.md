# CS2 Stats Data Reference (Web/API/AI)

This document explains exactly how to reuse CS2-STATPLAY data in a website, API, BI stack, or AI workflow.

It is intentionally explicit for developers and coding agents.

## Data Model Overview

The schema combines raw events and aggregate tables:

- raw facts for audit and replay (`kill_events`, `player_action_events`, `presence_*`)
- structural context (`players`, `map_sessions`, `player_sessions`, `rounds`)
- query-optimized aggregates (`player_lifetime_stats`, `player_session_stats`, `player_map_stats`)

Recommended strategy for web apps:

- use aggregate tables for dashboards and profile pages
- use raw event tables for advanced pages (kill feed, timelines, deep analytics)

## Table Catalog

## 1) `players`

Purpose: canonical player identity (Steam ID anchor).

Grain: 1 row per player.

Columns:

- `player_id`: internal PK (BIGINT UNSIGNED)
- `steam_id64`: unique Steam64 identifier
- `first_seen_utc`: first player appearance on this server
- `last_seen_utc`: latest observed activity
- `created_at_utc`: row creation timestamp
- `updated_at_utc`: row update timestamp

## 2) `map_sessions`

Purpose: timeline of each map runtime on the server.

Grain: 1 row per map session.

Columns:

- `map_session_id`: internal PK
- `map_name`: map identifier (for example `de_mirage`)
- `started_at_utc`: map session start
- `ended_at_utc`: map session end (nullable if active)
- `server_current_time_start`: server clock at start (optional)
- `server_current_time_end`: server clock at end (optional)
- `total_rounds`: rounds observed in this map session
- `created_at_utc`, `updated_at_utc`

## 3) `player_sessions`

Purpose: per-player connection sessions inside a map session.

Grain: 1 row per player connect/disconnect cycle.

Columns:

- `player_session_id`: internal PK
- `player_id`: FK -> `players.player_id`
- `map_session_id`: FK -> `map_sessions.map_session_id`
- `connected_at_utc`: connect timestamp
- `disconnected_at_utc`: disconnect timestamp (nullable if still online)
- `disconnect_reason`: optional text reason
- `server_current_time_connect`: server clock at connect
- `server_current_time_disconnect`: server clock at disconnect
- `created_at_utc`, `updated_at_utc`

## 4) `rounds`

Purpose: round lifecycle per map session.

Grain: 1 row per round number in a map session.

Columns:

- `round_id`: internal PK
- `map_session_id`: FK -> `map_sessions.map_session_id`
- `round_number`: sequential number in map session
- `started_at_utc`, `ended_at_utc`
- `end_reason`: optional integer code from game event
- `end_message`: optional reason text
- `player_count_at_end`: connected players when round ended
- `round_time_seconds`: duration if available
- `created_at_utc`, `updated_at_utc`

## 5) `player_lifetime_stats`

Purpose: global totals per player across all sessions/maps.

Grain: 1 row per player.

Columns:

- `player_id`: PK + FK -> `players.player_id`
- `kills`, `deaths`, `assists`, `headshots`
- `weapon_fire_count`
- `hegrenade_detonations`, `flashbang_detonations`, `molotov_detonations`, `smokegrenade_detonations`
- `bomb_plants`, `bomb_defuses`
- `mvps`, `rounds_played`, `total_playtime_seconds`
- `created_at_utc`, `updated_at_utc`

## 6) `player_session_stats`

Purpose: totals per player session.

Grain: 1 row per `player_session_id`.

Columns:

- `player_session_id`: PK + FK -> `player_sessions.player_session_id`
- same stat counters as lifetime table
- `playtime_seconds`
- `created_at_utc`, `updated_at_utc`

## 7) `player_map_stats`

Purpose: totals for one player in one map session.

Grain: composite key (`player_id`, `map_session_id`).

Columns:

- `player_id`: FK -> `players.player_id`
- `map_session_id`: FK -> `map_sessions.map_session_id`
- same stat counters as lifetime/session tables
- `playtime_seconds`
- `created_at_utc`, `updated_at_utc`

## 8) `kill_events`

Purpose: detailed kill feed facts.

Grain: 1 row per kill event.

Columns:

- `kill_event_id`: internal PK
- `map_session_id`: FK -> `map_sessions`
- `round_id`: FK -> `rounds` (nullable)
- `occurred_at_utc`
- `attacker_player_id`: FK -> `players` (nullable)
- `victim_player_id`: FK -> `players` (nullable)
- `assister_player_id`: FK -> `players` (nullable)
- `weapon_name`
- `is_headshot`, `hitgroup`, `penetrated`
- `noscope`, `thrusmoke`, `distance`
- `attacker_blind`, `attacker_in_air`, `assisted_flash`
- `created_at_utc`

## 9) `player_action_events`

Purpose: normalized player actions not represented as kills.

Grain: 1 row per player action event.

Columns:

- `player_action_event_id`: internal PK
- `player_id`: FK -> `players`
- `map_session_id`: FK -> `map_sessions`
- `round_id`: FK -> `rounds` (nullable)
- `occurred_at_utc`
- `action_type`: action category
- `action_value`: optional detail (for example grenade type)
- `created_at_utc`

Observed action types in V1:

- `weapon_fire`
- `grenade_detonation` with value: `hegrenade`, `flashbang`, `molotov`, `smokegrenade`
- `bomb_planted`
- `bomb_defused`
- `round_mvp`

## 10) `presence_snapshots`

Purpose: real-time snapshots of connected population.

Grain: 1 row per capture timestamp.

Columns:

- `presence_snapshot_id`: internal PK
- `map_session_id`: FK -> `map_sessions`
- `captured_at_utc`
- `connected_player_count`
- `created_at_utc`

## 11) `presence_snapshot_players`

Purpose: players present in each snapshot.

Grain: composite key (`presence_snapshot_id`, `player_id`).

Columns:

- `presence_snapshot_id`: FK -> `presence_snapshots`
- `player_id`: FK -> `players`
- `slot_index`: optional slot
- `team_value`: optional team code
- `created_at_utc`

## Stored Procedures

Procedures in `sql/002_v1_aggregation_stored_procedures.sql`:

- `sp_refresh_player_lifetime_stats(IN p_player_id BIGINT UNSIGNED)`
  - refreshes all players if `p_player_id IS NULL`
- `sp_refresh_player_session_stats(IN p_player_session_id BIGINT UNSIGNED)`
  - refreshes one session aggregate
- `sp_refresh_player_map_stats(IN p_player_id BIGINT UNSIGNED, IN p_map_session_id BIGINT UNSIGNED)`
  - refreshes one player-map aggregate

## Relationships

Primary links:

- `players` 1:N `player_sessions`
- `map_sessions` 1:N `player_sessions`
- `map_sessions` 1:N `rounds`
- `players` 1:N `kill_events` (attacker/victim/assister)
- `players` 1:N `player_action_events`
- `map_sessions` 1:N `kill_events`, `player_action_events`, `presence_snapshots`
- `presence_snapshots` 1:N `presence_snapshot_players`

## Practical Website/API Use Cases

## A) Global leaderboard (lifetime)

```sql
SELECT
  p.steam_id64,
  ls.kills,
  ls.deaths,
  ls.assists,
  ls.headshots,
  ROUND(ls.kills / NULLIF(ls.deaths, 0), 2) AS kd_ratio,
  ls.total_playtime_seconds
FROM player_lifetime_stats ls
JOIN players p ON p.player_id = ls.player_id
ORDER BY ls.kills DESC
LIMIT 100;
```

## B) Player profile page

```sql
SELECT
  p.steam_id64,
  ls.kills,
  ls.deaths,
  ls.assists,
  ls.headshots,
  ls.weapon_fire_count,
  ls.bomb_plants,
  ls.bomb_defuses,
  ls.rounds_played,
  ls.total_playtime_seconds,
  ls.updated_at_utc
FROM players p
JOIN player_lifetime_stats ls ON ls.player_id = p.player_id
WHERE p.steam_id64 = ?;
```

## C) Recent sessions for one player

```sql
SELECT
  ps.player_session_id,
  ms.map_name,
  ps.connected_at_utc,
  ps.disconnected_at_utc,
  ss.kills,
  ss.deaths,
  ss.assists,
  ss.headshots,
  ss.playtime_seconds
FROM players p
JOIN player_sessions ps ON ps.player_id = p.player_id
JOIN map_sessions ms ON ms.map_session_id = ps.map_session_id
LEFT JOIN player_session_stats ss ON ss.player_session_id = ps.player_session_id
WHERE p.steam_id64 = ?
ORDER BY ps.connected_at_utc DESC
LIMIT 20;
```

## D) Live online panel

```sql
SELECT
  s.presence_snapshot_id,
  s.captured_at_utc,
  s.connected_player_count,
  p.steam_id64,
  sp.slot_index,
  sp.team_value
FROM presence_snapshots s
JOIN presence_snapshot_players sp ON sp.presence_snapshot_id = s.presence_snapshot_id
JOIN players p ON p.player_id = sp.player_id
WHERE s.presence_snapshot_id = (
  SELECT MAX(presence_snapshot_id) FROM presence_snapshots
);
```

## E) Map-session report

```sql
SELECT
  ms.map_session_id,
  ms.map_name,
  ms.started_at_utc,
  ms.ended_at_utc,
  ms.total_rounds,
  COUNT(DISTINCT ps.player_id) AS unique_players
FROM map_sessions ms
LEFT JOIN player_sessions ps ON ps.map_session_id = ms.map_session_id
GROUP BY ms.map_session_id
ORDER BY ms.started_at_utc DESC
LIMIT 50;
```

## F) Headshot ranking (minimum sample)

```sql
SELECT
  p.steam_id64,
  ls.kills,
  ls.headshots,
  ROUND(ls.headshots / NULLIF(ls.kills, 0) * 100, 2) AS hs_pct
FROM player_lifetime_stats ls
JOIN players p ON p.player_id = ls.player_id
WHERE ls.kills >= 50
ORDER BY hs_pct DESC
LIMIT 100;
```

## API Design Suggestions

Suggested endpoints:

- `GET /api/leaderboard`
- `GET /api/players/{steamId64}`
- `GET /api/players/{steamId64}/sessions`
- `GET /api/maps/recent`
- `GET /api/live/presence`
- `GET /api/killfeed?mapSessionId=...&from=...&to=...`

Implementation notes:

- paginate heavy event endpoints (`kill_events`, `player_action_events`)
- add HTTP cache headers on aggregate endpoints
- avoid exposing internal `player_id` publicly; use `steam_id64`

## AI Reuse Ideas

Useful tasks for AI agents:

- detect anomalies in player progression (sudden ratio shifts)
- summarize map sessions in plain language
- generate weekly recaps from aggregate deltas
- classify play style from action distribution (entry, support, objective)

Recommended data access pattern for AI:

- read aggregates first (`player_lifetime_stats`, `player_session_stats`, `player_map_stats`)
- drill into raw event windows only when explanation detail is required

## Performance Tips

- keep existing FK/index strategy from baseline schema
- read from aggregate tables for homepage dashboards
- run heavy analytics during low-traffic windows
- archive very old raw events if volume grows significantly

## Compatibility Notes

- all timestamps are UTC
- ratios (KD, HS%) should be computed at query time from counters
- nullable columns in raw events reflect real game/event availability
