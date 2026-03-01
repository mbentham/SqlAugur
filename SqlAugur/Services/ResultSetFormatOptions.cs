namespace SqlAugur.Services;

public sealed class ResultSetFormatOptions
{
    public HashSet<string> ExcludedColumns { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int> TruncatedColumns { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int? MaxRowsOverride { get; init; }

    public HashSet<int> ExcludedResultSets { get; init; } = [];

    public int? MaxStringLength { get; init; }

    public static readonly ResultSetFormatOptions Default = new();
}
