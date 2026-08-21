import assert from "node:assert/strict";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { deriveCodeSafety, renderAssessment } from "./render-assessment.mjs";
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
      azureCompliance: {
        status: "passed",
        summary: {
          patternsAssessed: 1,
          findingCount: 0,
        },
        documents: [
          {
            title: "Models",
            url: "https://typespec.io/docs/language-basics/models/",
            section: "Model properties",
            guidanceExcerpt:
              "Models are collections of named properties and their types.",
            applicableGuidance: "Models define properties and defaults.",
            evidence: "The changed property uses the documented model syntax.",
            sourceReferences: [sourceReference],
          },
        ],
        findings: [],
      },
    },
  };
}

const markdown = renderAssessment(validDocument());

test("valid assessment passes", () => {
  assert.deepEqual(validateAssessment(validDocument(), markdown), []);
});

test("overall code safety reflects assessment risk", () => {
  const document = validDocument();
  assert.equal(deriveCodeSafety(document), "High");
  document.dimensions.azureCompliance.status = "not-assessed";
  assert.equal(deriveCodeSafety(document), "Medium");
  document.dimensions.restBreakingChanges.findings.push({
    id: "rest-high",
    title: "High severity REST break",
    severity: "high",
    confidence: "high",
    summary: "An existing wire contract is removed.",
    evidence: ["AutoRest diff"],
    sourceReferences: [sourceReference],
  });
  assert.equal(deriveCodeSafety(document), "Low");
});

test("action-required findings are ordered by severity", () => {
  const document = validDocument();
  document.dimensions.restBreakingChanges.findings.push({
    id: "rest-high",
    title: "High severity REST break",
    severity: "high",
    confidence: "high",
    summary: "An existing wire contract is removed.",
    evidence: ["AutoRest diff"],
    sourceReferences: [sourceReference],
  });
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.findings.push({
    id: "compliance-low",
    title: "Low severity compliance mismatch",
    severity: "low",
    summary: "A documented convention is not followed.",
    documentationUrl:
      "https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/",
    evidence: ["Fetched guidance", "Changed source"],
    sourceReferences: [sourceReference],
  });
  document.dimensions.azureCompliance.summary.findingCount = 1;
  const rendered = renderAssessment(document);
  assert.ok(
    rendered.indexOf("High severity REST break") <
      rendered.indexOf("Low severity compliance mismatch"),
  );
});

test("blocked assessments require resolving assessment errors", () => {
  const document = validDocument();
  document.errors = ["Head AutoRest compilation failed."];
  document.dimensions.restBreakingChanges.findings.push({
    id: "rest-blocked",
    title: "Known REST break",
    severity: "high",
    confidence: "high",
    summary: "A known REST break also requires action.",
    evidence: ["Source diff"],
    sourceReferences: [sourceReference],
  });
  const rendered = renderAssessment(document);
  assert.match(rendered, /Resolve the assessment blockers/);
  assert.match(rendered, /Known REST break/);
  assert.doesNotMatch(rendered, /No action required/);
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

test("breaking findings require the complete finding contract", () => {
  const document = validDocument();
  document.dimensions.restBreakingChanges.findings.push({
    id: "rest-incomplete",
    title: "Incomplete finding",
    confidence: "high",
    summary: "Severity is missing.",
    evidence: ["Source diff"],
    sourceReferences: [sourceReference],
  });
  assert.match(
    validateAssessment(document).join("\n"),
    /restBreakingChanges\.findings\[0\]\.severity is invalid/,
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

test("semantic items require every renderer input", () => {
  for (const field of ["id", "intent", "transformationChain", "confidence"]) {
    const document = validDocument();
    delete document.dimensions.semanticUnderstanding.items[0][field];
    assert.match(
      validateAssessment(document).join("\n"),
      new RegExp(
        `semanticUnderstanding\\.items\\[0\\]\\.${field} (?:is required|is invalid|must be)`,
      ),
    );
  }
  const document = validDocument();
  document.dimensions.semanticUnderstanding.items[0].transformationChain = [42];
  assert.match(
    validateAssessment(document).join("\n"),
    /transformationChain must be an array of non-empty strings/,
  );
});

test("failed compliance requires documented source-linked findings", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.findings.push({
    id: "compliance-standard-operation",
    title: "Operation does not use the documented template",
    severity: "medium",
    summary: "The operation duplicates a standard resource operation.",
    documentationUrl: "https://typespec.io/docs/language-basics/models/",
    evidence: ["Changed operation signature", "Fetched operation guidance"],
    sourceReferences: [sourceReference],
  });
  document.dimensions.azureCompliance.summary.findingCount = 1;
  assert.deepEqual(
    validateAssessment(document, renderAssessment(document)),
    [],
  );
});

test("compliance findings must cite a fetched document", () => {
  const document = validDocument();
  document.dimensions.azureCompliance.status = "failed";
  document.dimensions.azureCompliance.findings.push({
    id: "unsupported-guidance",
    title: "Finding without fetched guidance",
    severity: "medium",
    summary: "The finding cites a page that was not fetched.",
    documentationUrl: "https://typespec.io/docs/",
    evidence: ["Changed source"],
    sourceReferences: [sourceReference],
  });
  document.dimensions.azureCompliance.summary.findingCount = 1;
  assert.match(
    validateAssessment(document, renderAssessment(document)).join("\n"),
    /must match a fetched compliance document/,
  );
});

test("compliance documents require fetched guidance evidence", () => {
  const document = validDocument();
  delete document.dimensions.azureCompliance.documents[0].guidanceExcerpt;
  assert.match(
    validateAssessment(document, markdown).join("\n"),
    /guidanceExcerpt must be a short fetched-content excerpt/,
  );
});

test("not-assessed compliance requires a reason and empty findings", () => {
  const document = validDocument();
  document.dimensions.azureCompliance = {
    status: "not-assessed",
    reason: "Relevant authoritative documentation could not be retrieved.",
    documents: [],
    findings: [],
  };
  assert.deepEqual(
    validateAssessment(document, renderAssessment(document)),
    [],
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

test("stale Markdown is rejected", () => {
  const document = validDocument();
  const rendered = renderAssessment(document);
  document.dimensions.azureCompliance.documents[0].evidence =
    "The source changed after Markdown was rendered.";
  assert.match(
    validateAssessment(document, rendered).join("\n"),
    /must be generated from assessment.json/,
  );
});

test("generated fixture reports are reproducible in a clean checkout", () => {
  const output = mkdtempSync(join(tmpdir(), "typespec-assessment-reports-"));
  try {
    const result = spawnSync(
      process.execPath,
      [
        fileURLToPath(new URL("./generate-test-evidence.mjs", import.meta.url)),
        output,
      ],
      { encoding: "utf8" },
    );
    assert.equal(result.status, 0, result.stderr);
    const evidence = JSON.parse(
      readFileSync(join(output, "assessments.json"), "utf8"),
    );
    const summary = readFileSync(join(output, "assessment-summary.md"), "utf8");
    assert.equal(evidence.assessments.length, 10);
    for (const assessment of evidence.assessments) {
      const directory = join(output, "assessments", String(assessment.pr));
      const jsonPath = join(directory, "assessment.json");
      const markdownPath = join(directory, "assessment.md");
      assert.ok(existsSync(jsonPath));
      assert.ok(existsSync(markdownPath));
      const standalone = JSON.parse(readFileSync(jsonPath, "utf8"));
      const rendered = readFileSync(markdownPath, "utf8");
      assert.deepEqual(standalone, assessment);
      assert.deepEqual(validateAssessment(standalone, rendered), []);
      assert.match(
        summary,
        new RegExp(`assessments/${assessment.pr}/assessment\\.md`),
      );
    }
  } finally {
    rmSync(output, { recursive: true, force: true });
  }
});

test("PR 44988 compliance fixture retains both documented mismatches", () => {
  const fixture = JSON.parse(
    readFileSync(
      new URL("./fixtures/recent-pr-compliance.json", import.meta.url),
      "utf8",
    ),
  )["44988"];
  assert.equal(fixture.status, "failed");
  assert.equal(fixture.findings.length, 2);
  const operationFinding = fixture.findings.find(
    (finding) =>
      finding.id ===
      "compliance-connection-analyzer-standard-resource-operations",
  );
  assert.deepEqual(
    operationFinding.sourceReferences.map(({ startLine, endLine }) => [
      startLine,
      endLine,
    ]),
    [
      [41, 64],
      [470, 552],
    ],
  );
});

test("compliance fixtures select exact source evidence", () => {
  const fixture = JSON.parse(
    readFileSync(
      new URL("./fixtures/recent-pr-compliance.json", import.meta.url),
      "utf8",
    ),
  );
  for (const [pr, specification] of Object.entries(fixture)) {
    for (const [index, document] of specification.documents.entries()) {
      assert.ok(
        document.sourceReferences?.length > 0 ||
          document.semanticItemIds?.length > 0 ||
          document.sourcePathIncludes?.length > 0,
        `PR ${pr} compliance document ${index + 1} lacks an explicit source selection`,
      );
    }
    for (const [index, finding] of (specification.findings ?? []).entries()) {
      assert.ok(
        finding.sourceReferences?.length > 0 ||
          finding.semanticItemIds?.length > 0 ||
          finding.sourcePathIncludes?.length > 0,
        `PR ${pr} compliance finding ${index + 1} lacks an explicit source selection`,
      );
    }
  }
});

test("large reports summarize intents before preserving all operations", () => {
  const document = validDocument();
  const sourceItem = document.dimensions.semanticUnderstanding.items[0];
  const sourceOperation = sourceItem.restRepresentation.operations[0];
  const operationCounts = [7, 7, 7, 7, 7, 7, 7, 7, 6, 6];
  document.dimensions.semanticUnderstanding.items = operationCounts.map(
    (count, intentIndex) => ({
      ...structuredClone(sourceItem),
      id: `semantic-${intentIndex + 1}`,
      intent: `Intent ${intentIndex + 1}`,
      restRepresentation: {
        ...sourceItem.restRepresentation,
        operations: Array.from({ length: count }, (_, operationIndex) => ({
          ...structuredClone(sourceOperation),
          operationId: `Intent${intentIndex + 1}_Operation${operationIndex + 1}`,
        })),
      },
    }),
  );
  const rendered = renderAssessment(document);
  assert.match(
    rendered,
    /\*\*Scope:\*\* 10 intent\(s\), 68 affected operation\(s\)/,
  );
  assert.ok(
    rendered.indexOf("## 🧠 Semantic Understanding") <
      rendered.indexOf("### Change Overview"),
  );
  assert.deepEqual(validateAssessment(document, rendered), []);
});
