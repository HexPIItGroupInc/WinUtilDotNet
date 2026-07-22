using WinUtil.Core.Catalog;

namespace WinUtil.Core.Tests;

/// <summary>
/// Tests against the real, unmodified upstream winutil catalogs. The upstream
/// checkout is located via the WINUTIL_REPO environment variable (CI clones it;
/// locally point it at your winutil clone). Skipped when not available.
/// </summary>
public class UpstreamCatalogTests
{
    private static string? TweaksPath()
    {
        var repo = Environment.GetEnvironmentVariable("WINUTIL_REPO");
        if (repo is null)
        {
            return null;
        }

        var path = Path.Combine(repo, "config", "tweaks.json");
        return File.Exists(path) ? path : null;
    }

    [SkippableFact]
    public void Parses_full_upstream_tweaks_catalog_unchanged()
    {
        var path = TweaksPath();
        Skip.If(path is null, "WINUTIL_REPO not set or has no config/tweaks.json");

        var tweaks = CatalogLoader.LoadTweaks(File.ReadAllText(path));

        Assert.True(tweaks.Count >= 60, $"expected the upstream catalog to have 60+ tweaks, got {tweaks.Count}");
        Assert.All(tweaks, t => Assert.False(string.IsNullOrWhiteSpace(t.Content)));
        Assert.Contains(tweaks, t => t.Registry.Count > 0);
    }

    [SkippableFact]
    public void Reports_baseline_coverage_for_upstream_catalog()
    {
        var path = TweaksPath();
        Skip.If(path is null, "WINUTIL_REPO not set or has no config/tweaks.json");

        var coverage = CatalogCoverage.Measure(CatalogLoader.LoadTweaks(File.ReadAllText(path)));

        // Baseline observed 2026-07-22: 67 tweaks, 25 with InvokeScript. The
        // native share must never regress below the recorded baseline.
        Assert.True(coverage.NativePercent >= 55.0,
            $"PowerShell-free coverage regressed: {coverage.NativePercent:F1}%");
    }
}
