using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SqlServerMcp.Configuration;
using Microsoft.Extensions.Configuration;
using SqlServerMcp.Services;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Route all logging to stderr (stdout is reserved for MCP stdio transport)
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Bind and validate configuration
builder.Services.Configure<SqlServerMcpOptions>(
    builder.Configuration.GetSection("SqlServerMcp"));
builder.Services.AddSingleton<IValidateOptions<SqlServerMcpOptions>, SqlServerMcpOptionsValidator>();

// Register services
builder.Services.AddSingleton<IRateLimitingService, RateLimitingService>();
builder.Services.AddSingleton<ISqlServerService, SqlServerService>();
builder.Services.AddSingleton<IDiagramService, DiagramService>();
builder.Services.AddSingleton<ISchemaOverviewService, SchemaOverviewService>();
builder.Services.AddSingleton<ITableDescribeService, TableDescribeService>();
// Read tool flags directly from configuration (needed before DI container is built)
static bool ReadBoolFlag(IConfiguration config, string key)
    => bool.TryParse(config[$"SqlServerMcp:{key}"], out var value) && value;

var enableFirstResponderKit = ReadBoolFlag(builder.Configuration, "EnableFirstResponderKit");
var enableDarlingData = ReadBoolFlag(builder.Configuration, "EnableDarlingData");
var enableWhoIsActive = ReadBoolFlag(builder.Configuration, "EnableWhoIsActive");
var enableDynamicToolsets = ReadBoolFlag(builder.Configuration, "EnableDynamicToolsets");

// Register DBA services when their toolkit is enabled (needed in both static and dynamic modes)
if (enableFirstResponderKit)
    builder.Services.AddSingleton<IFirstResponderService, FirstResponderService>();
if (enableDarlingData)
    builder.Services.AddSingleton<IDarlingDataService, DarlingDataService>();
if (enableWhoIsActive)
    builder.Services.AddSingleton<IWhoIsActiveService, WhoIsActiveService>();

// In dynamic mode, register the toolset manager for on-demand tool discovery
if (enableDynamicToolsets)
    builder.Services.AddSingleton<IToolsetManager, ToolsetManager>();

// Configure MCP server with appropriate tool set
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "sqlserver-mcp",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithTools(ToolRegistry.GetToolTypes(
        enableFirstResponderKit, enableDarlingData, enableWhoIsActive, enableDynamicToolsets));

// Advertise dynamic tool list support when in dynamic mode
if (enableDynamicToolsets && (enableFirstResponderKit || enableDarlingData || enableWhoIsActive))
{
    builder.Services.PostConfigure<McpServerOptions>(options =>
    {
        options.Capabilities ??= new();
        options.Capabilities.Tools ??= new();
        options.Capabilities.Tools.ListChanged = true;
    });
}

var host = builder.Build();

// Log startup warnings for security-relevant configuration
var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SqlServerMcp.Startup");
var options = host.Services.GetRequiredService<IOptions<SqlServerMcpOptions>>().Value;

foreach (var (serverName, connection) in options.Servers)
{
    if (string.IsNullOrWhiteSpace(connection.ConnectionString))
        continue;

    try
    {
        var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection.ConnectionString);
        if (!csb.Encrypt)
            startupLogger.LogWarning("Server '{ServerName}': connection string has Encrypt=False. Traffic will not be encrypted.", serverName);
        if (csb.TrustServerCertificate)
            startupLogger.LogWarning("Server '{ServerName}': connection string has TrustServerCertificate=True. Server certificate will not be validated.", serverName);
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Server '{ServerName}': could not parse connection string for security checks.", serverName);
    }
}

if (options.MaxRows > 10_000)
    startupLogger.LogWarning("MaxRows is set to {MaxRows}. Large values may produce very large JSON responses.", options.MaxRows);

await host.RunAsync();
