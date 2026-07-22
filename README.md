# WinUtilsDotNet

A native .NET re-implementation of [Chris Titus's winutil](https://github.com/ChrisTitusTech/winutil), built to prove one architectural thesis:

> winutil is small because it is **data-driven** — its tweaks are JSON catalog entries, not code. A .NET rewrite should keep the catalogs as the contract and rewrite only the *engine*, replacing PowerShell cmdlets with the Win32/COM/.NET APIs they wrap (`Microsoft.Win32.Registry`, SCM, Task Scheduler COM, `Windows.Management.Deployment`, winget COM, DISM API). That hedges against PowerShell being restricted, in ~10–20k lines — not 200k.

## Planned layout

| Project | Role |
|---|---|
| `WinUtil.Core` | Domain models, catalog loader (consumes upstream `config/*.json` unchanged), orchestration engine, undo journal, dry-run. Zero Windows deps — unit-tests anywhere. |
| `WinUtil.System` | The only Windows-touching layer: adapters (`IRegistry`, `IServices`, `IScheduledTasks`, `IAppx`, `IPackageInstaller`) behind interfaces. |
| `WinUtil.Cli` | Headless `list / apply / detect / undo` over the engine; CI test harness. |
| `WinUtil.App` | GUI (later phase). Headline: the UI never lies — every action implements `Detect() → Applied / NotApplied / Drifted / Unknown`. |

Phases: 1) native registry/service/scheduled-task/appx actions + PowerShell escape hatch with a tracked "PowerShell-free %" burn-down → 2) burn down the escape hatch → 3) GUI → 4) MicroWin via DISM/wimgapi.

## Status

Planning. Spec: vault `Specs/SPEC winutil-dotnet-rewrite.md` (HexPi-Infrastructure). Implementation starts late July 2026.

## License

MIT, matching upstream.
