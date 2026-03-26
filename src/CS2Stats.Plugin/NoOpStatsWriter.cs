using CS2Stats.Contracts;
using Microsoft.Extensions.Logging;

namespace CS2Stats.Plugin;

public sealed class NoOpStatsWriter(ILogger logger) : IStatsWriter
{
    public Task WriteBatchAsync(StatsBatch batch, CancellationToken cancellationToken)
    {
        if (batch.IsEmpty)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "[CS2Stats] Flush no-op: sessions+{Opened}/-{Closed}, rounds+{RoundStart}/-{RoundEnd}, deaths={Deaths}, actions={Actions}, snapshots={Snapshots}",
            batch.SessionOpened.Count,
            batch.SessionClosed.Count,
            batch.RoundStarted.Count,
            batch.RoundEnded.Count,
            batch.PlayerDeaths.Count,
            batch.PlayerActions.Count,
            batch.PresenceSnapshots.Count
        );

        return Task.CompletedTask;
    }
}
