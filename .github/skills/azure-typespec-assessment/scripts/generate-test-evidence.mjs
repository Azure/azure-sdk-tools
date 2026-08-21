#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { renderAssessment } from "./render-assessment.mjs";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const skillRoot = resolve(scriptDirectory, "..");
const evidenceRoot = process.argv[2]
  ? resolve(process.argv[2])
  : resolve(skillRoot, "test-evidence");
const assessmentRoot = resolve(evidenceRoot, "assessments");
const cases = JSON.parse(
  readFileSync(resolve(scriptDirectory, "fixtures", "recent-pr-cases.json")),
);
const operationsByPr = JSON.parse(
  readFileSync(
    resolve(scriptDirectory, "fixtures", "recent-pr-operations.json"),
  ),
);

function withSources(value, sourceReferences) {
  return { ...value, sourceReferences };
}

function operationsFor(item) {
  return operationsByPr[item.pr].map(
    ({ request, responses, ...operation }) => ({
      ...operation,
      requestPayload: request,
      responsePayloads: responses,
      lro: operation.lro.isLongRunning
        ? {
            polling:
              "Poll the emitted async endpoint after Retry-After until a terminal state.",
            finalResult:
              "Use the final response contract described for this operation.",
            ...operation.lro,
          }
        : operation.lro,
      signature: `${operation.method} ${operation.path}`,
      sourceReferences: item.sourceReferences,
    }),
  );
}

function projectsFor(item) {
  return [
    ...new Set(
      item.sourceReferences.map((reference) =>
        reference.path.slice(0, reference.path.lastIndexOf("/")),
      ),
    ),
  ];
}

function emitterRunsFor(item) {
  const emitters = [
    {
      id: "autorest",
      name: "@azure-tools/typespec-autorest",
      output: "OpenAPI",
    },
    {
      id: "tcgc",
      name: "@azure-tools/typespec-client-generator-core",
      output: "SDK metadata (generic)",
    },
  ];

  return projectsFor(item).flatMap((project) =>
    ["baseline", "head"].flatMap((revision) =>
      emitters.map((emitter) => ({
        project,
        revision,
        emitter: emitter.name,
        emitterId: emitter.id,
        output: emitter.output,
        status: item.errors.some((error) =>
          error
            .toLowerCase()
            .includes(`${revision} ${emitter.id} compilation failed`),
        )
          ? "failed"
          : "succeeded",
        evidence: item.artifactEvidence[emitter.id],
      })),
    ),
  );
}

function buildAssessment(item) {
  return {
    schemaVersion: 1,
    overallConfidence: item.errors.length === 0 ? "high" : "low",
    pr: item.pr,
    title: item.title,
    url: item.url,
    state: item.state,
    createdAt: item.createdAt,
    baseline: { commit: item.baseCommit },
    head: { commit: item.headCommit },
    result: item.result,
    artifactEvidence: item.artifactEvidence,
    assessmentEvidence: {
      changedTypeSpec: item.sourceReferences,
      emitterRuns: emitterRunsFor(item),
    },
    dimensions: {
      semanticUnderstanding: {
        items: [
          {
            id: `pr-${item.pr}-intent`,
            intent: item.intent,
            transformationChain: item.transformationChain,
            restRepresentation: {
              summary: item.restRepresentation,
              operations: operationsFor(item),
            },
            confidence: "high",
            sourceReferences: item.sourceReferences,
          },
        ],
      },
      restBreakingChanges: {
        findings: item.restFindings.map((finding) =>
          withSources(finding, item.sourceReferences),
        ),
      },
      restCompatibleDownstreamBreakingChanges: {
        findings: item.downstreamFindings.map((finding) =>
          withSources(finding, item.sourceReferences),
        ),
      },
      azureCompliance: {
        status: "not-assessed",
        reason:
          "Historical fixture predates documentation-grounded compliance evidence.",
        documents: [],
        findings: [],
      },
    },
    errors: item.errors,
  };
}

mkdirSync(assessmentRoot, { recursive: true });
const assessments = cases.map(buildAssessment);

for (const assessment of assessments) {
  const outputDirectory = resolve(assessmentRoot, String(assessment.pr));
  mkdirSync(outputDirectory, { recursive: true });
  writeFileSync(
    resolve(outputDirectory, "assessment.json"),
    `${JSON.stringify(assessment, null, 2)}\n`,
  );
  writeFileSync(
    resolve(outputDirectory, "assessment.md"),
    renderAssessment(assessment),
  );
}

writeFileSync(
  resolve(evidenceRoot, "assessments.json"),
  `${JSON.stringify(
    {
      schemaVersion: 1,
      generatedAt: new Date().toISOString(),
      repository: "Azure/azure-rest-api-specs",
      assessments,
    },
    null,
    2,
  )}\n`,
);

writeFileSync(
  resolve(evidenceRoot, "assessment-summary.md"),
  `# TypeSpec Assessment Test Evidence

Ten representative TypeSpec PRs created within the previous year. Each result
uses the skill's current Markdown and JSON output contracts.

| PR | Case | State | Result | Assessment |
| --- | --- | --- | --- | --- |
${cases
  .map(
    (item) =>
      `| [#${item.pr}](${item.url}) | ${item.case} | ${item.state} | ${item.result} | [Markdown](assessments/${item.pr}/assessment.md) · [JSON](assessments/${item.pr}/assessment.json) |`,
  )
  .join("\n")}
`,
);
