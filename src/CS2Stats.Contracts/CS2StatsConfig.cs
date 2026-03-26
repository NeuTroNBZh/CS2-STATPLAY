namespace CS2Stats.Contracts;

public sealed class CS2StatsConfig
{
    public required MySqlSettings MySql { get; init; }
    public required StatsModulesSettings Modules { get; init; }
    public required SyncSettings Sync { get; init; }
}

public sealed class MySqlSettings
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 3306;
    public string Database { get; init; } = "cs2_stats";
    public string Username { get; init; } = "cs2stats";
    public string Password { get; init; } = "change-me";
    public bool SslRequired { get; init; } = false;
}

public sealed class StatsModulesSettings
{
    public bool SessionTrackingEnabled { get; init; } = true;
    public bool KdaEnabled { get; init; } = true;
    public bool HeadshotEnabled { get; init; } = true;
    public bool WeaponFireEnabled { get; init; } = true;
    public bool GrenadeStatsEnabled { get; init; } = true;
    public bool ObjectiveStatsEnabled { get; init; } = true;
    public bool PresenceSnapshotsEnabled { get; init; } = true;
    public bool MatchHistoryEnabled { get; init; } = false;
}

public sealed class SyncSettings
{
    public int FlushIntervalSeconds { get; init; } = 15;
    public int PresenceSnapshotIntervalSeconds { get; init; } = 10;
    public int MaxBufferedEvents { get; init; } = 5000;
}
