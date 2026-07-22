# SPEC: a native .NET winutil

## How we got here

In his 2026-07-22 video, Chris Titus described a real risk to [winutil](https://github.com/ChrisTitusTech/winutil): Windows could one day restrict the PowerShell commands the tool depends on. Everything winutil does is also achievable in .NET — so he had an LLM attempt a full rewrite. The result was ~200,000 lines of code he couldn't maintain, versus a winutil that is only ~14,000 lines of PowerShell plus ~4,700 lines of JSON.

That outcome was an architecture failure, not a capability limit. winutil is small because of the best design decision in it: **it is data-driven**. Tweaks are not code — they're JSON entries declaring registry keys, service startup types, scheduled tasks, and appx packages, with a script escape hatch for the odd cases. A small engine interprets the catalog. An LLM told to "rewrite this in .NET" translates each of hundreds of tweaks into bespoke C#, duplicating the glue every time. That's how 14k becomes 200k.

This project takes the opposite bet: **keep the catalogs as the contract, rewrite only the engine** — and replace PowerShell cmdlets with the Win32/COM/.NET APIs they wrap. Those APIs (`Microsoft.Win32.Registry`, the Service Control Manager, Task Scheduler COM, `Windows.Management.Deployment`, the winget COM API, DISM/wimgapi) are what PowerShell itself calls; they are far less likely to be taken away than cmdlets. Target: full capability in ~10–20k lines of C#, consuming winutil's existing `config/*.json` **unchanged**.

## How this is being built

The method matters as much as the architecture — it's the difference between this and the 2-billion-token attempt:

- **Spec before code.** This document was drafted in the HexPi knowledge vault (Obsidian) under a spec-first convention — `draft → human red-pen → approved → implemented` — before any C# exists. Correcting a paragraph is cheaper than correcting a diff; an LLM pointed at 200k lines never got that chance.
- **Coordinated agent sessions.** Work runs as parallel Claude Code sessions coordinated through a shared WIP claim board in the vault, so every session (and the human) can see who's touching what. Each finished task graduates with a written outcome and a change report closing against this spec.
- **A real integration rig.** Unit tests for `WinUtil.Core` run on Linux; integration runs on a dedicated, disposable Windows 11 test VM on `hecaton` (the HexPi Incus host) with a self-hosted CI runner — so "the tweak applied, detected, and undid cleanly" is a machine-checked fact on a real Windows install, not a hope. Snapshot-revert bracketing can be added if test pollution ever becomes a problem.
- **Acceptance checks, not vibes.** "Done" is the checklist at the bottom of this document, every line something a session can verify with a command.

## Goals

- A layered .NET implementation of winutil's capabilities with no runtime dependency on PowerShell (a tracked, shrinking compat layer during migration).
- A codebase a volunteer community can maintain and extend — the same property that made winutil itself contributor-friendly.
- A UI that never lies: every action can *detect* its real system state, not just apply it.

## Non-goals

- Not a fork of winutil's catalogs: `config/*.json` is consumed as-is; catalog changes belong upstream.
- Not a PR against the winutil repo — winutil's own SPEC.md declares a packaged desktop app out of scope there; this lives beside it.
- Not cross-platform at runtime: Windows-only. (The core library builds and unit-tests anywhere.)

## Architecture

Four projects, one solution:

| Project | Role |
|---|---|
| `WinUtil.Core` | Domain models (`Tweak`, typed actions), JSON catalog loader + schema validation, orchestration engine, transaction journal (original values recorded → real undo), dry-run diff mode. **Zero Windows dependencies** — enforced by project references, unit-testable on any OS. |
| `WinUtil.System` | The only layer that touches Windows: thin adapters behind interfaces (`IRegistry`, `IServices`, `IScheduledTasks`, `IAppx`, `IPackageInstaller`). Per-Windows-version quirks are isolated here. |
| `WinUtil.Cli` | Headless `list / apply / detect / undo` over the same engine. Scriptable, automatable, and the CI test harness. |
| `WinUtil.App` | WPF MVVM GUI over the same engine (later phase). |

### The UI never lies

winutil's toggles show what the tool *infers* was applied; Windows updates silently revert things and the UI drifts from reality. Here, every action type implements `Detect() → Applied | NotApplied | Drifted | Unknown` — the engine can read as well as write, the UI binds to detected state, and drift ("you applied this; an update reverted it") is surfaced explicitly. This falls out nearly for free from a typed engine and is close to impossible to retrofit into the script version.

## Community & reusability requirements

These are first-class requirements, not aspirations:

- **The 90% contribution** (add or change a tweak) requires **zero C#** — a JSON catalog edit, validated by schema in CI.
- **The 9% contribution** (a new action type) is one interface implementation plus its schema fragment — one file, a documented extension point, no core surgery.
- **Layering is mechanical**: project references make it impossible for `Core` to reference Windows APIs; analyzers and `.editorconfig` make style a build error, not a review argument.
- `WinUtil.Core` is a reusable, NuGet-publishable library — other front-ends (a TUI, a fleet tool) are expected consumers, not forks.
- CI on every PR: build, unit tests, catalog schema validation, lint. Contributors without a Windows machine can still land Core and catalog changes confidently.
- Decisions are recorded as lightweight ADRs in `docs/adr/`; `CONTRIBUTING.md` walks the three contribution tiers (tweak / action type / engine).
- **The anti-200k rule**: if a PR adds bespoke code for a single tweak, it's at the wrong layer — push it into data, or into an action type.

## Migration plan

1. **Phase 1** — engine + native `registry` / `service` / `ScheduledTask` / `appx` actions, plus a PowerShell escape hatch for `InvokeScript` tweaks, with a visible **"PowerShell-free: N%"** metric.
2. **Phase 2** — burn down the escape hatch, tweak by tweak, each a small reviewable PR.
3. **Phase 3** — the WPF GUI.
4. **Phase 4** — MicroWin (WIM/ISO surgery) via the DISM API / wimgapi.

The proof-of-concept is a Phase 1 subset: registry + service + appx actions native, driven by winutil's real `tweaks.json`.

## Proof-of-concept acceptance checks

- [ ] `dotnet test` on `WinUtil.Core` passes on Linux (proves zero Windows deps in Core).
- [ ] CLI parses upstream `config/tweaks.json` **unchanged**, lists all tweaks with their action breakdown.
- [ ] On the Windows test VM on `hecaton`: applying a registry tweak spawns no `powershell.exe`/`pwsh.exe` process and `detect` reports `Applied`.
- [ ] `undo` restores original values from the journal; `detect` reports `NotApplied`.
- [ ] Changing an applied tweak's key externally makes `detect` report `Drifted`.
- [ ] CLI reports the coverage metric: % of catalog entries executable natively vs. escape-hatch.
- [ ] Adding a demo tweak end-to-end touches only JSON.
- [ ] `CONTRIBUTING.md` and the first ADRs (layering, catalog-as-contract, GUI framework) exist.

## Open questions

(none — see Resolved)

## Resolved

- GUI framework: **Avalonia** (ADR-0005). Deciding factor: the development loop — maintainer and CI are on Linux, where Avalonia runs in browse/design mode; WPF would be reviewable only on the Windows VM. AOT single-exe publish also suits the one-artifact ethos.

- Chris's "settings" pain point (per his 2026-07-22 video): **UI state drift** — toggles showing inferred rather than real system state. Addressed head-on by first-class `Detect()`; already demonstrated in CI, where a `detect --all` sweep of the live test VM correctly reported partially-reverted tweaks as `Drifted`.

## License

MIT, matching upstream winutil.
