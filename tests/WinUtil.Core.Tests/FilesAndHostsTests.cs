using WinUtil.Core.Abstractions;
using WinUtil.Core.Catalog;
using WinUtil.Core.Engine;
using WinUtil.Core.Model;

namespace WinUtil.Core.Tests;

public class FilesAndHostsTests
{
    private sealed class OrderRecorder : ICommandRunner
    {
        public List<string> Log { get; }

        public OrderRecorder(List<string> log) => Log = log;

        public int Run(CommandAction command) { Log.Add($"cmd:{command.File}"); return 0; }
    }

    private const string Catalog = """
        {
          "WPFTweaksMixed": {
            "Content": "Mixed",
            "InvokeScript": ["x"],
            "registry": [
              { "Path": "HKLM:\\A", "Name": "X", "Value": "1", "Type": "DWord", "OriginalValue": "0" }
            ]
          }
        }
        """;

    private const string Overlay = """
        {
          "WPFTweaksMixed": {
            "preApply": [{ "file": "pre.exe" }],
            "ensureDirs": ["%SystemRoot%\\D"],
            "deletePaths": ["%Temp%\\*"],
            "hostsBlock": { "url": "https://example.invalid/hosts", "removeFromMarker": "#New Ver" },
            "apply": [{ "file": "post.exe" }],
            "undo": [{ "file": "undo.exe" }]
          }
        }
        """;

    [Fact]
    public void Apply_order_is_pre_typed_files_hosts_post()
    {
        var log = new List<string>();
        var engine = new TweakEngine(new LoggingRegistry(log), new InMemoryJournal(), commands: new OrderRecorder(log), files: new LoggingFiles(log), hosts: new LoggingHosts(log));
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        engine.Apply(tweak);

        Assert.Equal(["cmd:pre.exe", "reg:set", "ensure:%SystemRoot%\\D", "delete:%Temp%\\*", "hosts:apply", "cmd:post.exe"], log);
    }

    [Fact]
    public void Undo_removes_blocklist_then_runs_undo_commands()
    {
        var log = new List<string>();
        var engine = new TweakEngine(new LoggingRegistry(log), new InMemoryJournal(), commands: new OrderRecorder(log), files: new LoggingFiles(log), hosts: new LoggingHosts(log));
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        engine.Undo(tweak);

        Assert.Equal(["reg:set", "hosts:remove:#New Ver", "cmd:undo.exe"], log);
    }

    [Fact]
    public void Files_or_hosts_actions_without_adapters_throw_clearly()
    {
        var engine = new TweakEngine(new FakeRegistry(), new InMemoryJournal(), commands: new OrderRecorder([]));
        var tweak = CatalogLoader.LoadTweaks(Catalog, Overlay).Single();

        Assert.Throws<InvalidOperationException>(() => engine.Apply(tweak));
    }

    private sealed class LoggingRegistry(List<string> log) : IRegistry
    {
        public bool TryGetValue(string path, string name, out string? value) { value = null; return false; }

        public void SetValue(string path, string name, string value, string type) => log.Add("reg:set");

        public void DeleteValue(string path, string name) => log.Add("reg:del");

        public void DeleteKeyTree(string path) => log.Add($"reg:delTree:{path}");
    }

    private sealed class LoggingFiles(List<string> log) : IFileSystem
    {
        public void DeleteGlob(string pathWithGlob) => log.Add($"delete:{pathWithGlob}");

        public void EnsureDirectory(string path) => log.Add($"ensure:{path}");
    }

    private sealed class LoggingHosts(List<string> log) : IHostsBlocker
    {
        public void ApplyBlocklist(string url) => log.Add("hosts:apply");

        public void RemoveBlocklist(string marker) => log.Add($"hosts:remove:{marker}");
    }
}
