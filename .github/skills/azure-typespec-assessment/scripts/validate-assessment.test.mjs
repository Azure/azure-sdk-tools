import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";

import { validateAssessment } from "./validate-assessment.mjs";

const sourceReference = {
  path: "spec/main.tsp",
  revision: "head",
  startLine: 2,
  endLine: 4,
  link: "spec/main.tsp#L2-L4",
};

function validDocument() {
  return {
    schemaVersion: 1,
    overallConfidence: "high",
    dimensions: {
      semanticUnderstanding: {
        items: [
          {
            id: "semantic-1",
            intent: "Add a default value.",
            transformationChain: [],
            restRepresentation: {
              summary: "Returns one widget.",
              operations: [
                {
                  operationId: "Widgets_Get",
                  apiVersions: ["2026-01-01"],
                  method: "GET",
                  path: "/widgets/{widgetName}",
                  signature: "GET /widgets/{widgetName}",
                  parameters: ["path widgetName: string, required"],
                  requestPayload: "none",
                  responsePayloads: [
                    "200 application/json payload: Widget",
                    "default application/json payload: ErrorResponse",
                  ],
                  serviceBehavior: "Returns one widget.",
                  lro: { isLongRunning: false },
                  paging: { isPaged: false },
                  sourceReferences: [sourceReference],
                },
              ],
            },
            confidence: "high",
            sourceReferences: [sourceReference],
          },
        ],
      },
      restBreakingChanges: { findings: [] },
      restCompatibleDownstreamBreakingChanges: { findings: [] },
      azureCompliance: { status: "not-assessed", reason: "Deferred from MVP." },
    },
  };
}

const markdown = `# TypeSpec Assessment

**Overall confidence:** high

## Semantic Understanding
## REST Breaking Changes
## REST-Compatible Downstream Breaking Changes
## Azure Compliance
## Assessment Errors
## Assessment Evidence
`;

test("valid assessment passes", () => {
  assert.deepEqual(validateAssessment(validDocument(), markdown), []);
});

test("findings require source references", () => {
  const document = validDocument();
  document.dimensions.restCompatibleDownstreamBreakingChanges.findings.push({
    id: "downstream-1",
    title: "Method moved",
    sourceReferences: [],
  });
  assert.match(
    validateAssessment(document, markdown).join("\n"),
    /requires sourceReferences/,
  );
});

test("semantic operations require complete REST behavior", () => {
  const document = validDocument();
  delete document.dimensions.semanticUnderstanding.items[0].restRepresentation
    .operations[0].responsePayloads;
  assert.match(
    validateAssessment(document, markdown).join("\n"),
    /responsePayloads is required/,
  );
});

test("compliance cannot be reported as passed", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "passed";
  assert.match(
    validateAssessment(document, markdown).join("\n"),
    /must be not-assessed/,
  );
});

test("overall confidence is required and must match Markdown", () => {
  const document = validDocument();
  document.overallConfidence = "low";
  assert.match(
    validateAssessment(document, markdown).join("\n"),
    /overall confidence must match/,
  );
});

test("eleven PR assessment outputs satisfy the JSON contract and match Markdown", () => {
  const evidence = JSON.parse(
    readFileSync(
      new URL("../test-evidence/assessments.json", import.meta.url),
      "utf8",
    ),
  );
  const summary = readFileSync(
    new URL("../test-evidence/assessment-summary.md", import.meta.url),
    "utf8",
  );
  assert.equal(evidence.assessments.length, 11);
  for (const assessment of evidence.assessments) {
    const jsonUrl = new URL(
      `../test-evidence/assessments/${assessment.pr}/assessment.json`,
      import.meta.url,
    );
    const markdownUrl = new URL(
      `../test-evidence/assessments/${assessment.pr}/assessment.md`,
      import.meta.url,
    );
    assert.ok(existsSync(jsonUrl));
    assert.ok(existsSync(markdownUrl));
    const standaloneAssessment = JSON.parse(readFileSync(jsonUrl, "utf8"));
    const standaloneMarkdown = readFileSync(markdownUrl, "utf8");
    assert.deepEqual(standaloneAssessment, assessment);
    assert.deepEqual(
      validateAssessment(standaloneAssessment, standaloneMarkdown),
      [],
    );
    assert.equal(assessment.dimensions.azureCompliance.status, "not-assessed");
    assert.match(assessment.overallConfidence, /^(high|medium|low)$/);
    assert.match(
      standaloneMarkdown,
      new RegExp(`\\*\\*Overall confidence:\\*\\* ${assessment.overallConfidence}`),
    );
    assert.equal(assessment.assessmentEvidence.emitterRuns.length >= 4, true);
    assert.match(standaloneMarkdown, /## Assessment Evidence/);
    assert.match(
      standaloneMarkdown,
      /@azure-tools\/typespec-client-generator-core/,
    );
    assert.match(
      summary,
      new RegExp(`assessments/${assessment.pr}/assessment\\.md`),
    );
    for (const item of assessment.dimensions.semanticUnderstanding.items) {
      assert.ok(item.sourceReferences.length > 0);
      assert.ok(item.restRepresentation.operations.length > 0);
      assert.ok(
        item.sourceReferences.every(
          (source) =>
            source.link.includes("/blob/") && source.link.includes("#L"),
        ),
      );
    }
  }
  assert.deepEqual(
    evidence.assessments.map((assessment) => assessment.pr),
    [
      43308,
      44742,
      43745,
      42853,
      44200,
      44454,
      44882,
      45536,
      42435,
      45348,
      44988,
    ],
  );
  const operations = evidence.assessments.flatMap(
    (assessment) =>
      assessment.dimensions.semanticUnderstanding.items[0].restRepresentation
        .operations,
  );
  assert.ok(operations.some((operation) => operation.lro.isLongRunning));
  assert.ok(operations.some((operation) => operation.paging.isPaged));
});
