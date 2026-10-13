using System.Globalization;
using System.Text;

namespace SqlAugur.Services;

/// <summary>
/// CSV result files for read_data. Large results go to disk instead of over stdio: the
/// Claude Code stdio client drops any server that writes a single message over 16MB, and
/// results that large are unusable in a model's context anyway.
/// </summary>
internal static class ResultFiles
{
    internal const string FilePrefix = "read_data-";

    internal static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sqlaugur", "results");

    /// <summary>
    /// Creates a new, uniquely named CSV file (owner read/write only on Unix) and returns its
    /// path and a UTF-8 (no BOM) writer.
    /// </summary>
    internal static (string Path, StreamWriter Writer) Create(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory,
            $"{FilePrefix}{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid().ToString("N")[..8]}.csv");

        var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        return (path, new StreamWriter(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), fileOptions));
    }

    /// <summary>
    /// Writes one RFC 4180 row. NULL becomes an empty field; fields containing a comma, quote
    /// or line break are quoted with embedded quotes doubled. Numbers use the invariant culture.
    /// </summary>
    internal static void WriteRow(TextWriter writer, IReadOnlyList<object?> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
                writer.Write(',');

            var field = Convert.ToString(values[i], CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(field))
                continue;

            if (field.AsSpan().IndexOfAny(",\"\r\n") >= 0)
            {
                writer.Write('"');
                writer.Write(field.Replace("\"", "\"\""));
                writer.Write('"');
            }
            else
            {
                writer.Write(field);
            }
        }
        writer.Write("\r\n");
    }

    /// <summary>
    /// Deletes result files older than <paramref name="maxAge"/>. Only files matching the
    /// read_data naming pattern are touched, so a shared ResultsDirectory is safe. Files that
    /// cannot be deleted (locked, permissions) are skipped. Returns the number deleted.
    /// </summary>
    // ponytail: runs at startup only — a server left running for weeks keeps expired files until
    // its next restart. Add a periodic sweep if that ever matters.
    internal static int DeleteExpired(string directory, TimeSpan maxAge)
    {
        if (!Directory.Exists(directory))
            return 0;

        var cutoff = DateTime.UtcNow - maxAge;
        var deleted = 0;
        foreach (var file in new DirectoryInfo(directory).EnumerateFiles($"{FilePrefix}*.csv"))
        {
            if (file.LastWriteTimeUtc >= cutoff)
                continue;
            try
            {
                file.Delete();
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        return deleted;
    }
}
