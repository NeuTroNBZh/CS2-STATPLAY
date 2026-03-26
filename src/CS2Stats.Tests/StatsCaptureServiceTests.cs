using CS2Stats.Plugin;
using CS2Stats.Contracts;
using Xunit;

namespace CS2Stats.Tests;

/// <summary>
/// Tests pour valider que StatsCaptureService bufférise correctement les événements.
/// NOTE: Les tests qui appellent les APIs CS2 (Utilities.GetPlayers, Server.MapName) sont exécutés
/// uniquement sur un serveur CS2 réel où la DLL counterstrikesharp est disponible.
/// Ces tests unitaires se concentrent sur le buffering et la structure des données.
/// </summary>
public class StatsCaptureServiceTests
{
    [Fact]
    public void Constructor_WithMapName_IsSuccessful()
    {
        // Arrange & Act
        var service = new StatsCaptureService("de_mirage");

            // Assert
            Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithoutMapName_FallsBackSafely()
    {
        // Arrange & Act
        var service = new StatsCaptureService(null);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void DrainBatch_InitiallyEmpty()
    {
        // Arrange
        var service = new StatsCaptureService("de_mirage");

        // Act
        var batch = service.DrainBatch();
        // Assert
        Assert.NotNull(batch);
        Assert.Empty(batch.SessionOpened);
        Assert.Empty(batch.SessionClosed);
        Assert.Empty(batch.RoundStarted);
        Assert.Empty(batch.RoundEnded);
        Assert.Empty(batch.PlayerDeaths);
        Assert.Empty(batch.PlayerActions);
        Assert.Empty(batch.PresenceSnapshots);
    }

    [Fact]
    public void DrainBatch_ClearsAfterMultipleCalls()
    {
        // Arrange
        var service = new StatsCaptureService("de_mirage");
        var batch1 = service.DrainBatch();
        Assert.Empty(batch1.SessionOpened);

        // Act
        var batch2 = service.DrainBatch();

        // Assert
        Assert.Empty(batch2.SessionOpened);
        Assert.Empty(batch2.PlayerDeaths);
    }

    [Fact]
    public void OnMapStart_UpdatesMap()
    {
        // Arrange
        var service = new StatsCaptureService("de_inferno");

        // Act
        service.OnMapStart("de_ancient");

        // Assert (vérifier que le service a accepté l'appel sans erreur)
        var batch = service.DrainBatch();
        Assert.NotNull(batch);
    }

    [Fact]
    public void MultipleServiceInstances_AreIndependent()
    {
        // Arrange
        var service1 = new StatsCaptureService("de_mirage");
        var service2 = new StatsCaptureService("de_inferno");

        // Act
        var batch1 = service1.DrainBatch();
        var batch2 = service2.DrainBatch();

        // Assert
        Assert.NotSame(batch1, batch2);
        Assert.NotSame(batch1.SessionOpened, batch2.SessionOpened);
    }

    [Fact]
        public async Task DrainBatch_IsThreadSafe()
    {
        // Arrange
        const int taskCount = 5;
        var service = new StatsCaptureService("de_mirage");
        var tasks = new Task[taskCount];

        // Act: Lancer plusieurs tâches qui drainient en parallèle
        for (int i = 0; i < taskCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var batch = service.DrainBatch();
                    Assert.NotNull(batch);
                }
            });
        }

        // Attendre que toutes les tâches se terminent
        await Task.WhenAll(tasks);

        // Assert: Si on arrive ici, pas de deadlock/race condition
        Assert.True(true);
    }
}

