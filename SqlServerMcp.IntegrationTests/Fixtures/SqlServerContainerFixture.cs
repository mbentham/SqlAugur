using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;

namespace SqlServerMcp.IntegrationTests.Fixtures;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private static readonly string SaPassword = GeneratePassword();
    private const int MsSqlPort = 1433;

    private static string GeneratePassword()
    {
        // Prefix satisfies SQL Server complexity: uppercase, lowercase, digit, special char
        return "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    private readonly IContainer _container = new ContainerBuilder("mcr.microsoft.com/mssql/server:2025-latest")
        .WithPortBinding(MsSqlPort, true)
        .WithEnvironment("ACCEPT_EULA", "Y")
        .WithEnvironment("MSSQL_SA_PASSWORD", SaPassword)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilMessageIsLogged("SQL Server is now ready for client connections"))
        .Build();

    public string ConnectionString
    {
        get
        {
            var host = _container.Hostname;
            var port = _container.GetMappedPublicPort(MsSqlPort);
            return $"Server={host},{port};User Id=sa;Password={SaPassword};TrustServerCertificate=True;Encrypt=True;";
        }
    }

    public const string TestDatabaseName = "McpTestDb";
    public const string ServerName = "testcontainer";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await WaitForSqlServerAsync();
        await SeedDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task WaitForSqlServerAsync()
    {
        // SQL Server needs time after port is open to accept connections
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!cts.IsCancellationRequested)
        {
            try
            {
                await using var connection = new SqlConnection(ConnectionString);
                await connection.OpenAsync(cts.Token);
                await using var cmd = new SqlCommand("SELECT 1", connection);
                await cmd.ExecuteScalarAsync(cts.Token);
                return;
            }
            catch (SqlException)
            {
                await Task.Delay(500, cts.Token);
            }
        }

        throw new TimeoutException("SQL Server did not become ready within 60 seconds.");
    }

    private async Task SeedDatabaseAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Create the test database
        await ExecuteAsync(connection, $"CREATE DATABASE [{TestDatabaseName}]");

        // Switch to the test database
        await connection.ChangeDatabaseAsync(TestDatabaseName);

        // Create sales schema
        await ExecuteAsync(connection, "CREATE SCHEMA sales");

        // dbo.Categories — PK, unique constraint, check constraint, default
        await ExecuteAsync(connection, """
            CREATE TABLE dbo.Categories (
                CategoryId INT IDENTITY(1,1) NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                Description NVARCHAR(500) NULL,
                IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1,
                CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryId),
                CONSTRAINT UQ_Categories_Name UNIQUE (Name),
                CONSTRAINT CK_Categories_Name CHECK (LEN(Name) > 0)
            )
            """);

        // dbo.Products — PK, FK, identity, non-clustered index, check constraint, default
        await ExecuteAsync(connection, """
            CREATE TABLE dbo.Products (
                ProductId INT IDENTITY(1,1) NOT NULL,
                Name NVARCHAR(200) NOT NULL,
                CategoryId INT NOT NULL,
                Price DECIMAL(10,2) NOT NULL,
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductId),
                CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId)
                    REFERENCES dbo.Categories (CategoryId),
                CONSTRAINT CK_Products_Price CHECK (Price >= 0)
            )
            """);

        await ExecuteAsync(connection,
            "CREATE NONCLUSTERED INDEX IX_Products_CategoryId ON dbo.Products (CategoryId)");

        // sales.Orders — second schema, PK, default
        await ExecuteAsync(connection, """
            CREATE TABLE sales.Orders (
                OrderId INT IDENTITY(1,1) NOT NULL,
                CustomerName NVARCHAR(200) NOT NULL,
                OrderDate DATETIME2 NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (OrderId)
            )
            """);

        // sales.OrderItems — cross-schema FK, cascade delete, composite index
        await ExecuteAsync(connection, """
            CREATE TABLE sales.OrderItems (
                OrderItemId INT IDENTITY(1,1) NOT NULL,
                OrderId INT NOT NULL,
                ProductId INT NOT NULL,
                Quantity INT NOT NULL,
                UnitPrice DECIMAL(10,2) NOT NULL,
                CONSTRAINT PK_OrderItems PRIMARY KEY CLUSTERED (OrderItemId),
                CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId)
                    REFERENCES sales.Orders (OrderId) ON DELETE CASCADE,
                CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId)
                    REFERENCES dbo.Products (ProductId),
                CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0)
            )
            """);

        await ExecuteAsync(connection,
            "CREATE NONCLUSTERED INDEX IX_OrderItems_OrderId_ProductId ON sales.OrderItems (OrderId, ProductId)");

        // Seed sample data
        await ExecuteAsync(connection, """
            INSERT INTO dbo.Categories (Name, Description)
            VALUES ('Electronics', 'Electronic devices and accessories'),
                   ('Books', NULL)
            """);

        await ExecuteAsync(connection, """
            INSERT INTO dbo.Products (Name, CategoryId, Price)
            VALUES ('Laptop', 1, 999.99),
                   ('Mouse', 1, 29.50),
                   ('C# in Depth', 2, 45.00)
            """);

        await ExecuteAsync(connection, """
            INSERT INTO sales.Orders (CustomerName)
            VALUES ('Alice Smith'),
                   ('Bob Jones')
            """);

        await ExecuteAsync(connection, """
            INSERT INTO sales.OrderItems (OrderId, ProductId, Quantity, UnitPrice)
            VALUES (1, 1, 1, 999.99),
                   (1, 2, 2, 29.50),
                   (2, 3, 1, 45.00)
            """);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var cmd = new SqlCommand(sql, connection);
        cmd.CommandTimeout = 30;
        await cmd.ExecuteNonQueryAsync();
    }
}
