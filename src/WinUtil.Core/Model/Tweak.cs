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

    /// <summary>Native overlay commands replacing this tweak's scripts (ADR-0004).</summary>
    public IReadOnlyList<CommandAction> Commands { get; init; } = [];

    public IReadOnlyList<CommandAction> UndoCommands { get; init; } = [];

    /// <summary>Overlay: Appx identity-name patterns to remove on apply (removal has no undo).</summary>
    public IReadOnlyList<string> AppxRemove { get; init; } = [];

    /// <summary>True when a native overlay entry covers this tweak's scripts.</summary>
    public bool ScriptsCovered { get; init; }

    public bool HasScripts => InvokeScript.Count > 0 || UndoScript.Count > 0;

    /// <summary>
    /// True when every effect of this tweak is expressible through typed,
    /// natively-executable actions — i.e. no PowerShell escape hatch is needed.
    /// The "PowerShell-free %" metric counts these.
    /// </summary>
    public bool IsNativelyExecutable => !HasScripts || ScriptsCovered;
}
