using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace CS2Stats.Plugin;

public sealed class StatsCommandService
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    public StatsCommandService(string connectionString, ILogger logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task ShowStatsAsync(CCSPlayerController requester, CCSPlayerController target)
    {
        var steamId = target.AuthorizedSteamID?.SteamId64;
        if (steamId == null)
        {
            requester.PrintToChat($" {ChatColors.Red}[CS2Stats] SteamID introuvable.");
            return;
        }

        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.display_name, pls.kills, pls.deaths, pls.assists,
                       pls.headshots, pls.mvps, pls.rounds_played,
                       pls.total_playtime_seconds,
                       COALESCE(
                           (SELECT SUM(ke.dmg_health) FROM kill_events ke
                            WHERE ke.attacker_player_id = pls.player_id),
                           0
                       ) AS total_dmg
                FROM player_lifetime_stats pls
                JOIN players p ON p.player_id = pls.player_id
                WHERE p.steam_id64 = @steam_id64";
            cmd.Parameters.AddWithValue("@steam_id64", steamId.Value);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                Server.NextFrame(() =>
                {
                    if (requester.IsValid)
                        requester.PrintToChat($" {ChatColors.Grey}[CS2Stats] Aucune statistique trouvée.");
                });
                return;
            }

            var name = reader.IsDBNull(0) ? target.PlayerName : reader.GetString(0);
            var kills = reader.GetInt64(1);
            var deaths = reader.GetInt64(2);
            var assists = reader.GetInt64(3);
            var headshots = reader.GetInt64(4);
            var mvps = reader.GetInt64(5);
            var rounds = reader.GetInt64(6);
            var playtimeSeconds = reader.GetInt64(7);
            var totalDmg = reader.GetInt64(8);

            var kd = deaths > 0 ? (double)kills / deaths : (double)kills;
            var hsPercent = kills > 0 ? (double)headshots / kills * 100.0 : 0.0;
            var adr = rounds > 0 ? (double)totalDmg / rounds : 0.0;
            var hours = playtimeSeconds / 3600;
            var minutes = (playtimeSeconds % 3600) / 60;

            Server.NextFrame(() =>
            {
                if (!requester.IsValid) return;
                requester.PrintToChat($" {ChatColors.White}---- [CS2Stats] {ChatColors.Green}{name}{ChatColors.White} ----");
                requester.PrintToChat($" Kills: {ChatColors.Green}{kills}  {ChatColors.White}Deaths: {ChatColors.Red}{deaths}  {ChatColors.White}Assists: {ChatColors.Yellow}{assists}");
                requester.PrintToChat($" K/D: {ChatColors.Green}{kd:F2}  {ChatColors.White}HS: {ChatColors.Green}{hsPercent:F1}%  {ChatColors.White}ADR: {ChatColors.Green}{adr:F1}");
                requester.PrintToChat($" MVPs: {ChatColors.Green}{mvps}  {ChatColors.White}Rounds: {rounds}  Temps: {ChatColors.Green}{hours}h{minutes:D2}m");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to fetch stats for {SteamId}", steamId);
            Server.NextFrame(() =>
            {
                if (requester.IsValid)
                    requester.PrintToChat($" {ChatColors.Red}[CS2Stats] Erreur lors de la récupération des stats.");
            });
        }
    }

    public async Task ShowRankAsync(CCSPlayerController requester)
    {
        var steamId = requester.AuthorizedSteamID?.SteamId64;
        if (steamId == null)
        {
            requester.PrintToChat($" {ChatColors.Red}[CS2Stats] SteamID introuvable.");
            return;
        }

        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    (SELECT COUNT(*) + 1 FROM player_lifetime_stats
                     WHERE kills > pls.kills) AS rank,
                    (SELECT COUNT(*) FROM player_lifetime_stats) AS total,
                    pls.kills
                FROM player_lifetime_stats pls
                JOIN players p ON p.player_id = pls.player_id
                WHERE p.steam_id64 = @steam_id64";
            cmd.Parameters.AddWithValue("@steam_id64", steamId.Value);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            if (!await reader.ReadAsync().ConfigureAwait(false))
            {
                Server.NextFrame(() =>
                {
                    if (requester.IsValid)
                        requester.PrintToChat($" {ChatColors.Grey}[CS2Stats] Tu n'as pas encore de statistiques.");
                });
                return;
            }

            var rank = reader.GetInt64(0);
            var total = reader.GetInt64(1);
            var kills = reader.GetInt64(2);

            Server.NextFrame(() =>
            {
                if (requester.IsValid)
                    requester.PrintToChat($" {ChatColors.White}[CS2Stats] Rang {ChatColors.Green}#{rank}{ChatColors.White} / {total} joueurs {ChatColors.Grey}({kills} kills)");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to fetch rank for {SteamId}", steamId);
            Server.NextFrame(() =>
            {
                if (requester.IsValid)
                    requester.PrintToChat($" {ChatColors.Red}[CS2Stats] Erreur lors de la récupération du rang.");
            });
        }
    }

    public async Task ShowTopAsync(CCSPlayerController requester)
    {
        try
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT p.display_name, pls.kills, pls.deaths
                FROM player_lifetime_stats pls
                JOIN players p ON p.player_id = pls.player_id
                ORDER BY pls.kills DESC
                LIMIT 5";

            var lines = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            var pos = 1;
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var name = reader.IsDBNull(0) ? "Inconnu" : reader.GetString(0);
                var kills = reader.GetInt64(1);
                var deaths = reader.GetInt64(2);
                var kd = deaths > 0 ? (double)kills / deaths : (double)kills;
                lines.Add($" {ChatColors.Yellow}#{pos}  {ChatColors.Green}{name}  {ChatColors.White}K: {ChatColors.Green}{kills}  {ChatColors.White}K/D: {ChatColors.Green}{kd:F2}");
                pos++;
            }

            Server.NextFrame(() =>
            {
                if (!requester.IsValid) return;
                requester.PrintToChat($" {ChatColors.White}---- [CS2Stats] Top 5 ----");
                if (lines.Count == 0)
                {
                    requester.PrintToChat($" {ChatColors.Grey}Aucun joueur dans le classement.");
                    return;
                }
                foreach (var line in lines)
                    requester.PrintToChat(line);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CS2Stats] Failed to fetch top players");
            Server.NextFrame(() =>
            {
                if (requester.IsValid)
                    requester.PrintToChat($" {ChatColors.Red}[CS2Stats] Erreur lors de la récupération du classement.");
            });
        }
    }
}
