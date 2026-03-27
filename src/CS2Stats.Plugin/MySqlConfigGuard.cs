using CS2Stats.Contracts;

namespace CS2Stats.Plugin;

internal static class MySqlConfigGuard
{
    internal static bool IsPackagedPlaceholder(MySqlSettings settings)
    {
        return string.Equals(settings.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && settings.Port == 3306
            && string.Equals(settings.Database, "cs2_stats", StringComparison.OrdinalIgnoreCase)
            && string.Equals(settings.Username, "cs2stats", StringComparison.OrdinalIgnoreCase)
            && string.Equals(settings.Password, "change-me", StringComparison.Ordinal);
    }
}