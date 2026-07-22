# ADR-0004: script burn-down via a native overlay, not a catalog fork

**Status:** accepted · 2026-07-22

## Context

25 of 67 upstream tweaks carry `InvokeScript`/`UndoScript`. ADR-0001 forbids
editing the upstream catalogs, so conversions cannot happen "in place". Survey
shows most scripts are not PowerShell-dependent at all — they invoke plain
executables (powercfg, netsh, DISM, icacls) or just restart Explorer.

## Decision

`native/overrides.json` — a repo-owned overlay keyed by tweak id. An entry
declares `apply`/`undo` lists of **command actions** (direct exe invocations,
no shell, `%Var%` expansion, optional `ignoreExitCode`). When a tweak has an
overlay entry its scripts count as covered: the engine executes the commands
and never touches PowerShell, and the coverage metric counts it native.

An overlay id that no longer exists upstream fails the catalog load — a stale
overlay is a contract break we want CI to surface, not skip.

## Consequences

- Converting a script tweak is a **JSON-only contribution** (Tier 1 ergonomics
  in this repo), reviewable line by line against the original script.
- The long-term path remains contributing typed representations upstream; the
  overlay is the migration vehicle, shrinking from both ends.
- Complex scripts (Appx removal, OneDrive migration) wait for real typed action
  types rather than being crammed into command lists.
