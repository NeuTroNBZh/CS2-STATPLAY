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
}