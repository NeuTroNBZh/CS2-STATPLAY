using CounterStrikeSharp.API.Core;
using CS2Stats.Contracts;

namespace CS2Stats.Plugin;

public sealed class PluginConfig : BasePluginConfig
{
    public MySqlSettings MySql { get; init; } = new();
    public StatsModulesSettings Modules { get; init; } = new();
    public SyncSettings Sync { get; init; } = new();
}
