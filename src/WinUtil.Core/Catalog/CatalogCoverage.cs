using WinUtil.Core.Model;

namespace WinUtil.Core.Catalog;

/// <summary>The "PowerShell-free %" burn-down metric.</summary>
public sealed record CatalogCoverage(int Total, int Native, int EscapeHatch)
{
    public double NativePercent => Total == 0 ? 100.0 : 100.0 * Native / Total;

    public static CatalogCoverage Measure(IEnumerable<Tweak> tweaks)
    {
        var all = tweaks.ToList();
        var native = all.Count(t => t.IsNativelyExecutable);
        return new CatalogCoverage(all.Count, native, all.Count - native);
    }
}
