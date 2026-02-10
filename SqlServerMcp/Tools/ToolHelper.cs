using ModelContextProtocol;

namespace SqlServerMcp.Tools;

/// <summary>
/// Helper methods for tool implementations.
/// </summary>
internal static class ToolHelper
{
    /// <summary>
    /// Executes an async tool operation with standardized exception handling.
    /// Converts ArgumentException and InvalidOperationException to McpException.
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="McpException">Thrown when the operation fails with ArgumentException or InvalidOperationException.</exception>
    public static async Task<string> ExecuteAsync(Func<Task<string>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new McpException(ex.Message);
        }
    }
}
