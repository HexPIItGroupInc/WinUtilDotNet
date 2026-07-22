# Contributing

Three tiers of contribution, smallest first. Pick the lowest tier that solves
your problem — if a change needs a higher tier than you expected, that's worth
an issue first.

## Tier 1 — tweaks (zero C#)

Tweaks live in winutil's JSON catalogs, which this project consumes unchanged
(ADR-0001). **New or changed tweaks are contributed to
[upstream winutil](https://github.com/ChrisTitusTech/winutil)**, not here. If
the engine mishandles a valid catalog entry, that's a bug here — open an issue
with the entry.

## Tier 2 — action types (one file)

A new *kind* of system change (say, firewall rules) is: one port interface in
`src/WinUtil.Core/Abstractions/`, its model record in `Model/`, engine dispatch,
and one adapter in `src/WinUtil.System/`. Unit tests use an in-memory fake of
your port. If your PR adds bespoke code for one *specific* tweak, it's at the
wrong tier — push it into data or a general action type (the anti-200k rule).

## Tier 3 — engine and adapters

Core engine changes need unit tests with fakes; adapter changes need a Windows
integration run. Decisions with lasting consequences get an ADR in `docs/adr/`.

## Building

```sh
dotnet build          # zero warnings tolerated (warnings are errors)
dotnet test           # Core tests run on any OS
WINUTIL_REPO=/path/to/winutil dotnet test   # + tests against the real catalogs
dotnet run --project src/WinUtil.Cli -- coverage --catalog /path/to/winutil/config/tweaks.json
```

`WinUtil.Core` must keep zero Windows dependencies — the build enforces this
via target frameworks (ADR-0002). CI runs on Linux; Windows integration runs on
a snapshot-reverted VM.
