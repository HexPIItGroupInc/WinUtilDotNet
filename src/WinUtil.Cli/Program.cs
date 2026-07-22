using WinUtil.Core.Catalog;

if (args.Length == 0)
{
    return Usage();
}

var catalogPath = OptionValue(args, "--catalog") ?? "config/tweaks.json";

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
            Console.WriteLine($"PowerShell-free: {c.NativePercent:F1}% ({c.Native}/{c.Total} tweaks native, {c.EscapeHatch} on the script escape hatch)");
        });

    case "validate":
        return WithTweaks(catalogPath, tweaks =>
            Console.WriteLine($"OK: {tweaks.Count} tweaks parsed from {catalogPath}"));

    default:
        return Usage();
}

static int WithTweaks(string catalogPath, Action<IReadOnlyList<WinUtil.Core.Model.Tweak>> action)
{
    if (!File.Exists(catalogPath))
    {
        Console.Error.WriteLine($"Catalog not found: {catalogPath}");
        return 2;
    }

    action(CatalogLoader.LoadTweaks(File.ReadAllText(catalogPath)));
    return 0;
}

static string? OptionValue(string[] args, string option)
{
    var index = Array.IndexOf(args, option);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static int Usage()
{
    Console.Error.WriteLine("""
        usage: winutil <command> [--catalog <path to tweaks.json>]

        commands:
          list        list all catalog tweaks and their execution mode
          coverage    print the PowerShell-free % metric
          validate    parse the catalog and exit 0 if well-formed

        apply / detect / undo land with the Windows integration phase.
        """);
    return 1;
}
