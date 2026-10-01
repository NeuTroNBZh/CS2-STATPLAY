# Changelog

All notable changes to this project are documented in this file.

## [1.1.2] - 2026-10-01

### Fixed
- MySQL 8 compatibility: stored procedures are now dropped then created (`CREATE OR REPLACE PROCEDURE` only exists in MariaDB). Database initialization no longer fails and outdated procedures are replaced at startup.
- Full stats refresh no longer times out: new index `ix_player_action_events_player_type (player_id, action_type, action_value)` (migration 7). Measured on MySQL 8.4 with 200 players and 360k events: 125 s -> 0.2 s.
- `rounds_played` (lifetime and map stats) counts the rounds started while the player was connected, like session stats, instead of every round of each map where the player had an event.

## [1.0.1] - 2026-03-29

### Fixed
- Playtime inflation fix on disconnect: session close now targets the latest open session by player, without strict map-session filter.
- Additional hardening on connect: any pre-existing open session for the same player is auto-closed at new connect time to prevent overlapping sessions.

### Changed
- Version bumped from `1.0.0` to `1.0.1` in runtime/plugin metadata and packaging defaults.

### Release Assets
- `CS2-STATPLAY-1.0.1-linux-x64.zip` (with config)
- `CS2-STATPLAY-1.0.1-linux-x64-update-no-config.zip` (without config)
- `SHA256SUMS.txt`

## [1.0.0] - 2026-03-27

### Added
- Professional repository baseline files: `README.md`, `LICENSE`, `.gitignore`, `CONTRIBUTING.md`, `SECURITY.md`.
- Full data dictionary and web/API reuse guide: `docs/STATS_DATA_REFERENCE.md`.
- Clear installation and update model documentation for both package variants.

### Changed
- Version bumped from `0.9.0` to `1.0.0` in runtime/plugin metadata.
- Packaging defaults and CI package version default aligned to `1.0.0`.

### Release Assets
- `CS2-STATPLAY-1.0.0-linux-x64.zip` (with config)
- `CS2-STATPLAY-1.0.0-linux-x64-update-no-config.zip` (without config)
- `SHA256SUMS.txt`
