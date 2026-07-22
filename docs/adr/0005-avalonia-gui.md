# ADR-0005: Avalonia for the GUI

**Status:** accepted · 2026-07-22

## Context

The GUI phase needed the deferred framework decision. 2026 options: WPF
(mature, Chris-familiar XAML, Windows-only, no AOT, dated defaults), WinUI 3
(current Microsoft, notoriously rough tooling/packaging), Avalonia (XAML-family,
Fluent theme built in, AOT single-exe capable, cross-platform).

## Decision

Avalonia. The deciding factor is the development loop: this project is built
and reviewed from Linux (maintainer and CI both), and Avalonia runs there —
the app opens on a Linux desktop in browse/design mode (engine actions are
Windows-only via the same conditional composition as the CLI). WPF would make
the UI reviewable only on the Windows test VM. Secondary factors: AOT
single-file publish suits the one-artifact winutil ethos; the Fluent dark
theme gives a modern look without third-party theme dependencies.

## Consequences

- `WinUtil.App` multi-targets like the CLI: `net10.0` everywhere (browse mode,
  engine null) + `net10.0-windows10.0.19041.0` (full function).
- Chris's WPF familiarity doesn't transfer 1:1, but the XAML dialect is close.
- One transitive pin (`Tmds.DBus.Protocol` ≥ 0.92.0) — Avalonia 11.3.2 pulls a
  version with GHSA-xrw6-gwf8-vvr9, and warnings-as-errors rightly refused it.
