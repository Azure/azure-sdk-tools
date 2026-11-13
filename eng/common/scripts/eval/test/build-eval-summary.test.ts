// node:test unit tests for build-eval-summary.ts (port of Build-EvalSummary.Tests.ps1).
// Run from eng/common/scripts/eval:  npm test

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { after, before, beforeEach, describe, it } from "node:test";

import { getEvalSummary, formatEvalSummaryMarkdown, formatDeliverySummary } from "../build-eval-summary.ts";

function reportRows(report) {
  return [...report.matchAll(/<tr(?:\s[^>]*)?>([\s\S]*?)<\/tr>/g)].map(row =>
    [...row[1].matchAll(/<td\b[^>]*>([\s\S]*?)<\/td>/g)].map(cell => cell[1]));
}

function hasRow(report, ...values) {
  return reportRows(report).some(row => values.every((value, index) => row[index] === value));
}

function logUrls(report) {
  return [...report.matchAll(/href="(https:\/\/dev\.azure\.com\/[^\"]+)"/g)]
    .map(match => new URL(match[1].replaceAll("&amp;", "&")));
}

describe("approved evaluation-results UX", () => {
  const identity = { organization: "azure-sdk", project: "internal", build: "12345" };
  const sourceLog = { jobId: "11111111-1111-4111-8111-111111111111", taskId: "22222222-2222-4222-8222-222222222222",
    line: 250, graderLines: { "tool-calls requires edit|apply_patch": 259, "required skill": 261 } };
  const shards = { area_typespec: { shardName: "area_typespec", total: 1, failed: 1, skipped: 0, durationS: 1,
    failures: ["rename-client-property (0/1 runs passed)"],
    stimuli: new Map([["rename-client-property", { trials: 1, passed: 0, skipped: 0, threshold: 0.8 }]]) } };
  const failureDetails = [
    { shard: "area_typespec", scenario: "rename-client-property", model: "model", sourceLog,
      grade: { name: "tool-calls requires edit|apply_patch", passed: false,
        evidence: "The following required tools were not called: /edit|apply_patch/" } },
    { shard: "area_typespec", scenario: "rename-client-property", model: "model", sourceLog,
      grade: { name: "required skill", passed: false, evidence: "The required skill was not called" } },
  ];

  it("replaces the legacy collapsed list with the approved failure table and exact check links", () => {
    const markdown = formatEvalSummaryMarkdown(shards, [], { ...identity, failureDetails });
    assert.match(markdown, /## .* Evaluation results/);
    assert.ok(markdown.includes(">Scenario</th>") && markdown.includes(">Failed check</th>") && markdown.includes(">Trials passed</th>"));
    assert.doesNotMatch(markdown, /Vally eval results|<details open>|- \*\*area_typespec\*\*/);
    assert.ok(markdown.includes(">Shard</th>") && markdown.includes(">Time (s)</th>"));
    assert.match(markdown, /<table border="1" cellpadding="10" cellspacing="0"/);
    const urls = logUrls(markdown);
    for (const line of ["259", "261"]) {
      assert.ok(urls.some(url => url.searchParams.get("l") === line && url.searchParams.get("j") === sourceLog.jobId &&
        url.searchParams.get("t") === sourceLog.taskId && url.searchParams.get("buildId") === identity.build));
    }
    assert.ok(markdown.includes("edit|apply_patch"));
  });

  it("keeps verified task links without guessing an exact grader line", () => {
    const markdown = formatEvalSummaryMarkdown(shards, [], { ...identity,
      failureDetails: [{ ...failureDetails[0], sourceLog: { jobId: sourceLog.jobId, taskId: sourceLog.taskId } }] });
    const urls = logUrls(markdown);
    assert.ok(urls.length > 0);
    assert.ok(urls.every(url => url.searchParams.get("t") === sourceLog.taskId && !url.searchParams.has("l")));
  });

  it("uses only scenario, failed check and trials without a duplicate evidence action", () => {
    const markdown = formatEvalSummaryMarkdown(shards, [], { ...identity, failureDetails });
    assert.ok(markdown.includes(">Scenario</th>") && markdown.includes(">Failed check</th>") && markdown.includes(">Trials passed</th>"));
    assert.doesNotMatch(markdown, />Evidence<\/th>|>Task log<\/a>/);
    assert.ok(markdown.includes("l=259") && markdown.includes("l=261"));
  });

  it("adds recorded token and AI-credit totals without inventing missing costs", () => {
    const usageByShard = { area_typespec: { trials: 2, tokenRecords: 2, tokens: 33065, creditRecords: 2, credits: 4.35449 } };
    const markdown = formatEvalSummaryMarkdown(shards, [], { ...identity, failureDetails, usageByShard });
    assert.ok(markdown.includes(">Tokens</th>") && markdown.includes(">AI credits (AIU)</th>"));
    assert.ok(markdown.includes("33,065") && markdown.includes("4.35"));
    const missing = formatEvalSummaryMarkdown(shards);
    assert.match(missing, /Not recorded/);
    const partial = formatEvalSummaryMarkdown(shards, [], { usageByShard: {
      area_typespec: { ...usageByShard.area_typespec, tokenRecords: 1, creditRecords: 1 },
    } });
    assert.ok(partial.includes("1/2 trials"));
  });
});

describe("result-delivery summary", () => {
  const summary = { schemaVersion: 1, complete: true, totals: { scenarios: 5, failed: 1, skipped: 0 } };
  const delivery = { storageConfigured: true, organization: "azure-sdk", project: "Internal (SDK) & + quality",
    definition: "8255", build: "12345", attempt: "2", dashboardRefreshUrl: "https://synthetic.azurewebsites.net/api/refresh" };

  it("pending delivery relies on the native title and one concise status sentence", () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, archiveStatus: "stored", importStatus: "failed" });
    assert.doesNotMatch(markdown, /^#+ Result Delivery|Uploaded; dashboard import pending|No evaluation rerun/m);
    assert.match(markdown, /Results were uploaded successfully\. Dashboard import is not yet confirmed\./);
    assert.match(markdown, /\[View this build in dashboard\]/);
    assert.match(markdown, /\[Retained artifacts\]/);
  });

  it("artifact-only UX keeps raw downloads without skipped publication stages", () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, skipReason: "disabled" });
    assert.match(markdown, /\*\*Artifacts only\*\*/);
    assert.ok(hasRow(markdown, "Result data", "Complete"));
    assert.ok(hasRow(markdown, "ADO artifacts", "Retained"));
    assert.match(markdown, /\[Retained artifacts\]\(/);
    assert.doesNotMatch(markdown, />Storage upload<\/td>|>Dashboard import<\/td>|\[ADO tests\]|\[ADO logs\]|View this build in dashboard/);
  });

  it("publishing UX separates import readiness and retains downloads", () => {
    for (const [importStatus, expected] of [["confirmed", "Available in dashboard"], ["failed", "Results were uploaded successfully. Dashboard import is not yet confirmed."],
      ["not_configured", "Results were uploaded successfully. Dashboard import is not yet confirmed."], ["not_attempted", "Results were uploaded successfully. Dashboard import is not yet confirmed."]]) {
      const markdown = formatDeliverySummary(summary, { ...delivery, archiveStatus: "stored", importStatus });
      assert.ok(markdown.includes(expected));
      assert.match(markdown, /\[View this build in dashboard\]\(/);
      assert.ok(markdown.indexOf("[View this build in dashboard]") < markdown.indexOf("<table"));
      assert.match(markdown, /\[Retained artifacts\]\(/);
      assert.doesNotMatch(markdown, /\[ADO tests\]|\[ADO logs\]|CI succeeded/);
    }
  });

  it("build URL contract accepts the configured base independently of notification setup", () => {
    const options = { ...delivery, dashboardBaseUrl: "https://viewer.azurewebsites.net", dashboardRefreshUrl: "",
      archiveStatus: "stored", importStatus: "not_configured" };
    const pending = formatDeliverySummary(summary, options);
    const ready = formatDeliverySummary(summary, { ...options, importStatus: "confirmed" });
    const href = value => value.match(/\[View this build in dashboard\]\(([^)]+)\)/)?.[1];
    assert.ok(href(pending));
    assert.equal(href(pending), href(ready));
    const url = new URL(href(pending));
    assert.equal(url.origin, "https://viewer.azurewebsites.net");
    assert.equal(url.pathname, "/build");
    assert.deepEqual(Object.fromEntries(url.searchParams), { organization: "azure-sdk", project: delivery.project,
      definition: "8255", build: "12345", attempt: "2" });
  });

  it("confirmed upload is required before advertising a dashboard build link", () => {
    for (const archiveStatus of ["not_started", "failed", undefined]) {
      const markdown = formatDeliverySummary(summary, { ...delivery, archiveStatus, importStatus: "confirmed" });
      assert.doesNotMatch(markdown, /View this build in dashboard|View dashboard results\]|\*\*Available in dashboard\*\*/);
    }
  });

  it("keeps failed grades, completeness, stored data, and unconfirmed import separate", () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, archiveStatus: "stored", importStatus: "failed" });
    assert.match(markdown, /Evaluations: completed; 1 scenario\(s\) failed/);
    assert.ok(hasRow(markdown, "Result data", "Complete"));
    assert.ok(hasRow(markdown, "Storage upload", "Complete"));
    assert.ok(hasRow(markdown, "Dashboard import", "Unconfirmed"));
    assert.match(markdown, /The refresh request failed/);
    assert.doesNotMatch(markdown, /No evaluation rerun is needed/);
    const href = markdown.match(/\[View this build in dashboard\]\(([^)]+)\)/)?.[1];
    const url = new URL(href);
    assert.equal(url.pathname, "/build");
    assert.deepEqual(Object.fromEntries(url.searchParams), { organization: "azure-sdk", project: delivery.project,
      definition: "8255", build: "12345", attempt: "2" });
    assert.match(markdown, /\[Retained artifacts\]\(/);
    assert.doesNotMatch(markdown, /\[ADO tests\]|\[ADO logs\]/);
  });

  it("reports a complete passing run and confirmed import without claiming CI success", () => {
    const markdown = formatDeliverySummary({ ...summary, totals: { ...summary.totals, failed: 0 } },
      { ...delivery, archiveStatus: "stored", importStatus: "confirmed" });
    assert.match(markdown, /all executed scenarios passed/);
    assert.ok(hasRow(markdown, "Dashboard import", "Confirmed"));
    assert.match(markdown, /\*\*Available in dashboard\*\*/);
    assert.doesNotMatch(markdown, /CI succeeded|No evaluation rerun/);
  });

  for (const [options, expected, notAttempted = true, artifactsOnly = false] of [
    [{ skipReason: "disabled" }, "publication was not requested", true, true],
    [{ skipReason: "feature_branch" }, "feature-branch run", true, true],
    [{ skipReason: "ineligible_run" }, "not eligible for publication", true, true],
    [{ storageConfigured: false }, "storage configuration is missing"],
    [{ archiveStatus: "failed" }, "Storage upload failed"],
    [{}, "The publisher did not report a confirmed upload", false],
  ]) it(`explains publication state: ${expected}`, () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, ...options });
    assert.ok(markdown.includes(expected));
    if (artifactsOnly) assert.doesNotMatch(markdown, />Dashboard import<\/td>/);
    else assert.ok(hasRow(markdown, "Dashboard import", notAttempted ? "Not attempted" : "Not confirmed"));
    assert.ok(!hasRow(markdown, "Storage upload", "Complete") && !hasRow(markdown, "Dashboard import", "Confirmed"));
    assert.doesNotMatch(markdown, /View this build in dashboard/);
  });

  it("does not claim an import was never attempted when publisher status output is missing", () => {
    const markdown = formatDeliverySummary(summary, { ...delivery, archiveStatus: "not_started", importStatus: "not_attempted" });
    assert.ok(hasRow(markdown, "Storage upload", "Not confirmed"));
    assert.ok(hasRow(markdown, "Dashboard import", "Not confirmed"));
    assert.ok(!hasRow(markdown, "Dashboard import", "Not attempted"));
    const confirmed = formatDeliverySummary(summary, { ...delivery, archiveStatus: "not_started", importStatus: "confirmed" });
    assert.ok(hasRow(confirmed, "Storage upload", "Not confirmed"));
    assert.ok(hasRow(confirmed, "Dashboard import", "Not confirmed"));
    assert.doesNotMatch(confirmed, /Available in dashboard|View this build in dashboard/);
  });

  it("never turns incomplete or unavailable results into a complete passing run", () => {
    for (const value of [null, { ...summary, complete: false }, { ...summary, totals: {} }]) {
      const markdown = formatDeliverySummary(value, delivery);
      assert.match(markdown, /incomplete|not verified/);
      assert.match(markdown, /result data is incomplete or unverified/);
      assert.doesNotMatch(markdown, /all executed scenarios passed|View this build in dashboard/);
    }
  });

  it("does not invent a dashboard import when refresh is unconfigured or its outcome is missing", () => {
    for (const importStatus of ["not_configured", "not_attempted", undefined]) {
      assert.ok(hasRow(formatDeliverySummary(summary, { ...delivery, archiveStatus: "stored", importStatus }), "Dashboard import", "Unconfirmed"));
    }
  });

  it("never exposes credentials or invents an exact link from invalid dashboard configuration", () => {
    for (const dashboardRefreshUrl of ["$(EvalDashboardRefreshUrl)", "http://unsafe.test/api/refresh",
      "https://user:SECRET@synthetic.azurewebsites.net/api/refresh", "https://synthetic.azurewebsites.net/api/refresh?sig=SECRET"]) {
      const markdown = formatDeliverySummary(summary, { ...delivery, dashboardRefreshUrl, archiveStatus: "stored" });
      assert.doesNotMatch(markdown, /View this build in dashboard\]|SECRET|unsafe\.test/);
    }
  });

  it("rejects unsafe explicit base URLs without falling back or exposing configuration", () => {
    for (const dashboardBaseUrl of ["http://unsafe.test", "https://unsafe.test", "https://user:SECRET@viewer.azurewebsites.net",
      "https://viewer.azurewebsites.net?sig=SECRET", "https://viewer.azurewebsites.net/#SECRET",
      "https://viewer.azurewebsites.net/nested/", "https://viewer.azurewebsites.net:8443"]) {
      const markdown = formatDeliverySummary(summary, { ...delivery, dashboardBaseUrl, archiveStatus: "stored" });
      assert.doesNotMatch(markdown, /View this build in dashboard\]|SECRET|unsafe\.test/);
      assert.match(markdown, /\[Retained artifacts\]\(/);
    }
    const markdown = formatDeliverySummary(summary, { ...delivery, dashboardBaseUrl: "$(EvalDashboardBaseUrl)", archiveStatus: "stored" });
    assert.match(markdown, /\[View this build in dashboard\]\(https:\/\/synthetic\.azurewebsites\.net\/build\?/);
  });

  it("runs the final-report CLI without results, a token, or publisher dependencies, even when summary generation failed", () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), "eval-delivery-"));
    try {
      const output = path.join(root, "eval-delivery.md");
      const source = path.join(root, "eval-summary.json");
      fs.writeFileSync(source, JSON.stringify(summary));
      const env = { ...process.env, TF_BUILD: "True", EVAL_ARCHIVE_STATUS: "stored", EVAL_IMPORT_STATUS: "failed",
        EVAL_STORAGE_CONFIGURED: "True", EVAL_DASHBOARD_BASE_URL: "https://viewer.azurewebsites.net", EVAL_DASHBOARD_REFRESH_URL: "",
        EVAL_PUBLICATION_SKIP_REASON: "", SYSTEM_COLLECTIONURI: "https://dev.azure.com/azure-sdk/",
        SYSTEM_TEAMPROJECT: delivery.project, SYSTEM_DEFINITIONID: delivery.definition, BUILD_BUILDID: delivery.build,
        SYSTEM_JOBATTEMPT: delivery.attempt, SYSTEM_ACCESSTOKEN: "SYNTHETIC_TOKEN_MUST_NOT_APPEAR" };
      delete env.NODE_TEST_CONTEXT;
      const run = () => spawnSync(process.execPath, [path.resolve(import.meta.dirname, "../build-eval-summary.ts"),
        "--delivery", "--output-path", output], { env, encoding: "utf8", timeout: 10_000 });
      const result = run();
      assert.ifError(result.error);
      assert.equal(result.status, 0, result.stderr);
      assert.match(result.stdout, /task.addattachment type=Distributedtask.Core.Summary;name=Result delivery;/);
      assert.ok(hasRow(fs.readFileSync(output, "utf8"), "Storage upload", "Complete"));
      assert.doesNotMatch(result.stdout, /SYNTHETIC_TOKEN/);
      const ado = result.stdout.match(/\[Retained artifacts\]\(([^)]+)\)/)?.[1];
      assert.equal(decodeURIComponent(new URL(ado).pathname.split("/")[2]), delivery.project);
      const dashboard = new URL(result.stdout.match(/\[View this build in dashboard\]\(([^)]+)\)/)[1]);
      assert.equal(dashboard.origin, "https://viewer.azurewebsites.net");
      assert.equal(dashboard.searchParams.get("build"), delivery.build);
      assert.equal(dashboard.searchParams.get("attempt"), delivery.attempt);
      fs.unlinkSync(source);
      const missing = run();
      assert.equal(missing.status, 0, missing.stderr);
      assert.ok(hasRow(fs.readFileSync(output, "utf8"), "Result data", "Not verified"));
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
    assert.match(markdown, /## .* Evaluation results — FAILED/);
    assert.ok(reportRows(markdown).some(row => row[0] === "area_github" && row[3] === "2" && row[4] === "0" && row[5] === "0"));
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
    assert.match(markdown, /## .* Evaluation results — PASSED/);
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
    assert.match(markdown, /## .* Evaluation results — NO RESULTS/);
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
