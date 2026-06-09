using CS2Stats.Contracts;

namespace CS2Stats.Plugin;

public interface IStatsWriter
{
    Task WriteBatchAsync(StatsBatch batch, CancellationToken cancellationToken);
}
