# Controlled vocabulary (pinned)

This file is the **closed** list of theme/category labels and the severity
definitions you apply when judging (`references/judge.prompt.md`) and clustering
(`references/theme.prompt.md`). The allowed labels are the back-ticked tokens
under **## Themes / categories** and **## Severity** below; record its `sha256`
in `run.vocabularyHash` for reproducibility.

Changing the vocabulary is a **deliberate, reviewed commit**: a label that does
not appear here is invalid — do not assign it. The only escape hatch is `other`,
which **requires an explanation** and is **never eligible for promotion** to a
rule — so novel issue classes surface in the data without polluting trend lines
or the promotion gate.

## Themes / categories

- `error-handling` — missing/incorrect error handling, swallowed exceptions,
  unchecked results, missing ret/guard on failure paths.
- `concurrency` — races, unsynchronized shared state, deadlocks, missing
  cancellation, unsafe async ordering.
- `input-validation` — unvalidated/untrusted input, missing bounds/null checks,
  argument validation gaps.
- `security` — injection, secret handling, authz/authn, unsafe deserialization,
  TLS/crypto misuse.
- `resource-management` — leaks, undisposed handles, unclosed streams, missing
  `using`/`finally`, lifetime bugs.
- `api-design` — public surface shape, naming/compat of new API, parameter
  ordering, return-type ergonomics.
- `backward-compatibility` — breaking changes to a shipped contract, removed or
  renamed public members, behavioral breaks.
- `type-safety` — unsound casts, `any`/`unknown` leakage, nullability holes,
  generic misuse.
- `performance` — needless allocation, N+1 / quadratic patterns, blocking calls
  on hot paths.
- `testing` — missing/insufficient test coverage for the changed behavior,
  untested edge cases.
- `logging-observability` — missing/excessive logging, lost diagnostics, PII in
  logs, no telemetry on failure.
- `documentation` — missing/incorrect doc comments, changelog, or public-API
  docs for the change.
- `style-naming` — naming, formatting, idiom, and readability preferences (maps
  to `nit` severity by default).
- `configuration` — config/env/build/dependency wiring issues.
- `other` — a substantive issue that does not fit any label above. **Requires an
  explanation; never promoted.**

## Severity

- `critical` — a bug, security flaw, data-loss, crash, or concurrency hazard;
  shipping it would harm correctness or safety.
- `substantive` — a real correctness or maintainability problem worth fixing,
  but not itself critical.
- `nit` — style, naming, formatting, or minor preference with no correctness
  impact.
