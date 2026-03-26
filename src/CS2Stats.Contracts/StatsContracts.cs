using System;
using System.Collections.Generic;

namespace CS2Stats.Contracts;

public sealed record PlayerIdentity(
    ulong SteamId64,
    int? UserId,
    int? Slot,
    int? Team
);

public sealed record PlayerSessionOpened(
    ulong SteamId64,
    DateTime ConnectedAtUtc,
    string MapName,
    double? ServerCurrentTimeSeconds
);

public sealed record PlayerSessionClosed(
    ulong SteamId64,
    DateTime DisconnectedAtUtc,
    string? DisconnectReason,
    double? ServerCurrentTimeSeconds
);

public sealed record RoundStarted(
    string MapName,
    int RoundNumber,
    DateTime StartedAtUtc,
    int? FragLimit,
    int? TimeLimit,
    string? Objective
);

public sealed record RoundEnded(
    string MapName,
    int RoundNumber,
    DateTime EndedAtUtc,
    int? EndReason,
    string? EndMessage,
    int? PlayerCountAtEnd,
    int? RoundTimeSeconds
);

public sealed record PlayerDeathEvent(
    ulong? AttackerSteamId64,
    ulong? VictimSteamId64,
    ulong? AssisterSteamId64,
    DateTime OccurredAtUtc,
    string? Weapon,
    bool IsHeadshot,
    int? Hitgroup,
    int? Penetrated,
    bool NoScope,
    bool ThroughSmoke,
    float? Distance,
    bool AttackerBlind,
    bool AttackerInAir,
    bool AssistedFlash
);

public sealed record PlayerActionEvent(
    ulong SteamId64,
    DateTime OccurredAtUtc,
    string ActionType,
    string? ActionValue,
    int? RoundNumber
);

public sealed record PresenceSnapshot(
    DateTime CapturedAtUtc,
    string MapName,
    int ConnectedCount,
    IReadOnlyList<PlayerIdentity> Players
);

public sealed class StatsBatch
{
    public List<PlayerSessionOpened> SessionOpened { get; } = [];
    public List<PlayerSessionClosed> SessionClosed { get; } = [];
    public List<RoundStarted> RoundStarted { get; } = [];
    public List<RoundEnded> RoundEnded { get; } = [];
    public List<PlayerDeathEvent> PlayerDeaths { get; } = [];
    public List<PlayerActionEvent> PlayerActions { get; } = [];
    public List<PresenceSnapshot> PresenceSnapshots { get; } = [];

    public bool IsEmpty =>
        SessionOpened.Count == 0 &&
        SessionClosed.Count == 0 &&
        RoundStarted.Count == 0 &&
        RoundEnded.Count == 0 &&
        PlayerDeaths.Count == 0 &&
        PlayerActions.Count == 0 &&
        PresenceSnapshots.Count == 0;
}
