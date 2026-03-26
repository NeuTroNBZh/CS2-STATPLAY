using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Text;

namespace CS2Stats.Plugin;

/// <summary>
/// Service to initialize and migrate the CS2Stats database automatically.
/// Creates the database, tables, and stored procedures on first plugin load.
/// </summary>
public sealed class DatabaseInitializationService
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly ILogger _logger;

    public DatabaseInitializationService(string connectionString, string databaseName, ILogger logger)
    {
        _connectionString = connectionString;
        _databaseName = databaseName;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the database: create DB, tables, and procedures if they don't exist.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[CS2Stats] Starting database initialization...");

            // Step 1: Create database if not exists
            await CreateDatabaseAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[CS2Stats] Database ready: {DatabaseName}", _databaseName);

            // Step 2: Create tables
            await CreateTablesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[CS2Stats] Tables initialized");

            // Step 3: Create stored procedures
            await CreateStoredProceduresAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[CS2Stats] Stored procedures created");

            _logger.LogInformation("[CS2Stats] Database initialization completed successfully ✓");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Database initialization failed");
            throw;
        }
    }

    /// <summary>
    /// Create the database if it doesn't exist.
    /// </summary>
    private async Task CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var connectionStringBuilder = new MySqlConnectionStringBuilder(_connectionString)
        {
            Database = "" // Connect to MySQL without specifying a database
        };

        await using var connection = new MySqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{_databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Create all required tables.
    /// </summary>
    private async Task CreateTablesAsync(CancellationToken cancellationToken)
    {
        var schema = GetBaselineSchema();
        await ExecuteSqlScriptAsync(schema, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Create all required stored procedures.
    /// </summary>
    private async Task CreateStoredProceduresAsync(CancellationToken cancellationToken)
    {
        var procedures = GetStoredProceduresScript();
        await ExecuteSqlScriptAsync(procedures, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Execute a SQL script (can contain multiple statements).
    /// Note: This is a simplified version; for production, use proper SQL parsing.
    /// </summary>
    private async Task ExecuteSqlScriptAsync(string script, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        connection.ConnectionString = new MySqlConnectionStringBuilder(_connectionString)
        {
            Database = _databaseName
        }.ConnectionString;

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var statement in SplitSqlScript(script))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = statement;
            cmd.CommandTimeout = 60;

            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException ex) when (ex.Number == 1050) // Table already exists
            {
                // Ignore "table already exists" errors
                _logger.LogDebug("[CS2Stats] Table or object already exists (ignoring): {Error}", ex.Message);
            }
        }
    }

    internal static IReadOnlyList<string> SplitSqlScript(string script)
    {
        var statements = new List<string>();
        using var reader = new StringReader(script);

        var currentDelimiter = ";";
        var builder = new StringBuilder();

        while (reader.ReadLine() is { } rawLine)
        {
            var trimmedLine = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            if (trimmedLine.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmedLine.StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
            {
                currentDelimiter = trimmedLine[10..].Trim();
                continue;
            }

            builder.AppendLine(rawLine);

            if (!trimmedLine.EndsWith(currentDelimiter, StringComparison.Ordinal))
            {
                continue;
            }

            var statement = builder.ToString().Trim();
            statement = statement[..^currentDelimiter.Length].TrimEnd();
            if (!string.IsNullOrWhiteSpace(statement))
            {
                statements.Add(statement);
            }

            builder.Clear();
        }

        var trailing = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(trailing))
        {
            statements.Add(trailing);
        }

        return statements;
    }

    /// <summary>
    /// Get the baseline schema SQL script.
    /// </summary>
    private static string GetBaselineSchema()
    {
        return @"
CREATE TABLE IF NOT EXISTS players (
    player_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    steam_id64 BIGINT UNSIGNED NOT NULL,
    first_seen_utc DATETIME(6) NOT NULL,
    last_seen_utc DATETIME(6) NOT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (player_id),
    UNIQUE KEY uq_players_steam_id64 (steam_id64),
    KEY ix_players_last_seen_utc (last_seen_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS map_sessions (
    map_session_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    map_name VARCHAR(128) NOT NULL,
    started_at_utc DATETIME(6) NOT NULL,
    ended_at_utc DATETIME(6) NULL,
    server_current_time_start DOUBLE NULL,
    server_current_time_end DOUBLE NULL,
    total_rounds INT UNSIGNED NOT NULL DEFAULT 0,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (map_session_id),
    KEY ix_map_sessions_map_started (map_name, started_at_utc),
    KEY ix_map_sessions_started_at_utc (started_at_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS player_sessions (
    player_session_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    player_id BIGINT UNSIGNED NOT NULL,
    map_session_id BIGINT UNSIGNED NOT NULL,
    connected_at_utc DATETIME(6) NOT NULL,
    disconnected_at_utc DATETIME(6) NULL,
    disconnect_reason VARCHAR(255) NULL,
    server_current_time_connect DOUBLE NULL,
    server_current_time_disconnect DOUBLE NULL,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (player_session_id),
    KEY ix_player_sessions_player_connected (player_id, connected_at_utc),
    KEY ix_player_sessions_map_connected (map_session_id, connected_at_utc),
    CONSTRAINT fk_player_sessions_player FOREIGN KEY (player_id)
        REFERENCES players (player_id),
    CONSTRAINT fk_player_sessions_map_session FOREIGN KEY (map_session_id)
        REFERENCES map_sessions (map_session_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS rounds (
    round_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    map_session_id BIGINT UNSIGNED NOT NULL,
    round_number INT UNSIGNED NOT NULL,
    started_at_utc DATETIME(6) NOT NULL,
    ended_at_utc DATETIME(6) NULL,
    end_reason INT NULL,
    end_message VARCHAR(255) NULL,
    player_count_at_end INT NULL,
    round_time_seconds INT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (round_id),
    UNIQUE KEY uq_rounds_map_round_number (map_session_id, round_number),
    KEY ix_rounds_map_started (map_session_id, started_at_utc),
    CONSTRAINT fk_rounds_map_session FOREIGN KEY (map_session_id)
        REFERENCES map_sessions (map_session_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS player_lifetime_stats (
    player_id BIGINT UNSIGNED NOT NULL,
    kills BIGINT UNSIGNED NOT NULL DEFAULT 0,
    deaths BIGINT UNSIGNED NOT NULL DEFAULT 0,
    assists BIGINT UNSIGNED NOT NULL DEFAULT 0,
    headshots BIGINT UNSIGNED NOT NULL DEFAULT 0,
    weapon_fire_count BIGINT UNSIGNED NOT NULL DEFAULT 0,
    hegrenade_detonations BIGINT UNSIGNED NOT NULL DEFAULT 0,
    flashbang_detonations BIGINT UNSIGNED NOT NULL DEFAULT 0,
    molotov_detonations BIGINT UNSIGNED NOT NULL DEFAULT 0,
    smokegrenade_detonations BIGINT UNSIGNED NOT NULL DEFAULT 0,
    bomb_plants BIGINT UNSIGNED NOT NULL DEFAULT 0,
    bomb_defuses BIGINT UNSIGNED NOT NULL DEFAULT 0,
    mvps BIGINT UNSIGNED NOT NULL DEFAULT 0,
    rounds_played BIGINT UNSIGNED NOT NULL DEFAULT 0,
    total_playtime_seconds BIGINT UNSIGNED NOT NULL DEFAULT 0,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (player_id),
    CONSTRAINT fk_player_lifetime_stats_player FOREIGN KEY (player_id)
        REFERENCES players (player_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS player_session_stats (
    player_session_id BIGINT UNSIGNED NOT NULL,
    kills INT UNSIGNED NOT NULL DEFAULT 0,
    deaths INT UNSIGNED NOT NULL DEFAULT 0,
    assists INT UNSIGNED NOT NULL DEFAULT 0,
    headshots INT UNSIGNED NOT NULL DEFAULT 0,
    weapon_fire_count INT UNSIGNED NOT NULL DEFAULT 0,
    hegrenade_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    flashbang_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    molotov_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    smokegrenade_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    bomb_plants INT UNSIGNED NOT NULL DEFAULT 0,
    bomb_defuses INT UNSIGNED NOT NULL DEFAULT 0,
    mvps INT UNSIGNED NOT NULL DEFAULT 0,
    rounds_played INT UNSIGNED NOT NULL DEFAULT 0,
    playtime_seconds INT UNSIGNED NOT NULL DEFAULT 0,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (player_session_id),
    CONSTRAINT fk_player_session_stats_session FOREIGN KEY (player_session_id)
        REFERENCES player_sessions (player_session_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS player_map_stats (
    player_id BIGINT UNSIGNED NOT NULL,
    map_session_id BIGINT UNSIGNED NOT NULL,
    kills INT UNSIGNED NOT NULL DEFAULT 0,
    deaths INT UNSIGNED NOT NULL DEFAULT 0,
    assists INT UNSIGNED NOT NULL DEFAULT 0,
    headshots INT UNSIGNED NOT NULL DEFAULT 0,
    weapon_fire_count INT UNSIGNED NOT NULL DEFAULT 0,
    hegrenade_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    flashbang_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    molotov_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    smokegrenade_detonations INT UNSIGNED NOT NULL DEFAULT 0,
    bomb_plants INT UNSIGNED NOT NULL DEFAULT 0,
    bomb_defuses INT UNSIGNED NOT NULL DEFAULT 0,
    mvps INT UNSIGNED NOT NULL DEFAULT 0,
    rounds_played INT UNSIGNED NOT NULL DEFAULT 0,
    playtime_seconds INT UNSIGNED NOT NULL DEFAULT 0,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (player_id, map_session_id),
    CONSTRAINT fk_player_map_stats_player FOREIGN KEY (player_id)
        REFERENCES players (player_id),
    CONSTRAINT fk_player_map_stats_map_session FOREIGN KEY (map_session_id)
        REFERENCES map_sessions (map_session_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS kill_events (
    kill_event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    map_session_id BIGINT UNSIGNED NOT NULL,
    round_id BIGINT UNSIGNED NULL,
    occurred_at_utc DATETIME(6) NOT NULL,
    attacker_player_id BIGINT UNSIGNED NULL,
    victim_player_id BIGINT UNSIGNED NULL,
    assister_player_id BIGINT UNSIGNED NULL,
    weapon_name VARCHAR(128) NULL,
    is_headshot TINYINT(1) NOT NULL DEFAULT 0,
    hitgroup INT NULL,
    penetrated INT NULL,
    noscope TINYINT(1) NOT NULL DEFAULT 0,
    thrusmoke TINYINT(1) NOT NULL DEFAULT 0,
    distance FLOAT NULL,
    attacker_blind TINYINT(1) NOT NULL DEFAULT 0,
    attacker_in_air TINYINT(1) NOT NULL DEFAULT 0,
    assisted_flash TINYINT(1) NOT NULL DEFAULT 0,
    created_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (kill_event_id),
    KEY ix_kill_events_map_occurred (map_session_id, occurred_at_utc),
    KEY ix_kill_events_attacker_occurred (attacker_player_id, occurred_at_utc),
    KEY ix_kill_events_victim_occurred (victim_player_id, occurred_at_utc),
    CONSTRAINT fk_kill_events_map_session FOREIGN KEY (map_session_id)
        REFERENCES map_sessions (map_session_id),
    CONSTRAINT fk_kill_events_round FOREIGN KEY (round_id)
        REFERENCES rounds (round_id),
    CONSTRAINT fk_kill_events_attacker FOREIGN KEY (attacker_player_id)
        REFERENCES players (player_id),
    CONSTRAINT fk_kill_events_victim FOREIGN KEY (victim_player_id)
        REFERENCES players (player_id),
    CONSTRAINT fk_kill_events_assister FOREIGN KEY (assister_player_id)
        REFERENCES players (player_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS player_action_events (
    player_action_event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    player_id BIGINT UNSIGNED NOT NULL,
    map_session_id BIGINT UNSIGNED NOT NULL,
    round_id BIGINT UNSIGNED NULL,
    occurred_at_utc DATETIME(6) NOT NULL,
    action_type VARCHAR(64) NOT NULL,
    action_value VARCHAR(255) NULL,
    created_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (player_action_event_id),
    KEY ix_player_action_events_player_time (player_id, occurred_at_utc),
    KEY ix_player_action_events_type_time (action_type, occurred_at_utc),
    CONSTRAINT fk_player_action_events_player FOREIGN KEY (player_id)
        REFERENCES players (player_id),
    CONSTRAINT fk_player_action_events_map_session FOREIGN KEY (map_session_id)
        REFERENCES map_sessions (map_session_id),
    CONSTRAINT fk_player_action_events_round FOREIGN KEY (round_id)
        REFERENCES rounds (round_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS presence_snapshots (
    presence_snapshot_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    map_session_id BIGINT UNSIGNED NOT NULL,
    captured_at_utc DATETIME(6) NOT NULL,
    connected_player_count INT UNSIGNED NOT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (presence_snapshot_id),
    KEY ix_presence_snapshots_map_time (map_session_id, captured_at_utc),
    CONSTRAINT fk_presence_snapshots_map_session FOREIGN KEY (map_session_id)
        REFERENCES map_sessions (map_session_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS presence_snapshot_players (
    presence_snapshot_id BIGINT UNSIGNED NOT NULL,
    player_id BIGINT UNSIGNED NOT NULL,
    slot_index INT NULL,
    team_value INT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (presence_snapshot_id, player_id),
    KEY ix_presence_snapshot_players_player (player_id),
    CONSTRAINT fk_presence_snapshot_players_snapshot FOREIGN KEY (presence_snapshot_id)
        REFERENCES presence_snapshots (presence_snapshot_id),
    CONSTRAINT fk_presence_snapshot_players_player FOREIGN KEY (player_id)
        REFERENCES players (player_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
";
    }

    /// <summary>
    /// Get the stored procedures SQL script.
    /// </summary>
    private static string GetStoredProceduresScript()
    {
        return @"
DROP PROCEDURE IF EXISTS sp_refresh_player_lifetime_stats;
DELIMITER $$
CREATE PROCEDURE sp_refresh_player_lifetime_stats(
    IN p_player_id BIGINT UNSIGNED
)
BEGIN
    DECLARE v_player_count INT;
    
    IF p_player_id IS NOT NULL THEN
        SELECT COUNT(*) INTO v_player_count FROM players WHERE player_id = p_player_id;
        IF v_player_count = 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player ID not found';
        END IF;
    END IF;
    
    INSERT INTO player_lifetime_stats (
        player_id, kills, deaths, assists, headshots, weapon_fire_count,
        hegrenade_detonations, flashbang_detonations, molotov_detonations,
        smokegrenade_detonations, bomb_plants, bomb_defuses, mvps,
        rounds_played, total_playtime_seconds, created_at_utc, updated_at_utc
    )
    SELECT
        p.player_id,
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.victim_player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.assister_player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = p.player_id AND ke.is_headshot = 1 THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'weapon_fire' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'hegrenade' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'flashbang' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'molotov' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'smokegrenade' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_planted' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_defused' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'round_mvp' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0),
        0, 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
    FROM players p
    LEFT JOIN kill_events ke ON (ke.attacker_player_id = p.player_id OR ke.victim_player_id = p.player_id OR ke.assister_player_id = p.player_id)
    LEFT JOIN player_action_events pae ON pae.player_id = p.player_id
    WHERE (p_player_id IS NULL OR p.player_id = p_player_id)
    GROUP BY p.player_id
    ON DUPLICATE KEY UPDATE
        kills = VALUES(kills), deaths = VALUES(deaths), assists = VALUES(assists),
        headshots = VALUES(headshots), weapon_fire_count = VALUES(weapon_fire_count),
        updated_at_utc = UTC_TIMESTAMP(6);
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_refresh_player_session_stats;
DELIMITER $$
CREATE PROCEDURE sp_refresh_player_session_stats(
    IN p_player_session_id BIGINT UNSIGNED
)
BEGIN
    DECLARE v_session_count INT;
    
    SELECT COUNT(*) INTO v_session_count FROM player_sessions WHERE player_session_id = p_player_session_id;
    IF v_session_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player session ID not found';
    END IF;
    
    INSERT INTO player_session_stats (
        player_session_id, kills, deaths, assists, headshots, weapon_fire_count,
        hegrenade_detonations, flashbang_detonations, molotov_detonations,
        smokegrenade_detonations, bomb_plants, bomb_defuses, mvps,
        rounds_played, playtime_seconds, created_at_utc, updated_at_utc
    )
    SELECT
        p_player_session_id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
    ON DUPLICATE KEY UPDATE updated_at_utc = UTC_TIMESTAMP(6);
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_refresh_player_map_stats;
DELIMITER $$
CREATE PROCEDURE sp_refresh_player_map_stats(
    IN p_player_id BIGINT UNSIGNED,
    IN p_map_session_id BIGINT UNSIGNED
)
BEGIN
    DECLARE v_player_count INT;
    DECLARE v_map_count INT;
    
    SELECT COUNT(*) INTO v_player_count FROM players WHERE player_id = p_player_id;
    IF v_player_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player ID not found';
    END IF;
    
    SELECT COUNT(*) INTO v_map_count FROM map_sessions WHERE map_session_id = p_map_session_id;
    IF v_map_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Map session ID not found';
    END IF;
    
    INSERT INTO player_map_stats (
        player_id, map_session_id, kills, deaths, assists, headshots,
        weapon_fire_count, hegrenade_detonations, flashbang_detonations,
        molotov_detonations, smokegrenade_detonations, bomb_plants, bomb_defuses,
        mvps, rounds_played, playtime_seconds, created_at_utc, updated_at_utc
    ) VALUES (
        p_player_id, p_map_session_id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
    )
    ON DUPLICATE KEY UPDATE updated_at_utc = UTC_TIMESTAMP(6);
END$$
DELIMITER ;
";
    }
}
