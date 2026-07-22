using WinUtil.Core.Abstractions;
using WinUtil.Core.Catalog;
using WinUtil.Core.Engine;
using WinUtil.Core.Model;

namespace WinUtil.Core.Tests;

public class OverlayTests
{
    private const string Catalog = """
        {
          "WPFTweaksScripted": {
            "Content": "Scripted",
            "InvokeScript": ["powercfg.exe /hibernate off"],
            "UndoScript": ["powercfg.exe /hibernate on"]
          },
          "WPFTweaksPlain": { "Content": "Plain" }
        }
        """;

    private const string Overlay = """
        {
          "WPFTweaksScripted": {
            "apply": [{ "file": "powercfg.exe", "args": "/hibernate off" }],
            "undo": [{ "file": "powercfg.exe", "args": "/hibernate on", "ignoreExitCode": true }]
          }
        }
        """;

    private sealed class RecordingRunner(int exitCode = 0) : ICommandRunner
    {
        public List<string> Ran { get; } = [];

        public int Run(CommandAction command)
        {
            Ran.Add($"{command.File} {command.Args}".Trim());
            return exitCode;
        }
    }

    [Fact]
    public void Overlay_marks_scripts_covered_and_carries_commands()
    {
        var tweaks = CatalogLoader.LoadTweaks(Catalog, Overlay);
        var scripted = tweaks.Single(t => t.Id == "WPFTweaksScripted");

        Assert.True(scripted.HasScripts);
        Assert.True(scripted.ScriptsCovered);
        Assert.True(scripted.IsNativelyExecutable);
        Assert.Equal("/hibernate off", scripted.Commands.Single().Args);
        Assert.True(scripted.UndoCommands.Single().IgnoreExitCode);

        var coverage = CatalogCoverage.Measure(tweaks);
        Assert.Equal(1, coverage.TypedNative);
        Assert.Equal(1, coverage.Overlaid);
        Assert.Equal(0, coverage.EscapeHatch);
        Assert.Equal(100.0, coverage.NativePercent);
    }

    [Fact]
    public void Stale_overlay_id_fails_the_load()
    {
        var stale = """{ "WPFTweaksGone": { "apply": [] } }""";

        var e = Assert.Throws<FormatException>(() => CatalogLoader.LoadTweaks(Catalog, stale));
        Assert.Contains("WPFTweaksGone", e.Message);
    }

    [Fact]
    public void Engine_runs_apply_and_undo_commands()
    {
        var runner = new RecordingRunner();
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal(), commands: runner);
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single(t => t.Id == "WPFTweaksScripted");

        engine.Apply(tweak);
        engine.Undo(tweak);

        Assert.Equal(["powercfg.exe /hibernate off", "powercfg.exe /hibernate on"], runner.Ran);
    }

    [Fact]
    public void Nonzero_exit_fails_unless_ignored()
    {
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal(), commands: new RecordingRunner(exitCode: 5));
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single(t => t.Id == "WPFTweaksScripted");

        Assert.Throws<InvalidOperationException>(() => engine.Apply(tweak));
        engine.Undo(tweak); // undo command has ignoreExitCode: true
    }

    [Fact]
    public void Commands_without_runner_throw_clearly()
    {
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal());
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single(t => t.Id == "WPFTweaksScripted");

        Assert.Throws<InvalidOperationException>(() => engine.Apply(tweak));
    }

    [SkippableFact]
    public void Repo_overlay_file_is_valid_against_the_real_catalog()
    {
        var overlayPath = FindUp("native/overrides.json");
        var repo = Environment.GetEnvironmentVariable("WINUTIL_REPO");
        Skip.If(overlayPath is null || repo is null, "overlay or WINUTIL_REPO not found");

        var tweaks = CatalogLoader.LoadTweaks(
            File.ReadAllText(Path.Combine(repo!, "config", "tweaks.json")),
            File.ReadAllText(overlayPath!));

        Assert.True(CatalogCoverage.Measure(tweaks).Overlaid >= 9);
    }

    private static string? FindUp(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
