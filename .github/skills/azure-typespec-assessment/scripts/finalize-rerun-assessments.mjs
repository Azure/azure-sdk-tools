#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { buildCompliance } from "./generate-document-compliance-evidence.mjs";
import { sourceLink } from "./prepare-assessment.mjs";
import { renderAssessment } from "./render-assessment.mjs";
import { validateAssessment } from "./validate-assessment.mjs";

const emitterNames = {
  autorest: "@azure-tools/typespec-autorest",
  tcgc: "@azure-tools/typespec-client-generator-core",
};

function readJson(path) {
  return JSON.parse(readFileSync(path, "utf8"));
}

function emitterRuns(evidence) {
  return evidence.projects.flatMap((project) =>
    project.compilations.flatMap((compilation) =>
      compilation.emitters.map((emitter) => ({
        project: project.path,
        revision: compilation.side,
        emitter: emitterNames[emitter.emitter] ?? emitter.emitter,
        emitterId: emitter.emitter,
        output:
          emitter.emitter === "autorest" ? "OpenAPI" : "SDK metadata (generic)",
        status: emitter.status,
        evidence: emitter.failureSummary ?? emitter.outputDirectory,
      })),
    ),
  );
}

function repositoryValidation(evidence) {
  return evidence.projects.map((project) => ({
    project: project.path,
    tool: project.validation.tool,
    status: project.validation.status,
    durationMs: project.validation.durationMs,
    log: project.validation.log ?? project.validation.reason,
    ...(project.validation.failureSummary
      ? { failureSummary: project.validation.failureSummary }
      : {}),
  }));
}

function formatDuration(milliseconds) {
  const seconds = Math.round(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes > 0 ? `${minutes}m ${seconds % 60}s` : `${seconds}s`;
}

function assessmentRemoteUrl(assessmentUrl) {
  if (!assessmentUrl) return undefined;
  const match = assessmentUrl.match(
    /^(https:\/\/github\.com\/[^/]+\/[^/]+)(?:\/|$)/,
  );
  return match?.[1];
}

export function normalizeAssessmentSourceLinks(assessment) {
  const remoteUrl = assessmentRemoteUrl(assessment.url);

  function visit(value) {
    if (Array.isArray(value)) {
      for (const item of value) visit(item);
      return;
    }
    if (!value || typeof value !== "object") return;
    if (
      typeof value.path === "string" &&
      value.path.endsWith(".tsp") &&
      ["base", "head"].includes(value.revision) &&
      Number.isInteger(value.startLine) &&
      Number.isInteger(value.endLine)
    ) {
      const commit =
        value.revision === "base"
          ? assessment.baseline?.commit
          : assessment.head?.commit;
      value.link = sourceLink(
        value.path,
        value.revision,
        commit,
        remoteUrl,
        value.startLine,
        value.endLine,
      );
    }
    for (const child of Object.values(value)) visit(child);
  }

  visit(assessment);
  return assessment;
}

function main() {
  const [reportRootValue, rerunRootValue, ...prValues] = process.argv.slice(2);
  if (!reportRootValue || !rerunRootValue || prValues.length === 0) {
    throw new Error(
      "Usage: finalize-rerun-assessments.mjs <report-root> <rerun-root> <pr>...",
    );
  }
  const reportRoot = resolve(reportRootValue);
  const rerunRoot = resolve(rerunRootValue);
  const complianceFixture = readJson(
    new URL("./fixtures/recent-pr-compliance.json", import.meta.url),
  );
  const assessments = [];

  for (const pr of prValues) {
    const outputDirectory = join(reportRoot, "assessments", pr);
    const assessment = readJson(join(outputDirectory, "assessment.json"));
    const evidence = readJson(join(rerunRoot, pr, "evidence.json"));
    const timing = readJson(join(rerunRoot, pr, "run-timing.json"));
    const documentation = readJson(
      join(rerunRoot, pr, "documentation-timing.json"),
    );
    const complianceSpecification = complianceFixture[pr];
    if (!complianceSpecification) {
      throw new Error(`Missing compliance fixture for PR ${pr}.`);
    }

    const incompleteValidation = evidence.projects.filter((project) =>
      ["skipped", "unavailable"].includes(project.validation.status),
    );
    if (incompleteValidation.length > 0) {
      throw new Error(
        `PR ${pr} cannot be finalized without repository validation: ${incompleteValidation
          .map((project) => `${project.path} (${project.validation.status})`)
          .join(", ")}`,
      );
    }
    assessment.overallConfidence = evidence.errors.length > 0 ? "low" : "high";
    assessment.baseline = evidence.baseline;
    assessment.head = evidence.head;
    assessment.projects = evidence.projects.map((project) => project.path);
    assessment.assessmentDuration = {
      toolchainSetupMs: timing.toolchainSetupMs,
      preparationMs: evidence.durationMs,
      documentationReviewMs: documentation.documentationAssessmentMs,
      ...(documentation.note ? { note: documentation.note } : {}),
      totalMs:
        timing.toolchainSetupMs +
        evidence.durationMs +
        (documentation.documentationAssessmentMs ?? 0),
    };
    assessment.assessmentEvidence = {
      changedTypeSpec: evidence.sourceReferences,
      emitterRuns: emitterRuns(evidence),
      repositoryValidation: repositoryValidation(evidence),
    };
    try {
      assessment.dimensions.azureCompliance = buildCompliance(
        assessment,
        complianceSpecification,
      );
    } catch (error) {
      throw new Error(`PR ${pr} compliance evidence failed: ${error.message}`, {
        cause: error,
      });
    }
    assessment.errors = evidence.errors;
    normalizeAssessmentSourceLinks(assessment);

    const markdown = renderAssessment(assessment);
    const errors = validateAssessment(assessment, markdown);
    if (errors.length > 0) {
      throw new Error(`PR ${pr} report is invalid:\n${errors.join("\n")}`);
    }
    mkdirSync(outputDirectory, { recursive: true });
    writeFileSync(
      join(outputDirectory, "assessment.json"),
      `${JSON.stringify(assessment, null, 2)}\n`,
    );
    writeFileSync(join(outputDirectory, "assessment.md"), markdown);
    assessments.push(assessment);
  }

  writeFileSync(
    join(reportRoot, "assessments.json"),
    `${JSON.stringify(
      {
        schemaVersion: 1,
        generatedAt: new Date().toISOString(),
        assessments,
      },
      null,
      2,
    )}\n`,
  );
  const rows = assessments.map((assessment) => {
    const operations = assessment.dimensions.semanticUnderstanding.items.reduce(
      (sum, item) => sum + item.restRepresentation.operations.length,
      0,
    );
    const duration = assessment.assessmentDuration;
    const totalTime =
      duration.documentationReviewMs === null
        ? "unavailable"
        : duration.note?.toLowerCase().includes("approximate")
          ? `~${formatDuration(duration.totalMs)}`
          : formatDuration(duration.totalMs);
    return `| [${assessment.pr}](assessments/${assessment.pr}/assessment.md) | ${assessment.overallConfidence} | ${operations} | ${assessment.dimensions.restBreakingChanges.findings.length} | ${assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings.length} | ${assessment.dimensions.azureCompliance.status} | ${totalTime} | ${assessment.errors.length} |`;
  });
  writeFileSync(
    join(reportRoot, "assessment-summary.md"),
    `# Live TypeSpec Assessment Evidence

All ${assessments.length} assessments were rerun from their recorded PR head and base revisions with exact lockfile dependencies plus base/head AutoRest and generic TCGC compilation. Repository-native TypeSpec Validation and documentation-grounded compliance assessment were both performed.

| PR | Confidence | Operations | REST findings | Downstream findings | Compliance | Total assessment | Errors |
| --- | --- | ---: | ---: | ---: | --- | ---: | ---: |
${rows.join("\n")}
`,
  );
  process.stdout.write(`Finalized ${assessments.length} assessment reports.\n`);
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  main();
}
