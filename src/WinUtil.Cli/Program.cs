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
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinUtilsDotNet", "journal.json");

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

    default:
        return Usage();
}

int WithEngine(string catalog, string journal, Action<TweakEngine, IReadOnlyList<Tweak>> action)
{
#if WINDOWS
    return WithTweaks(catalog, tweaks =>
        action(new TweakEngine(new WinUtil.System.WindowsRegistry(), new FileJournal(journal), new WinUtil.System.WindowsServices(), new WinUtil.System.ProcessCommandRunner()), tweaks));
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
        """);
    return 1;
}
