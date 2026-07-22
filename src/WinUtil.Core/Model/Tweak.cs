namespace WinUtil.Core.Model;

/// <summary>
/// One entry from winutil's tweaks.json. Property names mirror the upstream
/// catalog schema (catalog-as-contract, ADR-0001) — do not rename to taste.
/// </summary>
public sealed record Tweak
{
    public required string Id { get; init; }

    public required string Content { get; init; }

    public string? Description { get; init; }

    public string? Category { get; init; }

    public string? Panel { get; init; }

    public string? Link { get; init; }

    public IReadOnlyList<RegistryChange> Registry { get; init; } = [];

    public IReadOnlyList<ServiceChange> Service { get; init; } = [];

    public IReadOnlyList<string> InvokeScript { get; init; } = [];

    public IReadOnlyList<string> UndoScript { get; init; } = [];

    /// <summary>
    /// True when every effect of this tweak is expressible through typed,
    /// natively-executable actions — i.e. no script escape hatch is needed.
    /// The "PowerShell-free %" metric counts these.
    /// </summary>
    public bool IsNativelyExecutable => InvokeScript.Count == 0 && UndoScript.Count == 0;
}
