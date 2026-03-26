# Guide Rapide Dathost CS2: Installation et Test CS2-STATPLAY

Ce guide est volontairement simple pour valider vite si le plugin marche sur un serveur Dathost.

## 1. Prerequis

- Serveur CS2 Dathost actif
- CounterStrikeSharp deja installe sur le serveur
- Une base MySQL accessible depuis le serveur
- Le zip release: `CS2-STATPLAY-0.9.0-linux-x64.zip`

## 2. Preparer la base MySQL

1. Cree une base de donnees (ex: `cs2stats`).
2. Importe les scripts SQL du package:
   - `sql/001_v1_baseline_schema.sql`
   - `sql/002_v1_aggregation_stored_procedures.sql`
3. Verifie que les tables existent (`players`, `kill_events`, `player_sessions`, `presence_snapshots`, etc.).

## 3. Installer le plugin sur Dathost

1. Dezippe `CS2-STATPLAY-0.9.0-linux-x64.zip` en local.
2. Ouvre le file manager Dathost (ou FTP/SFTP).
3. Upload le dossier `addons` a la racine du serveur.
4. Verifie ces chemins sur le serveur:
   - `addons/counterstrikesharp/plugins/CS2Stats`
   - `addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json`

## 4. Configurer le plugin

Edite:

- `addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json`

Renseigne au minimum:

- `mySql.host`
- `mySql.port`
- `mySql.database`
- `mySql.username`
- `mySql.password`
- `mySql.sslRequired`

Pour un premier test, laisse les modules actifs et les intervalles par defaut.

## 5. Redemarrer et verifier le chargement

1. Redemarre le serveur Dathost.
2. Ouvre les logs serveur.
3. Verifie:
   - pas d'erreur de chargement plugin
   - pas d'erreur de connexion MySQL

## 6. Procedure de test en jeu (10 a 15 minutes)

### Test A: Presence online

1. Connecte 2 joueurs.
2. Attendu: des lignes sont inserees dans `presence_snapshots` et `presence_snapshot_players`.

### Test B: Kills et headshots

1. Fais plusieurs kills.
2. Fais au moins un headshot.
3. Attendu: `kill_events` se remplit, avec le headshot marque.

### Test C: Actions objectif/stuff

1. Plante/defuse une bombe.
2. Lance des grenades.
3. Attendu: `player_action_events` se remplit.

### Test D: Session joueur

1. Un joueur se connecte.
2. Joue quelques rounds.
3. Le joueur se deconnecte.
4. Attendu: `player_sessions` contient connect/disconnect.

## 7. Requetes SQL rapides de verification

```sql
SELECT COUNT(*) AS players_count FROM players;
SELECT COUNT(*) AS kill_events_count FROM kill_events;
SELECT COUNT(*) AS action_events_count FROM player_action_events;
SELECT * FROM presence_snapshots ORDER BY captured_at_utc DESC LIMIT 5;
```

## 8. Diagnostic rapide si rien ne remonte

- Verifie host/port MySQL
- Verifie droits du user SQL
- Verifie que les scripts SQL ont bien ete importes
- Verifie que le plugin est present dans le bon dossier
- Verifie les logs au redemarrage serveur

## 9. Validation finale

Le test est considere OK si:

- le plugin charge sans erreur
- des kills sont enregistres
- des actions sont enregistrees
- des snapshots de presence sont enregistres
- des sessions joueurs sont enregistrees

Tu peux ensuite passer en exploitation normale.