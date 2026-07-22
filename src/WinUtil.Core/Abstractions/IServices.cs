namespace WinUtil.Core.Abstractions;

/// <summary>Port to the Windows Service Control Manager.</summary>
public interface IServices
{
    /// <summary>Read a service's startup type. Returns false when the service does not exist.</summary>
    bool TryGetStartupType(string serviceName, out string? startupType);

    void SetStartupType(string serviceName, string startupType);
}
