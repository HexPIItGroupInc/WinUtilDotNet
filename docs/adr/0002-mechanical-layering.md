# ADR-0002: layering is enforced by project references, not discipline

**Status:** accepted · 2026-07-22

## Context

The codebase must stay maintainable by a volunteer community. Review-time
conventions ("please don't call the registry from the engine") do not survive
hundreds of contributors.

## Decision

- `WinUtil.Core` targets plain `net10.0`, references **nothing**, and defines
  the ports (`IRegistry`, `IServices`, `IScriptRunner`, …). It cannot compile a
  Windows API call.
- `WinUtil.System` targets `net10.0-windows` and is the **only** project that
  touches Windows. Per-Windows-version quirks live here.
- Front-ends (`WinUtil.Cli`, later `WinUtil.App`) compose Core with System's
  adapters; they contain no tweak logic.
- `TreatWarningsAsErrors` + analyzers + `.editorconfig` ship in-repo, so style
  and safety are build errors, not review arguments.

## Consequences

- Core builds and unit-tests on any OS — contributors without Windows can work
  on the engine and catalogs with full confidence.
- The engine is testable with in-memory fakes; Windows integration tests are
  reserved for the real adapters on a snapshot-reverted VM.
- `WinUtil.Core` is NuGet-publishable for third-party front-ends.
