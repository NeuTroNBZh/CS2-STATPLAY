using System.Text.Json.Serialization;

namespace CS2Stats.Contracts;

public sealed class CS2StatsConfig
{
    public required MySqlSettings MySql { get; init; }
    public required StatsModulesSettings Modules { get; init; }
    public required SyncSettings Sync { get; init; }
}

public sealed class MySqlSettings
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 3306;

    [JsonPropertyName("database")]
    public string Database { get; set; } = "cs2_stats";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "cs2stats";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "change-me";

    [JsonPropertyName("sslRequired")]
    public bool SslRequired { get; set; } = false;
}

public sealed class StatsModulesSettings
{
    [JsonPropertyName("sessionTrackingEnabled")]
    public bool SessionTrackingEnabled { get; set; } = true;

    [JsonPropertyName("kdaEnabled")]
    public bool KdaEnabled { get; set; } = true;

    [JsonPropertyName("headshotEnabled")]
    public bool HeadshotEnabled { get; set; } = true;

    [JsonPropertyName("weaponFireEnabled")]
    public bool WeaponFireEnabled { get; set; } = true;

    [JsonPropertyName("grenadeStatsEnabled")]
    public bool GrenadeStatsEnabled { get; set; } = true;

    [JsonPropertyName("objectiveStatsEnabled")]
    public bool ObjectiveStatsEnabled { get; set; } = true;

    [JsonPropertyName("presenceSnapshotsEnabled")]
    public bool PresenceSnapshotsEnabled { get; set; } = true;

    [JsonPropertyName("matchHistoryEnabled")]
    public bool MatchHistoryEnabled { get; set; } = false;
}

public sealed class SyncSettings
{
    [JsonPropertyName("flushIntervalSeconds")]
    public int FlushIntervalSeconds { get; set; } = 15;

    [JsonPropertyName("presenceSnapshotIntervalSeconds")]
    public int PresenceSnapshotIntervalSeconds { get; set; } = 10;

    [JsonPropertyName("maxBufferedEvents")]
    public int MaxBufferedEvents { get; set; } = 5000;
}
