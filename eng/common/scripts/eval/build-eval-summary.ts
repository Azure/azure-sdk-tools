// Renders a Markdown rollup from the per-shard Vally JUnit results: groups by shard,
// collapses per-trial testcases to one stimulus, applies the threshold, and lists failing
// scenarios. Presentation only — never changes pass/fail.

import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { globFiles } from "./lib/glob.ts";
import { expectedShardsFromMatrix, selectSummaryResults } from "./lib/shard-results.ts";
import { selectedLogLines, sourceLogForTrial } from "./lib/source-log.ts";
import { reportTable, tableText } from "./lib/report-table.ts";

// Maps a JUnit file path back to its shard and job attempt. Result artifacts download into
// folders named `eval-result-<shardName>-<attempt>` (the attempt suffix keeps "Rerun failed
// jobs" from colliding on the artifact name); everything below that is the shard's JUnit.
export function getShardArtifact(filePath) {
  const segments = filePath.split(/[\\/]/).filter(Boolean);
  for (const segment of segments) {
    if (segment.startsWith("eval-result-")) {
      const raw = segment.replace(/^eval-result-/, "");
      // Strip a trailing `-<digits>` job-attempt suffix (absent in older single-attempt runs).
      const match = raw.match(/^(.*)-(\d+)$/);
      if (match) {
        return { shardName: match[1], attempt: Number(match[2]) };
      }
      return { shardName: raw, attempt: 1 };
    }
  }
  // Fallback: nearest ancestor dir that isn't a Vally timestamp folder.
  for (let i = segments.length - 2; i >= 0; i--) {
    if (!/^\d{4}-\d{2}-\d{2}T/.test(segments[i])) {
      return { shardName: segments[i], attempt: 1 };
    }
  }
  return { shardName: "unknown", attempt: 1 };
}

// Back-compat: return just the shard name (attempt suffix stripped).
export function getShardName(filePath) {
  return getShardArtifact(filePath).shardName;
}

// Strips Vally's ' (trial N)' suffix so every trial of a stimulus collapses to one stimulus.
export function getStimulusName(name) {
  if (!name || !name.trim()) {
    return "(unnamed scenario)";
  }
  return name.replace(/\s*\(trial\s+\d+\)\s*$/, "").trim();
}

// Formats a 0..1 ratio as a percentage, dropping a trailing '.0' so whole numbers read as
// '100%' while fractional rates keep one decimal ('93.5%').
export function formatPct(ratio) {
  const value = ratio * 100;
  if (Math.abs(value - Math.round(value)) < 0.05) {
    return `${Math.round(value)}%`;
  }
  return `${value.toFixed(1)}%`;
}

function getAttr(attrs, name) {
  const match = attrs.match(new RegExp(`\\b${name}=["']([^"']*)["']`));
  return match ? match[1] : undefined;
}

// Minimal dependency-free JUnit parse: testsuite (optional threshold property) > testcase
// with optional <failure>/<error>/<skipped> children. `\b` avoids matching `testsuites`.
// NOTE: coupled to Vally's JUnit output shape; if that XML changes, update these regexes
// (or swap in a real XML parser).
function parseSuites(content) {
  const suites = [];
  const suiteRe = /<testsuite\b([^>]*?)(\/>|>([\s\S]*?)<\/testsuite>)/g;
  let suiteMatch;
  while ((suiteMatch = suiteRe.exec(content))) {
    const inner = suiteMatch[2].startsWith("/>") ? "" : suiteMatch[3];

    let threshold;
    const thresholdMatch = inner.match(
      /<property\b[^>]*\bname=["']threshold["'][^>]*\bvalue=["']([^"']+)["']/
    );
    if (thresholdMatch) {
      const parsed = Number(thresholdMatch[1]);
      if (!Number.isNaN(parsed)) {
        threshold = parsed;
      }
    }

    const testcases = [];
    const caseRe = /<testcase\b([^>]*?)(\/>|>([\s\S]*?)<\/testcase>)/g;
    let caseMatch;
    while ((caseMatch = caseRe.exec(inner))) {
      const attrs = caseMatch[1];
      const body = caseMatch[2].startsWith("/>") ? "" : caseMatch[3];
      const timeStr = getAttr(attrs, "time");
      testcases.push({
        name: getAttr(attrs, "name") ?? "",
        time: timeStr ? Number(timeStr) || 0 : 0,
        failure: /<failure\b/.test(body) || /<error\b/.test(body),
        skipped: /<skipped\b/.test(body),
      });
    }

    suites.push({ threshold, testcases });
  }
  return suites;
}

/**
 * Reads every JUnit XML under `resultsRoot`, grouped by shard, and returns an object keyed
 * by shard name with aggregated totals and failing-scenario names.
 *
 * @param {string} resultsRoot Folder containing the downloaded per-shard result artifacts.
 * @returns {Record<string, {shardName: string, total: number, failed: number, skipped: number, durationS: number, failures: string[]}>}
 */
export function getEvalSummary(resultsRoot) {
  const root = path.resolve(resultsRoot);
  const xmlFiles = globFiles(root, "**/*.xml");
  const shards = {};

  // A "Rerun failed jobs" retry publishes a higher-attempt artifact for the same shard.
  // Resolve each file's shard + attempt up front, then only aggregate the highest attempt
  // per shard so a retry supersedes the earlier one instead of double-counting.
  const parsedFiles = xmlFiles.map((xmlFile) => ({ xmlFile, ...getShardArtifact(xmlFile) }));
  const maxAttempt = {};
  for (const { shardName, attempt } of parsedFiles) {
    maxAttempt[shardName] = Math.max(maxAttempt[shardName] ?? 0, attempt);
  }

  for (const { xmlFile, shardName, attempt } of parsedFiles) {
    if (attempt !== maxAttempt[shardName]) {
      continue; // superseded by a later rerun attempt of this shard
    }
    if (!shards[shardName]) {
      shards[shardName] = {
        shardName,
        total: 0,
        failed: 0,
        skipped: 0,
        durationS: 0,
        failures: [],
        // stimulus name -> { trials, passed, skipped, threshold }
        stimuli: new Map(),
      };
    }
    const shard = shards[shardName];

    const content = fs.readFileSync(xmlFile, "utf8");
    // Read each <testsuite>'s threshold and aggregate its trials back up to the stimulus,
    // mirroring exactly what `vally eval` gates on.
    for (const suite of parseSuites(content)) {
      const threshold = suite.threshold ?? 0.8;
      for (const testcase of suite.testcases) {
        const stimulus = getStimulusName(testcase.name);
        if (!shard.stimuli.has(stimulus)) {
          shard.stimuli.set(stimulus, { trials: 0, passed: 0, skipped: 0, threshold });
        }
        const entry = shard.stimuli.get(stimulus);
        entry.threshold = threshold;
        shard.durationS += testcase.time;

        if (testcase.skipped) {
          entry.skipped++;
        } else {
          entry.trials++;
          if (!testcase.failure) {
            entry.passed++;
          }
        }
      }
    }
  }

  // Collapse each stimulus's trials into one pass/fail, once per shard after all XML is read.
  for (const shardName of Object.keys(shards)) {
    const shard = shards[shardName];
    for (const [stimulus, entry] of shard.stimuli) {
      shard.total++;
      if (entry.trials === 0) {
        shard.skipped++;
        continue;
      }
      const passRate = entry.passed / entry.trials;
      // 1e-9 epsilon guards float rounding so 4/5 = 0.8 is not dropped below an 0.8 gate.
      if (passRate + 1e-9 < entry.threshold) {
        shard.failed++;
        shard.failures.push(`${stimulus} (${entry.passed}/${entry.trials} runs passed)`);
      }
    }
  }

  return shards;
}

/**
 * Renders the Markdown summary for the given shard map.
 *
 * @param {Record<string, object>} shards Output of getEvalSummary.
 * @returns {string} Markdown.
 */
function failureLogUrl({ organization, project, build }, sourceLog, gradeName) {
  const guid = /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i;
  if (!/^[a-z0-9][a-z0-9-]{0,99}$/.test(organization ?? "") || typeof project !== "string" ||
    !project || project.length > 200 || /[\x00-\x1f\x7f]/.test(project) || /^\.+$/.test(project) ||
    !/^[1-9]\d{0,14}$/.test(String(build ?? "")) || !guid.test(sourceLog?.jobId ?? "") || !guid.test(sourceLog?.taskId ?? "")) return null;
  const encodedProject = encodeURIComponent(project).replaceAll("(", "%28").replaceAll(")", "%29");
  const url = new URL(`https://dev.azure.com/${organization}/${encodedProject}/_build/results`);
  url.search = new URLSearchParams({ buildId: String(build), view: "logs", j: sourceLog.jobId, t: sourceLog.taskId }).toString();
  const line = sourceLog.graderLines?.[gradeName];
  if (Number.isSafeInteger(line) && line > 0) url.searchParams.set("l", String(line));
  return url.href;
}

async function selectedReportData(resultsRoot, selection) {
  const details = [];
  const usageByShard = {};
  let bytesLeft = 128 * 1024 * 1024;
  let recordsLeft = 100_000;
  for (const selected of selection.shardInput.attempts) {
    const usage = { trials: 0, tokenRecords: 0, tokens: 0, creditRecords: 0, credits: 0 };
    usageByShard[selected.shard] = usage;
    let observedTrials = 0;
    try {
      const directory = path.join(resultsRoot, selected.directory);
      try {
        const markerPath = path.join(directory, "shard.json");
        const markerStat = fs.lstatSync(markerPath);
        if (markerStat.isFile() && markerStat.size < 64 * 1024) {
          const marker = JSON.parse(fs.readFileSync(markerPath, "utf8"));
          if (Number.isSafeInteger(marker.trials) && marker.trials > 0) usage.trials = marker.trials;
        }
      } catch {}
      const file = path.join(directory, "results.jsonl");
      const metadata = fs.lstatSync(file);
      if (!metadata.isFile() || metadata.size < 1 || metadata.size > bytesLeft) continue;
      bytesLeft -= metadata.size;
      const bytes = fs.readFileSync(file);
      if (bytes.length !== metadata.size) continue;
      const locations = await selectedLogLines(directory, selected);
      for (const line of bytes.toString("utf8").split(/\r?\n/)) {
        if (!line.trim() || Buffer.byteLength(line) > 8 * 1024 * 1024) continue;
        if (--recordsLeft < 0) return { failureDetails: details, usageByShard };
        let record;
        try { record = JSON.parse(line); } catch { continue; }
        if (!record || record.experiment || ![undefined, "trial-result"].includes(record.type) ||
          !["success", "error"].includes(record.status)) continue;
        usage.trials = Math.max(usage.trials, ++observedTrials);
        const tokenUsage = record.trajectory?.metrics?.tokenUsage;
        if (Number.isSafeInteger(tokenUsage?.totalTokens) && tokenUsage.totalTokens >= 0) {
          usage.tokenRecords++;
          usage.tokens += tokenUsage.totalTokens;
        }
        const cost = tokenUsage?.cost;
        if (cost?.provider === "github-copilot" && cost.unit === "nano-aiu" && Number.isSafeInteger(cost.amount) && cost.amount >= 0) {
          usage.creditRecords++;
          usage.credits += cost.amount / 1_000_000_000;
        }
        if (typeof record.stimulus !== "string") continue;
        const grades = record.gradeResult?.details ?? record.grade?.details;
        if (!Array.isArray(grades)) continue;
        let sourceLog;
        try { if (selected.sourceLog) sourceLog = sourceLogForTrial(record, selected.sourceLog, locations); } catch {}
        for (const grade of grades) {
          if (grade?.passed === false && typeof grade.name === "string") details.push({ shard: selected.shard,
            scenario: record.stimulus, model: record.model ?? "Unknown model", grade, sourceLog });
        }
      }
    } catch {}
  }
  return { failureDetails: details, usageByShard };
}

function usageCell(usage, credit = false) {
  const records = credit ? usage?.creditRecords : usage?.tokenRecords;
  const value = credit ? usage?.credits : usage?.tokens;
  if (!records || !Number.isFinite(value) || value < 0 || (!credit && !Number.isSafeInteger(value))) return "Not recorded";
  const amount = value.toLocaleString("en-US", credit ? { minimumFractionDigits: 2, maximumFractionDigits: 2 } : {});
  return amount + (records < usage.trials ? ` (${records}/${usage.trials} trials)` : "");
}

export function formatEvalSummaryMarkdown(shards, incompleteShards = [], options = {}) {
  const all = Object.values(shards);
  const shardCount = all.length;

  let totalTests = 0;
  let totalFailed = 0;
  let totalSkipped = 0;
  for (const shard of all) {
    totalTests += shard.total;
    totalFailed += shard.failed;
    totalSkipped += shard.skipped;
  }
  const totalPassed = totalTests - totalFailed - totalSkipped;

  const lines = [];

  // A run that parsed zero testcases is NOT a pass — surface it as a loud NO RESULTS state.
  let overall;
  let overallIcon;
  if (incompleteShards.length) {
    overall = "INCOMPLETE";
    overallIcon = "⚠️";
  } else if (totalTests === 0) {
    overall = "NO RESULTS";
    overallIcon = "⚠️";
  } else if (totalFailed === 0) {
    overall = "PASSED";
    overallIcon = "✅";
  } else {
    overall = "FAILED";
    overallIcon = "❌";
  }

  // Pass rate is measured over scenarios that actually ran (skips excluded).
  const nonSkipped = totalPassed + totalFailed;
  const overallRatio = nonSkipped > 0 ? totalPassed / nonSkipped : 0;

  lines.push(`## ${overallIcon} Evaluation results — ${overall}`);
  lines.push("");

  if (incompleteShards.length) {
    lines.push("> Some expected shards are missing or incomplete. The available results below are **not** a complete build result; no dashboard bundle will be uploaded.");
    lines.push("");
    for (const shard of incompleteShards) lines.push(`- **${shard.shard}**: ${shard.reason}`);
    lines.push("");
  }

  if (totalTests === 0) {
    lines.push(`No scenarios were found across ${shardCount} shard(s).`);
    lines.push("");
    lines.push("> ⚠️ No eval testcases were found in the downloaded results. This usually means the");
    lines.push("> eval shards did not publish JUnit — the shard jobs failed before running, or the");
    lines.push("> `eval-result-*` artifacts were empty. Check the Eval stage shard logs.");
    return lines.join("\n") + "\n";
  }

  // Glanceable one-liner.
  const scenarioWord = totalTests === 1 ? "scenario" : "scenarios";
  const shardWord = shardCount === 1 ? "shard" : "shards";
  const tallies = [`✅ **${totalPassed} passed**`];
  if (totalFailed > 0) {
    tallies.push(`❌ **${totalFailed} failed**`);
  }
  if (totalSkipped > 0) {
    tallies.push(`⏭️ ${totalSkipped} skipped`);
  }
  tallies.push(`**${formatPct(overallRatio)}** pass rate`);
  lines.push(
    `**${totalTests} ${scenarioWord}** across **${shardCount} ${shardWord}** — ${tallies.join(" · ")}`
  );
  lines.push("");

  // Red shards first (then alphabetical) so a reader's eye lands on failures.
  const ordered = [...all].sort((a, b) => {
    const aClean = a.failed === 0 ? 1 : 0;
    const bClean = b.failed === 0 ? 1 : 0;
    if (aClean !== bClean) {
      return aClean - bClean;
    }
    return a.shardName.localeCompare(b.shardName);
  });

  const metricRows = [];
  for (const shard of ordered) {
    const passed = shard.total - shard.failed - shard.skipped;
    const icon = shard.failed === 0 ? "✅" : "❌";
    const ran = passed + shard.failed;
    const shardPct = ran > 0 ? formatPct(passed / ran) : "—";
    metricRows.push([shard.shardName, icon, shardPct, passed, shard.failed, shard.skipped, shard.durationS.toFixed(1),
      usageCell(options.usageByShard?.[shard.shardName]), usageCell(options.usageByShard?.[shard.shardName], true)].map(tableText));
  }
  // Totals row.
  const totalIcon = totalFailed === 0 ? "✅" : "❌";
  let totalDuration = 0;
  for (const shard of all) {
    totalDuration += shard.durationS;
  }
  const totalUsage = all.every(shard => options.usageByShard?.[shard.shardName]) ? all.reduce((total, shard) => {
    const usage = options.usageByShard[shard.shardName];
    for (const key of ["trials", "tokenRecords", "tokens", "creditRecords", "credits"]) total[key] += usage[key];
    return total;
  }, { trials: 0, tokenRecords: 0, tokens: 0, creditRecords: 0, credits: 0 }) : null;
  lines.push(reportTable(["Shard", "Result", "Pass rate", "Passed", "Failed", "Skipped", "Time (s)", "Tokens", "AI credits (AIU)"], metricRows,
    { numeric: [2, 3, 4, 5, 6, 7, 8], footer: ["Total", totalIcon, formatPct(overallRatio), totalPassed, totalFailed, totalSkipped,
      totalDuration.toFixed(1), usageCell(totalUsage), usageCell(totalUsage, true)].map(tableText) }));
  lines.push("", "AI credits are recorded Copilot AIU, rounded to two decimals, not USD. Partial totals show recorded/selected trials.");

  if (totalFailed > 0) {
    lines.push("", "<br>", "", "---", "", "### Failing scenarios", "");
    const failureRows = [];
    for (const shard of ordered) {
      if (shard.failed === 0) continue;
      for (const failure of shard.failures) {
        const match = /^(.*) \((\d+)\/(\d+) runs passed\)$/.exec(failure);
        const scenario = match?.[1] ?? failure;
        const trials = match ? `${match[2]}/${match[3]}` : "Not recorded";
        const details = (options.failureDetails ?? []).filter(item => item.shard === shard.shardName &&
          item.scenario === scenario && item.grade?.passed === false);
        if (!details.length) {
          failureRows.push([`${tableText(scenario)}<br><small>${tableText(shard.shardName)}</small>`, "Check details unavailable", tableText(trials)]);
          continue;
        }
        const seen = new Set();
        for (const detail of details) {
          const grade = detail.grade;
          const url = failureLogUrl(options, detail.sourceLog, grade.name);
          const key = JSON.stringify([detail.model, grade.name, grade.evidence, url]);
          if (seen.has(key)) continue;
          seen.add(key);
          const linked = value => url ? `<a href="${tableText(url)}" target="_blank" rel="noopener noreferrer">${tableText(value)}</a>` : tableText(value);
          const evidence = String(grade.evidence ?? "");
          const reason = evidence.split(/\r?\n/).find(line => /not called:|disallowed.*called:/.test(line)) ?? evidence;
          failureRows.push([`${linked(scenario)}<br><small>${tableText(shard.shardName)} · ${tableText(detail.model)}</small>`,
            `${linked(grade.name)}${reason ? `<br>${tableText(reason)}` : ""}`, tableText(trials)]);
        }
      }
    }
    lines.push(reportTable(["Scenario", "Failed check", "Trials passed"], failureRows, { numeric: [2], widths: ["35%", "52%", "13%"] }),
      "", "Scenario verdicts use the configured threshold. Trial counts show passing executions.");
  }

  return lines.join("\n") + "\n";
}

export function formatDeliverySummary(summary, { skipReason = "", storageConfigured = false,
  archiveStatus = "not_started", importStatus = "not_attempted", dashboardBaseUrl = "", dashboardRefreshUrl = "",
  organization, project, definition, build, attempt } = {}) {
  const totals = summary?.totals;
  const verified = summary?.schemaVersion === 1 && typeof summary.complete === "boolean" && totals
    && [totals.scenarios, totals.failed, totals.skipped].every(value => Number.isSafeInteger(value) && value >= 0)
    && totals.failed + totals.skipped <= totals.scenarios;
  const complete = verified && summary.complete;
  const evaluations = !verified ? "unavailable; summary generation did not finish."
    : !complete ? "incomplete; check the affected shards in the evaluation summary."
    : totals.scenarios === totals.skipped ? "no scenarios ran."
    : totals.failed ? `completed; ${totals.failed} scenario(s) failed.` : "completed; all executed scenarios passed.";
  const reasons = { disabled: "publication was not requested", feature_branch: "feature-branch run",
    ineligible_run: "run is not eligible for publication", incomplete: "result data is incomplete or unverified",
    missing_configuration: "storage configuration is missing" };
  const reason = skipReason || (!complete ? "incomplete" : !storageConfigured ? "missing_configuration" : "");
  const stored = archiveStatus === "stored";
  const available = stored && importStatus === "confirmed";
  const artifactsOnly = !stored && archiveStatus !== "failed" && ["disabled", "feature_branch", "ineligible_run"].includes(reason);
  const status = available ? "Available in dashboard" : stored ? "Uploaded; dashboard import pending"
    : artifactsOnly ? "Artifacts only" : archiveStatus === "failed" ? "Delivery failed"
    : !complete ? "Result data not complete" : reason ? "Publishing not configured" : "Delivery unconfirmed";
  const message = available ? "Results for this build are available in the dashboard."
    : stored ? "Results were uploaded successfully. Dashboard import is not yet confirmed."
    : artifactsOnly ? `Results are retained in ADO; ${reasons[reason]}.`
    : archiveStatus === "failed" ? "Storage upload failed. Evaluation results remain in ADO artifacts."
    : reason ? `Results remain in ADO artifacts; ${reasons[reason] ?? reasons.ineligible_run}.`
    : "The publisher did not report a confirmed upload. Evaluation results remain in ADO artifacts.";
  const lines = stored && !available ? [message, ""] : [`**${status}**`, "", message, ""];
  const actionIndex = lines.length;
  const deliveryRows = [
    ["Result data", !verified ? "Not verified" : complete ? "Complete" : "Incomplete", `Evaluations: ${evaluations}`],
    ["ADO artifacts", "Retained", complete ? "JSONL, JUnit and evaluation summary" : "Available diagnostics; some result files may be missing"],
  ];
  if (!artifactsOnly) {
    const storageState = stored ? "Complete" : archiveStatus === "failed" ? "Failed" : reason ? "Skipped" : "Not confirmed";
    const storageDetail = stored ? "Upload or matching existing archive verified."
      : archiveStatus === "failed" ? "Upload failed; evaluation results remain in ADO artifacts."
      : reason ? `Publication skipped: ${reasons[reason] ?? reasons.ineligible_run}.`
      : "The publisher did not report a confirmed upload.";
    const notAttempted = !stored && (archiveStatus === "failed" || reason);
    const importState = available ? "Confirmed" : stored ? "Unconfirmed" : notAttempted ? "Not attempted" : "Not confirmed";
    const importDetail = available ? "Confirmed by the dashboard refresh response."
      : stored && importStatus === "failed" ? "The refresh request failed; import is not confirmed."
      : stored && importStatus === "not_configured" ? "Refresh configuration is missing or invalid; import is not confirmed."
      : notAttempted ? "Publication was skipped or upload failed."
      : "The publisher did not report a confirmed import.";
    deliveryRows.push(["Storage upload", storageState, storageDetail], ["Dashboard import", importState, importDetail]);
  }
  lines.push(reportTable(["Stage", "Status", "Details"], deliveryRows.map(row => row.map(tableText)), { widths: ["23%", "19%", "58%"] }), "");
  const validIdentity = /^[a-z0-9][a-z0-9-]{0,99}$/.test(organization ?? "") && typeof project === "string"
    && project.length > 0 && project.length <= 200 && !/[\x00-\x1f\x7f]/.test(project) && !/^\.+$/.test(project)
    && [definition, build, attempt].every(value => /^[1-9]\d{0,14}$/.test(String(value ?? "")));
  if (validIdentity) {
    const encodedProject = encodeURIComponent(project).replaceAll("(", "%28").replaceAll(")", "%29");
    const ado = new URL(`https://dev.azure.com/${organization}/${encodedProject}/_build/results`);
    ado.searchParams.set("buildId", build);
    try {
      const explicitBase = dashboardBaseUrl && !/^\$\([A-Za-z0-9_.-]+\)$/.test(dashboardBaseUrl);
      const base = new URL(explicitBase ? dashboardBaseUrl : dashboardRefreshUrl);
      if (stored && base.protocol === "https:" && base.port === "" && !base.username && !base.password
        && /^[a-z0-9][a-z0-9.-]*\.azurewebsites\.net$/.test(base.hostname)
        && base.pathname === (explicitBase ? "/" : "/api/refresh") && !base.search && !base.hash) {
        const dashboard = new URL("/build", base);
        dashboard.search = new URLSearchParams({ organization, project, definition, build, attempt }).toString();
        lines.splice(actionIndex, 0, `**[View this build in dashboard](${dashboard})**`, "");
      }
    } catch {}
    lines.push(`[Retained artifacts](${ado}&view=artifacts)`, "");
  }
  return lines.join("\n");
}

// ----- CLI -----

function parseArgs(argv) {
  const options = { outputPath: "eval-summary.md" };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const next = () => {
      const value = argv[++i];
      if (!value || value.startsWith("--")) throw new Error(`Missing value for ${arg}`);
      return value;
    };
    switch (arg) {
      case "--results-root":
        options.resultsRoot = next();
        break;
      case "--output-path":
        options.outputPath = next();
        break;
      case "--selected-root":
        options.selectedRoot = next();
        break;
      case "--attempts-file":
        options.attemptsFile = next();
        break;
      case "--delivery":
        options.delivery = true;
        break;
      default:
        throw new Error(`Unknown argument: ${arg}`);
    }
  }
  if (!options.resultsRoot && !options.delivery) {
    throw new Error("Missing required argument: --results-root");
  }
  return options;
}

async function main(argv) {
  const options = parseArgs(argv);
  fs.mkdirSync(path.dirname(path.resolve(options.outputPath)), { recursive: true });
  if (options.delivery) {
    let summary = null;
    try { summary = JSON.parse(fs.readFileSync(path.join(path.dirname(options.outputPath), "eval-summary.json"), "utf8")); }
    catch {}
    const markdown = formatDeliverySummary(summary, {
      skipReason: process.env.EVAL_PUBLICATION_SKIP_REASON,
      storageConfigured: process.env.EVAL_STORAGE_CONFIGURED?.toLowerCase() === "true",
      archiveStatus: process.env.EVAL_ARCHIVE_STATUS, importStatus: process.env.EVAL_IMPORT_STATUS,
      dashboardBaseUrl: process.env.EVAL_DASHBOARD_BASE_URL,
      dashboardRefreshUrl: process.env.EVAL_DASHBOARD_REFRESH_URL,
      organization: process.env.SYSTEM_COLLECTIONURI?.replace(/\/$/, "").split("/").at(-1),
      project: process.env.SYSTEM_TEAMPROJECT, definition: process.env.SYSTEM_DEFINITIONID,
      build: process.env.BUILD_BUILDID, attempt: process.env.SYSTEM_JOBATTEMPT,
    });
    fs.writeFileSync(options.outputPath, markdown, "utf8");
    console.log(markdown);
    if (process.env.TF_BUILD) console.log(`##vso[task.addattachment type=Distributedtask.Core.Summary;name=Result delivery;]${path.resolve(options.outputPath)}`);
    return;
  }
  fs.mkdirSync(options.resultsRoot, { recursive: true });
  let selection = null;
  if (options.selectedRoot) {
    let expectedShards = [];
    let matrixValid = true;
    try {
      expectedShards = expectedShardsFromMatrix(process.env.EVAL_EXPECTED_MATRIX);
    } catch {
      matrixValid = false;
    }
    // Even a failed Prepare must use an empty destination, not stale selected results.
    let jobAttempts = { schemaVersion: 1, valid: false, attempts: {} };
    if (matrixValid && options.attemptsFile) {
      try {
        const stat = fs.lstatSync(options.attemptsFile);
        if (!stat.isFile() || stat.size > 2 * 1024 * 1024) throw new Error("Invalid attempt evidence.");
        jobAttempts = JSON.parse(fs.readFileSync(options.attemptsFile, "utf8"));
      }
      catch {
        console.warn("##vso[task.logissue type=warning]Latest shard attempts could not be verified; Summary will remain incomplete and refuse publication.");
      }
    }
    selection = selectSummaryResults({ resultsRoot: options.resultsRoot,
      selectedRoot: options.selectedRoot, expectedShards, jobAttempts });
    if (!matrixValid) {
      selection.status.push({ shard: "Prepare", attempt: null, complete: false,
        reason: "The expected shard matrix is unavailable or invalid; check the Prepare stage." });
    }
  }
  const shards = getEvalSummary(options.selectedRoot ?? options.resultsRoot);
  if (selection) {
    for (const entry of selection.status) {
      if (entry.complete && !shards[entry.shard]?.total) {
        entry.complete = false;
        entry.reason = "The selected JUnit contains no evaluation testcases.";
      }
    }
    selection.complete = selection.status.every((entry) => entry.complete);
    selection.shardInput.complete = selection.complete;
    fs.writeFileSync(path.join(options.resultsRoot, "shard-index.json"), JSON.stringify(selection.shardInput, null, 2) + "\n");
  }
  const reportData = selection ? await selectedReportData(options.resultsRoot, selection) : {};
  const markdown = formatEvalSummaryMarkdown(shards, selection?.status.filter((entry) => !entry.complete) ?? [], {
    organization: process.env.SYSTEM_COLLECTIONURI?.replace(/\/$/, "").split("/").at(-1),
    project: process.env.SYSTEM_TEAMPROJECT, build: process.env.BUILD_BUILDID,
    ...reportData,
  });
  if (selection) {
    fs.writeFileSync(path.join(path.dirname(options.outputPath), "eval-summary.json"), JSON.stringify({
      schemaVersion: 1, complete: selection.complete, shards: selection.status,
      totals: Object.values(shards).reduce((totals, shard) => ({ scenarios: totals.scenarios + shard.total,
        failed: totals.failed + shard.failed, skipped: totals.skipped + shard.skipped }), { scenarios: 0, failed: 0, skipped: 0 }),
    }, null, 2) + "\n");
  }
  fs.writeFileSync(options.outputPath, markdown, "utf8");

  console.log(markdown);

  if (process.env.TF_BUILD) {
    console.log(`##vso[task.addattachment type=Distributedtask.Core.Summary;name=Evaluation results;]${path.resolve(options.outputPath)}`);
    if (selection) console.log(`##vso[task.setvariable variable=EvalSummaryComplete]${selection.complete}`);
  }

  // Completed evaluation failures remain governed by failOnFailedTests. Missing result
  // data is an infrastructure failure, reported after retaining a diagnostic summary.
  if (selection && !selection.complete) process.exitCode = 1;

  return shards;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    await main(process.argv.slice(2));
  } catch (error) {
    if (process.env.TF_BUILD && process.argv.includes("--selected-root")) {
      console.log("##vso[task.setvariable variable=EvalSummaryComplete]false");
    }
    console.error(error.message);
    process.exit(1);
  }
}
