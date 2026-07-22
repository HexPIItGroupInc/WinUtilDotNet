namespace WinUtil.Core.Model;

/// <summary>A Windows service startup-type change, as declared in the catalog.</summary>
public sealed record ServiceChange
{
    public required string Name { get; init; }

    /// <summary>Target startup type: Automatic, AutomaticDelayedStart, Manual, Disabled.</summary>
    public required string StartupType { get; init; }

    public string? OriginalType { get; init; }
}
