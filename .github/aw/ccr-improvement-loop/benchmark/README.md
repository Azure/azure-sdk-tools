# Frozen replay benchmark (anti-Goodhart)

A committed, labeled held-out PR set used to gate every applied instruction-rule
delta. The workflow's closed-loop validation step blocks any candidate that
**regresses** this benchmark (loses useful catches or adds net noise) versus
baseline, even if it improves the proposal's own held-out miss rate.

Changing this set is a **deliberate, reviewed commit** — never edit it to make a
failing rule pass. Each entry is a PR whose CCR-relevant outcome is human-labeled.

`example-benchmark.json` is a tiny illustrative fixture (schema only); a real
benchmark is curated per target repo before closed-loop validation is relied upon.
