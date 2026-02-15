using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace SqlAugur.Configuration;

public sealed class SqlAugurOptionsValidator : IValidateOptions<SqlAugurOptions>
{
    internal static bool TryValidateKeyVaultUri(string? rawUri, out Uri? vaultUri, out string? error)
    {
        vaultUri = null;
        error = null;

        if (string.IsNullOrEmpty(rawUri))
            return true;

        if (!Uri.TryCreate(rawUri, UriKind.Absolute, out vaultUri))
        {
            error = $"AzureKeyVaultUri is not a valid absolute URI: '{rawUri}'.";
            vaultUri = null;
            return false;
        }

        if (!string.Equals(vaultUri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            error = $"AzureKeyVaultUri must use the https scheme (got '{vaultUri.Scheme}').";
            vaultUri = null;
            return false;
        }

        return true;
    }

    public ValidateOptionsResult Validate(string? name, SqlAugurOptions options)
    {
        var errors = new List<string>();

        if (options.Servers.Count == 0)
            errors.Add("At least one server must be configured in SqlAugur:Servers.");

        foreach (var (serverName, connection) in options.Servers)
        {
            if (string.IsNullOrWhiteSpace(connection.ConnectionString))
            {
                errors.Add($"Server '{serverName}' has an empty ConnectionString.");
            }
            else
            {
                try
                {
                    _ = new SqlConnectionStringBuilder(connection.ConnectionString);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Server '{serverName}' has an invalid ConnectionString: {ex.Message}");
                }
            }
        }

        if (options.MaxRows < 1 || options.MaxRows > 100_000)
            errors.Add($"MaxRows must be between 1 and 100,000 (got {options.MaxRows}).");

        if (options.CommandTimeoutSeconds < 1 || options.CommandTimeoutSeconds > 600)
            errors.Add($"CommandTimeoutSeconds must be between 1 and 600 (got {options.CommandTimeoutSeconds}).");

        if (options.MaxConcurrentQueries < 1 || options.MaxConcurrentQueries > 100)
            errors.Add($"MaxConcurrentQueries must be between 1 and 100 (got {options.MaxConcurrentQueries}).");

        if (options.MaxQueriesPerMinute < 1 || options.MaxQueriesPerMinute > 10_000)
            errors.Add($"MaxQueriesPerMinute must be between 1 and 10,000 (got {options.MaxQueriesPerMinute}).");

        if (!TryValidateKeyVaultUri(options.AzureKeyVaultUri, out _, out var kvError))
            errors.Add(kvError!);

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
