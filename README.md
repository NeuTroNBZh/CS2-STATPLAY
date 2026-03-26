# CS2-STATPLAY

CS2-STATPLAY is a CounterStrikeSharp plugin for Counter-Strike 2 servers that captures server and player statistics and persists them to MySQL.

The project is designed for practical server deployment, with versioned release packages that are ready to copy into a CounterStrikeSharp server layout.

## Features

- Player session tracking
- Kills, deaths, assists tracking
- Headshot capture when available from validated events
- Weapon fire and action-event capture
- Real-time connected-player snapshots
- Map/session-oriented history
- MySQL persistence
- Ready-to-deploy packaged releases

## Repository Layout

- `src/CS2Stats.Contracts`: shared contracts and configuration models
- `src/CS2Stats.Plugin`: CounterStrikeSharp plugin runtime and persistence code
- `src/CS2Stats.Tests`: automated tests
- `sql/`: schema and aggregation procedures
- `scripts/`: release packaging automation
- `docs/journals/`: architecture, decisions, sources, and work log

## Requirements

- .NET 8 SDK
- CounterStrikeSharp-compatible CS2 server
- MySQL 8+
- PowerShell 5.1+ or PowerShell 7+

## Build

```powershell
dotnet build CSStat.sln
```

To build the plugin release package directly:

```powershell
dotnet build src/CS2Stats.Plugin/CS2Stats.Plugin.csproj -c Release
```

Or explicitly run the packager:

```powershell
pwsh ./scripts/package-release.ps1
```

## Release Output

The release process generates:

- `artifacts/CS2-STATPLAY-<version>-linux-x64/`
- `artifacts/CS2-STATPLAY-<version>-linux-x64.zip`
- `artifacts/SHA256SUMS.txt`

The package includes:

- plugin binaries in a CounterStrikeSharp-ready layout
- starter configuration
- SQL schema files
- `install.sh`
- `install.ps1`
- deployment guide
- changelog

## Deployment

See [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md).

## CI And Releases

- `.github/workflows/release-package.yml` builds and uploads artifacts
- `.github/workflows/github-release.yml` creates a GitHub Release when a tag like `v0.9.0` is pushed

## Tests

```powershell
dotnet test CSStat.sln
```

## Publish To GitHub

If this folder is not yet a Git repository, initialize it and push it to GitHub:

```powershell
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin <your-github-repo-url>
git push -u origin main
```

To publish a release:

```powershell
git tag v0.9.0
git push origin v0.9.0
```