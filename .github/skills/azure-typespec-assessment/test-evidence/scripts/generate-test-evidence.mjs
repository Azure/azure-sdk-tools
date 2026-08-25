#!/usr/bin/env node

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { renderAssessmentHtml } from "../../scripts/render-assessment-html.mjs";
import { deriveOperationChanges } from "./operation-changes.mjs";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const evidenceRoot = process.argv[2]
  ? resolve(process.argv[2])
  : resolve(scriptDirectory, "..");
const assessmentRoot = resolve(evidenceRoot, "assessments");
const cases = JSON.parse(
  readFileSync(
    resolve(scriptDirectory, "..", "fixtures", "recent-pr-cases.json"),
  ),
);
const operationsByPr = JSON.parse(
  readFileSync(
    resolve(
      scriptDirectory,
      "..",
      "fixtures",
      "recent-pr-operations.json",
    ),
  ),
);
const typeSpecDiffsByPr = JSON.parse(
  readFileSync(
    resolve(
      scriptDirectory,
      "..",
      "fixtures",
      "recent-pr-typespec-diffs.json",
    ),
  ),
);
const historicalChangeOverrides = {
  "pr-42435-intent": {
    kind: "modified",
    aspects: [
      {
        field: "paging",
        before: "LRO result is exposed as a non-pageable response.",
        after:
          "LRO result is pageable with items `value` and continuation link `nextLink`.",
      },
    ],
    typeSpecCause:
      "Add `@list` to the operation and use the existing `@pageItems` and `@nextLink` response properties.",
  },
  "pr-42853-intent": {
    kind: "modified",
    aspects: [
      {
        field: "Go client location",
        before:
          "Affected methods follow the prior language exclusions and client placement.",
        after:
          "Go is added to the client-location exclusions, moving the four methods to a different generated Go client.",
      },
    ],
    typeSpecCause:
      "Extend the existing `@@clientLocation` customizations to exclude Go and preserve the released client placement.",
  },
  "pr-43308-intent": {
    kind: "modified",
    aspects: [
      {
        field: "TypeSpec LRO metadata",
        before:
          "The shared helpers represent final-state-via location with raw OpenAPI extensions.",
        after:
          'The shared helpers represent final-state-via location with @Azure.Core.useFinalStateVia("location").',
      },
    ],
  },
  "pr-43745-intent": {
    kind: "modified",
    aspects: [
      {
        field: "`sku.name` accepted values",
        before:
          "The closed enum accepts only `Standard_B1` and `Standard B10`.",
        after:
          "The open string union preserves both known values and also accepts future string values.",
      },
    ],
    typeSpecCause:
      "Replace the suppressed closed enum with an open string union while preserving both serialized values.",
  },
  "pr-44200-js-flattening": {
    kind: "modified",
    aspects: [
      {
        field: "JavaScript client shape",
        before: "AssociationUpdate properties are flattened.",
        after: "AssociationUpdate properties remain nested.",
      },
    ],
  },
  "pr-44742-intent": {
    kind: "removed",
    aspects: [
      {
        field: "2026-12-06 File Storage contracts",
        before:
          "NFSv2 file types, include values, response fields, and item models are present.",
        after: null,
      },
    ],
  },
  "pr-44882-intent": {
    kind: "modified",
    aspects: [
      {
        field: "Go delete parameter order",
        before:
          "The stable API version would expose the default generated parameter order.",
        after:
          "A Go-scoped replacement operation preserves the released parameter order.",
      },
    ],
    typeSpecCause:
      "Define a Go-scoped replacement `newRelicMonitorResourceDelete` operation with the released parameter order.",
  },
};

function withSources(value, sourceReferences) {
  return { ...value, sourceReferences };
}

function operationsFor(item, semanticItem) {
  const operationIds = semanticItem.operationIds
    ? new Set(semanticItem.operationIds)
    : undefined;
  return operationsByPr[item.pr]
    .filter(
      (operation) =>
        operationIds === undefined || operationIds.has(operation.operationId),
    )
    .map(({ request, responses, ...operation }) => ({
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
      sourceReferences: semanticItem.sourceReferences ?? item.sourceReferences,
    }));
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
  const allFindingIds = [
    ...item.restFindings,
    ...item.downstreamFindings,
    ...(item.compliance?.findings ?? []),
  ].map((finding) => finding.id);
  const semanticItems = item.semanticItems ?? [
    {
      id: `pr-${item.pr}-intent`,
      intent: item.intent,
      transformationChain: item.transformationChain,
      restRepresentation: item.restRepresentation,
      sourceReferences: item.sourceReferences,
    },
  ];
  return {
    schemaVersion: 2,
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
        items: semanticItems.map((semanticItem) => {
          const operations = operationsFor(item, semanticItem);
          const typeSpecDiffs =
            typeSpecDiffsByPr[item.pr]?.[semanticItem.id] ?? [];
          return {
            id: semanticItem.id,
            intent: semanticItem.intent,
            transformationChain: semanticItem.transformationChain,
            restRepresentation: {
              summary: semanticItem.restRepresentation,
              operations,
            },
            changes: deriveOperationChanges(
              {
                ...semanticItem,
                changes: historicalChangeOverrides[semanticItem.id]
                  ? [historicalChangeOverrides[semanticItem.id]]
                  : undefined,
                restRepresentation: {
                  summary: semanticItem.restRepresentation,
                },
              },
              operations,
              {
                typeSpecDiffs,
                linkedFindingIds:
                  semanticItem.linkedFindingIds ?? allFindingIds,
              },
            ),
            confidence: "high",
            sourceReferences:
              semanticItem.sourceReferences ?? item.sourceReferences,
          };
        }),
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
    resolve(outputDirectory, "assessment.html"),
    renderAssessmentHtml(assessment),
  );
}

writeFileSync(
  resolve(evidenceRoot, "assessments.json"),
  `${JSON.stringify(
    {
      schemaVersion: 2,
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
uses the skill's current HTML and JSON output contracts.

| PR | Case | State | Result | Assessment |
| --- | --- | --- | --- | --- |
${cases
  .map(
    (item) =>
      `| [#${item.pr}](${item.url}) | ${item.case} | ${item.state} | ${item.result} | [HTML](assessments/${item.pr}/assessment.html) · [JSON](assessments/${item.pr}/assessment.json) |`,
  )
  .join("\n")}
`,
);
