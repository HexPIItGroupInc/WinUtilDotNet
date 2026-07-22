# ADR-0001: winutil's JSON catalogs are the contract

**Status:** accepted · 2026-07-22

## Context

winutil stays maintainable at ~14k lines because its hundreds of tweaks are
declarative JSON, interpreted by a small engine. A previous LLM-driven .NET
rewrite translated tweaks into bespoke code and ballooned to ~200k lines.

## Decision

We consume winutil's `config/*.json` files **unchanged** — same schema, same
values, byte-for-byte the upstream files. The domain model mirrors the upstream
field names. Catalog improvements are contributed upstream, never forked.
Script entries (`InvokeScript`/`UndoScript`) are supported through an explicit
escape-hatch port (`IScriptRunner`) whose usage is a tracked metric
("PowerShell-free %") to be burned down to zero.

## Consequences

- CI validates every commit against a fresh clone of the upstream catalogs; an
  upstream schema change breaks our build, not our users.
- Adding a tweak requires zero C# (see CONTRIBUTING).
- The loader must accept upstream's lenient JSON (see ADR-0003).
