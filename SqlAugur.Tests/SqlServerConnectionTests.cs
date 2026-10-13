using Microsoft.Data.SqlClient;
using SqlAugur.Configuration;

namespace SqlAugur.Tests;

public class SqlServerConnectionTests
{
    [Fact]
    public void NoApplicationName_GetsSqlAugur()
    {
        var result = SqlServerConnection.WithDefaultApplicationName("Server=localhost;Database=master;Integrated Security=True;");

        var csb = new SqlConnectionStringBuilder(result);
        Assert.Equal("SqlAugur", csb.ApplicationName);
        Assert.Equal("localhost", csb.DataSource);
        Assert.Equal("master", csb.InitialCatalog);
    }

    [Theory]
    [InlineData("Server=localhost;Application Name=SqlAugur (Claude);")]
    [InlineData("Server=localhost;App=Custom;")]
    [InlineData("Server=localhost;Application Name=Core Microsoft SqlClient Data Provider;")]
    public void ExplicitApplicationName_IsKept(string connectionString)
    {
        Assert.Equal(connectionString, SqlServerConnection.WithDefaultApplicationName(connectionString));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a connection string")]
    public void EmptyOrInvalid_IsLeftForTheValidator(string connectionString)
    {
        Assert.Equal(connectionString, SqlServerConnection.WithDefaultApplicationName(connectionString));
    }
}
