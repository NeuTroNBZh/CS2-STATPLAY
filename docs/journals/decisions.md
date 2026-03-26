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
