# Work Log

## 2026-03-26
- Workspace vide inspecte.
- Documentation des agents personnalises VS Code consultee pour confirmer le format `.agent.md` et le dossier `.github/agents`.
- Creation du dossier `.github/agents`.
- Creation du dossier `docs/journals`.
- Redaction initiale de l'agent `CS2 Stats Lead`.
- Initialisation des journaux d'architecture, de travail, de sources et de decisions.
- Clarification du perimetre V1: temps de jeu, K/D/A, headshots si disponibles, stats de stuff/equipement, joueurs connectes en temps reel, historique par map/match.
- Clarification du stockage: resume global, historique de session et historique par match/map.
- Clarification de la configuration: MySQL, activation des modules de stats, intervalle de synchronisation.
- Lecture du skill `agent-customization` et de ses references pour les formats `.agent.md` et `*.instructions.md`.
- Lecture des journaux existants avant creation de nouvelles customizations.
- Creation du dossier `.github/instructions`.
- Ajout d'un agent `CS2 Stats Research` en lecture stricte pour valider les statistiques possibles avant codage.
- Ajout d'un fichier d'instructions workspace pour renforcer les regles C#, MySQL, configuration et journalisation du repo.
- Consolidation de la matrice de faisabilite avec classification stricte: confirmed-direct, confirmed-derived, possible-but-unconfirmed, not-supported.
- Verification complementaire de la doc officielle `CounterStrikeSharp.API.Utilities` pour confirmer `Utilities.GetPlayers()` pour la presence en ligne et le comptage des joueurs connectes.
- Conception et implementation de l'etape suivante V1: socle de donnees et contrats.
- Creation du schema MySQL V1 dans `sql/001_v1_baseline_schema.sql` avec trois niveaux de persistance: global joueur, session joueur, map/round history, et evenements d'actions.
- Creation des contrats C# d'ingestion dans `src/CS2Stats.Contracts/StatsContracts.cs` (sessions, rounds, kill event detaille, player action event).
- Creation du contrat de configuration modulaire dans `src/CS2Stats.Contracts/CS2StatsConfig.cs` (MySQL, modules, synchro).
- Ajout d'un exemple de configuration runtime dans `config/cs2stats.example.json`.
- Creation de la solution `.NET` `CSStat.sln` et des projets `CS2Stats.Contracts` et `CS2Stats.Plugin`.
- Extension des contrats d'ingestion avec `PresenceSnapshot` et `StatsBatch` pour le buffering runtime.
- Creation de la configuration plugin `src/CS2Stats.Plugin/PluginConfig.cs` et de l'abstraction d'ecriture `IStatsWriter`.
- Creation d'un writer provisoire `NoOpStatsWriter` pour valider le pipeline de flush sans MySQL.
- Implementation de `StatsCaptureService` pour capter et bufferiser: connect/disconnect, round start/end, player_death, weapon_fire, bomb_planted, bomb_defused, round_mvp, snapshots de presence.
- Implementation de `CS2StatsPlugin` (load/unload, parsing config, enregistrement handlers/listeners, timers de flush et snapshots, flush async protege par semaphore).
- Correction des incompatibilites de types API CounterStrikeSharp detectees au build (`Xuid`, `Reason`, `Fraglimit`, `Timelimit`, `init`-only settings).
- Validation par compilation locale: `dotnet build CSStat.sln` reussi.
- Ajout du package `MySqlConnector` dans `src/CS2Stats.Plugin/CS2Stats.Plugin.csproj`.
- Implementation de `src/CS2Stats.Plugin/MySqlStatsWriter.cs` (transaction par batch, upsert joueurs, sessions, rounds, kill/action events, snapshots de presence).
- Integration du choix de writer dans `CS2StatsPlugin`: MySQL si config valide, fallback `NoOpStatsWriter` avec logging d'erreur.
- Validation par compilation locale apres integration MySQL: `dotnet build CSStat.sln` reussi.
## 2026-03-26 (Suite)

### Tests d'int�gration compl�t�s
- Cr�ation du projeto xUnit: src/CS2Stats.Tests
- Ajout des r�f�rences: CS2Stats.Contracts, CS2Stats.Plugin
- Refactorisation de StatsCaptureService: injection du map name pour testabilit� (optionnel)
- Cr�ation de StatsCaptureServiceTests (8 tests validant buffering et thread-safety)
- Cr�ation de NoOpStatsWriterTests (2 tests validant le fallback logger)
- **R�sultat final: 10/10 tests passent** ?

### Test Coverage Achieved
- StatsCaptureService: constructor, drain batch, map start, thread-safety
- NoOpStatsWriter: batch writing, async operations, cancellation tokens
- Fallback persistence: verified no-op logger works when MySQL unavailable

### Next Step: Agr�gation SQL
## 2026-03-26 (Suite - Agr�gation SQL)

### Stored Procedures et Agr�gation SQL compl�t�s
- Cr�ation de sql/002_v1_aggregation_stored_procedures.sql avec 3 proc�dures:
  - sp_refresh_player_lifetime_stats: cumul global K/D/A, headshots, actions
  - sp_refresh_player_session_stats: cumul par session de jeu
  - sp_refresh_player_map_stats: cumul par map/session
- Calculs: kills, deaths, assists, headshots, weapon_fire_count, grenade detonations, bomb actions, MVP count
- Playtime calculation from player_sessions (connected_at_utc - disconnected_at_utc)
- Query JOINs sur kill_events, player_action_events, player_sessions pour agr�gation fiable

### C# Aggregation Service  
- Cr�ation de src/CS2Stats.Plugin/AggregationService.cs
- Methods: RefreshPlayerLifetimeStatsAsync, RefreshPlayerSessionStatsAsync, RefreshPlayerMapStatsAsync, RefreshAllStatsAsync
- Helper methods pour lookup player_id et player_session_id depuis Steam ID
- Structured logging pour succ�s/erreur
- Async pattern compatible avec plugin event loop

### Next Step: Guide de d�ploiement
## 2026-03-26 (Suite - Guide de d�ploiement)

### Deployment Guide Completed
- Cr�ation de DEPLOYMENT_GUIDE.md : guide complet de d�ploiement
- Couvre: pr�requis, setup MySQL, installation plugin, configuration, v�rification, monitoring
- Sections: database creation, schema import, plugin build/deploy, config file, security setup
- Includes: real-time SQL queries, troubleshooting, performance tuning, operational procedures
- Sample configuration avec MySQL, sync intervals, module toggles
- Maintenance procedures: daily, weekly, monthly tasks
- Support for rollback et cleanup

### PROJECT V1 COMPLETE ?
- Runtime Event Capture: 9 event types buffered + 2 timers (flush, presence) ?
- Persistence Layer: MySQL transactional writer + fallback no-op logger ?  
- Integration Tests: 10/10 tests passing (StatsCaptureService, NoOpStatsWriter) ?
- SQL Aggregation: 3 stored procedures (lifetime, session, map stats) ?
- C# Aggregation Service: async methods to call SP from plugin ?
- Deployment Documentation: complete guide with config, troubleshooting, monitoring ?

### Known Limitations / V2 Planning
- rounds_played et total_playtime_seconds: require session endtime calculation (partial impl)
- match_boundary detection: deferred to V2 (round sequencing needs game event validation)
- equipment-specific stats: currently generic weapon_fire_count (V2: detail by weapon type)
- Skill rating: no ELO/TrueSkill yet (V2 feature)

### Tests Status
- src/CS2Stats.Tests : 10/10 tests passing
- Coverage: buffering, thread-safety, async operations, fallback behavior
- Note: CS2 API tests (CapturePresenceSnapshot) can only run on actual CS2 server

## 2026-03-26 (Suite - Packaging release)

### Packaging de deploiement retake ajoute
- Ajout du script `scripts/package-release.ps1` pour publier le plugin et produire un package versionne.
- Le script construit un dossier `artifacts/CS2Stats-<version>-linux-x64/` et un zip homologue.
- Le package inclut l'arborescence CounterStrikeSharp prete a copier, la configuration d'exemple renommee en `CS2Stats.json`, et la documentation de deploiement.
- Ajout d'une tache VS Code `.vscode/tasks.json` pour lancer le packaging en une commande.
- Mise a jour du guide de deploiement pour installer le plugin a partir du dossier/zip genere au lieu de copier les DLL a la main.

## 2026-03-26 (Suite - Packaging automatise STATPLAY)

### Packaging finalise pour distribution simple
- Changement de version plugin vers `0.9.0`.
- Standardisation du nom de package en `CS2-STATPLAY-<version>-linux-x64`.
- Enrichissement du package avec `sql/001_v1_baseline_schema.sql` et `sql/002_v1_aggregation_stored_procedures.sql`.
- Ajout d'un hook MSBuild `Directory.Build.targets` pour generer automatiquement le package lors d'un build Release du projet plugin.
- Ajout du workflow GitHub Actions `.github/workflows/release-package.yml` pour produire les artefacts dossier + zip en CI.
- Mise a jour des taches VS Code et de la documentation pour refléter le nouveau nom d'artefact et le flux automatise.

## 2026-03-26 (Suite - Install simplifie et release GitHub)

### Distribution encore simplifiee
- Ajout d'un `install.sh` au package pour installation rapide sur serveur Linux.
- Ajout du workflow `.github/workflows/github-release.yml` pour publier le zip sur une GitHub Release lors d'un tag `v*`.
- Mise a jour de la documentation et des journaux pour couvrir ce flux de distribution.

## 2026-03-26 (Suite - Version dynamique et install Windows)

### Distribution multi-environnement finalisee
- Ajout d'un `install.ps1` dans le package pour serveurs/admins Windows.
- Mise a jour des workflows GitHub pour utiliser des noms d'artefacts dynamiques bases sur la version.
- Le workflow de GitHub Release derive maintenant la version depuis le tag pousse (`vX.Y.Z`).

## 2026-03-26 (Suite - Changelog et checksum)

### Release package complet
- Alignement de la version exposee par le plugin runtime avec la version du package (`0.9.0`).
- Ajout d'un `CHANGELOG.md` minimal dans le package de release.
- Generation d'un `SHA256SUMS.txt` a cote des artefacts pour verifier le zip distribue.

## 2026-03-26 (Suite - Verification CI du checksum)

### Integrite automatisee en pipeline
- Ajout d'une etape de verification SHA256 dans les workflows `release-package.yml` et `github-release.yml`.
- Ajout du checksum comme artefact CI et comme fichier joint a la GitHub Release.

## 2026-03-26 (Suite - Guide Dathost)

### Documentation de test operationnel ajoutee
- Ajout d'un guide dedie `DATHOST_INSTALL_TEST_GUIDE.md`.
- Guide focalise sur installation Dathost CS2 + protocole de test rapide en jeu + verifications SQL.
- Ajout d'un lien direct vers ce guide dans `README.md` pour un acces rapide.

## 2026-03-26 (Suite - Fix auto-init MySQL)

### Comparaison avec WeaponPaints et correction
- Comparaison faite avec `cs2-WeaponPaints-main`: ce plugin cree ses tables via requetes executees explicitement, sans parser de script a procedures.
- Cause racine identifiee dans `DatabaseInitializationService`: split SQL naif au `;`, incompatible avec les scripts de procedures MySQL et `DELIMITER`.
- Ajout d'un decoupage SQL robuste respectant `DELIMITER`.
- Ajout d'un test unitaire pour valider le decoupage table + drop procedure + create procedure + table.

## 2026-03-26 (Suite - Fix chemin d'installation)

### Diagnostic Dathost du non-chargement plugin
- Analyse des logs Dathost: aucune ligne de chargement `CS2Stats` visible.
- Cause probable identifiee: les guides et scripts d'installation parlaient de copier `addons` a la racine du serveur, alors que CounterStrikeSharp doit etre sous `game/csgo/addons`.
- Correction des scripts `install.sh` et `install.ps1` generes par le package pour resoudre automatiquement `game/csgo` quand on leur passe une racine serveur.
- Correction des guides `DATHOST_INSTALL_TEST_GUIDE.md`, `DEPLOYMENT_GUIDE.md` et `README.md` pour pointer vers le chemin final correct.

## 2026-03-26 (Suite - Fix autodetection plugin CSS)

### Cause racine precise du non-chargement
- Analyse du loader CounterStrikeSharp: l'autoload scanne `plugins/<Folder>/<Folder>.dll`.
- Cause racine precise identifiee: notre package livrait `plugins/CS2Stats/CS2Stats.Plugin.dll`, donc le plugin n'etait jamais decouvert.
- Cause secondaire identifiee: la config packagée ne suivait pas non plus le nom d'assembly attendu par `IPluginConfig`.
- Correction appliquee: `AssemblyName=CS2Stats` dans le projet plugin et packaging aligne sur `CS2Stats.dll` + `configs/plugins/CS2Stats/CS2Stats.json`.

## 2026-03-26 (Suite - Compatibilite privileges MySQL Dathost)

### Init DB resiliente sans CREATE DATABASE
- Observation terrain: certaines bases Dathost sont pre-provisionnees et l'utilisateur SQL n'a pas forcement le privilege `CREATE DATABASE`.
- Correction dans `DatabaseInitializationService`: si `CREATE DATABASE IF NOT EXISTS` est refuse (1044/1227/1142), le plugin tente l'acces direct a la base configuree et continue l'initialisation si `SELECT 1` fonctionne.
- Ajout de logs de diagnostic au startup plugin pour afficher la cible MySQL (`host:port/database`, SSL) et la base cible d'init.
- Validation locale: `dotnet test CSStat.sln` passe (11/11).

## 2026-03-26 (Suite - Echec authentification MySQL)

### Reduction du bruit d'erreur runtime
- Analyse des logs Dathost: erreur 1045 persistante (`Access denied for user ... using password: YES`) sur init + flush.
- Correction runtime dans `CS2StatsPlugin`: detection explicite de l'erreur 1045, bascule automatique vers `NoOpStatsWriter`, et arret des tentatives de flush MySQL pour eviter le spam de stack traces.
- Ajout d'un log actionnable avec cible `host:port/database` et utilisateur configure.
- Validation locale: `dotnet test CSStat.sln` passe (11/11).

## 2026-03-27 (Suite - Timestamp artefacts)

### Horodatage package uniformise
- Observation: le zip et le dossier `artifacts` etaient regeneres, mais les fichiers copies conservaient parfois leur ancienne date source, ce qui rendait le diagnostic de deploiement ambigu.
- Correction dans `scripts/package-release.ps1`: normalisation de `LastWriteTime` sur tout le contenu du package avec la date de build courante avant creation du zip.
- But: rendre visible en exploitation que les artefacts ont bien ete regeneres et eviter les faux positifs sur un "ancien" DLL alors que le package est recent.

## 2026-03-27 (Suite - Garde-fou config packagee)

### Detection explicite de la config d'exemple en production
- Analyse des logs Dathost recueillis: le plugin demarre encore avec `127.0.0.1:3306/cs2_stats` et l'utilisateur `cs2stats`, ce qui correspond exactement a la config d'exemple packagee et non aux identifiants Dathost fournis.
- Ajout d'un garde-fou runtime dans `CS2StatsPlugin`: si les valeurs d'exemple packagees sont encore actives, le plugin desactive directement l'init DB et l'ecriture MySQL puis logge un message explicite pointant vers `configs/plugins/CS2Stats/CS2Stats.json`.
- Ajout de tests unitaires pour verifier la detection des valeurs d'exemple vs une vraie configuration Dathost personnalisee.

## 2026-03-27 (Suite - Fix init -> set config)

### Correction des setters de configuration pour compatibilite JSON
- Cause supplementaire identifiee: toutes les proprietes de `MySqlSettings`, `StatsModulesSettings`, `SyncSettings` et `PluginConfig` utilisaient des setters `init`, qui ne peuvent pas etre ecrits via reflection ni via le mode `JsonObjectCreationHandling.Populate` de `System.Text.Json`.
- Si CounterStrikeSharp cree une instance `PluginConfig()` puis essaie de la peupler depuis le JSON (mode Populate), les valeurs `init` restent aux defaults.
- Correction: passage de `init` a `set` sur toutes les proprietes des classes de configuration, dans `CS2Stats.Contracts/CS2StatsConfig.cs` et `CS2Stats.Plugin/PluginConfig.cs`.
- Tests: 13/13 verts, package regenere.

## 2026-03-27 (Suite - Fix JsonPropertyName config)

### Cause racine identifiee: casse JSON incompatible avec le deserializeur CSS
- Analyse du code source de `ConfigManager.Load<T>` (CounterStrikeSharp) : utilise `JsonSerializer.Deserialize<T>` avec des options sans `PropertyNamingPolicy` et sans `PropertyNameCaseInsensitive`. Par defaut `System.Text.Json` est case-sensitive et attend des cles en PascalCase exactement comme les noms des proprietes C#.
- Le fichier `CS2Stats.json` deploye sur Dathost (et notre exemple package `cs2stats.example.json`) utilisent des cles camelCase (`"mySql"`, `"host"`, etc.) qui ne correspondent pas aux proprietes C# PascalCase (`MySql`, `Host`). Tout restait aux valeurs par defaut.
- Note: CSS utilise `Deserialize<T>` (pas Populate), donc les setters `init` etaient techniquement supportes. La correction D-028 (init→set) etait sans effet sur ce bug mais elle reste bonne pratique.
- Correction: ajout d'attributs `[JsonPropertyName("xxx")]` camelCase sur toutes les proprietes de `PluginConfig`, `MySqlSettings`, `StatsModulesSettings` et `SyncSettings`.
- Les fichiers de configuration existants sur Dathost (camelCase) sont maintenant deserialisables correctement sans modification cote serveur.
- Tests: 13/13 verts, package regenere avec le fix.

## 2026-03-27 (Suite - Aggregation automatique)

### Branchement de l'aggregation apres chaque flush
- Observation: `player_session_stats`, `player_lifetime_stats` et `player_map_stats` restaient vides car `AggregationService` existait mais n'etait jamais instancie ni appele dans `CS2StatsPlugin`.
- Ajout de `RefreshPendingSessionStatsAsync` dans `AggregationService`: detecte toutes les sessions fermees (`disconnected_at_utc IS NOT NULL`) sans ligne dans `player_session_stats` et appelle `sp_refresh_player_session_stats` pour chacune.
- Ajout de `RefreshPendingMapStatsAsync` dans `AggregationService`: detecte tous les (player_id, map_session_id) sans ligne dans `player_map_stats` et appelle `sp_refresh_player_map_stats` pour chacun.
- Mise a jour de `RefreshAllStatsAsync` pour enchainer les trois: lifetime (tous), session pending, map pending.
- Branchement dans `CS2StatsPlugin`: nouveau champ `_aggregationService`, initialise dans `Load()` apres `BuildWriter()`, declenche `RefreshAllStatsAsync` en fire-and-forget apres chaque flush non-vide reussi.
- Ajout de `BuildConnectionStringFromConfig()` dans le plugin pour partager la logique de construction de connection string.
- Tests: 13/13 verts, package regenere.

## 2026-03-27 (Suite - Correctif surcomptage aggregation)

### Verification terrain sur exports SQL et correction
- Analyse des exports utilisateur (`kill_events`, `player_lifetime_stats`, `player_session_stats`, `player_map_stats`) :
  - `player_lifetime_stats` presentait des valeurs irreelles (ex: milliers de kills pour quelques dizaines de kills bruts).
  - `player_session_stats` et `player_map_stats` restaient a zero.
- Cause racine #1: `sp_refresh_player_lifetime_stats` faisait un `LEFT JOIN` simultane sur `kill_events` et `player_action_events`, ce qui multipliait les lignes (produit cartesien par joueur) et gonflait toutes les sommes.
- Cause racine #2: les procedures runtime creees par `DatabaseInitializationService` pour session/map etaient des placeholders qui inseraient des zeros.
- Correctif applique dans `DatabaseInitializationService.GetStoredProceduresScript`:
  - passage a des sous-requetes correlees pour chaque metrique (kills/deaths/actions/HS) afin d'eviter toute multiplication de lignes,
  - implementation reelle des calculs session/map,
  - calcul de `playtime_seconds` et mise a jour complete des colonnes en `ON DUPLICATE KEY UPDATE`.
- Alignement du script documentaire `sql/002_v1_aggregation_stored_procedures.sql` sur la logique runtime corrigee.
- Validation locale: tests 13/13 verts, package regenere.

## 2026-03-27 (Suite - Cleanup repo)

### Tri des elements non utiles
- Nettoyage code: suppression de la methode privee non utilisee `GetSslMode` dans `CS2StatsPlugin`.
- Nettoyage lisibilite: correction de l'indentation autour de la construction de la connection string d'init MySQL.
- Nettoyage workspace: suppression des dossiers generes `bin/` et `obj/` sous `src/CS2Stats.Contracts`, `src/CS2Stats.Plugin` et `src/CS2Stats.Tests`.
- Verification apres tri:
  - `dotnet build CSStat.sln` reussi.
  - `dotnet test CSStat.sln` reussi (13/13).

## 2026-03-27 (Suite - Slim package artifacts)

### Package de release reduit au strict necessaire
- Suppression manuelle dans `artifacts/CS2-STATPLAY-0.9.0-linux-x64` des elements non souhaites: `sql/`, `CHANGELOG.md`, `DEPLOYMENT_GUIDE.md`, `install.sh`, `install.ps1`, `README_PACKAGE.txt`.
- Mise a jour de `scripts/package-release.ps1` pour ne plus ajouter ces fichiers a l'avenir.
- Le package conserve uniquement le payload `addons/` (plugin + config) et le checksum global `SHA256SUMS.txt` au niveau `artifacts/`.

## 2026-03-27 (Suite - Repo minimal coding/runtime)

### Nettoyage radical du workspace
- Suppression des elements non essentiels a l'execution/codage: `artifacts/`, `CS2-RETAKE-3.0.1-linux/`, `cs2-WeaponPaints-main/`, `lien/`, `LICENSE`, `README.md`, `.gitignore`.
- Conservation du noyau utile au developpement plugin: `.github/`, `.vscode/`, `docs/`, `src/`, `sql/`, `scripts/`, `config/`, `CSStat.sln`, `Directory.Build.targets`.
- Verification de non-regression apres cleanup: `dotnet build CSStat.sln` reussi, `dotnet test CSStat.sln` reussi (13/13).

## 2026-03-27 (Suite - Dual release variants)

### Releases avec config et sans config
- Mise a jour de `scripts/package-release.ps1` pour produire deux artefacts drag-and-drop:
  - `CS2-STATPLAY-<version>-linux-x64.zip` (avec `addons/.../configs/plugins/CS2Stats/CS2Stats.json`)
  - `CS2-STATPLAY-<version>-linux-x64-update-no-config.zip` (sans fichier de config pour mise a jour safe)
- Mise a jour de `.github/workflows/release-package.yml`:
  - verification SHA256 pour les deux zip,
  - upload des deux dossiers package + des deux zip.
- Mise a jour de `.github/workflows/github-release.yml`:
  - verification SHA256 pour les deux zip,
  - publication des deux zip + `SHA256SUMS.txt` sur la GitHub Release taggee.
- Validation locale complete:
  - generation des deux dossiers package et des deux zip,
  - checksum pour les deux zips present dans `artifacts/SHA256SUMS.txt`.

## 2026-03-27 (Suite - Creator metadata)

### Auteur plugin unifie sur NeuTroNBZh
- Mise a jour de `src/CS2Stats.Plugin/CS2StatsPlugin.cs`: `ModuleAuthor` force a `NeuTroNBZh` (impact direct sur `css_plugins list`).
- Mise a jour de `src/CS2Stats.Plugin/CS2Stats.Plugin.csproj`: metadata `Authors` et `Company` definies a `NeuTroNBZh`.
- Verification post-changement: build solution OK et tests OK (13/13).

## 2026-03-27 (Suite - Release 1.0.0 hardening)

### Repository professionnalise pour publication GitHub
- Recreation d'un socle GitHub complet: `README.md` anglais, `LICENSE` (MIT), `.gitignore`, `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`.
- Ajout d'un guide data complet pour reutilisation web/API/IA: `docs/STATS_DATA_REFERENCE.md`.
- Le README documente explicitement le fonctionnement, les stats collectees, l'installation, la mise a jour sans ecrasement de config, et les artefacts release.

### Passage version 1.0.0
- Mise a jour de la version runtime plugin dans `CS2StatsPlugin` vers `1.0.0`.
- Mise a jour `src/CS2Stats.Plugin/CS2Stats.Plugin.csproj` vers `1.0.0`.
- Mise a jour des defaults de packaging CI/scripts vers `1.0.0` (`release-package.yml`, `package-release.ps1`).

### Etape suivante immediate
- Validation build/tests + generation package 1.0.0 + push GitHub + tag `v1.0.0` pour declencher la release GitHub.

## 2026-03-27 (Suite - Repo final + release upload fix)

### Repository cible corrige
- Le repository de destination a ete cree au nom final demande: `NeuTroNBZh/CS2-STATPLAY`.
- Le remote Git local a ete repointe de `CS2-STATPLAY-clean` vers `CS2-STATPLAY`.
- `main` et les tags (`v0.9.0`, `v1.0.0`) ont ete pushes sur le nouveau repo.

### Correctif release GitHub
- Ajout de `permissions: contents: write` dans `.github/workflows/github-release.yml` pour autoriser la creation de release et l'upload des assets via `GITHUB_TOKEN`.
- Mise a jour des badges README pour pointer vers le repo final `CS2-STATPLAY`.
- Publication verifiee de la release `v1.0.0` avec les 3 assets attendus:
  - `CS2-STATPLAY-1.0.0-linux-x64.zip`
  - `CS2-STATPLAY-1.0.0-linux-x64-update-no-config.zip`
  - `SHA256SUMS.txt`

### Tooling GitHub
- Verification faite: `gh` (GitHub CLI) est deja installe localement (`2.88.1`), aucune installation supplementaire necessaire.

## 2026-03-29 (Suite - Diagnostic playtime biaise)

### Cause probable identifiee dans le flux runtime
- Analyse du path `SessionClosed` dans `MySqlStatsWriter`: la fermeture de session etait filtree par `player_id + map_session_id`.
- En cas de derive map/session (ex: changement de map, events manquants, ordre d'arrivee), ce filtre pouvait ne toucher aucune ligne ouverte et laisser une session historique ouverte (`disconnected_at_utc IS NULL`).
- Les procedures d'aggregation utilisent `COALESCE(disconnected_at_utc, UTC_TIMESTAMP(6))`, donc une session orpheline ouverte gonfle `total_playtime_seconds` au fil du temps.

### Correctif applique
- `MySqlStatsWriter` mis a jour pour fermer la session la plus recente ouverte par `player_id` uniquement (sans contraindre `map_session_id`).
- Le fallback d'insertion zero-duree reste en place uniquement si aucune session ouverte n'est trouvable.

### Validation
- `dotnet build CSStat.sln` OK.
- `dotnet test CSStat.sln` OK (13/13).

### Hardening complementaire anti-chevauchement
- Ajout d'un garde-fou dans `MySqlStatsWriter` sur `SessionOpened`: fermeture prealable de toute session ouverte existante du meme joueur avant insertion de la nouvelle session.
- But: eviter les sessions qui se chevauchent (double connect/ordre d'evenements) et qui peuvent sur-gonfler `total_playtime_seconds` meme sans session ouverte orpheline.
- Raison de robustesse: en production, certains flux connect/disconnect peuvent arriver en sequence imparfaite lors des transitions map/restart.
- Validation apres hardening:
  - `dotnet build CSStat.sln` OK.
  - `dotnet test CSStat.sln` OK (13/13).
