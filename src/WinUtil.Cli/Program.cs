using WinUtil.Core.Catalog;
using WinUtil.Core.Engine;
using WinUtil.Core.Model;

if (args.Length == 0)
{
    return Usage();
}

var catalogPath = OptionValue(args, "--catalog") ?? "config/tweaks.json";
var overlayPath = OptionValue(args, "--overlay") ?? (File.Exists("native/overrides.json") ? "native/overrides.json" : null);
var journalPath = OptionValue(args, "--journal")
#if WINDOWS
    ?? WinUtil.System.SharedJournal.EnsureWritable();
#else
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinUtilsDotNet", "journal.json");
#endif

switch (args[0])
{
    case "list":
        return WithTweaks(catalogPath, tweaks =>
        {
            foreach (var t in tweaks.OrderBy(t => t.Category).ThenBy(t => t.Content))
            {
                var mode = t.IsNativelyExecutable ? "native" : "script";
                Console.WriteLine($"{t.Id,-40} [{mode}] reg:{t.Registry.Count} svc:{t.Service.Count}  {t.Content}");
            }
        });

    case "coverage":
        return WithTweaks(catalogPath, tweaks =>
        {
            var c = CatalogCoverage.Measure(tweaks);
            Console.WriteLine($"PowerShell-free: {c.NativePercent:F1}% ({c.Native}/{c.Total} — {c.TypedNative} typed, {c.Overlaid} overlay-converted, {c.EscapeHatch} still on the script escape hatch)");
        });

    case "validate":
        return WithTweaks(catalogPath, tweaks =>
            Console.WriteLine($"OK: {tweaks.Count} tweaks parsed from {catalogPath}"));

    case "detect":
        return WithEngine(catalogPath, journalPath, (engine, tweaks) =>
        {
            var targets = args.Contains("--all")
                ? tweaks.Where(t => t.Registry.Count > 0 || t.Service.Count > 0)
                : [FindTweak(tweaks, RequireId())];

            foreach (var t in targets)
            {
                Console.WriteLine($"{t.Id,-40} {engine.Detect(t)}  {t.Content}");
            }
        });

    case "apply":
        return WithEngine(catalogPath, journalPath, (engine, tweaks) =>
        {
            var tweak = FindTweak(tweaks, RequireId());
            RequireNative(tweak);
            engine.Apply(tweak);
            Console.WriteLine($"applied: {tweak.Id} -> {engine.Detect(tweak)}");
        });

    case "undo":
        return WithEngine(catalogPath, journalPath, (engine, tweaks) =>
        {
            var tweak = FindTweak(tweaks, RequireId());
            RequireNative(tweak);
            engine.Undo(tweak);
            Console.WriteLine($"undone: {tweak.Id} -> {engine.Detect(tweak)}");
        });

    case "journal":
    {
        if (!File.Exists(journalPath))
        {
            Console.WriteLine($"Journal is empty ({journalPath}).");
            return 0;
        }

        var snapshot = new FileJournal(journalPath).Snapshot();
        if (snapshot.Count == 0)
        {
            Console.WriteLine("Journal is empty — nothing applied through this tool.");
            return 0;
        }

        Console.WriteLine($"Applied tweaks recorded in {journalPath}:");
        foreach (var (tweakId, tweakEntries) in snapshot.OrderBy(kv => kv.Key))
        {
            var source = tweakEntries.Select(e => e.Source).Distinct().FirstOrDefault() ?? "unknown";
            Console.WriteLine($"  {tweakId,-42} via {source,-4} ({tweakEntries.Count} value(s) journaled)");
        }

        return 0;
    }

    case "debloat":
    {
#if WINDOWS
        var appxCatalogPath = OptionValue(args, "--appx");
        if (appxCatalogPath is null || !File.Exists(appxCatalogPath))
        {
            Console.Error.WriteLine("debloat requires --appx <path to appx.json>");
            return 2;
        }

        var packages = CatalogLoader.LoadAppx(File.ReadAllText(appxCatalogPath));
        var appxPort = new WinUtil.System.WindowsAppx();
        var dryRun = args.Contains("--dry-run");
        var present = 0;

        foreach (var package in packages)
        {
            var found = appxPort.FindInstalled(package.PackageId);
            if (found.Count == 0)
            {
                continue;
            }

            present++;
            if (dryRun)
            {
                Console.WriteLine($"{package.PackageId,-50} INSTALLED ({package.Content})");
            }
            else
            {
                foreach (var fullName in found)
                {
                    appxPort.Remove(fullName);
                }

                Console.WriteLine($"{package.PackageId,-50} removed");
            }
        }

        Console.WriteLine($"{(dryRun ? "Would remove" : "Removed")} {present} of {packages.Count} debloat catalog targets present on this system.");
        return 0;
#else
        Console.Error.WriteLine("debloat requires Windows (this build targets another OS).");
        return 3;
#endif
    }

    default:
        return Usage();
}

int WithEngine(string catalog, string journal, Action<TweakEngine, IReadOnlyList<Tweak>> action)
{
#if WINDOWS
    return WithTweaks(catalog, tweaks =>
    {
        var appx = new WinUtil.System.WindowsAppx();
        action(new TweakEngine(new WinUtil.System.WindowsRegistry(), new FileJournal(journal), new WinUtil.System.WindowsServices(), new WinUtil.System.ProcessCommandRunner(), appx, new WinUtil.System.NativeFileSystem(), new WinUtil.System.HostsFileBlocker(), new WinUtil.System.WindowsTokenProvider(appx), new WinUtil.System.WindowsSystemRestore(), source: "cli"), tweaks);
    });
#else
    Console.Error.WriteLine("apply/detect/undo require Windows (this build targets another OS).");
    return 3;
#endif
}

string RequireId()
{
    var id = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
    return id ?? throw new ArgumentException("Missing tweak id. Usage: winutil <apply|detect|undo> <TweakId> [--catalog path] [--journal path]");
}

static Tweak FindTweak(IReadOnlyList<Tweak> tweaks, string id) =>
    tweaks.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No tweak '{id}' in the catalog.");

static void RequireNative(Tweak tweak)
{
    if (!tweak.IsNativelyExecutable)
    {
        throw new NotSupportedException(
            $"'{tweak.Id}' still uses the script escape hatch (InvokeScript/UndoScript); native execution refuses it. This is the burn-down backlog.");
    }
}

int WithTweaks(string catalogPath, Action<IReadOnlyList<Tweak>> action)
{
    if (!File.Exists(catalogPath))
    {
        Console.Error.WriteLine($"Catalog not found: {catalogPath}");
        return 2;
    }

    try
    {
        action(CatalogLoader.LoadTweaks(File.ReadAllText(catalogPath), overlayPath is null ? null : File.ReadAllText(overlayPath)));
        return 0;
    }
    catch (Exception e) when (e is ArgumentException or NotSupportedException or InvalidOperationException)
    {
        Console.Error.WriteLine(e.Message);
        return 4;
    }
}

static string? OptionValue(string[] args, string option)
{
    var index = Array.IndexOf(args, option);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static int Usage()
{
    Console.Error.WriteLine("""
        usage: winutil <command> [args] [--catalog <path to tweaks.json>] [--journal <path>]

        commands:
          list                 list all catalog tweaks and their execution mode
          coverage             print the PowerShell-free % metric
          validate             parse the catalog and exit 0 if well-formed
          detect <id>|--all    read the REAL system state: Applied | NotApplied | Drifted | Unknown
          apply <id>           apply a tweak natively (journals prior values; refuses script tweaks)
          undo <id>            restore journaled values (falls back to catalog originals)
          journal              list tweaks applied through this tool, with source (cli/gui)
          debloat --appx <path> [--dry-run]
                               remove (or list, with --dry-run) installed appx.json debloat targets
        """);
    return 1;
}
