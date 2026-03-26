using System;
using System.Collections.Generic;
using CS2Stats.Contracts;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CS2Stats.Plugin;

public sealed class StatsCaptureService
{
    private readonly object _gate = new();
    private readonly StatsBatch _buffer = new();

    private string _currentMap;
    private int _roundNumber;

    /// <summary>
    /// Constructeur avec initialisation du map name (paramètre optionnel pour les tests).
    /// </summary>
    public StatsCaptureService(string? initialMapName = null)
    {
        _currentMap = initialMapName ?? TryGetMapNameSafely() ?? "unknown";
    }

    /// <summary>
    /// Tenter de récupérer le nom de la map depuis l'API CS2 (via Server.MapName).
    /// Retourne null si la DLL CounterStrikeSharp n'est pas chargée.
    /// </summary>
    private static string? TryGetMapNameSafely()
    {
        try
        {
            return Server.MapName;
        }
        catch
        {
            // La DLL est probablement pas disponible (test unitaire hors du serveur)
            return null;
        }
    }

    public void OnMapStart(string mapName)
    {
        lock (_gate)
        {
            _currentMap = mapName;
            _roundNumber = 0;
        }
    }

    public void OnRoundStart(EventRoundStart @event)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            _roundNumber++;
            _buffer.RoundStarted.Add(new RoundStarted(
                _currentMap,
                _roundNumber,
                now,
                ToNullableInt(@event.Fraglimit),
                ToNullableInt(@event.Timelimit),
                @event.Objective
            ));
        }
    }

    public void OnRoundEnd(EventRoundEnd @event)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            _buffer.RoundEnded.Add(new RoundEnded(
                _currentMap,
                _roundNumber,
                now,
                null,
                null,
                null,
                null
            ));
        }
    }

    public void OnPlayerConnectFull(EventPlayerConnectFull @event)
    {
        var steamId = TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue)
        {
            return;
        }

        _buffer.SessionOpened.Add(new PlayerSessionOpened(
            steamId.Value,
            DateTime.UtcNow,
            Server.MapName,
            Server.CurrentTime
        ));
    }

    public void OnPlayerDisconnect(EventPlayerDisconnect @event)
    {
        var steamId = @event.Xuid > 0 ? @event.Xuid : TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue)
        {
            return;
        }

        _buffer.SessionClosed.Add(new PlayerSessionClosed(
            steamId.Value,
            DateTime.UtcNow,
            @event.Reason.ToString(),
            Server.CurrentTime
        ));
    }

    public void OnPlayerDeath(EventPlayerDeath @event)
    {
        var death = new PlayerDeathEvent(
            TryGetSteamId64(@event.Attacker),
            TryGetSteamId64(@event.Userid),
            TryGetSteamId64(@event.Assister),
            DateTime.UtcNow,
            @event.Weapon,
            @event.Headshot,
            @event.Hitgroup,
            @event.Penetrated,
            @event.Noscope,
            @event.Thrusmoke,
            @event.Distance,
            @event.Attackerblind,
            @event.Attackerinair,
            @event.Assistedflash
        );

        _buffer.PlayerDeaths.Add(death);
    }

    public void OnWeaponFire(EventWeaponFire @event)
    {
        var steamId = TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue)
        {
            return;
        }

        _buffer.PlayerActions.Add(new PlayerActionEvent(
            steamId.Value,
            DateTime.UtcNow,
            "weapon_fire",
            @event.Weapon,
            _roundNumber > 0 ? _roundNumber : null
        ));
    }

    public void OnBombPlanted(EventBombPlanted @event)
    {
        AppendBombAction("bomb_planted", @event.Userid, @event.Site);
    }

    public void OnBombDefused(EventBombDefused @event)
    {
        AppendBombAction("bomb_defused", @event.Userid, @event.Site);
    }

    public void OnRoundMvp(EventRoundMvp @event)
    {
        var steamId = TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue)
        {
            return;
        }

        _buffer.PlayerActions.Add(new PlayerActionEvent(
            steamId.Value,
            DateTime.UtcNow,
            "round_mvp",
            $"reason={@event.Reason};value={@event.Value}",
            _roundNumber > 0 ? _roundNumber : null
        ));
    }

    public void CapturePresenceSnapshot()
    {
        var players = Utilities.GetPlayers();
        var identities = new List<PlayerIdentity>(players.Count);

        foreach (var player in players)
        {
            var steamId = TryGetSteamId64(player);
            if (!steamId.HasValue)
            {
                continue;
            }

            identities.Add(new PlayerIdentity(
                steamId.Value,
                player.UserId,
                player.Slot,
                (int)player.Team
            ));
        }

        _buffer.PresenceSnapshots.Add(new PresenceSnapshot(
            DateTime.UtcNow,
            Server.MapName,
            identities.Count,
            identities
        ));
    }

    public StatsBatch DrainBatch()
    {
        lock (_gate)
        {
            var drained = new StatsBatch();
            drained.SessionOpened.AddRange(_buffer.SessionOpened);
            drained.SessionClosed.AddRange(_buffer.SessionClosed);
            drained.RoundStarted.AddRange(_buffer.RoundStarted);
            drained.RoundEnded.AddRange(_buffer.RoundEnded);
            drained.PlayerDeaths.AddRange(_buffer.PlayerDeaths);
            drained.PlayerActions.AddRange(_buffer.PlayerActions);
            drained.PresenceSnapshots.AddRange(_buffer.PresenceSnapshots);

            _buffer.SessionOpened.Clear();
            _buffer.SessionClosed.Clear();
            _buffer.RoundStarted.Clear();
            _buffer.RoundEnded.Clear();
            _buffer.PlayerDeaths.Clear();
            _buffer.PlayerActions.Clear();
            _buffer.PresenceSnapshots.Clear();

            return drained;
        }
    }

    private void AppendBombAction(string actionType, CCSPlayerController? player, int site)
    {
        var steamId = TryGetSteamId64(player);
        if (!steamId.HasValue)
        {
            return;
        }

        _buffer.PlayerActions.Add(new PlayerActionEvent(
            steamId.Value,
            DateTime.UtcNow,
            actionType,
            site.ToString(),
            _roundNumber > 0 ? _roundNumber : null
        ));
    }

    private static ulong? TryGetSteamId64(CCSPlayerController? player)
    {
        if (player is null)
        {
            return null;
        }

        try
        {
            return player.AuthorizedSteamID?.SteamId64;
        }
        catch
        {
            return null;
        }
    }

    private static int? ToNullableInt(long value)
    {
        return value > int.MaxValue || value < int.MinValue ? null : (int)value;
    }

}
