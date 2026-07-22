namespace WinUtil.Core.Model;

/// <summary>
/// Overlay action: fetch a hosts-format blocklist and append it to the system
/// hosts file. Undo truncates the file from the first occurrence of
/// <see cref="RemoveFromMarker"/> (mirroring upstream's regex-removal undo).
/// </summary>
public sealed record HostsBlock
{
    public required string Url { get; init; }

    public required string RemoveFromMarker { get; init; }
}
