using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CS2Stats.Contracts;

namespace CS2Stats.Plugin;

public sealed class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("mySql")]
    public MySqlSettings MySql { get; set; } = new();

    [JsonPropertyName("modules")]
    public StatsModulesSettings Modules { get; set; } = new();

    [JsonPropertyName("sync")]
    public SyncSettings Sync { get; set; } = new();
}
