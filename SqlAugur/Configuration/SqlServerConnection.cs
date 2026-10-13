using Microsoft.Data.SqlClient;

namespace SqlAugur.Configuration;

public sealed class SqlServerConnection
{
    public const string DefaultApplicationName = "SqlAugur";

    public string ConnectionString { get; set; } = string.Empty;

    // Without this, connections show as "Core Microsoft SqlClient Data Provider" and DBAs
    // can't tell SqlAugur traffic apart from any other .NET client. An explicit
    // Application Name in the connection string wins.
    internal static string WithDefaultApplicationName(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        try
        {
            var csb = new SqlConnectionStringBuilder(connectionString);
            if (csb.ShouldSerialize("Application Name"))
                return connectionString;

            csb.ApplicationName = DefaultApplicationName;
            return csb.ConnectionString;
        }
        catch (ArgumentException)
        {
            // Left unchanged so SqlAugurOptionsValidator reports it.
            return connectionString;
        }
    }
}
