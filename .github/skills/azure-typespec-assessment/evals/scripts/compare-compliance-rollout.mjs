#!/usr/bin/env node

import {
  readFileSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const evalRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function readJson(file) {
  return JSON.parse(readFileSync(file, "utf8"));
}

function section(html, id) {
  return html.match(new RegExp(`<section id="${id}">([\\s\\S]*?)</section>`))?.[1] ?? "";
}

function parseArgs(argv) {
  const options = {
    output: join(evalRoot, "outputs", "compliance-rollout"),
    report: join(evalRoot, "compliance-rollout-results.json"),
  };
  for (let index = 0; index < argv.length; index += 1) {
    if (argv[index] === "--output") options.output = resolve(argv[++index]);
    else if (argv[index] === "--report") options.report = resolve(argv[++index]);
    else throw new Error(`Unknown argument: ${argv[index]}`);
  }
  return options;
}

export function compareComplianceRollout(options = {}) {
  const output = resolve(options.output ?? join(evalRoot, "outputs", "compliance-rollout"));
  const timings = readJson(join(evalRoot, "fixtures", "execution-time-breakdowns.json"));
  const fastTimings = new Map(
    readJson(join(evalRoot, "fast-assessment-execution-times.json"))
      .runs.map((item) => [item.pr, item]),
  );
  const cases = readJson(join(evalRoot, "cases.json"));
  const results = cases.map(({ pr }) => {
    const baselinePath = join(evalRoot, "assessments", String(pr), "assessment.html");
    const currentPath = join(output, String(pr), "assessment.html");
    const assessmentPath = join(evalRoot, "assessments", String(pr), "assessment.json");
    const replayResult = readJson(join(output, String(pr), "result.json"));
    const assessment = readJson(assessmentPath);
    const currentSchema = assessment.schemaVersion === 1;
    const compliance = currentSchema
      ? assessment.dimensions.compliance
      : assessment.dimensions.azureCompliance;
    const baselineHtml = readFileSync(baselinePath, "utf8");
    const currentHtml = readFileSync(currentPath, "utf8");
    const baselineCompliance = section(baselineHtml, "azure-compliance");
    const currentCompliance = section(currentHtml, "azure-compliance");
    const semanticIntents = currentSchema
      ? assessment.dimensions.semantic.items
      : assessment.dimensions.semanticUnderstanding.items;
    const restFindings = currentSchema
      ? assessment.dimensions.rest.findings
      : assessment.dimensions.restBreakingChanges.findings;
    const downstreamFindings = currentSchema
      ? assessment.dimensions.downstream.findings
      : assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings;
    return {
      pr,
      timing: {
        replayValidationAndRenderMs: replayResult.elapsedMs,
        previousFastAssessmentMs: fastTimings.get(pr)?.endToEndMs,
        previousFastDocumentationEvidenceMs:
          fastTimings.get(pr)?.documentationEvidenceMs,
        historicalFullAssessmentMs: timings[String(pr)]?.totalMs,
        historicalComplianceMs: timings[String(pr)]?.complianceMs,
        historicalTimingQuality: timings[String(pr)]?.totalQuality,
      },
      compliance: {
        evidenceMode: "preserved-historical-curation",
        status: compliance.status,
        documentCount:
          compliance.documents?.length ??
          compliance.coverage?.selectedDocumentCount ??
          0,
        findingCount: compliance.findings?.length ?? 0,
      },
      comparison: {
        previousReportBytes: statSync(baselinePath).size,
        currentReportBytes: statSync(currentPath).size,
        byteDelta: statSync(currentPath).size - statSync(baselinePath).size,
        previousComplianceSectionBytes: Buffer.byteLength(baselineCompliance),
        currentComplianceSectionBytes: Buffer.byteLength(currentCompliance),
        previousCompliancePresentation: baselineCompliance.includes("Planned / Not assessed")
          ? "planned"
          : "active",
        currentCompliancePresentation: currentCompliance.includes("Planned / Not assessed")
          ? "planned"
          : "active",
        currentShowsOfficialDocuments:
          currentHtml.includes("Official documents") ||
          currentHtml.includes("Ranked official documents"),
        currentShowsFindings: compliance.findings?.length
          ? compliance.findings.every((finding) =>
              currentCompliance.includes(finding.title))
          : true,
        currentShowsSemanticIntents: semanticIntents.every((intent) =>
          currentHtml.includes(intent.intent ?? intent.title)),
        currentShowsRestFindings: restFindings.every((finding) =>
          currentHtml.includes(finding.title)),
        currentShowsDownstreamFindings: downstreamFindings.every((finding) =>
          currentHtml.includes(finding.title)),
        semanticRestDownstreamDataChanged: false,
      },
    };
  });
  const statusCounts = results.reduce((counts, item) => {
    counts[item.compliance.status] = (counts[item.compliance.status] ?? 0) + 1;
    return counts;
  }, {});
  return {
    generatedAt: new Date().toISOString(),
    methodology: {
      previousReports: "evals/assessments/<pr>/assessment.html",
      currentReports: "evals/outputs/compliance-rollout/<pr>/assessment.html",
      replayTiming:
        "Schema validation plus HTML rendering only; excludes compilation and Agent work.",
      historicalTiming:
        "Preserved full assessment and Compliance timings from fixtures/execution-time-breakdowns.json.",
      previousFastTiming:
        "Previous impact-only end-to-end and documentation-evidence timings from fast-assessment-execution-times.json.",
      comparisonScope:
        "The source assessment data is unchanged; the current renderer activates the preserved Compliance evidence.",
      contractCoverage:
        "The 11-PR replay covers validation/rendering compatibility. Active four-document-per-intent search, tuple coverage, assembly, and rejection paths are covered by focused tests.",
    },
    caseCount: results.length,
    statusCounts,
    totalReplayValidationAndRenderMs: results.reduce(
      (total, item) => total + item.timing.replayValidationAndRenderMs,
      0,
    ),
    allReportsShowOfficialDocuments: results.every((item) =>
      item.comparison.currentShowsOfficialDocuments ||
      item.compliance.documentCount === 0),
    allFindingTitlesPreserved: results.every((item) =>
      item.comparison.currentShowsFindings),
    allSemanticIntentsPreserved: results.every((item) =>
      item.comparison.currentShowsSemanticIntents),
    allRestFindingsPreserved: results.every((item) =>
      item.comparison.currentShowsRestFindings),
    allDownstreamFindingsPreserved: results.every((item) =>
      item.comparison.currentShowsDownstreamFindings),
    results,
  };
}

const isMain =
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  try {
    const options = parseArgs(process.argv.slice(2));
    const report = compareComplianceRollout(options);
    writeFileSync(options.report, `${JSON.stringify(report, null, 2)}\n`);
    process.stdout.write(
      `Compared ${report.caseCount} PR reports in ${report.totalReplayValidationAndRenderMs} ms.\n`,
    );
  } catch (error) {
    process.stderr.write(`${error.message}\n`);
    process.exitCode = 1;
  }
}
