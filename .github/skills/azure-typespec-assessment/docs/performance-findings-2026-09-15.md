# Performance Findings: 12-PR Sequential Assessment

## Scope and method

This report records a fresh, strictly sequential assessment of 12
`Azure/azure-rest-api-specs` pull requests on September 17, 2026 using Node.js
v26.7.0 and the current `azure-typespec-assessment` workflow.

Each case ran the PR-aware coordinator, consumed one bounded
`model-input.json`, ranked the 31-document guidance catalog once, fetched up to
four official documents, ran guarded finalization, and opened the generated
HTML report before the next PR started. Eleven reports validated and opened.
PR 43745 remained blocked by the one-correction finalization guard and
therefore has no validated report.

Times come from each case's `workflow-state.json`:

- **Preparation**: `preparing.startedAt` to `deterministicReadyAt`.
- **Agent**: `observableWaitBeforeFirstAgentArtifactMs`; this includes model
  judgment, tool calls, scheduling, retrieval, and serialization, not active
  model inference alone.
- **Finalization**: guarded finalizer telemetry.
- **Total**: `preparing.startedAt` to `complete.startedAt`.

## Per-PR results

`R/D/I/G` means REST candidates, downstream candidates, inference requests,
and guideline requests.

| PR | Result | Input | R/D/I/G | Preparation | Agent | Correction | Finalization | Total |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 42435 | 3 SDK breaks; Guidelines fail | 33.3 KB | 1/3/0/1 | 1m 56.5s | 2m 17.8s | - | 66ms | 4m 19.4s |
| 42853 | 4 inferred Go SDK breaks | 15.5 KB | 0/0/2/2 | 1m 55.2s | 1m 41.3s | - | 132ms | 3m 41.9s |
| 43308 | 21 SDK breaks; Guidelines fail | 121.4 KB | 0/16/2/3 | 1m 45.4s | 2m 53.1s | 39.4s | 294ms | 5m 32.8s |
| 43745 | **Blocked** after 2 expected SDK breaks | 29.8 KB | 0/2/0/1 | 1m 16.2s | 1m 33.7s* | Guard exhausted | - | 2m 35.3s* |
| 44200 | 1 inferred JavaScript SDK break | 42.0 KB | 0/1/4/2 | 1m 34.2s | 1m 10.9s | ~33.1s | 94ms | 3m 18.3s |
| 44454 | No findings | 11.2 KB | 0/0/2/2 | 1m 20.1s | 2m 10.5s | - | 58ms | 3m 35.1s |
| 44742 | 22 REST, 18 SDK, 5 Guidelines findings | 290.8 KB | 22/19/0/5 | 1m 09.0s | 2m 05.7s | - | 143ms | 3m 21.6s |
| 44882 | No findings; documentation passed | 10.6 KB | 0/0/0/1 | 1m 30.7s | 1m 59.9s | - | 77ms | 3m 35.2s |
| 44988 | 7 SDK, 2 Guidelines, 4 documentation findings | 191.4 KB | 0/10/0/10 | 9m 29.5s | 2m 33.9s | - | 643ms | 12m 09.0s |
| 45162 | No findings; 1 candidate rejected | 38.8 KB | 0/1/2/2 | 1m 05.4s | 2m 10.4s | - | 89ms | 3m 23.3s |
| 45348 | Informational 109-operation version change | 3.5 KB | 0/0/0/0 | 1m 30.4s | 1m 09.5s | - | 139ms | 2m 44.8s |
| 45536 | No findings; Java names preserved | 7.5 KB | 0/0/1/1 | 1m 11.0s | 2m 19.0s | - | 64ms | 3m 36.4s |

\* PR 43745 did not reach `complete`; its partial elapsed and first-artifact
telemetry are not additive and are excluded from aggregate statistics.

## Aggregate performance

| Metric | Validated runs | Excluding PR 44988 preparation outlier |
| --- | ---: | ---: |
| Reports | 11 | 10 |
| Mean total | 4m 28.9s | 3m 42.9s |
| Median total | 3m 35.2s | 3m 35.2s |
| P90 total | 5m 32.8s | 4m 19.4s |
| Minimum total | 2m 44.8s | 2m 44.8s |
| Maximum total | 12m 09.0s | 5m 32.8s |
| Mean preparation | 2m 13.4s | 1m 29.8s |
| Median preparation | 1m 30.7s | 1m 30.5s |
| Mean Agent interval | 2m 02.9s | 1m 59.8s |
| Median Agent interval | 2m 10.4s | 2m 10.2s |
| Median finalization | 94ms | 91.5ms |

Ten of 11 validated reports met the five-minute target. Across validated runs,
preparation consumed 49.6% of summed per-run wall time, the Agent interval
45.7%, and finalization 0.06%. Excluding PR 44988, preparation fell to 40.3%
and the Agent interval became the dominant cost at 53.8%.

The sequential batch ran from 11:13:03Z to 12:14:37Z: **1h 01m 34s** including
report serving, opening, worker startup, and handoffs. Validated per-run totals
sum to 49m 17.8s; PR 43745 contributed another 2m 35.3s of incomplete work.

## Failures and corrections

- **PR 43308:** the first guarded finalization rejected a catalog ranking that
  did not select the first four retrievable documents. Re-ranking and retrying
  added approximately 39.4 seconds.
- **PR 44200:** the first finalization rejected `no-candidates`, which is not a
  valid inference decision; changing it to `no-impact` succeeded. The residual
  correction and workflow-transition interval was approximately 33.1 seconds.
- **PR 43745:** the initial judgment used the wrong wrapper schema. The allowed
  correction then failed because equal-score catalog entries were not ordered
  by `catalogOrder`. The workflow correctly refused to produce a success-shaped
  report after the correction budget was exhausted.

## Findings

1. **Normal warm performance is now below five minutes.** The ten-run
   steady-state mean was 3m 42.9s and P90 was 4m 19.4s.
2. **Preparation remains variable.** Ten cases prepared in 1m 05s-1m 57s, but
   PR 44988 took 9m 29.5s and accounted for 78.1% of that report's total.
3. **Agent latency has a large fixed component.** Inputs from 7.5-290.8 KB
   generally finished the Agent interval in 2m 00s-2m 19s. Workload size
   affects latency, but tool and orchestration overhead mask linear scaling.
4. **The informational-intent reduction is effective.** PR 45348 reduced a
   109-operation version-wide change to a 3.5 KB bounded input and completed
   fastest at 2m 44.8s.
5. **Finalization is no longer a bottleneck.** Median finalization was 94ms;
   even the largest run completed it in 643ms.
6. **Schema/ranking errors are disproportionately expensive.** Two correction
   turns added tens of seconds, while PR 43745 lost the complete report.

## Recommended optimizations

1. Cache and prewarm dependency/compiler state, and add subphase telemetry to
   explain preparation outliers such as PR 44988.
2. Generate schema-valid Agent artifact skeletons and use enum-constrained
   helpers so invalid decisions such as `no-candidates` cannot be emitted.
3. Sort catalog ranking deterministically in the materializer and validate the
   top-four selection before consuming the single correction turn.
4. Keep API-version-wide publication changes informational when they own no
   independent compatibility finding; PR 45348 validates the input reduction.
5. Target **under 90 seconds preparation**, **under two minutes Agent wall
   time**, and **under four minutes end to end** for warm single-report runs.
