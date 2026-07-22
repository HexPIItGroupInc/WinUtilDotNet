using WinUtil.Core.Abstractions;
using WinUtil.Core.Catalog;
using WinUtil.Core.Engine;
using WinUtil.Core.Model;

namespace WinUtil.Core.Tests;

public class TokenTests
{
    private sealed class FakeTokens : ITokenProvider
    {
        public string Resolve(string text) => text.Replace("{{RAM_KB}}", "8388608", StringComparison.Ordinal);
    }

    private const string Catalog = """{ "WPFTweaksSvc": { "Content": "Svc", "InvokeScript": ["x"] } }""";
    private const string Overlay = """
        {
          "WPFTweaksSvc": {
            "registry": [
              { "Path": "HKLM:\\SYSTEM\\CurrentControlSet\\Control", "Name": "SvcHostSplitThresholdInKB", "Value": "{{RAM_KB}}", "Type": "DWord" }
            ]
          }
        }
        """;

    [Fact]
    public void Tokens_expand_on_apply_and_detect()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal(), tokens: new FakeTokens());
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        engine.Apply(tweak);

        Assert.True(registry.TryGetValue(@"HKLM:\SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", out var value));
        Assert.Equal("8388608", value);
        Assert.Equal(ActionState.Applied, engine.Detect(tweak));
    }

    [Fact]
    public void Unknown_token_without_provider_is_left_literal_but_never_matches()
    {
        var registry = new FakeRegistry();
        var engine = new TweakEngine(registry, new InMemoryJournal());
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        // No provider: value passes through unexpanded (documents the guard).
        engine.Apply(tweak);
        Assert.True(registry.TryGetValue(@"HKLM:\SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", out var value));
        Assert.Equal("{{RAM_KB}}", value);
    }
}
