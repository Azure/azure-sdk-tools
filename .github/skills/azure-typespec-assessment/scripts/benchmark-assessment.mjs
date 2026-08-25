#!/usr/bin/env node

import { mkdirSync, rmSync, writeFileSync } from "node:fs";
import { isAbsolute, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { runAssessmentAnalysis } from "./run-assessment-analysis.mjs";

function parseArgs(argv) {
  const args = { repo: ".", output: ".typespec-assessment-benchmark" };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (!["--repo", "--base", "--output"].includes(value)) {
      throw new Error(`Unknown argument: ${value}`);
    }
    const next = argv[index + 1];
    if (!next) throw new Error(`${value} requires a value`);
    args[value.slice(2)] = next;
    index += 1;
  }
  return args;
}

function runSummary(draft) {
  return {
    totalMs: draft.assessmentDuration.totalMs,
    preparationMs: draft.assessmentDuration.preparationMs,
    deterministicAnalysisMs: draft.assessmentDuration.deterministicAnalysisMs,
    documentationEvidenceMs: draft.assessmentDuration.documentationEvidenceMs,
    artifactCacheHits: draft.artifactCache?.hits ?? 0,
    artifactCacheMisses: draft.artifactCache?.misses ?? 0,
    checkoutCacheReused: draft.checkoutCache?.reused ?? {
      base: false,
      head: false,
    },
  };
}

export function summarizeBenchmark(coldDraft, warmDraft) {
  const cold = runSummary(coldDraft);
  const warm = runSummary(warmDraft);
  return {
    schemaVersion: 1,
    cold,
    warm,
    improvement: {
      totalMs: cold.totalMs - warm.totalMs,
      percent:
        cold.totalMs === 0
          ? 0
          : Math.round(
              ((cold.totalMs - warm.totalMs) / cold.totalMs) * 10_000,
            ) / 100,
    },
    note: "Both runs include required compilation and documentation search. The warm run reuses only input-matched emitter and document cache entries.",
  };
}

export async function benchmarkAssessment(options) {
  const repoRoot = resolve(options.repo);
  const benchmarkRoot = isAbsolute(options.output)
    ? options.output
    : resolve(repoRoot, options.output);
  const environmentRoot = `${repoRoot}-environment`;
  const artifactCache = join(benchmarkRoot, "cache", "artifacts");
  const documentCache = join(benchmarkRoot, "cache", "documents");
  const checkoutCache = join(environmentRoot, "checkouts");
  rmSync(benchmarkRoot, { recursive: true, force: true });
  mkdirSync(benchmarkRoot, { recursive: true });

  async function run(name) {
    return runAssessmentAnalysis({
      prepare: {
        repo: repoRoot,
        base: options.base,
        output: join(benchmarkRoot, name),
        artifactCache,
        checkoutCache,
        excludePaths: [benchmarkRoot],
        skipCompile: false,
      },
      documentCache,
    });
  }

  const cold = await run("cold");
  const warm = await run("warm");
  const summary = summarizeBenchmark(cold, warm);
  writeFileSync(
    join(benchmarkRoot, "benchmark.json"),
    `${JSON.stringify(summary, null, 2)}\n`,
  );
  return summary;
}

async function main() {
  const summary = await benchmarkAssessment(parseArgs(process.argv.slice(2)));
  process.stdout.write(
    `Cold ${(summary.cold.totalMs / 1000).toFixed(1)}s; warm ${(summary.warm.totalMs / 1000).toFixed(1)}s; improvement ${summary.improvement.percent.toFixed(2)}%.\n`,
  );
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  main().catch((error) => {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  });
}
