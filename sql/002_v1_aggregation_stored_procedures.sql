-- CS2 Stats V1 - Aggregation Stored Procedures
-- Procedures to calculate and refresh player statistics from raw events
-- MySQL 8+

-- ============================================================================
-- PROCEDURE: sp_refresh_player_lifetime_stats
-- PURPOSE: Recalculate lifetime stats for all players or a specific player
-- PARAMETERS:
--   @p_player_id: (optional) specific player_id; if NULL, recalculates all
-- ============================================================================
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_refresh_player_lifetime_stats$$

CREATE PROCEDURE sp_refresh_player_lifetime_stats(
    IN p_player_id BIGINT UNSIGNED
)
PROC_LABEL: BEGIN
    DECLARE v_player_count INT;
    
    -- Check parameter
    IF p_player_id IS NOT NULL THEN
        SELECT COUNT(*) INTO v_player_count FROM players WHERE player_id = p_player_id;
        IF v_player_count = 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player ID not found';
        END IF;
    END IF;
    
    -- Insert or update player_lifetime_stats
    -- Strategy: Calculate all stats from kill_events and player_action_events
    INSERT INTO player_lifetime_stats (
        player_id,
        kills,
        deaths,
        assists,
        headshots,
        weapon_fire_count,
        hegrenade_detonations,
        flashbang_detonations,
        molotov_detonations,
        smokegrenade_detonations,
        bomb_plants,
        bomb_defuses,
        mvps,
        rounds_played,
        total_playtime_seconds,
        created_at_utc,
        updated_at_utc
    )
    SELECT
        p.player_id,
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = p.player_id THEN 1 ELSE 0 END), 0) AS kills,
        COALESCE(SUM(CASE WHEN ke.victim_player_id = p.player_id THEN 1 ELSE 0 END), 0) AS deaths,
        COALESCE(SUM(CASE WHEN ke.assister_player_id = p.player_id THEN 1 ELSE 0 END), 0) AS assists,
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = p.player_id AND ke.is_headshot = 1 THEN 1 ELSE 0 END), 0) AS headshots,
        COALESCE(SUM(CASE WHEN pae.action_type = 'weapon_fire' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS weapon_fire_count,
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'hegrenade' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS hegrenade_detonations,
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'flashbang' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS flashbang_detonations,
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'molotov' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS molotov_detonations,
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'smokegrenade' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS smokegrenade_detonations,
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_planted' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS bomb_plants,
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_defused' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS bomb_defuses,
        COALESCE(SUM(CASE WHEN pae.action_type = 'round_mvp' AND pae.player_id = p.player_id THEN 1 ELSE 0 END), 0) AS mvps,
        0 AS rounds_played,  -- TODO: Calculate from presence snapshots or player_sessions
        0 AS total_playtime_seconds,  -- TODO: Calculate from player_sessions.disconnected_at_utc - connected_at_utc
        UTC_TIMESTAMP(6) AS created_at_utc,
        UTC_TIMESTAMP(6) AS updated_at_utc
    FROM players p
    LEFT JOIN kill_events ke ON (ke.attacker_player_id = p.player_id OR ke.victim_player_id = p.player_id OR ke.assister_player_id = p.player_id)
    LEFT JOIN player_action_events pae ON pae.player_id = p.player_id
    WHERE (p_player_id IS NULL OR p.player_id = p_player_id)
    GROUP BY p.player_id
    ON DUPLICATE KEY UPDATE
        kills = VALUES(kills),
        deaths = VALUES(deaths),
        assists = VALUES(assists),
        headshots = VALUES(headshots),
        weapon_fire_count = VALUES(weapon_fire_count),
        hegrenade_detonations = VALUES(hegrenade_detonations),
        flashbang_detonations = VALUES(flashbang_detonations),
        molotov_detonations = VALUES(molotov_detonations),
        smokegrenade_detonations = VALUES(smokegrenade_detonations),
        bomb_plants = VALUES(bomb_plants),
        bomb_defuses = VALUES(bomb_defuses),
        mvps = VALUES(mvps),
        updated_at_utc = UTC_TIMESTAMP(6);
END$$

DELIMITER ;

-- ============================================================================
-- PROCEDURE: sp_refresh_player_session_stats
-- PURPOSE: Recalculate session stats for a specific player session
-- PARAMETERS:
--   @p_player_session_id: player_session_id to refresh
-- ============================================================================
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_refresh_player_session_stats$$

CREATE PROCEDURE sp_refresh_player_session_stats(
    IN p_player_session_id BIGINT UNSIGNED
)
PROC_LABEL: BEGIN
    DECLARE v_session_count INT;
    DECLARE v_player_id BIGINT UNSIGNED;
    DECLARE v_map_session_id BIGINT UNSIGNED;
    DECLARE v_connected_at_utc DATETIME(6);
    DECLARE v_disconnected_at_utc DATETIME(6);
    
    -- Fetch session details
    SELECT COUNT(*) INTO v_session_count FROM player_sessions WHERE player_session_id = p_player_session_id;
    IF v_session_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player session ID not found';
    END IF;
    
    SELECT player_id, map_session_id, connected_at_utc, disconnected_at_utc
    INTO v_player_id, v_map_session_id, v_connected_at_utc, v_disconnected_at_utc
    FROM player_sessions
    WHERE player_session_id = p_player_session_id;
    
    -- Calculate playtime in seconds
    DECLARE v_playtime_seconds INT DEFAULT 0;
    IF v_disconnected_at_utc IS NOT NULL THEN
        SET v_playtime_seconds = TIMESTAMPDIFF(SECOND, v_connected_at_utc, v_disconnected_at_utc);
    END IF;
    
    -- Insert or update player_session_stats
    INSERT INTO player_session_stats (
        player_session_id,
        kills,
        deaths,
        assists,
        headshots,
        weapon_fire_count,
        hegrenade_detonations,
        flashbang_detonations,
        molotov_detonations,
        smokegrenade_detonations,
        bomb_plants,
        bomb_defuses,
        mvps,
        rounds_played,
        playtime_seconds,
        created_at_utc,
        updated_at_utc
    )
    SELECT
        p_player_session_id,
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = v_player_id AND ke.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.victim_player_id = v_player_id AND ke.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.assister_player_id = v_player_id AND ke.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = v_player_id AND ke.is_headshot = 1 AND ke.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'weapon_fire' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'hegrenade' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'flashbang' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'molotov' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'smokegrenade' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_planted' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_defused' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'round_mvp' AND pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id THEN 1 ELSE 0 END), 0),
        COUNT(DISTINCT CASE WHEN r.map_session_id = v_map_session_id THEN r.round_id END),
        v_playtime_seconds,
        UTC_TIMESTAMP(6),
        UTC_TIMESTAMP(6)
    FROM dual
    LEFT JOIN kill_events ke ON (ke.attacker_player_id = v_player_id OR ke.victim_player_id = v_player_id OR ke.assister_player_id = v_player_id) AND ke.map_session_id = v_map_session_id
    LEFT JOIN player_action_events pae ON pae.player_id = v_player_id AND pae.map_session_id = v_map_session_id
    LEFT JOIN rounds r ON r.map_session_id = v_map_session_id
    ON DUPLICATE KEY UPDATE
        kills = VALUES(kills),
        deaths = VALUES(deaths),
        assists = VALUES(assists),
        headshots = VALUES(headshots),
        weapon_fire_count = VALUES(weapon_fire_count),
        hegrenade_detonations = VALUES(hegrenade_detonations),
        flashbang_detonations = VALUES(flashbang_detonations),
        molotov_detonations = VALUES(molotov_detonations),
        smokegrenade_detonations = VALUES(smokegrenade_detonations),
        bomb_plants = VALUES(bomb_plants),
        bomb_defuses = VALUES(bomb_defuses),
        mvps = VALUES(mvps),
        rounds_played = VALUES(rounds_played),
        playtime_seconds = VALUES(playtime_seconds),
        updated_at_utc = UTC_TIMESTAMP(6);
END$$

DELIMITER ;

-- ============================================================================
-- PROCEDURE: sp_refresh_player_map_stats
-- PURPOSE: Recalculate per-map stats for a specific player and map session
-- PARAMETERS:
--   @p_player_id: player_id
--   @p_map_session_id: map_session_id
-- ============================================================================
DELIMITER $$

DROP PROCEDURE IF EXISTS sp_refresh_player_map_stats$$

CREATE PROCEDURE sp_refresh_player_map_stats(
    IN p_player_id BIGINT UNSIGNED,
    IN p_map_session_id BIGINT UNSIGNED
)
PROC_LABEL: BEGIN
    DECLARE v_player_count INT;
    DECLARE v_map_count INT;
    
    -- Validate parameters
    SELECT COUNT(*) INTO v_player_count FROM players WHERE player_id = p_player_id;
    IF v_player_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player ID not found';
    END IF;
    
    SELECT COUNT(*) INTO v_map_count FROM map_sessions WHERE map_session_id = p_map_session_id;
    IF v_map_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Map session ID not found';
    END IF;
    
    -- Insert or update player_map_stats
    INSERT INTO player_map_stats (
        player_id,
        map_session_id,
        kills,
        deaths,
        assists,
        headshots,
        weapon_fire_count,
        hegrenade_detonations,
        flashbang_detonations,
        molotov_detonations,
        smokegrenade_detonations,
        bomb_plants,
        bomb_defuses,
        mvps,
        rounds_played,
        playtime_seconds,
        created_at_utc,
        updated_at_utc
    )
    SELECT
        p_player_id,
        p_map_session_id,
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.victim_player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.assister_player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN ke.attacker_player_id = p_player_id AND ke.is_headshot = 1 THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'weapon_fire' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'hegrenade' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'flashbang' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'molotov' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'grenade_detonation' AND pae.action_value = 'smokegrenade' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_planted' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'bomb_defused' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COALESCE(SUM(CASE WHEN pae.action_type = 'round_mvp' AND pae.player_id = p_player_id THEN 1 ELSE 0 END), 0),
        COUNT(DISTINCT r.round_id),
        COALESCE(SUM(TIMESTAMPDIFF(SECOND, ps.connected_at_utc, COALESCE(ps.disconnected_at_utc, CURTIME(6)))), 0),
        UTC_TIMESTAMP(6),
        UTC_TIMESTAMP(6)
    FROM dual
    LEFT JOIN kill_events ke ON ke.attacker_player_id = p_player_id OR ke.victim_player_id = p_player_id OR ke.assister_player_id = p_player_id
    LEFT JOIN player_action_events pae ON pae.player_id = p_player_id AND pae.map_session_id = p_map_session_id
    LEFT JOIN rounds r ON r.map_session_id = p_map_session_id
    LEFT JOIN player_sessions ps ON ps.player_id = p_player_id AND ps.map_session_id = p_map_session_id
    ON DUPLICATE KEY UPDATE
        kills = VALUES(kills),
        deaths = VALUES(deaths),
        assists = VALUES(assists),
        headshots = VALUES(headshots),
        weapon_fire_count = VALUES(weapon_fire_count),
        hegrenade_detonations = VALUES(hegrenade_detonations),
        flashbang_detonations = VALUES(flashbang_detonations),
        molotov_detonations = VALUES(molotov_detonations),
        smokegrenade_detonations = VALUES(smokegrenade_detonations),
        bomb_plants = VALUES(bomb_plants),
        bomb_defuses = VALUES(bomb_defuses),
        mvps = VALUES(mvps),
        rounds_played = VALUES(rounds_played),
        playtime_seconds = VALUES(playtime_seconds),
        updated_at_utc = UTC_TIMESTAMP(6);
END$$

DELIMITER ;
