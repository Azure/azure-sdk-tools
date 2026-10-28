import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { afterEach, describe, it, mock } from "node:test";

import { getPassAtKVerdict, logPassAtKOnFailure } from "./typespec-author-pass-at-k.ts";

const roots: string[] = [];

function trialRecords(passes: boolean[], evalFilePath = "001.eval.yaml", stimulus = "case") {
  return passes.map((passed, trialIndex) => ({
    type: "trial-result", runId: "run", evalFilePath, stimulus, variant: "default",
    trialIndex, totalTrials: passes.length, status: "success",
    gradeResult: { passed },
  }));
}

function summary(passed = false) {
  return {
    type: "run-summary", runId: "run", hadExecutionErrors: false,
    evals: [{ evalFilePath: "001.eval.yaml", variant: "default", passed, stimuliRun: 1 }],
  };
}

function newResults(lines: object[]) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "typespec-pass-at-k-test-"));
  roots.push(root);
  const run = path.join(root, "run");
  fs.mkdirSync(run);
  const jsonl = path.join(run, "results.jsonl");
  const junit = path.join(run, "eval-results.junit.xml");
  fs.writeFileSync(jsonl, lines.map(line => JSON.stringify(line)).join("\n"));
  fs.writeFileSync(junit, '<testsuite failures="2"><properties><property name="threshold" value="0.97"/></properties></testsuite>');
  return { root, run, jsonl, junit };
}

function captureLog(action: () => void): string[] {
  const log = mock.method(console, "log", () => {});
  try {
    action();
    return log.mock.calls.map(call => call.arguments.join(" "));
  } finally {
    log.mock.restore();
  }
}

afterEach(() => {
  for (const root of roots.splice(0)) fs.rmSync(root, { recursive: true, force: true });
});

describe("TypeSpec pass@k failure diagnostics", () => {
  it("preserves one-of-three success when the standard verdict fails", () => {
    const { root, jsonl } = newResults([...trialRecords([false, true, false]), summary()]);
    assert.equal(getPassAtKVerdict(jsonl, 3).passed, true);
    const logs = captureLog(() => logPassAtKOnFailure(root, 3));
    assert.match(logs[0], /standard Vally verdict failed; official results are unchanged/);
    assert.match(logs[1], /PASS.*001.eval.yaml.*case - pass@3 \(1\/3 trials passed\)/);
    assert.equal(logs.length, 2);
  });

  it("logs failure when none of ten trials passes", () => {
    const { root, jsonl } = newResults([...trialRecords(Array(10).fill(false)), summary()]);
    assert.equal(getPassAtKVerdict(jsonl, 10).passed, false);
    assert.match(captureLog(() => logPassAtKOnFailure(root, 10)).join("\n"), /FAIL.*pass@10 \(0\/10 trials passed\)/);
  });

  it("is silent when the common scored verdict passes, regardless of CLI failures", () => {
    const record = {
      ...summary(),
      hadExecutionErrors: true,
      evals: [{
        ...summary().evals[0], passed: false, scoringApplied: true,
        overallScore: 0.9, threshold: 0.8,
      }],
    };
    const { root } = newResults([...trialRecords([false, true, true]), record]);
    assert.deepEqual(captureLog(() => logPassAtKOnFailure(root, 3)), []);
  });

  it("is silent when the common unscored verdict passes", () => {
    const { root } = newResults([...trialRecords([true, true, true]), summary(true)]);
    assert.deepEqual(captureLog(() => logPassAtKOnFailure(root, 3)), []);
  });

  it("is silent for a single repeated trial, including multi-turn failures or missing results", () => {
    const { root } = newResults([
      { ...trialRecords([false])[0], trajectory: { turns: [{}, {}, {}] } }, summary(),
    ]);
    assert.deepEqual(captureLog(() => logPassAtKOnFailure(root, 1)), []);
    assert.deepEqual(captureLog(() => logPassAtKOnFailure(path.join(root, "missing"), 1)), []);
  });

  it("does not rewrite results or JUnit, or create report artifacts", () => {
    const { root, run, jsonl, junit } = newResults([...trialRecords([false, true, false]), summary()]);
    const raw = fs.readFileSync(jsonl);
    const xml = fs.readFileSync(junit);
    captureLog(() => logPassAtKOnFailure(root, 3));
    assert.deepEqual(fs.readFileSync(jsonl), raw);
    assert.deepEqual(fs.readFileSync(junit), xml);
    assert.deepEqual(fs.readdirSync(root), ["run"]);
    assert.deepEqual(fs.readdirSync(run).sort(), ["eval-results.junit.xml", "results.jsonl"]);
  });

  it("keeps identically named stimuli in different evals and variants independent", () => {
    const second = trialRecords([false, false, false], "002.eval.yaml");
    const third = trialRecords([false, false, false]).map(trial => ({ ...trial, variant: "other" }));
    const record = summary();
    record.evals.push(
      { ...record.evals[0], evalFilePath: "002.eval.yaml" },
      { ...record.evals[0], variant: "other" }
    );
    const { root } = newResults([...trialRecords([true, false, false]), ...second, ...third, record]);
    const logs = captureLog(() => logPassAtKOnFailure(root, 3));
    assert.equal(logs.filter(line => line.startsWith("PASS")).length, 1);
    assert.equal(logs.filter(line => line.startsWith("FAIL")).length, 2);
  });

  it("does not count an execution error as a successful trial", () => {
    const trials = trialRecords([true, false, false]);
    trials[0].status = "error";
    const { root } = newResults([...trials, summary()]);
    assert.match(captureLog(() => logPassAtKOnFailure(root, 3)).join("\n"), /FAIL.*0\/3 trials passed/);
  });

  const invalidCases: [string, () => object[]][] = [
    ["missing summary", () => trialRecords([false, true, false])],
    ["missing trials", () => [...trialRecords([true, false, false]).slice(0, 2), summary()]],
    ["duplicated trial index", () => {
      const trials = trialRecords([true, false, false]);
      trials[2].trialIndex = 0;
      return [...trials, summary()];
    }],
    ["wrong expected count", () => [...trialRecords([true, false]), summary()]],
    ["missing totalTrials", () => [
      ...trialRecords([true, false, false]).map(({ totalTrials: _, ...trial }) => trial), summary(),
    ]],
    ["mixed run identities", () => [
      ...trialRecords([true, false, false]), { ...summary(), runId: "other" },
    ]],
    ["trial after summary", () => [...trialRecords([true, false, false]), summary(), trialRecords([true])[0]]],
    ["missing whole stimulus", () => [
      ...trialRecords([true, false, false]),
      { ...summary(), evals: [{ ...summary().evals[0], stimuliRun: 2 }] },
    ]],
    ["missing whole eval", () => [
      ...trialRecords([true, false, false]),
      { ...summary(), evals: [...summary().evals, { ...summary().evals[0], evalFilePath: "002.eval.yaml" }] },
    ]],
  ];
  for (const [name, records] of invalidCases) {
    it(`warns without claiming pass@k success for ${name}`, () => {
      const { root } = newResults(records());
      const logs = captureLog(() => logPassAtKOnFailure(root, 3));
      assert.equal(logs.length, 1);
      assert.match(logs[0], /type=warning.*pass@k is unavailable/);
      assert.doesNotMatch(logs[0], /PASS/);
    });
  }

  for (const corruption of ["malformed JSONL", "missing JUnit", "missing results"]) {
    it(`warns without gating for ${corruption}`, () => {
      const { root, jsonl, junit } = newResults([...trialRecords([true, false, false]), summary()]);
      if (corruption === "malformed JSONL") fs.appendFileSync(jsonl, "\n{bad JSON");
      if (corruption === "missing JUnit") fs.unlinkSync(junit);
      if (corruption === "missing results") fs.unlinkSync(jsonl);
      const logs = captureLog(() => logPassAtKOnFailure(root, 3));
      assert.equal(logs.length, 1);
      assert.match(logs[0], /type=warning.*unavailable/);
    });
  }

  it("never falls back to stale success when the latest invocation is interrupted", () => {
    const { root, jsonl } = newResults([...trialRecords([true, true, true]), summary(true)]);
    fs.utimesSync(jsonl, new Date(1000), new Date(1000));
    const latest = path.join(root, "latest");
    fs.mkdirSync(latest);
    fs.writeFileSync(path.join(latest, "results.jsonl"), JSON.stringify(trialRecords([true, false, false])[0]));
    assert.match(captureLog(() => logPassAtKOnFailure(root, 3)).join("\n"), /type=warning.*unavailable/);
  });

  it("rejects invalid configured runs explicitly", () => {
    for (const runs of [0, -1, 1.5, NaN]) {
      assert.throws(() => logPassAtKOnFailure("unused", runs), /positive safe integer/);
    }
  });

  it("keeps the reporting CLI non-gating for failed and incomplete evaluations", () => {
    const { root, jsonl } = newResults([...trialRecords([true, false, false]), summary()]);
    const script = fileURLToPath(new URL("./typespec-author-pass-at-k.ts", import.meta.url));
    const run = () => spawnSync(process.execPath, [
      "--experimental-strip-types", script, "--report", "--results-root", root, "--runs", "3",
    ], { encoding: "utf8" });
    const failed = run();
    assert.equal(failed.status, 0, failed.stderr);
    assert.match(failed.stdout, /PASS.*pass@3 \(1\/3 trials passed\)/);
    fs.appendFileSync(jsonl, "\n{interrupted");
    const incomplete = run();
    assert.equal(incomplete.status, 0, incomplete.stderr);
    assert.match(incomplete.stdout, /type=warning.*unavailable/);
    assert.doesNotMatch(incomplete.stdout, /PASS/);
  });
});
