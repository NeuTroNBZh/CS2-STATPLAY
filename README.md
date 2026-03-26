# CS2-STATPLAY

CS2-STATPLAY is a production-oriented CounterStrikeSharp plugin for Counter-Strike 2 that captures server and player statistics, persists them to MySQL, and ships as a ready-to-deploy release package.

It is intended for server operators who want a practical stats plugin with a clean deployment flow, structured data persistence, and reproducible GitHub releases.

## Highlights

- CounterStrikeSharp plugin for CS2 dedicated servers
- MySQL-backed persistence for player, session, and map-level history
- Kills, deaths, assists, headshots, weapon fire, and action-event capture
- Real-time presence snapshots for connected players
- Release packaging that produces a server-ready folder and zip
- Linux and Windows installer scripts included in releases
- GitHub Actions for CI artifacts and tagged releases

## Release Package Layout

Each release generates a package like:

```text
CS2-STATPLAY-0.9.0-linux-x64/
	addons/
		counterstrikesharp/
			plugins/
				CS2Stats/
			configs/
				plugins/
					CS2Stats/
						CS2Stats.json
	sql/
		001_v1_baseline_schema.sql
		002_v1_aggregation_stored_procedures.sql
	install.sh
	install.ps1
	CHANGELOG.md
	DEPLOYMENT_GUIDE.md
	README_PACKAGE.txt
```

The corresponding zip and SHA256 checksum are generated automatically.

## Features

- Player session tracking
- K/D/A capture
- Headshot tracking from validated events
- Weapon fire and objective action capture
- Online presence snapshots
- Map/session persistence model
- SQL aggregation procedures for reporting
- Automated packaging and release flow

## Project Structure

- `src/CS2Stats.Contracts` shared contracts and configuration models
- `src/CS2Stats.Plugin` plugin runtime and persistence code
- `src/CS2Stats.Tests` automated tests
- `sql/` database schema and aggregation procedures
- `scripts/` packaging automation
- `config/` example runtime configuration

## Requirements

- .NET 8 SDK
- CounterStrikeSharp environment for Counter-Strike 2
- MySQL 8+
- PowerShell 5.1+ or PowerShell 7+

## Quick Start

Build the solution:

```powershell
dotnet build CSStat.sln
```

Generate a release package:

```powershell
pwsh ./scripts/package-release.ps1
```

Or build the plugin in Release, which also generates the package automatically:

```powershell
dotnet build src/CS2Stats.Plugin/CS2Stats.Plugin.csproj -c Release
```

## Deployment

Full deployment instructions are available in [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md).

Typical deployment flow:

1. Build or download the release package.
2. Extract it at the root of the server.
3. Edit `addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json`.
4. Import the SQL files into MySQL.
5. Restart the server or reload the plugin.

## Testing

```powershell
dotnet test CSStat.sln
```

## CI And GitHub Releases

This repository includes:

- `.github/workflows/release-package.yml` for CI artifact generation
- `.github/workflows/github-release.yml` for release publication on tags like `v0.9.0`

Tagged releases publish:

- packaged zip
- SHA256 checksums
- package readme
- package changelog

## Versioning

Current packaged version: `0.9.0`.

To publish a release manually:

```powershell
git tag v0.9.0
git push origin v0.9.0
```

## Repository Quality

The project includes:

- release automation
- checksum verification in CI
- Linux and Windows installers
- SQL schema and aggregation procedures
- tests for core capture behavior

## License

No license file is currently defined in this repository.

If the project is intended for long-term public distribution, adding a license file is recommended.