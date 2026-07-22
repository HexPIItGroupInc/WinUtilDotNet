using WinUtil.Core.Model;

namespace WinUtil.Core.Catalog;

/// <summary>The "PowerShell-free %" burn-down metric.</summary>
public sealed record CatalogCoverage(int Total, int TypedNative, int Overlaid, int EscapeHatch)
{
    public int Native => TypedNative + Overlaid;

    public double NativePercent => Total == 0 ? 100.0 : 100.0 * Native / Total;

    public static CatalogCoverage Measure(IEnumerable<Tweak> tweaks)
    {
        var all = tweaks.ToList();
        var typed = all.Count(t => !t.HasScripts);
        var overlaid = all.Count(t => t.HasScripts && t.ScriptsCovered);
        return new CatalogCoverage(all.Count, typed, overlaid, all.Count - typed - overlaid);
    }
}
