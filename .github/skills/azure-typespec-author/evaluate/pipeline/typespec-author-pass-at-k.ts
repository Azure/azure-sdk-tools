import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { parseArgs } from "node:util";

import { stageShardResults } from "../../../../../eng/common/scripts/eval/lib/shard-results.ts";
import { getVallyShardVerdict } from "../../../../../eng/common/scripts/eval/lib/verdict.ts";

type PassAtKVerdict = {
  found: boolean;
  passed: boolean;
  lines: string[];
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function evalIdentity(record: Record<string, unknown>): string {
  return JSON.stringify([record.evalFilePath, record.variant, record.model]);
}

// Reads only the canonical, complete snapshot selected by common.
export function getPassAtKVerdict(resultsFile: string, runs: number): PassAtKVerdict {
  const unavailable: PassAtKVerdict = { found: false, passed: false, lines: [] };
  if (!Number.isSafeInteger(runs) || runs < 2) return unavailable;
  const trials = new Map<string, { label: string; evalKey: string; indices: Set<number>; passed: number }>();
  let summary: Record<string, unknown> | undefined;
  let runId: string | undefined;
  try {
    for (const line of fs.readFileSync(resultsFile, "utf8").split(/\r?\n/)) {
      if (!line.trim()) continue;
      const record: unknown = JSON.parse(line);
      if (!isRecord(record)) return unavailable;
      if (typeof record.runId !== "string" || !record.runId || (runId && record.runId !== runId)) return unavailable;
      runId = record.runId;
      if (record.type === "run-summary") {
        if (summary) return unavailable;
        summary = record;
      } else if (record.type === "trial-result") {
        const index = record.trialIndex;
        if (summary || typeof record.evalFilePath !== "string" || typeof record.stimulus !== "string" ||
            record.totalTrials !== runs || typeof index !== "number" || !Number.isSafeInteger(index) ||
            index < 0 || index >= runs || !["success", "error"].includes(String(record.status))) return unavailable;
        const evalKey = evalIdentity(record);
        const key = JSON.stringify([evalKey, record.stimulus]);
        const aggregate = trials.get(key) ?? {
          label: `${record.evalFilePath} [${record.variant ?? "default"}/${record.model ?? "default"}] :: ${record.stimulus}`,
          evalKey, indices: new Set<number>(), passed: 0,
        };
        if (aggregate.indices.has(index)) return unavailable;
        aggregate.indices.add(index);
        if (record.status === "success" && isRecord(record.gradeResult) && record.gradeResult.passed === true) {
          aggregate.passed++;
        }
        trials.set(key, aggregate);
      } else {
        return unavailable;
      }
    }
  } catch {
    return unavailable;
  }
  if (!summary || !Array.isArray(summary.evals) || !summary.evals.length || !trials.size) return unavailable;
  const expected = new Map<string, number>();
  for (const entry of summary.evals) {
    if (!isRecord(entry) || typeof entry.evalFilePath !== "string" ||
        typeof entry.stimuliRun !== "number" || !Number.isSafeInteger(entry.stimuliRun) || entry.stimuliRun < 1) return unavailable;
    const key = evalIdentity(entry);
    if (expected.has(key)) return unavailable;
    expected.set(key, entry.stimuliRun);
  }
  const result: PassAtKVerdict = { found: true, passed: true, lines: [] };
  for (const aggregate of trials.values()) {
    const remaining = expected.get(aggregate.evalKey);
    if (aggregate.indices.size !== runs || remaining === undefined) return unavailable;
    expected.set(aggregate.evalKey, remaining - 1);
    const passed = aggregate.passed > 0;
    result.passed &&= passed;
    // Keep diagnostic labels on one log line, even for malformed display names.
    const label = aggregate.label.replace(/[\x00-\x1f\x7f-\x9f\u2028\u2029]/g, " ");
    result.lines.push(`${passed ? "PASS" : "FAIL"}  ${label} - pass@${runs} (${aggregate.passed}/${runs} trials passed)`);
  }
  return [...expected.values()].every(count => count === 0) ? result : unavailable;
}

export function logPassAtKOnFailure(resultsRoot: string, runs: number) {
  if (!Number.isSafeInteger(runs) || runs < 1) {
    throw new Error("Runs must be a positive safe integer.");
  }
  if (runs === 1) return;

  const unavailable = () => console.log(
    "##vso[task.logissue type=warning]Supplementary pass@k is unavailable: results are incomplete or unverifiable; common verdict and completeness checks remain authoritative."
  );
  let temporary: string | undefined;
  try {
    temporary = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-pass-at-k-"));
    // Reuse common selection and completeness on a disposable snapshot; never
    // pick an older successful invocation when the newest one is interrupted.
    const snapshot = path.join(temporary, "snapshot");
    const staged = stageShardResults({
      resultsRoot, outputDirectory: snapshot, shardName: "diagnostic", attempt: 1,
    });
    if (!staged.complete) return unavailable();
    const standard = getVallyShardVerdict({ resultsDir: snapshot });
    if (!standard.found) return unavailable();
    if (standard.passed) return;
    const verdict = getPassAtKVerdict(path.join(snapshot, "results.jsonl"), runs);
    if (!verdict.found) return unavailable();
    console.log(`Supplementary pass@${runs}: standard Vally verdict failed; official results are unchanged.`);
    for (const line of verdict.lines) console.log(line);
  } catch {
    // Diagnostic failures must not replace the official verdict or expose raw results.
    unavailable();
  } finally {
    if (temporary) {
      try {
        fs.rmSync(temporary, { recursive: true, force: true });
      } catch {
        console.log("##vso[task.logissue type=warning]Could not remove the temporary pass@k diagnostic snapshot.");
      }
    }
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const { values } = parseArgs({ options: {
      report: { type: "boolean" },
      "results-root": { type: "string" }, runs: { type: "string" },
    } });
    if (values.report && values["results-root"] && values.runs) {
      logPassAtKOnFailure(values["results-root"], Number(values.runs));
    } else {
      throw new Error("Use --report --results-root --runs.");
    }
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}
