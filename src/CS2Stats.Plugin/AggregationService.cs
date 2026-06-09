using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace CS2Stats.Plugin;

/// <summary>
/// Service to refresh aggregated statistics tables from raw event data.
/// Calls stored procedures to recalculate K/D/A, headshots, and other stats
/// at different aggregation levels (lifetime, session, per-map).
/// </summary>
public sealed class AggregationService
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public AggregationService(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Refresh lifetime stats for all players or a specific player.
    /// </summary>
    public async Task RefreshPlayerLifetimeStatsAsync(ulong? playerSteamId64 = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = "sp_refresh_player_lifetime_stats";

            if (playerSteamId64.HasValue)
            {
                // Find player_id from steam_id64
                var playerId = await GetPlayerIdBySteamIdAsync(connection, playerSteamId64.Value, cancellationToken);
                if (playerId.HasValue)
                {
                    cmd.Parameters.AddWithValue("@p_player_id", playerId.Value);
                }
                else
                {
                    _logger.LogWarning("[CS2Stats] Player with Steam ID {SteamId} not found for aggregation", playerSteamId64);
                    return;
                }
            }
            else
            {
                cmd.Parameters.AddWithValue("@p_player_id", DBNull.Value);
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[CS2Stats] Refreshed lifetime stats for player {SteamId}", playerSteamId64 ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to refresh lifetime stats");
        }
    }

    /// <summary>
    /// Refresh session-level stats for a specific player session.
    /// </summary>
    public async Task RefreshPlayerSessionStatsAsync(ulong playerSteamId64, DateTime sessionStart, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Find player_session_id
            var playerSessionId = await GetPlayerSessionIdAsync(connection, playerSteamId64, sessionStart, cancellationToken);
            if (!playerSessionId.HasValue)
            {
                _logger.LogWarning("[CS2Stats] Player session not found for Steam ID {SteamId}", playerSteamId64);
                return;
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = "sp_refresh_player_session_stats";
            cmd.Parameters.AddWithValue("@p_player_session_id", playerSessionId.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[CS2Stats] Refreshed session stats for player {SteamId}", playerSteamId64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to refresh session stats");
        }
    }

    /// <summary>
    /// Refresh per-map stats for a specific player and map session.
    /// </summary>
    public async Task RefreshPlayerMapStatsAsync(ulong playerSteamId64, ulong mapSessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Find player_id
            var playerId = await GetPlayerIdBySteamIdAsync(connection, playerSteamId64, cancellationToken);
            if (!playerId.HasValue)
            {
                _logger.LogWarning("[CS2Stats] Player with Steam ID {SteamId} not found for map aggregation", playerSteamId64);
                return;
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = "sp_refresh_player_map_stats";
            cmd.Parameters.AddWithValue("@p_player_id", playerId.Value);
            cmd.Parameters.AddWithValue("@p_map_session_id", mapSessionId);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[CS2Stats] Refreshed map stats for player {SteamId} on map session {MapSessionId}", playerSteamId64, mapSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to refresh map stats");
        }
    }

    /// <summary>
    /// Refresh stats for all players after a map ends.
    /// Useful to run after map end or on a scheduled basis.
    /// </summary>
    public async Task RefreshAllStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[CS2Stats] Starting full stats refresh");

            await RefreshPlayerLifetimeStatsAsync(null, cancellationToken).ConfigureAwait(false);
            await RefreshPendingSessionStatsAsync(cancellationToken).ConfigureAwait(false);
            await RefreshPendingMapStatsAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("[CS2Stats] Full stats refresh completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to refresh all stats");
        }
    }

    /// <summary>
    /// Helper: Get player_id from Steam ID.
    /// </summary>
    private static async Task<ulong?> GetPlayerIdBySteamIdAsync(MySqlConnection connection, ulong steamId64, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT player_id FROM players WHERE steam_id64 = @steam_id64 LIMIT 1";
        cmd.Parameters.AddWithValue("@steam_id64", steamId64);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null ? (ulong?)Convert.ToUInt64(result) : null;
    }

    /// <summary>
    /// Helper: Get player_session_id for a player and session start time.
    /// </summary>
    private static async Task<ulong?> GetPlayerSessionIdAsync(MySqlConnection connection, ulong playerSteamId64, DateTime sessionStart, CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT ps.player_session_id
            FROM player_sessions ps
            JOIN players p ON p.player_id = ps.player_id
            WHERE p.steam_id64 = @steam_id64
            AND ps.connected_at_utc >= @session_start
            ORDER BY ps.connected_at_utc DESC
            LIMIT 1
        ";
        cmd.Parameters.AddWithValue("@steam_id64", playerSteamId64);
        cmd.Parameters.AddWithValue("@session_start", sessionStart);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null ? (ulong?)Convert.ToUInt64(result) : null;
    }

    /// <summary>
    /// Refresh session stats for all closed sessions that don't have a stats row yet.
    /// </summary>
    public async Task RefreshPendingSessionStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var pendingIds = new List<ulong>();
            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = @"
                    SELECT ps.player_session_id
                    FROM player_sessions ps
                    LEFT JOIN player_session_stats pss ON ps.player_session_id = pss.player_session_id
                    WHERE ps.disconnected_at_utc IS NOT NULL
                      AND pss.player_session_id IS NULL";
                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    pendingIds.Add(reader.GetUInt64(0));
            }

            foreach (var sessionId in pendingIds)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandText = "sp_refresh_player_session_stats";
                cmd.Parameters.AddWithValue("@p_player_session_id", sessionId);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (pendingIds.Count > 0)
                _logger.LogInformation("[CS2Stats] Refreshed session stats for {Count} pending sessions", pendingIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to refresh pending session stats");
        }
    }

    /// <summary>
    /// Refresh map stats for all (player, map_session) combinations that don't have a map stats row yet.
    /// </summary>
    public async Task RefreshPendingMapStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var pending = new List<(ulong PlayerId, ulong MapSessionId)>();
            await using (var selectCmd = connection.CreateCommand())
            {
                selectCmd.CommandText = @"
                    SELECT DISTINCT ps.player_id, ps.map_session_id
                    FROM player_sessions ps
                    LEFT JOIN player_map_stats pms
                      ON ps.player_id = pms.player_id
                     AND ps.map_session_id = pms.map_session_id
                    WHERE ps.disconnected_at_utc IS NOT NULL
                      AND pms.player_id IS NULL";
                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    pending.Add((reader.GetUInt64(0), reader.GetUInt64(1)));
            }

            foreach (var (playerId, mapSessionId) in pending)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandText = "sp_refresh_player_map_stats";
                cmd.Parameters.AddWithValue("@p_player_id", playerId);
                cmd.Parameters.AddWithValue("@p_map_session_id", mapSessionId);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (pending.Count > 0)
                _logger.LogInformation("[CS2Stats] Refreshed map stats for {Count} pending player/map combinations", pending.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to refresh pending map stats");
        }
    }
}
