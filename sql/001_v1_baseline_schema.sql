-- CS2 Stats V1 baseline schema (MySQL 8+)
-- Focused on validated data: sessions, K/D/A events, headshots, weapon fire,
-- grenade/objective actions, round/map history, presence snapshots.

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
