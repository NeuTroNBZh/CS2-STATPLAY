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
            Host = "test-db.example.com",
            Database = "test_database",
            Username = "test_user",
            Password = "test_password"
        };

        Assert.False(MySqlConfigGuard.IsPackagedPlaceholder(settings));
    }
}