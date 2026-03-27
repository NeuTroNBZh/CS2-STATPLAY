using CS2Stats.Contracts;
using CS2Stats.Plugin;

namespace CS2Stats.Tests;

public class MySqlConfigGuardTests
{
    [Fact]
    public void IsPackagedPlaceholder_ReturnsTrue_ForPackagedExampleValues()
    {
        var settings = new MySqlSettings();

        Assert.True(MySqlConfigGuard.IsPackagedPlaceholder(settings));
    }

    [Fact]
    public void IsPackagedPlaceholder_ReturnsFalse_WhenCredentialsAreCustomized()
    {
        var settings = new MySqlSettings
        {
            Host = "kinvara.dathost.net",
            Database = "69b7c2666316be76ea88f82b",
            Username = "abfCZmMWvnbqiPgr",
            Password = "YAyjQGEPLrM"
        };

        Assert.False(MySqlConfigGuard.IsPackagedPlaceholder(settings));
    }
}