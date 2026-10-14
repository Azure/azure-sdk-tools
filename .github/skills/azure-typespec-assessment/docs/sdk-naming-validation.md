# SDK Naming Validation Evidence

This evidence validates the SDK Naming report dimension without claiming a
corpus-wide quality or performance result.

## Real PR Sample

The skill was run against the pre-fix state of
[Azure/azure-rest-api-specs#43308](https://github.com/Azure/azure-rest-api-specs/pull/43308)
at commit `a8ff521a2c1d8bce0e2471e293bf5eed808bdaf8`.

- The report produced 15 C# ARM naming recommendations.
- Findings were shown in the main JSON and HTML reports as an independent SDK
  Naming dimension.
- Overall compatibility safety remained scoped to REST and downstream SDK
  breaking changes.
- Generated SDK names without supplied generated API evidence were marked
  unverified.
- The later fix commit
  `8050b5792f5a96e5ef17e2fb91f1d7a470302132` supplied compatibility evidence
  for the recommended names.

The locally generated report is review evidence, not a checked-in golden file.

## Measured Timings

The sample's preparation manifest reported `totalMs: 113082` for one cold
end-to-end deterministic preparation:

| Work                                     |  Duration |
| ---------------------------------------- | --------: |
| Baseline dependency setup                | 68,458 ms |
| Target dependency reuse                  |     50 ms |
| Baseline and target AutoRest compilation | 14,782 ms |
| Baseline and target TCGC compilation     | 13,570 ms |

The manifest total includes dependency setup and orchestration overhead; it is
not isolated SDK Naming time. SDK Naming reuses the existing bounded Agent
judgment and does not add another TypeSpec, AutoRest, or TCGC compilation.

One-trial hermetic workflow evals measured these bounded naming-review cases:

| Case                         | Duration |
| ---------------------------- | -------: |
| Supported C# ARM profile     |   39.7 s |
| Missing target evidence      |   23.1 s |
| Unsupported Java ARM profile |   34.3 s |

These timings include model orchestration and validate bounded execution, not
average incremental latency for a complete PR assessment. Corpus-wide report
quality and performance validation remains tracked by
[Azure/azure-sdk-tools#17164](https://github.com/Azure/azure-sdk-tools/issues/17164).
