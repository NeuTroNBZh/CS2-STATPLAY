# Changelog

All notable changes to this project are documented in this file.

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
