using CS2Stats.Plugin;
using CS2Stats.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CS2Stats.Tests;

/// <summary>
/// Tests pour valider que NoOpStatsWriter fonctionne correctement
/// (fallback quand MySQL n'est pas disponible).
/// </summary>
public class NoOpStatsWriterTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly NoOpStatsWriter _writer;

    public NoOpStatsWriterTests()
    {
        _mockLogger = new Mock<ILogger>();
        _writer = new NoOpStatsWriter(_mockLogger.Object);
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_Completes()
    {
        // Arrange
        var batch = new StatsBatch();

        // Act & Assert: Devrait compléter sans erreur
        try
        {
            await _writer.WriteBatchAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected exception: {ex.Message}");
        }
    }

    [Fact]
    public async Task WriteBatchAsync_MultipleCallsWork()
    {
        // Arrange
        var batch1 = new StatsBatch { SessionOpened = { new(123, DateTime.UtcNow, "de_mirage", 10.0) } };
        var batch2 = new StatsBatch { SessionOpened = { new(456, DateTime.UtcNow, "de_inferno", 20.0) } };

        // Act
        await _writer.WriteBatchAsync(batch1, CancellationToken.None);
        await _writer.WriteBatchAsync(batch2, CancellationToken.None);

        // Assert: Vérifier qu'au moins 2 logs ont été appelés
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task WriteBatchAsync_RespectsCancellationToken()
    {
        // Arrange
        var batch = new StatsBatch();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert: NoOp doesn't use the token, so it should still complete
        try
        {
            await _writer.WriteBatchAsync(batch, cts.Token);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected exception: {ex.Message}");
        }
    }
}
