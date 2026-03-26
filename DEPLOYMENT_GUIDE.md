# CS2 Stats Plugin V1 - Deployment Guide

## Overview

The **CS2 Stats** plugin captures Counter-Strike 2 server and player statistics in real-time, storing them in MySQL for analysis. This guide covers installation, configuration, database setup, and operational procedures.

---

## Prerequisites

- **Counter-Strike 2 Server** running with CounterStrikeSharp support
- **MySQL 8.0+** server accessible from the CS2 server
- **.NET Runtime 8.0+** (if building from source)
- **Basic SQL knowledge** (for schema management)

---

## Step 1: Database Setup

### 1.1 Create the Database

Connect to your MySQL server and create the stats database:

```sql
CREATE DATABASE cs2stats CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE cs2stats;
```

### 1.2 Import Base Schema

Execute the baseline schema script to create all required tables:

```bash
mysql -u <user> -p<password> -h <host> cs2stats < sql/001_v1_baseline_schema.sql
```

### 1.3 Import Aggregation Procedures

Import the stored procedures for statistics aggregation:

```bash
mysql -u <user> -p<password> -h <host> cs2stats < sql/002_v1_aggregation_stored_procedures.sql
```

These two SQL files are also included inside the packaged release under `sql/`.

### 1.4 Verify Schema

Confirm that all tables and procedures are created:

```sql
USE cs2stats;
SHOW TABLES;
SHOW PROCEDURE STATUS WHERE Db = 'cs2stats';
```

Expected tables:
- `players`
- `map_sessions`
- `player_sessions`
- `rounds`
- `kill_events`
- `player_action_events`
- `presence_snapshots`
- `presence_snapshot_players`
- `player_lifetime_stats`
- `player_session_stats`
- `player_map_stats`

Expected procedures:
- `sp_refresh_player_lifetime_stats`
- `sp_refresh_player_session_stats`
- `sp_refresh_player_map_stats`

---

## Step 2: Plugin Installation

### 2.1 Build the Plugin

From the project root:

```bash
pwsh ./scripts/package-release.ps1
```

Output:
- `artifacts/CS2-STATPLAY-0.9.0-linux-x64/`
- `artifacts/CS2-STATPLAY-0.9.0-linux-x64.zip`

Optional custom version:

```bash
pwsh ./scripts/package-release.ps1 -Version 1.0.0
```

The generated folder is already laid out for a CounterStrikeSharp server:

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

If you run `dotnet build src/CS2Stats.Plugin/CS2Stats.Plugin.csproj -c Release`, the package is also generated automatically.

On Linux, you can also run the packaged installer:

```bash
chmod +x install.sh
./install.sh /path/to/cs2-server-root
```

On Windows PowerShell, you can also run:

```powershell
./install.ps1 -ServerRoot C:\path\to\cs2-server
```

### 2.2 Deploy to Server

Copy or extract the generated package at the root of your CS2 server:

```bash
# Example for Linux CS2 server
unzip artifacts/CS2-STATPLAY-0.9.0-linux-x64.zip -d /path/to/server/
```

If you prefer, you can also copy the contents of `artifacts/CS2-STATPLAY-0.9.0-linux-x64/` manually.

---

## Step 3: Configuration

### 3.1 Create Configuration File

The package already includes a starter config here:

```text
addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json
```

### 3.2 Edit Configuration

Edit `CS2Stats.json` with your MySQL connection details:

```json
{
  "MySQL": {
    "Host": "127.0.0.1",
    "Port": 3306,
    "Database": "cs2stats",
    "User": "cs2stats_user",
    "Password": "secure_password_here",
    "SslMode": "None"
  },
  "StatsModules": {
    "CaptureKDA": true,
    "CaptureHeadshots": true,
    "CaptureEquipmentStats": true,
    "CaptureActionEvents": true,
    "CapturePresence": true
  },
  "SyncSettings": {
    "BatchFlushIntervalSeconds": 15,
    "PresenceSnapshotIntervalSeconds": 10
  }
}
```

**Configuration Options:**

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `MySQL.Host` | string | `127.0.0.1` | MySQL server hostname or IP |
| `MySQL.Port` | int | `3306` | MySQL port |
| `MySQL.Database` | string | `cs2stats` | Database name |
| `MySQL.User` | string | - | MySQL username |
| `MySQL.Password` | string | - | MySQL password |
| `MySQL.SslMode` | string | `None` | SSL mode: `None`, `Required`, `Preferred` |
| `StatsModules.CaptureKDA` | bool | `true` | Enable K/D/A tracking |
| `StatsModules.CaptureHeadshots` | bool | `true` | Enable headshot tracking |
| `StatsModules.CaptureEquipmentStats` | bool | `true` | Enable weapon/equipment stats |
| `StatsModules.CaptureActionEvents` | bool | `true` | Enable grenade/bomb events |
| `StatsModules.CapturePresence` | bool | `true` | Enable real-time player presence |
| `SyncSettings.BatchFlushIntervalSeconds` | int | `15` | Event buffer flush interval |
| `SyncSettings.PresenceSnapshotIntervalSeconds` | int | `10` | Online player snapshot interval |

### 3.3 Secure MySQL User (Recommended)

Create a dedicated MySQL user with minimal privileges:

```sql
CREATE USER 'cs2stats_user'@'127.0.0.1' IDENTIFIED BY 'secure_password_here';
GRANT SELECT, INSERT, UPDATE ON cs2stats.* TO 'cs2stats_user'@'127.0.0.1';
GRANT EXECUTE ON PROCEDURE cs2stats.sp_refresh_player_lifetime_stats TO 'cs2stats_user'@'127.0.0.1';
GRANT EXECUTE ON PROCEDURE cs2stats.sp_refresh_player_session_stats TO 'cs2stats_user'@'127.0.0.1';
GRANT EXECUTE ON PROCEDURE cs2stats.sp_refresh_player_map_stats TO 'cs2stats_user'@'127.0.0.1';
FLUSH PRIVILEGES;
```

---

## Step 4: Start and Verify

### 4.1 Start CS2 Server

Start the CS2 server. The plugin should load automatically:

```bash
./cs2 -dedicated -console -port 27015 +map de_mirage
```

### 4.2 Check Plugin Logs

Monitor the server console for plugin initialization:

```
[CS2Stats] Plugin loaded successfully
[CS2Stats] MySQL connection validated
[CS2Stats] Batch flush timer started: interval=15s
[CS2Stats] Presence snapshot timer started: interval=10s
```

### 4.3 Verify Database Activity

After players connect, check for data in MySQL:

```sql
USE cs2stats;

-- Check players
SELECT COUNT(*) FROM players;

-- Check events
SELECT COUNT(*) FROM kill_events;
SELECT COUNT(*) FROM player_action_events;

-- Check presence snapshots
SELECT COUNT(*) FROM presence_snapshots;
```

---

## Step 5: Monitor and Maintain

### 5.1 Real-Time Monitoring

#### Check Connected Players

```sql
SELECT ps.*, p.steam_id64
FROM presence_snapshots ps
JOIN presence_snapshot_players psp ON ps.presence_snapshot_id = psp.presence_snapshot_id
JOIN players p ON psp.player_id = p.player_id
WHERE ps.map_session_id = (SELECT MAX(map_session_id) FROM map_sessions)
ORDER BY ps.captured_at_utc DESC
LIMIT 1;
```

#### Check Live K/D/A

```sql
SELECT
    p.steam_id64,
    COALESCE(SUM(CASE WHEN ke.attacker_player_id = p.player_id THEN 1 ELSE 0 END), 0) AS kills,
    COALESCE(SUM(CASE WHEN ke.victim_player_id = p.player_id THEN 1 ELSE 0 END), 0) AS deaths,
    COALESCE(SUM(CASE WHEN ke.assister_player_id = p.player_id THEN 1 ELSE 0 END), 0) AS assists
FROM players p
LEFT JOIN kill_events ke ON ke.attacker_player_id = p.player_id OR ke.victim_player_id = p.player_id OR ke.assister_player_id = p.player_id
JOIN player_sessions ps ON ps.player_id = p.player_id
WHERE ps.disconnected_at_utc IS NULL
GROUP BY p.player_id;
```

### 5.2 Manual Aggregation Refresh

Update player stats by calling aggregation procedures:

```sql
-- Refresh all players' lifetime stats
CALL sp_refresh_player_lifetime_stats(NULL);

-- Refresh specific player (by player_id)
CALL sp_refresh_player_lifetime_stats(1);

-- Refresh session stats
CALL sp_refresh_player_session_stats(<player_session_id>);

-- Refresh map stats
CALL sp_refresh_player_map_stats(<player_id>, <map_session_id>);
```

### 5.3 Performance Maintenance

#### Index Usage

Key indexes are created on:
- `players.steam_id64` (unique lookup)
- `kill_events.attacker_player_id / victim_player_id` (stat queries)
- `player_sessions.player_id, map_session_id` (session tracking)

#### Regular Maintenance

```sql
-- Optimize tables after heavy data collection
OPTIMIZE TABLE kill_events;
OPTIMIZE TABLE player_action_events;
OPTIMIZE TABLE presence_snapshots;

-- Check table sizes
SELECT table_name, ROUND(((data_length + index_length) / 1024 / 1024), 2) AS size_mb
FROM information_schema.tables
WHERE table_schema = 'cs2stats'
ORDER BY size_mb DESC;
```

---

## Troubleshooting

### Issue: Plugin fails to load

**Symptom:**  
Plugin DLL not found or load error in console.

**Solution:**
1. Verify DLL files are in the plugins directory
2. Check that .NET Runtime 8.0+ is installed on the server
3. Ensure all dependencies (MySqlConnector, CounterStrikeSharp) are available

### Issue: "MySQL connection failed"

**Symptom:**  
`[CS2Stats] Failed to initialize MySQL writer: Connection refused`

**Solution:**
1. Verify MySQL server is running: `telnet <host> 3306`
2. Check credentials in `cs2stats.json`
3. Confirm firewall allows port 3306 from CS2 server
4. Test connection locally: `mysql -u <user> -p -h <host> -e "SELECT 1"`

### Issue: Data not persisting

**Symptom:**  
No data appears in MySQL tables after players connect.

**Solution:**
1. Check plugin logs for errors
2. Verify MySQL user has `INSERT` permissions on `cs2stats.*`
3. Confirm table foreign keys are not blocking inserts
4. Check for duplicate key violations: `SELECT * FROM kill_events LIMIT 1`

### Issue: Aggregation performance slow

**Symptom:**  
Sp_refresh_player_lifetime_stats takes > 10 seconds.

**Solution:**
1. Build indexes on frequently queried columns
2. Archive old data to separate tables
3. Run aggregation procedures during low-traffic hours
4. Use `EXPLAIN` to analyze slow queries:
   ```sql
   EXPLAIN CALL sp_refresh_player_lifetime_stats(NULL)
   ```

---

## Operational Procedures

### Daily Tasks

- Monitor database disk space
- Check plugin logs for errors
- Verify data integrity: `COUNT(*) FROM kill_events` should grow steadily

### Weekly Tasks

- Run `OPTIMIZE TABLE` on large tables
- Export stats reports for analysis
- Backup database: `mysqldump -u <user> -p cs2stats > backup.sql`

### Monthly Tasks

- Review and archive old data (> 90 days)
- Analyze player trends from `player_lifetime_stats`
- Plan storage growth

---

## Performance Tuning

### MySQL Configuration

Add to `my.cnf` for better performance:

```ini
[mysqld]
max_connections = 200
innodb_buffer_pool_size = 2G
innodb_log_file_size = 512M
slow_query_log = 1
long_query_time = 2
```

### Plugin Tuning

Adjust sync intervals based on load:

```json
{
  "SyncSettings": {
    "BatchFlushIntervalSeconds": 30,      # Increase for lower CPU usage
    "PresenceSnapshotIntervalSeconds": 20 # Increase for fewer snapshots
  }
}
```

---

## Rollback and Cleanup

### Disable Plugin

Remove the DLL and restart server:

```bash
rm /path/to/cs2/plugins/CS2Stats.Plugin.dll
```

### Cleanup Database

If needed, drop the database:

```sql
DROP DATABASE cs2stats;
```

---

## Support and Logging

### Enable Verbose Logging

Set environment variable before starting server:

```bash
export DOTNET_ENVIRONMENT=Development
./cs2 -dedicated -console ...
```

### Log Locations

- **CS2 Console**: Real-time plugin messages
- **MySQL Slow Query Log**: Queries taking > 2 seconds
- **InnoDB Error Log**: Connection and constraint errors

---

## Next Steps

After deployment:

1. **Load Test**: Run a 10-20 player match to verify data collection
2. **Query Reports**: Build custom dashboards using aggregated stats
3. **Extend V2**: Add skill ratings, team balancing, advanced analytics

---

**Version**: 1.0  
**Last Updated**: 2026-03-26  
**Compatibility**: CounterStrikeSharp 1.x, MySQL 8.0+, .NET 8.0+
