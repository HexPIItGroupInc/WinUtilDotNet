namespace WinUtil.Core.Model;

/// <summary>
/// A single registry mutation, as declared in the catalog. Upstream records
/// <see cref="OriginalValue"/> alongside the target value — that is what makes
/// undo and drift detection possible without ever having run the tweak.
/// </summary>
public sealed record RegistryChange
{
    /// <summary>Hive-qualified path in PowerShell notation, e.g. "HKLM:\\System\\...".</summary>
    public required string Path { get; init; }

    public required string Name { get; init; }

    public required string Value { get; init; }

    /// <summary>Registry value kind as named upstream: DWord, QWord, String, ExpandString, Binary, MultiString.</summary>
    public required string Type { get; init; }

    public string? OriginalValue { get; init; }
}
