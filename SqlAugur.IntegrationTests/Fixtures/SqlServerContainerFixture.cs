using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;

namespace SqlAugur.IntegrationTests.Fixtures;

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

        // Programmable objects for schema exploration tests
        await ExecuteAsync(connection, """
            CREATE VIEW dbo.vw_ActiveProducts AS
            SELECT p.ProductId, p.Name, p.Price, c.Name AS CategoryName
            FROM dbo.Products p
            INNER JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
            """);

        await ExecuteAsync(connection, """
            CREATE PROCEDURE dbo.usp_GetProductsByCategory
                @CategoryId INT
            AS
            BEGIN
                SELECT ProductId, Name, Price
                FROM dbo.Products
                WHERE CategoryId = @CategoryId
                ORDER BY Name
            END
            """);

        await ExecuteAsync(connection, """
            CREATE FUNCTION dbo.fn_GetCategoryName(@CategoryId INT)
            RETURNS NVARCHAR(100)
            AS
            BEGIN
                DECLARE @Name NVARCHAR(100)
                SELECT @Name = Name FROM dbo.Categories WHERE CategoryId = @CategoryId
                RETURN @Name
            END
            """);

        // Extended properties
        await ExecuteAsync(connection, """
            EXEC sp_addextendedproperty
                @name = N'MS_Description',
                @value = N'Product catalog table',
                @level0type = N'SCHEMA', @level0name = N'dbo',
                @level1type = N'TABLE', @level1name = N'Products'
            """);

        await ExecuteAsync(connection, """
            EXEC sp_addextendedproperty
                @name = N'MS_Description',
                @value = N'Product display name',
                @level0type = N'SCHEMA', @level0name = N'dbo',
                @level1type = N'TABLE', @level1name = N'Products',
                @level2type = N'COLUMN', @level2name = N'Name'
            """);

        // Test procedure for FormatResultSets integration tests
        await ExecuteAsync(connection, """
            CREATE PROCEDURE dbo.usp_FormatResultSetsTest
            AS
            BEGIN
                -- Result set 0: columns including a large string and binary
                SELECT
                    1 AS Id,
                    'ShortValue' AS ShortCol,
                    REPLICATE(CAST('X' AS NVARCHAR(MAX)), 50000) AS LargeCol,
                    CAST(0xDEADBEEF AS VARBINARY(4)) AS BinaryCol,
                    'QueryPlanXml' AS QueryPlan

                -- Result set 1: second result set
                SELECT
                    100 AS MetricId,
                    'MetricName' AS MetricName

                -- Result set 2: third result set
                SELECT
                    'ThirdSet' AS Label
            END
            """);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var cmd = new SqlCommand(sql, connection);
        cmd.CommandTimeout = 30;
        await cmd.ExecuteNonQueryAsync();
    }
}
