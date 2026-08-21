#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";

import { buildCompliance } from "./generate-document-compliance-evidence.mjs";
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

    const validationSkipped = evidence.projects.some(
      (project) => project.validation.status === "skipped",
    );
    assessment.overallConfidence =
      evidence.errors.length > 0
        ? "low"
        : validationSkipped
          ? "medium"
          : "high";
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
    const documentationTime =
      duration.documentationReviewMs === null
        ? "shared batch; unavailable"
        : formatDuration(duration.documentationReviewMs);
    return `| [${assessment.pr}](assessments/${assessment.pr}/assessment.md) | ${assessment.overallConfidence} | ${operations} | ${assessment.dimensions.restBreakingChanges.findings.length} | ${assessment.dimensions.restCompatibleDownstreamBreakingChanges.findings.length} | ${assessment.dimensions.azureCompliance.status} | ${formatDuration(duration.toolchainSetupMs)} | ${formatDuration(duration.preparationMs)} | ${documentationTime} | ${formatDuration(duration.totalMs)} | ${assessment.errors.length} |`;
  });
  const validationWasSkipped = assessments.every((assessment) =>
    assessment.assessmentEvidence.repositoryValidation.every(
      (validation) => validation.status === "skipped",
    ),
  );
  const validationDescription = validationWasSkipped
    ? "Repository-native TypeSpec Validation was explicitly skipped for this timing experiment; compliance was still assessed from freshly fetched authoritative documentation and exact changed TypeSpec."
    : "Repository-native TypeSpec Validation and documentation-grounded compliance assessment were both performed.";
  writeFileSync(
    join(reportRoot, "assessment-summary.md"),
    `# Live TypeSpec Assessment Evidence

All ${assessments.length} assessments were rerun from their recorded PR head and base revisions with exact lockfile dependencies plus base/head AutoRest and generic TCGC compilation. ${validationDescription}

| PR | Confidence | Operations | REST findings | Downstream findings | Compliance | Toolchain setup | Preparation | Documentation assessment | Total time | Errors |
| --- | --- | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: |
${rows.join("\n")}
`,
  );
  process.stdout.write(`Finalized ${assessments.length} assessment reports.\n`);
}

main();
