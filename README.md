# WinUtilsDotNet

<!-- CI badge goes here once the GitHub mirror is live:
[![CI](https://github.com/OWNER/REPO/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/REPO/actions/workflows/ci.yml) -->

A native .NET re-implementation of [Chris Titus's winutil](https://github.com/ChrisTitusTech/winutil), proving one architectural thesis:

> winutil is small because it is **data-driven** — its tweaks are JSON catalog entries, not code. A .NET rewrite should keep those catalogs as the contract and rewrite only the *engine*, replacing PowerShell cmdlets with the Win32/COM/.NET APIs they wrap. That hedges against PowerShell being restricted — in a few thousand lines, not the ~200k an unstructured LLM rewrite produced.

**Status: working v1.** All **67** of upstream winutil's tweaks run through the native engine at **100% PowerShell-free** — zero script escape hatches, enforced by CI. Consumes winutil's `config/tweaks.json` and `config/appx.json` **byte-for-byte unchanged**. ~2,500 lines of production C# plus a declarative overlay.

## What works

- **Detect / Apply / Undo** every tweak natively — registry, services, scheduled-task and Appx actions via direct Win32/SCM/WMI/`Windows.Management.Deployment` APIs. No PowerShell process is ever spawned (there's a CI test that asserts it).
- **The UI never lies.** Every action implements `Detect() → Applied | NotApplied | Drifted | Unknown`, reading real system state — so the GUI shows what's *actually* on the machine, including drift when Windows silently reverts a tweak.
- **Real undo.** A transaction journal records each value's true prior state before changing it, so undo restores what the machine had — not a guessed catalog default. Shared across the CLI and GUI, with a source column.
- **Debloat catalog.** `winutil debloat --appx appx.json` drives winutil's full app-removal list through the native `PackageManager`.
- **OneShot.** Apply every actionable tweak in a category with one (confirmed) click.

## Layout

| Project | Role |
|---|---|
| `WinUtil.Core` | Domain model, catalog + overlay loader, orchestration engine, undo journal, coverage metric. **Zero Windows deps** — builds and unit-tests on any OS. |
| `WinUtil.System` | The only Windows-touching layer: adapters behind ports (`IRegistry`, `IServices`, `IAppx`, `ICommandRunner`, `IFileSystem`, `IHostsBlocker`, `ISystemRestore`, `ITokenProvider`). |
| `WinUtil.Cli` | `list / coverage / validate / detect / apply / undo / debloat / journal`. Scriptable, and the CI harness. |
| `WinUtil.App` | Avalonia MVVM GUI — category sidebar, live state chips, per-card apply/undo, OneShot. Runs in browse mode on any OS; full function on Windows. |

The `native/overrides.json` overlay converts the handful of tweaks upstream ships as scripts into typed, native actions **without editing the upstream catalog** — see `docs/adr/0004`. Adding or fixing a tweak is a JSON edit; no C# required (see `CONTRIBUTING.md`).

## Build & run

```sh
dotnet build                                   # warnings are errors
dotnet test                                    # Core unit tests, any OS
WINUTIL_REPO=/path/to/winutil dotnet test      # + tests against the real upstream catalogs

# See the metric against the real catalog:
dotnet run --project src/WinUtil.Cli -- coverage \
  --catalog /path/to/winutil/config/tweaks.json --overlay native/overrides.json
# -> PowerShell-free: 100.0% (67/67 — 42 typed, 25 overlay-converted, 0 escape-hatch)

# The GUI:
dotnet run --project src/WinUtil.App
```

Apply/undo require Windows and admin (like winutil itself — the tweaks write HKLM, services, and Appx). On non-Windows the app opens in read-only browse mode.

## Roadmap

MicroWin (ISO/WIM surgery via the DISM API), a non-elevated GUI that shells to an elevated helper for privileged tweaks, control-type-aware cards (combobox/toggle), and an AOT single-file publish.

## License

MIT, matching upstream.
