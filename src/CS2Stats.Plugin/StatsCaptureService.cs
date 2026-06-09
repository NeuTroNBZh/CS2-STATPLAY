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
    private readonly StatsModulesSettings _modules;
    private readonly int _maxBufferedEvents;

    private string _currentMap;
    private int _roundNumber;
    private DateTime? _roundStartedAt;

    public StatsCaptureService(string? initialMapName = null, StatsModulesSettings? modules = null, SyncSettings? sync = null)
    {
        _currentMap = initialMapName ?? TryGetMapNameSafely() ?? "unknown";
        _modules = modules ?? new StatsModulesSettings();
        _maxBufferedEvents = sync?.MaxBufferedEvents ?? 5000;
    }

    private static string? TryGetMapNameSafely()
    {
        try { return Server.MapName; }
        catch { return null; }
    }

    private static double? TryGetServerCurrentTime()
    {
        try { return Server.CurrentTime; }
        catch { return null; }
    }

    private static int? TryGetPlayerCount()
    {
        try { return Utilities.GetPlayers().Count; }
        catch { return null; }
    }

    private bool IsBufferFull()
    {
        return _buffer.SessionOpened.Count +
               _buffer.SessionClosed.Count +
               _buffer.RoundStarted.Count +
               _buffer.RoundEnded.Count +
               _buffer.PlayerDeaths.Count +
               _buffer.PlayerActions.Count +
               _buffer.PresenceSnapshots.Count >= _maxBufferedEvents;
    }

    public void OnMapStart(string mapName)
    {
        lock (_gate)
        {
            _currentMap = mapName;
            _roundNumber = 0;
            _roundStartedAt = null;
        }
    }

    public void OnRoundStart(EventRoundStart @event)
    {
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            _roundNumber++;
            _roundStartedAt = now;
            if (IsBufferFull()) return;
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
        var playerCount = TryGetPlayerCount();
        lock (_gate)
        {
            if (IsBufferFull()) return;
            var roundTimeSeconds = _roundStartedAt.HasValue
                ? (int)(now - _roundStartedAt.Value).TotalSeconds
                : (int?)null;
            _buffer.RoundEnded.Add(new RoundEnded(
                _currentMap,
                _roundNumber,
                now,
                (int)@event.Reason,
                @event.Message,
                playerCount,
                roundTimeSeconds
            ));
        }
    }

    public void OnPlayerConnectFull(EventPlayerConnectFull @event)
    {
        if (!_modules.SessionTrackingEnabled) return;
        var steamId = TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue) return;

        var now = DateTime.UtcNow;
        var serverTime = TryGetServerCurrentTime();
        lock (_gate)
        {
            if (IsBufferFull()) return;
            _buffer.SessionOpened.Add(new PlayerSessionOpened(
                steamId.Value,
                now,
                _currentMap,
                serverTime
            ));
        }
    }

    public void OnPlayerDisconnect(EventPlayerDisconnect @event)
    {
        if (!_modules.SessionTrackingEnabled) return;
        var steamId = @event.Xuid > 0 ? @event.Xuid : TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue) return;

        var now = DateTime.UtcNow;
        var serverTime = TryGetServerCurrentTime();
        lock (_gate)
        {
            if (IsBufferFull()) return;
            _buffer.SessionClosed.Add(new PlayerSessionClosed(
                steamId.Value,
                now,
                @event.Reason.ToString(),
                serverTime
            ));
        }
    }

    public void OnPlayerDeath(EventPlayerDeath @event)
    {
        if (!_modules.KdaEnabled) return;
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

        lock (_gate)
        {
            if (!IsBufferFull())
                _buffer.PlayerDeaths.Add(death);
        }
    }

    public void OnWeaponFire(EventWeaponFire @event)
    {
        if (!_modules.WeaponFireEnabled) return;
        var steamId = TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue) return;

        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (IsBufferFull()) return;
            _buffer.PlayerActions.Add(new PlayerActionEvent(
                steamId.Value,
                now,
                "weapon_fire",
                @event.Weapon,
                _roundNumber > 0 ? _roundNumber : null
            ));
        }
    }

    public void OnBombPlanted(EventBombPlanted @event) =>
        AppendBombAction("bomb_planted", @event.Userid, @event.Site);

    public void OnBombDefused(EventBombDefused @event) =>
        AppendBombAction("bomb_defused", @event.Userid, @event.Site);

    public void OnRoundMvp(EventRoundMvp @event)
    {
        if (!_modules.ObjectiveStatsEnabled) return;
        var steamId = TryGetSteamId64(@event.Userid);
        if (!steamId.HasValue) return;

        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (IsBufferFull()) return;
            _buffer.PlayerActions.Add(new PlayerActionEvent(
                steamId.Value,
                now,
                "round_mvp",
                $"reason={@event.Reason};value={@event.Value}",
                _roundNumber > 0 ? _roundNumber : null
            ));
        }
    }

    public void OnHegrenadeDetonate(EventHegrenadeDetonate @event) =>
        AppendGrenadeAction(@event.Userid, "hegrenade");

    public void OnFlashbangDetonate(EventFlashbangDetonate @event) =>
        AppendGrenadeAction(@event.Userid, "flashbang");

    public void OnSmokeGrenadeDetonate(EventSmokegrenadeDetonate @event) =>
        AppendGrenadeAction(@event.Userid, "smokegrenade");

    public void OnMolotovDetonate(EventMolotovDetonate @event) =>
        AppendGrenadeAction(@event.Userid, "molotov");

    public void CapturePresenceSnapshot()
    {
        if (!_modules.PresenceSnapshotsEnabled) return;
        var players = Utilities.GetPlayers();
        var now = DateTime.UtcNow;
        var identities = new List<PlayerIdentity>(players.Count);

        foreach (var player in players)
        {
            var steamId = TryGetSteamId64(player);
            if (!steamId.HasValue) continue;
            identities.Add(new PlayerIdentity(
                steamId.Value,
                player.UserId,
                player.Slot,
                (int)player.Team
            ));
        }

        lock (_gate)
        {
            if (IsBufferFull()) return;
            _buffer.PresenceSnapshots.Add(new PresenceSnapshot(
                now,
                _currentMap,
                identities.Count,
                identities
            ));
        }
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
        if (!_modules.ObjectiveStatsEnabled) return;
        var steamId = TryGetSteamId64(player);
        if (!steamId.HasValue) return;

        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (IsBufferFull()) return;
            _buffer.PlayerActions.Add(new PlayerActionEvent(
                steamId.Value,
                now,
                actionType,
                site.ToString(),
                _roundNumber > 0 ? _roundNumber : null
            ));
        }
    }

    private void AppendGrenadeAction(CCSPlayerController? player, string grenadeType)
    {
        if (!_modules.GrenadeStatsEnabled) return;
        var steamId = TryGetSteamId64(player);
        if (!steamId.HasValue) return;

        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (IsBufferFull()) return;
            _buffer.PlayerActions.Add(new PlayerActionEvent(
                steamId.Value,
                now,
                "grenade_detonation",
                grenadeType,
                _roundNumber > 0 ? _roundNumber : null
            ));
        }
    }

    private static ulong? TryGetSteamId64(CCSPlayerController? player)
    {
        if (player is null) return null;
        try { return player.AuthorizedSteamID?.SteamId64; }
        catch { return null; }
    }

    private static int? ToNullableInt(long value)
    {
        return value > int.MaxValue || value < int.MinValue ? null : (int)value;
    }
}
