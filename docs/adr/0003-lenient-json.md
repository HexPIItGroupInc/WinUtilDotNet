# ADR-0003: sanitize upstream's lenient JSON instead of forking it

**Status:** accepted · 2026-07-22

## Context

Upstream `tweaks.json` is not strict JSON: string literals contain raw control
characters (tabs, newlines). PowerShell's `ConvertFrom-Json` tolerates this;
`System.Text.Json` (correctly) rejects it. Discovered 2026-07-22 when both
`jq` and Python's strict parser refused the file.

## Decision

`JsonSanitizer` escapes control characters found inside string literals before
parsing. We do **not** reformat, fix, or vendor a corrected copy of the catalog
(that would fork the contract, ADR-0001). Upstreaming a cleanup PR to winutil
is worthwhile but our loader must accept the files as they are.

## Consequences

- A single well-tested pre-pass (~40 lines) instead of a lenient parser
  dependency or a forked catalog.
- The sanitizer is part of the loader's public behavior and covered by fixture
  tests containing literal control characters.
