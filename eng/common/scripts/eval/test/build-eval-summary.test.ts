// node:test unit tests for build-eval-summary.ts (port of Build-EvalSummary.Tests.ps1).
// Run from eng/common/scripts/eval:  npm test

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { after, before, beforeEach, describe, it } from "node:test";

import { getEvalSummary, formatEvalSummaryMarkdown, formatDeliverySummary } from "../build-eval-summary.ts";

describe("result-delivery summary", () => {
  const summary = { schemaVersion: 1, complete: true, totals: { scenarios: 5, failed: 1, skipped: 0 } };
  const delivery = { storageConfigured: true, organization: "azure-sdk", project: "Internal (SDK) & + quality",
    definition: "8255", build: "12345", attempt: "2", dashboardRefreshUrl: "https://synthetic.azurewebsites.net/api/refresh" };

  it("keeps failed grades, completeness, stored data, and unconfirmed import separate", () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, archiveStatus: "stored", importStatus: "failed" });
    assert.match(markdown, /Evaluations: completed; 1 scenario\(s\) failed/);
    assert.match(markdown, /Result data: complete/);
    assert.match(markdown, /Storage: stored/);
    assert.match(markdown, /Dashboard import: not confirmed; the refresh request failed/);
    assert.match(markdown, /No evaluation rerun is needed/);
    const href = markdown.match(/\[View dashboard results\]\(([^)]+)\)/)?.[1];
    const url = new URL(href);
    assert.equal(url.pathname, "/build");
    assert.deepEqual(Object.fromEntries(url.searchParams), { organization: "azure-sdk", project: delivery.project,
      definition: "8255", build: "12345", attempt: "2" });
    for (const label of ["ADO tests", "ADO logs", "Retained artifacts"]) assert.ok(markdown.includes(`[${label}]`));
  });

  it("reports a complete passing run and confirmed import without claiming CI success", () => {
    const markdown = formatDeliverySummary({ ...summary, totals: { ...summary.totals, failed: 0 } },
      { ...delivery, archiveStatus: "stored", importStatus: "confirmed" });
    assert.match(markdown, /all executed scenarios passed/);
    assert.match(markdown, /Dashboard import: confirmed by the refresh response/);
    assert.doesNotMatch(markdown, /CI succeeded|No evaluation rerun/);
  });

  for (const [options, expected, notAttempted = true] of [
    [{ skipReason: "disabled" }, "publication was not requested"],
    [{ skipReason: "feature_branch" }, "feature-branch run"],
    [{ skipReason: "ineligible_run" }, "not eligible for publication"],
    [{ storageConfigured: false }, "storage configuration is missing"],
    [{ archiveStatus: "failed" }, "upload failed"],
    [{}, "Storage: not confirmed", false],
  ]) it(`explains publication state: ${expected}`, () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, ...options });
    assert.ok(markdown.includes(expected));
    assert.match(markdown, notAttempted ? /Dashboard import: not attempted/ : /Dashboard import: not confirmed/);
    assert.doesNotMatch(markdown, /Storage: stored|Dashboard import: confirmed/);
  });

  it("does not claim an import was never attempted when publisher status output is missing", () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, archiveStatus: "not_started", importStatus: "not_attempted" });
    assert.match(markdown, /Storage: not confirmed/);
    assert.match(markdown, /Dashboard import: not confirmed/);
    assert.doesNotMatch(markdown, /Dashboard import: not attempted/);
    const confirmed = formatDeliverySummary(summary, { ...delivery, archiveStatus: "not_started", importStatus: "confirmed" });
    assert.match(confirmed, /Storage: not confirmed/);
    assert.match(confirmed, /Dashboard import: confirmed by the refresh response/);
  });

  it("never turns incomplete or unavailable results into a complete passing run", () => {
    for (const value of [null, { ...summary, complete: false }, { ...summary, totals: {} }]) {
      const markdown = formatDeliverySummary(value, delivery);
      assert.match(markdown, /incomplete|not verified/);
      assert.match(markdown, /result data is incomplete or unverified/);
      assert.doesNotMatch(markdown, /all executed scenarios passed|View dashboard results/);
    }
  });

  it("does not invent a dashboard import when refresh is unconfigured or its outcome is missing", () => {
    for (const importStatus of ["not_configured", "not_attempted", undefined]) {
      assert.match(formatDeliverySummary(summary, { ...delivery, archiveStatus: "stored", importStatus }),
        /Dashboard import: not confirmed/);
    }
  });

  it("never exposes credentials or invents an exact link from invalid dashboard configuration", () => {
    for (const dashboardRefreshUrl of ["$(EvalDashboardRefreshUrl)", "http://unsafe.test/api/refresh",
      "https://user:SECRET@synthetic.azurewebsites.net/api/refresh", "https://synthetic.azurewebsites.net/api/refresh?sig=SECRET"]) {
      const markdown = formatDeliverySummary(summary, { ...delivery, dashboardRefreshUrl, archiveStatus: "stored" });
      assert.doesNotMatch(markdown, /View dashboard results\]|SECRET|unsafe\.test/);
    }
  });

  it("runs the final-report CLI without results, a token, or publisher dependencies, even when summary generation failed", () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "eval-delivery-"));
    try {
      const output = path.join(root, "eval-delivery.md");
      const source = path.join(root, "eval-summary.json");
      fs.writeFileSync(source, JSON.stringify(summary));
      const env = { ...process.env, TF_BUILD: "True", EVAL_ARCHIVE_STATUS: "stored", EVAL_IMPORT_STATUS: "failed",
        EVAL_STORAGE_CONFIGURED: "True", EVAL_DASHBOARD_REFRESH_URL: delivery.dashboardRefreshUrl,
        EVAL_PUBLICATION_SKIP_REASON: "", SYSTEM_COLLECTIONURI: "https://dev.azure.com/azure-sdk/",
        SYSTEM_TEAMPROJECT: delivery.project, SYSTEM_DEFINITIONID: delivery.definition, BUILD_BUILDID: delivery.build,
        SYSTEM_JOBATTEMPT: delivery.attempt, SYSTEM_ACCESSTOKEN: "SYNTHETIC_TOKEN_MUST_NOT_APPEAR" };
      delete env.NODE_TEST_CONTEXT;
      const run = () => spawnSync(process.execPath, [path.resolve(import.meta.dirname, "../build-eval-summary.ts"),
        "--delivery", "--output-path", output], { env, encoding: "utf8", timeout: 10_000 });
      const result = run();
      assert.ifError(result.error);
      assert.equal(result.status, 0, result.stderr);
      assert.match(result.stdout, /task.uploadsummary/);
      assert.match(fs.readFileSync(output, "utf8"), /Storage: stored/);
      assert.doesNotMatch(result.stdout, /SYNTHETIC_TOKEN/);
      const ado = result.stdout.match(/\[ADO tests\]\(([^)]+)\)/)?.[1];
      assert.equal(decodeURIComponent(new URL(ado).pathname.split("/")[2]), delivery.project);
      fs.unlinkSync(source);
      const missing = run();
      assert.equal(missing.status, 0, missing.stderr);
      assert.match(fs.readFileSync(output, "utf8"), /Result data: not verified/);
    } finally { fs.rmSync(root, { recursive: true, force: true }); }
  });
});

// Convenience: build the summary and also write the markdown, returning both.
function summarize(resultsRoot, outFile) {
  const shards = getEvalSummary(resultsRoot);
  const markdown = formatEvalSummaryMarkdown(shards);
  fs.writeFileSync(outFile, markdown, "utf8");
  return { shards, markdown };
}

describe("build-eval-summary", () => {
  let root;
  let outFile;

  before(() => {
    root = fs.mkdtempSync(path.join(os.tmpdir(), "vally-summary-"));

    const newJunit = (shard, xml) => {
      const dir = path.join(root, `eval-result-${shard}`);
      fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(path.join(dir, "junit.xml"), xml);
    };

    // area_github: 2 pass.
    newJunit(
      "area_github",
      `<testsuites>
  <testsuite name="github">
    <testcase name="lists issues" time="1.2" />
    <testcase name="creates pr" time="0.8" />
  </testsuite>
</testsuites>`
    );

    // area_typespec: 1 pass, 1 fail, 1 skip.
    newJunit(
      "area_typespec",
      `<testsuites>
  <testsuite name="typespec">
    <testcase name="adds arm resource" time="2.0" />
    <testcase name="renames client" time="1.5"><failure message="assertion failed">expected tool call</failure></testcase>
    <testcase name="generation step 2" time="0.0"><skipped /></testcase>
  </testsuite>
</testsuites>`
    );

    // area_multitrial: two stimuli, each run 5 times, gated at 0.8.
    // 'flaky pass' 4/5 (>= 0.8 passes); 'flaky fail' 2/5 (< 0.8 fails).
    newJunit(
      "area_multitrial",
      `<testsuites>
  <testsuite name="multitrial">
    <properties><property name="threshold" value="0.8" /></properties>
    <testcase name="flaky pass (trial 1)" time="1.0" />
    <testcase name="flaky pass (trial 2)" time="1.0" />
    <testcase name="flaky pass (trial 3)" time="1.0" />
    <testcase name="flaky pass (trial 4)" time="1.0"><failure message="x">nope</failure></testcase>
    <testcase name="flaky pass (trial 5)" time="1.0" />
    <testcase name="flaky fail (trial 1)" time="1.0" />
    <testcase name="flaky fail (trial 2)" time="1.0" />
    <testcase name="flaky fail (trial 3)" time="1.0"><failure message="x">nope</failure></testcase>
    <testcase name="flaky fail (trial 4)" time="1.0"><failure message="x">nope</failure></testcase>
    <testcase name="flaky fail (trial 5)" time="1.0"><failure message="x">nope</failure></testcase>
  </testsuite>
</testsuites>`
    );

    // area_multifile: one shard with TWO JUnit files; total must be 4, not double-counted.
    const multiDir = path.join(root, "eval-result-area_multifile");
    fs.mkdirSync(multiDir, { recursive: true });
    fs.writeFileSync(
      path.join(multiDir, "part1.xml"),
      `<testsuites>
  <testsuite name="multifile-a">
    <testcase name="alpha" time="1.0" />
    <testcase name="beta" time="1.0" />
  </testsuite>
</testsuites>`
    );
    fs.writeFileSync(
      path.join(multiDir, "part2.xml"),
      `<testsuites>
  <testsuite name="multifile-b">
    <testcase name="gamma" time="1.0" />
    <testcase name="delta" time="1.0"><failure message="x">nope</failure></testcase>
  </testsuite>
</testsuites>`
    );
  });

  after(() => fs.rmSync(root, { recursive: true, force: true }));

  beforeEach(() => {
    outFile = path.join(root, `summary-${Math.random().toString(36).slice(2)}.md`);
  });

  it("aggregates pass/fail/skip per shard", () => {
    const { shards } = summarize(root, outFile);
    assert.equal(shards.area_github.total, 2);
    assert.equal(shards.area_github.failed, 0);
    assert.equal(shards.area_typespec.total, 3);
    assert.equal(shards.area_typespec.failed, 1);
    assert.equal(shards.area_typespec.skipped, 1);
  });

  it("captures the failing scenario name", () => {
    const { shards } = summarize(root, outFile);
    assert.ok(shards.area_typespec.failures.includes("renames client (0/1 runs passed)"));
  });

  it("collapses per-trial testcases to one stimulus and applies the threshold", () => {
    const { shards } = summarize(root, outFile);
    assert.equal(shards.area_multitrial.total, 2);
    assert.equal(shards.area_multitrial.failed, 1);
    assert.ok(shards.area_multitrial.failures.includes("flaky fail (2/5 runs passed)"));
    assert.ok(!shards.area_multitrial.failures.includes("flaky pass (4/5 runs passed)"));
  });

  it("does not double-count totals when a shard has multiple JUnit files", () => {
    const { shards } = summarize(root, outFile);
    assert.equal(shards.area_multifile.total, 4);
    assert.equal(shards.area_multifile.failed, 1);
    const deltaCount = shards.area_multifile.failures.filter((f) => f.startsWith("delta ")).length;
    assert.equal(deltaCount, 1);
  });

  it("writes a Markdown file with an overall FAILED header when any shard is red", () => {
    const { markdown } = summarize(root, outFile);
    assert.match(markdown, /## .* Vally eval results — FAILED/);
    assert.match(markdown, /\| area_github \| .* \| 2 \| 0 \| 0 \|/);
    assert.match(markdown, /failing scenarios/i);
    assert.match(markdown, /renames client/);
  });

  it("reports PASSED when no shard has failures", () => {
    const passRoot = path.join(root, "pass-only");
    const dir = path.join(passRoot, "eval-result-area_github");
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(
      path.join(dir, "junit.xml"),
      `<testsuites><testsuite name="github"><testcase name="ok" time="0.1" /></testsuite></testsuites>`
    );
    const { markdown } = summarize(passRoot, outFile);
    assert.match(markdown, /## .* Vally eval results — PASSED/);
  });

  it("reports NO RESULTS (not PASSED) when an XML has zero testcases", () => {
    const emptyRoot = path.join(root, "empty-results");
    const dir = path.join(emptyRoot, "eval-result-area_empty");
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(
      path.join(dir, "junit.xml"),
      `<testsuites><testsuite name="empty"></testsuite></testsuites>`
    );
    const { markdown } = summarize(emptyRoot, outFile);
    assert.match(markdown, /## .* Vally eval results — NO RESULTS/);
    assert.doesNotMatch(markdown, /results — PASSED/);
    assert.match(markdown, /No eval testcases were found/);
  });

  it("falls back to a meaningful shard name when not under eval-result-*", () => {
    const fbRoot = path.join(root, "fallback");
    const dir = path.join(fbRoot, "_unit5", "2026-06-17T23-53-02-457Z");
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(
      path.join(dir, "eval-results.junit.xml"),
      `<testsuites><testsuite name="x"><testcase name="ok" time="0.1" /></testsuite></testsuites>`
    );
    const { shards } = summarize(fbRoot, outFile);
    assert.ok("_unit5" in shards);
    assert.ok(!("unknown" in shards));
  });

  it("supersedes an earlier job attempt with the highest attempt on rerun", () => {
    const rerunRoot = path.join(root, "rerun");
    // Attempt 1 failed; attempt 2 (the rerun) passed. Only attempt 2 should count, and the
    // shard name must drop the -<attempt> suffix so it reads as a single shard.
    const a1 = path.join(rerunRoot, "eval-result-area_flaky-1");
    const a2 = path.join(rerunRoot, "eval-result-area_flaky-2");
    fs.mkdirSync(a1, { recursive: true });
    fs.mkdirSync(a2, { recursive: true });
    fs.writeFileSync(
      path.join(a1, "junit.xml"),
      `<testsuites><testsuite name="flaky"><testcase name="does thing" time="1.0"><failure message="x">nope</failure></testcase></testsuite></testsuites>`
    );
    fs.writeFileSync(
      path.join(a2, "junit.xml"),
      `<testsuites><testsuite name="flaky"><testcase name="does thing" time="1.0" /></testsuite></testsuites>`
    );
    const { shards } = summarize(rerunRoot, outFile);
    assert.ok("area_flaky" in shards);
    assert.ok(!("area_flaky-1" in shards));
    assert.ok(!("area_flaky-2" in shards));
    assert.equal(shards.area_flaky.total, 1);
    assert.equal(shards.area_flaky.failed, 0);
  });
});
