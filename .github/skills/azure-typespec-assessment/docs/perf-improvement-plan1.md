# Plan: Run AutoRest and TCGC Compilation in Parallel

## Problem and expected gain

The assessment currently runs AutoRest and TCGC sequentially for each selected
comparison role. Both emitter processes read the same immutable sparse
worktree, but write to separate output and log directories.

For PR 43718, the four recorded compiler invocations took:

| Comparison role | AutoRest | TCGC | Sequential total | Pairwise lower bound |
| --------------- | -------: | ---: | ---------------: | -------------------: |
| Baseline        |    2m 24s | 1m 12s |           3m 36s |                2m 24s |
| Target          |    4m 44s | 2m 38s |           7m 22s |                4m 44s |
| Total           |    7m 8s | 3m 50s |          10m 58s |                 7m 8s |

Pairwise parallelism has a theoretical saving of approximately 3m 50s for this
run, before process startup and resource-contention overhead. The practical
target is to reduce compiler wall time by at least 30 percent without changing
compiler inputs, generated artifacts, assessment findings, or report quality.

This plan depends on completion of `perf-improvement-plan.md`. It does not
change PR resolution, sparse-worktree preparation, dependency installation,
Agent judgment, Azure Guidelines retrieval, assembly, or rendering.

## Coordination with concurrent performance work

Azure Guidelines batching is being implemented separately. That change analyzes
the selected official guideline documents once across all Semantic intents,
instead of repeating document analysis for each intent.

This compiler plan must not modify the guideline query, ranking, fetching,
shared evidence, per-intent applicability, or judgment workflow. Keep the two
optimizations independently measurable:

- guideline batching reports guideline-analysis wall time and preserves one
  complete per-intent decision;
- compiler parallelism reports compiler wall time and preserves identical
  AutoRest and TCGC artifacts;
- end-to-end benchmarks may report the combined saving, but must not attribute
  the guideline improvement to compiler concurrency or vice versa.

Complete both equivalence checks before enabling both optimizations by default.
The combined result must retain the same Semantic intents, guideline decisions,
REST and downstream findings, Documentation Completeness results, blockers, and
HTML sections as the sequential reference assessment.

## Design constraints

- Run at most two TypeSpec compiler processes concurrently.
- Parallelize AutoRest and TCGC only within one comparison role.
- Keep baseline and target comparison roles sequential.
- Preserve the exact compiler executable, arguments, selected API version,
  working directory, environment, warning behavior, output directories, and
  logs.
- Keep AutoRest and TCGC outputs physically separate.
- Wait for both emitters to finish even when one fails, and retain both results.
- Do not silently retry in sequential mode or convert failures into successful
  results.
- Preserve deterministic manifest and model-input ordering regardless of which
  emitter finishes first.
- Do not combine both emitters into one TypeSpec invocation in this change.
- Do not reduce or bypass Agent judgment, evidence collection, validation, or
  HTML report content.

## Proposed execution model

For each project and comparison role:

```text
                         runProjectCompilers
                                  |
                    +-------------+-------------+
                    |                           |
                    v                           v
              AutoRest process              TCGC process
            isolated output/log          isolated output/log
                    |                           |
                    +-------------+-------------+
                                  |
                        wait for both results
                                  |
                    describe and hash artifacts
                                  |
                     return stable result object
```

The project loop remains:

```text
baseline AutoRest + TCGC in parallel
                    |
                    v
 target AutoRest + TCGC in parallel
```

This caps concurrency at two and avoids running four memory-intensive compiler
processes against the same toolchain simultaneously.

## Implementation plan

1. **Make emitter execution asynchronous**
   - Replace `spawnSync` in `compiler-runner.mjs` with an asynchronous child
     process wrapper based on `spawn`.
   - Collect stdout and stderr independently and write the existing log format
     after process completion.
   - Preserve the existing 64 MiB combined-output safety limit. If the limit is
     exceeded, terminate only that child process and return an explicit failed
     compiler result.
   - Resolve launch errors, nonzero exit codes, and signals into explicit
     emitter results; do not use broad catches or success-shaped fallbacks.
   - Preserve Windows `.cmd` execution behavior.

2. **Run the emitter pair concurrently**
   - Make `runProjectCompilers` asynchronous.
   - Start AutoRest and TCGC before awaiting either result.
   - Use settlement behavior that waits for both processes and preserves each
     result independently.
   - Construct the returned object in the existing fixed property order:
     `autorest`, then `tcgc`.
   - Describe and hash generated artifacts only after both processes have
     completed.

3. **Propagate async execution through preparation**
   - Await `runProjectCompilers` from `prepare-assessment.mjs`.
   - Keep the existing baseline-then-target and project ordering.
   - Do not parallelize projects or comparison roles in this phase.
   - Preserve existing blocker creation and partial compiler evidence.

4. **Add compiler wall-time telemetry**
   - Retain each emitter's existing `durationMs`.
   - Record a pair-level `wallDurationMs` measured around both concurrent
     processes.
   - Record aggregate preparation timings for:
     - `compilerWallMs`;
     - `compilerProcessMs`, the sum of individual emitter durations;
     - `compilerParallelSavingsMs`, calculated as process time minus wall time.
   - Treat these values as telemetry only; they must not affect assessment
     decisions or status.

5. **Provide a controlled rollout switch**
   - Add `--compiler-concurrency 1|2` to the analysis CLI.
   - Keep `1` as the temporary default while equivalence and real-run
     validation are performed.
   - Use `2` in focused tests and benchmarks.
   - Change the production default to `2` only after all acceptance gates pass.
   - Record the selected concurrency in invocation provenance.

6. **Preserve cancellation and process cleanup**
   - If the coordinator is interrupted, terminate only child processes started
     by the current coordinator.
   - Wait for child exit and close output streams before returning.
   - Do not use process-name-based termination.
   - Remove only incomplete emitter output directories owned by the current
     failed invocation, when existing repository behavior requires cleanup.

## Correctness and quality validation

1. **Emitter unit tests**
   - Add a dedicated `compiler-runner.test.mjs`.
   - Use fake compiler executables that write distinct AutoRest and TCGC
     artifacts after controlled delays.
   - Prove both children overlap when concurrency is `2`.
   - Prove concurrency `1` preserves sequential execution.
   - Prove output and log paths never overlap.
   - Prove one emitter failure does not cancel or erase the other result.
   - Prove launch errors, output-limit failures, nonzero exits, and signals are
     explicit failures.
   - Prove the returned object and discovered file lists remain deterministic
     when completion order changes.

2. **Preparation integration tests**
   - Verify baseline completes before target starts.
   - Verify no more than two compiler processes run concurrently.
   - Verify compiler blockers and successful sibling artifacts flow into the
     preparation manifest exactly as before.
   - Verify telemetry reports positive overlap and never reports negative
     savings.

3. **Sequential-versus-parallel equivalence**
   - Run the same fixture once with concurrency `1` and once with concurrency
     `2`.
   - Compare AutoRest and TCGC artifact paths and content hashes.
   - Compare source evidence, semantic inputs, REST candidates, downstream
     candidates, documentation input, compliance requests, and model input
     after excluding timing and invocation-concurrency fields.
   - Assemble both runs from the same judgment and require equivalent
     assessment findings, dimension statuses, blockers, provenance, and report
     sections after excluding timing-only presentation.

4. **Existing regression suites**
   - Run the focused compiler, preparation, source-index, semantic, REST,
     downstream, assembly, validation, and renderer tests.
   - Run the deterministic end-to-end assessment tests.
   - Run the assessment skill evals and report-quality graders.
   - Run formatting and `vally lint azure-typespec-assessment`.

## Performance validation

Use at least one small fixture, one representative fixture, and PR 43718.
Measure sequential and parallel modes on the same machine with the same warm
toolchain.

Record:

- each emitter duration;
- pair wall duration;
- total compiler wall duration;
- total deterministic analysis duration;
- process exit status;
- artifact hashes;
- peak concurrent process count;
- available memory before and after each pair.

Acceptance targets:

- PR 43718 compiler wall time is at most 8 minutes, compared with the recorded
  10m 58s sequential baseline;
- pairwise compiler wall time improves by at least 30 percent on the
  representative case;
- neither emitter's individual duration regresses by more than 20 percent in
  the representative case;
- deterministic analysis produces identical non-timing evidence and
  assessment results;
- no increase in flaky compiler failures across repeated runs;
- peak concurrency never exceeds two;
- the machine retains sufficient memory and does not page heavily enough to
  erase the wall-time improvement.

If the representative or real-PR benchmark misses the performance target,
retain concurrency `1` as the default and preserve the telemetry for further
diagnosis. Do not accept a faster result when evidence, findings, blockers, or
report quality differ.

## Documentation updates

- Update `docs/design.md` with the pairwise compiler execution model.
- Update `references/workflow.md` with the concurrency option and default.
- Update the skill README with resource expectations and troubleshooting.
- Add sequential and parallel measurements to
  `docs/performance-findings-2026-09-15.md`.
- Document that summed process duration can exceed wall duration under
  parallelism and must not be interpreted as elapsed time.

## Out of scope

- Running baseline and target concurrently.
- Running multiple projects concurrently.
- Combining AutoRest and TCGC into one `tsp compile` command.
- Changing API-version selection.
- Changing compiler or emitter versions.
- Reusing stale compiler output.
- Reducing assessment dimensions, evidence, Agent judgment, or report content.
- Persistent dependency caching.
- Azure Guidelines batching, caching, ranking, retrieval, or judgment changes.
