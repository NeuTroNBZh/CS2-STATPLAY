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
- Les artefacts de sortie sont produits dans `artifacts/` sous deux variantes operationnelles:
  - variante standard avec config (`CS2-STATPLAY-<version>-linux-x64` + zip) pour installation initiale,
  - variante update sans config (`CS2-STATPLAY-<version>-linux-x64-update-no-config` + zip) pour mise a jour sans ecraser la config serveur.
- Les deux variantes restent en format drag-and-drop (`addons/`) pour deploiement simple.
- Le fichier `SHA256SUMS.txt` reste genere a cote du zip pour verification d'integrite.
- Un hook MSBuild dans `Directory.Build.targets` declenche le packaging apres un build Release du projet plugin.
- Un workflow GitHub Actions produit et publie les artefacts de release en CI.
- Les workflows CI et release verifient le checksum SHA256 du zip avant publication/upload.

## Operational Documentation Update
- Ajout d'un guide operationnel court pour validation serveur Dathost.
- Le guide formalise une verification end-to-end: installation plugin, configuration MySQL, test en jeu, verification SQL.

## Database Initialization Reliability Update
- L'auto-initialisation SQL ne repose plus sur un split naif au `;`.
- Les scripts avec `DELIMITER` sont maintenant decoupes proprement avant execution, ce qui aligne mieux notre approche avec le besoin reel de creation de procedures MySQL.

## Deployment Path Reliability Update
- Le package de release reste livre avec un dossier `addons/`, mais l'installation cible doit etre `game/csgo/addons` cote serveur.
- Les scripts d'installation resolvent maintenant automatiquement `game/csgo` quand on leur fournit une racine serveur plus haute.
- Les guides de deploiement insistent desormais sur la verification d'un chargement effectif CounterStrikeSharp/CS2Stats apres copie.

## Plugin Discovery Reliability Update
- L'assembly principal du plugin est aligne sur `CS2Stats` pour correspondre au dossier package `plugins/CS2Stats`.
- La convention CounterStrikeSharp `plugins/<Name>/<Name>.dll` est maintenant respectee.
- Le chemin de configuration package suit la meme identite `configs/plugins/CS2Stats/CS2Stats.json`.

## MySQL Privilege Compatibility Update
- Le flux d'auto-initialisation accepte maintenant un mode "base deja existante" quand `CREATE DATABASE` est interdit.
- Le service verifie alors l'accessibilite de la base configuree (`SELECT 1`) puis poursuit la creation des tables/procedures.
- Ce comportement est adapte aux hebergeurs qui pre-creent les schemas et limitent les privileges SQL.

## Packaged Config Safety Update
- Une garde de configuration detecte maintenant la signature exacte de la config MySQL d'exemple packagee (`127.0.0.1`, `cs2_stats`, `cs2stats`, `change-me`).
- Quand cette signature est active, le plugin saute volontairement l'auto-init DB et remplace l'ecriture MySQL par `NoOpStatsWriter`.
- L'objectif est de transformer un deploiement stale en erreur operationnelle explicite plutot qu'en simple `Access denied` ambigu.

## Repository Minimal Layout Update
- Le workspace a ete reduit au strict necessaire pour coder et faire tourner le plugin.
- Elements conserves pour l'implementation: `src/`, `sql/`, `config/`, `scripts/`, `CSStat.sln`, `Directory.Build.targets`.
- Elements conserves pour l'assistance IA et la traçabilite: `.github/`, `.vscode/`, `docs/`.
- Elements supprimes comme non essentiels au flux code/runtime: artefacts generes, dossiers externes de reference/donnees volumineuses et metadata docs racine non critiques.

## Repository Publishing Layer (V1.0.0)
- Le repo expose maintenant un socle publication complet et coherent:
  - `README.md` (anglais, installation, update path, release variants, architecture rapide),
  - `LICENSE` (MIT),
  - `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`.
- Une documentation data orientee consommation applicative est ajoutee:
  - `docs/STATS_DATA_REFERENCE.md` (catalogue complet des tables, colonnes, relations, requetes type, patterns API/web/IA).
- L'objectif est double:
  - faciliter l'adoption serveur (ops)
  - accelerer la reutilisation des donnees par des developpeurs web/API/AI sans reverse engineering SQL.

## Release Posture V1.0.0
- Version plugin alignee sur `1.0.0` (runtime + csproj + defaults packaging CI/script).
- Le modele dual-package est conserve et formalise pour production:
  - package normal avec config pour premiere installation,
  - package update sans config pour upgrade sans ecrasement de credentials.
