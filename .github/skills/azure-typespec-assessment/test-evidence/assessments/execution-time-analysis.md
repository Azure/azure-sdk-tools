# Assessment Execution-Time Details

This file contains the per-dimension timing breakdown for the catalog-first,
documentation-grounded reassessment round. Individual PR reports show only the
end-to-end total.

TypeSpec Validation, compilation, emitter execution, and package installation
were not rerun. The agents reused unchanged compiler evidence and independently
reassessed the exact TypeSpec diffs.

| PR                           | Semantic understanding | REST breaking | Downstream breaking |    Compliance |    Overhead |         Total | Compliance result | Search                   |
| ---------------------------- | ---------------------: | ------------: | ------------------: | ------------: | ----------: | ------------: | ----------------- | ------------------------ |
| [43745](43745/assessment.md) |            ~2m 25s (E) |      ~50s (E) |            ~40s (E) |    3m 28s (M) |    ~29s (D) |    7m 52s (M) | not-assessed      | catalog only             |
| [44454](44454/assessment.md) |            ~5m 20s (E) |   ~1m 20s (E) |          ~1m 5s (E) |    2m 45s (M) |     ~0s (D) |   10m 29s (M) | not-assessed      | catalog only             |
| [45348](45348/assessment.md) |            ~2m 50s (E) |   ~3m 10s (E) |         ~1m 18s (E) |       49s (M) | ~3m 21s (D) |   11m 29s (M) | passed            | catalog only             |
| [45536](45536/assessment.md) |             ~6m 0s (E) |   ~2m 30s (E) |          ~1m 0s (E) |     1m 6s (M) | ~1m 16s (D) |   11m 52s (M) | not-assessed      | catalog only             |
| [42435](42435/assessment.md) |            ~5m 10s (E) |   ~1m 20s (E) |         ~1m 20s (E) |    3m 12s (M) |    ~29s (D) |   11m 31s (M) | passed            | catalog only             |
| [43308](43308/assessment.md) |            ~3m 10s (E) |   ~1m 20s (E) |          ~1m 0s (E) |    7m 51s (M) |    ~29s (D) |   13m 49s (M) | not-assessed      | expanded beyond catalog  |
| [44882](44882/assessment.md) |             ~7m 0s (E) |   ~2m 30s (E) |          ~1m 0s (E) |    3m 38s (M) | ~1m 31s (D) |   15m 39s (M) | not-assessed      | official source fallback |
| [44200](44200/assessment.md) |            ~4m 14s (E) |   ~2m 20s (E) |            ~55s (E) |    4m 15s (M) |    ~24s (D) |    12m 8s (M) | not-assessed      | catalog only             |
| [44742](44742/assessment.md) |            ~6m 30s (E) |   ~2m 30s (E) |          ~1m 0s (E) |    2m 28s (M) |    ~18s (D) |   12m 46s (M) | passed            | catalog only             |
| [42853](42853/assessment.md) |             ~5m 0s (E) |   ~1m 40s (E) |         ~2m 59s (E) |    3m 26s (M) |    ~28s (D) |   13m 33s (M) | not-assessed      | catalog only             |
| [44988](44988/assessment.md) |            ~8m 40s (E) |   ~1m 55s (E) |          ~1m 7s (E) | ~3m 16s (E/M) |  ~2m 4s (D) | ~17m 3s (E/M) | failed            | catalog only             |

**Timing quality:** M = measured directly, E = estimated allocation from a
measured combined phase, E/M = a measured interval plus an estimated correction,
D = derived from total minus dimension durations.

## Summary

- **Completed:** 11 of 11 PRs
- **Compliance:** 3 passed, 1 failed, 7 not-assessed
- **Document routing:** 9 catalog-only; #43308 expanded because the required
  Azure.Core data-types page was absent and has now been added; #44882 used the
  catalog page's official source because rendered content omitted decisive text
- **Average total:** 12m 34s
- **Median total:** 12m 8s
- **Fastest / slowest:** 7m 52s / ~17m 3s
- **Average compliance:** 3m 18s
- **Median compliance:** 3m 16s
- **Average dimension allocation:** semantic 5m 7s, REST 1m 57s, downstream
  1m 13s, compliance 3m 18s, overhead 59s
