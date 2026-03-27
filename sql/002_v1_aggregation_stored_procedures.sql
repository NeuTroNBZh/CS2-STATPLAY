-- CS2 Stats V1 - Aggregation Stored Procedures
-- Procedures to calculate and refresh player statistics from raw events
-- MySQL 8+

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_refresh_player_lifetime_stats$$

CREATE PROCEDURE sp_refresh_player_lifetime_stats(
    IN p_player_id BIGINT UNSIGNED
)
PROC_LABEL: BEGIN
    DECLARE v_player_count INT;

    IF p_player_id IS NOT NULL THEN
        SELECT COUNT(*) INTO v_player_count FROM players WHERE player_id = p_player_id;
        IF v_player_count = 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player ID not found';
        END IF;
    END IF;

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
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.attacker_player_id = p.player_id),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.victim_player_id = p.player_id),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.assister_player_id = p.player_id),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.attacker_player_id = p.player_id AND ke.is_headshot = 1),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'weapon_fire'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'hegrenade'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'flashbang'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'molotov'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'smokegrenade'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'bomb_planted'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'bomb_defused'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.player_id = p.player_id AND pae.action_type = 'round_mvp'),
        (
            SELECT COUNT(DISTINCT r.round_id)
            FROM rounds r
            WHERE EXISTS (
                SELECT 1
                FROM kill_events ke
                WHERE ke.map_session_id = r.map_session_id
                  AND (ke.attacker_player_id = p.player_id OR ke.victim_player_id = p.player_id OR ke.assister_player_id = p.player_id)
            )
            OR EXISTS (
                SELECT 1
                FROM player_action_events pae
                WHERE pae.map_session_id = r.map_session_id
                  AND pae.player_id = p.player_id
            )
        ),
        (
            SELECT COALESCE(SUM(TIMESTAMPDIFF(SECOND, ps.connected_at_utc, COALESCE(ps.disconnected_at_utc, UTC_TIMESTAMP(6)))), 0)
            FROM player_sessions ps
            WHERE ps.player_id = p.player_id
        ),
        UTC_TIMESTAMP(6),
        UTC_TIMESTAMP(6)
    FROM players p
    WHERE (p_player_id IS NULL OR p.player_id = p_player_id)
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
        total_playtime_seconds = VALUES(total_playtime_seconds),
        updated_at_utc = UTC_TIMESTAMP(6);
END$$

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
    DECLARE v_session_end DATETIME(6);
    DECLARE v_playtime_seconds INT DEFAULT 0;

    SELECT COUNT(*) INTO v_session_count FROM player_sessions WHERE player_session_id = p_player_session_id;
    IF v_session_count = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Player session ID not found';
    END IF;

    SELECT player_id, map_session_id, connected_at_utc, disconnected_at_utc
    INTO v_player_id, v_map_session_id, v_connected_at_utc, v_disconnected_at_utc
    FROM player_sessions
    WHERE player_session_id = p_player_session_id;

    SET v_session_end = COALESCE(v_disconnected_at_utc, UTC_TIMESTAMP(6));
    SET v_playtime_seconds = TIMESTAMPDIFF(SECOND, v_connected_at_utc, v_session_end);

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
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = v_map_session_id AND ke.attacker_player_id = v_player_id AND ke.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = v_map_session_id AND ke.victim_player_id = v_player_id AND ke.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = v_map_session_id AND ke.assister_player_id = v_player_id AND ke.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = v_map_session_id AND ke.attacker_player_id = v_player_id AND ke.is_headshot = 1 AND ke.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'weapon_fire' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'hegrenade' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'flashbang' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'molotov' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'smokegrenade' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'bomb_planted' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'bomb_defused' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = v_map_session_id AND pae.player_id = v_player_id AND pae.action_type = 'round_mvp' AND pae.occurred_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        (SELECT COUNT(DISTINCT r.round_id) FROM rounds r WHERE r.map_session_id = v_map_session_id AND r.started_at_utc BETWEEN v_connected_at_utc AND v_session_end),
        v_playtime_seconds,
        UTC_TIMESTAMP(6),
        UTC_TIMESTAMP(6)
    FROM dual
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

DROP PROCEDURE IF EXISTS sp_refresh_player_map_stats$$

CREATE PROCEDURE sp_refresh_player_map_stats(
    IN p_player_id BIGINT UNSIGNED,
    IN p_map_session_id BIGINT UNSIGNED
)
PROC_LABEL: BEGIN
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
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = p_map_session_id AND ke.attacker_player_id = p_player_id),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = p_map_session_id AND ke.victim_player_id = p_player_id),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = p_map_session_id AND ke.assister_player_id = p_player_id),
        (SELECT COUNT(*) FROM kill_events ke WHERE ke.map_session_id = p_map_session_id AND ke.attacker_player_id = p_player_id AND ke.is_headshot = 1),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'weapon_fire'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'hegrenade'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'flashbang'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'molotov'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'grenade_detonation' AND pae.action_value = 'smokegrenade'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'bomb_planted'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'bomb_defused'),
        (SELECT COUNT(*) FROM player_action_events pae WHERE pae.map_session_id = p_map_session_id AND pae.player_id = p_player_id AND pae.action_type = 'round_mvp'),
        (SELECT COUNT(DISTINCT r.round_id) FROM rounds r WHERE r.map_session_id = p_map_session_id),
        (
            SELECT COALESCE(SUM(TIMESTAMPDIFF(SECOND, ps.connected_at_utc, COALESCE(ps.disconnected_at_utc, UTC_TIMESTAMP(6)))), 0)
            FROM player_sessions ps
            WHERE ps.player_id = p_player_id
              AND ps.map_session_id = p_map_session_id
        ),
        UTC_TIMESTAMP(6),
        UTC_TIMESTAMP(6)
    FROM dual
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
