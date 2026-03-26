# Architecture Journal

## Purpose
Suivre l'architecture du plugin CS2, ses composants, ses flux de donnees et ses choix structurants.

## Current State
- Workspace initialise.
- Aucun code plugin cree pour le moment.
- Agent personnalise prevu pour piloter la conception et l'implementation.
- Agent de recherche strict prevu pour valider la disponibilite reelle des statistiques avant implementation.
- Fichier d'instructions workspace prevu pour imposer des regles C# et MySQL plus strictes.

## Target Plugin Scope
- Plugin CounterStrikeSharp pour Counter-Strike 2.
- Collecte des statistiques serveur et joueur si les donnees sont reellement disponibles.
- Priorites V1: temps de jeu, K/D/A, headshots si disponibles, stats de stuff/equipement, joueurs connectes en temps reel, historique par map/match.
- Persistance MySQL.
- Configuration simple et extensible.

## Persistence Targets
- Resume global par joueur.
- Historique par session de jeu.
- Historique par match ou map.

## Configuration Targets
- Connexion MySQL.
- Activation ou desactivation des modules de stats.
- Intervalle de synchronisation.

## Proposed Modules
- Event Capture Layer (CounterStrikeSharp handlers/listeners)
	- capte uniquement des faits valides (connect/disconnect, death, weapon_fire, round start/end, actions confirmees).
- Ingestion Mapping Layer
	- convertit les evenements CS2 en contrats internes stables (`src/CS2Stats.Contracts/StatsContracts.cs`).
- Aggregation Layer
	- maintient les compteurs bruts par joueur/session/map et snapshots de presence.
- Persistence Layer
	- persistence MySQL asynchrone en lot, basee sur le schema `sql/001_v1_baseline_schema.sql`.
- Configuration Layer
	- options MySQL + toggles modules + intervalles (`src/CS2Stats.Contracts/CS2StatsConfig.cs`).

## Customization Assets
- `.github/agents/cs2-stats-lead.agent.md`: agent principal de conception et implementation.
- `.github/agents/cs2-stats-research.agent.md`: agent de recherche strict sans edition ni terminal.
- `.github/instructions/cs2-stats-csharp-mysql.instructions.md`: regles workspace pour implementation C#, SQL et configuration.

## Data Flow
- Event CS2 valide -> mapping vers contrat interne -> accumulation compteurs bruts -> flush periodique MySQL.
- Presence temps reel -> `Utilities.GetPlayers()` -> snapshot en memoire -> `presence_snapshots` + `presence_snapshot_players`.
- Sessions joueur -> bornes connect/disconnect + horloge serveur -> `player_sessions` + `player_session_stats`.
- Historique map/round -> map session + round lifecycle -> `map_sessions` + `rounds` + `player_map_stats`.
- Kill details et actions -> tables d'evenements (`kill_events`, `player_action_events`) pour audit et derivees futures.

## V1 Implemented Artifacts
- SQL baseline: `sql/001_v1_baseline_schema.sql`
- Contracts C#: `src/CS2Stats.Contracts/StatsContracts.cs`
- Config contracts: `src/CS2Stats.Contracts/CS2StatsConfig.cs`
- Runtime example config: `config/cs2stats.example.json`

## Open Technical Questions
- Quelles statistiques sont directement disponibles via CounterStrikeSharp ?
- Quelles statistiques doivent etre derivees ou accumulees par le plugin ?
- Quel niveau de detail est realiste pour les stats d'equipement et de stuff ?
- Comment modeliser proprement les trois niveaux de persistance sans dupliquer inutilement les donnees ?

## Current State Update (Runtime V1)
- Solution et projets C# en place: `CSStat.sln`, `CS2Stats.Contracts`, `CS2Stats.Plugin`.
- Couche runtime de capture implementee et compilee.
- `StatsCaptureService` bufferise les evenements V1 confirmes vers `StatsBatch`.
- `CS2StatsPlugin` orchestre handlers, listener map start, timers de snapshot/flush, et flush asynchrone.
- Ecriture MySQL encore non branchee: `NoOpStatsWriter` sert de placeholder pour verifier le pipeline.

## Runtime Components Added
- `src/CS2Stats.Plugin/CS2StatsPlugin.cs`
  - plugin principal CounterStrikeSharp (`BasePlugin`, `IPluginConfig<PluginConfig>`)
  - enregistrement handlers pour: `EventPlayerConnectFull`, `EventPlayerDisconnect`, `EventPlayerDeath`, `EventWeaponFire`, `EventBombPlanted`, `EventBombDefused`, `EventRoundStart`, `EventRoundEnd`, `EventRoundMvp`
  - timer de flush et timer de snapshot presence
- `src/CS2Stats.Plugin/StatsCaptureService.cs`
  - mapping event -> contrats internes
  - buffering thread-safe par verrou interne
  - drainage atomique en `StatsBatch`
- `src/CS2Stats.Plugin/IStatsWriter.cs` + `NoOpStatsWriter.cs`
  - frontiere de persistance pour brancher MySQL sans modifier les handlers

## Persistence Layer Update (MySQL)
- `MySqlStatsWriter` ajoute comme implementation concrete de `IStatsWriter`.
- Ecriture transactionnelle par `StatsBatch` pour limiter les ecritures partielles.
- Strategie de resilience initiale:
  - fallback no-op si configuration MySQL invalide
  - rollback transactionnel en cas d'erreur
  - propagation d'erreur au plugin (log).

## Release Packaging Update
- Packaging de release automatise via `scripts/package-release.ps1`.
- Le package cible une arborescence directement compatible CounterStrikeSharp:
  - `addons/counterstrikesharp/plugins/CS2Stats`
  - `addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json`
- Les artefacts de sortie sont produits dans `artifacts/` sous deux formes:
  - dossier versionne pret a copier sur serveur
  - archive zip du meme contenu pour distribution simple
- Le package inclut aussi les scripts SQL V1 pour installer la base sans revenir au repo source.
- Le package inclut un `install.sh` pour copier rapidement les fichiers plugin sur un serveur Linux.
- Le package inclut aussi `install.ps1` pour simplifier l'installation sur serveur Windows.
- Le packaging produit aussi un `CHANGELOG.md` dans le bundle et un `SHA256SUMS.txt` a cote du zip pour verification d'integrite.
- Un hook MSBuild dans `Directory.Build.targets` declenche le packaging apres un build Release du projet plugin.
- Un workflow GitHub Actions produit et publie les artefacts de release en CI.
- Les workflows CI et release verifient le checksum SHA256 du zip avant publication/upload.
