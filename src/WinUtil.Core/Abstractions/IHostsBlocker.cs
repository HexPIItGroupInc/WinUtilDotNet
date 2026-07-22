namespace WinUtil.Core.Abstractions;

/// <summary>Port for hosts-file blocklist actions.</summary>
public interface IHostsBlocker
{
    /// <summary>Fetch the blocklist at the URL and append it to the hosts file.</summary>
    void ApplyBlocklist(string url);

    /// <summary>Truncate the hosts file from the first occurrence of the marker.</summary>
    void RemoveBlocklist(string marker);
}
