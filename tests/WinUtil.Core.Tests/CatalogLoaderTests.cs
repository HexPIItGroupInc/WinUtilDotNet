using WinUtil.Core.Catalog;

namespace WinUtil.Core.Tests;

public class CatalogLoaderTests
{
    private static string Fixture => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-tweaks.json"));

    [Fact]
    public void Parses_lenient_json_with_raw_control_characters()
    {
        var tweaks = CatalogLoader.LoadTweaks(Fixture);

        Assert.Equal(3, tweaks.Count);
        var sample = Assert.Single(tweaks, t => t.Id == "WPFTweaksSample");
        Assert.Contains("raw\ttab", sample.Description);
    }

    [Fact]
    public void Maps_registry_changes_faithfully()
    {
        var sample = CatalogLoader.LoadTweaks(Fixture).Single(t => t.Id == "WPFTweaksSample");

        Assert.Equal(2, sample.Registry.Count);
        var first = sample.Registry[0];
        Assert.Equal(@"HKLM:\SOFTWARE\Fixture\Thing", first.Path);
        Assert.Equal("Enabled", first.Name);
        Assert.Equal("0", first.Value);
        Assert.Equal("DWord", first.Type);
        Assert.Equal("1", first.OriginalValue);
    }

    [Fact]
    public void Classifies_native_vs_escape_hatch()
    {
        var tweaks = CatalogLoader.LoadTweaks(Fixture);

        Assert.True(tweaks.Single(t => t.Id == "WPFTweaksSample").IsNativelyExecutable);
        Assert.True(tweaks.Single(t => t.Id == "WPFTweaksService").IsNativelyExecutable);
        Assert.False(tweaks.Single(t => t.Id == "WPFTweaksScripted").IsNativelyExecutable);

        var coverage = CatalogCoverage.Measure(tweaks);
        Assert.Equal(3, coverage.Total);
        Assert.Equal(2, coverage.Native);
        Assert.Equal(1, coverage.EscapeHatch);
    }

    [Fact]
    public void Maps_service_changes()
    {
        var service = CatalogLoader.LoadTweaks(Fixture).Single(t => t.Id == "WPFTweaksService").Service.Single();

        Assert.Equal("FixtureSvc", service.Name);
        Assert.Equal("Disabled", service.StartupType);
        Assert.Equal("Manual", service.OriginalType);
    }
}
