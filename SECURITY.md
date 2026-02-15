# Security Guide

Operational security guidance for SqlAugur — credential management, login and user hardening, and connection security. For an overview of query validation, parameter blocking, and rate limiting, see the [Security section in the README](README.md#security).

## Connection Security and Credential Management

**Recommended: Use Windows Authentication or Azure Managed Identity**

The most secure authentication methods avoid storing credentials in configuration files entirely:

**Windows Authentication (on-premises or domain-joined environments):**
```json
{
  "SqlAugur": {
    "Servers": {
      "production": {
        "ConnectionString": "Server=myserver;Database=master;Integrated Security=True;TrustServerCertificate=False;Encrypt=True;"
      }
    }
  }
}
```

**Azure Managed Identity (Azure SQL Database):**
```json
{
  "SqlAugur": {
    "Servers": {
      "azure-prod": {
        "ConnectionString": "Server=myserver.database.windows.net;Database=master;Authentication=Active Directory Managed Identity;TrustServerCertificate=False;Encrypt=True;"
      }
    }
  }
}
```

**If SQL Authentication is Required:**

When Windows Authentication or Managed Identity are not available, follow these practices:

1. **Never commit credentials to source control** — `appsettings.json` is already gitignored, but ensure you never commit credentials in example files or documentation

2. **Use .NET configuration environment variable overrides** — .NET's `IConfiguration` system supports overriding any config value via environment variables using the `__` (double-underscore) separator. This is the recommended approach for injecting credentials without putting them in config files:

   Start with a connection string template in `appsettings.json` (no password):
   ```json
   {
     "SqlAugur": {
       "Servers": {
         "production": {
           "ConnectionString": "Server=myserver;Database=master;User Id=sqlreader;Encrypt=True;TrustServerCertificate=False;"
         }
       }
     }
   }
   ```

   Then override the full connection string (including the password) via an environment variable:
   ```bash
   export SqlAugur__Servers__production__ConnectionString="Server=myserver;Database=master;User Id=sqlreader;Password=your-secure-password;Encrypt=True;TrustServerCertificate=False;"
   dotnet run --project SqlAugur
   ```

   > **Note:** Some MCP clients (e.g., Claude Desktop) support `${ENV_VAR}` substitution syntax in their own configuration files, but this is **not a .NET feature** — .NET's `IConfiguration` system does not resolve `${...}` placeholders in values. Do not rely on this syntax in `appsettings.json`. Use the `__` environment variable override pattern shown above, or inject credentials through your MCP client's own environment variable support.

3. **Use secure credential stores:**

   **Azure Key Vault** — native integration is built in. Set `AzureKeyVaultUri` in your `appsettings.json` to your vault URI:

   ```json
   {
     "SqlAugur": {
       "AzureKeyVaultUri": "https://myvault.vault.azure.net/"
     }
   }
   ```

   Then store connection strings as Key Vault secrets. For example, a secret named `SqlAugur--Servers--production--ConnectionString` would map to the connection string for a server named 'production'

   Authentication uses [`DefaultAzureCredential`](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential), which automatically tries Managed Identity, Azure CLI, Visual Studio, environment variables, and other methods in order. No additional authentication configuration is needed in most environments.

   **AWS Secrets Manager** — use a wrapper script to inject credentials via environment variables before starting SqlAugur:

   ```bash
   #!/usr/bin/env bash
   export SqlAugur__Servers__production__ConnectionString=$(
     aws secretsmanager get-secret-value \
       --secret-id sqlaugur/production \
       --query SecretString --output text
   )
   exec sqlaugur "$@"
   ```

   **HashiCorp Vault** — use a wrapper script to inject credentials via environment variables before starting SqlAugur:

   ```bash
   #!/usr/bin/env bash
   export SqlAugur__Servers__production__ConnectionString=$(
     vault kv get -field=connection_string secret/sqlaugur/production
   )
   exec sqlaugur "$@"
   ```

   Point your MCP client at the wrapper script instead of `sqlaugur` directly.

   **Windows Credential Manager** — for local development on Windows

4. **Use strong passwords** — use a password manager to generate a long (30+ characters), random password. A random password of this length naturally satisfies Windows complexity requirements. Keep [`CHECK_POLICY`](https://learn.microsoft.com/en-us/sql/relational-databases/security/password-policy) and `CHECK_EXPIRATION` enabled on the SQL login (the SQL Server defaults) to enforce complexity and rotation at the server level.

**Connection String Encryption:**

Always use encrypted connections to protect credentials in transit:
- Set `Encrypt=True` in all connection strings
- Use `TrustServerCertificate=False` for production (only use `True` for development with self-signed certificates)
- Ensure SQL Server has a valid SSL/TLS certificate from a trusted CA

## SQL Server Login and User Recommendations

The SQL Server login and database user used by this MCP server should follow least-privilege principles:

- **Grant read-only access** — the login only needs `SELECT` permission on the databases and schemas it should access. Do not grant `db_datawriter`, `db_ddladmin`, or server-level roles like `sysadmin`.
- **Do not grant EXECUTE on unsafe CLR assemblies** — `SELECT` statements can call user-defined functions, including CLR functions. If a CLR assembly is registered with `EXTERNAL_ACCESS` or `UNSAFE` permission sets, it can perform file I/O, network calls, and other side effects when invoked from a SELECT. The login should not have EXECUTE permission on any such assemblies.
- **Use a dedicated login** — do not reuse logins shared with other applications. A dedicated login makes it easy to audit activity and revoke access independently.
- **Restrict database access** — if the login should only query specific databases, create database users only in those databases. Three-part name queries (`OtherDb.dbo.Table`) are allowed by design, so database-level permissions are the control point.
- **Consider Resource Governor** — for production SQL Server instances, place the login in a Resource Governor workload group with CPU and memory limits to prevent expensive queries from impacting other workloads.
