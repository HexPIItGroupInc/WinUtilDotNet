using WinUtil.Core.Abstractions;

namespace WinUtil.Core.Tests;

/// <summary>In-memory IServices for engine tests.</summary>
public sealed class FakeServices : IServices
{
    private readonly Dictionary<string, string> types = [];

    public FakeServices(params (string Name, string Type)[] seed)
    {
        foreach (var (name, type) in seed)
        {
            types[name] = type;
        }
    }

    public bool TryGetStartupType(string serviceName, out string? startupType)
    {
        if (types.TryGetValue(serviceName, out var found))
        {
            startupType = found;
            return true;
        }

        startupType = null;
        return false;
    }

    public void SetStartupType(string serviceName, string startupType) =>
        types[serviceName] = startupType;
}
