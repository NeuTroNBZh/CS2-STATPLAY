using CS2Stats.Plugin;

namespace CS2Stats.Tests;

public class DatabaseInitializationServiceTests
{
    [Fact]
    public void SplitSqlScript_ParsesTablesAndProceduresWithDelimiter()
    {
        const string script = """
        CREATE TABLE IF NOT EXISTS players (
            player_id INT NOT NULL
        );

        DELIMITER $$
        DROP PROCEDURE IF EXISTS sp_test$$
        CREATE PROCEDURE sp_test()
        BEGIN
            SELECT 1;
            SELECT 2;
        END$$
        DELIMITER ;

        CREATE TABLE IF NOT EXISTS rounds (
            round_id INT NOT NULL
        );
        """;

        var statements = DatabaseInitializationService.SplitSqlScript(script);

        Assert.Equal(4, statements.Count);
        Assert.StartsWith("CREATE TABLE IF NOT EXISTS players", statements[0]);
        Assert.StartsWith("DROP PROCEDURE IF EXISTS sp_test", statements[1]);
        Assert.Contains("SELECT 1;", statements[2]);
        Assert.Contains("SELECT 2;", statements[2]);
        Assert.StartsWith("CREATE TABLE IF NOT EXISTS rounds", statements[3]);
    }

    // MySQL 8 has no CREATE OR REPLACE PROCEDURE (MariaDB only): each procedure is dropped then created.
    [Fact]
    public void StoredProceduresScript_IsMySqlCompatible()
    {
        var statements = DatabaseInitializationService.SplitSqlScript(DatabaseInitializationService.GetStoredProceduresScript()).ToList();

        Assert.DoesNotContain(statements, s => s.Contains("CREATE OR REPLACE", StringComparison.OrdinalIgnoreCase));
        var created = statements.Where(s => s.StartsWith("CREATE PROCEDURE ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, created.Count);
        foreach (var create in created)
        {
            var name = create["CREATE PROCEDURE ".Length..create.IndexOf('(')].Trim();
            var drop = statements.IndexOf($"DROP PROCEDURE IF EXISTS {name}");
            Assert.True(drop >= 0 && drop < statements.IndexOf(create), $"{name} is not dropped before being created");
        }
    }
}
