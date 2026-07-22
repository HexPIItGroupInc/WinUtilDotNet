using System.Runtime.Versioning;
using WinUtil.Core.Abstractions;

namespace WinUtil.System;

/// <summary>
/// IHostsBlocker against %SystemRoot%\System32\drivers\etc\hosts. Fetches the
/// blocklist over plain HTTPS (HttpClient) and appends it verbatim, matching
/// upstream's Invoke-RestMethod + Add-Content behavior.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HostsFileBlocker : IHostsBlocker
{
    private static string HostsPath =>
        Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\drivers\etc\hosts");

    public void ApplyBlocklist(string url)
    {
        using var http = new HttpClient();
        var content = http.GetStringAsync(url).GetAwaiter().GetResult();
        File.AppendAllText(HostsPath, Environment.NewLine + content);
    }

    public void RemoveBlocklist(string marker)
    {
        var text = File.ReadAllText(HostsPath);
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index >= 0)
        {
            File.WriteAllText(HostsPath, text[..index].TrimEnd() + Environment.NewLine);
        }
    }
}
