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

    /// <summary>Overlay commands that must run BEFORE typed actions (e.g. killing a process that would block an Appx removal).</summary>
    public IReadOnlyList<CommandAction> PreCommands { get; init; } = [];

    /// <summary>Overlay commands that run AFTER typed actions (e.g. flushdns after a hosts edit). ADR-0004.</summary>
    public IReadOnlyList<CommandAction> Commands { get; init; } = [];

    public IReadOnlyList<CommandAction> UndoCommands { get; init; } = [];

    /// <summary>Overlay: Appx identity-name patterns to remove on apply (removal has no undo).</summary>
    public IReadOnlyList<string> AppxRemove { get; init; } = [];

    /// <summary>Overlay: trailing-\* globs whose contents are deleted best-effort on apply.</summary>
    public IReadOnlyList<string> DeletePaths { get; init; } = [];

    /// <summary>Overlay: directories created if missing on apply.</summary>
    public IReadOnlyList<string> EnsureDirs { get; init; } = [];

    /// <summary>Overlay: hosts-file blocklist action.</summary>
    public HostsBlock? HostsBlock { get; init; }

    /// <summary>Overlay: create a System Restore checkpoint with this description on apply.</summary>
    public string? CreateRestorePoint { get; init; }

    /// <summary>Overlay: registry key trees deleted on apply, BEFORE typed registry writes.</summary>
    public IReadOnlyList<string> RegistryDeleteKeys { get; init; } = [];

    /// <summary>Overlay: registry key trees deleted on undo, after typed restores.</summary>
    public IReadOnlyList<string> UndoRegistryDeleteKeys { get; init; } = [];

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
