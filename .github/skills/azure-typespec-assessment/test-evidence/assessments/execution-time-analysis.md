# Assessment Execution-Time Details

This file contains the per-dimension timing breakdown for the catalog-first,
documentation-grounded reassessment round. Individual PR reports show only the
end-to-end total.

Compilation, emitter execution, and package installation were not rerun. The
agents reused unchanged compiler evidence and independently reassessed the exact
TypeSpec diffs.

| PR                           | Semantic understanding | REST breaking | Downstream breaking |    Compliance |    Overhead |         Total | Compliance result | Search                   |
| ---------------------------- | ---------------------: | ------------: | ------------------: | ------------: | ----------: | ------------: | ----------------- | ------------------------ |
| [43745](43745/assessment.md) |            ~2m 25s (E) |      ~50s (E) |            ~40s (E) |    3m 28s (M) |    ~29s (D) |    7m 52s (M) | passed            | catalog only             |
| [44454](44454/assessment.md) |            ~5m 20s (E) |   ~1m 20s (E) |          ~1m 5s (E) |    2m 45s (M) |     ~0s (D) |   10m 29s (M) | failed            | catalog only             |
| [45348](45348/assessment.md) |            ~2m 50s (E) |   ~3m 10s (E) |         ~1m 18s (E) |       49s (M) | ~3m 21s (D) |   11m 29s (M) | passed            | catalog only             |
| [45536](45536/assessment.md) |             ~6m 0s (E) |   ~2m 30s (E) |          ~1m 0s (E) |     1m 6s (M) | ~1m 16s (D) |   11m 52s (M) | passed            | catalog only             |
| [42435](42435/assessment.md) |            ~5m 10s (E) |   ~1m 20s (E) |         ~1m 20s (E) |    3m 12s (M) |    ~29s (D) |   11m 31s (M) | passed            | catalog only             |
| [43308](43308/assessment.md) |            ~3m 10s (E) |   ~1m 20s (E) |          ~1m 0s (E) |    7m 51s (M) |    ~29s (D) |   13m 49s (M) | passed            | expanded beyond catalog  |
| [44882](44882/assessment.md) |             ~7m 0s (E) |   ~2m 30s (E) |          ~1m 0s (E) |    3m 38s (M) | ~1m 31s (D) |   15m 39s (M) | failed            | official source fallback |
| [44200](44200/assessment.md) |            ~4m 14s (E) |   ~2m 20s (E) |            ~55s (E) |    4m 15s (M) |    ~24s (D) |    12m 8s (M) | failed            | catalog only             |
| [44742](44742/assessment.md) |            ~6m 30s (E) |   ~2m 30s (E) |          ~1m 0s (E) |    2m 28s (M) |    ~18s (D) |   12m 46s (M) | passed            | catalog only             |
| [42853](42853/assessment.md) |             ~5m 0s (E) |   ~1m 40s (E) |         ~2m 59s (E) |    3m 26s (M) |    ~28s (D) |   13m 33s (M) | failed            | catalog only             |
| [44988](44988/assessment.md) |            ~8m 40s (E) |   ~1m 55s (E) |          ~1m 7s (E) | ~3m 16s (E/M) |  ~2m 4s (D) | ~17m 3s (E/M) | failed            | catalog only             |

**Timing quality:** M = measured directly, E = estimated allocation from a
measured combined phase, E/M = a measured interval plus an estimated correction,
D = derived from total minus dimension durations.

## Summary

- **Completed:** 11 of 11 PRs
- **Compliance:** 6 passed, 5 failed, 0 not-assessed
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

## Optimized local Network benchmark

The retained local Network/#44988 worktree was reassessed after adding
input-keyed emitter caches, persistent sparse checkouts, deterministic
REST/TCGC analysis, cached compliance evidence preparation, and a compact model
draft.

| Run                                | Semantic understanding | REST breaking | Downstream breaking | Compliance |  Overhead |        Total | Compliance result | Search                      |
| ---------------------------------- | ---------------------: | ------------: | ------------------: | ---------: | --------: | -----------: | ----------------- | --------------------------- |
| Network/#44988 optimized local run |            ~1m 51s (E) |      ~25s (E) |            ~14s (E) |   ~42s (E) | 46.3s (M) | 3m 58.5s (M) | failed            | deterministic catalog/cache |

The four model dimensions are estimated allocations of the measured 3m 12.3s
combined model-review phase, using the previous #44988 phase proportions.
Overhead is measured: 29.5s deterministic preparation plus 16.8s report
rendering and report validation.

The model-facing draft fell from 85.8 MB to 1.52 MB by replacing complete
added/removed operation contracts with a method/path/version manifest, retaining
detailed before/after contracts only for modified operations, filtering
non-TypeSpec changed files, and removing repeated source-link objects from the
source index.

## Optimized 11-PR reassessment — 2026-08-24

This new preserved-evidence reassessment round does not replace the historical
timings above. Compilation was not rerun; each report was independently reviewed
against retained source, AutoRest/TCGC artifacts, and cached authoritative
documentation, then rendered and checked with the current report contract.

| PR                                                                          | Semantic understanding | REST breaking | Downstream breaking |    Compliance |     Overhead |         Total | Compliance result | Search       |
| --------------------------------------------------------------------------- | ---------------------: | ------------: | ------------------: | ------------: | -----------: | ------------: | ----------------- | ------------ |
| [42435](42435/assessment.md)                                                |            ~3m 50s (E) |    ~1m 0s (E) |         ~1m 20s (E) |    ~2m 0s (E) | 2m 49.2s (M) | 10m 59.2s (M) | passed            | catalog only |
| [42853](42853/assessment.md)                                                |          ~4m 52.6s (E) | ~2m 26.3s (E) |        ~4m 3.9s (E) | ~4m 52.6s (E) |     5.5s (M) | 16m 20.9s (M) | failed            | catalog only |
| [43308](43308/assessment.md)                                                |            ~5m 21s (E) | ~2m 40.5s (E) |       ~4m 27.5s (E) |   ~3m 34s (E) |   1m 47s (M) |   17m 50s (M) | passed            | catalog only |
| [43745](43745/assessment.md)                                                |             ~5m 0s (E) |   ~1m 40s (E) |          ~2m 0s (E) |    ~2m 0s (E) | 1m 59.6s (M) | 12m 39.6s (M) | passed            | catalog only |
| [44200](44200/assessment.md)                                                |          ~5m 50.2s (E) | ~3m 53.5s (E) |       ~1m 56.7s (E) | ~4m 51.8s (E) | 2m 55.1s (D) | 19m 27.3s (M) | failed            | catalog only |
| [44454](../optimized-reassessment-20260824/assessments/44454/assessment.md) |             ~2m 0s (E) |   ~1m 30s (E) |          ~1m 0s (E) |   ~1m 30s (E) |  5m 4.8s (D) |  11m 4.8s (M) | failed            | catalog only |
| [44742](../optimized-reassessment-20260824/assessments/44742/assessment.md) |            ~7m 30s (E) |   ~8m 20s (E) |          ~1m 0s (E) |   ~2m 30s (E) |    34.3s (D) | 19m 54.3s (M) | passed            | catalog only |
| [44882](44882/assessment.md)                                                |          ~5m 12.8s (E) | ~1m 55.2s (E) |       ~1m 14.1s (E) | ~3m 25.8s (E) | 1m 55.2s (D) | 13m 43.1s (M) | failed            | catalog only |
| [44988](44988/assessment.md)                                                |          ~8m 40.3s (E) | ~5m 25.2s (E) |       ~2m 10.1s (E) | ~5m 25.2s (E) |     3.9s (M) | 21m 44.5s (M) | failed            | catalog only |
| [45348](45348/assessment.md)                                                |            ~1m 15s (E) |      ~40s (E) |            ~20s (E) |      ~30s (E) | 2m 23.5s (D) |   5m 8.5s (M) | passed            | catalog only |
| [45536](45536/assessment.md)                                                |             ~6m 0s (E) |   ~2m 30s (E) |          ~1m 0s (E) |    ~2m 0s (E) | 2m 10.1s (D) | 13m 40.1s (M) | passed            | catalog only |

**Timing quality:** M = measured directly, E = estimated allocation from a
measured combined phase, D = derived from measured total minus estimated
dimension durations.

- **Completed:** 11 of 11 PRs
- **Average / median total:** 14m 46.6s / 13m 43.1s
- **Fastest / slowest:** 5m 8.5s / 21m 44.5s
- **Compliance:** 6 passed, 5 failed
- **Canonical comparison:** 9 materially consistent reports replace their
  canonical JSON/Markdown/HTML. PRs 44454 and 44742 retain both versions because
  their fresh operation contracts or impact scope materially differ.

## Judgment-only artifact-backed reassessment — 2026-08-24

This section appends the final measured run; it does not replace either timing
table above. Deterministic preparation produced bounded inputs, one aggregate
model judgment resolved semantic/REST/downstream/compliance decisions, and the
artifact-backed assembler generated and validated all report formats.

Because the four dimensions share model calls, their inference time is
allocated by each dimension's serialized evidence size and is labeled
estimated. Compliance combines its estimated judgment allocation with measured
document preparation. Overhead contains the remaining deterministic analysis,
assembly, rendering, and validation. Total time is measured wall time. Quality
correction retries are included rather than hidden.

| PR | Semantic understanding | REST breaking | Downstream breaking | Compliance | Overhead | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| [42435](42435/assessment.md) | ~25s (E) | ~27s (E) | ~3s (E) | ~42s (E/M) | 4s (M) | 1m 41s (M) |
| [42853](42853/assessment.md) | ~1m 29s (E) | ~0s (E) | ~5s (E) | ~1m 42s (E/M) | 2s (M) | 3m 18s (M) |
| [43308](43308/assessment.md) | ~1m 8s (E) | ~35s (E) | ~5s (E) | ~1m 9s (E/M) | 0s (M) | 2m 57s (M) |
| [43745](43745/assessment.md) | ~27s (E) | ~0s (E) | ~10s (E) | ~1m 55s (E/M) | 0s (M) | 2m 32s (M) |
| [44200](44200/assessment.md) | ~7m 42s (E) | ~0s (E) | ~13s (E) | ~3m 16s (E/M) | 0s (M) | 11m 12s (M) |
| [44454](44454/assessment.md) | ~27s (E) | ~0s (E) | ~0s (E) | ~3m 12s (E/M) | 0s (M) | 3m 39s (M) |
| [44742](44742/assessment.md) | ~2m 22s (E) | ~23s (E) | ~0s (E) | ~1m 0s (E/M) | 2s (M) | 3m 47s (M) |
| [44882](44882/assessment.md) | ~53s (E) | ~0s (E) | ~0s (E) | ~3m 28s (E/M) | 0s (M) | 4m 22s (M) |
| [44988](44988/assessment.md) | ~8m 55s (E) | ~1m 21s (E) | ~2s (E) | ~31s (E/M) | 27s (M) | 11m 17s (M) |
| [45348](45348/assessment.md) | ~13s (E) | ~0s (E) | ~0s (E) | ~1m 53s (E/M) | 8s (M) | 2m 14s (M) |
| [45536](45536/assessment.md) | ~2m 41s (E) | ~0s (E) | ~0s (E) | ~10m 5s (E/M) | 1s (M) | 12m 48s (M) |

**Timing quality:** M = measured, E = deterministic allocation of a measured
shared judgment, E/M = estimated judgment allocation plus measured document
preparation.

- **Completed:** 11 of 11 PRs
- **Average / median total:** 5m 26s / 3m 39s
- **Fastest / slowest:** 1m 41s / 12m 48s
- **Input budget:** every non-Network input is below 100 KB; Network is
  140,521 bytes, below its 250 KB limit
- **Compliance:** 5 passed, 5 failed, 1 not-assessed
- **Under-five-minute result including quality retries:** 8 of 11
- **Performance target:** not fully met. PRs 44200 and 44988 exceed five
  minutes because compliance quality corrections are included; PR 45536 had a
  12m 47s initial judgment outlier. Their deterministic preparation and report
  assembly remain low-cost.
- **42435 timing caveat:** its 96-second judgment duration is estimated from
  adjacent sequential agent runtime because launch instrumentation was added
  immediately afterward.
