namespace WinUtil.Core.Model;

/// <summary>One entry from winutil's appx.json debloat catalog.</summary>
public sealed record AppxPackage
{
    public required string Id { get; init; }

    public required string Content { get; init; }

    public string? Category { get; init; }

    /// <summary>Package identity name, e.g. "Microsoft.WindowsFeedbackHub".</summary>
    public required string PackageId { get; init; }

    public string? StoreId { get; init; }
}
