using WinUtil.Core.Abstractions;
using WinUtil.Core.Catalog;
using WinUtil.Core.Engine;

namespace WinUtil.Core.Tests;

public class AppxTests
{
    private sealed class FakeAppx : IAppx
    {
        public Dictionary<string, List<string>> Installed { get; } = [];

        public List<string> Removed { get; } = [];

        public IReadOnlyList<string> FindInstalled(string pattern) =>
            Installed.TryGetValue(pattern, out var found) ? found : [];

        public void Remove(string packageFullName) => Removed.Add(packageFullName);
    }

    [Fact]
    public void Appx_catalog_parses_upstream_shape()
    {
        const string json = """
            {
              "WPFAppxMicrosoft_GetHelp": {
                "Category": "Microsoft Apps",
                "Content": "Get Help",
                "PackageId": "Microsoft.GetHelp",
                "StoreId": "9PKDZBMV1H3T"
              }
            }
            """;

        var package = CatalogLoader.LoadAppx(json).Single();
        Assert.Equal("Microsoft.GetHelp", package.PackageId);
        Assert.Equal("Get Help", package.Content);
        Assert.Equal("9PKDZBMV1H3T", package.StoreId);
    }

    [Fact]
    public void Engine_removes_every_installed_match_for_overlay_patterns()
    {
        const string catalog = """{ "WPFTweaksW": { "Content": "W", "InvokeScript": ["x"] } }""";
        const string overlay = """{ "WPFTweaksW": { "appxRemove": ["WebExperience"] } }""";

        var appx = new FakeAppx();
        appx.Installed["WebExperience"] = ["MicrosoftWindows.Client.WebExperience_424.1301.450.0_x64__cw5n1h2txyewy"];

        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal(), appx: appx);
        var tweak = CatalogLoader.LoadTweaks(catalog, overlay).Single();

        Assert.True(tweak.IsNativelyExecutable);
        engine.Apply(tweak);

        Assert.Equal(appx.Installed["WebExperience"], appx.Removed);
    }

    [Fact]
    public void Appx_overlay_without_adapter_throws_clearly()
    {
        const string catalog = """{ "WPFTweaksW": { "Content": "W", "InvokeScript": ["x"] } }""";
        const string overlay = """{ "WPFTweaksW": { "appxRemove": ["WebExperience"] } }""";

        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal());
        var tweak = CatalogLoader.LoadTweaks(catalog, overlay).Single();

        Assert.Throws<InvalidOperationException>(() => engine.Apply(tweak));
    }
}
