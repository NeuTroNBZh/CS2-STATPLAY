using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using CS2Stats.Contracts;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace CS2Stats.Plugin;

public sealed class MySqlStatsWriter : IStatsWriter
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public MySqlStatsWriter(MySqlSettings settings, ILogger logger)
    {
        _logger = logger;
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Host,
            Port = (uint)settings.Port,
            Database = settings.Database,
            UserID = settings.Username,
            Password = settings.Password,
            SslMode = settings.SslRequired ? MySqlSslMode.Required : MySqlSslMode.None,
            AllowUserVariables = true,
            ConnectionTimeout = 15,
            DefaultCommandTimeout = 30
        };
        _connectionString = builder.ConnectionString;
    }

    public async Task WriteBatchAsync(StatsBatch batch, CancellationToken cancellationToken)
    {
        if (batch.IsEmpty) return;

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            var playerCache = new Dictionary<ulong, ulong>();
            var mapCache = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            var roundCache = new Dictionary<(ulong MapSessionId, int RoundNumber), ulong>();

            await WriteSessionsOpenedAsync(connection, tx, batch.SessionOpened, playerCache, mapCache, cancellationToken).ConfigureAwait(false);
            await WriteSessionsClosedAsync(connection, tx, batch.SessionClosed, playerCache, cancellationToken).ConfigureAwait(false);
            await WriteRoundsStartedAsync(connection, tx, batch.RoundStarted, mapCache, cancellationToken).ConfigureAwait(false);
            await WriteRoundsEndedAsync(connection, tx, batch.RoundEnded, mapCache, cancellationToken).ConfigureAwait(false);
            await WritePlayerDeathsAsync(connection, tx, batch.PlayerDeaths, playerCache, cancellationToken).ConfigureAwait(false);
            await WritePlayerActionsAsync(connection, tx, batch.PlayerActions, playerCache, roundCache, cancellationToken).ConfigureAwait(false);
            await WritePresenceSnapshotsAsync(connection, tx, batch.PresenceSnapshots, playerCache, cancellationToken).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Failed to write stats batch to MySQL");
            throw;
        }
    }

    private static async Task WriteSessionsOpenedAsync(
        MySqlConnection connection, MySqlTransaction tx,
        List<PlayerSessionOpened> sessions,
        Dictionary<ulong, ulong> playerCache,
        Dictionary<string, ulong> mapCache,
        CancellationToken cancellationToken)
    {
        foreach (var opened in sessions)
        {
            var mapSessionId = await EnsureOpenMapSessionIdAsync(connection, tx, opened.MapName, opened.ConnectedAtUtc, cancellationToken).ConfigureAwait(false);
            mapCache[opened.MapName] = mapSessionId;

            var playerId = await EnsurePlayerIdAsync(connection, tx, opened.SteamId64, opened.ConnectedAtUtc, playerCache, cancellationToken).ConfigureAwait(false);

            await using (var closePrev = new MySqlCommand(@"
UPDATE player_sessions
SET disconnected_at_utc = @disconnected_at_utc,
    disconnect_reason = @disconnect_reason,
    server_current_time_disconnect = @server_time,
    updated_at_utc = @updated_at_utc
WHERE player_id = @player_id
  AND disconnected_at_utc IS NULL
  AND connected_at_utc <= @connected_at_utc", connection, tx))
            {
                closePrev.Parameters.AddWithValue("@disconnected_at_utc", opened.ConnectedAtUtc);
                closePrev.Parameters.AddWithValue("@disconnect_reason", "auto_close_on_new_connect");
                closePrev.Parameters.AddWithValue("@server_time", opened.ServerCurrentTimeSeconds);
                closePrev.Parameters.AddWithValue("@updated_at_utc", opened.ConnectedAtUtc);
                closePrev.Parameters.AddWithValue("@player_id", playerId);
                closePrev.Parameters.AddWithValue("@connected_at_utc", opened.ConnectedAtUtc);
                await closePrev.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var cmd = new MySqlCommand(@"
INSERT INTO player_sessions (
    player_id, map_session_id, connected_at_utc, server_current_time_connect, created_at_utc, updated_at_utc
) VALUES (
    @player_id, @map_session_id, @connected_at_utc, @server_time, @now_utc, @now_utc
)", connection, tx);

            cmd.Parameters.AddWithValue("@player_id", playerId);
            cmd.Parameters.AddWithValue("@map_session_id", mapSessionId);
            cmd.Parameters.AddWithValue("@connected_at_utc", opened.ConnectedAtUtc);
            cmd.Parameters.AddWithValue("@server_time", opened.ServerCurrentTimeSeconds);
            cmd.Parameters.AddWithValue("@now_utc", opened.ConnectedAtUtc);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteSessionsClosedAsync(
        MySqlConnection connection, MySqlTransaction tx,
        List<PlayerSessionClosed> sessions,
        Dictionary<ulong, ulong> playerCache,
        CancellationToken cancellationToken)
    {
        foreach (var closed in sessions)
        {
            var playerId = await EnsurePlayerIdAsync(connection, tx, closed.SteamId64, closed.DisconnectedAtUtc, playerCache, cancellationToken).ConfigureAwait(false);

            await using var updateCmd = new MySqlCommand(@"
UPDATE player_sessions
SET disconnected_at_utc = @disconnected_at_utc,
    disconnect_reason = @disconnect_reason,
    server_current_time_disconnect = @server_time,
    updated_at_utc = @updated_at_utc
WHERE player_id = @player_id
  AND disconnected_at_utc IS NULL
ORDER BY connected_at_utc DESC
LIMIT 1", connection, tx);

            updateCmd.Parameters.AddWithValue("@disconnected_at_utc", closed.DisconnectedAtUtc);
            updateCmd.Parameters.AddWithValue("@disconnect_reason", closed.DisconnectReason);
            updateCmd.Parameters.AddWithValue("@server_time", closed.ServerCurrentTimeSeconds);
            updateCmd.Parameters.AddWithValue("@updated_at_utc", closed.DisconnectedAtUtc);
            updateCmd.Parameters.AddWithValue("@player_id", playerId);

            var affected = await updateCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affected > 0) continue;

            var mapSessionId = await EnsureOpenMapSessionIdAsync(connection, tx, string.Empty, closed.DisconnectedAtUtc, cancellationToken).ConfigureAwait(false);

            await using var insertCmd = new MySqlCommand(@"
INSERT INTO player_sessions (
    player_id, map_session_id, connected_at_utc, disconnected_at_utc, disconnect_reason,
    server_current_time_connect, server_current_time_disconnect, created_at_utc, updated_at_utc
) VALUES (
    @player_id, @map_session_id, @connected_at_utc, @disconnected_at_utc, @disconnect_reason,
    @server_connect, @server_disconnect, @created_at_utc, @updated_at_utc
)", connection, tx);

            insertCmd.Parameters.AddWithValue("@player_id", playerId);
            insertCmd.Parameters.AddWithValue("@map_session_id", mapSessionId);
            insertCmd.Parameters.AddWithValue("@connected_at_utc", closed.DisconnectedAtUtc);
            insertCmd.Parameters.AddWithValue("@disconnected_at_utc", closed.DisconnectedAtUtc);
            insertCmd.Parameters.AddWithValue("@disconnect_reason", closed.DisconnectReason);
            insertCmd.Parameters.AddWithValue("@server_connect", closed.ServerCurrentTimeSeconds);
            insertCmd.Parameters.AddWithValue("@server_disconnect", closed.ServerCurrentTimeSeconds);
            insertCmd.Parameters.AddWithValue("@created_at_utc", closed.DisconnectedAtUtc);
            insertCmd.Parameters.AddWithValue("@updated_at_utc", closed.DisconnectedAtUtc);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteRoundsStartedAsync(
        MySqlConnection connection, MySqlTransaction tx,
        List<RoundStarted> rounds,
        Dictionary<string, ulong> mapCache,
        CancellationToken cancellationToken)
    {
        foreach (var round in rounds)
        {
            var mapSessionId = await EnsureOpenMapSessionIdAsync(connection, tx, round.MapName, round.StartedAtUtc, cancellationToken).ConfigureAwait(false);
            mapCache[round.MapName] = mapSessionId;

            await using var cmd = new MySqlCommand(@"
INSERT INTO rounds (
    map_session_id, round_number, started_at_utc, created_at_utc, updated_at_utc
) VALUES (
    @map_session_id, @round_number, @started_at_utc, @created_at_utc, @updated_at_utc
)
ON DUPLICATE KEY UPDATE
    started_at_utc = VALUES(started_at_utc),
    updated_at_utc = VALUES(updated_at_utc)", connection, tx);

            cmd.Parameters.AddWithValue("@map_session_id", mapSessionId);
            cmd.Parameters.AddWithValue("@round_number", round.RoundNumber);
            cmd.Parameters.AddWithValue("@started_at_utc", round.StartedAtUtc);
            cmd.Parameters.AddWithValue("@created_at_utc", round.StartedAtUtc);
            cmd.Parameters.AddWithValue("@updated_at_utc", round.StartedAtUtc);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteRoundsEndedAsync(
        MySqlConnection connection, MySqlTransaction tx,
        List<RoundEnded> rounds,
        Dictionary<string, ulong> mapCache,
        CancellationToken cancellationToken)
    {
        foreach (var round in rounds)
        {
            var mapSessionId = await EnsureOpenMapSessionIdAsync(connection, tx, round.MapName, round.EndedAtUtc, cancellationToken).ConfigureAwait(false);
            mapCache[round.MapName] = mapSessionId;

            await using var cmd = new MySqlCommand(@"
UPDATE rounds
SET ended_at_utc = @ended_at_utc,
    end_reason = @end_reason,
    end_message = @end_message,
    player_count_at_end = @player_count_at_end,
    round_time_seconds = @round_time_seconds,
    updated_at_utc = @updated_at_utc
WHERE map_session_id = @map_session_id
  AND round_number = @round_number", connection, tx);

            cmd.Parameters.AddWithValue("@ended_at_utc", round.EndedAtUtc);
            cmd.Parameters.AddWithValue("@end_reason", round.EndReason);
            cmd.Parameters.AddWithValue("@end_message", round.EndMessage);
            cmd.Parameters.AddWithValue("@player_count_at_end", round.PlayerCountAtEnd);
            cmd.Parameters.AddWithValue("@round_time_seconds", round.RoundTimeSeconds);
            cmd.Parameters.AddWithValue("@updated_at_utc", round.EndedAtUtc);
            cmd.Parameters.AddWithValue("@map_session_id", mapSessionId);
            cmd.Parameters.AddWithValue("@round_number", round.RoundNumber);

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affected > 0) continue;

            await using var insert = new MySqlCommand(@"
INSERT INTO rounds (
    map_session_id, round_number, started_at_utc, ended_at_utc, end_reason, end_message,
    player_count_at_end, round_time_seconds, created_at_utc, updated_at_utc
) VALUES (
    @map_session_id, @round_number, @started_at_utc, @ended_at_utc, @end_reason, @end_message,
    @player_count_at_end, @round_time_seconds, @created_at_utc, @updated_at_utc
)", connection, tx);

            insert.Parameters.AddWithValue("@map_session_id", mapSessionId);
            insert.Parameters.AddWithValue("@round_number", round.RoundNumber);
            insert.Parameters.AddWithValue("@started_at_utc", round.EndedAtUtc);
            insert.Parameters.AddWithValue("@ended_at_utc", round.EndedAtUtc);
            insert.Parameters.AddWithValue("@end_reason", round.EndReason);
            insert.Parameters.AddWithValue("@end_message", round.EndMessage);
            insert.Parameters.AddWithValue("@player_count_at_end", round.PlayerCountAtEnd);
            insert.Parameters.AddWithValue("@round_time_seconds", round.RoundTimeSeconds);
            insert.Parameters.AddWithValue("@created_at_utc", round.EndedAtUtc);
            insert.Parameters.AddWithValue("@updated_at_utc", round.EndedAtUtc);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WritePlayerDeathsAsync(
        MySqlConnection connection, MySqlTransaction tx,
        List<PlayerDeathEvent> deaths,
        Dictionary<ulong, ulong> playerCache,
        CancellationToken cancellationToken)
    {
        foreach (var death in deaths)
        {
            var mapSessionId = await EnsureOpenMapSessionIdAsync(connection, tx, string.Empty, death.OccurredAtUtc, cancellationToken).ConfigureAwait(false);
            var attackerId = await EnsureOptionalPlayerIdAsync(connection, tx, death.AttackerSteamId64, death.OccurredAtUtc, playerCache, cancellationToken).ConfigureAwait(false);
            var victimId = await EnsureOptionalPlayerIdAsync(connection, tx, death.VictimSteamId64, death.OccurredAtUtc, playerCache, cancellationToken).ConfigureAwait(false);
            var assisterId = await EnsureOptionalPlayerIdAsync(connection, tx, death.AssisterSteamId64, death.OccurredAtUtc, playerCache, cancellationToken).ConfigureAwait(false);

            await using var cmd = new MySqlCommand(@"
INSERT INTO kill_events (
    map_session_id, round_id, occurred_at_utc, attacker_player_id, victim_player_id, assister_player_id,
    weapon_name, is_headshot, hitgroup, penetrated, noscope, thrusmoke, distance,
    attacker_blind, attacker_in_air, assisted_flash, created_at_utc
) VALUES (
    @map_session_id, NULL, @occurred_at_utc, @attacker_player_id, @victim_player_id, @assister_player_id,
    @weapon_name, @is_headshot, @hitgroup, @penetrated, @noscope, @thrusmoke, @distance,
    @attacker_blind, @attacker_in_air, @assisted_flash, @created_at_utc
)", connection, tx);

            cmd.Parameters.AddWithValue("@map_session_id", mapSessionId);
            cmd.Parameters.AddWithValue("@occurred_at_utc", death.OccurredAtUtc);
            cmd.Parameters.AddWithValue("@attacker_player_id", attackerId);
            cmd.Parameters.AddWithValue("@victim_player_id", victimId);
            cmd.Parameters.AddWithValue("@assister_player_id", assisterId);
            cmd.Parameters.AddWithValue("@weapon_name", death.Weapon);
            cmd.Parameters.AddWithValue("@is_headshot", death.IsHeadshot);
            cmd.Parameters.AddWithValue("@hitgroup", death.Hitgroup);
            cmd.Parameters.AddWithValue("@penetrated", death.Penetrated);
            cmd.Parameters.AddWithValue("@noscope", death.NoScope);
            cmd.Parameters.AddWithValue("@thrusmoke", death.ThroughSmoke);
            cmd.Parameters.AddWithValue("@distance", death.Distance);
            cmd.Parameters.AddWithValue("@attacker_blind", death.AttackerBlind);
            cmd.Parameters.AddWithValue("@attacker_in_air", death.AttackerInAir);
            cmd.Parameters.AddWithValue("@assisted_flash", death.AssistedFlash);
            cmd.Parameters.AddWithValue("@created_at_utc", death.OccurredAtUtc);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WritePlayerActionsAsync(
        MySqlConnection connection, MySqlTransaction tx,
        List<PlayerActionEvent> actions,
        Dictionary<ulong, ulong> playerCache,
        Dictionary<(ulong MapSessionId, int RoundNumber), ulong> roundCache,
        CancellationToken cancellationToken)
    {
        foreach (var action in actions)
        {
            var mapSessionId = await EnsureOpenMapSessionIdAsync(connection, tx, string.Empty, action.OccurredAtUtc, cancellationToken).ConfigureAwait(false);
            var playerId = await EnsurePlayerIdAsync(connection, tx, action.SteamId64, action.OccurredAtUtc, playerCache, cancellationToken).ConfigureAwait(false);

            ulong? roundId = null;
            if (action.RoundNumber.HasValue)
                roundId = await TryGetRoundIdAsync(connection, tx, mapSessionId, action.RoundNumber.Value, roundCache, cancellationToken).ConfigureAwait(false);

            await using var cmd = new MySqlCommand(@"
INSERT INTO player_action_events (
    player_id, map_session_id, round_id, occurred_at_utc, action_type, action_value, created_at_utc
) VALUES (
    @player_id, @map_session_id, @round_id, @occurred_at_utc, @action_type, @action_value, @created_at_utc
)", connection, tx);

            cmd.Parameters.AddWithValue("@player_id", playerId);
            cmd.Parameters.AddWithValue("@map_session_id", mapSessionId);
            cmd.Parameters.AddWithValue("@round_id", roundId);
            cmd.Parameters.AddWithValue("@occurred_at_utc", action.OccurredAtUtc);
            cmd.Parameters.AddWithValue("@action_type", action.ActionType);
            cmd.Parameters.AddWithValue("@action_value", action.ActionValue);
            cmd.Parameters.AddWithValue("@created_at_utc", action.OccurredAtUtc);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WritePresenceSnapshotsAsync(
        MySqlConnection connection, MySqlTransaction tx,
        List<PresenceSnapshot> snapshots,
        Dictionary<ulong, ulong> playerCache,
        CancellationToken cancellationToken)
    {
        foreach (var snapshot in snapshots)
        {
            var mapSessionId = await EnsureOpenMapSessionIdAsync(connection, tx, snapshot.MapName, snapshot.CapturedAtUtc, cancellationToken).ConfigureAwait(false);

            ulong snapshotId;
            await using (var insertSnapshot = new MySqlCommand(@"
INSERT INTO presence_snapshots (
    map_session_id, captured_at_utc, connected_player_count, created_at_utc
) VALUES (
    @map_session_id, @captured_at_utc, @connected_player_count, @created_at_utc
);
SELECT LAST_INSERT_ID();", connection, tx))
            {
                insertSnapshot.Parameters.AddWithValue("@map_session_id", mapSessionId);
                insertSnapshot.Parameters.AddWithValue("@captured_at_utc", snapshot.CapturedAtUtc);
                insertSnapshot.Parameters.AddWithValue("@connected_player_count", snapshot.ConnectedCount);
                insertSnapshot.Parameters.AddWithValue("@created_at_utc", snapshot.CapturedAtUtc);
                snapshotId = Convert.ToUInt64(await insertSnapshot.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            }

            foreach (var player in snapshot.Players)
            {
                var playerId = await EnsurePlayerIdAsync(connection, tx, player.SteamId64, snapshot.CapturedAtUtc, playerCache, cancellationToken).ConfigureAwait(false);

                await using var insertPlayer = new MySqlCommand(@"
INSERT INTO presence_snapshot_players (
    presence_snapshot_id, player_id, slot_index, team_value, created_at_utc
) VALUES (
    @presence_snapshot_id, @player_id, @slot_index, @team_value, @created_at_utc
)", connection, tx);

                insertPlayer.Parameters.AddWithValue("@presence_snapshot_id", snapshotId);
                insertPlayer.Parameters.AddWithValue("@player_id", playerId);
                insertPlayer.Parameters.AddWithValue("@slot_index", player.Slot);
                insertPlayer.Parameters.AddWithValue("@team_value", player.Team);
                insertPlayer.Parameters.AddWithValue("@created_at_utc", snapshot.CapturedAtUtc);
                await insertPlayer.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<ulong?> EnsureOptionalPlayerIdAsync(
        MySqlConnection connection, MySqlTransaction tx,
        ulong? steamId64, DateTime atUtc,
        Dictionary<ulong, ulong> playerCache,
        CancellationToken cancellationToken)
    {
        if (!steamId64.HasValue) return null;
        return await EnsurePlayerIdAsync(connection, tx, steamId64.Value, atUtc, playerCache, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ulong> EnsurePlayerIdAsync(
        MySqlConnection connection, MySqlTransaction tx,
        ulong steamId64, DateTime atUtc,
        Dictionary<ulong, ulong> playerCache,
        CancellationToken cancellationToken)
    {
        if (playerCache.TryGetValue(steamId64, out var cachedId)) return cachedId;

        await using (var upsert = new MySqlCommand(@"
INSERT INTO players (
    steam_id64, first_seen_utc, last_seen_utc, created_at_utc, updated_at_utc
) VALUES (
    @steam_id64, @at_utc, @at_utc, @at_utc, @at_utc
)
ON DUPLICATE KEY UPDATE
    last_seen_utc = VALUES(last_seen_utc),
    updated_at_utc = VALUES(updated_at_utc)", connection, tx))
        {
            upsert.Parameters.AddWithValue("@steam_id64", steamId64);
            upsert.Parameters.AddWithValue("@at_utc", atUtc);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var select = new MySqlCommand("SELECT player_id FROM players WHERE steam_id64 = @steam_id64 LIMIT 1", connection, tx);
        select.Parameters.AddWithValue("@steam_id64", steamId64);

        var value = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var playerId = Convert.ToUInt64(value);
        playerCache[steamId64] = playerId;
        return playerId;
    }

    private static async Task<ulong> EnsureOpenMapSessionIdAsync(
        MySqlConnection connection, MySqlTransaction tx,
        string? mapName, DateTime atUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            await using var anyOpen = new MySqlCommand(@"
SELECT map_session_id FROM map_sessions
WHERE ended_at_utc IS NULL
ORDER BY started_at_utc DESC
LIMIT 1", connection, tx);

            var openId = await anyOpen.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (openId is not null) return Convert.ToUInt64(openId);

            mapName = "unknown";
        }

        await using (var findOpen = new MySqlCommand(@"
SELECT map_session_id FROM map_sessions
WHERE map_name = @map_name
  AND ended_at_utc IS NULL
ORDER BY started_at_utc DESC
LIMIT 1", connection, tx))
        {
            findOpen.Parameters.AddWithValue("@map_name", mapName);
            var existing = await findOpen.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null) return Convert.ToUInt64(existing);
        }

        await using var insert = new MySqlCommand(@"
INSERT INTO map_sessions (
    map_name, started_at_utc, server_current_time_start, created_at_utc, updated_at_utc
) VALUES (
    @map_name, @started_at_utc, NULL, @created_at_utc, @updated_at_utc
);
SELECT LAST_INSERT_ID();", connection, tx);

        insert.Parameters.AddWithValue("@map_name", mapName);
        insert.Parameters.AddWithValue("@started_at_utc", atUtc);
        insert.Parameters.AddWithValue("@created_at_utc", atUtc);
        insert.Parameters.AddWithValue("@updated_at_utc", atUtc);

        var inserted = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToUInt64(inserted);
    }

    private static async Task<ulong?> TryGetRoundIdAsync(
        MySqlConnection connection, MySqlTransaction tx,
        ulong mapSessionId, int roundNumber,
        Dictionary<(ulong MapSessionId, int RoundNumber), ulong> cache,
        CancellationToken cancellationToken)
    {
        var key = (mapSessionId, roundNumber);
        if (cache.TryGetValue(key, out var cached)) return cached;

        await using var cmd = new MySqlCommand(@"
SELECT round_id FROM rounds
WHERE map_session_id = @map_session_id
  AND round_number = @round_number
LIMIT 1", connection, tx);

        cmd.Parameters.AddWithValue("@map_session_id", mapSessionId);
        cmd.Parameters.AddWithValue("@round_number", roundNumber);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null) return null;

        var roundId = Convert.ToUInt64(result);
        cache[key] = roundId;
        return roundId;
    }
}
