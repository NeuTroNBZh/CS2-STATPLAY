# Decision Log

## 2026-03-26

### D-001: Workspace Agent Location
- Status: accepted
- Decision: stocker l'agent personnalise dans `.github/agents` pour qu'il soit partage au niveau du workspace.
- Reason: emplacement standard detecte par VS Code pour les agents personnalises.

### D-002: Journal Set
- Status: accepted
- Decision: utiliser au minimum `architecture.md`, `worklog.md`, `sources.md` et `decisions.md`.
- Reason: couvre les besoins exprimes par l'utilisateur: architecture, actions realisees, documentation consultee et arbitrages techniques.

### D-003: Documentation Priority
- Status: accepted
- Decision: utiliser la documentation CounterStrikeSharp comme source prioritaire avant GitHub et autres sources web.
- Reason: exigence explicite de la demande et meilleure fiabilite pour valider les capacites du plugin.

### D-004: V1 Statistics Scope
- Status: accepted
- Decision: prioriser en V1 le temps de jeu, K/D/A, les stats de stuff/equipement, les joueurs connectes en temps reel, l'historique par map/match, et les headshots si ces donnees sont confirmees techniquement.
- Reason: correspond au besoin utilisateur et fixe un perimetre exploitable pour les prochaines etapes.

### D-005: Persistence Levels
- Status: accepted
- Decision: concevoir la base MySQL pour supporter un resume global par joueur, un historique par session de jeu et un historique par match/map.
- Reason: besoin explicite de conserver a la fois des agregats et de l'historique detaille.

### D-006: Configuration Scope
- Status: accepted
- Decision: exposer au minimum la connexion MySQL, l'activation/desactivation des modules de stats et l'intervalle de synchronisation.
- Reason: couvre le besoin de configurabilite tout en restant simple pour une V1.

### D-007: Separate Research Agent
- Status: accepted
- Decision: creer un agent distinct de recherche, sans outils d'edition ni d'execution, pour verifier la disponibilite reelle des statistiques avant implementation.
- Reason: reduit le risque d'inventer des capacites CounterStrikeSharp et force une phase de validation sourcee avant codage.

### D-008: Workspace Instruction Scope
- Status: accepted
- Decision: ajouter un fichier `*.instructions.md` workspace pour encadrer l'implementation C#, la conception MySQL, la configuration et la journalisation.
- Reason: complete les agents avec des regles transverses reappliquees pendant les taches de code et de schema.

### D-009: V1 Step Order After Feasibility Matrix
- Status: accepted
- Decision: apres validation de faisabilite des stats, implementer d'abord le socle de persistance et les contrats internes (SQL + DTO/config) avant de coder la capture event CounterStrikeSharp.
- Reason: reduit le risque d'incoherence entre events et stockage, garantit la separation raw counters / derivees, et permet une implementation incrementalement verifiable.

### D-010: V1 Match History Reliability Policy
- Status: accepted
- Decision: en V1, considerer l'historique map/round comme niveau fiable de base; traiter l'historique match complet comme module optionnel tant que les frontieres de match ne sont pas confirmees de bout en bout.
- Reason: aligne la promesse produit avec les preuves disponibles et evite de sur-interpreter des signaux partiels.

### D-011: Buffered Capture + Async Flush Boundary
- Status: accepted
- Decision: capter les events CS2 dans un service de capture bufferise (`StatsCaptureService`) puis flusher periodiquement via une abstraction `IStatsWriter`.
- Reason: eviter toute I/O synchrone dans les handlers de jeu, isoler la persistence (MySQL) et permettre une evolution incrementalement testable.

### D-012: Presence Snapshot Timer
- Status: accepted
- Decision: collecter la presence en ligne par timer dedie en utilisant `Utilities.GetPlayers()` plutot que de la deduire uniquement des events connect/disconnect.
- Reason: fournir un etat temps reel robuste (compte + identite) meme en cas de desynchronisation ponctuelle des events.

### D-013: Writer Selection Strategy (MySQL with Fallback)
- Status: accepted
- Decision: initialiser `MySqlStatsWriter` quand la config MySQL est exploitable; sinon fallback automatique vers `NoOpStatsWriter`.
- Reason: demarrer le plugin sans crash en environnement partiellement configure, tout en conservant la capture d'evenements.

### D-014: Release Artifact Shape
- Status: accepted
- Decision: produire un package de release versionne avec un dossier pret a deployer et un zip contenant l'arborescence CounterStrikeSharp complete du plugin.
- Reason: simplifier l'installation sur les serveurs retake et eviter les copies manuelles de DLL/config disperses.

### D-015: Package Identity And Automation
- Status: accepted
- Decision: standardiser l'identite du package de release en `CS2-STATPLAY-<version>-linux-x64`, inclure les scripts SQL dans le bundle, et automatiser la generation via build Release et GitHub Actions.
- Reason: fournir un artefact unique, auto-suffisant et repetable pour les deployements serveur et les futures releases.

### D-016: Linux Installer And Tagged Release
- Status: accepted
- Decision: ajouter un script `install.sh` dans le package et un workflow GitHub dedie aux tags pour publier automatiquement le zip sur une GitHub Release.
- Reason: simplifier l'installation cote serveur Linux et la distribution des versions publiees.

### D-017: Dynamic Versioned Releases And Windows Installer
- Status: accepted
- Decision: deriver la version des releases GitHub depuis le tag `v*` et ajouter un `install.ps1` au package en plus de `install.sh`.
- Reason: eviter les noms d'artefacts figes et couvrir les environnements d'administration Windows.

### D-018: Release Integrity And Changelog
- Status: accepted
- Decision: generer un `SHA256SUMS.txt` pour le zip et inclure un `CHANGELOG.md` minimal directement dans le package.
- Reason: faciliter la verification d'integrite et la comprehension rapide du contenu de la release.

### D-019: Checksum Verification In CI
- Status: accepted
- Decision: verifier en workflow le SHA256 du zip avant upload d'artefact ou publication GitHub Release.
- Reason: eviter de publier un artefact corrompu ou desynchronise par rapport au checksum distribue.

### D-020: Dathost First Validation Path
- Status: accepted
- Decision: fournir un guide operationnel simplifie cible Dathost pour valider rapidement le plugin en conditions reelles.
- Reason: accelerer la boucle de verification terrain avant extension fonctionnelle V2.

### D-021: Robust SQL Script Splitting
- Status: accepted
- Decision: remplacer le decoupage SQL base sur `;` par un decoupage qui respecte `DELIMITER` pour l'auto-initialisation MySQL.
- Reason: les procedures stockees ne peuvent pas etre executees correctement avec un split naif, contrairement aux simples `CREATE TABLE` du plugin de reference.

### D-022: Install Path Must Target game/csgo
- Status: accepted
- Decision: documenter et automatiser l'installation du package vers `game/csgo/addons` plutot que vers une racine serveur generique.
- Reason: le symptome principal en production est un non-chargement silencieux du plugin quand `addons` est copie trop haut dans l'arborescence.

### D-023: Plugin Assembly Name Must Match Folder Name
- Status: accepted
- Decision: aligner le nom d'assembly principal sur `CS2Stats` pour respecter la convention CounterStrikeSharp `plugins/<Name>/<Name>.dll`.
- Reason: sinon le loader auto ne decouvre pas le plugin et `css_plugins list` ne l'affiche jamais.

### D-024: Continue init when CREATE DATABASE is denied
- Status: accepted
- Decision: en cas de refus `CREATE DATABASE`, tenter l'acces a la base deja configuree et poursuivre la creation des tables/procedures si l'acces est valide.
- Reason: compatibilite avec les environnements manages (ex: Dathost) qui imposent un schema pre-cree et limitent les privileges SQL du compte applicatif.

### D-025: Disable MySQL writer on auth failure
- Status: accepted
- Decision: si l'authentification MySQL echoue avec 1045, desactiver l'ecriture MySQL pour la session runtime en cours et logger une erreur actionnable.
- Reason: eviter le spam d'erreurs toutes les 15 secondes et conserver un serveur stable pendant la correction de configuration.

### D-026: Package timestamps should reflect build time
- Status: accepted
- Decision: forcer les timestamps des fichiers contenus dans `artifacts/` a la date de generation du package.
- Reason: le package doit etre diagnostiquable visuellement sans confusion avec les dates de compilation/copie precedentes des fichiers sources.

### D-027: Treat packaged sample MySQL config as a deployment error
- Status: accepted
- Decision: detecter explicitement la signature de la config MySQL d'exemple packagee et desactiver l'init/ecriture MySQL tant que cette signature est active.
- Reason: en exploitation, ce cas signifie presque toujours que le serveur tourne encore avec un fichier `CS2Stats.json` stale ou ecrase par l'exemple package; il faut un log immediat et non ambigu au lieu d'un simple echec 1045.

### D-028: Use set instead of init for config property setters
- Status: accepted
- Decision: changer tous les setters `init` en `set` dans `MySqlSettings`, `StatsModulesSettings`, `SyncSettings` et `PluginConfig`.
- Reason: les setters `init` ne sont pas compatibles avec le mode `JsonObjectCreationHandling.Populate` de `System.Text.Json`, ni avec une deserialization par reflection. Si CounterStrikeSharp cree le `PluginConfig()` puis peuple l'objet depuis le JSON, les proprietes `init` restent aux valeurs par defaut. Le passage a `set` garantit la compatibilite avec tous les modes de deserialization.

### D-029: Add JsonPropertyName attributes to all config properties
- Status: accepted
- Decision: ajouter des attributs `[JsonPropertyName("xxx")]` camelCase sur toutes les proprietes de `PluginConfig`, `MySqlSettings`, `StatsModulesSettings` et `SyncSettings`.
- Reason: le `ConfigManager.Load<T>` de CounterStrikeSharp utilise `JsonSerializer.Deserialize<T>` sans naming policy et en mode case-sensitive (comportement par defaut STJ). Il attend donc les cles JSON exactement en PascalCase (ex: `"MySql"`, `"Host"`). Or tous nos fichiers de configuration (example package et fichier deploye par l'utilisateur) utilisent le camelCase (`"mySql"`, `"host"`). Sans `[JsonPropertyName]`, toutes les valeurs restent aux defaults du constructeur. Les attributs forcent STJ a mapper les cles camelCase JSON vers les proprietes C# PascalCase, quelle que soit la naming policy.

### D-030: Trigger aggregation automatically after each successful flush
- Status: accepted
- Decision: apres chaque `WriteBatchAsync` reussi dans `FlushAsync`, declencher `RefreshAllStatsAsync` en fire-and-forget. `RefreshAllStatsAsync` enchaine: lifetime stats (tous les joueurs), session stats pending (sessions fermees sans ligne agr.), map stats pending (combos player/map sans ligne agr.).
- Reason: sans ce declenchement, `player_session_stats`, `player_lifetime_stats` et `player_map_stats` restaient indefiniment vides. Le pattern "pending" (LEFT JOIN / IS NULL) garantit que seules les lignes manquantes sont calculees, sans surcharger la base sur les maps longues.

### D-031: Use correlated subqueries in aggregation procedures to avoid overcount
- Status: accepted
- Decision: remplacer les aggregations basees sur des `JOIN` simultanes entre `kill_events` et `player_action_events` par des sous-requetes correlees par metrique (kills/deaths/assists/headshots/actions), et remplacer les placeholders zero pour session/map par de vrais calculs.
- Reason: les `JOIN` sur deux tables d'evenements 1-N provoquent une multiplication de lignes et des totaux absurdement eleves. Les sous-requetes correlees garantissent des comptes exacts, lisibles, et robustes avec le volume V1.

### D-032: Cleanup policy after stabilization
- Status: accepted
- Decision: apres stabilisation fonctionnelle, limiter le tri a des suppressions a risque faible (code prive non reference, dossiers generes `bin/obj`) et valider systematiquement par build + tests.
- Reason: repondre a la demande de nettoyage sans introduire de regression sur le pipeline Dathost/MySQL maintenant valide.

### D-033: Slim release package scope
- Status: accepted
- Decision: limiter le contenu du dossier package `artifacts/CS2-STATPLAY-<version>-linux-x64/` au strict necessaire deploiement plugin (`addons/`) et exclure `sql/`, `CHANGELOG.md`, `DEPLOYMENT_GUIDE.md`, `install.sh`, `install.ps1`, `README_PACKAGE.txt`.
- Reason: aligner les artefacts avec le besoin utilisateur de distribution minimale et supprimer les fichiers juges non utiles en environnement cible.

### D-034: Keep only coding/runtime-essential repository content
- Status: accepted
- Decision: appliquer un tri strict du repo pour ne conserver que les elements utiles au plugin et au developpement IA (`.github`, `.vscode`, `docs`, code source, solution/build essentials), et supprimer metadata/contenus externes non indispensables.
- Reason: demande explicite de minimiser le workspace au strict necessaire pour coder et faire fonctionner le plugin.

### D-035: Publish dual release artifacts (with config and update-no-config)
- Status: accepted
- Decision: produire deux variantes de release pretes au glisser-deposer:
	- variante standard avec config (`...linux-x64.zip`) pour installation initiale,
	- variante update sans config (`...linux-x64-update-no-config.zip`) pour mise a jour sans ecraser les identifiants serveur.
- Reason: simplifier les operations d'installation/mise a jour (type Dathost) avec un chemin explicite et sans risque d'ecrasement de config en production.

### D-036: Standardize plugin creator identity to NeuTroNBZh
- Status: accepted
- Decision: utiliser `NeuTroNBZh` comme auteur officiel du plugin dans les metadonnees runtime (affichage `css_plugins list`) et metadonnees projet plugin (`Authors`, `Company`).
- Reason: aligner tous les points d'identification auteur sur une valeur unique demandee par le proprietaire du projet.

### D-037: Release 1.0.0 requires full repository publication baseline
- Status: accepted
- Decision: restaurer un socle de publication GitHub complet (`README.md`, `LICENSE`, `.gitignore`, `CONTRIBUTING.md`, `SECURITY.md`, `CHANGELOG.md`) avant de pousser/tagger `v1.0.0`.
- Reason: la release vise une diffusion publique, pas seulement un repo minimal interne. Il faut donc une base legale, operationnelle et collaborative explicite.

### D-038: Provide a complete SQL data reference for web/API/AI reuse
- Status: accepted
- Decision: ajouter une documentation dediee `docs/STATS_DATA_REFERENCE.md` listant toutes les tables, leur granularite, leurs colonnes, leurs relations, et des requetes exemplaires reutilisables.
- Reason: faciliter la reutilisation des stats en dehors du plugin (site web, API, dashboards, agents IA) sans ambiguite sur le schema.

### D-039: Promote V1 to semantic release 1.0.0
- Status: accepted
- Decision: aligner la version plugin et packaging sur `1.0.0` et conserver le modele de distribution dual-package (normal + update-no-config) comme standard de release.
- Reason: la fonctionnalite V1 demandee est couverte, stabilisee et documentee; la release doit refleter ce niveau de maturite.
