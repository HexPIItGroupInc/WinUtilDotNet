using WinUtil.Core.Engine;
using WinUtil.Core.Model;

namespace WinUtil.Core.Tests;

public class ServiceAndJournalTests
{
    private static Tweak ServiceTweak() => new()
    {
        Id = "WPFTweaksSvc",
        Content = "Service tweak",
        Service = [new ServiceChange { Name = "TestSvc", StartupType = "Disabled", OriginalType = "Manual" }],
    };

    [Fact]
    public void Service_apply_detect_undo_roundtrip()
    {
        var services = new FakeServices(("TestSvc", "Manual"));
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal(), services);
        var tweak = ServiceTweak();

        Assert.Equal(ActionState.NotApplied, engine.Detect(tweak));
        engine.Apply(tweak);
        Assert.Equal(ActionState.Applied, engine.Detect(tweak));
        engine.Undo(tweak);
        Assert.Equal(ActionState.NotApplied, engine.Detect(tweak));

        Assert.True(services.TryGetStartupType("TestSvc", out var restored));
        Assert.Equal("Manual", restored);
    }

    [Fact]
    public void Service_undo_restores_journaled_type_over_catalog_original()
    {
        // Machine had Automatic (not the catalog's assumed Manual) before apply.
        var services = new FakeServices(("TestSvc", "Automatic"));
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal(), services);
        var tweak = ServiceTweak();

        engine.Apply(tweak);
        engine.Undo(tweak);

        Assert.True(services.TryGetStartupType("TestSvc", out var restored));
        Assert.Equal("Automatic", restored);
    }

    [Fact]
    public void Service_tweak_without_adapter_throws_clearly()
    {
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal());

        Assert.Throws<InvalidOperationException>(() => engine.Apply(ServiceTweak()));
    }

    [Fact]
    public void FileJournal_persists_across_instances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"winutil-journal-test-{Guid.NewGuid():N}.json");
        try
        {
            var first = new FileJournal(path);
            first.Record(new JournalEntry("WPFTweaksX", @"HKLM:\A", "X", "7", true));
            first.Record(new JournalEntry("WPFTweaksX", "SomeSvc", "StartupType", "Manual", true, JournalEntry.ServiceKind));

            var second = new FileJournal(path);
            var entries = second.EntriesFor("WPFTweaksX");
            Assert.Equal(2, entries.Count);
            Assert.Equal("7", entries[0].PreviousValue);
            Assert.Equal(JournalEntry.ServiceKind, entries[1].Kind);

            second.Clear("WPFTweaksX");
            Assert.Empty(new FileJournal(path).EntriesFor("WPFTweaksX"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
